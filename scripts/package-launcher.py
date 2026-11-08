#!/usr/bin/env python3
"""Package the local launcher. This script never connects to a TV.

The default profile is an experimental, public Tizen development profile.
Acceptance on the target Samsung TV must be established by an install test.
Use --profile for a Samsung-issued profile when one is available.
"""

import argparse
import base64
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import urllib.parse
import xml.etree.ElementTree as ET
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
DS = "{http://www.w3.org/2000/09/xmldsig#}"
WIDGET = "{http://www.w3.org/ns/widgets}"
TIZEN = "{http://tizen.org/ns/widgets}"


def fail(message):
    raise SystemExit(message)


def validate_archive(path, signed):
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            fail("Package has duplicate archive entries")
        for name in names:
            if name.startswith("/") or ".." in pathlib.PurePosixPath(name).parts:
                fail("Unsafe package path: " + name)
        ET.fromstring(archive.read("config.xml"))
        checks = 0
        if signed:
            for signature in ("author-signature.xml", "signature1.xml"):
                root = ET.fromstring(archive.read(signature))
                for reference in root.findall("./" + DS + "SignedInfo/" + DS + "Reference"):
                    uri = reference.get("URI", "")
                    if not uri or uri.startswith("#"):
                        continue
                    if reference.find(DS + "Transforms") is not None:
                        fail("Unexpected file digest transform: " + uri)
                    file_path = urllib.parse.unquote(uri)
                    method = reference.find(DS + "DigestMethod").get("Algorithm")
                    algorithms = {
                        "http://www.w3.org/2001/04/xmlenc#sha256": "sha256",
                        "http://www.w3.org/2001/04/xmlenc#sha512": "sha512",
                        "http://www.w3.org/2000/09/xmldsig#sha1": "sha1",
                    }
                    if method not in algorithms:
                        fail("Unrecognized digest algorithm: " + method)
                    digest = hashlib.new(algorithms[method], archive.read(file_path)).digest()
                    recorded = base64.b64decode(reference.findtext(DS + "DigestValue"))
                    if digest != recorded:
                        fail("Signature file digest mismatch: " + file_path)
                    checks += 1
        return len(names), checks


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=pathlib.Path, default=ROOT / "launcher")
    parser.add_argument("--profile", default="SunsetLocalDev")
    parser.add_argument("--unsigned", action="store_true", help="Create a review archive without signatures")
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    source = args.source.resolve()
    try:
        config = ET.parse(source / "config.xml").getroot()
    except (ET.ParseError, OSError) as error:
        fail("Cannot read launcher config.xml: " + str(error))
    app = config.find(TIZEN + "application")
    content = config.find(WIDGET + "content")
    if app is None or not app.get("id") or not app.get("package"):
        fail("config.xml needs a Tizen application ID and package ID")
    if content is None or not (source / content.get("src", "")).is_file():
        fail("config.xml content entry does not exist")
    output = (args.output or ROOT / "toolchain/build" / (app.get("package") + ("-unsigned" if args.unsigned else "") + ".wgt")).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    stage = ROOT / "toolchain/build/stage"
    if stage.exists():
        shutil.rmtree(stage)
    stage.mkdir(parents=True)
    excluded_dirs = {"node_modules", ".git", ".buildResult", "tests", "test", "__pycache__"}
    excluded_names = {"author-signature.xml", "signature1.xml", "package-lock.json", "package.json"}
    source_hashes = {}
    for item in sorted(source.rglob("*")):
        rel = item.relative_to(source)
        if any(part in excluded_dirs or part.startswith(".") for part in rel.parts):
            continue
        if item.is_symlink():
            fail("Launcher symlinks are unsupported: " + str(rel))
        if item.name.startswith(("preview-", "screenshot-")):
            continue
        if not item.is_file() or item.name in excluded_names or item.suffix.lower() in {".md", ".wgt", ".py", ".sh"}:
            continue
        if item.stat().st_size > 100 * 1024 * 1024:
            fail("Launcher file exceeds 100 MiB: " + str(rel))
        target = stage / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(item, target)
        source_hashes[rel.as_posix()] = hashlib.sha256(target.read_bytes()).hexdigest()
    if args.unsigned:
        with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
            for item in sorted(stage.rglob("*")):
                if item.is_file():
                    archive.write(item, item.relative_to(stage).as_posix())
    else:
        cli = ROOT / "toolchain/sdk/tools/ide/bin/tizen"
        if not cli.is_file():
            fail("Missing local Tizen CLI. See toolchain/README.md")
        package_output = ROOT / "toolchain/build/package-output"
        if package_output.exists():
            shutil.rmtree(package_output)
        package_output.mkdir()
        result = subprocess.run([str(cli), "package", "-t", "wgt", "-s", args.profile, "-o", str(package_output), "--", str(stage)], capture_output=True, text=True)
        if result.returncode:
            fail("Tizen packaging failed:\n" + result.stdout + result.stderr)
        # The CLI chooses the widget display name as the filename.
        produced = list(package_output.glob("*.wgt"))
        if len(produced) != 1:
            fail("Tizen did not produce exactly one WGT:\n" + result.stdout)
        shutil.move(str(produced[0]), str(output))
        print(result.stdout.strip())
    entries, checks = validate_archive(output, not args.unsigned)
    report = {
        "package": str(output),
        "application_id": app.get("id"),
        "package_id": app.get("package"),
        "version": config.get("version"),
        "profile": None if args.unsigned else args.profile,
        "tv_install_tested": False,
        "sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
        "archive_entries": entries,
        "signature_file_digests_verified": checks,
        "source_sha256": source_hashes,
    }
    output.with_suffix(".build.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({k: v for k, v in report.items() if k != "source_sha256"}, indent=2))


if __name__ == "__main__":
    main()

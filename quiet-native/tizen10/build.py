#!/usr/bin/env python3
"""Build the standalone Tizen 10 app. No device connection or installation."""
import argparse
import base64
import hashlib
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import re
import shlex
import shutil
import subprocess
import tempfile
import urllib.request
import urllib.parse
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parent
NATIVE = ROOT.parent
WORKSPACE = NATIVE.parent
PACKAGES = {
    "api": ("tizen.net.api13", "13.0.0.19198", "9a390a5684607d040134f66c5d8e2da0bc233fe8b79c4148f3277eb3799b7137"),
    "signer": ("tizen.net.sdk", "1.1.9", "d065c111e5e6e1a91b3d6babb4685737f5e50c219ceb8ec9f28f2a25ba16d5a8"),
}


def extract_package(archive, destination):
    with zipfile.ZipFile(archive) as source:
        for item in source.infolist():
            path = PurePosixPath(item.filename)
            if path.is_absolute() or ".." in path.parts or "\\" in item.filename or ":" in item.filename:
                raise ValueError("Unsafe NuGet archive path")
            if (item.external_attr >> 16) & 0o170000 == 0o120000:
                raise ValueError("NuGet package symlinks are unsupported")
        source.extractall(destination)


def official_package(cache, kind):
    name, version, expected = PACKAGES[kind]
    cache.mkdir(parents=True, exist_ok=True)
    archive = cache / f"{name}-{version}.nupkg"
    if not archive.is_file() or hashlib.sha256(archive.read_bytes()).hexdigest() != expected:
        url = f"https://api.nuget.org/v3-flatcontainer/{name}/{version}/{name}.{version}.nupkg"
        temporary = archive.with_suffix(".download")
        try:
            with urllib.request.urlopen(url, timeout=60) as response, temporary.open("wb") as out:
                count = 0
                while True:
                    block = response.read(1024 * 1024)
                    if not block:
                        break
                    count += len(block)
                    if count > 64 * 1024 * 1024:
                        raise ValueError("Oversized NuGet download")
                    out.write(block)
            if hashlib.sha256(temporary.read_bytes()).hexdigest() != expected:
                raise ValueError("Official NuGet package checksum differs")
            temporary.replace(archive)
        finally:
            temporary.unlink(missing_ok=True)
    destination = cache / f"{name}-{version}"
    # Re-extract the verified archive rather than trusting modified cached DLLs.
    if destination.exists():
        shutil.rmtree(destination)
    extract_package(archive, destination)
    return destination


def asset_packager():
    spec = importlib.util.spec_from_file_location("quiet_asset_builder", NATIVE / "build.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def copy_public_files(source, destination):
    destination.mkdir(parents=True, exist_ok=True)
    if source.is_dir():
        for item in source.iterdir():
            if item.is_file() and (item.suffix.lower() in {".svg", ".txt"} or item.name in {"brand-provenance.json", "tv-icon-provenance.json"}):
                shutil.copy2(item, destination / item.name)


def package_resources(destination, media_dir=None, artwork_dirs=(), workspace=WORKSPACE, native=NATIVE):
    """Reuse the tested asset packager through a view with explicit private inputs."""
    module = asset_packager()
    upstream = workspace / "research/quiet-tizen"
    if not (upstream / "launcher/assets").is_dir():
        raise ValueError("Quiet assets missing; initialize the research/quiet-tizen submodule")
    with tempfile.TemporaryDirectory(prefix="quiet-public-assets-") as directory:
        view = Path(directory)
        viewed_upstream = view / "research/quiet-tizen"
        shutil.copytree(upstream / "launcher/assets", viewed_upstream / "launcher/assets", ignore=shutil.ignore_patterns(".*", "*.private.json", "*.local.json", "dusk-settings.json", "*.mp4"))
        shutil.copytree(upstream / "licenses", viewed_upstream / "licenses")
        for name in ("LICENSE", "NOTICE.md"):
            shutil.copy2(upstream / name, viewed_upstream / name)
        copy_public_files(workspace / "launcher/assets/brands", view / "launcher/assets/brands")
        copy_public_files(native / "assets", view / "native/assets")
        portrait = workspace / "launcher/assets/quiet/portrait-ps5.svg"
        if portrait.is_file():
            target = view / "launcher/assets/quiet/portrait-ps5.svg"
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(portrait, target)
        for source in artwork_dirs:
            if not source.is_dir():
                raise ValueError("Artwork directory does not exist")
            for item in source.iterdir():
                if item.is_file() and item.suffix.lower() in {".png", ".jpg", ".jpeg", ".svg"}:
                    shutil.copy2(item, view / "native/assets" / item.name)
        if media_dir:
            if not media_dir.is_dir():
                raise ValueError("Media directory does not exist")
            for name in module.AMBIENT_NAMES:
                for extension in ("mp4", "jpg"):
                    source = media_dir / f"snoopy-{name}.{extension}"
                    if source.is_file():
                        shutil.copy2(source, view / "launcher/assets" / source.name)
        # Deliberately never import any reports, settings seeds or app history.
        return module.package_assets(view, view / "native", destination, seed=None)


def profiles_file(explicit=None):
    if explicit:
        return explicit.expanduser().resolve()
    if os.environ.get("TIZEN_PROFILES"):
        return Path(os.environ["TIZEN_PROFILES"]).expanduser().resolve()
    cli = shutil.which("tizen")
    if cli:
        result = subprocess.run([cli, "cli-config", "-l"], capture_output=True, text=True, timeout=15)
        match = re.search(r"default\.profiles\.path\s*[=:]\s*(.+)", result.stdout)
        if match:
            return Path(match.group(1).strip()).expanduser().resolve()
    standard = Path.home() / "tizen-studio-data/profile/profiles.xml"
    if standard.is_file():
        return standard
    raise ValueError("SDK profile list not found; pass --profiles PATH or TIZEN_PROFILES")


def profile_certificates(filename, name):
    root = ET.parse(filename).getroot()
    profile = next((p for p in root.iter() if p.tag.split("}")[-1] == "profile" and p.get("name") == name), None)
    if profile is None:
        raise ValueError("Named signing profile was not found")
    result = {}
    for item in profile:
        value = item.get("key", "")
        if item.tag.split("}")[-1] != "profileitem" or not value:
            continue
        path = Path(os.path.expandvars(value)).expanduser()
        path = path.resolve() if path.is_absolute() else (filename.parent / path).resolve()
        if not path.is_file():
            raise ValueError("Signing profile refers to a missing certificate")
        result[item.get("distributor")] = path
    if not all(key in result for key in ("0", "1")):
        raise ValueError("Signing profile needs author and distributor certificates")
    return result


def signing_password(filename=None):
    if filename:
        text = filename.expanduser().read_text(encoding="utf-8-sig").strip()
        match = re.search(r"(?m)^\s*(?:export\s+)?TIZEN_CERTIFICATE_PASSWORD\s*=\s*(.*)$", text)
        if match:
            values = shlex.split(match.group(1), comments=False)
            value = values[0] if len(values) == 1 else ""
        else:
            value = text
    else:
        value = os.environ.get("TIZEN_CERTIFICATE_PASSWORD", "")
    if not value or "\0" in value:
        raise ValueError("Supply --password-file or TIZEN_CERTIFICATE_PASSWORD; SDK encrypted passwords are not plaintext")
    return value


def sign(dotnet, signer, stage, output, certificates, password, local):
    project = ET.Element("Project", {"xmlns": "http://schemas.microsoft.com/developer/msbuild/2003", "DefaultTargets": "SignHome"})
    ET.SubElement(project, "UsingTask", {"AssemblyFile": str(signer / "tools/netstandard1.6/Tizen.NET.Build.Tasks.dll"), "TaskName": "Tizen.NET.Build.Tasks.Sign"})
    target = ET.SubElement(project, "Target", {"Name": "SignHome"})
    attributes = {"RootDir": str(stage), "AuthorFile": str(certificates["0"]), "AuthorPassword": "$(AUTHOR_PASSWORD)", "DistFile": str(certificates["1"]), "DistPassword": "$(DISTRIBUTOR_PASSWORD)", "OutputSignedTpk": str(output)}
    if "2" in certificates:
        attributes.update(DistFile2=str(certificates["2"]), DistPassword2="$(DISTRIBUTOR2_PASSWORD)")
    ET.SubElement(target, "Sign", attributes)
    filename = local / "Sign.proj"
    ET.ElementTree(project).write(filename, encoding="utf-8", xml_declaration=True)
    environment = {**os.environ, "DOTNET_CLI_TELEMETRY_OPTOUT": "1"}
    environment.update(AUTHOR_PASSWORD=os.environ.get("TIZEN_AUTHOR_PASSWORD") or password, DISTRIBUTOR_PASSWORD=os.environ.get("TIZEN_DISTRIBUTOR_PASSWORD") or password, DISTRIBUTOR2_PASSWORD=os.environ.get("TIZEN_DISTRIBUTOR2_PASSWORD") or password)
    output.unlink(missing_ok=True)
    result = subprocess.run([dotnet, "msbuild", str(filename), "-nologo", "-verbosity:minimal"], capture_output=True, text=True, timeout=120, env=environment)
    message = result.stdout + result.stderr
    for secret in (password, environment["AUTHOR_PASSWORD"], environment["DISTRIBUTOR_PASSWORD"], environment["DISTRIBUTOR2_PASSWORD"]):
        message = message.replace(secret, "<redacted>")
    if result.returncode or not output.is_file():
        raise RuntimeError("Official Tizen package signing failed:\n" + message)


def verify_package(filename, signed):
    namespace = {"ds": "http://www.w3.org/2000/09/xmldsig#"}
    with zipfile.ZipFile(filename) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise ValueError("Package has duplicate archive entries")
        for name in names:
            if name.startswith("/") or ".." in PurePosixPath(name).parts:
                raise ValueError("Package has an unsafe archive path")
        ET.fromstring(archive.read("tizen-manifest.xml"))
        checked = 0
        if signed:
            for signature in ("author-signature.xml", "signature1.xml"):
                root = ET.fromstring(archive.read(signature))
                for reference in root.findall("./ds:SignedInfo/ds:Reference", namespace):
                    uri = reference.get("URI", "")
                    if not uri or uri.startswith("#"):
                        continue
                    algorithm = reference.find("ds:DigestMethod", namespace).get("Algorithm").split("#")[-1]
                    if algorithm not in ("sha256", "sha512"):
                        raise ValueError("Unexpected package digest algorithm")
                    expected = base64.b64decode(reference.findtext("ds:DigestValue", namespaces=namespace))
                    if hashlib.new(algorithm, archive.read(urllib.parse.unquote(uri))).digest() != expected:
                        raise ValueError("Package file digest mismatch")
                    checked += 1
        return len(names), checked


def build(args):
    dotnet = shutil.which(args.dotnet)
    if not dotnet:
        raise ValueError(".NET SDK not found; pass --dotnet PATH or set DOTNET")
    version = subprocess.check_output([dotnet, "--version"], text=True).strip()
    if int(version.split(".")[0]) < 8:
        raise ValueError("The Tizen 10 build requires the .NET 8 SDK or later")
    local = ROOT / ".local"
    cache = (args.package_cache or local / "packages").expanduser().resolve()
    local.mkdir(parents=True, exist_ok=True, mode=0o700)
    api = official_package(cache, "api")
    out, stage = local / "build", local / "stage"
    subprocess.run([dotnet, "build", str(ROOT / "QuietHome.csproj"), "-c", "Release", "--nologo", "-o", str(out), f"-p:TizenReferenceDir={api / 'ref/net6.0'}"], check=True, env={**os.environ, "DOTNET_CLI_TELEMETRY_OPTOUT": "1"})
    if stage.exists():
        shutil.rmtree(stage)
    (stage / "bin").mkdir(parents=True)
    for extension in ("dll", "pdb", "deps.json", "runtimeconfig.json"):
        shutil.copy2(out / f"BetterTizenHome.{extension}", stage / "bin")
    shutil.copy2(ROOT / "tizen-manifest.xml", stage / "tizen-manifest.xml")
    media = args.media_dir.expanduser().resolve() if args.media_dir else None
    artwork = [path.expanduser().resolve() for path in args.artwork_dir]
    media_count = package_resources(stage / "res/quiet-home", media, artwork)
    (stage / "shared/res").mkdir(parents=True)
    shutil.copy2(WORKSPACE / "launcher/assets/icon.png", stage / "shared/res/quiet.png")
    if args.diagnostics_config:
        config = json.loads(args.diagnostics_config.expanduser().read_text())
        if not isinstance(config, dict):
            raise ValueError("Diagnostics configuration must be a JSON object")
        private_config = stage / "res/config.json"
        private_config.write_text(json.dumps(config, indent=2) + "\n")
        private_config.chmod(0o600)
    manifest = ET.parse(ROOT / "tizen-manifest.xml").getroot()
    version = manifest.get("version")
    packages = local / "packages-out"
    packages.mkdir(exist_ok=True)
    output = packages / f"org.bettertizen.home-{version}{'-unsigned' if args.unsigned else ''}.tpk"
    if args.unsigned:
        with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
            for item in sorted(stage.rglob("*")):
                if item.is_file():
                    archive.write(item, item.relative_to(stage).as_posix())
    else:
        certificates = profile_certificates(profiles_file(args.profiles), args.profile)
        password = signing_password(args.password_file)
        signer = official_package(cache, "signer")
        sign(dotnet, signer, stage, output, certificates, password, local)
    output.chmod(0o600)
    entries, digests = verify_package(output, not args.unsigned)
    report = {"package": str(output), "sha256": hashlib.sha256(output.read_bytes()).hexdigest(), "archive_entries": entries, "signature_file_digests_verified": digests, "application_id": "org.bettertizen.home", "version": version, "signed": not args.unsigned, "installed": False, "diagnostics_config_included": args.diagnostics_config is not None, "media_count": media_count, "settings_seed_imported": False, "platform_assemblies_distributed": False, "official_reference_package": PACKAGES["api"][:2]}
    (local / "build.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=os.environ.get("DOTNET") or "dotnet", help=".NET 8 SDK executable; also accepts DOTNET/PATH")
    signing = parser.add_mutually_exclusive_group(required=True)
    signing.add_argument("--unsigned", action="store_true", help="Create an unsigned review TPK; not an installable Samsung package")
    signing.add_argument("--profile", help="Existing SDK signing profile name")
    parser.add_argument("--profiles", type=Path, help="SDK profiles.xml; otherwise TIZEN_PROFILES or standard SDK configuration")
    parser.add_argument("--password-file", type=Path, help="Explicit private plaintext or TIZEN_CERTIFICATE_PASSWORD=... file")
    parser.add_argument("--diagnostics-config", type=Path, help="Explicit private diagnostic/bootstrap JSON; omitted by default")
    parser.add_argument("--media-dir", type=Path, help="Explicit private Snoopy MP4/JPEG directory; omitted by default")
    parser.add_argument("--artwork-dir", type=Path, action="append", default=[], help="Explicit optional publisher artwork directory; repeatable")
    parser.add_argument("--package-cache", type=Path, help="Cache for checksum-verified official NuGet dependencies")
    args = parser.parse_args()
    try:
        build(args)
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError, ET.ParseError, zipfile.BadZipFile) as error:
        parser.exit(1, str(error) + "\n")


if __name__ == "__main__":
    main()

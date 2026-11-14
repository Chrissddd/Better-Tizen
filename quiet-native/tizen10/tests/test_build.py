import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

SPEC = importlib.util.spec_from_file_location("tizen10_build", Path(__file__).resolve().parents[1] / "build.py")
BUILD = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BUILD)


class BuildTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def fixture_assets(self):
        upstream = self.root / "research/quiet-tizen"
        (upstream / "launcher/assets").mkdir(parents=True)
        (upstream / "launcher/assets/.DS_Store").write_bytes(b"private metadata")
        (upstream / "licenses").mkdir()
        for name in ("LICENSE", "NOTICE.md"):
            (upstream / name).write_text("public test notice")
        brands = self.root / "launcher/assets/brands"
        brands.mkdir(parents=True)
        (brands / "youtube.svg").write_text("<svg/>")
        (brands / "private-publisher.png").write_bytes(b"private bitmap")
        native = self.root / "quiet-native"
        (native / "assets").mkdir(parents=True)
        (native / "assets/nuvio.png").write_bytes(b"private bitmap")
        (native / "assets/owner-settings.private.json").write_text('{"private":"history"}')
        (self.root / "launcher/assets/snoopy-space.mp4").write_bytes(b"private media")
        (self.root / "launcher/assets/snoopy-space.jpg").write_bytes(b"private poster")
        (self.root / "reports").mkdir()
        (self.root / "reports/dusk-settings.json").write_text('{"favoriteIds":["private"]}')
        return native

    def test_default_assets_exclude_private_media_bitmaps_and_history(self):
        native = self.fixture_assets()
        target = self.root / "output"
        count = BUILD.package_resources(target, workspace=self.root, native=native)
        self.assertEqual(count, 0)
        self.assertEqual(list(target.rglob("*.mp4")), [])
        self.assertFalse((target / ".DS_Store").exists())
        self.assertFalse((target / "brands/nuvio.png").exists())
        self.assertFalse((target / "brands/private-publisher.png").exists())
        self.assertFalse((target / "brands/owner-settings.private.json").exists())
        self.assertFalse((target / "dusk-settings.json").exists())
        apps = json.loads((target / "apps.json").read_text())
        self.assertEqual(next(a for a in apps if a["Name"] == "Nuvio")["Asset"], "")
        self.assertEqual(next(a for a in apps if a["Name"] == "YouTube")["Asset"], "brands/youtube.svg")

    def test_media_and_artwork_require_explicit_input(self):
        native = self.fixture_assets()
        target = self.root / "output"
        count = BUILD.package_resources(target, self.root / "launcher/assets", [native / "assets"], self.root, native)
        self.assertEqual(count, 1)
        self.assertTrue((target / "brands/nuvio.png").is_file())
        self.assertTrue((target / "ambient/snoopy-space.mp4").is_file())

    def test_bad_nuget_path_is_rejected(self):
        archive = self.root / "bad.zip"
        with zipfile.ZipFile(archive, "w") as out:
            out.writestr("../escaped", "bad")
        with self.assertRaisesRegex(ValueError, "Unsafe"):
            BUILD.extract_package(archive, self.root / "out")
        self.assertFalse((self.root / "escaped").exists())

    def test_explicit_password_formats_are_not_shell_evaluated(self):
        password = self.root / "password"
        password.write_text("plain password\n")
        self.assertEqual(BUILD.signing_password(password), "plain password")
        password.write_text("TIZEN_CERTIFICATE_PASSWORD='literal$(command)`text`'\n")
        self.assertEqual(BUILD.signing_password(password), "literal$(command)`text`")

    def test_named_profile_resolves_only_certificate_paths(self):
        (self.root / "author.p12").write_bytes(b"test")
        (self.root / "dist.p12").write_bytes(b"test")
        profiles = self.root / "profiles.xml"
        profiles.write_text('<profiles><profile name="Example"><profileitem distributor="0" key="author.p12" password="encrypted"/><profileitem distributor="1" key="dist.p12" password="encrypted"/></profile></profiles>')
        certificates = BUILD.profile_certificates(profiles, "Example")
        self.assertEqual(certificates["0"], (self.root / "author.p12").resolve())
        self.assertEqual(certificates["1"], (self.root / "dist.p12").resolve())


if __name__ == "__main__":
    unittest.main()

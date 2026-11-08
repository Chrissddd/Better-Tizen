"""Publication regressions: optional local inputs must not leak into clean builds."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


spec = importlib.util.spec_from_file_location("quiet_build", Path(__file__).resolve().parents[1] / "build.py")
build = importlib.util.module_from_spec(spec)
spec.loader.exec_module(build)


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.workspace = Path(self.temporary.name)
        self.native = self.workspace / "quiet-native"
        self.destination = self.native / ".local/package/quiet-home"
        upstream = self.workspace / "research/quiet-tizen"
        (upstream / "launcher/assets").mkdir(parents=True)
        (upstream / "launcher/assets/art.svg").write_text("upstream art")
        (upstream / "licenses").mkdir()
        (upstream / "licenses/OFL.txt").write_text("font license")
        for name in ("LICENSE", "NOTICE.md"):
            (upstream / name).write_text(name)

    def package(self, seed=None):
        return build.package_assets(self.workspace, self.native, self.destination, seed)

    def test_public_build_removes_previous_private_seed_and_never_reads_reports(self):
        self.package({"favoriteIds": ["private-owner-favorite"]})
        reports = self.workspace / "reports"
        reports.mkdir()
        (reports / "2026-11-08-dusk-saved-settings.txt").write_text("private history")
        self.package()
        self.assertFalse((self.destination / "dusk-settings.json").exists())
        self.assertNotIn("private", "".join(path.read_text() for path in self.destination.rglob("*") if path.is_file()))
        self.assertTrue((self.destination / "QUIET-LICENSE").is_file())
        self.assertTrue((self.destination / "licenses/OFL.txt").is_file())

    def test_absent_publisher_artwork_uses_text_and_existing_art_stays_referenced(self):
        brands = self.workspace / "launcher/assets/brands"
        brands.mkdir(parents=True)
        (brands / "youtube.svg").write_text("youtube vector")
        self.package()
        catalog = json.loads((self.destination / "apps.json").read_text())
        youtube = next(app for app in catalog if app["Name"] == "YouTube")
        self.assertEqual(youtube["Asset"], "brands/youtube.svg")
        nuvio = next(app for app in catalog if app["Name"] == "Nuvio")
        self.assertEqual(nuvio["Asset"], "")
        for app in catalog:
            if app["Asset"]:
                self.assertTrue((self.destination / app["Asset"]).is_file())

    def test_only_complete_media_pairs_survive_rebuild(self):
        media = self.workspace / "launcher/assets"
        media.mkdir(parents=True)
        (media / "snoopy-cards.mp4").write_bytes(b"movie")
        (media / "snoopy-cards.jpg").write_bytes(b"poster")
        (media / "snoopy-sleep.mp4").write_bytes(b"unpaired")
        self.assertEqual(self.package(), 1)
        self.assertEqual(sorted(path.name for path in (self.destination / "ambient").iterdir()),
                         ["snoopy-cards.jpg", "snoopy-cards.mp4"])
        (media / "snoopy-cards.jpg").unlink()
        self.assertEqual(self.package(), 0)
        self.assertFalse(any((self.destination / "ambient").iterdir()))

    def test_seed_import_requires_an_explicit_json_object(self):
        seed = self.workspace / "seed.json"
        for value in ({"favoriteMode": "manual"}, {"settings": {"favoriteMode": "manual"}}):
            seed.write_text(json.dumps(value))
            self.assertEqual(build.load_seed(seed), {"favoriteMode": "manual", "pinNuvio": True})
        for value in ([], {"settings": None}, "a report instead of JSON settings"):
            seed.write_text(json.dumps(value))
            with self.assertRaises(ValueError):
                build.load_seed(seed)


if __name__ == "__main__":
    unittest.main()

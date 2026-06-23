import tempfile
import types
import unittest
import zipfile
from pathlib import Path

from cvwindowsservice_publish import (
    CVWS_PACKAGE_RE,
    build_cvws_page_context,
    choose_target_filename,
    infer_version_from_filename,
    is_official_filename,
    save_cvws_package,
    update_cvws_latest_release,
    validate_version,
)


class RegexTests(unittest.TestCase):
    def test_official_zip_with_suffix(self):
        m = CVWS_PACKAGE_RE.match("CVWindowsService[4.0.6.522]-0522.zip")
        self.assertIsNotNone(m)
        self.assertEqual(m.group("version"), "4.0.6.522")
        self.assertEqual(m.group("suffix"), "0522")
        self.assertEqual(m.group("ext"), "zip")

    def test_official_zip_no_suffix(self):
        m = CVWS_PACKAGE_RE.match("CVWindowsService[1.8.0.1107].zip")
        self.assertIsNotNone(m)
        self.assertEqual(m.group("version"), "1.8.0.1107")
        self.assertIsNone(m.group("suffix"))
        self.assertEqual(m.group("ext"), "zip")

    def test_official_rar_with_suffix(self):
        m = CVWS_PACKAGE_RE.match("CVWindowsService[1.7.2.1022]-1022.rar")
        self.assertIsNotNone(m)
        self.assertEqual(m.group("version"), "1.7.2.1022")
        self.assertEqual(m.group("suffix"), "1022")
        self.assertEqual(m.group("ext"), "rar")

    def test_non_official_no_match(self):
        self.assertIsNone(CVWS_PACKAGE_RE.match("random-4.0.6.522.zip"))
        self.assertIsNone(CVWS_PACKAGE_RE.match("service.zip"))
        self.assertIsNone(CVWS_PACKAGE_RE.match("CVWindowsService[4.0.6].zip"))


class IsOfficialFilenameTests(unittest.TestCase):
    def test_official_zip(self):
        self.assertTrue(is_official_filename("CVWindowsService[4.0.6.522]-0522.zip"))

    def test_official_zip_no_suffix(self):
        self.assertTrue(is_official_filename("CVWindowsService[1.0.0.0].zip"))

    def test_rar_not_uploadable(self):
        self.assertFalse(is_official_filename("CVWindowsService[1.0.0.0].rar"))

    def test_non_official(self):
        self.assertFalse(is_official_filename("random-4.0.6.522.zip"))
        self.assertFalse(is_official_filename("service-1.0.0.0.zip"))


class ValidateVersionTests(unittest.TestCase):
    def test_valid_versions(self):
        for v in ["1.0.0.0", "4.0.3.318", "12.34.56.789"]:
            self.assertTrue(validate_version(v), f"Expected valid: {v}")

    def test_invalid_versions(self):
        for v in ["", "1.0.0", "1.0.0.0.0", "abc", "1.0.0.a", "v1.0.0.0", "1.0.0.0 "]:
            self.assertFalse(validate_version(v), f"Expected invalid: {v}")


class InferVersionTests(unittest.TestCase):
    def test_official_with_suffix(self):
        self.assertEqual(infer_version_from_filename("CVWindowsService[4.0.6.522]-0522.zip"), "4.0.6.522")

    def test_official_no_suffix(self):
        self.assertEqual(infer_version_from_filename("CVWindowsService[1.0.0.0].zip"), "1.0.0.0")

    def test_non_official_returns_none(self):
        self.assertIsNone(infer_version_from_filename("random-4.0.6.522.zip"))

    def test_rar_returns_none(self):
        self.assertIsNone(infer_version_from_filename("CVWindowsService[1.0.0.0].rar"))

    def test_no_version(self):
        self.assertIsNone(infer_version_from_filename("random.zip"))


class ChooseTargetFilenameTests(unittest.TestCase):
    def test_no_conflict(self):
        with tempfile.TemporaryDirectory() as td:
            target = Path(td)
            result = choose_target_filename("1.0.0.0", target)
            self.assertEqual(result, "CVWindowsService[1.0.0.0].zip")

    def test_preserves_official_filename(self):
        with tempfile.TemporaryDirectory() as td:
            target = Path(td)
            result = choose_target_filename(
                "4.0.6.522", target,
                original_filename="CVWindowsService[4.0.6.522]-0522.zip",
            )
            self.assertEqual(result, "CVWindowsService[4.0.6.522]-0522.zip")

    def test_official_filename_conflict_generates_suffix(self):
        with tempfile.TemporaryDirectory() as td:
            target = Path(td)
            (target / "CVWindowsService[4.0.6.522]-0522.zip").write_bytes(b"a")
            result = choose_target_filename(
                "4.0.6.522", target,
                original_filename="CVWindowsService[4.0.6.522]-0522.zip",
            )
            # Original conflicts; falls back to canonical with next suffix after max existing (0522)
            self.assertEqual(result, "CVWindowsService[4.0.6.522]-523.zip")

    def test_conflict_with_base(self):
        with tempfile.TemporaryDirectory() as td:
            target = Path(td)
            (target / "CVWindowsService[1.0.0.0].zip").write_bytes(b"a")
            result = choose_target_filename("1.0.0.0", target)
            self.assertEqual(result, "CVWindowsService[1.0.0.0]-1.zip")

    def test_conflict_with_existing_suffix(self):
        with tempfile.TemporaryDirectory() as td:
            target = Path(td)
            (target / "CVWindowsService[1.0.0.0].zip").write_bytes(b"a")
            (target / "CVWindowsService[1.0.0.0]-1.zip").write_bytes(b"b")
            result = choose_target_filename("1.0.0.0", target)
            self.assertEqual(result, "CVWindowsService[1.0.0.0]-2.zip")

    def test_conflict_only_with_suffix(self):
        with tempfile.TemporaryDirectory() as td:
            target = Path(td)
            (target / "CVWindowsService[1.0.0.0]-3.zip").write_bytes(b"a")
            result = choose_target_filename("1.0.0.0", target)
            self.assertEqual(result, "CVWindowsService[1.0.0.0]-4.zip")

    def test_different_version_no_conflict(self):
        with tempfile.TemporaryDirectory() as td:
            target = Path(td)
            (target / "CVWindowsService[1.0.0.0].zip").write_bytes(b"a")
            result = choose_target_filename("2.0.0.0", target)
            self.assertEqual(result, "CVWindowsService[2.0.0.0].zip")


class SaveCvwsPackageTests(unittest.TestCase):
    def _make_file_storage(self, filename: str, content: bytes = b"PKzip"):
        return types.SimpleNamespace(
            filename=filename,
            save=lambda path: Path(path).write_bytes(content),
        )

    def test_save_creates_file(self):
        with tempfile.TemporaryDirectory() as td:
            target = Path(td)
            fs = self._make_file_storage("test.zip")
            result = save_cvws_package(fs, target, "1.0.0.0")
            self.assertEqual(result.saved_filename, "CVWindowsService[1.0.0.0].zip")
            self.assertTrue((target / result.saved_filename).exists())
            self.assertEqual(result.version, "1.0.0.0")

    def test_save_preserves_official_name(self):
        with tempfile.TemporaryDirectory() as td:
            target = Path(td)
            fs = self._make_file_storage("CVWindowsService[4.0.6.522]-0522.zip")
            result = save_cvws_package(
                fs, target, "4.0.6.522",
                original_filename="CVWindowsService[4.0.6.522]-0522.zip",
            )
            self.assertEqual(result.saved_filename, "CVWindowsService[4.0.6.522]-0522.zip")

    def test_save_reads_existing_latest(self):
        with tempfile.TemporaryDirectory() as td:
            target = Path(td)
            (target / "LATEST_RELEASE").write_text("2.0.0.0", encoding="utf-8")
            fs = self._make_file_storage("test.zip")
            result = save_cvws_package(fs, target, "1.0.0.0")
            self.assertEqual(result.latest_version, "2.0.0.0")

    def test_save_no_latest(self):
        with tempfile.TemporaryDirectory() as td:
            target = Path(td)
            fs = self._make_file_storage("test.zip")
            result = save_cvws_package(fs, target, "1.0.0.0")
            self.assertEqual(result.latest_version, "")


class UpdateLatestReleaseTests(unittest.TestCase):
    def test_writes_version(self):
        with tempfile.TemporaryDirectory() as td:
            target = Path(td)
            update_cvws_latest_release(target, "1.2.3.4")
            self.assertEqual((target / "LATEST_RELEASE").read_text(encoding="utf-8"), "1.2.3.4")

    def test_overwrites_existing(self):
        with tempfile.TemporaryDirectory() as td:
            target = Path(td)
            (target / "LATEST_RELEASE").write_text("0.0.0.0", encoding="utf-8")
            update_cvws_latest_release(target, "5.0.0.0")
            self.assertEqual((target / "LATEST_RELEASE").read_text(encoding="utf-8"), "5.0.0.0")


class BuildCvwsPageContextTests(unittest.TestCase):
    def test_empty_directory(self):
        with tempfile.TemporaryDirectory() as td:
            storage = Path(td)
            ctx = build_cvws_page_context(
                storage,
                scan_packages=lambda: [],
                read_text_file=lambda p: None,
                human_size=lambda s: f"{s}B",
            )
            self.assertEqual(ctx["latest_version"], "")
            self.assertEqual(ctx["package_count"], 0)

    def test_with_packages(self):
        pkgs = [{"fileName": "test.zip", "version": "1.0.0.0"}]
        with tempfile.TemporaryDirectory() as td:
            storage = Path(td)
            (storage / "Tool" / "CVWindowsService").mkdir(parents=True)
            (storage / "Tool" / "CVWindowsService" / "LATEST_RELEASE").write_text("1.0.0.0")
            ctx = build_cvws_page_context(
                storage,
                scan_packages=lambda: pkgs,
                read_text_file=lambda p: "1.0.0.0" if p.name == "LATEST_RELEASE" else None,
                human_size=lambda s: f"{s}B",
            )
            self.assertEqual(ctx["latest_version"], "1.0.0.0")
            self.assertEqual(ctx["package_count"], 1)


if __name__ == "__main__":
    unittest.main()

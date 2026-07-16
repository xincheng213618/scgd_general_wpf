import tempfile
import unittest
import zipfile
from pathlib import Path

from Scripts.build_update import (
    REQUIRED_SERVICE_HOST_RUNTIME_PATHS,
    find_incremental_baseline,
    make_incremental_zip,
    validate_service_host_runtime,
)


class IncrementalBaselineTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="build-update-tests-")
        self.history_directory = Path(self._temp_directory.name)

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    def test_revision_update_uses_first_package_in_current_build(self) -> None:
        self._create_release("1.4.9.8")
        self._create_release("1.4.9.1")
        self._create_release("1.4.8.1")

        baseline = find_incremental_baseline(self.history_directory, "1.4.9.14")

        self.assertEqual("ColorVision-[1.4.9.1].zip", Path(baseline).name)

    def test_first_revision_in_new_build_uses_first_package_in_previous_build(self) -> None:
        self._create_release("1.4.8.11")
        self._create_release("1.4.8.1")
        self._create_release("1.4.7.1")

        baseline = find_incremental_baseline(self.history_directory, "1.4.9.1")

        self.assertEqual("ColorVision-[1.4.8.1].zip", Path(baseline).name)

    def test_other_major_minor_series_is_not_used_as_baseline(self) -> None:
        self._create_release("1.4.9.1")

        baseline = find_incremental_baseline(self.history_directory, "1.5.1.1")

        self.assertIsNone(baseline)

    def test_fallback_uses_oldest_available_version_in_same_series(self) -> None:
        self._create_release("1.4.7.11")
        self._create_release("1.4.6.5")

        baseline = find_incremental_baseline(self.history_directory, "1.4.9.1")

        self.assertEqual("ColorVision-[1.4.6.5].zip", Path(baseline).name)

    def _create_release(self, version: str) -> None:
        (self.history_directory / f"ColorVision-[{version}].zip").write_bytes(b"release")


class IncrementalServiceHostPackageTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="build-update-service-host-tests-")
        self.root = Path(self._temp_directory.name)
        self.old_directory = self.root / "old"
        self.new_directory = self.root / "new"
        self.old_directory.mkdir()
        self.new_directory.mkdir()

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    def test_incremental_package_always_contains_complete_service_host_runtime(self) -> None:
        for relative_path in REQUIRED_SERVICE_HOST_RUNTIME_PATHS:
            self._write_file(self.old_directory / relative_path, b"same-runtime")
            self._write_file(self.new_directory / relative_path, b"same-runtime")
        self._write_file(self.old_directory / "unchanged.dll", b"same")
        self._write_file(self.new_directory / "unchanged.dll", b"same")

        old_zip = self.root / "old.zip"
        with zipfile.ZipFile(old_zip, "w", zipfile.ZIP_DEFLATED) as archive:
            for path in self.old_directory.rglob("*"):
                if path.is_file():
                    archive.write(path, path.relative_to(self.old_directory))

        incremental_zip = self.root / "incremental.cvx"
        make_incremental_zip(old_zip, self.new_directory, incremental_zip)

        with zipfile.ZipFile(incremental_zip, "r") as archive:
            names = {name.replace("\\", "/") for name in archive.namelist()}

        self.assertTrue(set(REQUIRED_SERVICE_HOST_RUNTIME_PATHS).issubset(names))
        self.assertNotIn("unchanged.dll", names)

    def test_runtime_validation_rejects_incomplete_service_host(self) -> None:
        self._write_file(self.new_directory / "ServiceHost/ColorVisionServiceHost.exe", b"host")

        with self.assertRaisesRegex(FileNotFoundError, "ServiceHost runtime is incomplete"):
            validate_service_host_runtime(self.new_directory)

    @staticmethod
    def _write_file(path: Path, content: bytes) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)


if __name__ == "__main__":
    unittest.main()

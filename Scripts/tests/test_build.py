import tempfile
import unittest
from pathlib import Path

from Scripts.build import validate_installer_runtime_dlls, validate_runtime_copy_integrity
from Scripts.service_host_runtime import REQUIRED_SERVICE_HOST_RUNTIME_PATHS


class InstallerRuntimeValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="build-installer-tests-")
        self.root = Path(self._temp_directory.name)
        self.runtime_directory = self.root / "runtime"
        self.runtime_directory.mkdir()
        (self.runtime_directory / "ColorVision.UI.dll").write_bytes(b"runtime")
        for relative_path in REQUIRED_SERVICE_HOST_RUNTIME_PATHS:
            path = self.runtime_directory / relative_path
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"service-host")

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    def test_accepts_complete_runtime_and_installer_mapping(self) -> None:
        aip_path = self._write_aip(REQUIRED_SERVICE_HOST_RUNTIME_PATHS)

        self.assertTrue(validate_installer_runtime_dlls(self.runtime_directory, aip_path, report=lambda _: None))

    def test_rejects_service_host_file_missing_from_installer_mapping(self) -> None:
        aip_path = self._write_aip(REQUIRED_SERVICE_HOST_RUNTIME_PATHS[:-1])

        self.assertFalse(validate_installer_runtime_dlls(self.runtime_directory, aip_path, report=lambda _: None))

    def test_rejects_incomplete_service_host_build_output(self) -> None:
        (self.runtime_directory / REQUIRED_SERVICE_HOST_RUNTIME_PATHS[0]).unlink()
        aip_path = self._write_aip(REQUIRED_SERVICE_HOST_RUNTIME_PATHS)

        self.assertFalse(validate_installer_runtime_dlls(self.runtime_directory, aip_path, report=lambda _: None))

    def test_runtime_copy_integrity_rejects_a_mismatched_dll(self) -> None:
        solution_root = self.root / "source"
        project_output = solution_root / "Module" / "bin" / "Module.dll"
        project_output.parent.mkdir(parents=True)
        project_output.write_bytes(b"valid module")
        runtime_output = self.runtime_directory / "Module.dll"
        runtime_output.write_bytes(b"corrupt module")

        self.assertFalse(validate_runtime_copy_integrity(
            solution_root,
            self.runtime_directory,
            project_outputs=(("Module/bin/Module.dll", "Module.dll"),),
            report=lambda _: None,
        ))

    def test_runtime_copy_integrity_accepts_an_exact_copy(self) -> None:
        solution_root = self.root / "source"
        project_output = solution_root / "Module" / "bin" / "Module.dll"
        project_output.parent.mkdir(parents=True)
        project_output.write_bytes(b"valid module")
        runtime_output = self.runtime_directory / "Module.dll"
        runtime_output.write_bytes(project_output.read_bytes())

        self.assertTrue(validate_runtime_copy_integrity(
            solution_root,
            self.runtime_directory,
            project_outputs=(("Module/bin/Module.dll", "Module.dll"),),
            report=lambda _: None,
        ))

    def _write_aip(self, service_host_paths: tuple[str, ...]) -> Path:
        source_paths = ["C:\\build\\ColorVision.UI.dll", *[f"C:\\build\\{path}" for path in service_host_paths]]
        rows = "".join(f'<ROW SourcePath="{path}" />' for path in source_paths)
        aip_path = self.root / "ColorVision.aip"
        aip_path.write_text(f"<DOCUMENT>{rows}</DOCUMENT>", encoding="utf-8")
        return aip_path


if __name__ == "__main__":
    unittest.main()

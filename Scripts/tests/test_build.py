import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from Scripts.build import (
    RemoteUploadSettings,
    ensure_runtime_copy_integrity,
    get_installer_for_version,
    publish_primary_release,
    validate_installer_runtime_dlls,
    validate_runtime_copy_integrity,
    validate_shared_files_manifests,
)
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

    def test_rejects_native_runtime_dll_missing_from_installer_mapping(self) -> None:
        native_relative_path = "runtimes/win-x64/native/opencv_core4140.dll"
        native_path = self.runtime_directory / native_relative_path
        native_path.parent.mkdir(parents=True)
        native_path.write_bytes(b"opencv")
        aip_path = self._write_aip(REQUIRED_SERVICE_HOST_RUNTIME_PATHS)

        self.assertFalse(validate_installer_runtime_dlls(self.runtime_directory, aip_path, report=lambda _: None))

    def test_accepts_native_runtime_dll_in_installer_mapping(self) -> None:
        native_relative_path = "runtimes/win-x64/native/opencv_core4140.dll"
        native_path = self.runtime_directory / native_relative_path
        native_path.parent.mkdir(parents=True)
        native_path.write_bytes(b"opencv")
        aip_path = self._write_aip(REQUIRED_SERVICE_HOST_RUNTIME_PATHS, (native_relative_path,))

        self.assertTrue(validate_installer_runtime_dlls(self.runtime_directory, aip_path, report=lambda _: None))

    def test_shared_files_release_gate_accepts_matching_set(self) -> None:
        manifest_path = self.root / "shared_files.json"
        shared_files = sorted(
            path.relative_to(self.runtime_directory).as_posix()
            for path in self.runtime_directory.rglob("*")
            if path.is_file()
        )
        manifest_path.write_text(json.dumps({
            "generated_at": "2000-01-01T00:00:00+00:00",
            "shared_files": list(reversed(shared_files)),
        }), encoding="utf-8")

        self.assertTrue(validate_shared_files_manifests(
            self.runtime_directory,
            manifest_paths=(manifest_path,),
            report=lambda _: None,
        ))

    def test_shared_files_release_gate_rejects_drift(self) -> None:
        manifest_path = self.root / "shared_files.json"
        manifest_path.write_text(json.dumps({"shared_files": ["Old.dll"]}), encoding="utf-8")

        self.assertFalse(validate_shared_files_manifests(
            self.runtime_directory,
            manifest_paths=(manifest_path,),
            report=lambda _: None,
        ))

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

    def test_runtime_copy_integrity_repairs_a_mismatched_dll(self) -> None:
        solution_root = self.root / "source"
        project_output = solution_root / "Module" / "bin" / "Module.dll"
        project_output.parent.mkdir(parents=True)
        project_output.write_bytes(b"valid module")
        runtime_output = self.runtime_directory / "Module.dll"
        runtime_output.write_bytes(b"corrupt module")

        self.assertTrue(ensure_runtime_copy_integrity(
            solution_root,
            self.runtime_directory,
            project_outputs=(("Module/bin/Module.dll", "Module.dll"),),
            report=lambda _: None,
        ))
        self.assertEqual(runtime_output.read_bytes(), project_output.read_bytes())

    def test_runtime_copy_integrity_repairs_a_missing_dll(self) -> None:
        solution_root = self.root / "source"
        project_output = solution_root / "Module" / "bin" / "Module.dll"
        project_output.parent.mkdir(parents=True)
        project_output.write_bytes(b"valid module")
        runtime_output = self.runtime_directory / "Module.dll"

        self.assertTrue(ensure_runtime_copy_integrity(
            solution_root,
            self.runtime_directory,
            project_outputs=(("Module/bin/Module.dll", "Module.dll"),),
            report=lambda _: None,
        ))
        self.assertEqual(runtime_output.read_bytes(), project_output.read_bytes())

    def _write_aip(self, service_host_paths: tuple[str, ...], additional_paths: tuple[str, ...] = ()) -> Path:
        source_paths = [
            "C:\\build\\ColorVision.UI.dll",
            *[f"C:\\build\\{path}" for path in service_host_paths],
            *[f"C:\\build\\{path}" for path in additional_paths],
        ]
        rows = "".join(f'<ROW SourcePath="{path}" />' for path in source_paths)
        aip_path = self.root / "ColorVision.aip"
        aip_path.write_text(f"<DOCUMENT>{rows}</DOCUMENT>", encoding="utf-8")
        return aip_path


class PrimaryReleasePublishTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="build-publish-tests-")
        self.root = Path(self._temp_directory.name)
        self.installer = self.root / "ColorVision-1.2.3.4.exe"
        self.installer.write_bytes(b"installer")
        self.changelog = self.root / "CHANGELOG.md"
        self.changelog.write_text("## 1.2.3.4\n- release", encoding="utf-8")
        self.settings = RemoteUploadSettings(
            base_url="http://example.test:9998",
            folder_name="ColorVision",
            username="user",
            password="password",
        )

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    def test_publishes_package_changelog_then_latest_release(self) -> None:
        events: list[tuple[str, str]] = []

        def upload_file(path, _settings):
            events.append(("file", Path(path).name))
            return True

        def upload_content(content, remote_filename, _settings):
            events.append(("content", f"{remote_filename}:{content}"))
            return True

        with mock.patch("Scripts.build.backend_fetch_latest_version", return_value="1.2.3.3"):
            result = publish_primary_release(
                "1.2.3.4",
                self.installer,
                self.changelog,
                self.settings,
                upload_func=upload_file,
                upload_content_func=upload_content,
            )

        self.assertTrue(result)
        self.assertEqual(
            [
                ("file", "ColorVision-1.2.3.4.exe"),
                ("file", "CHANGELOG.md"),
                ("content", "LATEST_RELEASE:1.2.3.4"),
            ],
            events,
        )

    def test_changelog_failure_does_not_publish_latest_release(self) -> None:
        uploaded_content = mock.Mock(return_value=True)

        def upload_file(path, _settings):
            return Path(path).name != "CHANGELOG.md"

        with mock.patch("Scripts.build.backend_fetch_latest_version", return_value="1.2.3.3"):
            result = publish_primary_release(
                "1.2.3.4",
                self.installer,
                self.changelog,
                self.settings,
                upload_func=upload_file,
                upload_content_func=uploaded_content,
            )

        self.assertFalse(result)
        uploaded_content.assert_not_called()


class InstallerSelectionTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="build-selection-tests-")
        self.root = Path(self._temp_directory.name)

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    def test_selects_installer_matching_built_file_version_instead_of_highest_version(self) -> None:
        expected_installer = self.root / "ColorVision-1.2.3.4.exe"
        expected_installer.write_bytes(b"current build")
        (self.root / "ColorVision-9.9.9.9.exe").write_bytes(b"stale higher version")

        selected = get_installer_for_version(self.root, "1.2.3.4")

        self.assertEqual(expected_installer, selected)

    def test_rejects_directory_without_installer_matching_built_file_version(self) -> None:
        (self.root / "ColorVision-9.9.9.9.exe").write_bytes(b"stale higher version")

        selected = get_installer_for_version(self.root, "1.2.3.4")

        self.assertIsNone(selected)

    def test_rejects_ambiguous_installers_for_built_file_version(self) -> None:
        (self.root / "ColorVision-1.2.3.4.exe").write_bytes(b"exe")
        (self.root / "ColorVision-1.2.3.4.msi").write_bytes(b"msi")

        selected = get_installer_for_version(self.root, "1.2.3.4")

        self.assertIsNone(selected)


if __name__ == "__main__":
    unittest.main()

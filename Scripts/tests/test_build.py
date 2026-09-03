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

    def test_rejects_service_host_management_dependency_missing_from_installer_mapping(self) -> None:
        missing_path = "ServiceHost/System.Management.dll"
        aip_path = self._write_aip(tuple(
            path for path in REQUIRED_SERVICE_HOST_RUNTIME_PATHS if path != missing_path
        ))

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
        self.runtime = self.root / "runtime"
        self.runtime.mkdir()
        (self.runtime / "ColorVision.UI.dll").write_bytes(b"host")
        self.aip = self.root / "ColorVision.aip"
        self.aip.write_text('''<DOCUMENT>
<COMPONENT cid="caphyon.advinst.msicomp.MsiDirsComponent">
  <ROW Directory="APPDIR" Directory_Parent="TARGETDIR" DefaultDir="APPDIR:." />
</COMPONENT>
<COMPONENT cid="caphyon.advinst.msicomp.MsiCompsComponent">
  <ROW Component="Host" Directory_="APPDIR" />
</COMPONENT>
<COMPONENT cid="caphyon.advinst.msicomp.MsiFilesComponent">
  <ROW File="Host" Component_="Host" FileName="ColorVision.UI.dll" SourcePath="runtime/ColorVision.UI.dll" />
</COMPONENT>
</DOCUMENT>''', encoding="utf-8")
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
            if remote_filename.endswith(".json"):
                manifest = json.loads(content)
                self.assertEqual("1.2.3.4", manifest["host_version"])
                self.assertEqual(["ColorVision.UI.dll"], manifest["shared_files"])
                self.assertEqual("Tool/PluginKit/shared-files/1.2.3.4", _settings.folder_name)
                events.append(("manifest", remote_filename))
                return True
            events.append(("content", f"{remote_filename}:{content}"))
            return True

        with mock.patch("Scripts.build.backend_fetch_latest_version", return_value="1.2.3.3"):
            result = publish_primary_release(
                "1.2.3.4",
                self.installer,
                self.changelog,
                self.settings,
                runtime_directory=self.runtime,
                aip_path=self.aip,
                upload_func=upload_file,
                upload_content_func=upload_content,
            )

        self.assertTrue(result)
        self.assertEqual(
            [
                ("file", "ColorVision-1.2.3.4.exe"),
                ("file", "CHANGELOG.md"),
                ("manifest", "net10.0-windows-x64.json"),
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
                runtime_directory=self.runtime,
                aip_path=self.aip,
                upload_func=upload_file,
                upload_content_func=uploaded_content,
            )

        self.assertFalse(result)
        uploaded_content.assert_not_called()

    def test_manifest_failure_does_not_publish_latest_release(self) -> None:
        uploaded_content = mock.Mock(return_value=False)
        with mock.patch("Scripts.build.backend_fetch_latest_version", return_value="1.2.3.3"):
            result = publish_primary_release(
                "1.2.3.4", self.installer, self.changelog, self.settings,
                runtime_directory=self.runtime, aip_path=self.aip,
                upload_func=mock.Mock(return_value=True), upload_content_func=uploaded_content,
            )
        self.assertFalse(result)
        self.assertEqual(["net10.0-windows-x64.json"], [call.args[1] for call in uploaded_content.call_args_list])

    def test_manifest_excludes_files_not_proven_in_both_installer_and_zip(self) -> None:
        (self.runtime / "NotInstalled.dll").write_bytes(b"host output only")
        (self.runtime / "ColorVisionServiceHost.exe").write_bytes(b"not shipped at root in zip")
        aip_text = self.aip.read_text(encoding="utf-8")
        aip_text = aip_text.replace(
            '<ROW File="Host" Component_="Host" FileName="ColorVision.UI.dll" SourcePath="runtime/ColorVision.UI.dll" />',
            '<ROW File="Host" Component_="Host" FileName="ColorVision.UI.dll" SourcePath="runtime/ColorVision.UI.dll" />'
            '<ROW File="Service" Component_="Host" FileName="ColorVisionServiceHost.exe" SourcePath="runtime/ColorVisionServiceHost.exe" />',
        )
        self.aip.write_text(aip_text, encoding="utf-8")
        uploaded_content = mock.Mock(return_value=True)
        with mock.patch("Scripts.build.backend_fetch_latest_version", return_value="1.2.3.3"):
            result = publish_primary_release(
                "1.2.3.4", self.installer, self.changelog, self.settings,
                runtime_directory=self.runtime, aip_path=self.aip,
                upload_func=mock.Mock(return_value=True), upload_content_func=uploaded_content,
            )
        self.assertTrue(result)
        manifest = json.loads(uploaded_content.call_args_list[0].args[0])
        self.assertEqual(["ColorVision.UI.dll"], manifest["shared_files"])


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

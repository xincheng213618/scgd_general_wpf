import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

import Scripts.build as build_module
from Scripts.build import (
    ProjectConfig,
    RemoteUploadSettings,
    ensure_runtime_copy_integrity,
    get_installer_for_version,
    publish_primary_release,
    rebuild_project,
    validate_cuda_release_runtime,
    validate_installer_runtime_dlls,
    validate_runtime_copy_integrity,
    validate_shared_files_manifests,
)
from Scripts.service_host_runtime import REQUIRED_SERVICE_HOST_RUNTIME_PATHS
from Scripts.verify_native_contracts import CUDA_PACKAGE_MEMBER, CUDA_TRACKED_DLL


class ReleaseBuildOrchestrationTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary_directory = tempfile.TemporaryDirectory(prefix="release-build-order-tests-")
        self.root = Path(self._temporary_directory.name)
        self.msbuild = self.root / "MSBuild.exe"
        self.solution = self.root / "build.sln"
        self.advanced_installer = self.root / "AdvancedInstaller.com"
        self.aip = self.root / "ColorVision.aip"

    def tearDown(self) -> None:
        self._temporary_directory.cleanup()

    def test_rebuild_project_runs_native_gate_before_installer_build(self) -> None:
        events: list[str] = []

        def run(command, *, check):
            self.assertTrue(check)
            events.append("msbuild" if Path(command[0]) == self.msbuild else "advanced-installer")

        def successful(name: str):
            def invoke(*_args, **_kwargs):
                events.append(name)
                return True
            return invoke

        def native_gate(*_args, **_kwargs):
            events.append("native-gate")
            return mock.Mock(size=123, sha256="ABCD")

        with (
            mock.patch.object(build_module.subprocess, "run", side_effect=run),
            mock.patch.object(build_module, "ensure_runtime_copy_integrity", side_effect=successful("runtime-copy")),
            mock.patch.object(build_module, "validate_native_contracts", side_effect=native_gate),
            mock.patch.object(build_module, "validate_shared_files_manifests", side_effect=successful("shared-files")),
            mock.patch.object(build_module, "validate_installer_runtime_dlls", side_effect=successful("installer-map")),
        ):
            result = rebuild_project(self.msbuild, self.solution, self.advanced_installer, self.aip)

        self.assertTrue(result)
        self.assertEqual(
            [
                "msbuild",
                "runtime-copy",
                "native-gate",
                "shared-files",
                "installer-map",
                "advanced-installer",
            ],
            events,
        )

    def test_build_main_returns_one_when_native_gate_rejects_runtime(self) -> None:
        events: list[str] = []
        project = ProjectConfig(
            name="ColorVision",
            msbuild_path=self.msbuild,
            solution_path=self.solution,
            advanced_installer_path=self.advanced_installer,
            aip_path=self.aip,
            setup_files_dir=self.root / "setup",
            changelog_src=self.root / "CHANGELOG.md",
        )
        args = SimpleNamespace(
            project="ColorVision",
            upload_url=None,
            upload_folder="ColorVision",
            upload_user="user",
            upload_password="password",
            upload_use_system_proxy=False,
            connect_timeout=10,
            read_timeout=30,
            upload_retries=1,
        )

        def run(command, *, check):
            self.assertTrue(check)
            events.append("msbuild" if Path(command[0]) == self.msbuild else "advanced-installer")

        def runtime_copy(*_args, **_kwargs):
            events.append("runtime-copy")
            return True

        def native_gate(*_args, **_kwargs):
            events.append("native-gate")
            raise build_module.NativeContractError("mutated ABI")

        with (
            mock.patch.object(build_module, "parse_args", return_value=args),
            mock.patch.object(build_module, "build_projects", return_value={"ColorVision": project}),
            mock.patch.object(build_module, "preflight_remote_upload", return_value=True),
            mock.patch.object(build_module.subprocess, "run", side_effect=run) as run_mock,
            mock.patch.object(build_module, "ensure_runtime_copy_integrity", side_effect=runtime_copy),
            mock.patch.object(build_module, "validate_native_contracts", side_effect=native_gate),
            mock.patch.object(build_module, "validate_shared_files_manifests") as shared_mock,
            mock.patch.object(build_module, "validate_installer_runtime_dlls") as installer_mock,
            mock.patch.object(build_module, "get_file_version") as version_mock,
            mock.patch.object(build_module, "publish_primary_release") as publish_mock,
        ):
            result = build_module.main()

        self.assertEqual(1, result)
        self.assertEqual(["msbuild", "runtime-copy", "native-gate"], events)
        self.assertEqual(1, run_mock.call_count)
        shared_mock.assert_not_called()
        installer_mock.assert_not_called()
        version_mock.assert_not_called()
        publish_mock.assert_not_called()


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
        cuda_runtime = self.runtime_directory / CUDA_PACKAGE_MEMBER
        cuda_runtime.parent.mkdir(parents=True, exist_ok=True)
        cuda_runtime.write_bytes(b"cuda-runtime")

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

    def test_rejects_cuda_runtime_missing_from_installer_mapping(self) -> None:
        aip_path = self._write_aip(REQUIRED_SERVICE_HOST_RUNTIME_PATHS, include_cuda=False)

        self.assertFalse(validate_installer_runtime_dlls(self.runtime_directory, aip_path, report=lambda _: None))

    def test_cuda_release_gate_accepts_tracked_runtime_copy(self) -> None:
        repository_root = Path(__file__).resolve().parents[2]
        cuda_runtime = self.runtime_directory / CUDA_PACKAGE_MEMBER
        cuda_runtime.write_bytes((repository_root / CUDA_TRACKED_DLL).read_bytes())

        self.assertTrue(validate_cuda_release_runtime(
            repository_root,
            self.runtime_directory,
            report=lambda _: None,
        ))

    def test_cuda_release_gate_rejects_stale_runtime_copy(self) -> None:
        repository_root = Path(__file__).resolve().parents[2]

        self.assertFalse(validate_cuda_release_runtime(
            repository_root,
            self.runtime_directory,
            report=lambda _: None,
        ))

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

    def _write_aip(self, service_host_paths: tuple[str, ...], *, include_cuda: bool = True) -> Path:
        source_paths = ["C:\\build\\ColorVision.UI.dll", *[f"C:\\build\\{path}" for path in service_host_paths]]
        if include_cuda:
            source_paths.append(f"C:\\build\\{CUDA_PACKAGE_MEMBER}")
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

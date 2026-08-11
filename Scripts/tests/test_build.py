import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import Scripts.build as build_module
from Scripts.build import (
    ProjectConfig,
    RemoteUploadSettings,
    ensure_runtime_copy_integrity,
    get_installer_for_version,
    prepare_primary_release,
    publish_primary_release,
    rebuild_project,
    validate_built_installer_cuda,
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
        self.setup = self.root / "setup"
        self.setup.mkdir()

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

    def test_prepare_primary_requires_final_installer_payload_gate(self) -> None:
        project = ProjectConfig(
            "ColorVision",
            self.msbuild,
            self.solution,
            self.advanced_installer,
            self.aip,
            self.setup,
            self.root / "CHANGELOG.md",
        )
        installer = self.setup / "ColorVision-1.2.3.4.exe"
        installer.write_bytes(b"installer")
        with (
            mock.patch.object(build_module, "rebuild_project", return_value=True),
            mock.patch.object(build_module, "get_file_version", return_value="1.2.3.4"),
            mock.patch.object(build_module, "validate_built_installer_cuda", return_value=False) as final_gate,
        ):
            prepared = prepare_primary_release(project)

        self.assertIsNone(prepared)
        final_gate.assert_called_once()

class InstallerRuntimeValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="build-installer-tests-")
        self.root = Path(self._temp_directory.name)
        self.solution_root = self.root / "repo"
        self.tracked_cuda = self.solution_root / CUDA_TRACKED_DLL
        self.tracked_cuda.parent.mkdir(parents=True)
        self.tracked_cuda.write_bytes(b"cuda-runtime")
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

        self.assertTrue(validate_installer_runtime_dlls(
            self.solution_root, self.runtime_directory, aip_path, report=lambda _: None,
        ))

    def test_rejects_service_host_file_missing_from_installer_mapping(self) -> None:
        aip_path = self._write_aip(REQUIRED_SERVICE_HOST_RUNTIME_PATHS[:-1])

        self.assertFalse(validate_installer_runtime_dlls(
            self.solution_root, self.runtime_directory, aip_path, report=lambda _: None,
        ))

    def test_rejects_incomplete_service_host_build_output(self) -> None:
        (self.runtime_directory / REQUIRED_SERVICE_HOST_RUNTIME_PATHS[0]).unlink()
        aip_path = self._write_aip(REQUIRED_SERVICE_HOST_RUNTIME_PATHS)

        self.assertFalse(validate_installer_runtime_dlls(
            self.solution_root, self.runtime_directory, aip_path, report=lambda _: None,
        ))

    def test_rejects_cuda_runtime_missing_from_installer_mapping(self) -> None:
        aip_path = self._write_aip(REQUIRED_SERVICE_HOST_RUNTIME_PATHS, include_cuda=False)

        self.assertFalse(validate_installer_runtime_dlls(
            self.solution_root, self.runtime_directory, aip_path, report=lambda _: None,
        ))

    def test_rejects_missing_installer_source_entity(self) -> None:
        aip_path = self._write_aip(REQUIRED_SERVICE_HOST_RUNTIME_PATHS)
        (self.root / "sources" / Path(CUDA_PACKAGE_MEMBER)).unlink()

        self.assertFalse(validate_installer_runtime_dlls(
            self.solution_root, self.runtime_directory, aip_path, report=lambda _: None,
        ))

    def test_rejects_stale_external_cuda_source_bytes(self) -> None:
        aip_path = self._write_aip(REQUIRED_SERVICE_HOST_RUNTIME_PATHS)
        (self.root / "sources" / Path(CUDA_PACKAGE_MEMBER)).write_bytes(b"stale external DLL")

        self.assertFalse(validate_installer_runtime_dlls(
            self.solution_root, self.runtime_directory, aip_path, report=lambda _: None,
        ))

    def test_rejects_cuda_source_mapped_to_wrong_target(self) -> None:
        aip_path = self._write_aip(REQUIRED_SERVICE_HOST_RUNTIME_PATHS, cuda_target="opencv_cuda.dll")

        self.assertFalse(validate_installer_runtime_dlls(
            self.solution_root, self.runtime_directory, aip_path, report=lambda _: None,
        ))

    def test_rejects_duplicate_case_variant_cuda_target(self) -> None:
        aip_path = self._write_aip(
            REQUIRED_SERVICE_HOST_RUNTIME_PATHS,
            extra_cuda_target="runtimes/win-x64/native/OpenCV_CUDA.dll",
        )

        self.assertFalse(validate_installer_runtime_dlls(
            self.solution_root, self.runtime_directory, aip_path, report=lambda _: None,
        ))

    def test_rejects_backslash_variant_duplicate_cuda_source(self) -> None:
        aip_path = self._write_aip(REQUIRED_SERVICE_HOST_RUNTIME_PATHS)
        source_path = (self.root / "sources" / Path(CUDA_PACKAGE_MEMBER)).as_posix()
        duplicate_rows = (
            '<ROW Component="duplicate_cuda_component" '
            'ComponentId="{00000000-0000-0000-0000-999999999999}" '
            'Directory_="APPDIR" KeyPath="duplicate_cuda_file"/>'
            '<ROW File="duplicate_cuda_file" Component_="duplicate_cuda_component" '
            f'FileName="Alias.dll" SourcePath="{source_path}"/>'
        )
        aip_path.write_text(
            aip_path.read_text(encoding="utf-8").replace("</DOCUMENT>", duplicate_rows + "</DOCUMENT>"),
            encoding="utf-8",
        )

        self.assertFalse(validate_installer_runtime_dlls(
            self.solution_root, self.runtime_directory, aip_path, report=lambda _: None,
        ))

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

    def _write_aip(
        self,
        service_host_paths: tuple[str, ...],
        *,
        include_cuda: bool = True,
        cuda_target: str = CUDA_PACKAGE_MEMBER,
        extra_cuda_target: str | None = None,
    ) -> Path:
        targets = ["ColorVision.UI.dll", *service_host_paths]
        if include_cuda:
            targets.append(cuda_target)
        if extra_cuda_target:
            targets.append(extra_cuda_target)

        source_root = self.root / "sources"
        directory_rows = [
            '<ROW Directory="TARGETDIR" DefaultDir="SourceDir"/>',
            '<ROW Directory="APPDIR" Directory_Parent="TARGETDIR" DefaultDir="APPDIR:."/>',
        ]
        directory_ids: dict[str, str] = {"": "APPDIR"}
        component_rows: list[str] = []
        file_rows: list[str] = []
        for index, target in enumerate(targets):
            target_path = Path(target.replace("\\", "/"))
            parent_key = ""
            parent_id = "APPDIR"
            for part in target_path.parts[:-1]:
                key = f"{parent_key}/{part}".strip("/")
                if key not in directory_ids:
                    directory_id = f"dir_{len(directory_ids)}"
                    directory_ids[key] = directory_id
                    directory_rows.append(
                        f'<ROW Directory="{directory_id}" Directory_Parent="{parent_id}" DefaultDir="{part}"/>'
                    )
                parent_key = key
                parent_id = directory_ids[key]

            component_id = f"component_{index}"
            file_id = f"file_{index}"
            component_rows.append(
                f'<ROW Component="{component_id}" ComponentId="{{00000000-0000-0000-0000-{index:012d}}}" '
                f'Directory_="{parent_id}" KeyPath="{file_id}"/>'
            )
            source_relative = CUDA_PACKAGE_MEMBER if target_path.name.casefold() == "opencv_cuda.dll" else target
            source_path = source_root / Path(source_relative.replace("\\", "/"))
            source_path.parent.mkdir(parents=True, exist_ok=True)
            if not source_path.exists():
                runtime_source = self.runtime_directory / Path(source_relative.replace("\\", "/"))
                source_path.write_bytes(runtime_source.read_bytes() if runtime_source.is_file() else b"cuda-runtime")
            file_rows.append(
                f'<ROW File="{file_id}" Component_="{component_id}" FileName="{target_path.name}" '
                f'SourcePath="{source_path}"/>'
            )

        aip_path = self.root / "ColorVision.aip"
        aip_path.write_text(
            '<DOCUMENT RootPath=".">' + ''.join(directory_rows + component_rows + file_rows) + '</DOCUMENT>',
            encoding="utf-8",
        )
        return aip_path


class BuiltInstallerPayloadTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="built-installer-payload-tests-")
        self.root = Path(self._temp_directory.name)
        self.solution_root = self.root / "repo"
        self.tracked = self.solution_root / CUDA_TRACKED_DLL
        self.tracked.parent.mkdir(parents=True)
        self.tracked.write_bytes(b"tracked cuda")
        self.runtime = self.root / "runtime"
        runtime_cuda = self.runtime / Path(CUDA_PACKAGE_MEMBER)
        runtime_cuda.parent.mkdir(parents=True)
        runtime_cuda.write_bytes(self.tracked.read_bytes())
        self.installer = self.root / "ColorVision-1.2.3.4.exe"
        self.installer.write_bytes(b"installer")
        self.msiexec = self.root / "msiexec.exe"
        self.msiexec.write_bytes(b"tool")

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    def test_accepts_unique_installed_cuda_at_expected_suffix(self) -> None:
        self.assertTrue(validate_built_installer_cuda(
            self.installer,
            self.solution_root,
            self.runtime,
            command_runner=self._runner(),
            msiexec_path=self.msiexec,
            report=lambda _: None,
        ))

    def test_rejects_stale_cuda_in_final_installer_image(self) -> None:
        self.assertFalse(validate_built_installer_cuda(
            self.installer,
            self.solution_root,
            self.runtime,
            command_runner=self._runner(installed_bytes=b"stale"),
            msiexec_path=self.msiexec,
            report=lambda _: None,
        ))

    def test_rejects_duplicate_cuda_targets_in_final_installer_image(self) -> None:
        self.assertFalse(validate_built_installer_cuda(
            self.installer,
            self.solution_root,
            self.runtime,
            command_runner=self._runner(duplicate=True),
            msiexec_path=self.msiexec,
            report=lambda _: None,
        ))

    def test_rejects_cuda_at_wrong_final_installer_target(self) -> None:
        self.assertFalse(validate_built_installer_cuda(
            self.installer,
            self.solution_root,
            self.runtime,
            command_runner=self._runner(wrong_target=True),
            msiexec_path=self.msiexec,
            report=lambda _: None,
        ))

    def test_fails_closed_when_msiexec_is_unavailable(self) -> None:
        self.assertFalse(validate_built_installer_cuda(
            self.installer,
            self.solution_root,
            self.runtime,
            command_runner=mock.Mock(),
            msiexec_path=self.root / "missing-msiexec.exe",
            report=lambda _: None,
        ))

    def test_rejects_ambiguous_extracted_msi_files(self) -> None:
        self.assertFalse(validate_built_installer_cuda(
            self.installer,
            self.solution_root,
            self.runtime,
            command_runner=self._runner(multiple_msi=True),
            msiexec_path=self.msiexec,
            report=lambda _: None,
        ))

    def test_fails_closed_when_extraction_command_fails(self) -> None:
        self.assertFalse(validate_built_installer_cuda(
            self.installer,
            self.solution_root,
            self.runtime,
            command_runner=mock.Mock(side_effect=OSError("extract failed")),
            msiexec_path=self.msiexec,
            report=lambda _: None,
        ))

    def _runner(
        self,
        *,
        installed_bytes: bytes | None = None,
        duplicate: bool = False,
        multiple_msi: bool = False,
        wrong_target: bool = False,
    ):
        payload = self.tracked.read_bytes() if installed_bytes is None else installed_bytes

        def run(command, *, check, **kwargs):
            self.assertTrue(check)
            if Path(command[0]) == self.installer:
                self.assertEqual("RunAsInvoker", kwargs["env"]["__COMPAT_LAYER"])
                extract_directory = Path(command[2])
                (extract_directory / "ColorVision.msi").write_bytes(b"msi")
                if multiple_msi:
                    (extract_directory / "Other.msi").write_bytes(b"msi")
                return
            image_argument = next(argument for argument in command if argument.startswith("TARGETDIR="))
            image_directory = Path(image_argument.split("=", 1)[1])
            installed = (
                image_directory / "ColorVision" / "opencv_cuda.dll"
                if wrong_target
                else image_directory / "ColorVision" / Path(CUDA_PACKAGE_MEMBER)
            )
            installed.parent.mkdir(parents=True, exist_ok=True)
            installed.write_bytes(payload)
            if duplicate:
                other = image_directory / "Other" / "opencv_cuda.dll"
                other.parent.mkdir(parents=True)
                other.write_bytes(payload)

        return run


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
        self.incremental = self.root / "ColorVision-Update-[1.2.3.4].cvx"
        self.incremental.write_bytes(b"update")

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    def test_publishes_package_update_changelog_then_latest_release(self) -> None:
        events: list[tuple[str, str, str]] = []

        def upload_file(path, settings):
            events.append(("file", settings.folder_name, Path(path).name))
            return True

        def upload_content(content, remote_filename, settings):
            events.append(("content", settings.folder_name, f"{remote_filename}:{content}"))
            return True

        with mock.patch("Scripts.build.backend_fetch_latest_version", return_value="1.2.3.3"):
            result = publish_primary_release(
                "1.2.3.4",
                self.installer,
                self.changelog,
                self.settings,
                incremental_file=self.incremental,
                upload_func=upload_file,
                upload_content_func=upload_content,
            )

        self.assertTrue(result)
        self.assertEqual(
            [
                ("file", "ColorVision", "ColorVision-1.2.3.4.exe"),
                ("file", "ColorVision/Update", "ColorVision-Update-[1.2.3.4].cvx"),
                ("file", "ColorVision", "CHANGELOG.md"),
                ("content", "ColorVision", "LATEST_RELEASE:1.2.3.4"),
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

    def test_installer_failure_does_not_publish_update_or_metadata(self) -> None:
        uploaded_files: list[str] = []
        uploaded_content = mock.Mock(return_value=True)

        def upload_file(path, _settings):
            uploaded_files.append(Path(path).name)
            return False

        with mock.patch("Scripts.build.backend_fetch_latest_version", return_value="1.2.3.3"):
            result = publish_primary_release(
                "1.2.3.4",
                self.installer,
                self.changelog,
                self.settings,
                incremental_file=self.incremental,
                upload_func=upload_file,
                upload_content_func=uploaded_content,
            )

        self.assertFalse(result)
        self.assertEqual([self.installer.name], uploaded_files)
        uploaded_content.assert_not_called()

    def test_update_failure_does_not_publish_changelog_or_latest_release(self) -> None:
        events: list[str] = []
        uploaded_content = mock.Mock(return_value=True)

        def upload_file(path, _settings):
            events.append(Path(path).name)
            return Path(path) != self.incremental

        with mock.patch("Scripts.build.backend_fetch_latest_version", return_value="1.2.3.3"):
            result = publish_primary_release(
                "1.2.3.4",
                self.installer,
                self.changelog,
                self.settings,
                incremental_file=self.incremental,
                upload_func=upload_file,
                upload_content_func=uploaded_content,
            )

        self.assertFalse(result)
        self.assertEqual([self.installer.name, self.incremental.name], events)
        uploaded_content.assert_not_called()

    def test_latest_failure_is_reported_after_all_payload_uploads(self) -> None:
        uploaded_files: list[str] = []

        def upload_file(path, _settings):
            uploaded_files.append(Path(path).name)
            return True

        with mock.patch("Scripts.build.backend_fetch_latest_version", return_value="1.2.3.3"):
            result = publish_primary_release(
                "1.2.3.4",
                self.installer,
                self.changelog,
                self.settings,
                incremental_file=self.incremental,
                upload_func=upload_file,
                upload_content_func=mock.Mock(return_value=False),
            )

        self.assertFalse(result)
        self.assertEqual([self.installer.name, self.incremental.name, self.changelog.name], uploaded_files)


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

import os
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest import mock

import Scripts.build_update as build_update_module
from Scripts.build_update import (
    REQUIRED_SERVICE_HOST_RUNTIME_PATHS,
    create_full_zip,
    find_incremental_baseline,
    make_incremental_zip,
    validate_service_host_runtime,
)
from Scripts.verify_native_contracts import NativeContractError


class UpdateBuildOrchestrationTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary_directory = tempfile.TemporaryDirectory(prefix="update-build-order-tests-")
        self.root = Path(self._temporary_directory.name)
        self.runtime = self.root / "runtime"
        self.history = self.root / "history"
        self.update = self.history / "update"
        self.executable = self.runtime / "ColorVision.exe"
        self.update.mkdir(parents=True, exist_ok=True)

    def tearDown(self) -> None:
        self._temporary_directory.cleanup()

    def test_prepare_validates_and_promotes_full_then_incremental_without_upload(self) -> None:
        events: list[str] = []
        validated_packages: list[tuple[Path, ...]] = []
        real_replace = os.replace
        full_zip = self.history / "ColorVision-[1.2.3.4].zip"
        pending_full_zip = self.history / "ColorVision-[1.2.3.4].zip.pending"
        incremental_zip = self.update / "ColorVision-Update-[1.2.3.4].cvx"
        pending_incremental_zip = self.update / "ColorVision-Update-[1.2.3.4].cvx.pending"
        full_zip.write_bytes(b"previous validated package")

        def create_full(_runtime, output):
            events.append("full")
            self.assertEqual(pending_full_zip, Path(output))
            Path(output).write_bytes(b"validated full package")

        def native_gate(_root, *, package_files):
            events.append("gate")
            self.assertEqual(b"previous validated package", full_zip.read_bytes())
            validated_packages.append(tuple(Path(path) for path in package_files))
            return mock.Mock(sha256="ABCD")

        def promote(source, destination):
            events.append("promote-full" if Path(source) == pending_full_zip else "promote-incremental")
            real_replace(source, destination)

        def make_incremental(_old, _runtime, output):
            events.append("incremental")
            self.assertEqual(pending_incremental_zip, Path(output))
            Path(output).write_bytes(b"validated incremental package")

        def archive_gate(path):
            events.append("zip-gate-full" if Path(path) == pending_full_zip else "zip-gate-incremental")

        with (
            self._patch_main_environment(),
            mock.patch.object(build_update_module, "create_full_zip", side_effect=create_full),
            mock.patch.object(build_update_module, "validate_native_contracts", side_effect=native_gate),
            mock.patch.object(build_update_module, "validate_zip_archive", side_effect=archive_gate),
            mock.patch.object(build_update_module.os, "replace", side_effect=promote),
            mock.patch.object(build_update_module, "find_incremental_baseline", return_value=str(self.history / "old.zip")),
            mock.patch.object(build_update_module, "make_incremental_zip", side_effect=make_incremental),
        ):
            result = build_update_module.prepare_update_release()

        self.assertIsNotNone(result)
        self.assertEqual(
            ["full", "gate", "zip-gate-full", "promote-full", "incremental", "zip-gate-incremental", "promote-incremental"],
            events,
        )
        self.assertEqual([(pending_full_zip,)], validated_packages)
        self.assertEqual(b"validated full package", full_zip.read_bytes())
        self.assertEqual(b"validated incremental package", incremental_zip.read_bytes())
        self.assertFalse(pending_full_zip.exists())
        self.assertFalse(pending_incremental_zip.exists())

    def test_main_gate_failure_preserves_existing_full_zip_and_removes_pending(self) -> None:
        events: list[str] = []
        full_zip = self.history / "ColorVision-[1.2.3.4].zip"
        pending_full_zip = self.history / "ColorVision-[1.2.3.4].zip.pending"
        full_zip.write_bytes(b"previous validated package")

        def create_full(_runtime, output):
            events.append("full")
            self.assertEqual(pending_full_zip, Path(output))
            Path(output).write_bytes(b"rejected pending package")

        def native_gate(_root, *, package_files):
            self.assertEqual((pending_full_zip,), tuple(Path(path) for path in package_files))
            events.append("gate")
            raise NativeContractError("mutated ABI")

        with (
            self._patch_main_environment(),
            mock.patch.object(build_update_module, "create_full_zip", side_effect=create_full),
            mock.patch.object(build_update_module, "validate_native_contracts", side_effect=native_gate),
            mock.patch.object(build_update_module.os, "replace") as replace_mock,
            mock.patch.object(build_update_module, "find_incremental_baseline") as baseline_mock,
            mock.patch.object(build_update_module, "make_incremental_zip") as incremental_mock,
        ):
            result = build_update_module.prepare_update_release()

        self.assertIsNone(result)
        self.assertEqual(["full", "gate"], events)
        self.assertEqual(b"previous validated package", full_zip.read_bytes())
        self.assertFalse(pending_full_zip.exists())
        replace_mock.assert_not_called()
        baseline_mock.assert_not_called()
        incremental_mock.assert_not_called()

    def test_incremental_failure_preserves_existing_package_and_removes_pending(self) -> None:
        full_zip = self.history / "ColorVision-[1.2.3.4].zip"
        incremental_zip = self.update / "ColorVision-Update-[1.2.3.4].cvx"
        pending_incremental_zip = Path(f"{incremental_zip}.pending")
        self.update.mkdir(parents=True, exist_ok=True)
        incremental_zip.write_bytes(b"previous update")

        def create_full(_runtime, output):
            with zipfile.ZipFile(output, "w") as archive:
                archive.writestr("runtimes/win-x64/native/opencv_cuda.dll", b"cuda")

        def make_incremental(_old, _runtime, output):
            Path(output).write_bytes(b"partial")
            raise OSError("disk full")

        with (
            self._patch_main_environment(),
            mock.patch.object(build_update_module, "create_full_zip", side_effect=create_full),
            mock.patch.object(build_update_module, "validate_native_contracts", return_value=mock.Mock(sha256="ABCD")),
            mock.patch.object(build_update_module, "find_incremental_baseline", return_value=str(self.history / "old.zip")),
            mock.patch.object(build_update_module, "make_incremental_zip", side_effect=make_incremental),
        ):
            result = build_update_module.prepare_update_release()

        self.assertIsNone(result)
        self.assertEqual(b"previous update", incremental_zip.read_bytes())
        self.assertFalse(pending_incremental_zip.exists())

    def test_rejects_version_mismatch_before_creating_packages(self) -> None:
        with (
            self._patch_main_environment(),
            mock.patch.object(build_update_module, "create_full_zip") as create_mock,
        ):
            result = build_update_module.prepare_update_release(expected_version="9.9.9.9")

        self.assertIsNone(result)
        create_mock.assert_not_called()

    def _patch_main_environment(self):
        return mock.patch.multiple(
            build_update_module,
            exe_path=str(self.executable),
            new_version_dir=str(self.runtime),
            history_dir=str(self.history),
            update_dir=str(self.update),
            get_file_version=mock.Mock(return_value="1.2.3.4"),
            validate_service_host_runtime=mock.Mock(return_value=None),
            create_directory_if_not_exists=mock.Mock(return_value=None),
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


class CopilotSkillsPackageTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="build-update-copilot-skills-tests-")
        self.root = Path(self._temp_directory.name)
        self.old_directory = self.root / "old"
        self.new_directory = self.root / "new"
        self.old_directory.mkdir()
        self.new_directory.mkdir()

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    def test_full_zip_preserves_recursive_copilot_skill_paths_and_content(self) -> None:
        expected_files = {
            "Copilot/Skills/nested-skill/SKILL.md": b"# Nested skill\nfull-package-content\n",
            "Copilot/Skills/nested-skill/agents/openai.yaml": b"name: nested-skill\n",
            "Copilot/Skills/nested-skill/references/overview.md": b"overview-bytes\n",
            "Copilot/Skills/nested-skill/references/guides/details.md": b"deep-reference-bytes\n",
        }
        self._write_files(self.new_directory, expected_files)

        full_zip = self.root / "full.zip"
        create_full_zip(self.new_directory, full_zip)

        self.assertEqual(expected_files, self._read_zip(full_zip))

    def test_incremental_zip_preserves_added_and_changed_copilot_skill_trees(self) -> None:
        old_files = {
            "Copilot/Skills/existing-skill/SKILL.md": b"# Existing skill\nold\n",
            "Copilot/Skills/existing-skill/agents/openai.yaml": b"name: old-existing\n",
            "Copilot/Skills/existing-skill/references/overview.md": b"old-reference\n",
            "Copilot/Skills/existing-skill/references/unchanged.md": b"same-reference\n",
        }
        changed_files = {
            "Copilot/Skills/existing-skill/SKILL.md": b"# Existing skill\nchanged\n",
            "Copilot/Skills/existing-skill/agents/openai.yaml": b"name: changed-existing\n",
            "Copilot/Skills/existing-skill/references/overview.md": b"changed-reference\n",
        }
        added_files = {
            "Copilot/Skills/added-skill/SKILL.md": b"# Added skill\nnew\n",
            "Copilot/Skills/added-skill/agents/openai.yaml": b"name: added-skill\n",
            "Copilot/Skills/added-skill/references/start-here.md": b"new-reference\n",
            "Copilot/Skills/added-skill/references/deep/checklist.md": b"new-deep-reference\n",
        }
        new_files = old_files | changed_files | added_files
        self._write_files(self.old_directory, old_files)
        self._write_files(self.new_directory, new_files)

        old_zip = self.root / "old.zip"
        create_full_zip(self.old_directory, old_zip)
        incremental_zip = self.root / "incremental.cvx"
        make_incremental_zip(old_zip, self.new_directory, incremental_zip)

        self.assertEqual(changed_files | added_files, self._read_zip(incremental_zip))

    @staticmethod
    def _write_files(root: Path, files: dict[str, bytes]) -> None:
        for relative_path, content in files.items():
            path = root / Path(relative_path)
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)

    def _read_zip(self, path: Path) -> dict[str, bytes]:
        with zipfile.ZipFile(path, "r") as archive:
            names = archive.namelist()
            self.assertTrue(all("\\" not in name for name in names))
            return {name: archive.read(name) for name in names}


if __name__ == "__main__":
    unittest.main()

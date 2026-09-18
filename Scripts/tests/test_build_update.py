import tempfile
import hashlib
import unittest
import zipfile
from pathlib import Path
from unittest import mock
from Scripts import build_update

from Scripts.build_update import (
    REQUIRED_SERVICE_HOST_RUNTIME_PATHS,
    create_full_zip,
    find_incremental_baseline,
    make_incremental_zip,
    validate_service_host_runtime,
)


class NativeRuntimeDeliveryTests(unittest.TestCase):
    def test_native_dependencies_follow_content_diff_for_old_and_new_baselines(self):
        native_paths = (
            'runtimes/win-x64/native/OpenCvSharpExtern.dll',
            'runtimes/win-x64/native/opencv_videoio_ffmpeg4130_64.dll',
            'runtimes/win-x64/native/opencv_videoio_ffmpeg4140_64.dll',
        )
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            runtime = root / "runtime"
            baseline = root / "ColorVision-[1.4.14.1].zip"
            expected = {}
            with zipfile.ZipFile(baseline, "w") as z:
                for relative in native_paths:
                    data = relative.encode()
                    path = runtime / relative
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(data)
                    z.writestr(relative, data)
                    expected[relative] = hashlib.sha256(data).hexdigest()
            package = root / "incremental.cvx"
            for name in ("ColorVision-[1.4.14.1].zip", "ColorVision-[1.4.15.1].zip"):
                with self.subTest(baseline=name):
                    candidate = root / name
                    if candidate != baseline:
                        candidate.write_bytes(baseline.read_bytes())
                    make_incremental_zip(candidate, runtime, package, native_hashes=expected)
                    with zipfile.ZipFile(package) as z:
                        self.assertEqual(z.namelist(), [])

            # Changed native libraries must still be delivered; do not blacklist these DLLs.
            changed = native_paths[0]
            content = b'updated native runtime'
            (runtime / changed).write_bytes(content)
            expected[changed] = hashlib.sha256(content).hexdigest()
            make_incremental_zip(baseline, runtime, package, native_hashes=expected)
            with zipfile.ZipFile(package) as z:
                self.assertEqual(z.namelist(), [changed])
                self.assertEqual(z.read(changed), content)

    def test_runtime_corruption_stops_before_packaging_or_upload(self):
        with mock.patch.object(build_update, "get_file_version", return_value="1.4.14.82"), \
             mock.patch.object(build_update, "validate_service_host_runtime"), \
             mock.patch.object(build_update, "validate_operations_watchdog_runtime"), \
             mock.patch.object(build_update, "ensure_native_runtime_integrity", side_effect=ValueError("bad native")), \
             mock.patch.object(build_update, "make_incremental_zip") as pack, \
             mock.patch.object(build_update, "upload_file") as upload:
            self.assertEqual(build_update.main(), 1)
        pack.assert_not_called()
        upload.assert_not_called()

    def test_packaged_content_failure_stops_upload(self):
        with mock.patch.object(build_update, "get_file_version", return_value="1.4.14.82"), \
             mock.patch.object(build_update, "validate_service_host_runtime"), \
             mock.patch.object(build_update, "validate_operations_watchdog_runtime"), \
             mock.patch.object(build_update, "ensure_native_runtime_integrity", return_value={"native.dll": "hash"}), \
             mock.patch.object(build_update, "create_directory_if_not_exists"), \
             mock.patch.object(build_update, "find_incremental_baseline", return_value="base.zip"), \
             mock.patch.object(build_update, "make_incremental_zip", side_effect=ValueError("bad archive")) as pack, \
             mock.patch.object(build_update, "upload_file") as upload:
            self.assertEqual(build_update.main(), 1)
        self.assertEqual(pack.call_args.kwargs["native_hashes"], {"native.dll": "hash"})
        upload.assert_not_called()


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

    def test_runtime_validation_rejects_missing_management_dependency(self) -> None:
        for relative_path in REQUIRED_SERVICE_HOST_RUNTIME_PATHS:
            self._write_file(self.new_directory / relative_path, b"runtime")
        (self.new_directory / "ServiceHost/System.Management.dll").unlink()

        with self.assertRaisesRegex(FileNotFoundError, "ServiceHost/System.Management.dll"):
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


class RuntimeDiagnosticsPackageTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="build-update-diagnostics-tests-")
        self.root = Path(self._temp_directory.name)
        self.old_directory = self.root / "old"
        self.new_directory = self.root / "new"
        self.old_directory.mkdir()
        self.new_directory.mkdir()

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    def test_full_and_incremental_packages_exclude_window_resize_diagnostics(self) -> None:
        (self.new_directory / "Host.dll").write_bytes(b"host")
        (self.new_directory / "window-resize-diagnostics.mode").write_text("compact", encoding="utf-8")
        trace_directory = self.new_directory / "window-resize-traces"
        trace_directory.mkdir()
        (trace_directory / "resize.json").write_text("{}", encoding="utf-8")

        old_zip = self.root / "old.zip"
        create_full_zip(self.old_directory, old_zip)
        full_zip = self.root / "full.zip"
        create_full_zip(self.new_directory, full_zip)
        incremental_zip = self.root / "incremental.cvx"
        make_incremental_zip(old_zip, self.new_directory, incremental_zip)

        self.assertEqual({"Host.dll": b"host"}, self._read_zip(full_zip))
        self.assertEqual({"Host.dll": b"host"}, self._read_zip(incremental_zip))

    @staticmethod
    def _read_zip(path: Path) -> dict[str, bytes]:
        with zipfile.ZipFile(path, "r") as archive:
            return {name.replace("\\", "/"): archive.read(name) for name in archive.namelist()}


if __name__ == "__main__":
    unittest.main()

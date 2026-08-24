import tempfile
import unittest
import zipfile
from pathlib import Path

from Scripts.build_update import (
    REQUIRED_SERVICE_HOST_RUNTIME_PATHS,
    create_full_zip,
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


if __name__ == "__main__":
    unittest.main()

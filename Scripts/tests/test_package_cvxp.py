import json
import tempfile
import unittest
import zipfile
from pathlib import Path

from Scripts.package_cvxp import (
    REPO_ROOT,
    ensure_default_shared_files_are_current,
    is_repository_package_project,
    package_plugin,
    resolve_primary_dll_path,
    synchronize_manifest_version,
    validate_plugin_manifest,
)


class PackageCvxManifestValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="package-cvxp-tests-")
        self.manifest_path = Path(self._temp_directory.name) / "manifest.json"

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    def test_valid_manifest_reports_identity(self) -> None:
        self._write_manifest({"id": "legacy.plugin", "name": "Legacy Plugin"})

        summary = validate_plugin_manifest(self.manifest_path)

        self.assertTrue(summary.manifest_present)
        self.assertEqual("legacy.plugin", summary.plugin_id)
        self.assertEqual("", summary.dll_path)

    def test_top_level_manifest_fields_match_runtime_case_insensitively(self) -> None:
        self._write_manifest({"Id": "LegacyPlugin", "DllPath": "LegacyPlugin.dll"})

        summary = validate_plugin_manifest(self.manifest_path)

        self.assertEqual("LegacyPlugin", summary.plugin_id)
        self.assertEqual("LegacyPlugin.dll", summary.dll_path)

    def test_manifest_version_is_updated_to_match_primary_dll(self) -> None:
        self._write_manifest({"id": "sample.plugin", "version": "1.1.7.53"})

        updated, previous_version = synchronize_manifest_version(self.manifest_path, "1.1.7.54")

        self.assertTrue(updated)
        self.assertEqual("1.1.7.53", previous_version)
        self.assertEqual("1.1.7.54", json.loads(self.manifest_path.read_text(encoding="utf-8"))["version"])

    def test_matching_manifest_version_is_not_rewritten(self) -> None:
        self._write_manifest({"id": "sample.plugin", "version": "1.1.7.54"})
        original_contents = self.manifest_path.read_bytes()

        updated, previous_version = synchronize_manifest_version(self.manifest_path, "1.1.7.54")

        self.assertFalse(updated)
        self.assertEqual("1.1.7.54", previous_version)
        self.assertEqual(original_contents, self.manifest_path.read_bytes())

    def test_manifest_identity_can_differ_from_project_and_dll_names(self) -> None:
        self._write_manifest({
            "id": "company.plugin",
            "name": "Company Plugin",
            "dllpath": "DifferentAssembly.dll",
        })

        summary = validate_plugin_manifest(self.manifest_path)

        self.assertEqual("company.plugin", summary.plugin_id)
        self.assertEqual("DifferentAssembly.dll", summary.dll_path)

    def test_manifest_id_cannot_be_a_path(self) -> None:
        self._write_manifest({"id": "../Other", "dllpath": "Plugin.dll"})

        with self.assertRaisesRegex(ValueError, "single 1-64 character directory name"):
            validate_plugin_manifest(self.manifest_path)

    def test_manifest_dll_path_cannot_escape_plugin_output(self) -> None:
        self._write_manifest({"id": "company.plugin", "dllpath": "../Host.dll"})

        with self.assertRaisesRegex(ValueError, "must stay inside the plugin directory"):
            validate_plugin_manifest(self.manifest_path)

    def test_package_root_uses_manifest_id_and_manifest_dll_resolves_independently(self) -> None:
        plugin_root = Path(self._temp_directory.name) / "plugin"
        output_dir = Path(self._temp_directory.name) / "output"
        plugin_root.mkdir()
        output_dir.mkdir()
        manifest_path = plugin_root / "manifest.json"
        manifest_path.write_text(json.dumps({
            "id": "company.plugin",
            "name": "Company Plugin",
            "dllpath": "DifferentAssembly.dll",
        }), encoding="utf-8")
        (output_dir / "DifferentAssembly.dll").write_bytes(b"plugin")

        summary = validate_plugin_manifest(manifest_path)
        primary_dll = resolve_primary_dll_path(output_dir, "ProjectName", summary)
        package_path = Path(self._temp_directory.name) / "company.plugin-1.0.cvxp"
        package_plugin(output_dir, plugin_root, set(), package_path, summary.plugin_id)

        self.assertEqual(output_dir / "DifferentAssembly.dll", primary_dll)
        with zipfile.ZipFile(package_path) as archive:
            self.assertIn("company.plugin/manifest.json", archive.namelist())
            self.assertIn("company.plugin/DifferentAssembly.dll", archive.namelist())
            self.assertFalse(any(name.startswith("ProjectName/") for name in archive.namelist()))

    def test_invalid_json_is_rejected_before_packaging(self) -> None:
        self.manifest_path.write_text('{"id": "broken",}', encoding="utf-8")

        with self.assertRaisesRegex(ValueError, "invalid UTF-8 JSON"):
            validate_plugin_manifest(self.manifest_path)

    def test_repository_plugin_and_project_paths_enable_live_shared_file_gate(self) -> None:
        self.assertTrue(is_repository_package_project(REPO_ROOT / "Plugins" / "Sample" / "Sample.csproj"))
        self.assertTrue(is_repository_package_project(REPO_ROOT / "Projects" / "Sample" / "Sample.csproj"))
        self.assertFalse(is_repository_package_project(Path(self._temp_directory.name) / "Sample.csproj"))
        self.assertFalse(is_repository_package_project(None))

    def test_default_shared_file_gate_fails_closed_on_runtime_drift(self) -> None:
        host_root = Path(self._temp_directory.name) / "host"
        host_root.mkdir()
        (host_root / "Current.dll").write_bytes(b"runtime")
        shared_files_path = Path(self._temp_directory.name) / "shared_files.json"
        shared_files_path.write_text(json.dumps({"shared_files": ["Old.dll"]}), encoding="utf-8")

        with self.assertRaisesRegex(RuntimeError, "manifest-only=1, runtime-only=1"):
            ensure_default_shared_files_are_current(shared_files_path, host_root)

    def _write_manifest(self, manifest: dict) -> None:
        self.manifest_path.write_text(json.dumps(manifest), encoding="utf-8")


if __name__ == "__main__":
    unittest.main()

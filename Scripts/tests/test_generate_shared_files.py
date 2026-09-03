import json
import tempfile
import unittest
from pathlib import Path

from Scripts.generate_shared_files import (
    build_release_manifest,
    check_manifest,
    collect_shared_files,
    compare_shared_file_sets,
    write_manifest_if_changed,
)


class SharedFilesGeneratorTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="shared-files-tests-")
        self.root_dir = Path(self._temp_directory.name)

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    def test_collect_is_deterministic_and_excludes_runtime_generated_content(self) -> None:
        (self.root_dir / "nested").mkdir()
        (self.root_dir / "Plugins").mkdir()
        (self.root_dir / "LOG").mkdir()
        (self.root_dir / "window-resize-traces").mkdir()
        (self.root_dir / "Host.dll").write_bytes(b"host")
        (self.root_dir / "window-resize-diagnostics.mode").write_text("compact", encoding="utf-8")
        (self.root_dir / "nested" / "Resource.resources.dll").write_bytes(b"resource")
        (self.root_dir / "Plugins" / "Plugin.dll").write_bytes(b"plugin")
        (self.root_dir / "LOG" / "runtime.log").write_bytes(b"log")
        (self.root_dir / "window-resize-traces" / "resize.json").write_text("{}", encoding="utf-8")

        self.assertEqual(
            ["Host.dll", "nested/Resource.resources.dll"],
            collect_shared_files(self.root_dir),
        )

    def test_check_ignores_timestamp_order_and_duplicate_entries(self) -> None:
        (self.root_dir / "A.dll").write_bytes(b"a")
        (self.root_dir / "B.dll").write_bytes(b"b")
        manifest_path = self.root_dir / "shared_files.json"
        manifest_path.write_text(json.dumps({
            "version": 1,
            "generated_at": "2000-01-01T00:00:00+00:00",
            "shared_files": ["B.dll", "A.dll", "A.dll"],
        }), encoding="utf-8")

        manifest_only, runtime_only = check_manifest(self.root_dir, manifest_path)

        self.assertEqual(set(), manifest_only)
        self.assertEqual(set(), runtime_only)

    def test_compare_reports_both_drift_directions(self) -> None:
        manifest_only, runtime_only = compare_shared_file_sets(
            ["Host.dll", "new/Runtime.dll"],
            ["Host.dll", "old/Runtime.dll"],
        )

        self.assertEqual({"old/Runtime.dll"}, manifest_only)
        self.assertEqual({"new/Runtime.dll"}, runtime_only)

    def test_unchanged_set_does_not_rewrite_generated_at(self) -> None:
        manifest_path = self.root_dir / "shared_files.json"
        original_manifest = {
            "version": 1,
            "generated_at": "2000-01-01T00:00:00+00:00",
            "shared_files": ["Host.dll"],
        }
        manifest_path.write_text(json.dumps(original_manifest), encoding="utf-8")
        original_bytes = manifest_path.read_bytes()

        was_updated = write_manifest_if_changed(manifest_path, {
            "version": 1,
            "generated_at": "2099-01-01T00:00:00+00:00",
            "shared_files": ["Host.dll"],
        })

        self.assertFalse(was_updated)
        self.assertEqual(original_bytes, manifest_path.read_bytes())

    def test_release_manifest_is_version_pinned_and_excludes_plugins(self) -> None:
        (self.root_dir / "Host.dll").write_bytes(b"host")
        (self.root_dir / "Plugins").mkdir()
        (self.root_dir / "Plugins" / "Private.dll").write_bytes(b"plugin")
        manifest = build_release_manifest(self.root_dir, "1.4.14.1", delivered_files=["Host.dll", "Plugins/Private.dll"])
        self.assertEqual("1.4.14.1", manifest["host_version"])
        self.assertEqual("x64", manifest["platform"])
        self.assertEqual("net10.0-windows", manifest["framework"])
        self.assertEqual(["Host.dll"], manifest["shared_files"])

    def test_release_manifest_rejects_latest_or_empty_output(self) -> None:
        for version in ("latest", "../1.2.3.4", "1.2.3", "1.2.3.4"):
            with self.subTest(version=version), self.assertRaises(ValueError):
                build_release_manifest(self.root_dir, version, delivered_files=[])

    def test_release_manifest_only_lists_delivered_host_files(self) -> None:
        (self.root_dir / "Host.dll").write_bytes(b"host")
        (self.root_dir / "NotDelivered.dll").write_bytes(b"output only")
        manifest = build_release_manifest(self.root_dir, "1.4.14.1", delivered_files=["host.DLL", "Missing.dll"])
        self.assertEqual(["Host.dll"], manifest["shared_files"])

    def test_release_manifest_rejects_empty_delivery_intersection(self) -> None:
        (self.root_dir / "Host.dll").write_bytes(b"host")
        with self.assertRaises(ValueError):
            build_release_manifest(self.root_dir, "1.4.14.1", delivered_files=["NotPresent.dll"])


if __name__ == "__main__":
    unittest.main()

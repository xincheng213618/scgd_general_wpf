import io
import tempfile
import unittest
from pathlib import Path

from package_publish import (
    extract_package_version,
    finalize_plugin_publish,
    load_manifest,
    persist_plugin_metadata,
    save_package_file,
    validate_html_upload_request,
)
from storage_uploads import UploadTooLargeError, UploadWorkflowError, store_legacy_upload


class _FakeUpload:
    def __init__(self, filename: str, payload: bytes = b"payload"):
        self.filename = filename
        self._payload = payload

    def save(self, target_path: str) -> None:
        Path(target_path).write_bytes(self._payload)


class UploadServiceTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.storage = Path(self.temp_dir.name) / "storage"
        (self.storage / "Plugins").mkdir(parents=True, exist_ok=True)

    def tearDown(self):
        self.temp_dir.cleanup()

    @staticmethod
    def _sanitize_filename(filename: str) -> str:
        return Path(filename).name

    @staticmethod
    def _validate_plugin_id(plugin_id: str) -> bool:
        return plugin_id.replace("_", "").replace("-", "").isalnum()

    @staticmethod
    def _validate_version(version: str) -> bool:
        parts = version.split(".")
        return bool(version) and all(part.isdigit() for part in parts)

    @staticmethod
    def _version_tuple(version: str) -> tuple[int, ...]:
        return tuple(int(part) for part in version.split(".") if part.isdigit())

    def test_validate_html_upload_request_infers_plugin_id(self):
        upload = _FakeUpload("DemoPlugin-1.2.3.cvxp")

        request = validate_html_upload_request(
            upload,
            "",
            sanitize_filename=self._sanitize_filename,
            validate_plugin_id=self._validate_plugin_id,
            validate_version=self._validate_version,
        )

        self.assertEqual(request.plugin_id, "DemoPlugin")
        self.assertEqual(request.version, "1.2.3")
        self.assertEqual(request.safe_filename, "DemoPlugin-1.2.3.cvxp")

    def test_save_package_file_updates_latest_release_and_reconciles_history(self):
        plugin_dir = self.storage / "Plugins" / "DemoPlugin"
        plugin_dir.mkdir(parents=True, exist_ok=True)
        (plugin_dir / "LATEST_RELEASE").write_text("1.0.0", encoding="utf-8")
        moved_plugins: list[str] = []

        result = save_package_file(
            self.storage,
            _FakeUpload("DemoPlugin-1.2.3.cvxp", b"new-package"),
            validate_html_upload_request(
                _FakeUpload("DemoPlugin-1.2.3.cvxp", b"new-package"),
                "DemoPlugin",
                sanitize_filename=self._sanitize_filename,
                validate_plugin_id=self._validate_plugin_id,
                validate_version=self._validate_version,
            ),
            validate_plugin_id=self._validate_plugin_id,
            read_text_file=lambda path: path.read_text(encoding="utf-8") if path.exists() else None,
            version_tuple=self._version_tuple,
            reconcile_plugin_package_history=lambda plugin_id: moved_plugins.append(plugin_id) or [],
        )

        self.assertEqual((plugin_dir / "LATEST_RELEASE").read_text(encoding="utf-8"), "1.2.3")
        self.assertTrue(result.save_path.exists())
        self.assertEqual(moved_plugins, ["DemoPlugin"])

    def test_persist_plugin_metadata_writes_manifest_changelog_and_icon(self):
        plugin_dir = self.storage / "Plugins" / "MetaPlugin"
        plugin_dir.mkdir(parents=True, exist_ok=True)

        persist_plugin_metadata(
            plugin_dir,
            plugin_id="MetaPlugin",
            version="2.0.0",
            name="Meta Plugin",
            description="demo",
            author="copilot",
            category="Tools",
            requires_version="2026.03",
            changelog_text="## 2.0.0\n- added",
            icon_file=_FakeUpload("PackageIcon.png", b"png"),
            manifest_loader=load_manifest,
        )

        manifest = load_manifest(plugin_dir / "manifest.json")
        self.assertEqual(manifest["id"], "MetaPlugin")
        self.assertEqual(manifest["name"], "Meta Plugin")
        self.assertEqual(manifest["version"], "2.0.0")
        self.assertEqual(manifest["requires"], "2026.03")
        self.assertTrue((plugin_dir / "CHANGELOG.md").exists())
        self.assertTrue((plugin_dir / "PackageIcon.png").exists())

    def test_finalize_plugin_publish_refreshes_cache_then_prewarms(self):
        events: list[tuple[str, object]] = []

        finalize_plugin_publish(
            self.storage,
            plugin_id="WarmPlugin",
            version="1.0.0",
            refresh_related_caches=lambda **kwargs: events.append(("refresh", kwargs["plugin_id"])),
            prewarm_plugin_metadata=lambda *args, **kwargs: events.append(("prewarm", args[1])),
            get_download_counts=lambda: {"WarmPlugin": 3},
            get_cache_entry=lambda *args, **kwargs: None,
            set_cache_entry=lambda *args, **kwargs: None,
            ttl_seconds=300,
        )

        self.assertEqual(events, [("refresh", "WarmPlugin"), ("prewarm", "WarmPlugin")])

    def test_store_legacy_upload_rejects_oversized_payload(self):
        with self.assertRaises(UploadTooLargeError):
            store_legacy_upload(
                storage=self.storage,
                raw_filepath="ColorVision/Plugins/DemoPlugin/DemoPlugin-1.0.0.cvxp",
                stream=io.BytesIO(b"0123456789"),
                max_size=4,
                normalize_relative_path=lambda value: value.replace("\\", "/").strip("/"),
                validate_plugin_id=self._validate_plugin_id,
                extract_package_version=lambda filename, plugin_id: extract_package_version(
                    filename,
                    plugin_id,
                    sanitize_filename=self._sanitize_filename,
                    validate_version=self._validate_version,
                ),
                is_root_release_file=lambda path: False,
                reconcile_app_release_history=lambda: [],
                reconcile_plugin_package_history=lambda plugin_id: [],
                prune_update_packages=lambda storage: None,
                refresh_related_caches=lambda **kwargs: None,
            )

    def test_store_legacy_upload_rejects_invalid_plugin_package_filename(self):
        with self.assertRaises(UploadWorkflowError) as context:
            store_legacy_upload(
                storage=self.storage,
                raw_filepath="ColorVision/Plugins/DemoPlugin/bad-name.cvxp",
                stream=io.BytesIO(b"abc"),
                max_size=32,
                normalize_relative_path=lambda value: value.replace("\\", "/").strip("/"),
                validate_plugin_id=self._validate_plugin_id,
                extract_package_version=lambda filename, plugin_id: extract_package_version(
                    filename,
                    plugin_id,
                    sanitize_filename=self._sanitize_filename,
                    validate_version=self._validate_version,
                ),
                is_root_release_file=lambda path: False,
                reconcile_app_release_history=lambda: [],
                reconcile_plugin_package_history=lambda plugin_id: [],
                prune_update_packages=lambda storage: None,
                refresh_related_caches=lambda **kwargs: None,
            )

        self.assertEqual(context.exception.status_code, 400)
        self.assertIn("Invalid plugin package filename", context.exception.message)

    def test_store_legacy_upload_normalizes_windows_update_paths_and_prunes(self):
        events: list[tuple[str, object]] = []

        result = store_legacy_upload(
            storage=self.storage,
            raw_filepath=r"ColorVision\Update\ColorVision-Update-[1.2.3.4].cvx",
            stream=io.BytesIO(b"incremental"),
            max_size=64,
            normalize_relative_path=lambda value: value.replace("\\", "/").strip("/"),
            validate_plugin_id=self._validate_plugin_id,
            extract_package_version=lambda filename, plugin_id: extract_package_version(
                filename,
                plugin_id,
                sanitize_filename=self._sanitize_filename,
                validate_version=self._validate_version,
            ),
            is_root_release_file=lambda path: False,
            reconcile_app_release_history=lambda: [],
            reconcile_plugin_package_history=lambda plugin_id: [],
            prune_update_packages=lambda storage: events.append(("prune", storage)),
            refresh_related_caches=lambda **kwargs: events.append(("refresh", kwargs["relative_path"])),
        )

        expected_path = self.storage / "Update" / "ColorVision-Update-[1.2.3.4].cvx"
        self.assertEqual(result.normalized_path, "Update/ColorVision-Update-[1.2.3.4].cvx")
        self.assertEqual(result.target, expected_path)
        self.assertTrue(expected_path.exists())
        self.assertEqual(events, [("prune", self.storage), ("refresh", "Update/ColorVision-Update-[1.2.3.4].cvx")])


if __name__ == "__main__":
    unittest.main()




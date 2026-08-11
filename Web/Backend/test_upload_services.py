import io
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from werkzeug.exceptions import Forbidden

from package_publish import (
    extract_package_version,
    finalize_plugin_publish,
    load_manifest,
    persist_plugin_metadata,
    save_package_file,
    validate_html_upload_request,
)
from storage_uploads import UploadTooLargeError, UploadWorkflowError, store_legacy_upload
from storage_paths import normalize_relative_path


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

    def test_save_package_file_does_not_downgrade_latest_release(self):
        plugin_dir = self.storage / "Plugins" / "DemoPlugin"
        plugin_dir.mkdir(parents=True, exist_ok=True)
        latest_release = plugin_dir / "LATEST_RELEASE"
        latest_release.write_text("2.0.0", encoding="utf-8")

        result = save_package_file(
            self.storage,
            _FakeUpload("DemoPlugin-1.9.9.cvxp", b"older-package"),
            validate_html_upload_request(
                _FakeUpload("DemoPlugin-1.9.9.cvxp"),
                "DemoPlugin",
                sanitize_filename=self._sanitize_filename,
                validate_plugin_id=self._validate_plugin_id,
                validate_version=self._validate_version,
            ),
            validate_plugin_id=self._validate_plugin_id,
            read_text_file=lambda path: path.read_text(encoding="utf-8") if path.exists() else None,
            version_tuple=self._version_tuple,
            reconcile_plugin_package_history=lambda plugin_id: [],
        )

        self.assertEqual(latest_release.read_text(encoding="utf-8"), "2.0.0")
        self.assertEqual(result.save_path.read_bytes(), b"older-package")

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

    def test_store_legacy_upload_interruption_preserves_live_marker_and_cleans_temp(self):
        marker = self.storage / "LATEST_RELEASE"
        marker.write_bytes(b"1.2.3.3")
        observations: list[bytes] = []

        class InterruptedStream:
            reads = 0

            def read(self, _size):
                self.reads += 1
                observations.append(marker.read_bytes())
                if self.reads == 1:
                    return b"1.2"
                raise OSError("simulated disconnect")

        with self.assertRaises(UploadWorkflowError) as context:
            store_legacy_upload(
                storage=self.storage,
                raw_filepath="ColorVision/LATEST_RELEASE",
                stream=InterruptedStream(),
                max_size=64,
                normalize_relative_path=lambda value: value.replace("\\", "/").strip("/"),
                validate_plugin_id=self._validate_plugin_id,
                extract_package_version=lambda filename, plugin_id: None,
                is_root_release_file=lambda path: False,
                reconcile_app_release_history=lambda: [],
                reconcile_plugin_package_history=lambda plugin_id: [],
                prune_update_packages=lambda storage: None,
                refresh_related_caches=lambda **kwargs: None,
            )

        self.assertEqual(500, context.exception.status_code)
        self.assertEqual([b"1.2.3.3", b"1.2.3.3"], observations)
        self.assertEqual(b"1.2.3.3", marker.read_bytes())
        self.assertEqual([], list(self.storage.glob(".LATEST_RELEASE.*.uploading")))

    def test_store_legacy_upload_oversize_preserves_existing_payload(self):
        target = self.storage / "Update" / "ColorVision-Update-[1.2.3.4].cvx"
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(b"previous verified payload")

        with self.assertRaises(UploadTooLargeError):
            store_legacy_upload(
                storage=self.storage,
                raw_filepath="ColorVision/Update/ColorVision-Update-[1.2.3.4].cvx",
                stream=io.BytesIO(b"payload exceeds the configured limit"),
                max_size=4,
                normalize_relative_path=lambda value: value.replace("\\", "/").strip("/"),
                validate_plugin_id=self._validate_plugin_id,
                extract_package_version=lambda filename, plugin_id: None,
                is_root_release_file=lambda path: False,
                reconcile_app_release_history=lambda: [],
                reconcile_plugin_package_history=lambda plugin_id: [],
                prune_update_packages=lambda storage: None,
                refresh_related_caches=lambda **kwargs: None,
            )

        self.assertEqual(b"previous verified payload", target.read_bytes())
        self.assertEqual([], list(target.parent.glob(f".{target.name}.*.uploading")))

    def test_store_legacy_upload_replace_failure_preserves_existing_marker_and_cleans_temp(self):
        marker = self.storage / "LATEST_RELEASE"
        marker.write_bytes(b"1.2.3.3")

        with (
            mock.patch("storage_uploads.os.replace", side_effect=OSError("replace failed")),
            self.assertRaises(UploadWorkflowError) as context,
        ):
            store_legacy_upload(
                storage=self.storage,
                raw_filepath="ColorVision/LATEST_RELEASE",
                stream=io.BytesIO(b"1.2.3.4"),
                max_size=64,
                normalize_relative_path=lambda value: value.replace("\\", "/").strip("/"),
                validate_plugin_id=self._validate_plugin_id,
                extract_package_version=lambda filename, plugin_id: None,
                is_root_release_file=lambda path: False,
                reconcile_app_release_history=lambda: [],
                reconcile_plugin_package_history=lambda plugin_id: [],
                prune_update_packages=lambda storage: None,
                refresh_related_caches=lambda **kwargs: None,
            )

        self.assertEqual(500, context.exception.status_code)
        self.assertEqual(b"1.2.3.3", marker.read_bytes())
        self.assertEqual([], list(self.storage.glob(".LATEST_RELEASE.*.uploading")))

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

    def test_store_legacy_upload_rejects_case_variant_path_escape_without_side_effects(self):
        temp_root = Path(self.temp_dir.name)
        paths = (
            "ColorVision/../escaped-canonical.bin",
            r"cOlOrViSiOn\..\escaped-case-variant.bin",
        )

        for raw_filepath in paths:
            with self.subTest(raw_filepath=raw_filepath):
                effects: list[str] = []
                files_before = {
                    path.relative_to(temp_root).as_posix()
                    for path in temp_root.rglob("*")
                }

                with self.assertRaises(Forbidden):
                    store_legacy_upload(
                        storage=self.storage,
                        raw_filepath=raw_filepath,
                        stream=io.BytesIO(b"must-not-be-written"),
                        max_size=64,
                        normalize_relative_path=normalize_relative_path,
                        validate_plugin_id=self._validate_plugin_id,
                        extract_package_version=lambda filename, plugin_id: None,
                        is_root_release_file=lambda path: effects.append("root-release") or False,
                        reconcile_app_release_history=lambda: effects.append("app-history") or [],
                        reconcile_plugin_package_history=lambda plugin_id: effects.append("plugin-history") or [],
                        prune_update_packages=lambda storage: effects.append("prune"),
                        refresh_related_caches=lambda **kwargs: effects.append("refresh"),
                        on_upload_complete=lambda normalized: effects.append("complete"),
                    )

                files_after = {
                    path.relative_to(temp_root).as_posix()
                    for path in temp_root.rglob("*")
                }
                escaped_name = Path(raw_filepath.replace("\\", "/")).name
                self.assertFalse((temp_root / escaped_name).exists())
                self.assertEqual(files_after, files_before)
                self.assertEqual(effects, [])

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




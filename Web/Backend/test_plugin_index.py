"""Tests for plugin index service and admin API endpoints."""

import base64
import copy
import io
import json
import os
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

import app as marketplace_app
from db_cache import CacheManager, now_ts


class PluginIndexTests(unittest.TestCase):
    """Tests for the plugin index service and related admin APIs."""

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.storage = self.root / "storage"
        (self.storage / "Plugins").mkdir(parents=True, exist_ok=True)

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)
        self.original_testing = marketplace_app.app.config.get("TESTING", False)
        self.original_secret_key = marketplace_app.app.secret_key

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {
            "username": "tester",
            "password": "secret",
        }
        marketplace_app.CONFIG["secret_key"] = "test-secret-key"
        marketplace_app.CONFIG["debug"] = False
        marketplace_app.app.secret_key = marketplace_app.CONFIG["secret_key"]
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.app.config["MAX_CONTENT_LENGTH"] = marketplace_app.MAX_UPLOAD_SIZE_BYTES
        marketplace_app.init_db()

        self.cache = CacheManager(self.root / "marketplace.db")
        self.cache.init_db()

        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.secret_key = self.original_secret_key
        marketplace_app.app.config["TESTING"] = self.original_testing
        self.temp_dir.cleanup()

    def _auth_headers(self, username: str = "tester", password: str = "secret"):
        token = base64.b64encode(f"{username}:{password}".encode("utf-8")).decode("ascii")
        return {"Authorization": f"Basic {token}"}

    def _create_plugin(self, plugin_id: str = "TestPlugin", version: str = "1.0.0"):
        plugin_dir = self.storage / "Plugins" / plugin_id
        plugin_dir.mkdir(parents=True, exist_ok=True)
        (plugin_dir / "LATEST_RELEASE").write_text(version, encoding="utf-8")
        (plugin_dir / f"{plugin_id}-{version}.cvxp").write_bytes(b"test-package")
        (plugin_dir / "manifest.json").write_text(
            json.dumps({"id": plugin_id, "name": f"{plugin_id} Name", "description": "test"}),
            encoding="utf-8",
        )
        return plugin_dir

    def _rewrite_with_new_stat(self, path: Path, payload: bytes | str) -> None:
        """Rewrite a fixture and force a distinct NTFS metadata signature."""
        before = path.stat()
        if isinstance(payload, bytes):
            path.write_bytes(payload)
        else:
            path.write_text(payload, encoding="utf-8")
        forced_mtime_ns = before.st_mtime_ns + 2_000_000_000
        os.utime(path, ns=(forced_mtime_ns, forced_mtime_ns))
        after = path.stat()
        self.assertNotEqual(
            (before.st_size, before.st_mtime_ns),
            (after.st_size, after.st_mtime_ns),
        )

    # -------------------------------------------------------------------
    # Plugin index refresh tests
    # -------------------------------------------------------------------

    def test_refresh_all_plugin_index_writes_to_tables(self):
        self._create_plugin("PluginA", "1.0.0")
        self._create_plugin("PluginB", "2.0.0")

        from services.plugin_index import refresh_all_plugin_index
        result = refresh_all_plugin_index(self.cache, self.storage)

        self.assertEqual(result["indexed_count"], 2)
        self.assertEqual(result["deleted_count"], 0)
        self.assertEqual(len(result["errors"]), 0)

        # Verify plugin_index table
        db = self.cache.get_db()
        rows = db.execute("SELECT * FROM plugin_index WHERE is_deleted = 0").fetchall()
        db.close()
        self.assertEqual(len(rows), 2)
        plugin_ids = {r["plugin_id"] for r in rows}
        self.assertEqual(plugin_ids, {"PluginA", "PluginB"})

    def test_refresh_all_plugin_index_marks_deleted_plugins(self):
        self._create_plugin("ExistingPlugin", "1.0.0")

        from services.plugin_index import refresh_all_plugin_index
        refresh_all_plugin_index(self.cache, self.storage)

        # Verify it's in the index
        db = self.cache.get_db()
        row = db.execute(
            "SELECT * FROM plugin_index WHERE plugin_id = 'ExistingPlugin'"
        ).fetchone()
        self.assertEqual(row["is_deleted"], 0)
        db.close()

        # Remove the plugin directory
        import shutil
        shutil.rmtree(self.storage / "Plugins" / "ExistingPlugin")

        # Refresh again
        result = refresh_all_plugin_index(self.cache, self.storage)
        self.assertEqual(result["deleted_count"], 1)

        db = self.cache.get_db()
        row = db.execute(
            "SELECT * FROM plugin_index WHERE plugin_id = 'ExistingPlugin'"
        ).fetchone()
        self.assertEqual(row["is_deleted"], 1)
        db.close()

    def test_refresh_single_plugin_writes_index(self):
        self._create_plugin("SinglePlugin", "1.0.0")

        from services.plugin_index import refresh_plugin_index
        result = refresh_plugin_index(self.cache, self.storage, "SinglePlugin")

        self.assertIsNotNone(result)
        self.assertEqual(result["plugin_id"], "SinglePlugin")
        self.assertEqual(result["latest_version"], "1.0.0")

        # Verify package_index
        db = self.cache.get_db()
        pkgs = db.execute(
            "SELECT * FROM package_index WHERE plugin_id = 'SinglePlugin' AND is_deleted = 0"
        ).fetchall()
        db.close()
        self.assertGreaterEqual(len(pkgs), 1)

    def test_refresh_plugin_missing_dir_marks_deleted(self):
        from services.plugin_index import is_plugin_index_populated, refresh_plugin_index
        result = refresh_plugin_index(self.cache, self.storage, "NonExistent")
        self.assertIsNone(result)

        db = self.cache.get_db()
        row = db.execute(
            "SELECT * FROM plugin_index WHERE plugin_id = 'NonExistent'"
        ).fetchone()
        db.close()
        self.assertIsNotNone(row)
        self.assertEqual(row["is_deleted"], 1)
        self.assertFalse(is_plugin_index_populated(self.cache))

    # -------------------------------------------------------------------
    # API reads from index
    # -------------------------------------------------------------------

    def test_api_plugins_reads_from_plugin_index(self):
        self._create_plugin("IndexPlugin", "1.0.0")

        from services.plugin_index import refresh_all_plugin_index
        refresh_all_plugin_index(self.cache, self.storage)

        # Now the index is populated. API should read from it.
        # Patch disk scanning to prove it reads from index
        with patch("plugin_marketplace.scan_plugin_summaries", side_effect=AssertionError("should not scan disk")):
            response = self.client.get("/api/plugins")

        self.assertEqual(response.status_code, 200)
        items = response.get_json()["items"]
        self.assertTrue(any(item["pluginId"] == "IndexPlugin" for item in items))

    def test_api_plugins_falls_back_to_disk_scan_when_index_empty(self):
        self._create_plugin("FallbackPlugin", "1.0.0")

        # Index is empty, should fallback to disk scan
        response = self.client.get("/api/plugins")
        self.assertEqual(response.status_code, 200)
        items = response.get_json()["items"]
        self.assertTrue(any(item["pluginId"] == "FallbackPlugin" for item in items))

    # -------------------------------------------------------------------
    # Issue 1: Detail API compatibility after index refresh
    # -------------------------------------------------------------------

    def test_detail_api_compatible_after_index_refresh(self):
        """After refresh_all_plugin_index, /api/plugins/<id> must return 200
        with all required fields: latestVersion, requiresVersion, has_icon, etc."""
        plugin_dir = self._create_plugin("DetailPlugin", "2.0.0")
        (plugin_dir / "manifest.json").write_text(
            json.dumps({
                "id": "DetailPlugin",
                "name": "Detail Plugin",
                "description": "detail test",
                "requires": "2026.01",
                "author": "TestAuthor",
                "category": "Tools",
            }),
            encoding="utf-8",
        )

        from services.plugin_index import refresh_all_plugin_index
        refresh_all_plugin_index(self.cache, self.storage)

        response = self.client.get("/api/plugins/DetailPlugin")
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(payload["latestVersion"], "2.0.0")
        self.assertEqual(payload["requiresVersion"], "2026.01")
        self.assertEqual(payload["pluginId"], "DetailPlugin")
        self.assertEqual(payload["name"], "Detail Plugin")
        self.assertEqual(payload["author"], "TestAuthor")
        self.assertEqual(payload["category"], "Tools")
        self.assertIn("totalDownloads", payload)
        self.assertIn("readme", payload)
        self.assertIn("readmeHtml", payload)
        self.assertIn("changelog", payload)
        self.assertIn("changelogHtml", payload)
        self.assertIn("relatedDocs", payload)
        self.assertIn("versions", payload)
        self.assertIn("archivedVersions", payload)
        self.assertIn("currentPackageCount", payload)
        self.assertIn("historicalPackageCount", payload)
        self.assertIn("iconUrl", payload)

    # -------------------------------------------------------------------
    # Issue 2: fileHash present in detail after index refresh
    # -------------------------------------------------------------------

    def test_detail_returns_file_hash_after_index_refresh(self):
        """Detail endpoint must return fileHash even when reading from index."""
        plugin_dir = self._create_plugin("HashPlugin", "1.0.0")
        (plugin_dir / "manifest.json").write_text(
            json.dumps({"id": "HashPlugin", "name": "Hash Plugin"}),
            encoding="utf-8",
        )

        from services.plugin_index import refresh_all_plugin_index
        refresh_all_plugin_index(self.cache, self.storage)

        response = self.client.get("/api/plugins/HashPlugin")
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertTrue(payload["versions"], "Should have versions")
        self.assertTrue(payload["versions"][0].get("fileHash"), "fileHash should be present")

        db = self.cache.get_db()
        row = db.execute(
            "SELECT file_hash, file_signature FROM package_index WHERE plugin_id = 'HashPlugin' AND is_deleted = 0"
        ).fetchone()
        db.close()
        self.assertEqual(row["file_hash"], payload["versions"][0]["fileHash"])
        self.assertTrue(row["file_signature"])

    def test_detail_get_never_computes_missing_hash(self):
        self._create_plugin("PendingHash", "1.0.0")
        from services.plugin_index import refresh_all_plugin_index
        refresh_all_plugin_index(self.cache, self.storage)
        db = self.cache.get_db()
        db.execute("UPDATE package_index SET file_hash = '' WHERE plugin_id = 'PendingHash'")
        db.commit()
        db.close()

        with patch("plugin_marketplace._compute_file_hash", side_effect=AssertionError("GET must not hash")):
            response = self.client.get("/api/plugins/PendingHash")

        self.assertEqual(response.status_code, 200)
        version = response.get_json()["versions"][0]
        self.assertTrue(version["hashPending"])
        self.assertIsNone(version["fileHash"])

    def test_refresh_reuses_hash_when_package_signature_is_unchanged(self):
        self._create_plugin("ReuseHash", "1.0.0")
        from services.plugin_index import refresh_plugin_index
        refresh_plugin_index(self.cache, self.storage, "ReuseHash")

        with patch("plugin_marketplace._compute_file_hash", side_effect=AssertionError("unchanged file must reuse hash")):
            refresh_plugin_index(self.cache, self.storage, "ReuseHash")

    def test_refresh_recomputes_hash_when_package_signature_changes(self):
        plugin_dir = self._create_plugin("ChangedHash", "1.0.0")
        from services.plugin_index import refresh_plugin_index
        refresh_plugin_index(self.cache, self.storage, "ChangedHash")
        package = plugin_dir / "ChangedHash-1.0.0.cvxp"
        package.write_bytes(b"changed-package-payload")

        with patch("plugin_marketplace._compute_file_hash", return_value="replacement-hash") as compute_hash:
            refresh_plugin_index(self.cache, self.storage, "ChangedHash")

        compute_hash.assert_called_once_with(package)
        db = self.cache.get_db()
        row = db.execute(
            "SELECT file_hash FROM package_index WHERE plugin_id = 'ChangedHash' AND is_deleted = 0"
        ).fetchone()
        db.close()
        self.assertEqual(row["file_hash"], "replacement-hash")

    def test_changed_package_hash_failure_clears_stale_hash_and_reports_pending(self):
        plugin_dir = self._create_plugin("FailedHash", "1.0.0")
        from services.plugin_index import refresh_plugin_index
        refresh_plugin_index(self.cache, self.storage, "FailedHash")
        package = plugin_dir / "FailedHash-1.0.0.cvxp"
        package.write_bytes(b"changed-package-that-cannot-be-hashed")

        with patch("plugin_marketplace._compute_file_hash", return_value=None):
            refresh_plugin_index(self.cache, self.storage, "FailedHash")

        db = self.cache.get_db()
        row = db.execute(
            "SELECT file_hash, file_signature FROM package_index WHERE plugin_id = 'FailedHash' AND is_deleted = 0"
        ).fetchone()
        db.close()
        self.assertEqual(row["file_hash"], "")
        self.assertTrue(row["file_signature"])
        with patch("plugin_marketplace._compute_file_hash", side_effect=AssertionError("GET must not hash")):
            response = self.client.get("/api/plugins/FailedHash")
        version = response.get_json()["versions"][0]
        self.assertTrue(version["hashPending"])
        self.assertIsNone(version["fileHash"])

    def test_upsert_does_not_replace_good_hash_with_empty_value(self):
        self._create_plugin("KeepHash", "1.0.0")
        from services.plugin_index import _upsert_package, refresh_plugin_index
        refresh_plugin_index(self.cache, self.storage, "KeepHash")
        db = self.cache.get_db()
        original = db.execute(
            "SELECT * FROM package_index WHERE plugin_id = 'KeepHash' AND is_deleted = 0"
        ).fetchone()
        _upsert_package(
            db,
            "KeepHash",
            {
                "version": original["version"],
                "filename": original["filename"],
                "relative_path": original["relative_path"],
                "size": original["size"],
                "modified": original["modified"],
                "fileHash": "",
                "fileSignature": "",
            },
            "current",
            original["indexed_at"],
            self.storage,
        )
        db.commit()
        updated = db.execute(
            "SELECT file_hash, file_signature FROM package_index WHERE relative_path = ?",
            (original["relative_path"],),
        ).fetchone()
        db.close()

        self.assertEqual(updated["file_hash"], original["file_hash"])
        self.assertEqual(updated["file_signature"], original["file_signature"])

    def test_startup_check_backfills_active_packages_missing_hash(self):
        self._create_plugin("StartupHash", "1.0.0")
        from services.plugin_index import refresh_plugin_index
        from services.scheduler import _run_startup_check
        refresh_plugin_index(self.cache, self.storage, "StartupHash")
        db = self.cache.get_db()
        db.execute("UPDATE package_index SET file_hash = '' WHERE plugin_id = 'StartupHash'")
        db.commit()
        db.close()

        with patch("services.artifact_index.startup_check_all_indexes", return_value="artifacts ready"):
            summary = _run_startup_check(self.cache, self.storage, lambda: self.cache.get_db())

        db = self.cache.get_db()
        row = db.execute(
            "SELECT file_hash FROM package_index WHERE plugin_id = 'StartupHash' AND is_deleted = 0"
        ).fetchone()
        db.close()
        self.assertIn("hashes backfilled", summary)
        self.assertTrue(row["file_hash"])

    # -------------------------------------------------------------------
    # Issue 3: index_state.signature written after refresh
    # -------------------------------------------------------------------

    def test_index_state_signature_written_after_refresh(self):
        """refresh_all_plugin_index should write plugin_catalog_signature."""
        self._create_plugin("SigPlugin", "1.0.0")

        from services.plugin_index import refresh_all_plugin_index, get_plugin_index_state
        refresh_all_plugin_index(self.cache, self.storage)

        state = get_plugin_index_state(self.cache)
        self.assertIsNotNone(state)
        self.assertTrue(state.get("signature"), "signature should be non-empty")

    def test_plugin_index_check_no_change_skips_refresh(self):
        """Second plugin_index_check should detect no changes and skip refresh."""
        self._create_plugin("CheckPlugin", "1.0.0")

        from services.plugin_index import refresh_all_plugin_index
        from services.scheduler import _run_plugin_index_check
        refresh_all_plugin_index(self.cache, self.storage)

        # First check - signatures should match, no refresh needed
        result1 = _run_plugin_index_check(self.cache, self.storage, lambda: self.cache.get_db())
        self.assertIn("No changes detected", result1)

        # Second check - still no changes
        result2 = _run_plugin_index_check(self.cache, self.storage, lambda: self.cache.get_db())
        self.assertIn("No changes detected", result2)

    def test_plugin_summary_signature_is_stat_only_and_covers_summary_inputs(self):
        plugin_dir = self._create_plugin("SignatureOnly", "1.0.0")
        (plugin_dir / "README.md").write_text("readme", encoding="utf-8")
        (plugin_dir / "CHANGELOG.md").write_text("changelog", encoding="utf-8")
        history_dir = self.storage / "History" / "Plugins" / "SignatureOnly"
        history_dir.mkdir(parents=True)
        (history_dir / "SignatureOnly-0.9.0.cvxp").write_bytes(b"history")

        from plugin_marketplace import plugin_catalog_signature, plugin_summary_signature
        with patch("plugin_marketplace._compute_file_hash", side_effect=AssertionError("signature must not hash")) as compute_hash:
            summary_signature = plugin_summary_signature(self.storage, "SignatureOnly")
            catalog_signature = plugin_catalog_signature(self.storage)

        compute_hash.assert_not_called()
        self.assertIn("README.md:", summary_signature)
        self.assertIn("CHANGELOG.md:", summary_signature)
        self.assertIn("history:SignatureOnly-0.9.0.cvxp:", summary_signature)
        self.assertIn(summary_signature, catalog_signature)

    def test_periodic_check_refreshes_readme_and_changelog_edits(self):
        plugin_dir = self._create_plugin("DocsPlugin", "1.0.0")
        readme_path = plugin_dir / "README.md"
        changelog_path = plugin_dir / "CHANGELOG.md"
        readme_path.write_text("old readme", encoding="utf-8")
        changelog_path.write_text("old changelog", encoding="utf-8")

        from services.plugin_index import refresh_all_plugin_index
        from services.scheduler import _run_plugin_index_check
        refresh_all_plugin_index(self.cache, self.storage)

        self._rewrite_with_new_stat(readme_path, "new readme content")
        readme_result = _run_plugin_index_check(
            self.cache, self.storage, lambda: self.cache.get_db()
        )
        self.assertIn("Refreshed", readme_result)

        self._rewrite_with_new_stat(changelog_path, "new changelog content")
        changelog_result = _run_plugin_index_check(
            self.cache, self.storage, lambda: self.cache.get_db()
        )
        self.assertIn("Refreshed", changelog_result)

        db = self.cache.get_db()
        row = db.execute(
            "SELECT readme, changelog FROM plugin_index WHERE plugin_id = 'DocsPlugin'"
        ).fetchone()
        db.close()
        self.assertEqual(row["readme"], "new readme content")
        self.assertEqual(row["changelog"], "new changelog content")

    def test_periodic_check_detects_same_name_history_package_overwrite(self):
        self._create_plugin("HistoryOverwrite", "1.0.0")
        history_dir = self.storage / "History" / "Plugins" / "HistoryOverwrite"
        history_dir.mkdir(parents=True)
        package_path = history_dir / "HistoryOverwrite-0.9.0.cvxp"
        package_path.write_bytes(b"old")

        from services.plugin_index import refresh_all_plugin_index
        from services.scheduler import _run_plugin_index_check
        refresh_all_plugin_index(self.cache, self.storage)

        db = self.cache.get_db()
        before = db.execute(
            """SELECT size, file_hash, file_signature FROM package_index
               WHERE plugin_id = 'HistoryOverwrite'
                 AND filename = 'HistoryOverwrite-0.9.0.cvxp'
                 AND is_deleted = 0"""
        ).fetchone()
        db.close()

        # Keep both the path and size unchanged.  NTFS directory mtime and
        # package count are therefore insufficient; the file mtime must be in
        # the catalog signature.
        self._rewrite_with_new_stat(package_path, b"new")
        result = _run_plugin_index_check(
            self.cache, self.storage, lambda: self.cache.get_db()
        )
        self.assertIn("Refreshed", result)

        db = self.cache.get_db()
        row = db.execute(
            """SELECT size, file_hash, file_signature FROM package_index
               WHERE plugin_id = 'HistoryOverwrite'
                 AND filename = 'HistoryOverwrite-0.9.0.cvxp'
                 AND is_deleted = 0"""
        ).fetchone()
        db.close()
        self.assertEqual(row["size"], before["size"])
        self.assertNotEqual(row["file_signature"], before["file_signature"])
        self.assertNotEqual(row["file_hash"], before["file_hash"])

    def test_full_refresh_persists_pre_scan_signature_when_file_changes_mid_scan(self):
        plugin_dir = self._create_plugin("RacePlugin", "1.0.0")
        readme_path = plugin_dir / "README.md"
        readme_path.write_text("before scan", encoding="utf-8")

        import services.plugin_index as plugin_index_service
        from plugin_marketplace import plugin_catalog_signature
        from services.scheduler import _run_plugin_index_check

        pre_scan_signature = plugin_catalog_signature(self.storage)
        original_refresh = plugin_index_service.refresh_plugin_index

        def refresh_then_mutate(*args, **kwargs):
            summary = original_refresh(*args, **kwargs)
            self._rewrite_with_new_stat(readme_path, "changed during full refresh")
            return summary

        with patch(
            "services.plugin_index.refresh_plugin_index",
            side_effect=refresh_then_mutate,
        ):
            result = plugin_index_service.refresh_all_plugin_index(
                self.cache, self.storage
            )

        post_scan_signature = plugin_catalog_signature(self.storage)
        state = plugin_index_service.get_plugin_index_state(self.cache)
        self.assertNotEqual(pre_scan_signature, post_scan_signature)
        self.assertEqual(state["signature"], pre_scan_signature)
        self.assertTrue(result["changed_during_refresh"])

        db = self.cache.get_db()
        row = db.execute(
            "SELECT readme FROM plugin_index WHERE plugin_id = 'RacePlugin'"
        ).fetchone()
        db.close()
        self.assertEqual(row["readme"], "before scan")

        retry_result = _run_plugin_index_check(
            self.cache, self.storage, lambda: self.cache.get_db()
        )
        self.assertIn("Refreshed", retry_result)
        self.assertIn(
            "No changes detected",
            _run_plugin_index_check(
                self.cache, self.storage, lambda: self.cache.get_db()
            ),
        )

        db = self.cache.get_db()
        row = db.execute(
            "SELECT readme FROM plugin_index WHERE plugin_id = 'RacePlugin'"
        ).fetchone()
        db.close()
        self.assertEqual(row["readme"], "changed during full refresh")

    def test_full_refresh_single_flight_is_nonblocking(self):
        from services.plugin_index import (
            _PLUGIN_FULL_REFRESH_LOCK,
            refresh_all_plugin_index,
        )

        self.assertTrue(_PLUGIN_FULL_REFRESH_LOCK.acquire(blocking=False))
        try:
            result = refresh_all_plugin_index(self.cache, self.storage)
        finally:
            _PLUGIN_FULL_REFRESH_LOCK.release()

        self.assertEqual(result["status"], "skipped")
        self.assertEqual(result["reason"], "refresh_in_progress")

    # -------------------------------------------------------------------
    # Issue 5: Fine-grained scope enforcement
    # -------------------------------------------------------------------

    # -------------------------------------------------------------------
    # Publish triggers index refresh
    # -------------------------------------------------------------------

    def test_publish_triggers_plugin_index_refresh(self):
        # Seed the index first
        self._create_plugin("SeedPlugin", "1.0.0")
        from services.plugin_index import refresh_all_plugin_index
        refresh_all_plugin_index(self.cache, self.storage)

        # Now publish a new plugin
        response = self.client.post(
            "/api/packages/publish",
            headers=self._auth_headers(),
            data={
                "PluginId": "PublishedPlugin",
                "Version": "1.0.0",
                "Name": "Published Plugin",
                "Description": "auto-indexed",
                "package": (io.BytesIO(b"pkg"), "PublishedPlugin-1.0.0.cvxp"),
            },
            content_type="multipart/form-data",
        )
        self.assertEqual(response.status_code, 201)

        # Verify the new plugin is in the index
        db = self.cache.get_db()
        row = db.execute(
            "SELECT * FROM plugin_index WHERE plugin_id = 'PublishedPlugin'"
        ).fetchone()
        db.close()
        self.assertIsNotNone(row)
        self.assertEqual(row["is_deleted"], 0)
        self.assertEqual(row["latest_version"], "1.0.0")

    # -------------------------------------------------------------------
    # Admin API: cache/status
    # -------------------------------------------------------------------

    def test_admin_cache_status_returns_key_fields(self):
        response = self.client.get(
            "/api/admin/cache/status",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertIn("db_path", payload)
        self.assertIn("cache_entry_count", payload)
        self.assertIn("plugin_index_count", payload)
        self.assertIn("package_index_count", payload)
        self.assertIn("storage_path", payload)

    def test_admin_cache_status_requires_auth(self):
        response = self.client.get("/api/admin/cache/status")
        self.assertEqual(response.status_code, 401)

    # -------------------------------------------------------------------
    # Admin API: cache/cleanup
    # -------------------------------------------------------------------

    def test_admin_cache_cleanup_deletes_expired(self):
        # Insert an expired cache entry
        self.cache.set_cache_entry("expired_key", "value", ttl_seconds=0)

        response = self.client.post(
            "/api/admin/cache/cleanup",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertGreaterEqual(payload["deleted_count"], 1)

        # Verify it's gone
        self.assertIsNone(self.cache.get_cache_entry("expired_key"))

    def test_admin_cache_cleanup_requires_auth(self):
        response = self.client.post("/api/admin/cache/cleanup")
        self.assertEqual(response.status_code, 401)

    # -------------------------------------------------------------------
    # Admin API: index refresh
    # -------------------------------------------------------------------

    def test_admin_index_refresh_all(self):
        self._create_plugin("AdminPlugin", "1.0.0")

        response = self.client.post(
            "/api/admin/index/plugins/refresh",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertIn("indexed_count", payload)
        self.assertIn("deleted_count", payload)
        self.assertIn("duration_ms", payload)
        self.assertEqual(payload["indexed_count"], 1)

    def test_admin_index_refresh_requires_auth(self):
        response = self.client.post("/api/admin/index/plugins/refresh")
        self.assertEqual(response.status_code, 401)

    def test_admin_index_refresh_single_plugin(self):
        self._create_plugin("RefreshOne", "1.0.0")

        response = self.client.post(
            "/api/admin/index/plugins/RefreshOne/refresh",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(payload["pluginId"], "RefreshOne")
        self.assertEqual(payload["status"], "ok")

    def test_admin_index_refresh_single_plugin_not_found(self):
        response = self.client.post(
            "/api/admin/index/plugins/MissingPlugin/refresh",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(payload["status"], "not_found")

    # -------------------------------------------------------------------
    # Admin API: stats/overview
    # -------------------------------------------------------------------

    def test_admin_stats_overview(self):
        response = self.client.get(
            "/api/admin/stats/overview",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertIn("totalDownloads", payload)
        self.assertIn("pluginCount", payload)
        self.assertIn("packageCount", payload)

    # -------------------------------------------------------------------
    # Admin API: audit-log
    # -------------------------------------------------------------------

    def test_admin_audit_log_records_actions(self):
        # Perform an action that writes audit
        self.client.post(
            "/api/admin/cache/cleanup",
            headers=self._auth_headers(),
        )

        response = self.client.get(
            "/api/admin/audit-log",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        entries = response.get_json()["entries"]
        self.assertGreaterEqual(len(entries), 1)
        self.assertEqual(entries[0]["action"], "cache_cleanup")

    # -------------------------------------------------------------------
    # Admin API: jobs
    # -------------------------------------------------------------------

    def test_admin_jobs_list(self):
        from services.scheduler import ensure_default_jobs
        ensure_default_jobs(self.cache)

        response = self.client.get(
            "/api/admin/jobs",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        jobs = response.get_json()
        self.assertGreaterEqual(len(jobs), 1)
        job_ids = {j["id"] for j in jobs}
        self.assertIn("cache_cleanup", job_ids)

    def test_admin_job_run(self):
        from services.scheduler import ensure_default_jobs
        ensure_default_jobs(self.cache)

        response = self.client.post(
            "/api/admin/jobs/cache_cleanup/run",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(payload["job_id"], "cache_cleanup")
        self.assertIn(payload["status"], ("success", "error"))

        # Verify job_runs record exists
        db = self.cache.get_db()
        row = db.execute(
            "SELECT * FROM job_runs WHERE job_id = 'cache_cleanup' ORDER BY id DESC LIMIT 1"
        ).fetchone()
        db.close()
        self.assertIsNotNone(row)
        self.assertIn(row["status"], ("success", "error"))

    def test_admin_job_enable_disable(self):
        from services.scheduler import ensure_default_jobs
        ensure_default_jobs(self.cache)

        # Disable
        response = self.client.post(
            "/api/admin/jobs/cache_cleanup/disable",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.get_json()["status"], "disabled")

        # Enable
        response = self.client.post(
            "/api/admin/jobs/cache_cleanup/enable",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.get_json()["status"], "enabled")

    # -------------------------------------------------------------------
    # HTTP caching for plugin icon
    # -------------------------------------------------------------------

    def test_plugin_icon_returns_etag(self):
        plugin_dir = self.storage / "Plugins" / "IconPlugin"
        plugin_dir.mkdir(parents=True, exist_ok=True)
        (plugin_dir / "PackageIcon.png").write_bytes(b"icon-bytes")

        response = self.client.get("/plugins/IconPlugin/icon")
        self.assertEqual(response.status_code, 200)
        self.assertIsNotNone(response.headers.get("ETag"))
        self.assertIsNotNone(response.headers.get("Last-Modified"))
        self.assertEqual(response.get_data(), b"icon-bytes")

    def test_plugin_icon_returns_304_on_matching_etag(self):
        plugin_dir = self.storage / "Plugins" / "IconPlugin"
        plugin_dir.mkdir(parents=True, exist_ok=True)
        (plugin_dir / "PackageIcon.png").write_bytes(b"icon-bytes")

        first = self.client.get("/plugins/IconPlugin/icon")
        etag = first.headers.get("ETag")
        self.assertIsNotNone(etag)

        second = self.client.get(
            "/plugins/IconPlugin/icon",
            headers={"If-None-Match": etag},
        )
        self.assertEqual(second.status_code, 304)

    # -------------------------------------------------------------------
    # DB schema: new tables exist
    # -------------------------------------------------------------------

    def test_new_tables_exist(self):
        db = self.cache.get_db()
        tables = {row[0] for row in db.execute(
            "SELECT name FROM sqlite_master WHERE type='table'"
        ).fetchall()}
        db.close()

        for table in ("plugin_index", "package_index", "index_state",
                       "users", "api_keys", "audit_log",
                       "scheduled_jobs", "job_runs"):
            self.assertIn(table, tables, f"Table {table} should exist")

    def test_db_uses_wal_mode(self):
        db = self.cache.get_db()
        mode = db.execute("PRAGMA journal_mode").fetchone()[0]
        db.close()
        self.assertEqual(mode, "wal")

    # -------------------------------------------------------------------
    # Index state tracking
    # -------------------------------------------------------------------

    def test_index_state_updated_after_refresh(self):
        self._create_plugin("StatePlugin", "1.0.0")

        from services.plugin_index import refresh_all_plugin_index, get_plugin_index_state
        refresh_all_plugin_index(self.cache, self.storage)

        state = get_plugin_index_state(self.cache)
        self.assertIsNotNone(state)
        self.assertEqual(state["status"], "ready")
        self.assertGreater(state["item_count"], 0)

    # -------------------------------------------------------------------
    # Audit log writes on publish
    # -------------------------------------------------------------------

    def test_publish_writes_audit_log(self):
        response = self.client.post(
            "/api/packages/publish",
            headers=self._auth_headers(),
            data={
                "PluginId": "AuditPlugin",
                "Version": "1.0.0",
                "Name": "Audit Plugin",
                "package": (io.BytesIO(b"pkg"), "AuditPlugin-1.0.0.cvxp"),
            },
            content_type="multipart/form-data",
        )
        self.assertEqual(response.status_code, 201)

        entries = self.cache.get_audit_log(action="index_refresh_plugin")
        self.assertGreaterEqual(len(entries), 1)
        self.assertEqual(entries[0]["target_id"], "AuditPlugin")

    # -------------------------------------------------------------------
    # API Key tests
    # -------------------------------------------------------------------

    def test_create_api_key_returns_plaintext_once(self):
        response = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Test Key", "scopes": "cache:read,plugin:publish"},
        )
        self.assertEqual(response.status_code, 201)
        payload = response.get_json()
        self.assertIn("key", payload)
        self.assertTrue(payload["key"].startswith("cvmp_"))
        self.assertEqual(payload["name"], "Test Key")
        self.assertIn("cache:read", payload["scopes"])

    def test_api_key_list_does_not_expose_hash(self):
        self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "List Key"},
        )

        response = self.client.get(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        keys = response.get_json()
        self.assertGreaterEqual(len(keys), 1)
        for key in keys:
            self.assertNotIn("key_hash", key)

    def test_revoke_api_key(self):
        create_resp = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Revoke Key"},
        )
        key_id = create_resp.get_json()["id"]

        response = self.client.post(
            f"/api/admin/api-keys/{key_id}/revoke",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.get_json()["status"], "revoked")

    def test_rotate_api_key(self):
        create_resp = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Rotate Key", "scopes": "cache:read"},
        )
        key_id = create_resp.get_json()["id"]

        response = self.client.post(
            f"/api/admin/api-keys/{key_id}/rotate",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 201)
        new_key = response.get_json()
        self.assertTrue(new_key["key"].startswith("cvmp_"))
        self.assertNotEqual(new_key["id"], key_id)

    def test_api_key_usage(self):
        create_resp = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Usage Key"},
        )
        key_id = create_resp.get_json()["id"]

        response = self.client.get(
            f"/api/admin/api-keys/{key_id}/usage",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        usage = response.get_json()
        self.assertEqual(usage["name"], "Usage Key")

    def test_bearer_api_key_can_access_admin(self):
        # Create an admin:* key
        create_resp = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Admin Key", "scopes": "admin:*"},
        )
        full_key = create_resp.get_json()["key"]

        # Use Bearer token to access admin endpoint
        response = self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": f"Bearer {full_key}"},
        )
        self.assertEqual(response.status_code, 200)

    def test_revoked_key_cannot_access(self):
        create_resp = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Will Revoke", "scopes": "admin:*"},
        )
        key_data = create_resp.get_json()
        full_key = key_data["key"]
        key_id = key_data["id"]

        # Revoke
        self.client.post(
            f"/api/admin/api-keys/{key_id}/revoke",
            headers=self._auth_headers(),
        )

        # Try to use revoked key
        response = self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": f"Bearer {full_key}"},
        )
        self.assertEqual(response.status_code, 401)

    def test_api_key_scope_enforcement(self):
        """Key with insufficient scope should get 403."""
        # Create key with only stats:read scope (valid but insufficient for cache:read)
        create_resp = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Limited Key", "scopes": "stats:read"},
        )
        self.assertEqual(create_resp.status_code, 201)
        full_key = create_resp.get_json()["key"]

        # Should fail for cache:read endpoint (needs cache:read scope)
        response = self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": f"Bearer {full_key}"},
        )
        self.assertEqual(response.status_code, 403)

    def test_api_key_cache_read_scope_works(self):
        """Key with cache:read should access cache status."""
        create_resp = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Cache Read Key", "scopes": "cache:read"},
        )
        full_key = create_resp.get_json()["key"]

        response = self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": f"Bearer {full_key}"},
        )
        self.assertEqual(response.status_code, 200)

    def test_api_key_cache_read_cannot_write(self):
        """Key with cache:read should not be able to cleanup cache."""
        create_resp = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Read Only Key", "scopes": "cache:read"},
        )
        full_key = create_resp.get_json()["key"]

        response = self.client.post(
            "/api/admin/cache/cleanup",
            headers={"Authorization": f"Bearer {full_key}"},
        )
        self.assertEqual(response.status_code, 403)

    def test_api_key_jobs_read_can_list_but_not_run(self):
        """Key with jobs:read can list but not run jobs."""
        from services.scheduler import ensure_default_jobs
        ensure_default_jobs(self.cache)

        create_resp = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Jobs Read Key", "scopes": "jobs:read"},
        )
        full_key = create_resp.get_json()["key"]
        headers = {"Authorization": f"Bearer {full_key}"}

        # Can list
        response = self.client.get("/api/admin/jobs", headers=headers)
        self.assertEqual(response.status_code, 200)

        # Cannot run
        response = self.client.post("/api/admin/jobs/cache_cleanup/run", headers=headers)
        self.assertEqual(response.status_code, 403)

    def test_api_key_stats_read_scope(self):
        """Key with stats:read can access stats overview."""
        create_resp = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Stats Key", "scopes": "stats:read"},
        )
        full_key = create_resp.get_json()["key"]

        response = self.client.get(
            "/api/admin/stats/overview",
            headers={"Authorization": f"Bearer {full_key}"},
        )
        self.assertEqual(response.status_code, 200)

    def test_api_key_last_used_at_updated(self):
        create_resp = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Tracked Key", "scopes": "admin:*"},
        )
        key_data = create_resp.get_json()
        full_key = key_data["key"]
        key_id = key_data["id"]

        # Use the key
        self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": f"Bearer {full_key}"},
        )

        # Check last_used_at
        response = self.client.get(
            f"/api/admin/api-keys/{key_id}/usage",
            headers=self._auth_headers(),
        )
        usage = response.get_json()
        self.assertIsNotNone(usage.get("last_used_at"))


if __name__ == "__main__":
    unittest.main()

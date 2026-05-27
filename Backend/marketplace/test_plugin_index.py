"""Tests for plugin index service and admin API endpoints."""

import base64
import copy
import io
import json
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
        from services.plugin_index import refresh_plugin_index
        result = refresh_plugin_index(self.cache, self.storage, "NonExistent")
        self.assertIsNone(result)

        db = self.cache.get_db()
        row = db.execute(
            "SELECT * FROM plugin_index WHERE plugin_id = 'NonExistent'"
        ).fetchone()
        db.close()
        self.assertIsNotNone(row)
        self.assertEqual(row["is_deleted"], 1)

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
        self.assertIn("changelog", payload)
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
            json={"name": "Test Key", "scopes": "plugin:read,plugin:publish"},
        )
        self.assertEqual(response.status_code, 201)
        payload = response.get_json()
        self.assertIn("key", payload)
        self.assertTrue(payload["key"].startswith("cvmp_"))
        self.assertEqual(payload["name"], "Test Key")
        self.assertIn("plugin:read", payload["scopes"])

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
            json={"name": "Rotate Key", "scopes": "plugin:read"},
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
        """Key with insufficient scope should get 401."""
        # Create key with only plugin:read scope
        create_resp = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Limited Key", "scopes": "plugin:read"},
        )
        full_key = create_resp.get_json()["key"]

        # Should fail for cache:read endpoint (needs cache:read scope)
        response = self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": f"Bearer {full_key}"},
        )
        self.assertEqual(response.status_code, 401)

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
        self.assertEqual(response.status_code, 401)

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
        self.assertEqual(response.status_code, 401)

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

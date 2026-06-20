"""
Contract tests for ColorVision Marketplace API.

These tests freeze the current Web and API interface contracts. Any rewrite
must pass all of these tests before replacing the deployed portal.

Covers:
  - React SPA page routes and public site-data endpoints
  - All public API endpoints
  - All admin API endpoints
  - Auth behavior (Session, Basic, Bearer)
  - Index update on upload/publish
"""

import base64
import copy
import io
import json
import tempfile
import unittest
import zipfile
from pathlib import Path

import app as marketplace_app


class ContractTestBase(unittest.TestCase):
    """Base class with shared setup for contract tests."""

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.storage = self.root / "storage"
        (self.storage / "Plugins").mkdir(parents=True, exist_ok=True)

        self._orig_storage = marketplace_app.STORAGE
        self._orig_db_path = marketplace_app.DB_PATH
        self._orig_config = copy.deepcopy(marketplace_app.CONFIG)
        self._orig_testing = marketplace_app.app.config.get("TESTING", False)
        self._orig_secret = marketplace_app.app.secret_key

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {"username": "admin", "password": "secret"}
        marketplace_app.CONFIG["secret_key"] = "test-secret-key"
        marketplace_app.CONFIG["debug"] = False
        marketplace_app.app.secret_key = "test-secret-key"
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.app.config["MAX_CONTENT_LENGTH"] = marketplace_app.MAX_UPLOAD_SIZE_BYTES
        marketplace_app.init_db()

        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        marketplace_app.STORAGE = self._orig_storage
        marketplace_app.DB_PATH = self._orig_db_path
        marketplace_app.CONFIG = self._orig_config
        marketplace_app.app.secret_key = self._orig_secret
        marketplace_app.app.config["TESTING"] = self._orig_testing
        self.temp_dir.cleanup()

    def basic_auth(self, user="admin", pw="secret"):
        token = base64.b64encode(f"{user}:{pw}".encode()).decode()
        return {"Authorization": f"Basic {token}"}

    def create_plugin(self, pid="DemoPlugin", version="1.0.0"):
        d = self.storage / "Plugins" / pid
        d.mkdir(parents=True, exist_ok=True)
        (d / "LATEST_RELEASE").write_text(version, encoding="utf-8")
        (d / f"{pid}-{version}.cvxp").write_bytes(b"pkg")
        (d / "manifest.json").write_text(
            json.dumps({"id": pid, "name": f"{pid} Name", "description": "test"}),
            encoding="utf-8",
        )
        return d

    def create_release(self, version, suffix=".exe", in_history=False):
        if in_history:
            parts = version.split(".")
            d = self.storage / "History" / ".".join(parts[:2]) / ".".join(parts[:3])
            d.mkdir(parents=True, exist_ok=True)
        else:
            d = self.storage
        p = d / f"ColorVision-{version}{suffix}"
        p.write_bytes(b"release")
        return p

    def create_update(self, version):
        d = self.storage / "Update"
        d.mkdir(parents=True, exist_ok=True)
        p = d / f"ColorVision-Update-[{version}].cvx"
        p.write_bytes(b"update")
        return p

    def create_tool(self, name, is_dir=False):
        d = self.storage / "Tool"
        d.mkdir(parents=True, exist_ok=True)
        if is_dir:
            td = d / name
            td.mkdir(parents=True, exist_ok=True)
            (td / "file.txt").write_bytes(b"x")
            return td
        p = d / name
        p.write_bytes(b"tool")
        return p

    def create_admin_key(self, scopes="admin:*"):
        resp = self.client.post(
            "/api/admin/api-keys",
            headers=self.basic_auth(),
            json={"name": "Test Key", "scopes": scopes},
        )
        return resp.get_json()


# ===================================================================
# Public Page Contracts
# ===================================================================

class PublicPageContracts(ContractTestBase):
    """Contract tests for public React routes and site-data endpoints."""

    def assert_spa_shell(self, resp):
        self.assertEqual(resp.status_code, 200)
        self.assertIn("text/html", resp.content_type)
        self.assertIn('<div id="root">', resp.get_data(as_text=True))

    def test_home_page_returns_200(self):
        resp = self.client.get("/")
        self.assert_spa_shell(resp)

    def test_plugins_page_returns_200(self):
        self.create_plugin()
        resp = self.client.get("/plugins")
        self.assert_spa_shell(resp)

    def test_releases_page_returns_200(self):
        self.create_release("1.0.0.1")
        resp = self.client.get("/releases")
        self.assert_spa_shell(resp)

    def test_updates_page_returns_200(self):
        self.create_update("1.0.0.1")
        resp = self.client.get("/updates")
        self.assert_spa_shell(resp)
        data_resp = self.client.get("/api/site/updates")
        self.assertEqual(data_resp.status_code, 200)
        versions = {item["version"] for item in data_resp.get_json()["update_packages"]}
        self.assertIn("1.0.0.1", versions)

    def test_tools_page_returns_200(self):
        self.create_tool("Installer.exe")
        resp = self.client.get("/tools")
        self.assert_spa_shell(resp)

    def test_browse_root_returns_200(self):
        resp = self.client.get("/browse")
        self.assert_spa_shell(resp)

    def test_transfer_page_returns_200(self):
        resp = self.client.get("/transfer")
        self.assert_spa_shell(resp)

    def test_browse_subpath_returns_200(self):
        self.create_tool("SomeTool.exe")
        resp = self.client.get("/browse/Tool")
        self.assert_spa_shell(resp)
        data_resp = self.client.get("/api/site/browse/Tool")
        self.assertEqual(data_resp.status_code, 200)
        self.assertTrue(any(item["name"] == "SomeTool.exe" for item in data_resp.get_json()["items"]))

    def test_browse_nonexistent_returns_404(self):
        resp = self.client.get("/api/site/browse/nonexistent")
        self.assertEqual(resp.status_code, 404)

    def test_plugin_detail_page_returns_200(self):
        self.create_plugin("MyPlugin", "2.0.0")
        resp = self.client.get("/plugins/MyPlugin")
        self.assert_spa_shell(resp)

    def test_plugin_detail_page_404_for_missing(self):
        resp = self.client.get("/api/plugins/NoSuchPlugin")
        self.assertEqual(resp.status_code, 404)

    def test_changelog_page_returns_200(self):
        (self.storage / "CHANGELOG.md").write_text("## 1.0.0\n- test", encoding="utf-8")
        resp = self.client.get("/changelog")
        self.assert_spa_shell(resp)


# ===================================================================
# Public API Contracts
# ===================================================================

class PublicApiContracts(ContractTestBase):
    """Contract tests for public REST API endpoints."""

    def test_api_plugins_returns_json_with_items(self):
        self.create_plugin()
        resp = self.client.get("/api/plugins")
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("items", data)
        self.assertIsInstance(data["items"], list)

    def test_api_plugins_supports_pagination(self):
        for i in range(5):
            self.create_plugin(f"P{i}", "1.0.0")
        resp = self.client.get("/api/plugins?Page=1&PageSize=2")
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertLessEqual(len(data["items"]), 2)

    def test_api_plugin_detail_returns_required_fields(self):
        self.create_plugin("DetailPlugin", "3.0.0")
        resp = self.client.get("/api/plugins/DetailPlugin")
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        for field in ("pluginId", "name", "latestVersion", "versions", "archivedVersions",
                       "readme", "changelog", "iconUrl", "totalDownloads",
                       "currentPackageCount", "historicalPackageCount"):
            self.assertIn(field, data, f"Missing field: {field}")

    def test_api_plugin_detail_400_for_invalid_id(self):
        resp = self.client.get("/api/plugins/bad!id")
        self.assertEqual(resp.status_code, 400)

    def test_api_latest_version_returns_text(self):
        (self.storage / "LATEST_RELEASE").write_text("1.2.3.4", encoding="utf-8")
        resp = self.client.get("/api/app/latest-version")
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertEqual(data["version"], "1.2.3.4")

    def test_api_changelog_returns_plain_text(self):
        (self.storage / "CHANGELOG.md").write_text("## 1.0.0\n- notes", encoding="utf-8")
        resp = self.client.get("/api/app/changelog")
        self.assertEqual(resp.status_code, 200)
        self.assertTrue(resp.content_type.startswith("text/plain"))
        self.assertIn("notes", resp.get_data(as_text=True))

    def test_api_health_returns_ok(self):
        resp = self.client.get("/api/health")
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertEqual(data["status"], "ok")
        self.assertIn("service", data)

    def test_api_ready_returns_status(self):
        resp = self.client.get("/api/ready")
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("ready", data)
        self.assertIn("checks", data)

    def test_api_stats_returns_counts(self):
        resp = self.client.get("/api/stats")
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("totalDownloads", data)

    def test_api_categories_returns_list(self):
        resp = self.client.get("/api/plugins/categories")
        self.assertEqual(resp.status_code, 200)
        self.assertIsInstance(resp.get_json(), list)


# ===================================================================
# CVWindowsService API Contracts
# ===================================================================

class CVWSApiContracts(ContractTestBase):
    """Contract tests for CVWindowsService API."""

    def setUp(self):
        super().setUp()
        cvws_dir = self.storage / "Tool" / "CVWindowsService"
        cvws_dir.mkdir(parents=True, exist_ok=True)
        (cvws_dir / "LATEST_RELEASE").write_text("1.0.0.0", encoding="utf-8")
        (cvws_dir / "CVWindowsService[1.0.0.0]-0.zip").write_bytes(b"cvws")

    def test_latest_version_returns_version(self):
        resp = self.client.get("/api/tool/cvwindowsservice/latest-version")
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertEqual(data["version"], "1.0.0.0")

    def test_releases_returns_packages(self):
        resp = self.client.get("/api/tool/cvwindowsservice/releases")
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("packages", data)
        self.assertIn("latestVersion", data)
        self.assertGreaterEqual(len(data["packages"]), 1)

    def test_download_returns_file(self):
        resp = self.client.get("/api/tool/cvwindowsservice/download/1.0.0.0")
        self.assertEqual(resp.status_code, 200)

    def test_download_404_for_missing_version(self):
        resp = self.client.get("/api/tool/cvwindowsservice/download/9.9.9.9")
        self.assertEqual(resp.status_code, 404)


# ===================================================================
# Upload / Publish Contracts
# ===================================================================

class UploadContracts(ContractTestBase):
    """Contract tests for upload and publish endpoints."""

    def test_publish_requires_auth(self):
        resp = self.client.post("/api/packages/publish", data={
            "PluginId": "X", "Version": "1.0.0",
            "package": (io.BytesIO(b"pkg"), "X-1.0.0.cvxp"),
        }, content_type="multipart/form-data")
        self.assertEqual(resp.status_code, 401)

    def test_publish_with_basic_auth_returns_201(self):
        resp = self.client.post(
            "/api/packages/publish",
            headers=self.basic_auth(),
            data={
                "PluginId": "PubPlugin",
                "Version": "1.0.0",
                "Name": "Pub Plugin",
                "Description": "test",
                "package": (io.BytesIO(b"pkg"), "PubPlugin-1.0.0.cvxp"),
            },
            content_type="multipart/form-data",
        )
        self.assertEqual(resp.status_code, 201)
        data = resp.get_json()
        self.assertEqual(data["pluginId"], "PubPlugin")
        self.assertEqual(data["version"], "1.0.0")

    def test_publish_updates_plugin_index(self):
        self.client.post(
            "/api/packages/publish",
            headers=self.basic_auth(),
            data={
                "PluginId": "IndexedPlugin",
                "Version": "2.0.0",
                "Name": "Indexed Plugin",
                "package": (io.BytesIO(b"pkg"), "IndexedPlugin-2.0.0.cvxp"),
            },
            content_type="multipart/form-data",
        )
        # Verify index was updated
        resp = self.client.get("/api/plugins/IndexedPlugin")
        self.assertEqual(resp.status_code, 200)
        self.assertEqual(resp.get_json()["latestVersion"], "2.0.0")

    def test_legacy_put_requires_auth(self):
        resp = self.client.put(
            "/upload/ColorVision/Plugins/X/X-1.0.0.cvxp",
            data=b"pkg",
            content_type="application/octet-stream",
        )
        self.assertEqual(resp.status_code, 401)

    def test_legacy_put_plugin_returns_201(self):
        resp = self.client.put(
            "/upload/ColorVision/Plugins/PUTPlugin/PUTPlugin-1.0.0.cvxp",
            data=b"pkg",
            headers=self.basic_auth(),
            content_type="application/octet-stream",
        )
        self.assertEqual(resp.status_code, 201)

    def test_legacy_put_release_refreshes_index(self):
        resp = self.client.put(
            "/upload/ColorVision/ColorVision-1.2.0.1.exe",
            data=b"installer",
            headers=self.basic_auth(),
            content_type="application/octet-stream",
        )
        self.assertEqual(resp.status_code, 201)
        from services.artifact_index import get_releases_from_index
        releases = get_releases_from_index(marketplace_app._cache)
        self.assertIsNotNone(releases)
        self.assertTrue(any(r["version"] == "1.2.0.1" for r in releases))

    def test_legacy_put_update_refreshes_index(self):
        resp = self.client.put(
            "/upload/ColorVision/Update/ColorVision-Update-[1.0.0.5].cvx",
            data=b"update",
            headers=self.basic_auth(),
            content_type="application/octet-stream",
        )
        self.assertEqual(resp.status_code, 201)
        from services.artifact_index import get_updates_from_index
        updates = get_updates_from_index(marketplace_app._cache)
        self.assertIsNotNone(updates)
        self.assertTrue(any(u["version"] == "1.0.0.5" for u in updates))

    def test_latest_version_cache_refreshes_on_legacy_put(self):
        latest_path = self.storage / "LATEST_RELEASE"
        latest_path.write_text("1.2.3.4", encoding="utf-8")

        first = self.client.get("/api/app/latest-version")
        self.assertEqual(first.status_code, 200)
        self.assertEqual(first.get_json()["version"], "1.2.3.4")

        latest_path.write_text("9.9.9.9", encoding="utf-8")
        cached = self.client.get("/api/app/latest-version")
        self.assertEqual(cached.status_code, 200)
        self.assertEqual(cached.get_json()["version"], "1.2.3.4")

        resp = self.client.put(
            "/upload/ColorVision/LATEST_RELEASE",
            data=b"2.0.0.0",
            headers=self.basic_auth(),
            content_type="application/octet-stream",
        )
        self.assertEqual(resp.status_code, 201)

        refreshed = self.client.get("/api/app/latest-version")
        self.assertEqual(refreshed.status_code, 200)
        self.assertEqual(refreshed.get_json()["version"], "2.0.0.0")

    def test_legacy_put_tool_refreshes_index(self):
        resp = self.client.put(
            "/upload/ColorVision/Tool/NewTool.zip",
            data=b"tool",
            headers=self.basic_auth(),
            content_type="application/octet-stream",
        )
        self.assertEqual(resp.status_code, 201)
        from services.artifact_index import get_tools_from_index
        tools = get_tools_from_index(marketplace_app._cache)
        self.assertIsNotNone(tools)
        self.assertTrue(any(t["name"] == "NewTool.zip" for t in tools))


# ===================================================================
# Auth Contracts
# ===================================================================

class AuthContracts(ContractTestBase):
    """Contract tests for authentication behavior."""

    def test_login_page_renders(self):
        resp = self.client.get("/login")
        self.assertEqual(resp.status_code, 200)

    def test_login_success_redirects(self):
        resp = self.client.post("/login", data={
            "username": "admin", "password": "secret",
        }, follow_redirects=False)
        self.assertIn(resp.status_code, [302, 303])

    def test_login_failure_returns_401_json_error(self):
        resp = self.client.post("/api/auth/login", json={
            "username": "admin", "password": "wrong",
        })
        self.assertEqual(resp.status_code, 401)
        self.assertEqual(resp.get_json()["error"], "用户名或密码错误")

    def test_register_user_creates_non_admin_session(self):
        resp = self.client.post("/api/auth/register", json={
            "username": "worker",
            "password": "secret1",
        })
        self.assertEqual(resp.status_code, 201)
        data = resp.get_json()
        self.assertTrue(data["authenticated"])
        self.assertFalse(data["is_admin"])
        self.assertEqual(data["role"], "user")

        admin_resp = self.client.get("/api/admin/cache/status")
        self.assertEqual(admin_resp.status_code, 401)

    def test_logout_redirects(self):
        resp = self.client.get("/logout", follow_redirects=False)
        self.assertIn(resp.status_code, [302, 303])

    def test_admin_no_auth_returns_401(self):
        resp = self.client.get("/api/admin/cache/status")
        self.assertEqual(resp.status_code, 401)

    def test_admin_bad_basic_auth_returns_401(self):
        resp = self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": "Basic " + base64.b64encode(b"bad:bad").decode()},
        )
        self.assertEqual(resp.status_code, 401)

    def test_admin_correct_basic_auth_returns_200(self):
        resp = self.client.get("/api/admin/cache/status", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)

    def test_admin_bearer_with_admin_star_works(self):
        key = self.create_admin_key("admin:*")
        resp = self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": f"Bearer {key['key']}"},
        )
        self.assertEqual(resp.status_code, 200)

    def test_admin_bearer_insufficient_scope_returns_403(self):
        key = self.create_admin_key("stats:read")
        resp = self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": f"Bearer {key['key']}"},
        )
        self.assertEqual(resp.status_code, 403)

    def test_admin_bearer_cache_read_can_read(self):
        key = self.create_admin_key("cache:read")
        resp = self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": f"Bearer {key['key']}"},
        )
        self.assertEqual(resp.status_code, 200)

    def test_admin_bearer_cache_read_cannot_write(self):
        key = self.create_admin_key("cache:read")
        resp = self.client.post(
            "/api/admin/cache/cleanup",
            headers={"Authorization": f"Bearer {key['key']}"},
        )
        self.assertEqual(resp.status_code, 403)


# ===================================================================
# Admin API Contracts
# ===================================================================

class AdminApiContracts(ContractTestBase):
    """Contract tests for admin API endpoints."""

    def test_cache_status_returns_db_info(self):
        resp = self.client.get("/api/admin/cache/status", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("db_path", data)
        self.assertIn("cache_entry_count", data)

    def test_cache_cleanup_returns_count(self):
        resp = self.client.post("/api/admin/cache/cleanup", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("deleted_count", data)

    def test_index_status_returns_states(self):
        resp = self.client.get("/api/admin/index/status", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("states", data)
        self.assertIn("counts", data)

    def test_index_refresh_all_returns_results(self):
        self.create_plugin()
        self.create_release("1.0.0.1")
        resp = self.client.post("/api/admin/index/refresh-all", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)

    def test_index_refresh_plugins(self):
        self.create_plugin()
        resp = self.client.post("/api/admin/index/plugins/refresh", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("indexed_count", data)

    def test_index_refresh_releases(self):
        self.create_release("1.0.0.1")
        resp = self.client.post("/api/admin/index/releases/refresh", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)

    def test_index_refresh_updates(self):
        self.create_update("1.0.0.1")
        resp = self.client.post("/api/admin/index/updates/refresh", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)

    def test_index_refresh_tools(self):
        self.create_tool("Tool.exe")
        resp = self.client.post("/api/admin/index/tools/refresh", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)

    def test_jobs_list_returns_jobs(self):
        from services.scheduler import ensure_default_jobs
        ensure_default_jobs(marketplace_app._cache)
        resp = self.client.get("/api/admin/jobs", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIsInstance(data, list)
        self.assertGreater(len(data), 0)

    def test_job_run_returns_result(self):
        from services.scheduler import ensure_default_jobs
        ensure_default_jobs(marketplace_app._cache)
        resp = self.client.post("/api/admin/jobs/cache_cleanup/run", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("job_id", data)
        self.assertIn("status", data)

    def test_job_enable_disable(self):
        from services.scheduler import ensure_default_jobs
        ensure_default_jobs(marketplace_app._cache)
        resp = self.client.post("/api/admin/jobs/cache_cleanup/disable", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        resp = self.client.post("/api/admin/jobs/cache_cleanup/enable", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)

    def test_api_keys_crud(self):
        # Create
        resp = self.client.post(
            "/api/admin/api-keys", headers=self.basic_auth(),
            json={"name": "CRUD Key", "scopes": "cache:read"},
        )
        self.assertEqual(resp.status_code, 201)
        key_data = resp.get_json()
        self.assertIn("key", key_data)
        self.assertTrue(key_data["key"].startswith("cvmp_"))
        key_id = key_data["id"]

        # List
        resp = self.client.get("/api/admin/api-keys", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        self.assertGreater(len(resp.get_json()), 0)

        # Usage
        resp = self.client.get(f"/api/admin/api-keys/{key_id}/usage", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)

        # Rotate
        resp = self.client.post(f"/api/admin/api-keys/{key_id}/rotate", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 201)

        # Revoke (need new key since rotated)
        resp = self.client.post(
            "/api/admin/api-keys", headers=self.basic_auth(),
            json={"name": "Revoke Key"},
        )
        rid = resp.get_json()["id"]
        resp = self.client.post(f"/api/admin/api-keys/{rid}/revoke", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)

    def test_api_key_invalid_scope_rejected(self):
        resp = self.client.post(
            "/api/admin/api-keys", headers=self.basic_auth(),
            json={"name": "Bad", "scopes": "invalid:scope"},
        )
        self.assertEqual(resp.status_code, 400)

    def test_audit_log_returns_entries(self):
        self.client.post("/api/admin/cache/cleanup", headers=self.basic_auth())
        resp = self.client.get("/api/admin/audit-log", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("entries", data)

    def test_stats_overview_returns_counts(self):
        resp = self.client.get("/api/admin/stats/overview", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("totalDownloads", data)

    def test_backup_db_creates_file(self):
        resp = self.client.post("/api/admin/backup/db", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertEqual(data["status"], "ok")

    def test_perf_summary_returns_data(self):
        resp = self.client.get("/api/admin/perf/summary", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("slow_requests", data)
        self.assertIn("slow_jobs", data)


# ===================================================================
# Index Integration Contracts
# ===================================================================

class IndexIntegrationContracts(ContractTestBase):
    """Contract tests verifying index updates on write operations."""

    def test_publish_then_list_shows_new_plugin(self):
        self.client.post(
            "/api/packages/publish",
            headers=self.basic_auth(),
            data={
                "PluginId": "NewPlugin",
                "Version": "1.0.0",
                "Name": "New Plugin",
                "package": (io.BytesIO(b"pkg"), "NewPlugin-1.0.0.cvxp"),
            },
            content_type="multipart/form-data",
        )
        resp = self.client.get("/api/plugins")
        ids = [p["pluginId"] for p in resp.get_json()["items"]]
        self.assertIn("NewPlugin", ids)

    def test_put_release_then_releases_page_shows_it(self):
        self.client.put(
            "/upload/ColorVision/ColorVision-2.0.0.1.exe",
            data=b"installer",
            headers=self.basic_auth(),
            content_type="application/octet-stream",
        )
        from services.artifact_index import get_releases_from_index
        releases = get_releases_from_index(marketplace_app._cache)
        self.assertTrue(any(r["version"] == "2.0.0.1" for r in releases))

    def test_put_update_then_updates_page_shows_it(self):
        self.client.put(
            "/upload/ColorVision/Update/ColorVision-Update-[3.0.0.1].cvx",
            data=b"update",
            headers=self.basic_auth(),
            content_type="application/octet-stream",
        )
        from services.artifact_index import get_updates_from_index
        updates = get_updates_from_index(marketplace_app._cache)
        self.assertTrue(any(u["version"] == "3.0.0.1" for u in updates))

    def test_put_tool_then_tools_page_shows_it(self):
        self.client.put(
            "/upload/ColorVision/Tool/NewTool.zip",
            data=b"tool",
            headers=self.basic_auth(),
            content_type="application/octet-stream",
        )
        from services.artifact_index import get_tools_from_index
        tools = get_tools_from_index(marketplace_app._cache)
        self.assertTrue(any(t["name"] == "NewTool.zip" for t in tools))


if __name__ == "__main__":
    unittest.main()

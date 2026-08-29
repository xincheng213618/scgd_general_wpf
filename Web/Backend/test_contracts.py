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
from unittest import mock

import app as marketplace_app
from routes.admin_api import ENDPOINT_SCOPES
from services.permission_service import ALL_PERMISSION_CODES


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
        try:
            self.assertEqual(resp.status_code, 200)
            self.assertIn("text/html", resp.content_type)
            self.assertIn('<div id="root">', resp.get_data(as_text=True))
        finally:
            resp.close()

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

    def test_account_page_returns_spa_shell(self):
        resp = self.client.get("/account")
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

    def test_public_browse_hides_operational_storage_but_admin_can_access_it(self):
        self.create_release("1.0.0.1")
        (self.storage / "History").mkdir()
        (self.storage / "Update").mkdir()
        (self.storage / "Tool").mkdir()
        for directory in ("Feedback", "Logs", "web-deploy-backups", "web-deploy-bundles"):
            target = self.storage / directory
            target.mkdir()
            (target / "private.txt").write_text("private", encoding="utf-8")
        (self.storage / "web-9998.log").write_text("runtime", encoding="utf-8")

        public_response = self.client.get("/api/site/browse")
        self.assertEqual(public_response.status_code, 200)
        public_names = {item["name"] for item in public_response.get_json()["items"]}
        self.assertIn("History", public_names)
        self.assertIn("Plugins", public_names)
        self.assertIn("Update", public_names)
        self.assertIn("Tool", public_names)
        self.assertIn("ColorVision-1.0.0.1.exe", public_names)
        self.assertNotIn("Feedback", public_names)
        self.assertNotIn("Logs", public_names)
        self.assertNotIn("web-deploy-backups", public_names)
        self.assertNotIn("web-deploy-bundles", public_names)
        self.assertNotIn("web-9998.log", public_names)
        self.assertEqual(public_response.get_json()["total_count"], len(public_names))

        self.assertEqual(self.client.get("/api/site/browse/Logs").status_code, 404)
        self.assertEqual(self.client.get("/download/Logs/private.txt").status_code, 404)

        admin_response = self.client.get("/api/site/browse/Logs", headers=self.basic_auth())
        self.assertEqual(admin_response.status_code, 200)
        self.assertEqual(admin_response.get_json()["items"][0]["name"], "private.txt")
        with self.client.get("/download/Logs/private.txt", headers=self.basic_auth()) as download:
            self.assertEqual(download.status_code, 200)
            self.assertEqual(download.get_data(), b"private")

        login_response = self.client.post(
            "/api/auth/login",
            json={"username": "admin", "password": "secret"},
        )
        self.assertEqual(login_response.status_code, 200)
        self.assertEqual(self.client.get("/api/site/browse/Logs").status_code, 200)

    def test_legacy_storage_route_only_exposes_public_artifacts(self):
        release = self.create_release("1.0.0.1")
        logs = self.storage / "Logs"
        logs.mkdir()
        (logs / "private.txt").write_text("private", encoding="utf-8")

        with self.client.get(f"/D%3A/ColorVision/{release.name}") as public_download:
            self.assertEqual(public_download.status_code, 200)
            self.assertEqual(public_download.get_data(), b"release")
        with self.client.get("/D%3A/ColorVision/Logs/private.txt") as private_download:
            self.assertEqual(private_download.status_code, 404)
        with self.client.get(
            "/D%3A/ColorVision/Logs/private.txt",
            headers=self.basic_auth(),
        ) as legacy_admin_download:
            self.assertEqual(legacy_admin_download.status_code, 404)

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

    def test_docs_redirect_enters_vitepress_base(self):
        resp = self.client.get("/docs", follow_redirects=False)
        self.assertEqual(resp.status_code, 302)
        self.assertEqual(resp.headers["Location"], "/scgd_general_wpf/")
        path_resp = self.client.get("/docs/02-developer-guide/backend/README", follow_redirects=False)
        self.assertEqual(path_resp.status_code, 302)
        self.assertEqual(path_resp.headers["Location"], "/scgd_general_wpf/02-developer-guide/backend/README")
        root_resp = self.client.get("/scgd_general_wpf", follow_redirects=False)
        self.assertEqual(root_resp.status_code, 302)
        self.assertEqual(root_resp.headers["Location"], "/scgd_general_wpf/")

    def test_docs_site_serves_vitepress_index_and_clean_urls(self):
        with tempfile.TemporaryDirectory() as td:
            dist = Path(td)
            (dist / "index.html").write_text("<html>Docs Home</html>", encoding="utf-8")
            (dist / "guide").mkdir()
            (dist / "guide" / "README.html").write_text("<html>Guide</html>", encoding="utf-8")
            with mock.patch("services.docs_site.docs_dist_dir", return_value=dist):
                home = self.client.get("/scgd_general_wpf/")
                self.assertEqual(home.status_code, 200)
                self.assertIn("Docs Home", home.get_data(as_text=True))
                home.close()

                guide = self.client.get("/scgd_general_wpf/guide/README")
                self.assertEqual(guide.status_code, 200)
                self.assertIn("Guide", guide.get_data(as_text=True))
                guide.close()


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
        plugin_dir = self.create_plugin("DetailPlugin", "3.0.0")
        (plugin_dir / "README.md").write_text("## 安装说明\n\n| 项目 | 说明 |\n| --- | --- |\n| A | B |", encoding="utf-8")
        (plugin_dir / "CHANGELOG.md").write_text("## 3.0.0\n\n- 支持 Markdown", encoding="utf-8")
        resp = self.client.get("/api/plugins/DetailPlugin")
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        for field in ("pluginId", "name", "latestVersion", "versions", "archivedVersions",
                       "readme", "readmeHtml", "changelog", "changelogHtml", "relatedDocs",
                       "iconUrl", "totalDownloads",
                       "currentPackageCount", "historicalPackageCount"):
            self.assertIn(field, data, f"Missing field: {field}")
        self.assertIn("<h2>", data["readmeHtml"])
        self.assertIn("<table>", data["readmeHtml"])
        self.assertIn("支持 Markdown", data["changelogHtml"])
        self.assertTrue(any(doc["href"].startswith("/scgd_general_wpf/") for doc in data["relatedDocs"]))

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

    def test_api_home_includes_public_docs_summary_when_indexed(self):
        with tempfile.TemporaryDirectory() as td:
            source = Path(td) / "docs"
            source.mkdir(parents=True)
            (source / "README.md").write_text("# Docs Home\n\nStart here.", encoding="utf-8")
            (source / "02-developer-guide").mkdir()
            (source / "02-developer-guide" / "plugin-development").mkdir()
            (source / "02-developer-guide" / "plugin-development" / "overview.md").write_text(
                "# Plugin Overview\n\nBuild plugins.", encoding="utf-8",
            )
            with mock.patch("services.docs_site.docs_source_dir", return_value=source):
                from services.docs_site import refresh_docs_index
                refresh_docs_index(marketplace_app._cache)
                resp = self.client.get("/api/site/home")

        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("docs", data)
        self.assertEqual(data["docs"]["total"], 2)
        self.assertTrue(data["docs"]["featured"])
        self.assertTrue(all(item["href"].startswith("/scgd_general_wpf/") for item in data["docs"]["featured"]))

    def test_compact_site_contracts_are_bounded_and_legacy_remains_available(self):
        self.create_release("1.2.0.1")
        self.create_release("1.1.0.1", suffix=".zip", in_history=True)
        (self.storage / "CHANGELOG.md").write_text("## [1.2.0.1]\n- notes", encoding="utf-8")

        legacy_home = self.client.get("/api/site/home").get_json()
        legacy_releases = self.client.get("/api/site/releases").get_json()
        compact_home = self.client.get("/api/site/home?view=compact").get_json()
        compact_releases = self.client.get("/api/site/releases?view=compact&page=1&page_size=20").get_json()
        compact_changelog = self.client.get("/api/site/changelog?view=compact").get_json()

        self.assertIn("overview", legacy_home)
        self.assertIn("archived_releases", legacy_releases["app_info"])
        self.assertIn("android_releases", legacy_releases["app_info"])
        self.assertIn("items", legacy_releases["archive_visible_groups"][0])
        self.assertEqual(
            set(compact_home),
            {"app_info", "update_summary", "tool_summary", "recent_change_dashboard", "docs"},
        )
        self.assertIn("archive_page_item_count", compact_releases)
        self.assertIn("android_page_item_count", compact_releases)
        self.assertIn("android_total_item_count", compact_releases)
        self.assertNotIn("android_releases", compact_releases["app_info"])
        self.assertTrue(all("items" not in group for group in compact_releases["archive_visible_groups"]))
        self.assertTrue(all("page_item_count" in group for group in compact_releases["archive_visible_groups"]))
        self.assertEqual(set(compact_changelog["app_info"]), {"latest_version", "changelog_html"})

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
        with self.client.get("/api/tool/cvwindowsservice/download/1.0.0.0") as resp:
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

    def test_login_next_rejects_browser_normalized_network_paths(self):
        unsafe_targets = (
            "https://example.com",
            "//example.com",
            "/\\\\example.com",
            "/%5C%5Cexample.com",
            "/%2F%2Fexample.com",
            "/%252F%252Fexample.com",
        )
        for target in unsafe_targets:
            with self.subTest(target=target):
                client = marketplace_app.app.test_client()
                response = client.post("/api/auth/login", json={
                    "username": "admin",
                    "password": "secret",
                    "next": target,
                })
                self.assertEqual(response.status_code, 200)
                self.assertEqual(response.get_json()["next"], "/admin")

        valid = marketplace_app.app.test_client().post("/api/auth/login", json={
            "username": "admin",
            "password": "secret",
            "next": "/account?tab=security#sessions",
        })
        self.assertEqual(valid.status_code, 200)
        self.assertEqual(
            valid.get_json()["next"],
            "/account?tab=security#sessions",
        )

    def test_login_page_renders(self):
        with self.client.get("/login") as resp:
            self.assertEqual(resp.status_code, 200)

    def test_login_head_is_read_only_and_matches_get_status(self):
        before = len(marketplace_app._cache.get_audit_log(action="login_failed"))

        with self.client.head("/login") as response:
            self.assertEqual(response.status_code, 200)
            self.assertEqual(response.get_data(), b"")
        self.assertEqual(len(marketplace_app._cache.get_audit_log(action="login_failed")), before)

    def test_global_security_headers_cover_spa_api_and_errors(self):
        for path in ("/", "/api/health", "/api/not-found"):
            with self.subTest(path=path):
                with self.client.get(path) as response:
                    self.assertEqual(response.headers["X-Content-Type-Options"], "nosniff")
                    self.assertEqual(response.headers["X-Frame-Options"], "SAMEORIGIN")
                    self.assertEqual(response.headers["Referrer-Policy"], "same-origin")
                    self.assertIn("frame-ancestors 'self'", response.headers["Content-Security-Policy"])

    def test_session_csrf_token_rotates_across_authentication_boundaries(self):
        anonymous = self.client.get("/api/auth/session").get_json()
        self.assertFalse(anonymous["authenticated"])
        self.assertGreaterEqual(len(anonymous["csrf_token"]), 32)

        login_response = self.client.post(
            "/api/auth/login",
            headers={
                "Origin": "http://localhost",
                "Sec-Fetch-Site": "same-origin",
                "X-CSRF-Token": anonymous["csrf_token"],
            },
            json={"username": "admin", "password": "secret"},
        )
        self.assertEqual(login_response.status_code, 200)
        authenticated = login_response.get_json()
        self.assertNotEqual(authenticated["csrf_token"], anonymous["csrf_token"])

        missing_token = self.client.post(
            "/api/auth/logout",
            headers={"Origin": "http://localhost", "Sec-Fetch-Site": "same-origin"},
        )
        self.assertEqual(missing_token.status_code, 403)
        self.assertTrue(self.client.get("/api/auth/session").get_json()["authenticated"])

        logout_response = self.client.post(
            "/api/auth/logout",
            headers={
                "Origin": "http://localhost",
                "Sec-Fetch-Site": "same-origin",
                "X-CSRF-Token": authenticated["csrf_token"],
            },
        )
        self.assertEqual(logout_response.status_code, 200)
        logged_out = logout_response.get_json()
        self.assertFalse(logged_out["authenticated"])
        self.assertNotEqual(logged_out["csrf_token"], authenticated["csrf_token"])

    def test_cross_origin_auth_write_is_rejected_before_login(self):
        before = len(marketplace_app._cache.get_audit_log(action="login_failed"))
        response = self.client.post(
            "/api/auth/login",
            headers={"Origin": "https://evil.example", "Sec-Fetch-Site": "cross-site"},
            json={"username": "admin", "password": "secret"},
        )

        self.assertEqual(response.status_code, 403)
        self.assertEqual(len(marketplace_app._cache.get_audit_log(action="login_failed")), before)

    def test_same_origin_session_admin_write_requires_csrf_token(self):
        login = self.client.post("/api/auth/login", json={
            "username": "admin", "password": "secret",
        }).get_json()
        browser_headers = {"Origin": "http://localhost", "Sec-Fetch-Site": "same-origin"}

        rejected = self.client.post("/api/admin/cache/cleanup", headers=browser_headers)
        accepted = self.client.post(
            "/api/admin/cache/cleanup",
            headers={**browser_headers, "X-CSRF-Token": login["csrf_token"]},
        )

        self.assertEqual(rejected.status_code, 403)
        self.assertEqual(accepted.status_code, 200)

    def test_login_success_redirects(self):
        resp = self.client.post("/login", data={
            "username": "admin", "password": "secret",
        }, follow_redirects=False)
        self.assertIn(resp.status_code, [302, 303])
        session_cookie = resp.headers["Set-Cookie"]
        self.assertIn("HttpOnly", session_cookie)
        self.assertIn("SameSite=Lax", session_cookie)

    def test_login_failure_returns_401_json_error(self):
        resp = self.client.post("/api/auth/login", json={
            "username": "admin", "password": "wrong",
        })
        self.assertEqual(resp.status_code, 401)
        self.assertEqual(resp.get_json()["error"], "用户名或密码错误")
        self.assertEqual(resp.get_json()["attempts_remaining"], 4)

    def test_database_usernames_are_case_insensitive_at_login(self):
        from services.auth_service import create_user

        user, error = create_user(marketplace_app._cache, "CaseWorker", "correct-horse-1")
        self.assertIsNone(error)
        self.assertIsNotNone(user)

        response = self.client.post("/api/auth/login", json={
            "username": "caseworker",
            "password": "correct-horse-1",
        })
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.get_json()["username"], "CaseWorker")

    def test_public_registration_cannot_claim_configured_admin_username(self):
        marketplace_app.CONFIG["public_registration_enabled"] = True
        response = self.client.post("/api/auth/register", json={
            "username": "ADMIN",
            "password": "correct-horse-1",
        })
        self.assertEqual(response.status_code, 400)
        self.assertEqual(response.get_json()["error"], "用户名已存在")

    def test_public_registration_success_velocity_is_persistently_limited(self):
        marketplace_app.CONFIG["public_registration_enabled"] = True
        source = {"REMOTE_ADDR": "203.0.113.40"}
        for index in range(5):
            client = marketplace_app.app.test_client()
            response = client.post(
                "/api/auth/register",
                json={"username": f"velocity-user-{index}", "password": "correct-horse-1"},
                environ_base=source,
            )
            self.assertEqual(response.status_code, 201)

        blocked = marketplace_app.app.test_client().post(
            "/api/auth/register",
            json={"username": "velocity-user-blocked", "password": "correct-horse-1"},
            environ_base=source,
        )
        self.assertEqual(blocked.status_code, 429)
        self.assertEqual(blocked.get_json()["status"], 429)
        self.assertGreater(blocked.get_json()["retry_after"], 0)
        self.assertEqual(
            blocked.headers["Retry-After"],
            str(blocked.get_json()["retry_after"]),
        )
        self.assertEqual(
            len(marketplace_app._cache.get_audit_log(action="registration_throttled")),
            1,
        )

        db = marketplace_app._cache.get_db()
        try:
            db.execute(
                "UPDATE registration_rate_limits "
                "SET success_window_started_at = '2000-01-01T00:00:00+00:00' "
                "WHERE ip_address = '203.0.113.40'"
            )
            db.commit()
        finally:
            db.close()
        resumed = marketplace_app.app.test_client().post(
            "/api/auth/register",
            json={"username": "velocity-user-resumed", "password": "correct-horse-1"},
            environ_base=source,
        )
        self.assertEqual(resumed.status_code, 201)

    def test_password_recovery_source_velocity_is_persistently_limited(self):
        from services.password_recovery_service import RECOVERY_SOURCE_ATTEMPT_LIMIT

        source = {"REMOTE_ADDR": "203.0.113.60"}
        for index in range(RECOVERY_SOURCE_ATTEMPT_LIMIT):
            client = marketplace_app.app.test_client()
            response = client.post(
                "/api/auth/password-recovery",
                json={"identifier": f"missing-recovery-{index}@example.com"},
                environ_base=source,
            )
            self.assertEqual(response.status_code, 202)

        blocked = marketplace_app.app.test_client().post(
            "/api/auth/password-recovery",
            json={"identifier": "missing-recovery-blocked@example.com"},
            environ_base=source,
        )
        self.assertEqual(blocked.status_code, 429)
        self.assertEqual(blocked.get_json()["status"], 429)
        self.assertGreater(blocked.get_json()["retry_after"], 0)
        self.assertEqual(
            blocked.headers["Retry-After"],
            str(blocked.get_json()["retry_after"]),
        )
        audit = marketplace_app._cache.get_audit_log(
            action="password_recovery_throttled",
        )
        self.assertEqual(len(audit), 1)
        self.assertIn("attempt_velocity", audit[0]["detail"])
        self.assertNotIn("missing-recovery", audit[0]["detail"])

        db = marketplace_app._cache.get_db()
        try:
            db.execute(
                "UPDATE password_recovery_rate_limits "
                "SET window_started_at = '2000-01-01T00:00:00+00:00' "
                "WHERE ip_address = '203.0.113.60'"
            )
            db.commit()
        finally:
            db.close()
        resumed = marketplace_app.app.test_client().post(
            "/api/auth/password-recovery",
            json={"identifier": "missing-recovery-resumed@example.com"},
            environ_base=source,
        )
        self.assertEqual(resumed.status_code, 202)

    def test_public_registration_invalid_attempt_velocity_is_limited(self):
        marketplace_app.CONFIG["public_registration_enabled"] = True
        source = {"REMOTE_ADDR": "198.51.100.70"}
        for _ in range(20):
            response = self.client.post(
                "/api/auth/register",
                json={"username": "x", "password": "short"},
                environ_base=source,
            )
            self.assertEqual(response.status_code, 400)

        blocked = self.client.post(
            "/api/auth/register",
            json={"username": "valid-after-spam", "password": "correct-horse-1"},
            environ_base=source,
        )
        self.assertEqual(blocked.status_code, 429)
        self.assertGreater(blocked.get_json()["retry_after"], 0)
        audit = marketplace_app._cache.get_audit_log(action="registration_throttled")
        self.assertEqual(len(audit), 1)
        self.assertIn("attempt_velocity", audit[0]["detail"])

    def test_login_failures_lock_account_across_sources_and_success_clears_them(self):
        from services.auth_service import create_user

        user, error = create_user(
            marketplace_app._cache,
            "throttle-user",
            "correct-password",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(user)
        throttle_client = marketplace_app.app.test_client()

        for index in range(4):
            response = throttle_client.post(
                "/api/auth/login",
                json={"username": "throttle-user", "password": "wrong"},
                environ_base={"REMOTE_ADDR": f"10.0.0.{index + 1}"},
            )
            self.assertEqual(response.status_code, 401)
            self.assertEqual(response.get_json()["attempts_remaining"], 4 - index)

        locked = throttle_client.post(
            "/api/auth/login",
            json={"username": "throttle-user", "password": "wrong"},
            environ_base={"REMOTE_ADDR": "10.0.0.5"},
        )
        self.assertEqual(locked.status_code, 429)
        self.assertEqual(locked.get_json()["status"], 429)
        self.assertEqual(locked.get_json()["attempts_remaining"], 0)
        self.assertGreater(locked.get_json()["retry_after"], 0)
        self.assertEqual(
            locked.headers["Retry-After"],
            str(locked.get_json()["retry_after"]),
        )

        still_locked = throttle_client.post(
            "/api/auth/login",
            json={"username": "throttle-user", "password": "correct-password"},
            environ_base={"REMOTE_ADDR": "192.0.2.10"},
        )
        self.assertEqual(still_locked.status_code, 429)
        self.assertEqual(
            len(marketplace_app._cache.get_audit_log(action="login_throttled")),
            1,
        )
        self.assertEqual(
            len(marketplace_app._cache.get_audit_log(action="login_failed")),
            5,
        )

        db = marketplace_app._cache.get_db()
        try:
            db.execute(
                "UPDATE login_attempts SET locked_until = '2000-01-01T00:00:00+00:00' "
                "WHERE username_key = 'throttle-user'"
            )
            db.commit()
        finally:
            db.close()

        unlocked = throttle_client.post(
            "/api/auth/login",
            json={"username": "throttle-user", "password": "correct-password"},
            environ_base={"REMOTE_ADDR": "192.0.2.10"},
        )
        self.assertEqual(unlocked.status_code, 200)
        db = marketplace_app._cache.get_db()
        try:
            self.assertEqual(
                db.execute(
                    "SELECT COUNT(*) FROM login_attempts WHERE username_key = 'throttle-user'"
                ).fetchone()[0],
                0,
            )
        finally:
            db.close()

        activity = throttle_client.get("/api/account/activity?limit=20&offset=0").get_json()
        self.assertEqual(activity["summary"]["failed_logins"], 5)
        self.assertEqual(activity["summary"]["throttled_logins"], 1)
        self.assertIn("login_throttled", {entry["action"] for entry in activity["entries"]})

    def test_register_user_gets_default_full_role_permissions(self):
        marketplace_app.CONFIG["public_registration_enabled"] = True
        resp = self.client.post("/api/auth/register", json={
            "username": "worker",
            "password": "correct-horse-1",
            "display_name": "一线工程师",
            "email": "Worker@Example.com",
            "next": "/account?welcome=1",
        })
        self.assertEqual(resp.status_code, 201)
        data = resp.get_json()
        self.assertTrue(data["authenticated"])
        self.assertFalse(data["is_admin"])
        self.assertFalse(data["must_change_password"])
        self.assertTrue(data["can_access_admin"])
        self.assertEqual(data["role"], "user")
        self.assertEqual(data["next"], "/account?welcome=1")
        self.assertIn("admin:access", data["permissions"])
        self.assertTrue(data["public_registration_enabled"])

        profile = self.client.get("/api/account").get_json()
        self.assertEqual(profile["display_name"], "一线工程师")
        self.assertEqual(profile["email"], "worker@example.com")
        self.assertEqual(profile["account_origin"], "self_registered")
        self.assertTrue(profile["can_edit_profile"])

        admin_resp = self.client.get("/api/admin/cache/status")
        self.assertEqual(admin_resp.status_code, 200)
        with self.client.get("/admin", follow_redirects=False) as admin_page:
            self.assertEqual(admin_page.status_code, 200)

    def test_public_registration_is_enabled_by_default_and_exposed_in_session(self):
        session_response = self.client.get("/api/auth/session")
        self.assertEqual(session_response.status_code, 200)
        self.assertTrue(session_response.get_json()["public_registration_enabled"])

        register_page = self.client.get("/register", follow_redirects=False)
        self.assertEqual(register_page.status_code, 302)
        self.assertTrue(register_page.headers["Location"].endswith("/login?mode=register"))

        registration = self.client.post("/api/auth/register", json={
            "username": "default-user", "password": "correct-horse-1",
        })
        self.assertEqual(registration.status_code, 201)
        self.assertEqual(
            len(marketplace_app._cache.get_audit_log(action="user_register")),
            1,
        )

    def test_logout_get_is_read_only_and_post_clears_session(self):
        self.client.post("/api/auth/login", json={
            "username": "admin", "password": "secret",
        })

        get_response = self.client.get("/logout", follow_redirects=False)
        self.assertIn(get_response.status_code, [302, 303])
        self.assertTrue(self.client.get("/api/auth/session").get_json()["authenticated"])

        post_response = self.client.post("/logout", follow_redirects=False)
        self.assertIn(post_response.status_code, [302, 303])
        self.assertFalse(self.client.get("/api/auth/session").get_json()["authenticated"])

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

    def test_jobs_read_scope_can_read_history_but_cannot_run_jobs(self):
        from services.scheduler import ensure_default_jobs
        ensure_default_jobs(marketplace_app._cache)
        key = self.create_admin_key("jobs:read")
        headers = {"Authorization": f"Bearer {key['key']}"}

        self.assertEqual(self.client.get("/api/admin/jobs", headers=headers).status_code, 200)
        self.assertEqual(
            self.client.get(
                "/api/admin/jobs/cache_cleanup/runs", headers=headers
            ).status_code,
            200,
        )
        self.assertEqual(
            self.client.post(
                "/api/admin/jobs/cache_cleanup/run", headers=headers
            ).status_code,
            403,
        )


# ===================================================================
# Admin API Contracts
# ===================================================================

class AdminApiContracts(ContractTestBase):
    """Contract tests for admin API endpoints."""

    def test_every_admin_endpoint_declares_known_permission_scopes(self):
        endpoint_names = {
            rule.endpoint.rsplit(".", 1)[-1]
            for rule in marketplace_app.app.url_map.iter_rules()
            if rule.rule.startswith("/api/admin/")
        }

        self.assertSetEqual(endpoint_names, set(ENDPOINT_SCOPES))
        for endpoint, required_scopes in ENDPOINT_SCOPES.items():
            with self.subTest(endpoint=endpoint):
                self.assertTrue(required_scopes)
                self.assertLessEqual(set(required_scopes), ALL_PERMISSION_CODES)

    def test_admin_created_account_must_change_temporary_password_before_access(self):
        created_response = self.client.post(
            "/api/admin/users",
            headers=self.basic_auth(),
            json={
                "username": "temporary-worker",
                "password": "correct-horse-1",
                "role": "user",
            },
        )
        self.assertEqual(created_response.status_code, 201)
        self.assertTrue(created_response.get_json()["must_change_password"])

        temporary_client = marketplace_app.app.test_client()
        login = temporary_client.post("/api/auth/login", json={
            "username": "temporary-worker",
            "password": "correct-horse-1",
        })
        self.assertEqual(login.status_code, 200)
        login_payload = login.get_json()
        self.assertTrue(login_payload["authenticated"])
        self.assertTrue(login_payload["must_change_password"])
        self.assertFalse(login_payload["can_access_admin"])
        self.assertEqual(login_payload["permissions"], [])
        self.assertEqual(login_payload["next"], "/account?password_change=required")

        profile = temporary_client.get("/api/account")
        self.assertEqual(profile.status_code, 200)
        self.assertTrue(profile.get_json()["must_change_password"])
        self.assertEqual(profile.get_json()["permissions"], [])
        self.assertEqual(profile.get_json()["permission_details"], [])
        self.assertEqual(temporary_client.get("/api/account/sessions").status_code, 200)
        self.assertEqual(temporary_client.get("/api/account/activity").status_code, 200)

        for response in (
            temporary_client.put("/api/account", json={"display_name": "Blocked"}),
            temporary_client.get("/api/admin/users"),
            temporary_client.get("/api/transfer/files"),
        ):
            with self.subTest(path=response.request.path):
                self.assertEqual(response.status_code, 403)
                self.assertEqual(response.get_json()["code"], "password_change_required")

        unchanged = temporary_client.put("/api/account/password", json={
            "current_password": "correct-horse-1",
            "new_password": "correct-horse-1",
        })
        self.assertEqual(unchanged.status_code, 400)
        self.assertEqual(unchanged.get_json()["error"], "新密码不能与当前密码相同")
        still_restricted = temporary_client.get("/api/auth/session").get_json()
        self.assertTrue(still_restricted["must_change_password"])
        self.assertFalse(still_restricted["can_access_admin"])

        changed = temporary_client.put("/api/account/password", json={
            "current_password": "correct-horse-1",
            "new_password": "correct-horse-2",
        })
        self.assertEqual(changed.status_code, 200, changed.get_json())
        self.assertFalse(changed.get_json()["must_change_password"])

        active_session = temporary_client.get("/api/auth/session").get_json()
        self.assertFalse(active_session["must_change_password"])
        self.assertTrue(active_session["can_access_admin"])
        self.assertIn("admin:access", active_session["permissions"])
        active_profile = temporary_client.get("/api/account").get_json()
        self.assertEqual(
            {item["code"] for item in active_profile["permission_details"]},
            set(active_profile["permissions"]),
        )
        self.assertEqual(temporary_client.get("/api/admin/users").status_code, 200)
        self.assertEqual(temporary_client.get("/api/transfer/files").status_code, 200)

    def test_admin_can_require_password_change_without_resetting_the_password(self):
        from services.auth_service import create_user
        from services.login_throttle_service import (
            get_login_throttle_status,
            record_login_failure,
        )

        user, error = create_user(
            marketplace_app._cache,
            "security-review-user",
            "correct-horse-1",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(user)

        first_client = marketplace_app.app.test_client()
        second_client = marketplace_app.app.test_client()
        for client in (first_client, second_client):
            self.assertEqual(client.post("/api/auth/login", json={
                "username": "security-review-user",
                "password": "correct-horse-1",
            }).status_code, 200)
        record_login_failure(
            marketplace_app._cache,
            "security-review-user",
            "192.0.2.61",
        )
        record_login_failure(
            marketplace_app._cache,
            "security-review-user",
            "192.0.2.62",
        )

        response = self.client.post(
            f"/api/admin/users/{user['id']}/password-change-required",
            headers=self.basic_auth(),
        )
        self.assertEqual(response.status_code, 200, response.get_json())
        payload = response.get_json()
        self.assertTrue(payload["must_change_password"])
        self.assertTrue(payload["sessions_invalidated"])
        self.assertEqual(payload["sessions_revoked"], 2)
        self.assertEqual(payload["login_failure_sources_cleared"], 2)
        self.assertEqual(
            get_login_throttle_status(
                marketplace_app._cache,
                "security-review-user",
            ).failed_count,
            0,
        )
        self.assertFalse(first_client.get("/api/auth/session").get_json()["authenticated"])
        self.assertFalse(second_client.get("/api/auth/session").get_json()["authenticated"])

        login = first_client.post("/api/auth/login", json={
            "username": "security-review-user",
            "password": "correct-horse-1",
        })
        self.assertEqual(login.status_code, 200)
        self.assertTrue(login.get_json()["must_change_password"])
        self.assertEqual(login.get_json()["next"], "/account?password_change=required")

        activity = first_client.get("/api/account/activity?limit=20&offset=0").get_json()
        self.assertIn(
            "user_password_change_required",
            {entry["action"] for entry in activity["entries"]},
        )
        self.assertGreaterEqual(activity["summary"]["security_events"], 1)
        audit = marketplace_app._cache.get_audit_log(
            action="user_password_change_required",
        )
        self.assertEqual(len(audit), 1)
        self.assertEqual(audit[0]["target_id"], str(user["id"]))

        users = self.client.get(
            "/api/admin/users?limit=20&offset=0",
            headers=self.basic_auth(),
        ).get_json()
        self.assertEqual(users["summary"]["pending_password_changes"], 1)

    def _create_feedback(self, feedback_id="20260812_120000_contract"):
        directory = self.storage / "Feedback" / feedback_id
        directory.mkdir(parents=True)
        (directory / "feedback.json").write_text(json.dumps({
            "feedbackId": feedback_id,
            "message": "contract feedback",
            "userName": "tester",
            "appVersion": "1.2.3.4",
            "machineInfo": "contract host",
            "clientIp": "hashed-client",
            "createdAt": "2026-08-12T12:00:00+00:00",
            "files": ["report.zip"],
        }), encoding="utf-8")
        (directory / "report.zip").write_bytes(b"diagnostic")
        return directory

    def test_feedback_inbox_detail_download_and_status_lifecycle(self):
        directory = self._create_feedback()
        headers = self.basic_auth()

        listing = self.client.get("/api/admin/feedback?status=open", headers=headers)
        self.assertEqual(listing.status_code, 200)
        data = listing.get_json()
        self.assertEqual(data["total"], 1)
        self.assertEqual(data["summary"]["status_counts"]["new"], 1)
        self.assertEqual(data["summary"]["oldest_open_at"], "2026-08-12T12:00:00+00:00")
        self.assertNotIn("machine_info", data["items"][0])

        detail = self.client.get(
            f"/api/admin/feedback/{directory.name}", headers=headers,
        )
        self.assertEqual(detail.status_code, 200)
        self.assertEqual(detail.get_json()["message"], "contract feedback")

        with self.client.get(
            f"/api/admin/feedback/{directory.name}/attachments/report.zip",
            headers=headers,
        ) as download:
            self.assertEqual(download.status_code, 200)
            self.assertEqual(download.get_data(), b"diagnostic")
            self.assertIn("attachment", download.headers["Content-Disposition"])

        updated = self.client.put(
            f"/api/admin/feedback/{directory.name}/status",
            headers=headers,
            json={"status": "resolved"},
        )
        self.assertEqual(updated.status_code, 200)
        self.assertEqual(updated.get_json()["status"], "resolved")
        self.assertEqual(
            self.client.get(
                "/api/admin/feedback?status=resolved", headers=headers,
            ).get_json()["total"],
            1,
        )
        self.assertEqual(
            self.client.get(
                "/api/admin/feedback?status=open", headers=headers,
            ).get_json()["total"],
            0,
        )
        audits = marketplace_app._cache.get_audit_log()
        actions = {item["action"] for item in audits}
        self.assertIn("feedback_attachment_download", actions)
        self.assertIn("feedback_status_update", actions)

    def test_feedback_inbox_requires_admin_and_rejects_unsafe_paths(self):
        directory = self._create_feedback()
        self.assertEqual(self.client.get("/api/admin/feedback").status_code, 401)

        key = self.create_admin_key("stats:read")
        limited = {"Authorization": f"Bearer {key['key']}"}
        self.assertEqual(
            self.client.get("/api/admin/feedback", headers=limited).status_code,
            403,
        )
        headers = self.basic_auth()
        self.assertEqual(
            self.client.get("/api/admin/feedback?status=closed", headers=headers).status_code,
            400,
        )
        self.assertEqual(
            self.client.get(
                f"/api/admin/feedback/{directory.name}/attachments/feedback.json",
                headers=headers,
            ).status_code,
            404,
        )
        self.assertEqual(
            self.client.put(
                f"/api/admin/feedback/{directory.name}/status",
                headers=headers,
                json={"status": "closed"},
            ).status_code,
            400,
        )

    def test_cache_status_returns_db_info(self):
        resp = self.client.get("/api/admin/cache/status", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("db_path", data)
        self.assertIn("cache_entry_count", data)

    def test_retention_settings_get_exposes_only_safe_effective_values(self):
        from services.operational_settings import OPERATIONAL_RETENTION_SETTINGS

        response = self.client.get(
            "/api/admin/settings/retention",
            headers=self.basic_auth(),
        )

        self.assertEqual(response.status_code, 200)
        data = response.get_json()
        self.assertEqual(set(data["values"]), set(OPERATIONAL_RETENTION_SETTINGS))
        self.assertEqual(set(data["limits"]), set(OPERATIONAL_RETENTION_SETTINGS))
        self.assertFalse(data["restart_required"])
        serialized = json.dumps(data)
        for forbidden in ("secret", "password", "storage_path", "upload_auth", "copilot_sync"):
            self.assertNotIn(forbidden, serialized)

    def test_retention_settings_put_preserves_secrets_and_updates_live_config(self):
        from services.operational_settings import OPERATIONAL_RETENTION_SETTINGS

        config_path = self.root / "config.json"
        config_path.write_text(json.dumps({
            "secret_key": "preserved-secret",
            "storage_path": str(self.storage),
            "upload_auth": {"username": "admin", "password": "preserved-password"},
            "copilot_sync": {"version_keys": ["stable"]},
            "unrelated": {"enabled": True},
        }), encoding="utf-8")
        values = {
            name: spec.default for name, spec in OPERATIONAL_RETENTION_SETTINGS.items()
        }
        values["job_run_retention_days"] = 45

        response = self.client.put(
            "/api/admin/settings/retention",
            headers=self.basic_auth(),
            json={"values": values},
        )

        self.assertEqual(response.status_code, 200)
        data = response.get_json()
        self.assertEqual(data["status"], "updated")
        self.assertEqual(data["changed"], ["job_run_retention_days"])
        self.assertEqual(marketplace_app.CONFIG["job_run_retention_days"], 45)
        persisted = json.loads(config_path.read_text(encoding="utf-8"))
        self.assertEqual(persisted["secret_key"], "preserved-secret")
        self.assertEqual(persisted["upload_auth"]["password"], "preserved-password")
        self.assertEqual(persisted["copilot_sync"], {"version_keys": ["stable"]})
        self.assertEqual(persisted["unrelated"], {"enabled": True})
        audit = self.client.get(
            "/api/admin/audit-log?action=retention_settings_update",
            headers=self.basic_auth(),
        ).get_json()
        self.assertEqual(audit["total"], 1)
        self.assertNotIn("secret", audit["entries"][0]["detail"])

    def test_retention_settings_put_rejects_unknown_or_incomplete_values(self):
        from services.operational_settings import OPERATIONAL_RETENTION_SETTINGS

        values = {
            name: spec.default for name, spec in OPERATIONAL_RETENTION_SETTINGS.items()
        }
        for payload in (
            {"values": {**values, "secret_key": 1}},
            {"values": {k: v for k, v in values.items() if k != "job_run_retention_days"}},
            {"values": {**values, "audit_log_retention_days": False}},
        ):
            with self.subTest(payload=payload):
                response = self.client.put(
                    "/api/admin/settings/retention",
                    headers=self.basic_auth(),
                    json=payload,
                )
                self.assertEqual(response.status_code, 400)
        self.assertFalse((self.root / "config.json").exists())

    def test_retention_settings_require_full_admin_scope(self):
        key = self.create_admin_key("stats:read")
        headers = {"Authorization": f"Bearer {key['key']}"}

        self.assertEqual(
            self.client.get("/api/admin/settings/retention", headers=headers).status_code,
            403,
        )
        self.assertEqual(
            self.client.put(
                "/api/admin/settings/retention",
                headers=headers,
                json={"values": {}},
            ).status_code,
            403,
        )

    def test_account_settings_control_public_registration_live(self):
        marketplace_app.CONFIG["public_registration_enabled"] = False
        config_path = self.root / "config.json"
        config_path.write_text(json.dumps({
            "secret_key": "preserved-secret",
            "storage_path": str(self.storage),
            "upload_auth": {"username": "admin", "password": "preserved-password"},
            "unrelated": {"enabled": True},
        }), encoding="utf-8")

        initial = self.client.get(
            "/api/admin/settings/accounts",
            headers=self.basic_auth(),
        )
        self.assertEqual(initial.status_code, 200)
        self.assertFalse(initial.get_json()["public_registration_enabled"])

        enabled = self.client.put(
            "/api/admin/settings/accounts",
            headers=self.basic_auth(),
            json={"public_registration_enabled": True},
        )
        self.assertEqual(enabled.status_code, 200)
        self.assertEqual(enabled.get_json()["status"], "updated")
        self.assertTrue(enabled.get_json()["public_registration_enabled"])
        self.assertFalse(enabled.get_json()["restart_required"])
        self.assertTrue(
            self.client.get("/api/auth/session").get_json()["public_registration_enabled"]
        )

        registration = self.client.post("/api/auth/register", json={
            "username": "policy-user", "password": "correct-horse-1",
        })
        self.assertEqual(registration.status_code, 201)
        db = marketplace_app._cache.get_db()
        try:
            quota_before_disable = dict(db.execute(
                "SELECT attempt_count, success_count, pending_count, last_attempt_at "
                "FROM registration_rate_limits WHERE ip_address = '127.0.0.1'"
            ).fetchone())
        finally:
            db.close()

        disabled = self.client.put(
            "/api/admin/settings/accounts",
            headers=self.basic_auth(),
            json={"public_registration_enabled": False},
        )
        self.assertEqual(disabled.status_code, 200)
        self.assertFalse(disabled.get_json()["public_registration_enabled"])
        self.assertEqual(self.client.post("/api/auth/register", json={
            "username": "blocked-again", "password": "correct-horse-1",
        }).status_code, 403)
        db = marketplace_app._cache.get_db()
        try:
            quota_after_disable = dict(db.execute(
                "SELECT attempt_count, success_count, pending_count, last_attempt_at "
                "FROM registration_rate_limits WHERE ip_address = '127.0.0.1'"
            ).fetchone())
        finally:
            db.close()
        self.assertEqual(quota_after_disable, quota_before_disable)

        persisted = json.loads(config_path.read_text(encoding="utf-8"))
        self.assertEqual(persisted["secret_key"], "preserved-secret")
        self.assertEqual(persisted["upload_auth"]["password"], "preserved-password")
        self.assertEqual(persisted["unrelated"], {"enabled": True})
        self.assertFalse(persisted["public_registration_enabled"])
        audits = marketplace_app._cache.get_audit_log(action="account_settings_update")
        self.assertEqual(len(audits), 2)
        self.assertNotIn("password", json.dumps(audits))

    def test_role_permissions_are_persisted_and_enforced_for_registered_users(self):
        from services.auth_service import create_user, set_user_active

        user, error = create_user(marketplace_app._cache, "permission-user", "correct-horse-1")
        self.assertIsNone(error)
        self.assertIsNotNone(user)
        inactive_user, error = create_user(
            marketplace_app._cache,
            "inactive-permission-user",
            "correct-horse-2",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(inactive_user)
        _, error = set_user_active(
            marketplace_app._cache,
            inactive_user["id"],
            active=False,
        )
        self.assertIsNone(error)
        user_client = marketplace_app.app.test_client()
        login = user_client.post("/api/auth/login", json={
            "username": "permission-user", "password": "correct-horse-1",
        })
        self.assertEqual(login.status_code, 200)
        self.assertTrue(login.get_json()["can_access_admin"])
        self.assertEqual(user_client.get("/api/admin/cache/status").status_code, 200)

        matrix_response = self.client.get(
            "/api/admin/permissions",
            headers=self.basic_auth(),
        )
        self.assertEqual(matrix_response.status_code, 200)
        matrix = matrix_response.get_json()
        admin_role = next(role for role in matrix["roles"] if role["code"] == "admin")
        user_role = next(role for role in matrix["roles"] if role["code"] == "user")
        self.assertRegex(admin_role["revision"], r"^[0-9a-f]{64}$")
        self.assertRegex(user_role["revision"], r"^[0-9a-f]{64}$")
        self.assertEqual(admin_role["member_count"], 0)
        self.assertEqual(admin_role["active_member_count"], 0)
        self.assertEqual(user_role["member_count"], 2)
        self.assertEqual(user_role["active_member_count"], 1)
        permissions = [code for code in user_role["permissions"] if code != "cache:read"]
        updated = self.client.put(
            "/api/admin/roles/user/permissions",
            headers=self.basic_auth(),
            json={
                "permissions": permissions,
                "expected_revision": user_role["revision"],
            },
        )
        self.assertEqual(updated.status_code, 200)
        updated_payload = updated.get_json()
        updated_role = next(
            role for role in updated_payload["roles"] if role["code"] == "user"
        )
        self.assertNotEqual(updated_role["revision"], user_role["revision"])
        self.assertEqual(updated_payload["change"], {
            "role": "user",
            "added": [],
            "removed": ["cache:read"],
            "affected_active_members": 1,
            "revision": updated_role["revision"],
        })
        permission_audits = marketplace_app._cache.get_audit_log(
            action="role_permissions_update",
            target="user",
        )
        self.assertEqual(len(permission_audits), 1)
        self.assertIn("removed_permissions=cache:read", permission_audits[0]["detail"])
        self.assertIn("affected_active_members=1", permission_audits[0]["detail"])
        self.assertIn(f"revision={updated_role['revision']}", permission_audits[0]["detail"])

        stale_update = self.client.put(
            "/api/admin/roles/user/permissions",
            headers=self.basic_auth(),
            json={
                "permissions": [code for code in permissions if code != "jobs:read"],
                "expected_revision": user_role["revision"],
            },
        )
        self.assertEqual(stale_update.status_code, 409)
        self.assertEqual(
            stale_update.get_json()["code"],
            "permission_revision_conflict",
        )
        current_matrix = self.client.get(
            "/api/admin/permissions",
            headers=self.basic_auth(),
        ).get_json()
        current_role = next(
            role for role in current_matrix["roles"] if role["code"] == "user"
        )
        self.assertEqual(current_role["permissions"], updated_role["permissions"])
        self.assertEqual(current_role["revision"], updated_role["revision"])

        forbidden = user_client.get("/api/admin/cache/status")
        self.assertEqual(forbidden.status_code, 403)
        self.assertEqual(forbidden.get_json()["code"], "insufficient_scope")
        self.assertEqual(forbidden.get_json()["required"], ["cache:read"])
        self.assertTrue(user_client.get("/api/auth/session").get_json()["can_access_admin"])

        fixed_admin = self.client.put(
            "/api/admin/roles/admin/permissions",
            headers=self.basic_auth(),
            json={"permissions": []},
        )
        self.assertEqual(fixed_admin.status_code, 409)

        malformed_revision = self.client.put(
            "/api/admin/roles/user/permissions",
            headers=self.basic_auth(),
            json={"permissions": permissions, "expected_revision": "not-a-revision"},
        )
        self.assertEqual(malformed_revision.status_code, 400)

    def test_permission_denials_are_forbidden_without_guest_fallback(self):
        from services.auth_service import create_user

        user, error = create_user(
            marketplace_app._cache,
            "limited-publisher",
            "correct-horse-1",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(user)

        user_client = marketplace_app.app.test_client()
        self.assertEqual(user_client.post("/api/auth/login", json={
            "username": "limited-publisher",
            "password": "correct-horse-1",
        }).status_code, 200)

        matrix = self.client.get(
            "/api/admin/permissions",
            headers=self.basic_auth(),
        ).get_json()
        role = next(item for item in matrix["roles"] if item["code"] == "user")
        removed = {"file:transfer", "plugin:publish", "release:publish"}
        permissions = [code for code in role["permissions"] if code not in removed]
        updated = self.client.put(
            "/api/admin/roles/user/permissions",
            headers=self.basic_auth(),
            json={"permissions": permissions, "expected_revision": role["revision"]},
        )
        self.assertEqual(updated.status_code, 200)
        marketplace_app.CONFIG["anonymous_transfer_upload_enabled"] = True

        protected_requests = (
            (user_client.get("/api/transfer/files"), ["file:transfer"]),
            (user_client.post("/api/transfer/uploads"), ["file:transfer"]),
            (user_client.post("/api/packages/publish"), ["plugin:publish"]),
            (user_client.post("/api/tool/spectrum/publish"), ["release:publish"]),
            (user_client.post("/api/tool/cvwindowsservice/publish"), ["release:publish"]),
        )
        for response, required in protected_requests:
            self.assertEqual(response.status_code, 403)
            self.assertEqual(response.get_json()["code"], "insufficient_scope")
            self.assertEqual(response.get_json()["required"], required)

    def test_user_profile_and_self_service_password_change(self):
        from services.auth_service import create_user

        user, error = create_user(
            marketplace_app._cache,
            "profile-user",
            "correct-horse-1",
            display_name="原昵称",
            email="old@example.com",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(user)
        current_client = marketplace_app.app.test_client()
        other_client = marketplace_app.app.test_client()
        self.assertEqual(current_client.post("/api/auth/login", json={
            "username": "profile-user", "password": "correct-horse-1",
        }).status_code, 200)
        self.assertEqual(other_client.post("/api/auth/login", json={
            "username": "profile-user", "password": "correct-horse-1",
        }).status_code, 200)

        profile = current_client.get("/api/account")
        self.assertEqual(profile.status_code, 200)
        self.assertEqual(profile.get_json()["username"], "profile-user")
        initial_password_changed_at = profile.get_json()["password_changed_at"]
        self.assertEqual(initial_password_changed_at, user["created_at"])
        self.assertTrue(profile.get_json()["can_change_password"])
        self.assertIn("admin:access", profile.get_json()["permissions"])
        permission_details = profile.get_json()["permission_details"]
        self.assertEqual(
            {item["code"] for item in permission_details},
            set(profile.get_json()["permissions"]),
        )
        self.assertEqual(len(permission_details), len(profile.get_json()["permissions"]))
        admin_access = next(
            item for item in permission_details if item["code"] == "admin:access"
        )
        self.assertEqual(admin_access["name"], "管理后台")
        self.assertTrue(admin_access["description"])
        self.assertTrue(admin_access["category"])

        updated_profile = current_client.put("/api/account", json={
            "display_name": "新昵称",
            "email": "NEW@Example.com",
        })
        self.assertEqual(updated_profile.status_code, 200)
        self.assertEqual(updated_profile.get_json()["display_name"], "新昵称")
        self.assertEqual(updated_profile.get_json()["email"], "new@example.com")
        self.assertEqual(updated_profile.get_json()["permission_details"], permission_details)
        self.assertTrue(other_client.get("/api/auth/session").get_json()["authenticated"])

        changed = current_client.put("/api/account/password", json={
            "current_password": "correct-horse-1",
            "new_password": "correct-horse-2",
        })
        self.assertEqual(changed.status_code, 200)
        self.assertTrue(changed.get_json()["current_session_preserved"])
        refreshed_profile = current_client.get("/api/account").get_json()
        self.assertNotEqual(
            refreshed_profile["password_changed_at"],
            initial_password_changed_at,
        )
        self.assertTrue(current_client.get("/api/auth/session").get_json()["authenticated"])
        self.assertFalse(other_client.get("/api/auth/session").get_json()["authenticated"])
        self.assertEqual(other_client.post("/api/auth/login", json={
            "username": "profile-user", "password": "correct-horse-1",
        }).status_code, 401)
        self.assertEqual(other_client.post("/api/auth/login", json={
            "username": "profile-user", "password": "correct-horse-2",
        }).status_code, 200)

    def test_user_can_inspect_and_revoke_individual_login_sessions(self):
        from services.auth_service import create_user

        user, error = create_user(marketplace_app._cache, "session-user", "correct-horse-1")
        self.assertIsNone(error)
        self.assertIsNotNone(user)
        first_client = marketplace_app.app.test_client()
        second_client = marketplace_app.app.test_client()
        first_headers = {"User-Agent": "Mozilla/5.0 First Browser"}
        second_headers = {"User-Agent": "Mozilla/5.0 Second Browser"}

        self.assertEqual(first_client.post(
            "/api/auth/login",
            json={"username": "session-user", "password": "correct-horse-1"},
            headers=first_headers,
            environ_base={"REMOTE_ADDR": "10.0.0.1"},
        ).status_code, 200)
        self.assertEqual(second_client.post(
            "/api/auth/login",
            json={"username": "session-user", "password": "correct-horse-1"},
            headers=second_headers,
            environ_base={"REMOTE_ADDR": "10.0.0.2"},
        ).status_code, 200)

        sessions_response = first_client.get(
            "/api/account/sessions",
            headers=first_headers,
            environ_base={"REMOTE_ADDR": "10.0.0.1"},
        )
        self.assertEqual(sessions_response.status_code, 200)
        sessions = sessions_response.get_json()["items"]
        self.assertEqual(len(sessions), 2)
        current = next(item for item in sessions if item["is_current"])
        other = next(item for item in sessions if not item["is_current"])
        self.assertEqual(current["ip_address"], "10.0.0.1")
        self.assertIn("First Browser", current["user_agent"])

        current_rejected = first_client.delete(
            f"/api/account/sessions/{current['id']}",
            headers=first_headers,
        )
        self.assertEqual(current_rejected.status_code, 409)
        revoked = first_client.delete(
            f"/api/account/sessions/{other['id']}",
            headers=first_headers,
        )
        self.assertEqual(revoked.status_code, 200)
        self.assertFalse(second_client.get(
            "/api/auth/session",
            headers=second_headers,
            environ_base={"REMOTE_ADDR": "10.0.0.2"},
        ).get_json()["authenticated"])

        self.assertEqual(second_client.post(
            "/api/auth/login",
            json={"username": "session-user", "password": "correct-horse-1"},
            headers=second_headers,
        ).status_code, 200)
        revoked_others = first_client.delete(
            "/api/account/sessions/others",
            headers=first_headers,
        )
        self.assertEqual(revoked_others.status_code, 200)
        self.assertEqual(revoked_others.get_json()["revoked"], 1)
        self.assertFalse(second_client.get("/api/auth/session", headers=second_headers).get_json()["authenticated"])

        remaining = first_client.get("/api/account/sessions", headers=first_headers).get_json()["items"]
        self.assertEqual(len(remaining), 1)
        self.assertTrue(remaining[0]["is_current"])
        current_id = remaining[0]["id"]
        self.assertEqual(first_client.post("/api/auth/logout", headers=first_headers).status_code, 200)
        db = marketplace_app._cache.get_db()
        try:
            row = db.execute(
                "SELECT revoked_at, revoke_reason FROM user_sessions WHERE id = ?",
                (current_id,),
            ).fetchone()
            self.assertIsNotNone(row["revoked_at"])
            self.assertEqual(row["revoke_reason"], "logout")
        finally:
            db.close()

    def test_account_activity_is_paginated_and_scoped_to_current_user(self):
        from services.auth_service import create_user

        user, error = create_user(marketplace_app._cache, "activity-user", "correct-horse-1")
        self.assertIsNone(error)
        self.assertIsNotNone(user)
        other, error = create_user(marketplace_app._cache, "other-activity", "correct-horse-1")
        self.assertIsNone(error)
        self.assertIsNotNone(other)
        activity_client = marketplace_app.app.test_client()

        self.assertEqual(activity_client.post("/api/auth/login", json={
            "username": "activity-user", "password": "wrong-password",
        }).status_code, 401)
        self.assertEqual(activity_client.post(
            "/api/auth/login",
            json={"username": "activity-user", "password": "correct-horse-1"},
            headers={"User-Agent": "Activity Browser"},
            environ_base={"REMOTE_ADDR": "10.1.2.3"},
        ).status_code, 200)
        self.assertEqual(activity_client.put("/api/account", json={
            "display_name": "活动用户",
            "email": "activity@example.com",
        }).status_code, 200)
        marketplace_app._cache.write_audit(
            actor_type="user",
            actor_id="admin",
            action="user_role_update",
            target_type="user",
            target_id=str(user["id"]),
            detail="old_role=user;new_role=user",
        )
        marketplace_app._cache.write_audit(
            actor_type="user",
            actor_id="other-activity",
            action="user_profile_update",
            target_type="user",
            target_id=str(other["id"]),
        )

        first_page = activity_client.get("/api/account/activity?limit=2&offset=0")
        self.assertEqual(first_page.status_code, 200)
        page = first_page.get_json()
        self.assertEqual(len(page["entries"]), 2)
        self.assertEqual(page["total"], 4)
        self.assertEqual(page["limit"], 2)
        self.assertEqual(page["offset"], 0)
        self.assertGreaterEqual(page["summary"]["failed_logins"], 1)
        self.assertGreaterEqual(page["summary"]["security_events"], 2)

        all_activity = activity_client.get("/api/account/activity?limit=50&offset=0").get_json()
        actions = {entry["action"] for entry in all_activity["entries"]}
        self.assertTrue({
            "login_failed", "login_success", "user_profile_update", "user_role_update",
        } <= actions)
        admin_entry = next(
            entry for entry in all_activity["entries"] if entry["action"] == "user_role_update"
        )
        self.assertEqual(admin_entry["source"], "administrator")
        self.assertNotIn("actor_id", admin_entry)
        self.assertNotIn("target_id", admin_entry)
        self.assertEqual(len(all_activity["entries"]), 4)

        for query in ("limit=0", "limit=51", "offset=-1", "limit=invalid"):
            self.assertEqual(
                activity_client.get(f"/api/account/activity?{query}").status_code,
                400,
            )

    def test_account_settings_reject_invalid_payload_and_limited_keys(self):
        invalid = self.client.put(
            "/api/admin/settings/accounts",
            headers=self.basic_auth(),
            json={"public_registration_enabled": "true"},
        )
        self.assertEqual(invalid.status_code, 400)

        key = self.create_admin_key("stats:read")
        headers = {"Authorization": f"Bearer {key['key']}"}
        self.assertEqual(
            self.client.get("/api/admin/settings/accounts", headers=headers).status_code,
            403,
        )
        self.assertEqual(
            self.client.put(
                "/api/admin/settings/accounts",
                headers=headers,
                json={"public_registration_enabled": True},
            ).status_code,
            403,
        )

    def test_user_accounts_can_be_listed_disabled_and_reenabled(self):
        from services.auth_service import create_user
        from services.login_throttle_service import (
            get_login_throttle_status,
            record_login_failure,
        )
        from services.password_recovery_service import (
            get_pending_password_recovery,
            submit_password_recovery_request,
        )

        user, error = create_user(marketplace_app._cache, "worker", "correct-horse-1")
        self.assertIsNone(error)
        self.assertIsNotNone(user)
        worker_client = marketplace_app.app.test_client()

        users_response = self.client.get("/api/admin/users", headers=self.basic_auth())
        self.assertEqual(users_response.status_code, 200)
        listed = next(item for item in users_response.get_json() if item["id"] == user["id"])
        self.assertNotIn("password_hash", listed)
        self.assertTrue(listed["is_active"])

        login_response = worker_client.post("/api/auth/login", json={
            "username": "worker", "password": "correct-horse-1",
        })
        self.assertEqual(login_response.status_code, 200)
        self.assertTrue(submit_password_recovery_request(
            marketplace_app._cache,
            "worker",
            ip_address="192.0.2.70",
        ).recorded)
        record_login_failure(marketplace_app._cache, "worker", "192.0.2.71")
        record_login_failure(marketplace_app._cache, "worker", "192.0.2.72")

        disabled = self.client.post(
            f"/api/admin/users/{user['id']}/disable",
            headers=self.basic_auth(),
        )
        self.assertEqual(disabled.status_code, 200)
        disabled_payload = disabled.get_json()
        self.assertFalse(disabled_payload["is_active"])
        self.assertEqual(disabled_payload["sessions_revoked"], 1)
        self.assertEqual(disabled_payload["login_failure_sources_cleared"], 2)
        self.assertEqual(disabled_payload["password_recovery_requests_resolved"], 1)
        self.assertIsNone(get_pending_password_recovery(
            marketplace_app._cache,
            user["id"],
        ))
        self.assertEqual(
            get_login_throttle_status(marketplace_app._cache, "worker").failed_count,
            0,
        )

        record_login_failure(marketplace_app._cache, "worker", "192.0.2.73")

        # Re-enabling the account before its old cookie is used must not revive
        # that cookie. Status changes version authentication state.
        enabled = self.client.post(
            f"/api/admin/users/{user['id']}/enable",
            headers=self.basic_auth(),
        )
        self.assertEqual(enabled.status_code, 200)
        enabled_payload = enabled.get_json()
        self.assertTrue(enabled_payload["is_active"])
        self.assertEqual(enabled_payload["sessions_revoked"], 0)
        self.assertEqual(enabled_payload["login_failure_sources_cleared"], 1)
        self.assertEqual(enabled_payload["password_recovery_requests_resolved"], 0)
        self.assertFalse(worker_client.get("/api/auth/session").get_json()["authenticated"])
        self.assertEqual(
            worker_client.post("/api/auth/login", json={
                "username": "worker", "password": "correct-horse-1",
            }).status_code,
            200,
        )

        actions = {entry["action"] for entry in marketplace_app._cache.get_audit_log(target=str(user["id"]))}
        self.assertIn("user_disable", actions)
        self.assertIn("user_enable", actions)

    def test_user_list_supports_search_filters_and_pagination(self):
        from services.auth_service import (
            create_user,
            require_user_password_change,
            set_user_active,
        )

        alpha, error = create_user(
            marketplace_app._cache,
            "alpha-worker",
            "correct-horse-1",
            display_name="Alpha 工程师",
            email="alpha@example.com",
            account_origin="self_registered",
        )
        self.assertIsNone(error)
        beta, error = create_user(
            marketplace_app._cache,
            "beta-worker",
            "correct-horse-1",
            display_name="Beta 工程师",
            email="beta@example.com",
            account_origin="administrator_created",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(alpha)
        self.assertIsNotNone(beta)
        _, error = require_user_password_change(
            marketplace_app._cache,
            int(alpha["id"]),
        )
        self.assertIsNone(error)
        _, error = set_user_active(marketplace_app._cache, int(beta["id"]), active=False)
        self.assertIsNone(error)

        searched = self.client.get(
            "/api/admin/users?q=ALPHA&role=user&origin=self_registered&status=active&password_state=pending&limit=10&offset=0",
            headers=self.basic_auth(),
        )
        self.assertEqual(searched.status_code, 200)
        page = searched.get_json()
        self.assertEqual(page["total"], 1)
        self.assertEqual(page["limit"], 10)
        self.assertEqual(page["offset"], 0)
        self.assertEqual([item["username"] for item in page["items"]], ["alpha-worker"])
        self.assertNotIn("password_hash", page["items"][0])
        self.assertEqual(page["items"][0]["account_origin"], "self_registered")
        self.assertGreaterEqual(page["summary"]["total"], 2)
        self.assertGreaterEqual(page["summary"]["inactive"], 1)
        self.assertGreaterEqual(page["summary"]["pending_password_changes"], 1)
        self.assertGreaterEqual(page["summary"]["self_registered"], 1)
        self.assertGreaterEqual(page["summary"]["administrator_created"], 1)

        inactive = self.client.get(
            "/api/admin/users?q=beta%40example.com&origin=administrator_created&status=inactive&password_state=ready&limit=1&offset=0",
            headers=self.basic_auth(),
        )
        self.assertEqual(inactive.status_code, 200)
        self.assertEqual(inactive.get_json()["items"][0]["username"], "beta-worker")

        ascending = self.client.get(
            "/api/admin/users?sort_by=username&sort_order=asc&limit=10&offset=0",
            headers=self.basic_auth(),
        )
        self.assertEqual(ascending.status_code, 200)
        self.assertEqual(
            [item["username"] for item in ascending.get_json()["items"]],
            ["alpha-worker", "beta-worker"],
        )
        descending = self.client.get(
            "/api/admin/users?sort_by=username&sort_order=desc&limit=10&offset=0",
            headers=self.basic_auth(),
        )
        self.assertEqual(descending.status_code, 200)
        self.assertEqual(
            [item["username"] for item in descending.get_json()["items"]],
            ["beta-worker", "alpha-worker"],
        )
        for sort_field in (
            "username", "display_name", "email", "role", "is_active",
            "account_origin", "active_session_count", "created_at", "last_login_at",
            "password_recovery_requested_at",
        ):
            response = self.client.get(
                f"/api/admin/users?sort_by={sort_field}&sort_order=asc&limit=10&offset=0",
                headers=self.basic_auth(),
            )
            self.assertEqual(response.status_code, 200, sort_field)

        for query in (
            "role=owner", "origin=imported", "status=locked", "password_state=unknown",
            "sort_by=password_hash", "sort_order=sideways", "limit=101", "offset=-1",
        ):
            response = self.client.get(
                f"/api/admin/users?{query}",
                headers=self.basic_auth(),
            )
            self.assertEqual(response.status_code, 400)

    def test_admin_can_inspect_account_details_sessions_permissions_and_activity(self):
        from services.auth_service import create_user

        user, error = create_user(
            marketplace_app._cache,
            "detail-worker",
            "correct-horse-1",
            display_name="详情用户",
            email="detail@example.com",
            account_origin="self_registered",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(user)

        worker_client = marketplace_app.app.test_client()
        self.assertEqual(worker_client.post(
            "/api/auth/login",
            json={"username": "detail-worker", "password": "correct-horse-1"},
            environ_base={"REMOTE_ADDR": "192.0.2.24"},
            headers={"User-Agent": "Detail Browser"},
        ).status_code, 200)

        response = self.client.get(
            f"/api/admin/users/{user['id']}/details?activity_limit=1&activity_offset=0",
            headers=self.basic_auth(),
        )
        self.assertEqual(response.status_code, 200)
        details = response.get_json()
        self.assertEqual(details["user"]["username"], "detail-worker")
        self.assertEqual(details["user"]["account_origin"], "self_registered")
        self.assertEqual(details["user"]["active_session_count"], 1)
        self.assertNotIn("password_hash", details["user"])
        self.assertNotIn("auth_version", details["user"])
        self.assertEqual(details["sessions"]["total"], 1)
        self.assertEqual(details["sessions"]["items"][0]["ip_address"], "192.0.2.24")
        self.assertFalse(details["sessions"]["items"][0]["is_current"])
        self.assertIn("admin:access", {
            permission["code"] for permission in details["permissions"]
        })
        self.assertGreaterEqual(details["activity"]["total"], 1)
        self.assertEqual(len(details["activity"]["entries"]), 1)
        self.assertEqual(details["activity"]["entries"][0]["action"], "login_success")

        self.assertEqual(self.client.get(
            "/api/admin/users/999999/details",
            headers=self.basic_auth(),
        ).status_code, 404)
        for query in ("activity_limit=0", "activity_limit=51", "activity_offset=-1"):
            self.assertEqual(self.client.get(
                f"/api/admin/users/{user['id']}/details?{query}",
                headers=self.basic_auth(),
            ).status_code, 400)

    def test_password_recovery_is_private_visible_to_admin_and_resolved_by_reset(self):
        from services.auth_service import create_user
        from services.login_throttle_service import record_login_failure

        user, error = create_user(
            marketplace_app._cache,
            "recovery-worker",
            "correct-horse-1",
            email="recovery@example.com",
            account_origin="self_registered",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(user)

        known = self.client.post(
            "/api/auth/password-recovery",
            json={"identifier": "RECOVERY@EXAMPLE.COM"},
            environ_base={"REMOTE_ADDR": "192.0.2.50"},
        )
        unknown = self.client.post(
            "/api/auth/password-recovery",
            json={"identifier": "missing@example.com"},
            environ_base={"REMOTE_ADDR": "192.0.2.51"},
        )
        configured_admin = self.client.post(
            "/api/auth/password-recovery",
            json={"identifier": "admin"},
            environ_base={"REMOTE_ADDR": "192.0.2.52"},
        )
        self.assertEqual(known.status_code, 202)
        self.assertEqual(unknown.status_code, 202)
        self.assertEqual(configured_admin.status_code, 202)
        self.assertEqual(known.get_json(), unknown.get_json())
        self.assertEqual(known.get_json(), configured_admin.get_json())
        self.assertNotIn("recovery-worker", json.dumps(known.get_json()))
        self.assertEqual(self.client.post(
            "/api/auth/password-recovery",
            json={"identifier": ""},
        ).status_code, 400)
        self.assertEqual(self.client.post(
            "/api/auth/password-recovery",
            json={"identifier": "x" * 255},
        ).status_code, 400)

        pending_page = self.client.get(
            "/api/admin/users?recovery_state=pending&limit=20&offset=0",
            headers=self.basic_auth(),
        )
        self.assertEqual(pending_page.status_code, 200)
        pending_payload = pending_page.get_json()
        self.assertEqual(pending_payload["total"], 1)
        self.assertEqual(pending_payload["items"][0]["username"], "recovery-worker")
        self.assertTrue(pending_payload["items"][0]["password_recovery_pending"])
        self.assertEqual(pending_payload["items"][0]["password_recovery_request_count"], 1)
        self.assertGreaterEqual(pending_payload["summary"]["pending_password_recovery"], 1)

        details = self.client.get(
            f"/api/admin/users/{user['id']}/details",
            headers=self.basic_auth(),
        ).get_json()
        self.assertEqual(details["password_recovery"]["request_count"], 1)
        self.assertEqual(details["password_recovery"]["last_ip"], "192.0.2.50")
        self.assertIsNotNone(details["password_recovery"]["expires_at"])
        record_login_failure(
            marketplace_app._cache,
            "recovery-worker",
            "192.0.2.53",
        )
        record_login_failure(
            marketplace_app._cache,
            "recovery-worker",
            "192.0.2.54",
        )

        reset = self.client.post(
            f"/api/admin/users/{user['id']}/password",
            headers=self.basic_auth(),
            json={"password": "correct-horse-2"},
        )
        self.assertEqual(reset.status_code, 200)
        self.assertEqual(reset.get_json()["password_recovery_requests_resolved"], 1)
        self.assertEqual(reset.get_json()["login_failure_sources_cleared"], 2)
        self.assertTrue(reset.get_json()["must_change_password"])
        self.assertIsNone(self.client.get(
            f"/api/admin/users/{user['id']}/details",
            headers=self.basic_auth(),
        ).get_json()["password_recovery"])
        self.assertEqual(self.client.get(
            "/api/admin/users?recovery_state=pending&limit=20&offset=0",
            headers=self.basic_auth(),
        ).get_json()["total"], 0)
        self.assertEqual(self.client.get(
            "/api/admin/users?recovery_state=invalid&limit=20&offset=0",
            headers=self.basic_auth(),
        ).status_code, 400)

        recovery_client = marketplace_app.app.test_client()
        login = recovery_client.post("/api/auth/login", json={
            "username": "recovery-worker",
            "password": "correct-horse-2",
        })
        self.assertEqual(login.status_code, 200)
        self.assertTrue(login.get_json()["must_change_password"])

        audit = marketplace_app._cache.get_audit_log(target=str(user["id"]))
        self.assertIn("user_password_recovery_request", {
            entry["action"] for entry in audit
        })

    def test_admin_can_inspect_and_revoke_active_user_sessions(self):
        from services.auth_service import create_user

        user, error = create_user(marketplace_app._cache, "online-worker", "correct-horse-1")
        self.assertIsNone(error)
        self.assertIsNotNone(user)
        first_client = marketplace_app.app.test_client()
        second_client = marketplace_app.app.test_client()
        for client, address in (
            (first_client, "10.0.0.11"),
            (second_client, "10.0.0.12"),
        ):
            self.assertEqual(client.post(
                "/api/auth/login",
                json={"username": "online-worker", "password": "correct-horse-1"},
                environ_base={"REMOTE_ADDR": address},
            ).status_code, 200)
        stale_at = "2026-08-29T10:00:00+00:00"
        db = marketplace_app._cache.get_db()
        try:
            db.execute(
                """INSERT INTO user_sessions
                       (id, user_id, auth_version, ip_address, user_agent,
                        created_at, last_seen_at)
                   VALUES ('stale-session', ?, -1, '', '', ?, ?)""",
                (user["id"], stale_at, stale_at),
            )
            db.commit()
        finally:
            db.close()

        listing = self.client.get(
            "/api/admin/users?q=online-worker&limit=10&offset=0",
            headers=self.basic_auth(),
        )
        self.assertEqual(listing.status_code, 200)
        self.assertEqual(listing.get_json()["items"][0]["active_session_count"], 2)
        unpaged = self.client.get("/api/admin/users", headers=self.basic_auth()).get_json()
        self.assertEqual(
            next(item for item in unpaged if item["id"] == user["id"])["active_session_count"],
            2,
        )

        revoked = self.client.post(
            f"/api/admin/users/{user['id']}/sessions/revoke",
            headers=self.basic_auth(),
        )
        self.assertEqual(revoked.status_code, 200)
        self.assertEqual(revoked.get_json(), {
            "status": "revoked",
            "user_id": user["id"],
            "username": "online-worker",
            "revoked": 2,
        })
        self.assertFalse(first_client.get("/api/auth/session").get_json()["authenticated"])
        self.assertFalse(second_client.get("/api/auth/session").get_json()["authenticated"])
        db = marketplace_app._cache.get_db()
        try:
            stale = db.execute(
                "SELECT revoked_at, revoke_reason FROM user_sessions WHERE id = 'stale-session'"
            ).fetchone()
            self.assertIsNotNone(stale["revoked_at"])
            self.assertEqual(stale["revoke_reason"], "authentication_changed")
        finally:
            db.close()

        refreshed = self.client.get(
            "/api/admin/users?q=online-worker&limit=10&offset=0",
            headers=self.basic_auth(),
        ).get_json()["items"][0]
        self.assertEqual(refreshed["active_session_count"], 0)
        self.assertTrue(refreshed["is_active"])

        self.assertEqual(first_client.post(
            "/api/auth/login",
            json={"username": "online-worker", "password": "correct-horse-1"},
        ).status_code, 200)
        self_force = first_client.post(
            f"/api/admin/users/{user['id']}/sessions/revoke",
        )
        self.assertEqual(self_force.status_code, 409)
        self.assertTrue(first_client.get("/api/auth/session").get_json()["authenticated"])

        audits = marketplace_app._cache.get_audit_log(
            action="user_sessions_force_revoke",
            target=str(user["id"]),
        )
        self.assertEqual(len(audits), 1)
        self.assertIn("sessions_revoked=2", audits[0]["detail"])

        activity = first_client.get("/api/account/activity?limit=20&offset=0").get_json()
        forced = next(
            entry for entry in activity["entries"]
            if entry["action"] == "user_sessions_force_revoke"
        )
        self.assertEqual(forced["source"], "administrator")
        self.assertTrue(forced["security"])

    def test_admin_can_apply_bounded_bulk_user_security_actions(self):
        from services.auth_service import create_user, get_user_by_id
        from services.login_throttle_service import (
            get_login_throttle_status,
            record_login_failure,
        )

        first, error = create_user(
            marketplace_app._cache,
            "bulk-security-one",
            "correct-horse-1",
        )
        self.assertIsNone(error)
        second, error = create_user(
            marketplace_app._cache,
            "bulk-security-two",
            "correct-horse-2",
        )
        self.assertIsNone(error)
        config_shadow, error = create_user(
            marketplace_app._cache,
            "admin",
            "database-shadow-password",
            role="admin",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(first)
        self.assertIsNotNone(second)
        self.assertIsNotNone(config_shadow)

        first_client = marketplace_app.app.test_client()
        second_client = marketplace_app.app.test_client()
        self.assertEqual(first_client.post("/api/auth/login", json={
            "username": "bulk-security-one",
            "password": "correct-horse-1",
        }).status_code, 200)
        self.assertEqual(second_client.post("/api/auth/login", json={
            "username": "bulk-security-two",
            "password": "correct-horse-2",
        }).status_code, 200)

        self_blocked = first_client.post(
            "/api/admin/users/bulk-security",
            json={"action": "force_logout", "user_ids": [first["id"]]},
        )
        self.assertEqual(self_blocked.status_code, 200)
        self.assertEqual(self_blocked.get_json()["succeeded"], 0)
        self.assertEqual(self_blocked.get_json()["failed"], 1)
        self.assertEqual(
            self_blocked.get_json()["results"][0]["code"],
            "current_session_account",
        )
        self.assertTrue(first_client.get("/api/auth/session").get_json()["authenticated"])

        forced = self.client.post(
            "/api/admin/users/bulk-security",
            headers=self.basic_auth(),
            json={
                "action": "force_logout",
                "user_ids": [
                    first["id"], second["id"], first["id"],
                    config_shadow["id"], 999999,
                ],
            },
        )
        self.assertEqual(forced.status_code, 200)
        forced_payload = forced.get_json()
        self.assertEqual(forced_payload["requested"], 4)
        self.assertEqual(forced_payload["succeeded"], 2)
        self.assertEqual(forced_payload["failed"], 2)
        self.assertEqual(forced_payload["sessions_revoked"], 2)
        self.assertEqual(
            {
                item.get("code") for item in forced_payload["results"]
                if item["status"] == "failed"
            },
            {"config_admin_managed", "user_not_found"},
        )
        self.assertFalse(first_client.get("/api/auth/session").get_json()["authenticated"])
        self.assertFalse(second_client.get("/api/auth/session").get_json()["authenticated"])

        self.assertEqual(first_client.post("/api/auth/login", json={
            "username": "bulk-security-one",
            "password": "correct-horse-1",
        }).status_code, 200)
        self.assertEqual(second_client.post("/api/auth/login", json={
            "username": "bulk-security-two",
            "password": "correct-horse-2",
        }).status_code, 200)
        record_login_failure(
            marketplace_app._cache,
            "bulk-security-one",
            "192.0.2.81",
        )
        record_login_failure(
            marketplace_app._cache,
            "bulk-security-two",
            "192.0.2.82",
        )
        required = self.client.post(
            "/api/admin/users/bulk-security",
            headers=self.basic_auth(),
            json={
                "action": "require_password_change",
                "user_ids": [first["id"], second["id"]],
            },
        )
        self.assertEqual(required.status_code, 200)
        required_payload = required.get_json()
        self.assertEqual(required_payload["succeeded"], 2)
        self.assertEqual(required_payload["failed"], 0)
        self.assertEqual(required_payload["sessions_revoked"], 2)
        self.assertEqual(required_payload["login_failure_sources_cleared"], 2)
        self.assertTrue(all(
            item["login_failure_sources_cleared"] == 1
            for item in required_payload["results"]
        ))
        self.assertEqual(get_login_throttle_status(
            marketplace_app._cache,
            "bulk-security-one",
        ).failed_count, 0)
        self.assertEqual(get_login_throttle_status(
            marketplace_app._cache,
            "bulk-security-two",
        ).failed_count, 0)
        self.assertTrue(get_user_by_id(
            marketplace_app._cache,
            first["id"],
        )["must_change_password"])
        self.assertTrue(get_user_by_id(
            marketplace_app._cache,
            second["id"],
        )["must_change_password"])

        summary_audits = marketplace_app._cache.get_audit_log(
            action="user_bulk_security_action"
        )
        self.assertEqual(len(summary_audits), 3)
        forced_summary = next(
            audit for audit in summary_audits if "requested=4" in audit["detail"]
        )
        self.assertIn("succeeded=2", forced_summary["detail"])

        invalid_payloads = (
            {"action": "delete", "user_ids": [first["id"]]},
            {"action": "force_logout", "user_ids": []},
            {"action": "force_logout", "user_ids": [True]},
            {"action": "force_logout", "user_ids": list(range(1, 102))},
        )
        for payload in invalid_payloads:
            with self.subTest(action=payload["action"]):
                self.assertEqual(self.client.post(
                    "/api/admin/users/bulk-security",
                    headers=self.basic_auth(),
                    json=payload,
                ).status_code, 400)

    def test_admin_can_inspect_and_clear_login_throttles(self):
        from services.auth_service import create_user

        user, error = create_user(
            marketplace_app._cache,
            "security-worker",
            "correct-password",
            display_name="安全测试用户",
            email="security-worker@example.com",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(user)
        worker_client = marketplace_app.app.test_client()
        for index in range(5):
            response = worker_client.post(
                "/api/auth/login",
                json={"username": "security-worker", "password": "wrong"},
                environ_base={"REMOTE_ADDR": f"10.20.30.{index + 1}"},
            )
        self.assertEqual(response.status_code, 429)

        listing = self.client.get(
            "/api/admin/login-security?q=%E5%AE%89%E5%85%A8&status=locked&limit=10&offset=0",
            headers=self.basic_auth(),
        )
        self.assertEqual(listing.status_code, 200)
        page = listing.get_json()
        self.assertEqual(page["total"], 1)
        self.assertEqual(page["summary"]["locked"], 1)
        item = page["items"][0]
        self.assertEqual(item["username"], "security-worker")
        self.assertEqual(item["account_type"], "registered")
        self.assertEqual(item["user_id"], user["id"])
        self.assertEqual(item["failed_count"], 5)
        self.assertEqual(item["source_count"], 5)
        self.assertEqual(len(item["sources"]), 5)
        self.assertTrue(item["locked"])

        unlocked = self.client.post(
            "/api/admin/login-security/unlock",
            headers=self.basic_auth(),
            json={"username": "SECURITY-WORKER"},
        )
        self.assertEqual(unlocked.status_code, 200)
        self.assertEqual(unlocked.get_json()["status"], "unlocked")
        self.assertEqual(unlocked.get_json()["cleared_sources"], 5)
        self.assertEqual(
            self.client.get(
                "/api/admin/login-security?limit=10&offset=0",
                headers=self.basic_auth(),
            ).get_json()["total"],
            0,
        )
        self.assertEqual(
            worker_client.post("/api/auth/login", json={
                "username": "security-worker",
                "password": "correct-password",
            }).status_code,
            200,
        )
        audit = marketplace_app._cache.get_audit_log(action="login_throttle_unlock")
        self.assertEqual(len(audit), 1)
        self.assertEqual(audit[0]["target_type"], "user")
        self.assertEqual(audit[0]["target_id"], str(user["id"]))

        activity = worker_client.get("/api/account/activity?limit=20&offset=0").get_json()
        unlock_entry = next(
            entry for entry in activity["entries"]
            if entry["action"] == "login_throttle_unlock"
        )
        self.assertEqual(unlock_entry["source"], "administrator")

        for query in ("status=invalid", "limit=101", "offset=-1"):
            self.assertEqual(
                self.client.get(
                    f"/api/admin/login-security?{query}",
                    headers=self.basic_auth(),
                ).status_code,
                400,
            )
        self.assertEqual(
            self.client.post(
                "/api/admin/login-security/unlock",
                headers=self.basic_auth(),
                json={"username": ""},
            ).status_code,
            400,
        )

    def test_admin_can_inspect_and_clear_registration_source_limits(self):
        marketplace_app.CONFIG["public_registration_enabled"] = True
        blocked_source = {"REMOTE_ADDR": "198.51.100.90"}
        for _ in range(20):
            response = marketplace_app.app.test_client().post(
                "/api/auth/register",
                json={"username": "x", "password": "short"},
                environ_base=blocked_source,
            )
            self.assertEqual(response.status_code, 400)
        self.assertEqual(marketplace_app.app.test_client().post(
            "/api/auth/register",
            json={"username": "blocked-source-user", "password": "correct-horse-1"},
            environ_base=blocked_source,
        ).status_code, 429)
        self.assertEqual(marketplace_app.app.test_client().post(
            "/api/auth/register",
            json={"username": "tracked-source-user", "password": "correct-horse-1"},
            environ_base={"REMOTE_ADDR": "203.0.113.91"},
        ).status_code, 201)

        listing = self.client.get(
            "/api/admin/registration-security?q=198.51.100&status=blocked&limit=10&offset=0",
            headers=self.basic_auth(),
        )
        self.assertEqual(listing.status_code, 200)
        page = listing.get_json()
        self.assertEqual(page["total"], 1)
        self.assertEqual(page["summary"]["total"], 2)
        self.assertEqual(page["summary"]["blocked"], 1)
        self.assertEqual(page["summary"]["tracking"], 1)
        item = page["items"][0]
        self.assertEqual(item["ip_address"], "198.51.100.90")
        self.assertEqual(item["attempt_count"], 20)
        self.assertEqual(item["attempt_limit"], 20)
        self.assertEqual(item["reason"], "attempt_velocity")
        self.assertTrue(item["blocked"])
        self.assertGreater(item["retry_after"], 0)

        stats_key = self.create_admin_key("stats:read")
        self.assertEqual(self.client.get(
            "/api/admin/registration-security",
            headers={"Authorization": f"Bearer {stats_key['key']}"},
        ).status_code, 403)
        admin_key_response = self.client.post(
            "/api/admin/api-keys",
            headers=self.basic_auth(),
            json={"name": "Registration Security Key", "scopes": "admin:*"},
        )
        self.assertEqual(
            admin_key_response.status_code,
            201,
            admin_key_response.get_json(),
        )
        admin_key = admin_key_response.get_json()
        self.assertEqual(self.client.get(
            "/api/admin/registration-security",
            headers={"Authorization": f"Bearer {admin_key['key']}"},
        ).status_code, 200)

        cleared = self.client.post(
            "/api/admin/registration-security/clear",
            headers=self.basic_auth(),
            json={"ip_address": "198.51.100.90"},
        )
        self.assertEqual(cleared.status_code, 200)
        self.assertEqual(cleared.get_json(), {
            "status": "cleared",
            "ip_address": "198.51.100.90",
            "cleared": True,
            "pending_count": 0,
        })
        self.assertEqual(marketplace_app.app.test_client().post(
            "/api/auth/register",
            json={"username": "unblocked-source-user", "password": "correct-horse-1"},
            environ_base=blocked_source,
        ).status_code, 201)
        audit = marketplace_app._cache.get_audit_log(action="registration_throttle_clear")
        self.assertEqual(len(audit), 1)
        self.assertEqual(audit[0]["target_id"], "198.51.100.90")
        self.assertIn("cleared=true", audit[0]["detail"])

        for query in ("status=invalid", "limit=101", "offset=-1"):
            self.assertEqual(self.client.get(
                f"/api/admin/registration-security?{query}",
                headers=self.basic_auth(),
            ).status_code, 400)
        self.assertEqual(self.client.post(
            "/api/admin/registration-security/clear",
            headers=self.basic_auth(),
            json={"ip_address": ""},
        ).status_code, 400)

    def test_admin_can_create_promote_and_reset_an_account(self):
        created_response = self.client.post(
            "/api/admin/users",
            headers=self.basic_auth(),
            json={
                "username": "managed",
                "password": "correct-horse-1",
                "role": "user",
                "display_name": "托管用户",
                "email": "managed@example.com",
            },
        )
        self.assertEqual(created_response.status_code, 201)
        created = created_response.get_json()
        self.assertEqual(created["role"], "user")
        self.assertEqual(created["display_name"], "托管用户")
        self.assertEqual(created["email"], "managed@example.com")
        self.assertEqual(created["account_origin"], "administrator_created")
        self.assertTrue(created["must_change_password"])
        self.assertNotIn("password_hash", created)
        self.assertNotIn("auth_version", created)

        profile_response = self.client.put(
            f"/api/admin/users/{created['id']}/profile",
            headers=self.basic_auth(),
            json={"display_name": "已维护用户", "email": "updated@example.com"},
        )
        self.assertEqual(profile_response.status_code, 200)
        self.assertEqual(profile_response.get_json()["display_name"], "已维护用户")
        self.assertEqual(profile_response.get_json()["email"], "updated@example.com")

        managed_client = marketplace_app.app.test_client()
        login = managed_client.post("/api/auth/login", json={
            "username": "managed", "password": "correct-horse-1",
        })
        self.assertEqual(login.status_code, 200)
        self.assertFalse(login.get_json()["is_admin"])

        promoted_response = self.client.put(
            f"/api/admin/users/{created['id']}/role",
            headers=self.basic_auth(),
            json={"role": "admin"},
        )
        self.assertEqual(promoted_response.status_code, 200)
        self.assertEqual(promoted_response.get_json()["role"], "admin")
        self.assertFalse(managed_client.get("/api/auth/session").get_json()["authenticated"])

        promoted_login = managed_client.post("/api/auth/login", json={
            "username": "managed", "password": "correct-horse-1",
        })
        self.assertEqual(promoted_login.status_code, 200)
        self.assertTrue(promoted_login.get_json()["is_admin"])

        reset_response = self.client.post(
            f"/api/admin/users/{created['id']}/password",
            headers=self.basic_auth(),
            json={"password": "correct-horse-2"},
        )
        self.assertEqual(reset_response.status_code, 200)
        reset = reset_response.get_json()
        self.assertTrue(reset["sessions_invalidated"])
        self.assertFalse(reset["current_session_preserved"])
        self.assertTrue(reset["must_change_password"])
        self.assertNotIn("auth_version", reset)
        self.assertFalse(managed_client.get("/api/auth/session").get_json()["authenticated"])
        self.assertEqual(managed_client.post("/api/auth/login", json={
            "username": "managed", "password": "correct-horse-1",
        }).status_code, 401)
        reset_login = managed_client.post("/api/auth/login", json={
            "username": "managed", "password": "correct-horse-2",
        })
        self.assertEqual(reset_login.status_code, 200)
        self.assertTrue(reset_login.get_json()["must_change_password"])
        self.assertEqual(
            reset_login.get_json()["next"],
            "/account?password_change=required",
        )

        audit = marketplace_app._cache.get_audit_log(target=str(created["id"]))
        actions = {entry["action"] for entry in audit}
        self.assertTrue({
            "user_create", "user_profile_update", "user_role_update", "user_password_reset",
        } <= actions)
        serialized_audit = json.dumps(audit)
        self.assertNotIn("correct-horse-1", serialized_audit)
        self.assertNotIn("correct-horse-2", serialized_audit)

    def test_revoked_database_session_cannot_reach_any_publish_entrypoint(self):
        from services.auth_service import create_user

        user, error = create_user(
            marketplace_app._cache,
            "revoked-publisher",
            "correct-horse-1",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(user)

        user_client = marketplace_app.app.test_client()
        login = user_client.post("/api/auth/login", json={
            "username": "revoked-publisher",
            "password": "correct-horse-1",
        })
        self.assertEqual(login.status_code, 200)

        disabled = self.client.post(
            f"/api/admin/users/{user['id']}/disable",
            headers=self.basic_auth(),
        )
        self.assertEqual(disabled.status_code, 200)

        protected_requests = (
            user_client.post("/api/packages/publish"),
            user_client.post("/api/tool/spectrum/publish"),
            user_client.post("/api/tool/cvwindowsservice/publish"),
            user_client.put("/upload/Plugins/RevokedPublisher/package.cvx"),
        )
        for response in protected_requests:
            self.assertEqual(response.status_code, 401)
        self.assertFalse(user_client.get("/api/auth/session").get_json()["authenticated"])

    def test_admin_must_disable_and_confirm_an_account_before_permanent_deletion(self):
        from services.auth_service import create_user
        from services.password_recovery_service import submit_password_recovery_request

        user, error = create_user(
            marketplace_app._cache,
            "retired-user",
            "correct-horse-1",
            account_origin="self_registered",
        )
        self.assertIsNone(error)
        user_client = marketplace_app.app.test_client()
        self.assertEqual(user_client.post("/api/auth/login", json={
            "username": "retired-user",
            "password": "correct-horse-1",
        }).status_code, 200)
        submit_password_recovery_request(
            marketplace_app._cache,
            "retired-user",
            ip_address="192.0.2.30",
        )

        active = self.client.delete(
            f"/api/admin/users/{user['id']}",
            headers=self.basic_auth(),
            json={"username": "retired-user"},
        )
        self.assertEqual(active.status_code, 409)
        self.assertEqual(active.get_json()["code"], "account_must_be_disabled")

        disabled = self.client.post(
            f"/api/admin/users/{user['id']}/disable",
            headers=self.basic_auth(),
        )
        self.assertEqual(disabled.status_code, 200)
        mismatch = self.client.delete(
            f"/api/admin/users/{user['id']}",
            headers=self.basic_auth(),
            json={"username": "another-user"},
        )
        self.assertEqual(mismatch.status_code, 400)

        admin_client = marketplace_app.app.test_client()
        admin_login = admin_client.post("/api/auth/login", json={
            "username": "admin",
            "password": "secret",
        })
        self.assertEqual(admin_login.status_code, 200)
        response = admin_client.delete(
            f"/api/admin/users/{user['id']}",
            headers={
                "Origin": "http://localhost",
                "Sec-Fetch-Site": "same-origin",
                "X-CSRF-Token": admin_login.get_json()["csrf_token"],
            },
            json={"username": "retired-user"},
        )
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.get_json(), {
            "status": "deleted",
            "id": user["id"],
            "username": "retired-user",
            "role": "user",
            "account_origin": "self_registered",
            "sessions_deleted": 1,
            "password_recovery_requests_deleted": 1,
            "login_failure_sources_cleared": 0,
        })
        self.assertEqual(self.client.get(
            f"/api/admin/users/{user['id']}/details",
            headers=self.basic_auth(),
        ).status_code, 404)
        self.assertEqual(user_client.post("/api/auth/login", json={
            "username": "retired-user",
            "password": "correct-horse-1",
        }).status_code, 401)
        audit = marketplace_app._cache.get_audit_log(
            action="user_delete",
            target=str(user["id"]),
        )
        self.assertEqual(len(audit), 1)
        self.assertIn("username=retired-user", audit[0]["detail"])
        self.assertIn("sessions_deleted=1", audit[0]["detail"])

    def test_current_admin_password_reset_preserves_only_current_session(self):
        from services.auth_service import create_user

        current, error = create_user(
            marketplace_app._cache, "sessionadmin", "correct-horse-1", role="admin"
        )
        self.assertIsNone(error)
        self.assertIsNotNone(current)
        other, error = create_user(
            marketplace_app._cache, "backupadmin", "correct-horse-2", role="admin"
        )
        self.assertIsNone(error)
        self.assertIsNotNone(other)

        current_client = marketplace_app.app.test_client()
        login = current_client.post("/api/auth/login", json={
            "username": "sessionadmin", "password": "correct-horse-1",
        }).get_json()
        old_session = marketplace_app.app.test_client()
        self.assertEqual(old_session.post("/api/auth/login", json={
            "username": "sessionadmin", "password": "correct-horse-1",
        }).status_code, 200)

        response = current_client.post(
            f"/api/admin/users/{current['id']}/password",
            headers={
                "Origin": "http://localhost",
                "Sec-Fetch-Site": "same-origin",
                "X-CSRF-Token": login["csrf_token"],
            },
            json={"password": "correct-horse-3"},
        )
        self.assertEqual(response.status_code, 200)
        self.assertTrue(response.get_json()["current_session_preserved"])
        self.assertFalse(response.get_json()["must_change_password"])
        self.assertTrue(current_client.get("/api/auth/session").get_json()["authenticated"])
        self.assertFalse(old_session.get("/api/auth/session").get_json()["authenticated"])
        self.assertEqual(old_session.post("/api/auth/login", json={
            "username": "sessionadmin", "password": "correct-horse-1",
        }).status_code, 401)
        self.assertEqual(old_session.post("/api/auth/login", json={
            "username": "sessionadmin", "password": "correct-horse-3",
        }).status_code, 200)

        require_change = current_client.post(
            f"/api/admin/users/{current['id']}/password-change-required",
        )
        self.assertEqual(require_change.status_code, 409)
        self.assertIn("current session account", require_change.get_json()["error"])

    def test_last_admin_and_current_session_cannot_be_disabled(self):
        from services.auth_service import create_user

        admin, error = create_user(marketplace_app._cache, "dbadmin", "correct-horse-1", role="admin")
        self.assertIsNone(error)
        self.assertIsNotNone(admin)

        last_admin = self.client.post(
            f"/api/admin/users/{admin['id']}/disable",
            headers=self.basic_auth(),
        )
        self.assertEqual(last_admin.status_code, 409)
        self.assertIn("last active administrator", last_admin.get_json()["error"])

        last_admin_role = self.client.put(
            f"/api/admin/users/{admin['id']}/role",
            headers=self.basic_auth(),
            json={"role": "user"},
        )
        self.assertEqual(last_admin_role.status_code, 409)
        self.assertIn("last active administrator", last_admin_role.get_json()["error"])

        second_admin, error = create_user(marketplace_app._cache, "secondadmin", "correct-horse-2", role="admin")
        self.assertIsNone(error)
        self.assertIsNotNone(second_admin)
        self.assertEqual(self.client.post("/api/auth/login", json={
            "username": "secondadmin", "password": "correct-horse-2",
        }).status_code, 200)

        current_account = self.client.post(
            f"/api/admin/users/{second_admin['id']}/disable",
        )
        self.assertEqual(current_account.status_code, 409)
        self.assertIn("current session account", current_account.get_json()["error"])

        current_role = self.client.put(
            f"/api/admin/users/{second_admin['id']}/role",
            json={"role": "user"},
        )
        self.assertEqual(current_role.status_code, 409)
        self.assertIn("current session account", current_role.get_json()["error"])

        current_delete = self.client.delete(
            f"/api/admin/users/{second_admin['id']}",
            json={"username": "secondadmin"},
        )
        self.assertEqual(current_delete.status_code, 409)
        self.assertIn("current session account", current_delete.get_json()["error"])

    def test_configured_admin_remains_authoritative_over_legacy_database_shadow(self):
        from services.auth_service import create_user

        reserved = self.client.post(
            "/api/admin/users",
            headers=self.basic_auth(),
            json={"username": "ADMIN", "password": "another-secret", "role": "admin"},
        )
        self.assertEqual(reserved.status_code, 409)

        config_admin, error = create_user(marketplace_app._cache, "admin", "database-secret", role="admin")
        self.assertIsNone(error)
        self.assertIsNotNone(config_admin)

        listed = self.client.get("/api/admin/users", headers=self.basic_auth()).get_json()
        shadow = next(item for item in listed if item["id"] == config_admin["id"])
        self.assertTrue(shadow["is_config_admin"])

        protected_requests = (
            self.client.put(
                f"/api/admin/users/{config_admin['id']}/profile",
                headers=self.basic_auth(),
                json={"display_name": "changed", "email": "changed@example.com"},
            ),
            self.client.post(
                f"/api/admin/users/{config_admin['id']}/disable",
                headers=self.basic_auth(),
            ),
            self.client.put(
                f"/api/admin/users/{config_admin['id']}/role",
                headers=self.basic_auth(),
                json={"role": "user"},
            ),
            self.client.post(
                f"/api/admin/users/{config_admin['id']}/password",
                headers=self.basic_auth(),
                json={"password": "new-database-secret"},
            ),
            self.client.post(
                f"/api/admin/users/{config_admin['id']}/password-change-required",
                headers=self.basic_auth(),
            ),
            self.client.delete(
                f"/api/admin/users/{config_admin['id']}",
                headers=self.basic_auth(),
                json={"username": "admin"},
            ),
        )
        for response in protected_requests:
            self.assertEqual(response.status_code, 409)
            self.assertIn("服务配置维护", response.get_json()["error"])

        marketplace_app.CONFIG["upload_auth"]["password"] = "updated-config-secret"
        old_config_client = marketplace_app.app.test_client()
        self.assertEqual(old_config_client.post("/api/auth/login", json={
            "username": "admin", "password": "secret",
        }).status_code, 401)

        config_client = marketplace_app.app.test_client()
        config_login = config_client.post("/api/auth/login", json={
            "username": "ADMIN", "password": "updated-config-secret",
        })
        self.assertEqual(config_login.status_code, 200)
        self.assertTrue(config_login.get_json()["is_admin"])
        self.assertEqual(config_login.get_json()["username"], "admin")

        database_client = marketplace_app.app.test_client()
        database_login = database_client.post("/api/auth/login", json={
            "username": "admin", "password": "database-secret",
        })
        self.assertEqual(database_login.status_code, 401)

    def test_docs_status_returns_build_info(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            source = root / "docs"
            dist = source / ".vitepress" / "dist"
            source.mkdir(parents=True)
            dist.mkdir(parents=True)
            (source / "README.md").write_text("# Docs", encoding="utf-8")
            (source / "02-developer-guide").mkdir()
            (source / "02-developer-guide" / "backend.md").write_text("# Backend\n\nAPI and cache.", encoding="utf-8")
            (dist / "index.html").write_text("<html>Docs</html>", encoding="utf-8")
            (dist / "docs-search-index.json").write_text("{}", encoding="utf-8")

            with mock.patch("services.docs_site.docs_source_dir", return_value=source), \
                 mock.patch("services.docs_site.docs_dist_dir", return_value=dist):
                resp = self.client.get("/api/admin/docs/status", headers=self.basic_auth())

        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertEqual(data["entryUrl"], "/scgd_general_wpf/")
        self.assertEqual(data["healthStatus"], "ok")
        self.assertIn("文档", data["healthMessage"])
        self.assertEqual(data["buildCommand"], "npm run docs:build")
        self.assertTrue(data["sourceExists"])
        self.assertTrue(data["built"])
        self.assertEqual(data["sourceDocumentCount"], 2)
        self.assertEqual(data["builtPageCount"], 1)
        self.assertTrue(data["searchIndexExists"])
        self.assertEqual(data["indexedDocumentCount"], 2)
        self.assertIn("开发指南", data["categoryCounts"])
        self.assertTrue(any(item["title"] == "Backend" for item in data["recentDocuments"]))

    def test_docs_index_refresh_endpoint_updates_index_state(self):
        with tempfile.TemporaryDirectory() as td:
            source = Path(td) / "docs"
            source.mkdir(parents=True)
            (source / "README.md").write_text("# Docs Home", encoding="utf-8")
            with mock.patch("services.docs_site.docs_source_dir", return_value=source):
                resp = self.client.post("/api/admin/index/docs/refresh", headers=self.basic_auth())
                status = self.client.get("/api/admin/index/status", headers=self.basic_auth())

        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertEqual(data["status"], "ok")
        self.assertEqual(data["indexed_count"], 1)
        self.assertEqual(status.status_code, 200)
        status_data = status.get_json()
        self.assertIn("docs", status_data["states"])
        self.assertEqual(status_data["counts"]["docs"], 1)

    def test_publish_integrity_reports_missing_and_ready_items(self):
        (self.storage / "LATEST_RELEASE").write_text("4.0.0.1", encoding="utf-8")
        self.create_release("4.0.0.1")
        self.create_update("4.0.0.1")
        (self.storage / "CHANGELOG.md").write_text("## 4.0.0.1\n- release notes", encoding="utf-8")
        self.create_plugin("MissingDocs", "1.0.0")
        complete = self.create_plugin("CompletePlugin", "1.0.0")
        (complete / "README.md").write_text("# Complete", encoding="utf-8")
        (complete / "CHANGELOG.md").write_text("## 1.0.0\n- ready", encoding="utf-8")

        with tempfile.TemporaryDirectory() as td:
            source = Path(td) / "docs"
            dist = source / ".vitepress" / "dist"
            source.mkdir(parents=True)
            dist.mkdir(parents=True)
            (source / "README.md").write_text("# Docs", encoding="utf-8")
            (dist / "index.html").write_text("<html>Docs</html>", encoding="utf-8")
            with mock.patch("services.docs_site.docs_source_dir", return_value=source), \
                 mock.patch("services.docs_site.docs_dist_dir", return_value=dist):
                resp = self.client.get("/api/admin/publish/integrity", headers=self.basic_auth())

        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertEqual(data["app"]["latestVersion"], "4.0.0.1")
        self.assertGreaterEqual(data["app"]["matchedUpdateCount"], 1)
        self.assertTrue(data["app"]["changelogMentionsLatest"])
        self.assertEqual(data["plugins"]["total"], 2)
        self.assertTrue(any(item["pluginId"] == "MissingDocs" for item in data["plugins"]["missingReadme"]))
        self.assertTrue(any(item["pluginId"] == "MissingDocs" for item in data["plugins"]["missingChangelog"]))
        self.assertTrue(any(item["key"] == "docs_site" for item in data["checks"]))

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
        for state in data["states"].values():
            self.assertNotIn("signature", state)

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
        self.assertEqual([job["id"] for job in data], sorted(job["id"] for job in data))
        self.assertIn("latest_run", data[0])
        self.assertEqual(
            sorted(data[0]["run_counts"]),
            ["error", "interrupted", "running", "success", "total"],
        )

    def test_job_runs_history_is_paginated_and_filterable(self):
        from services.scheduler import ensure_default_jobs, run_job_now
        ensure_default_jobs(marketplace_app._cache)
        run_job_now(
            marketplace_app._cache,
            self.storage,
            lambda: marketplace_app.CONFIG,
            marketplace_app.get_db,
            "cache_cleanup",
        )

        response = self.client.get(
            "/api/admin/jobs/cache_cleanup/runs?status=success&limit=1&offset=0",
            headers=self.basic_auth(),
        )
        self.assertEqual(response.status_code, 200)
        page = response.get_json()
        self.assertEqual(page["total"], 1)
        self.assertEqual(page["limit"], 1)
        self.assertEqual(page["offset"], 0)
        self.assertEqual(page["items"][0]["status"], "success")

        invalid = self.client.get(
            "/api/admin/jobs/cache_cleanup/runs?status=unknown",
            headers=self.basic_auth(),
        )
        self.assertEqual(invalid.status_code, 400)
        missing = self.client.get(
            "/api/admin/jobs/missing/runs",
            headers=self.basic_auth(),
        )
        self.assertEqual(missing.status_code, 404)

    def test_jobs_missing_job_contracts_remain_404(self):
        for action in ("run", "enable", "disable"):
            with self.subTest(action=action):
                resp = self.client.post(
                    f"/api/admin/jobs/missing/{action}",
                    headers=self.basic_auth(),
                )
                self.assertEqual(resp.status_code, 404)
                self.assertEqual(resp.get_json(), {"error": "Job not found"})

    def test_job_run_returns_result(self):
        from services.scheduler import ensure_default_jobs
        ensure_default_jobs(marketplace_app._cache)
        resp = self.client.post("/api/admin/jobs/cache_cleanup/run", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("job_id", data)
        self.assertIn("status", data)

    def test_job_run_returns_conflict_when_same_job_is_running(self):
        from services.scheduler import ensure_default_jobs
        ensure_default_jobs(marketplace_app._cache)
        marketplace_app._cache.jobs.start_run(
            "cache_cleanup", "2026-08-12T00:00:00+00:00"
        )

        response = self.client.post(
            "/api/admin/jobs/cache_cleanup/run", headers=self.basic_auth()
        )

        self.assertEqual(response.status_code, 409)
        self.assertEqual(response.get_json()["status"], "skipped")
        self.assertEqual(response.get_json()["error"], "Job is already running")

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
        self.assertIn("audit_activity", resp.get_json())

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

    def test_api_key_scope_catalog_is_complete_and_admin_only(self):
        response = self.client.get(
            "/api/admin/api-keys/scopes",
            headers=self.basic_auth(),
        )
        self.assertEqual(response.status_code, 200)
        data = response.get_json()
        values = [item["value"] for item in data["items"]]
        self.assertEqual(len(values), len(set(values)))
        self.assertIn("ops:relay", values)
        self.assertIn("ops:operator", values)
        self.assertEqual(data["default_scopes"], ["stats:read"])
        for item in data["items"]:
            self.assertTrue(item["label"])
            self.assertTrue(item["description"])
            self.assertIn(item["access"], {"read", "write", "service", "admin"})

        limited = self.create_admin_key("stats:read")
        denied = self.client.get(
            "/api/admin/api-keys/scopes",
            headers={"Authorization": f"Bearer {limited['key']}"},
        )
        self.assertEqual(denied.status_code, 403)

    def test_api_key_description_and_audited_usage_are_independent_and_safe(self):
        created = self.client.post(
            "/api/admin/api-keys",
            headers=self.basic_auth(),
            json={
                "name": "Desktop Relay",
                "description": "Production line heartbeat",
                "scopes": "ops:relay",
            },
        )
        self.assertEqual(created.status_code, 201)
        key = created.get_json()
        self.assertEqual(key["name"], "Desktop Relay")
        self.assertEqual(key["description"], "Production line heartbeat")

        marketplace_app._cache.write_audit(
            actor_type="api_key",
            actor_id=f"key:{key['key_prefix']}",
            action="operations.heartbeat",
            target_type="operations_host",
            target_id="line-1",
            detail='{"status":"online"}',
            ip="192.0.2.1",
            user_agent="secret-client-fingerprint",
        )
        usage = self.client.get(
            f"/api/admin/api-keys/{key['id']}/usage",
            headers=self.basic_auth(),
        )
        self.assertEqual(usage.status_code, 200)
        data = usage.get_json()
        self.assertEqual(data["name"], "Desktop Relay")
        self.assertEqual(data["description"], "Production line heartbeat")
        self.assertEqual(data["audit_activity"]["total"], 1)
        self.assertEqual(data["audit_activity"]["items"][0]["action"], "operations.heartbeat")
        serialized_activity = json.dumps(data["audit_activity"])
        self.assertNotIn("192.0.2.1", serialized_activity)
        self.assertNotIn("secret-client-fingerprint", serialized_activity)

        listed = self.client.get(
            "/api/admin/api-keys",
            headers=self.basic_auth(),
        ).get_json()
        listed_key = next(item for item in listed if item["id"] == key["id"])
        self.assertEqual(listed_key["name"], "Desktop Relay")
        self.assertEqual(listed_key["description"], "Production line heartbeat")

    def test_api_key_invalid_scope_rejected(self):
        resp = self.client.post(
            "/api/admin/api-keys", headers=self.basic_auth(),
            json={"name": "Bad", "scopes": "invalid:scope"},
        )
        self.assertEqual(resp.status_code, 400)

    def test_api_key_invalid_or_past_expiry_rejected(self):
        for expires_at in ("not-a-timestamp", "2000-01-01T00:00:00Z"):
            with self.subTest(expires_at=expires_at):
                resp = self.client.post(
                    "/api/admin/api-keys",
                    headers=self.basic_auth(),
                    json={"name": "Bad Expiry", "expires_at": expires_at},
                )
                self.assertEqual(resp.status_code, 400)
                self.assertIn("expires_at", resp.get_json()["error"])

    def test_api_key_list_reports_effective_status(self):
        resp = self.client.post(
            "/api/admin/api-keys",
            headers=self.basic_auth(),
            json={"name": "Status Key", "expires_at": "2099-01-02T03:04:05+08:00"},
        )
        self.assertEqual(resp.status_code, 201)
        created = resp.get_json()
        self.assertEqual(created["expires_at"], "2099-01-01T19:04:05+00:00")
        self.assertEqual(created["status"], "active")

        listed = self.client.get(
            "/api/admin/api-keys",
            headers=self.basic_auth(),
        ).get_json()
        item = next(key for key in listed if key["id"] == created["id"])
        self.assertEqual(item["status"], "active")

    def test_audit_log_returns_entries(self):
        self.client.post("/api/admin/cache/cleanup", headers=self.basic_auth())
        resp = self.client.get("/api/admin/audit-log", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("entries", data)
        self.assertGreaterEqual(data["total"], len(data["entries"]))

    def test_audit_log_returns_exact_total_and_validates_pagination(self):
        db = marketplace_app._cache.get_db()
        try:
            db.executemany(
                "INSERT INTO audit_log (action, actor_type, actor_id, created_at) VALUES (?, ?, ?, ?)",
                [
                    ("pagination_probe", "system", f"worker-{index}", f"2026-08-12T00:00:{index:02d}+00:00")
                    for index in range(45)
                ],
            )
            db.commit()
        finally:
            db.close()

        response = self.client.get(
            "/api/admin/audit-log?action=pagination_probe&limit=10&offset=20",
            headers=self.basic_auth(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(payload["total"], 45)
        self.assertEqual(len(payload["entries"]), 10)
        self.assertEqual(payload["limit"], 10)
        self.assertEqual(payload["offset"], 20)

        for query in ("limit=0", "limit=501", "limit=invalid", "offset=-1", "offset=invalid"):
            with self.subTest(query=query):
                invalid = self.client.get(
                    f"/api/admin/audit-log?{query}",
                    headers=self.basic_auth(),
                )
                self.assertEqual(invalid.status_code, 400)

    def test_deployment_history_is_admin_only_paginated_and_sanitized(self):
        history = self.storage / "web-deploy-history.jsonl"
        history.write_text(
            json.dumps({
                "timestamp": "2026-08-12T12:00:00+08:00",
                "status": "success",
                "source": "origin",
                "deployed_commit": "c" * 40,
                "server": "PRIVATE-NAS",
                "backup_path": r"D:\ColorVision\web-deploy-backups\20260812-120000",
            }) + "\n",
            encoding="utf-8",
        )

        unauthenticated = self.client.get("/api/admin/deployments")
        self.assertEqual(unauthenticated.status_code, 401)
        response = self.client.get(
            "/api/admin/deployments?limit=10&offset=0&status=success",
            headers=self.basic_auth(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(payload["total"], 1)
        self.assertEqual(payload["entries"][0]["backup_name"], "20260812-120000")
        serialized = json.dumps(payload)
        self.assertNotIn("PRIVATE-NAS", serialized)
        self.assertNotIn("D:\\", serialized)

        invalid = self.client.get(
            "/api/admin/deployments?limit=invalid",
            headers=self.basic_auth(),
        )
        self.assertEqual(invalid.status_code, 400)

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
        self.assertRegex(data["backup_name"], r"^marketplace_backup_\d{8}_\d{6}\.db$")
        self.assertNotIn("backup_path", data)

        listing = self.client.get("/api/admin/backup/db", headers=self.basic_auth())
        self.assertEqual(listing.status_code, 200)
        inventory = listing.get_json()
        self.assertEqual(inventory["count"], 1)
        self.assertEqual(inventory["keep_count"], 10)
        self.assertEqual(inventory["backups"][0]["name"], data["backup_name"])
        self.assertNotIn("path", inventory["backups"][0])

        limited_key = self.create_admin_key("cache:read")
        forbidden = self.client.get(
            "/api/admin/backup/db",
            headers={"Authorization": f"Bearer {limited_key['key']}"},
        )
        self.assertEqual(forbidden.status_code, 403)

    def test_perf_summary_returns_data(self):
        resp = self.client.get("/api/admin/perf/summary", headers=self.basic_auth())
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertRegex(data["generated_at"], r"\+00:00$")
        self.assertRegex(data["process_started_at"], r"\+00:00$")
        self.assertEqual(data["threshold_ms"], 500)
        self.assertGreaterEqual(data["request_buffer_count"], 0)
        self.assertEqual(data["request_buffer_capacity"], 100)
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

import base64
import copy
import io
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

import app as marketplace_app
from update_retention import prune_update_packages, repair_update_storage_layout


class MarketplaceAppTests(unittest.TestCase):
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

    def _create_plugin(self, plugin_id: str = "DemoPlugin", version: str = "1.0.0"):
        plugin_dir = self.storage / "Plugins" / plugin_id
        plugin_dir.mkdir(parents=True, exist_ok=True)
        (plugin_dir / "LATEST_RELEASE").write_text(version, encoding="utf-8")
        (plugin_dir / f"{plugin_id}-{version}.cvxp").write_bytes(b"demo-package")
        return plugin_dir

    def _create_plugin_archive_with_metadata(
        self,
        plugin_id: str,
        version: str,
        *,
        manifest_text: str,
        readme_text: str = "",
        changelog_text: str = "",
    ) -> Path:
        plugin_dir = self.storage / "Plugins" / plugin_id
        plugin_dir.mkdir(parents=True, exist_ok=True)
        (plugin_dir / "LATEST_RELEASE").write_text(version, encoding="utf-8")
        archive_path = plugin_dir / f"{plugin_id}-{version}.cvxp"
        with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED) as archive:
            root = f"{plugin_id}/"
            archive.writestr(root + "manifest.json", manifest_text)
            if readme_text:
                archive.writestr(root + "README.md", readme_text)
            if changelog_text:
                archive.writestr(root + "CHANGELOG.md", changelog_text)
        return archive_path

    def _create_update_package(self, version: str, payload: bytes = b"update") -> Path:
        update_dir = self.storage / "Update"
        update_dir.mkdir(parents=True, exist_ok=True)
        path = update_dir / f"ColorVision-Update-[{version}].cvx"
        path.write_bytes(payload)
        return path

    def test_upload_page_requires_basic_auth(self):
        response = self.client.get("/upload")
        self.assertEqual(response.status_code, 401)
        self.assertIn("Basic", response.headers.get("WWW-Authenticate", ""))

        authed = self.client.get("/upload", headers=self._auth_headers())
        self.assertEqual(authed.status_code, 200)

    def test_api_plugins_invalid_page_returns_json_error(self):
        response = self.client.get("/api/plugins?Page=abc")
        self.assertEqual(response.status_code, 400)
        payload = response.get_json()
        self.assertIsNotNone(payload)
        self.assertEqual(payload["status"], 400)
        self.assertIn("Invalid integer parameter", payload["error"])

    def test_api_health_reports_service_liveness(self):
        response = self.client.get("/api/health")
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(payload["status"], "ok")
        self.assertEqual(payload["service"], "ColorVision Marketplace")

    def test_updates_page_lists_incremental_packages(self):
        self._create_update_package("1.0.0.1")
        self._create_update_package("1.0.0.3")
        (self.storage / "Update" / "notes.txt").write_text("misc", encoding="utf-8")

        response = self.client.get("/updates")
        self.assertEqual(response.status_code, 200)
        html = response.get_data(as_text=True)
        self.assertIn("1.0.0.1", html)
        self.assertIn("1.0.0.3", html)
        self.assertIn("notes.txt", html)

    def test_tools_page_lists_tool_directory_contents(self):
        tool_dir = self.storage / "Tool"
        tool_dir.mkdir(parents=True, exist_ok=True)
        (tool_dir / "SomeInstaller.exe").write_bytes(b"tool")

        response = self.client.get("/tools")
        self.assertEqual(response.status_code, 200)
        self.assertIn("SomeInstaller.exe", response.get_data(as_text=True))

    def test_prune_update_packages_keeps_latest_and_each_dot_one_package(self):
        keep_latest = self._create_update_package("1.1.0.4")
        keep_dot_one_a = self._create_update_package("1.0.0.1")
        self._create_update_package("1.0.0.3")
        keep_dot_one_b = self._create_update_package("1.0.1.1")
        dropped = self._create_update_package("1.0.1.5")

        result = prune_update_packages(self.storage)

        retained_names = {item["filename"] for item in result["retained"]}
        self.assertIn(keep_latest.name, retained_names)
        self.assertIn(keep_dot_one_a.name, retained_names)
        self.assertIn(keep_dot_one_b.name, retained_names)
        self.assertIn(dropped.name, result["deleted"])
        self.assertTrue(keep_latest.exists())
        self.assertFalse(dropped.exists())

    def test_repair_update_storage_layout_moves_misplaced_files(self):
        legacy_dir = self.storage / "ColorVision" / "Update"
        legacy_dir.mkdir(parents=True, exist_ok=True)
        misplaced = legacy_dir / "ColorVision-Update-[1.2.3.4].cvx"
        misplaced.write_bytes(b"update")

        result = repair_update_storage_layout(self.storage)

        repaired = self.storage / "Update" / misplaced.name
        self.assertTrue(repaired.exists())
        self.assertFalse(misplaced.exists())
        self.assertEqual(result[0]["to"], f"Update/{misplaced.name}")

    def test_legacy_update_download_repairs_misplaced_update_file(self):
        legacy_dir = self.storage / "ColorVision" / "Update"
        legacy_dir.mkdir(parents=True, exist_ok=True)
        misplaced = legacy_dir / "ColorVision-Update-[1.2.3.4].cvx"
        misplaced.write_bytes(b"update")

        response = self.client.get(f"/D:/ColorVision/Update/{misplaced.name}")

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.get_data(), b"update")
        response.close()
        self.assertTrue((self.storage / "Update" / misplaced.name).exists())
        self.assertFalse(misplaced.exists())

    def test_download_route_repairs_misplaced_update_file(self):
        legacy_dir = self.storage / "ColorVision" / "Update"
        legacy_dir.mkdir(parents=True, exist_ok=True)
        misplaced = legacy_dir / "ColorVision-Update-[2.3.4.5].cvx"
        misplaced.write_bytes(b"update-download")

        response = self.client.get(f"/download/Update/{misplaced.name}")

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.get_data(), b"update-download")
        response.close()
        self.assertTrue((self.storage / "Update" / misplaced.name).exists())
        self.assertFalse(misplaced.exists())

    def test_plugin_list_endpoints_do_not_compute_package_hashes(self):
        self._create_plugin("FastPlugin", "1.0.0")
        plugin_dir = self.storage / "Plugins" / "FastPlugin"
        (plugin_dir / "manifest.json").write_text(
            '{"id":"FastPlugin","name":"Fast Plugin","description":"quick list"}',
            encoding="utf-8",
        )

        with patch("plugin_marketplace._compute_file_hash", side_effect=AssertionError("hash should not be computed")):
            page_response = self.client.get("/plugins")
            api_response = self.client.get("/api/plugins")

        self.assertEqual(page_response.status_code, 200)
        self.assertEqual(api_response.status_code, 200)

    def test_plugin_detail_endpoint_still_returns_file_hash(self):
        self._create_plugin("HashPlugin", "1.0.0")
        plugin_dir = self.storage / "Plugins" / "HashPlugin"
        (plugin_dir / "manifest.json").write_text(
            '{"id":"HashPlugin","name":"Hash Plugin","description":"detail"}',
            encoding="utf-8",
        )

        response = self.client.get("/api/plugins/HashPlugin")

        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertTrue(payload["versions"])
        self.assertTrue(payload["versions"][0]["fileHash"])

    def test_plugin_summary_can_fallback_to_archive_manifest(self):
        self._create_plugin_archive_with_metadata(
            "ZipManifestPlugin",
            "1.0.0",
            manifest_text='{"Id":"ZipManifestPlugin","Name":"Zip Manifest Plugin","Description":"from archive","Category":"Tools"}',
        )

        response = self.client.get("/api/plugins")

        self.assertEqual(response.status_code, 200)
        items = response.get_json()["items"]
        plugin = next(item for item in items if item["pluginId"] == "ZipManifestPlugin")
        self.assertEqual(plugin["name"], "Zip Manifest Plugin")
        self.assertEqual(plugin["description"], "from archive")
        self.assertEqual(plugin["category"], "Tools")

    def test_plugin_catalog_snapshot_reuses_cached_summary_data(self):
        self._create_plugin_archive_with_metadata(
            "CachedListPlugin",
            "1.0.0",
            manifest_text='{"Id":"CachedListPlugin","Name":"Cached List Plugin","Description":"fast list"}',
        )

        first_response = self.client.get("/api/plugins")
        self.assertEqual(first_response.status_code, 200)

        with patch("plugin_marketplace._get_archive_metadata_for_plugin", side_effect=AssertionError("catalog should come from cache")):
            second_response = self.client.get("/api/plugins")

        self.assertEqual(second_response.status_code, 200)
        items = second_response.get_json()["items"]
        self.assertTrue(any(item["pluginId"] == "CachedListPlugin" for item in items))

    def test_plugin_detail_can_fallback_to_archive_readme_and_changelog(self):
        self._create_plugin_archive_with_metadata(
            "ZipDocPlugin",
            "1.0.0",
            manifest_text='{"id":"ZipDocPlugin","name":"Zip Doc Plugin","description":"archive detail"}',
            readme_text="# Hello from archive",
            changelog_text="## 1.0.0\n- archive changelog",
        )

        response = self.client.get("/api/plugins/ZipDocPlugin")

        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertIn("Hello from archive", payload["readme"])
        self.assertIn("archive changelog", payload["changelog"])

    def test_plugin_detail_returns_richer_remote_metadata(self):
        self._create_plugin_archive_with_metadata(
            "RichPlugin",
            "1.2.3",
            manifest_text=(
                '{"id":"RichPlugin","name":"Rich Plugin","description":"full detail",'
                '"author":"Copilot","category":"Tools","url":"https://example.com/rich",'
                '"requires":"2026.03"}'
            ),
            readme_text="# Rich readme",
            changelog_text="## 1.2.3\n- richer detail",
        )

        plugin_dir = self.storage / "Plugins" / "RichPlugin"
        historical = plugin_dir / "RichPlugin-1.0.0.cvxp"
        historical.write_bytes(b"old-package")

        response = self.client.get("/api/plugins/RichPlugin")

        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(payload["latestVersion"], "1.2.3")
        self.assertEqual(payload["requiresVersion"], "2026.03")
        self.assertEqual(payload["currentPackageCount"], 1)
        self.assertEqual(payload["historicalPackageCount"], 1)
        self.assertEqual(payload["url"], "https://example.com/rich")
        self.assertIn("richer detail", payload["changelog"])
        self.assertEqual(payload["versions"][0]["source"], "current")
        self.assertEqual(payload["archivedVersions"][0]["source"], "archive")

    def test_plugin_query_filter_and_sort_are_consistent_between_html_and_api(self):
        alpha_dir = self._create_plugin("AlphaPlugin", "1.0.0")
        (alpha_dir / "manifest.json").write_text(
            '{"id":"AlphaPlugin","name":"Alpha Plugin","description":"alpha tools","category":"Tools"}',
            encoding="utf-8",
        )

        beta_dir = self._create_plugin("BetaPlugin", "1.0.0")
        (beta_dir / "manifest.json").write_text(
            '{"id":"BetaPlugin","name":"Beta Plugin","description":"beta tools","category":"Tools"}',
            encoding="utf-8",
        )

        gamma_dir = self._create_plugin("GammaExtension", "1.0.0")
        (gamma_dir / "manifest.json").write_text(
            '{"id":"GammaExtension","name":"Gamma Extension","description":"gamma other","category":"Other"}',
            encoding="utf-8",
        )

        page_response = self.client.get("/plugins?q=plugin&category=Tools&sort=name")
        api_response = self.client.get(
            "/api/plugins?Keyword=plugin&Category=Tools&SortBy=name&SortOrder=asc"
        )

        self.assertEqual(page_response.status_code, 200)
        html = page_response.get_data(as_text=True)
        self.assertIn("Alpha Plugin", html)
        self.assertIn("Beta Plugin", html)
        self.assertNotIn("Gamma Extension", html)
        self.assertLess(html.index("Alpha Plugin"), html.index("Beta Plugin"))

        self.assertEqual(api_response.status_code, 200)
        payload = api_response.get_json()
        self.assertEqual([item["pluginId"] for item in payload["items"]], ["AlphaPlugin", "BetaPlugin"])

    def test_api_categories_returns_sorted_unique_values(self):
        first_dir = self._create_plugin("ToolsPlugin", "1.0.0")
        (first_dir / "manifest.json").write_text(
            '{"id":"ToolsPlugin","name":"Tools Plugin","category":"Tools"}',
            encoding="utf-8",
        )

        second_dir = self._create_plugin("ZooPlugin", "1.0.0")
        (second_dir / "manifest.json").write_text(
            '{"id":"ZooPlugin","name":"Zoo Plugin","category":"Zoo"}',
            encoding="utf-8",
        )

        duplicate_dir = self._create_plugin("MoreToolsPlugin", "1.0.0")
        (duplicate_dir / "manifest.json").write_text(
            '{"id":"MoreToolsPlugin","name":"More Tools Plugin","category":"Tools"}',
            encoding="utf-8",
        )

        response = self.client.get("/api/plugins/categories")

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.get_json(), ["Tools", "Zoo"])

    def test_api_download_package_updates_stats_and_plugin_totals(self):
        plugin_dir = self._create_plugin("StatPlugin", "1.0.0")
        (plugin_dir / "manifest.json").write_text(
            '{"id":"StatPlugin","name":"Stat Plugin","description":"track downloads"}',
            encoding="utf-8",
        )

        download_response = self.client.get("/api/packages/StatPlugin/1.0.0")
        self.assertEqual(download_response.status_code, 200)
        download_response.close()

        stats_response = self.client.get("/api/stats")
        self.assertEqual(stats_response.status_code, 200)
        stats_payload = stats_response.get_json()
        self.assertEqual(stats_payload["totalDownloads"], 1)
        self.assertEqual(stats_payload["perPlugin"][0], {"pluginId": "StatPlugin", "count": 1})
        self.assertEqual(stats_payload["recent"][0]["pluginId"], "StatPlugin")

        list_response = self.client.get("/api/plugins")
        self.assertEqual(list_response.status_code, 200)
        items = list_response.get_json()["items"]
        plugin = next(item for item in items if item["pluginId"] == "StatPlugin")
        self.assertEqual(plugin["totalDownloads"], 1)

    def test_archive_metadata_is_cached_across_detail_requests(self):
        self._create_plugin_archive_with_metadata(
            "CachedArchivePlugin",
            "1.0.0",
            manifest_text='{"id":"CachedArchivePlugin","name":"Cached Archive Plugin"}',
            readme_text="# Cached Readme",
        )

        first_response = self.client.get("/api/plugins/CachedArchivePlugin")
        self.assertEqual(first_response.status_code, 200)

        with patch("plugin_marketplace._read_archive_metadata", side_effect=AssertionError("archive should be served from cache")):
            second_response = self.client.get("/api/plugins/CachedArchivePlugin")

        self.assertEqual(second_response.status_code, 200)
        self.assertIn("Cached Readme", second_response.get_json()["readme"])

    def test_publish_package_prewarms_plugin_metadata(self):
        response = self.client.post(
            "/api/packages/publish",
            headers=self._auth_headers(),
            data={
                "PluginId": "WarmPlugin",
                "Version": "1.2.3",
                "Name": "Warm Plugin",
                "Description": "warm metadata",
                "package": (io.BytesIO(b"pkg"), "WarmPlugin-1.2.3.cvxp"),
            },
            content_type="multipart/form-data",
        )

        self.assertEqual(response.status_code, 201)
        cached_summary = marketplace_app._get_cache_entry("plugin_summary:v1:WarmPlugin")
        cached_detail = marketplace_app._get_cache_entry("plugin_detail:v1:WarmPlugin")
        self.assertIsNotNone(cached_summary)
        self.assertIsNotNone(cached_detail)

    def test_api_ready_reports_ready_when_dependencies_are_available(self):
        response = self.client.get("/api/ready")
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertTrue(payload["ready"])
        self.assertEqual(payload["status"], "ready")
        self.assertTrue(payload["checks"]["database"]["ok"])
        self.assertTrue(payload["checks"]["uploadAuth"]["ok"])

    def test_api_ready_reports_degraded_without_upload_auth(self):
        marketplace_app.CONFIG["upload_auth"] = {"username": "", "password": ""}
        response = self.client.get("/api/ready")
        self.assertEqual(response.status_code, 503)
        payload = response.get_json()
        self.assertFalse(payload["ready"])
        self.assertIn("upload authentication", " ".join(payload["issues"]))

    def test_api_plugin_detail_rejects_invalid_plugin_id(self):
        response = self.client.get("/api/plugins/bad!")
        self.assertEqual(response.status_code, 400)
        self.assertEqual(response.get_json()["error"], "Invalid plugin_id")

    def test_api_publish_package_requires_auth(self):
        response = self.client.post(
            "/api/packages/publish",
            data={
                "PluginId": "DemoPlugin",
                "Version": "1.2.3",
                "package": (io.BytesIO(b"pkg"), "DemoPlugin-1.2.3.cvxp"),
            },
            content_type="multipart/form-data",
        )
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.get_json()["error"], "Authentication required")

    def test_legacy_upload_requires_auth(self):
        response = self.client.put(
            "/upload/ColorVision/Plugins/DemoPlugin/DemoPlugin-1.0.0.cvxp",
            data=b"pkg",
            content_type="application/octet-stream",
        )
        self.assertEqual(response.status_code, 401)
        self.assertIn("Basic", response.headers.get("WWW-Authenticate", ""))

    def test_api_publish_package_saves_canonical_package_and_manifest(self):
        response = self.client.post(
            "/api/packages/publish",
            headers=self._auth_headers(),
            data={
                "PluginId": "DemoPlugin",
                "Version": "1.2.3",
                "Name": "Demo Plugin",
                "Description": "Test package",
                "package": (io.BytesIO(b"pkg"), "custom-name.cvxp"),
            },
            content_type="multipart/form-data",
        )
        self.assertEqual(response.status_code, 201)
        self.assertEqual(response.get_json(), {"pluginId": "DemoPlugin", "version": "1.2.3"})

        plugin_dir = self.storage / "Plugins" / "DemoPlugin"
        self.assertTrue((plugin_dir / "DemoPlugin-1.2.3.cvxp").exists())
        self.assertEqual((plugin_dir / "LATEST_RELEASE").read_text(encoding="utf-8"), "1.2.3")
        manifest = marketplace_app._load_manifest(plugin_dir / "manifest.json")
        self.assertEqual(manifest["id"], "DemoPlugin")
        self.assertEqual(manifest["name"], "Demo Plugin")
        self.assertEqual(manifest["version"], "1.2.3")

    def test_api_batch_version_check_rejects_non_list_payload(self):
        response = self.client.post(
            "/api/plugins/batch-version-check",
            json={"PluginIds": "not-a-list"},
        )
        self.assertEqual(response.status_code, 400)
        self.assertEqual(response.get_json()["error"], "PluginIds must be an array")

    def test_api_feedback_rejects_too_many_files(self):
        attachments = []
        for index in range(marketplace_app.MAX_FEEDBACK_FILES + 1):
            attachments.append((io.BytesIO(b"x"), f"file-{index}.txt"))

        data = {
            "message": "hello",
            "attachments": attachments,
        }

        response = self.client.post(
            "/api/feedback",
            data=data,
            content_type="multipart/form-data",
        )
        self.assertEqual(response.status_code, 400)
        self.assertIn("maximum", response.get_json()["error"])

    def test_validate_runtime_config_rejects_default_credentials(self):
        insecure_config = {
            "secret_key": marketplace_app.DEFAULT_SECRET_KEY,
            "upload_auth": copy.deepcopy(marketplace_app.DEFAULT_UPLOAD_AUTH),
        }
        issues = marketplace_app._validate_runtime_config(insecure_config)
        self.assertGreaterEqual(len(issues), 2)
        self.assertTrue(any("secret_key" in issue for issue in issues))
        self.assertTrue(any("upload_auth" in issue for issue in issues))


if __name__ == "__main__":
    unittest.main()




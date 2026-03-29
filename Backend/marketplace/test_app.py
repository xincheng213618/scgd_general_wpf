import base64
import copy
import io
import tempfile
import unittest
from pathlib import Path

import app as marketplace_app


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




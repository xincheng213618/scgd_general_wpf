import base64
import copy
import io
import tempfile
import unittest
from pathlib import Path

import app as marketplace_app


class WebLoginTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.storage = self.root / "storage"
        self.storage.mkdir()

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {"username": "tester", "password": "secret123"}
        marketplace_app.CONFIG["secret_key"] = "test-secret-key"
        marketplace_app.app.secret_key = "test-secret-key"
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.init_db()

        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.secret_key = self.original_config.get("secret_key", "")
        self.tmp.cleanup()

    def test_login_page_renders(self):
        resp = self.client.get("/login")
        self.assertEqual(resp.status_code, 200)
        html = resp.get_data(as_text=True)
        self.assertIn('<div id="root">', html)

    def test_login_with_wrong_credentials_returns_json_error(self):
        resp = self.client.post("/api/auth/login", json={"username": "bad", "password": "bad"})
        self.assertEqual(resp.status_code, 401)
        self.assertEqual(resp.get_json()["error"], "用户名或密码错误")

    def test_login_with_correct_credentials_redirects(self):
        resp = self.client.post(
            "/login",
            data={"username": "tester", "password": "secret123"},
            follow_redirects=False,
        )
        self.assertIn(resp.status_code, [302, 303])

    def test_login_sets_session(self):
        self.client.post("/login", data={"username": "tester", "password": "secret123"})
        resp = self.client.get("/api/auth/session")
        self.assertEqual(resp.status_code, 200)
        self.assertTrue(resp.get_json()["authenticated"])

    def test_logout_clears_session(self):
        self.client.post("/login", data={"username": "tester", "password": "secret123"})
        self.client.get("/logout")
        resp = self.client.get("/api/auth/session")
        self.assertFalse(resp.get_json()["authenticated"])

    def test_login_with_safe_next_redirects(self):
        resp = self.client.post(
            "/login?next=/admin/publish",
            data={"username": "tester", "password": "secret123"},
            follow_redirects=False,
        )
        self.assertIn(resp.status_code, [302, 303])
        self.assertIn("/admin/publish", resp.headers.get("Location", ""))

    def test_login_with_external_next_rejected(self):
        """External URL in next param should be ignored, defaulting to the admin dashboard."""
        resp = self.client.post(
            "/login?next=https://example.com",
            data={"username": "tester", "password": "secret123"},
            follow_redirects=False,
        )
        self.assertIn(resp.status_code, [302, 303])
        location = resp.headers.get("Location", "")
        self.assertNotIn("example.com", location)
        self.assertIn("/admin", location)

    def test_login_get_with_external_next_ignored(self):
        resp = self.client.get("/login?next=https://evil.com")
        html = resp.get_data(as_text=True)
        # The hidden next field should be empty, not the external URL
        self.assertNotIn("evil.com", html)

    def test_admin_page_requires_backend_login(self):
        resp = self.client.get("/admin/", follow_redirects=False)
        self.assertIn(resp.status_code, [302, 303])
        location = resp.headers.get("Location", "")
        self.assertIn("/login", location)
        self.assertIn("next=%2Fadmin%2F", location)

    def test_admin_v2_route_is_removed(self):
        resp = self.client.get("/admin-v2/", follow_redirects=False)
        self.assertEqual(resp.status_code, 404)

    def test_admin_dashboard_serves_react_shell_after_login(self):
        self.client.post("/login", data={"username": "tester", "password": "secret123"})

        resp = self.client.get("/admin/")

        self.assertEqual(resp.status_code, 200)
        html = resp.get_data(as_text=True)
        self.assertIn('<div id="root">', html)


class AdminPublishRouteAuthTests(unittest.TestCase):
    """Test that publish UI lives under the authenticated React admin area."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.storage = self.root / "storage"
        self.storage.mkdir()
        (self.storage / "Plugins").mkdir()

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {"username": "tester", "password": "secret123"}
        marketplace_app.CONFIG["secret_key"] = "test-secret-key"
        marketplace_app.app.secret_key = "test-secret-key"
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.init_db()

        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.secret_key = self.original_config.get("secret_key", "")
        self.tmp.cleanup()

    def test_admin_publish_requires_web_login(self):
        resp = self.client.get("/admin/publish", follow_redirects=False)
        self.assertIn(resp.status_code, [302, 303])
        location = resp.headers.get("Location", "")
        self.assertIn("login", location)
        self.assertIn("next=%2Fadmin%2Fpublish", location)

    def test_admin_publish_accessible_after_login(self):
        self.client.post("/login", data={"username": "tester", "password": "secret123"})
        resp = self.client.get("/admin/publish")
        self.assertEqual(resp.status_code, 200)
        html = resp.get_data(as_text=True)
        self.assertIn('<div id="root">', html)


class CVWSUploadPageTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.storage = self.root / "storage"
        self.storage.mkdir()
        (self.storage / "Tool" / "CVWindowsService").mkdir(parents=True)

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {"username": "tester", "password": "secret123"}
        marketplace_app.CONFIG["secret_key"] = "test-secret-key"
        marketplace_app.app.secret_key = "test-secret-key"
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.init_db()

        self.client = marketplace_app.app.test_client()
        self.client.post("/login", data={"username": "tester", "password": "secret123"})

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.secret_key = self.original_config.get("secret_key", "")
        self.tmp.cleanup()

    def test_publish_api_requires_login(self):
        client = marketplace_app.app.test_client()
        resp = client.post("/api/tool/cvwindowsservice/publish")
        self.assertEqual(resp.status_code, 401)
        self.assertEqual(resp.get_json()["error"], "Authentication required")

    def test_publish_context_api_returns_cvwindowsservice_state(self):
        resp = self.client.get("/api/tool/cvwindowsservice/context")
        self.assertEqual(resp.status_code, 200)
        payload = resp.get_json()
        self.assertEqual(payload["package_count"], 0)
        self.assertIn("tool_dir_display", payload)

    def test_upload_valid_zip_with_official_name(self):
        data = {
            "package": (io.BytesIO(b"PKdata"), "CVWindowsService[1.0.0.0].zip"),
            "version": "",
            "set_latest": "on",
        }
        resp = self.client.post(
            "/api/tool/cvwindowsservice/publish",
            data=data,
            content_type="multipart/form-data",
        )
        self.assertEqual(resp.status_code, 201)
        payload = resp.get_json()
        self.assertIn("上传成功", payload["message"])
        self.assertEqual(payload["result"]["savedFilename"], "CVWindowsService[1.0.0.0].zip")
        self.assertEqual(payload["result"]["version"], "1.0.0.0")
        self.assertEqual(payload["latestVersion"], "1.0.0.0")

        saved = self.storage / "Tool" / "CVWindowsService" / "CVWindowsService[1.0.0.0].zip"
        self.assertTrue(saved.exists())
        latest = self.storage / "Tool" / "CVWindowsService" / "LATEST_RELEASE"
        self.assertEqual(latest.read_text(encoding="utf-8"), "1.0.0.0")

    def test_upload_preserves_official_suffix(self):
        data = {
            "package": (io.BytesIO(b"PKdata"), "CVWindowsService[4.0.6.522]-0522.zip"),
            "version": "",
            "set_latest": "on",
        }
        resp = self.client.post(
            "/api/tool/cvwindowsservice/publish",
            data=data,
            content_type="multipart/form-data",
        )
        self.assertEqual(resp.status_code, 201)
        payload = resp.get_json()
        self.assertEqual(payload["result"]["savedFilename"], "CVWindowsService[4.0.6.522]-0522.zip")
        saved = self.storage / "Tool" / "CVWindowsService" / "CVWindowsService[4.0.6.522]-0522.zip"
        self.assertTrue(saved.exists())

    def test_upload_non_official_name_requires_version(self):
        data = {
            "package": (io.BytesIO(b"PKdata"), "random-service.zip"),
            "version": "",
        }
        resp = self.client.post(
            "/api/tool/cvwindowsservice/publish",
            data=data,
            content_type="multipart/form-data",
        )
        self.assertEqual(resp.status_code, 400)
        self.assertIn("文件名不符合", resp.get_json()["error"])

    def test_upload_non_official_name_with_manual_version(self):
        data = {
            "package": (io.BytesIO(b"PKdata"), "random-service.zip"),
            "version": "1.0.0.0",
            "set_latest": "on",
        }
        resp = self.client.post(
            "/api/tool/cvwindowsservice/publish",
            data=data,
            content_type="multipart/form-data",
        )
        self.assertEqual(resp.status_code, 201)
        self.assertEqual(resp.get_json()["result"]["savedFilename"], "CVWindowsService[1.0.0.0].zip")

    def test_upload_rejects_non_zip(self):
        data = {
            "package": (io.BytesIO(b"data"), "test.exe"),
            "version": "1.0.0.0",
        }
        resp = self.client.post(
            "/api/tool/cvwindowsservice/publish",
            data=data,
            content_type="multipart/form-data",
        )
        self.assertEqual(resp.status_code, 400)
        self.assertIn(".zip", resp.get_json()["error"])

    def test_upload_rejects_invalid_version(self):
        data = {
            "package": (io.BytesIO(b"PKdata"), "test.zip"),
            "version": "bad-version",
        }
        resp = self.client.post(
            "/api/tool/cvwindowsservice/publish",
            data=data,
            content_type="multipart/form-data",
        )
        self.assertEqual(resp.status_code, 400)
        self.assertIn("版本号格式不正确", resp.get_json()["error"])

    def test_upload_duplicate_version_gets_suffix(self):
        data1 = {
            "package": (io.BytesIO(b"PKv1"), "test.zip"),
            "version": "1.0.0.0",
            "set_latest": "on",
        }
        self.client.post(
            "/api/tool/cvwindowsservice/publish",
            data=data1,
            content_type="multipart/form-data",
        )

        data2 = {
            "package": (io.BytesIO(b"PKv2"), "test.zip"),
            "version": "1.0.0.0",
            "set_latest": "on",
        }
        resp = self.client.post(
            "/api/tool/cvwindowsservice/publish",
            data=data2,
            content_type="multipart/form-data",
        )
        self.assertEqual(resp.status_code, 201)
        self.assertEqual(resp.get_json()["result"]["savedFilename"], "CVWindowsService[1.0.0.0]-1.zip")

        self.assertTrue((self.storage / "Tool" / "CVWindowsService" / "CVWindowsService[1.0.0.0].zip").exists())
        self.assertTrue((self.storage / "Tool" / "CVWindowsService" / "CVWindowsService[1.0.0.0]-1.zip").exists())

    def test_upload_without_set_latest_does_not_write_latest(self):
        data = {
            "package": (io.BytesIO(b"PKdata"), "test.zip"),
            "version": "5.0.0.0",
            "set_latest": "false",
        }
        resp = self.client.post(
            "/api/tool/cvwindowsservice/publish",
            data=data,
            content_type="multipart/form-data",
        )
        self.assertEqual(resp.status_code, 201)
        self.assertFalse(resp.get_json()["result"]["isLatest"])
        latest = self.storage / "Tool" / "CVWindowsService" / "LATEST_RELEASE"
        self.assertFalse(latest.exists())

    def test_upload_with_set_latest_writes_latest(self):
        data = {
            "package": (io.BytesIO(b"PKdata"), "test.zip"),
            "version": "3.0.0.0",
            "set_latest": "on",
        }
        resp = self.client.post(
            "/api/tool/cvwindowsservice/publish",
            data=data,
            content_type="multipart/form-data",
        )
        self.assertEqual(resp.status_code, 201)
        latest = self.storage / "Tool" / "CVWindowsService" / "LATEST_RELEASE"
        self.assertTrue(latest.exists())
        self.assertEqual(latest.read_text(encoding="utf-8"), "3.0.0.0")

    def test_upload_without_set_latest_preserves_existing_latest(self):
        latest = self.storage / "Tool" / "CVWindowsService" / "LATEST_RELEASE"
        latest.write_text("5.0.0.0", encoding="utf-8")

        data = {
            "package": (io.BytesIO(b"PKdata"), "test.zip"),
            "version": "1.0.0.0",
            "set_latest": "false",
        }
        resp = self.client.post(
            "/api/tool/cvwindowsservice/publish",
            data=data,
            content_type="multipart/form-data",
        )
        self.assertEqual(resp.status_code, 201)
        self.assertEqual(latest.read_text(encoding="utf-8"), "5.0.0.0")


class CVWSAPICompatTests(unittest.TestCase):
    """Ensure existing CVWindowsService API endpoints still work."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.storage = self.root / "storage"
        cvws_dir = self.storage / "Tool" / "CVWindowsService"
        cvws_dir.mkdir(parents=True)
        (cvws_dir / "CVWindowsService[1.0.0.0].zip").write_bytes(b"PKdata")
        (cvws_dir / "LATEST_RELEASE").write_text("1.0.0.0", encoding="utf-8")

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {"username": "tester", "password": "secret123"}
        marketplace_app.app.secret_key = "test-secret-key"
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.init_db()

        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.secret_key = self.original_config.get("secret_key", "")
        self.tmp.cleanup()

    def test_releases_api_returns_expected_fields(self):
        resp = self.client.get("/api/tool/cvwindowsservice/releases")
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertIn("latestVersion", data)
        self.assertIn("packages", data)
        self.assertEqual(data["latestVersion"], "1.0.0.0")
        pkg = data["packages"][0]
        self.assertIn("fileName", pkg)
        self.assertIn("version", pkg)
        self.assertIn("downloadUrl", pkg)

    def test_latest_version_api(self):
        resp = self.client.get("/api/tool/cvwindowsservice/latest-version")
        self.assertEqual(resp.status_code, 200)
        data = resp.get_json()
        self.assertEqual(data["version"], "1.0.0.0")

    def test_download_api(self):
        resp = self.client.get("/api/tool/cvwindowsservice/download/1.0.0.0")
        self.assertEqual(resp.status_code, 200)

    def test_basic_auth_legacy_upload_still_works(self):
        auth = base64.b64encode(b"tester:secret123").decode()
        resp = self.client.put(
            "/upload/Tool/CVWindowsService/CVWindowsService[2.0.0.0].zip",
            data=b"PKnew",
            headers={"Authorization": f"Basic {auth}"},
        )
        self.assertIn(resp.status_code, [200, 201])
        saved = self.storage / "Tool" / "CVWindowsService" / "CVWindowsService[2.0.0.0].zip"
        self.assertTrue(saved.exists())


if __name__ == "__main__":
    unittest.main()

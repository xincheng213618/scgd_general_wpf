import base64
import copy
import tempfile
import unittest
from pathlib import Path

import app as marketplace_app
from transfer_files import (
    TransferFileError,
    delete_transfer_file,
    list_transfer_files,
    stream_transfer_upload,
    transfer_root,
)


class TransferFileServiceTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_stream_upload_rejects_nested_paths(self):
        with self.assertRaises(TransferFileError) as context:
            stream_transfer_upload(self.root, "nested/demo.bin", stream=b"payload")

        self.assertEqual(context.exception.status_code, 403)

    def test_delete_only_removes_files_in_transfer_root(self):
        target = self.root / "demo.bin"
        target.write_bytes(b"payload")

        deleted = delete_transfer_file(self.root, "demo.bin")

        self.assertEqual(deleted, target)
        self.assertFalse(target.exists())

    def test_list_transfer_files_ignores_incomplete_temp_files(self):
        (self.root / "ready.bin").write_bytes(b"ready")
        (self.root / ".ready.bin.123.uploading").write_bytes(b"partial")

        files = list_transfer_files(self.root)

        self.assertEqual([item.name for item in files], ["ready.bin"])


class TransferRouteTests(unittest.TestCase):
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
        self.original_max_content_length = marketplace_app.app.config.get("MAX_CONTENT_LENGTH")

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["transfer_upload_dir"] = "Transfer"
        marketplace_app.CONFIG["upload_auth"] = {"username": "tester", "password": "secret"}
        marketplace_app.CONFIG["secret_key"] = "test-secret-key"
        marketplace_app.app.secret_key = "test-secret-key"
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
        marketplace_app.app.config["MAX_CONTENT_LENGTH"] = self.original_max_content_length
        self.temp_dir.cleanup()

    def _auth_headers(self, username="tester", password="secret"):
        token = base64.b64encode(f"{username}:{password}".encode("utf-8")).decode("ascii")
        return {"Authorization": f"Basic {token}"}

    def test_transfer_page_route_returns_spa(self):
        response = self.client.get("/transfer", follow_redirects=False)

        self.assertEqual(response.status_code, 200)
        self.assertIn("text/html", response.content_type)

    def test_admin_files_route_requires_login(self):
        response = self.client.get("/admin/files", follow_redirects=False)
        self.assertIn(response.status_code, (302, 303))
        self.assertIn("login", response.headers.get("Location", ""))

    def test_transfer_api_requires_authentication(self):
        response = self.client.put(
            "/api/transfer/files/demo.bin",
            data=b"payload",
            content_type="application/octet-stream",
        )

        self.assertEqual(response.status_code, 401)

    def test_registered_user_can_use_transfer_but_not_admin(self):
        register_response = self.client.post(
            "/api/auth/register",
            json={"username": "alice", "password": "secret1"},
        )
        self.assertEqual(register_response.status_code, 201)
        self.assertFalse(register_response.get_json()["is_admin"])

        admin_response = self.client.get("/api/admin/cache/status")
        self.assertEqual(admin_response.status_code, 401)

        upload_response = self.client.put(
            "/api/transfer/files/user.bin",
            data=b"payload",
            content_type="application/octet-stream",
        )
        self.assertEqual(upload_response.status_code, 201)
        self.assertEqual((self.storage / "Transfer" / "user.bin").read_bytes(), b"payload")

    def test_transfer_upload_download_list_and_delete_with_basic_auth(self):
        response = self.client.put(
            "/api/transfer/files/demo.bin",
            headers=self._auth_headers(),
            data=b"payload",
            content_type="application/octet-stream",
        )

        self.assertEqual(response.status_code, 201)
        target = self.storage / "Transfer" / "demo.bin"
        self.assertEqual(target.read_bytes(), b"payload")

        list_response = self.client.get("/api/transfer/files", headers=self._auth_headers())
        self.assertEqual(list_response.status_code, 200)
        self.assertEqual(list_response.get_json()["files"][0]["name"], "demo.bin")

        download_response = self.client.get("/api/transfer/files/demo.bin", headers=self._auth_headers())
        self.assertEqual(download_response.status_code, 200)
        self.assertEqual(download_response.get_data(), b"payload")
        download_response.close()

        delete_response = self.client.delete("/api/transfer/files/demo.bin", headers=self._auth_headers())
        self.assertEqual(delete_response.status_code, 200)
        self.assertFalse(target.exists())

    def test_transfer_upload_ignores_global_content_length_limit(self):
        marketplace_app.app.config["MAX_CONTENT_LENGTH"] = 1

        response = self.client.put(
            "/api/transfer/files/large.bin",
            headers=self._auth_headers(),
            data=b"larger-than-one-byte",
            content_type="application/octet-stream",
        )

        self.assertEqual(response.status_code, 201)
        self.assertEqual((self.storage / "Transfer" / "large.bin").read_bytes(), b"larger-than-one-byte")

    def test_transfer_api_rejects_subdirectories(self):
        response = self.client.put(
            "/api/transfer/files/nested/demo.bin",
            headers=self._auth_headers(),
            data=b"payload",
            content_type="application/octet-stream",
        )

        self.assertEqual(response.status_code, 403)
        self.assertFalse((self.storage / "Transfer" / "nested" / "demo.bin").exists())

    def test_storage_download_for_transfer_folder_requires_auth(self):
        transfer_dir = transfer_root(self.storage, marketplace_app.CONFIG)
        transfer_dir.mkdir(parents=True, exist_ok=True)
        (transfer_dir / "demo.bin").write_bytes(b"payload")

        response = self.client.get("/download/Transfer/demo.bin")
        self.assertEqual(response.status_code, 401)

        authed = self.client.get("/download/Transfer/demo.bin", headers=self._auth_headers())
        self.assertEqual(authed.status_code, 200)
        self.assertEqual(authed.get_data(), b"payload")
        authed.close()

    def test_file_transfer_api_key_scope_is_accepted(self):
        key_response = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Transfer Key", "scopes": "file:transfer"},
        )
        self.assertEqual(key_response.status_code, 201)
        api_key = key_response.get_json()["key"]

        response = self.client.put(
            "/api/transfer/files/key.bin",
            headers={"Authorization": f"Bearer {api_key}"},
            data=b"payload",
            content_type="application/octet-stream",
        )

        self.assertEqual(response.status_code, 201)
        self.assertEqual((self.storage / "Transfer" / "key.bin").read_bytes(), b"payload")


if __name__ == "__main__":
    unittest.main()

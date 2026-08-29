import base64
import copy
import http.client
import tempfile
import threading
import unittest
from datetime import datetime
from io import BytesIO
from pathlib import Path
from unittest.mock import patch

import app as marketplace_app
from werkzeug.serving import make_server
from transfer_files import (
    ANONYMOUS_TRANSFER_OWNER_TYPE,
    ANONYMOUS_TRANSFER_FILE_TTL_SECONDS,
    TransferFileError,
    append_transfer_upload,
    cleanup_expired_transfer_files,
    create_or_resume_transfer_upload,
    delete_transfer_file,
    get_transfer_upload_session,
    get_transfer_share,
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

    def test_resumable_upload_continues_from_persisted_offset(self):
        fingerprint = "a" * 64
        session = create_or_resume_transfer_upload(
            self.root,
            "resume.bin",
            10,
            fingerprint,
            owner_type="user",
            owner_id="alice",
        )

        first = append_transfer_upload(
            self.root,
            session.upload_id,
            0,
            BytesIO(b"abcd"),
            owner_type="user",
            owner_id="alice",
        )
        resumed = create_or_resume_transfer_upload(
            self.root,
            "resume.bin",
            10,
            fingerprint,
            owner_type="user",
            owner_id="alice",
        )
        completed = append_transfer_upload(
            self.root,
            resumed.upload_id,
            resumed.offset,
            BytesIO(b"efghij"),
            owner_type="user",
            owner_id="alice",
        )

        self.assertEqual(first.session.offset, 4)
        self.assertEqual(resumed.upload_id, session.upload_id)
        self.assertEqual(resumed.offset, 4)
        self.assertTrue(completed.session.complete)
        self.assertTrue(completed.session.share_url.startswith("/transfer/share/"))
        self.assertEqual(completed.session.expires_at, 0)
        self.assertEqual((self.root / "resume.bin").read_bytes(), b"abcdefghij")

    def test_resumable_upload_rejects_wrong_offset_without_losing_progress(self):
        session = create_or_resume_transfer_upload(
            self.root,
            "offset.bin",
            8,
            "b" * 64,
            owner_type="user",
            owner_id="alice",
        )
        append_transfer_upload(
            self.root,
            session.upload_id,
            0,
            BytesIO(b"abcd"),
            owner_type="user",
            owner_id="alice",
        )

        with self.assertRaises(TransferFileError) as context:
            append_transfer_upload(
                self.root,
                session.upload_id,
                0,
                BytesIO(b"efgh"),
                owner_type="user",
                owner_id="alice",
            )

        status = get_transfer_upload_session(
            self.root,
            session.upload_id,
            owner_type="user",
            owner_id="alice",
        )
        self.assertEqual(context.exception.status_code, 409)
        self.assertEqual(status.offset, 4)

    def test_resumable_upload_session_is_private_to_its_owner(self):
        session = create_or_resume_transfer_upload(
            self.root,
            "private.bin",
            4,
            "c" * 64,
            owner_type="user",
            owner_id="alice",
        )

        with self.assertRaises(TransferFileError) as context:
            get_transfer_upload_session(
                self.root,
                session.upload_id,
                owner_type="user",
                owner_id="bob",
            )

        self.assertEqual(context.exception.status_code, 404)

    def test_interrupted_chunk_keeps_last_confirmed_offset_and_can_retry(self):
        class InterruptedStream:
            def __init__(self):
                self.read_count = 0

            def read(self, _size):
                self.read_count += 1
                if self.read_count == 1:
                    return b"partial"
                raise ConnectionError("client disconnected")

        session = create_or_resume_transfer_upload(
            self.root,
            "interrupted.bin",
            8,
            "e" * 64,
            owner_type="user",
            owner_id="alice",
        )

        with self.assertRaises(TransferFileError) as interrupted:
            append_transfer_upload(
                self.root,
                session.upload_id,
                0,
                InterruptedStream(),
                owner_type="user",
                owner_id="alice",
            )

        status = get_transfer_upload_session(
            self.root,
            session.upload_id,
            owner_type="user",
            owner_id="alice",
        )
        completed = append_transfer_upload(
            self.root,
            session.upload_id,
            status.offset,
            BytesIO(b"complete"),
            owner_type="user",
            owner_id="alice",
        )
        self.assertEqual(interrupted.exception.status_code, 500)
        self.assertEqual(status.offset, 0)
        self.assertTrue(completed.session.complete)
        self.assertEqual((self.root / "interrupted.bin").read_bytes(), b"complete")

    def test_anonymous_upload_does_not_replace_file_created_during_upload(self):
        session = create_or_resume_transfer_upload(
            self.root,
            "race.bin",
            4,
            "f" * 64,
            owner_type=ANONYMOUS_TRANSFER_OWNER_TYPE,
            owner_id="11111111-1111-4111-8111-111111111111",
        )
        target = self.root / "race.bin"
        target.write_bytes(b"original")

        with self.assertRaises(TransferFileError) as context:
            append_transfer_upload(
                self.root,
                session.upload_id,
                0,
                BytesIO(b"data"),
                owner_type=ANONYMOUS_TRANSFER_OWNER_TYPE,
                owner_id="11111111-1111-4111-8111-111111111111",
            )

        self.assertEqual(context.exception.status_code, 409)
        self.assertEqual(target.read_bytes(), b"original")

    def test_expired_anonymous_file_and_share_are_deleted(self):
        session = create_or_resume_transfer_upload(
            self.root,
            "temporary.bin",
            4,
            "0" * 64,
            owner_type=ANONYMOUS_TRANSFER_OWNER_TYPE,
            owner_id="11111111-1111-4111-8111-111111111111",
        )
        completed = append_transfer_upload(
            self.root,
            session.upload_id,
            0,
            BytesIO(b"data"),
            owner_type=ANONYMOUS_TRANSFER_OWNER_TYPE,
            owner_id="11111111-1111-4111-8111-111111111111",
        ).session
        share = get_transfer_share(self.root, completed.share_token)
        list_transfer_files(self.root)
        share_after_list = get_transfer_share(self.root, completed.share_token)

        deleted = cleanup_expired_transfer_files(self.root, now=completed.expires_at + 1)

        self.assertEqual(deleted, 1)
        self.assertTrue(share.temporary)
        self.assertTrue(share_after_list.temporary)
        self.assertEqual(share_after_list.expires_at, share.expires_at)
        self.assertAlmostEqual(
            completed.expires_at - completed.updated_at,
            ANONYMOUS_TRANSFER_FILE_TTL_SECONDS,
            delta=1,
        )
        self.assertFalse((self.root / "temporary.bin").exists())
        with self.assertRaises(TransferFileError) as context:
            get_transfer_share(self.root, completed.share_token)
        self.assertEqual(context.exception.status_code, 404)

    def test_hourly_scheduler_job_deletes_expired_anonymous_files(self):
        from services.scheduler import DEFAULT_JOBS, _run_transfer_file_cleanup

        session = create_or_resume_transfer_upload(
            self.root,
            "scheduled.bin",
            4,
            "1" * 64,
            owner_type=ANONYMOUS_TRANSFER_OWNER_TYPE,
            owner_id="11111111-1111-4111-8111-111111111111",
        )
        completed = append_transfer_upload(
            self.root,
            session.upload_id,
            0,
            BytesIO(b"data"),
            owner_type=ANONYMOUS_TRANSFER_OWNER_TYPE,
            owner_id="11111111-1111-4111-8111-111111111111",
        ).session

        with patch("transfer_files.time.time", return_value=completed.expires_at + 1):
            summary = _run_transfer_file_cleanup(
                self.root,
                lambda: {"transfer_upload_dir": str(self.root)},
            )

        job = next(item for item in DEFAULT_JOBS if item["id"] == "transfer_file_cleanup")
        self.assertEqual(job["interval_seconds"], 3600)
        self.assertIn("Deleted 1", summary)
        self.assertFalse((self.root / "scheduled.bin").exists())


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
        marketplace_app.CONFIG["anonymous_transfer_upload_enabled"] = False
        marketplace_app.CONFIG["anonymous_transfer_max_bytes"] = 8
        marketplace_app.CONFIG["public_registration_enabled"] = True
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

    def _anonymous_upload_headers(self, client_id="11111111-1111-4111-8111-111111111111"):
        return {
            "Origin": "http://localhost",
            "Sec-Fetch-Site": "same-origin",
            "X-ColorVision-Web": "1",
            "X-Transfer-Client": client_id,
        }

    def test_transfer_page_route_returns_spa(self):
        with self.client.get("/transfer", follow_redirects=False) as response:
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
        self.assertIn("Basic", response.headers.get("WWW-Authenticate", ""))

    def test_anonymous_resumable_upload_is_disabled_by_default(self):
        response = self.client.post(
            "/api/transfer/uploads",
            headers=self._anonymous_upload_headers(),
            json={
                "filename": "guest.bin",
                "total_size": 4,
                "fingerprint": "a" * 64,
            },
        )

        self.assertEqual(response.status_code, 401)

    def test_anonymous_user_can_resume_upload_but_cannot_browse_or_manage_files(self):
        marketplace_app.CONFIG["anonymous_transfer_upload_enabled"] = True
        headers = self._anonymous_upload_headers()
        session_payload = self.client.get("/api/auth/session").get_json()
        create_body = {
            "filename": "guest.bin",
            "total_size": 8,
            "fingerprint": "a" * 64,
        }

        created = self.client.post("/api/transfer/uploads", headers=headers, json=create_body)
        upload_id = created.get_json()["upload_id"]
        first_chunk = self.client.patch(
            f"/api/transfer/uploads/{upload_id}",
            headers={**headers, "Upload-Offset": "0"},
            data=b"half",
            content_type="application/offset+octet-stream",
        )
        resumed = self.client.post("/api/transfer/uploads", headers=headers, json=create_body)
        other_client = self.client.get(
            f"/api/transfer/uploads/{upload_id}",
            headers=self._anonymous_upload_headers("22222222-2222-4222-8222-222222222222"),
        )
        completed = self.client.patch(
            f"/api/transfer/uploads/{upload_id}",
            headers={**headers, "Upload-Offset": "4"},
            data=b"done",
            content_type="application/offset+octet-stream",
        )

        self.assertFalse(session_payload["authenticated"])
        self.assertTrue(session_payload["anonymous_transfer_upload_enabled"])
        self.assertEqual(session_payload["anonymous_transfer_max_bytes"], 8)
        self.assertEqual(created.status_code, 201)
        self.assertEqual(first_chunk.get_json()["offset"], 4)
        self.assertEqual(resumed.get_json()["upload_id"], upload_id)
        self.assertEqual(resumed.get_json()["offset"], 4)
        self.assertEqual(other_client.status_code, 404)
        self.assertTrue(completed.get_json()["complete"])
        self.assertTrue(completed.get_json()["temporary"])
        self.assertTrue(completed.get_json()["share_url"].startswith("/transfer/share/"))
        self.assertIsNotNone(completed.get_json()["expires_at"])
        self.assertEqual((self.storage / "Transfer" / "guest.bin").read_bytes(), b"halfdone")

        share_token = completed.get_json()["share_url"].rsplit("/", 1)[-1]
        share_page = self.client.get(f"/api/transfer/shares/{share_token}")
        shared_download = self.client.get(f"/api/transfer/shares/{share_token}/download")
        self.assertEqual(share_page.status_code, 200)
        self.assertEqual(share_page.get_json()["name"], "guest.bin")
        self.assertTrue(share_page.get_json()["temporary"])
        self.assertEqual(shared_download.status_code, 200)
        self.assertEqual(shared_download.get_data(), b"halfdone")
        shared_download.close()

        self.assertEqual(self.client.get("/api/transfer/files", headers=headers).status_code, 401)
        self.assertEqual(self.client.get("/api/transfer/files/guest.bin", headers=headers).status_code, 401)
        self.assertEqual(self.client.delete("/api/transfer/files/guest.bin", headers=headers).status_code, 401)
        self.assertTrue((self.storage / "Transfer" / "guest.bin").is_file())

    def test_expired_anonymous_share_returns_gone_and_removes_file(self):
        marketplace_app.CONFIG["anonymous_transfer_upload_enabled"] = True
        headers = self._anonymous_upload_headers()
        created = self.client.post(
            "/api/transfer/uploads",
            headers=headers,
            json={"filename": "expires.bin", "total_size": 4, "fingerprint": "9" * 64},
        ).get_json()
        completed = self.client.patch(
            f"/api/transfer/uploads/{created['upload_id']}",
            headers={**headers, "Upload-Offset": "0"},
            data=b"data",
            content_type="application/offset+octet-stream",
        ).get_json()
        share_token = completed["share_url"].rsplit("/", 1)[-1]
        expires_at = datetime.fromisoformat(completed["expires_at"]).timestamp()

        with patch("transfer_files.time.time", return_value=expires_at + 1):
            response = self.client.get(f"/api/transfer/shares/{share_token}")

        self.assertEqual(response.status_code, 410)
        self.assertFalse((self.storage / "Transfer" / "expires.bin").exists())

    def test_anonymous_upload_cannot_overwrite_existing_file(self):
        marketplace_app.CONFIG["anonymous_transfer_upload_enabled"] = True
        target = transfer_root(self.storage, marketplace_app.CONFIG) / "existing.bin"
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(b"original")

        response = self.client.post(
            "/api/transfer/uploads",
            headers=self._anonymous_upload_headers(),
            json={
                "filename": "existing.bin",
                "total_size": 4,
                "fingerprint": "b" * 64,
            },
        )

        self.assertEqual(response.status_code, 409)
        self.assertEqual(target.read_bytes(), b"original")

    def test_anonymous_upload_honors_configured_file_size_limit(self):
        marketplace_app.CONFIG["anonymous_transfer_upload_enabled"] = True
        marketplace_app.CONFIG["anonymous_transfer_max_bytes"] = 4

        response = self.client.post(
            "/api/transfer/uploads",
            headers=self._anonymous_upload_headers(),
            json={
                "filename": "too-large.bin",
                "total_size": 5,
                "fingerprint": "c" * 64,
            },
        )

        self.assertEqual(response.status_code, 413)

    def test_browser_transfer_auth_avoids_basic_challenge_and_redirects_navigation(self):
        fetch_headers = {
            "X-ColorVision-Web": "1",
        }

        transfer_api = self.client.get("/api/transfer/files", headers=fetch_headers)
        browse_api = self.client.get("/api/site/browse/Transfer", headers=fetch_headers)
        download = self.client.get(
            "/download/Transfer/missing.bin",
            headers={"Accept": "text/html,application/xhtml+xml"},
            follow_redirects=False,
        )

        self.assertEqual(transfer_api.status_code, 401)
        self.assertNotIn("WWW-Authenticate", transfer_api.headers)
        self.assertEqual(browse_api.status_code, 401)
        self.assertNotIn("WWW-Authenticate", browse_api.headers)
        self.assertIn(download.status_code, (302, 303))
        self.assertIn("/login?", download.headers["Location"])
        self.assertIn("next=%2Fdownload%2FTransfer%2Fmissing.bin", download.headers["Location"])

    def test_registered_user_can_use_transfer_and_default_admin_permissions(self):
        register_response = self.client.post(
            "/api/auth/register",
            json={"username": "alice", "password": "correct-horse-1"},
        )
        self.assertEqual(register_response.status_code, 201)
        self.assertFalse(register_response.get_json()["is_admin"])
        self.assertTrue(register_response.get_json()["can_access_admin"])

        admin_response = self.client.get("/api/admin/cache/status")
        self.assertEqual(admin_response.status_code, 200)

        upload_response = self.client.put(
            "/api/transfer/files/user.bin",
            data=b"payload",
            content_type="application/octet-stream",
        )
        self.assertEqual(upload_response.status_code, 201)
        self.assertEqual((self.storage / "Transfer" / "user.bin").read_bytes(), b"payload")

    def test_browser_session_transfer_write_requires_csrf_token(self):
        register_response = self.client.post(
            "/api/auth/register",
            json={"username": "browser-user", "password": "correct-horse-1"},
        )
        token = register_response.get_json()["csrf_token"]
        browser_headers = {
            "Origin": "http://localhost",
            "Sec-Fetch-Site": "same-origin",
            "Content-Type": "application/octet-stream",
        }

        rejected = self.client.put(
            "/api/transfer/files/browser.bin",
            headers=browser_headers,
            data=b"payload",
        )
        target = self.storage / "Transfer" / "browser.bin"
        self.assertEqual(rejected.status_code, 403)
        self.assertFalse(target.exists())

        accepted = self.client.put(
            "/api/transfer/files/browser.bin",
            headers={**browser_headers, "X-CSRF-Token": token},
            data=b"payload",
        )
        self.assertEqual(accepted.status_code, 201)
        self.assertEqual(target.read_bytes(), b"payload")

    def test_browser_session_resumable_upload_requires_csrf_token(self):
        register_response = self.client.post(
            "/api/auth/register",
            json={"username": "resume-browser", "password": "correct-horse-1"},
        )
        token = register_response.get_json()["csrf_token"]
        browser_headers = {
            "Origin": "http://localhost",
            "Sec-Fetch-Site": "same-origin",
        }
        create_body = {
            "filename": "browser-resume.bin",
            "total_size": 4,
            "fingerprint": "f" * 64,
        }

        rejected_create = self.client.post(
            "/api/transfer/uploads",
            headers=browser_headers,
            json=create_body,
        )
        accepted_create = self.client.post(
            "/api/transfer/uploads",
            headers={**browser_headers, "X-CSRF-Token": token},
            json=create_body,
        )
        upload_id = accepted_create.get_json()["upload_id"]
        chunk_headers = {
            **browser_headers,
            "Upload-Offset": "0",
            "Content-Type": "application/offset+octet-stream",
        }
        rejected_chunk = self.client.patch(
            f"/api/transfer/uploads/{upload_id}",
            headers=chunk_headers,
            data=b"data",
        )
        accepted_chunk = self.client.patch(
            f"/api/transfer/uploads/{upload_id}",
            headers={**chunk_headers, "X-CSRF-Token": token},
            data=b"data",
        )

        self.assertEqual(rejected_create.status_code, 403)
        self.assertEqual(accepted_create.status_code, 201)
        self.assertEqual(rejected_chunk.status_code, 403)
        self.assertEqual(accepted_chunk.status_code, 200)
        self.assertTrue(accepted_chunk.get_json()["complete"])

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
        self.assertEqual(download_response.headers["Accept-Ranges"], "bytes")
        self.assertEqual(download_response.headers["X-Content-Type-Options"], "nosniff")
        download_response.close()

        delete_response = self.client.delete("/api/transfer/files/demo.bin", headers=self._auth_headers())
        self.assertEqual(delete_response.status_code, 200)
        self.assertFalse(target.exists())

    def test_resumable_transfer_api_persists_offset_and_completes(self):
        create_response = self.client.post(
            "/api/transfer/uploads",
            headers=self._auth_headers(),
            json={
                "filename": "resumable.bin",
                "total_size": 10,
                "fingerprint": "d" * 64,
            },
        )
        self.assertEqual(create_response.status_code, 201)
        upload_id = create_response.get_json()["upload_id"]

        first_chunk = self.client.patch(
            f"/api/transfer/uploads/{upload_id}",
            headers={**self._auth_headers(), "Upload-Offset": "0"},
            data=b"abcd",
            content_type="application/offset+octet-stream",
        )
        self.assertEqual(first_chunk.status_code, 200)
        self.assertEqual(first_chunk.get_json()["offset"], 4)
        self.assertFalse(first_chunk.get_json()["complete"])

        status = self.client.get(
            f"/api/transfer/uploads/{upload_id}",
            headers=self._auth_headers(),
        )
        self.assertEqual(status.status_code, 200)
        self.assertEqual(status.get_json()["offset"], 4)

        resumed_create = self.client.post(
            "/api/transfer/uploads",
            headers=self._auth_headers(),
            json={
                "filename": "resumable.bin",
                "total_size": 10,
                "fingerprint": "d" * 64,
            },
        )
        self.assertEqual(resumed_create.status_code, 200)
        self.assertEqual(resumed_create.get_json()["upload_id"], upload_id)
        self.assertEqual(resumed_create.get_json()["offset"], 4)

        final_chunk = self.client.patch(
            f"/api/transfer/uploads/{upload_id}",
            headers={**self._auth_headers(), "Upload-Offset": "4"},
            data=b"efghij",
            content_type="application/offset+octet-stream",
        )
        self.assertEqual(final_chunk.status_code, 200)
        self.assertTrue(final_chunk.get_json()["complete"])
        self.assertEqual(final_chunk.get_json()["offset"], 10)
        self.assertEqual((self.storage / "Transfer" / "resumable.bin").read_bytes(), b"abcdefghij")

    def test_transfer_file_head_does_not_delete_the_file(self):
        target = transfer_root(self.storage, marketplace_app.CONFIG) / "head-check.bin"
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(b"payload")

        with self.client.head(
            "/api/transfer/files/head-check.bin",
            headers=self._auth_headers(),
        ) as response:
            self.assertEqual(response.status_code, 200)
            self.assertEqual(response.get_data(), b"")
            self.assertEqual(response.headers["Content-Length"], "7")

        self.assertEqual(target.read_bytes(), b"payload")

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

    def test_transfer_upload_completes_without_waiting_for_client_disconnect(self):
        server = make_server("127.0.0.1", 0, marketplace_app.app, threaded=True)
        server_thread = threading.Thread(target=server.serve_forever, daemon=True)
        server_thread.start()
        connection = http.client.HTTPConnection("127.0.0.1", server.server_port, timeout=2)
        payload = b"x" * 4097

        try:
            connection.request(
                "PUT",
                "/api/transfer/files/keep-alive.bin",
                body=payload,
                headers={
                    **self._auth_headers(),
                    "Content-Type": "application/octet-stream",
                },
            )
            response = connection.getresponse()
            response.read()

            self.assertEqual(response.status, 201)
            self.assertEqual((self.storage / "Transfer" / "keep-alive.bin").read_bytes(), payload)
        finally:
            connection.close()
            server.shutdown()
            server_thread.join(timeout=3)

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

from __future__ import annotations

import copy
import io
import tempfile
import unittest
from unittest import mock
from pathlib import Path

from app_setup import RuntimeOverrides, create_app_and_context
from config_loader import DEFAULT_CONFIG
from routes.public_api import register_public_api
from routes.public_pages import PublicPageContext, register_public_pages
from routes.admin_api import AdminApiContext, register_admin_api_routes
from services.api_key_service import create_api_key, revoke_api_key
from services.auth_service import create_user
from services.csrf_protection import register_csrf_protection
from services.session_service import revoke_all_user_sessions


class FeedbackRouteTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.root = Path(self._temp.name)
        self.storage = self.root / "storage"
        self.storage.mkdir()
        self.config = copy.deepcopy(DEFAULT_CONFIG)
        self.config.update({
            "storage_path": str(self.storage),
            "secret_key": "isolated-feedback-test-secret",
            "upload_auth": {"username": "config-admin", "password": "test-secret"},
        })
        app, self.ctx, _, helpers = create_app_and_context(RuntimeOverrides(
            config=lambda: self.config,
            storage=lambda: self.storage,
            db_path=lambda: self.root / "feedback-tests.db",
        ))
        app.config["TESTING"] = True
        register_public_pages(app, PublicPageContext(
            cache=helpers["cache"],
            storage=self.storage,
            config_getter=lambda: self.config,
            get_upload_auth=helpers["get_upload_auth"],
            check_web_session_auth=helpers["check_web_session_auth"],
            dist_dir=self.root / "dist",
        ))
        register_public_api(app, self.ctx)
        register_admin_api_routes(app, AdminApiContext(
            cache=helpers["cache"], jobs=helpers["cache"].jobs,
            storage_getter=lambda: self.storage, config_getter=lambda: self.config,
            config_path_getter=lambda: self.root / "config.json", get_db=helpers["get_db"],
            auth_policy=helpers["auth_policy"], request_context_factory=helpers["request_context_factory"],
            operations_admin=mock.Mock(), refresh_plugin_index=mock.Mock(),
            refresh_all_plugin_index=mock.Mock(), get_plugin_index_state=mock.Mock(),
            is_plugin_index_populated=mock.Mock(), get_plugin_catalog_from_index=mock.Mock(),
            human_size=str,
        ))
        register_csrf_protection(app)
        self.app = app
        self.cache = helpers["cache"]
        self.recorder = helpers["access_recorder"]

        self.alice, error = create_user(
            self.cache,
            "alice-user",
            "alice password phrase 123",
            role="user",
            account_origin="administrator_created",
        )
        self.assertIsNone(error)
        self.bob, error = create_user(
            self.cache,
            "bob-user",
            "bob password phrase 456",
            role="user",
            account_origin="administrator_created",
        )
        self.assertIsNone(error)
        self.developer, error = create_user(
            self.cache,
            "sdk-developer",
            "developer password 789",
            role="developer",
            account_origin="administrator_created",
        )
        self.assertIsNone(error)

    def tearDown(self):
        self.recorder.close()
        self._temp.cleanup()

    @staticmethod
    def _login(client, username: str, password: str):
        response = client.post("/api/auth/login", json={"username": username, "password": password})
        assert response.status_code == 200, response.get_data(as_text=True)
        return response.get_json()

    @staticmethod
    def _submit(client, *, machine: str, message: str):
        return client.post(
            "/api/feedback",
            data={
                "message": message,
                "userName": "untrusted-local-name",
                "machineName": machine,
                "machineInfo": f"{machine} / Windows",
                "files": (io.BytesIO(b"diagnostics"), "diagnostics.zip"),
            },
            content_type="multipart/form-data",
        )

    def test_two_accounts_are_isolated_and_direct_attachment_urls_do_not_bypass_scope(self):
        alice_client = self.app.test_client()
        bob_client = self.app.test_client()
        self._login(alice_client, "alice-user", "alice password phrase 123")
        self._login(bob_client, "bob-user", "bob password phrase 456")
        alice_id = self._submit(alice_client, machine="PC-ALICE", message="alice issue").get_json()["feedbackId"]
        bob_id = self._submit(bob_client, machine="PC-BOB", message="bob issue").get_json()["feedbackId"]

        alice_list = alice_client.get("/api/feedback?query=bob&limit=20&offset=0").get_json()
        self.assertEqual(alice_list["total"], 0)
        self.assertEqual(alice_list["summary"]["records"], 1)
        self.assertEqual(alice_client.get(f"/api/feedback/{bob_id}").status_code, 404)
        self.assertEqual(
            alice_client.get(f"/api/feedback/{bob_id}/attachments/diagnostics.zip").status_code,
            404,
        )
        own_download = alice_client.get(f"/api/feedback/{alice_id}/attachments/diagnostics.zip")
        self.assertEqual(own_download.status_code, 200)
        self.assertEqual(own_download.data, b"diagnostics")
        self.assertIn(alice_id, own_download.headers["Content-Disposition"])
        own_download.close()

    def test_anonymous_revoked_and_read_only_credentials_have_distinct_access(self):
        anonymous = self.app.test_client()
        self.assertEqual(anonymous.get("/api/feedback").status_code, 401)
        legacy_id = self._submit(anonymous, machine="LEGACY-PC", message="anonymous issue").get_json()["feedbackId"]

        alice_client = self.app.test_client()
        self._login(alice_client, "alice-user", "alice password phrase 123")
        alice_id = self._submit(alice_client, machine="PC-ALICE", message="owned issue").get_json()["feedbackId"]
        revoke_all_user_sessions(self.cache, int(self.alice["id"]), reason="test")
        self.assertEqual(alice_client.get("/api/feedback").status_code, 401)

        developer_client = self.app.test_client()
        self._login(developer_client, "sdk-developer", "developer password 789")
        result = developer_client.get("/api/feedback?limit=20&offset=0").get_json()
        self.assertEqual(result["access"], {"scope": "all", "can_manage": False})
        self.assertEqual({item["feedback_id"] for item in result["items"]}, {legacy_id, alice_id})

        key = create_api_key(
            self.cache,
            name="feedback-downloader",
            scopes="feedback:read",
            created_by="test",
        )
        key_result = anonymous.get(
            "/api/feedback?limit=20&offset=0",
            headers={"Authorization": f"Bearer {key['key']}"},
        )
        self.assertEqual(key_result.status_code, 200)
        self.assertEqual(key_result.get_json()["access"]["scope"], "all")
        revoke_api_key(self.cache, key["id"])
        self.assertEqual(anonymous.get(
            "/api/feedback?limit=20&offset=0",
            headers={"Authorization": f"Bearer {key['key']}"},
        ).status_code, 401)

        expired = create_api_key(
            self.cache,
            name="expired-feedback-downloader",
            scopes="feedback:read",
            created_by="test",
        )
        db = self.cache.get_db()
        try:
            db.execute(
                "UPDATE api_keys SET expires_at = ? WHERE id = ?",
                ("2020-01-01T00:00:00+00:00", expired["id"]),
            )
            db.commit()
        finally:
            db.close()
        self.assertEqual(anonymous.get(
            "/api/feedback?limit=20&offset=0",
            headers={"Authorization": f"Bearer {expired['key']}"},
        ).status_code, 401)

    def test_browser_login_submission_binds_verified_account_not_forged_form_owner(self):
        client = self.app.test_client()
        login = self._login(client, "alice-user", "alice password phrase 123")
        response = client.post(
            "/api/feedback",
            data={
                "message": "browser issue",
                "machineName": "BROWSER-PC",
                "ownerUserId": str(self.bob["id"]),
                "ownerUsername": "bob-user",
            },
            headers={
                "Origin": "http://localhost",
                "Sec-Fetch-Site": "same-origin",
                "X-CSRF-Token": login["csrf_token"],
            },
        )
        self.assertEqual(response.status_code, 201)
        detail = client.get(f"/api/feedback/{response.get_json()['feedbackId']}").get_json()
        self.assertEqual(detail["owner_user_id"], self.alice["id"])
        self.assertEqual(detail["owner_username"], "alice-user")

    def test_forced_password_change_session_cannot_submit_or_read_feedback(self):
        forced, error = create_user(
            self.cache,
            "forced-change-user",
            "temporary password phrase 123",
            role="user",
            must_change_password=True,
            account_origin="administrator_created",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(forced)
        client = self.app.test_client()
        self._login(client, "forced-change-user", "temporary password phrase 123")

        self.assertEqual(client.get("/api/feedback").status_code, 403)
        self.assertEqual(
            self._submit(client, machine="FORCED-PC", message="blocked issue").status_code,
            403,
        )

    def test_status_response_keeps_management_access_and_inbox_refreshes(self):
        client = self.app.test_client()
        login = self._login(client, "config-admin", "test-secret")
        identifier = self._submit(client, machine="PC-ADMIN", message="issue").get_json()["feedbackId"]
        headers = {"Origin": "http://localhost", "X-ColorVision-Web": "1", "X-CSRF-Token": login["csrf_token"]}
        with mock.patch("services.feedback_admin._sha256", side_effect=AssertionError("must not hash")):
            for status in ("in_progress", "resolved", "in_progress"):
                response = client.put(f"/api/admin/feedback/{identifier}/status", json={"status": status}, headers=headers)
                self.assertEqual(response.status_code, 200)
                self.assertEqual(response.get_json()["access"], {"scope": "all", "can_manage": True})
                self.assertEqual(response.get_json()["status"], status)
                inbox = client.get("/api/feedback?status=in_progress").get_json()
                self.assertEqual(inbox["total"], int(status == "in_progress"))
                self.assertEqual(inbox["summary"]["status_counts"][status], 1)

    def test_browser_detail_can_skip_hashing_but_default_download_manifest_keeps_hash(self):
        client = self.app.test_client()
        self._login(client, "config-admin", "test-secret")
        identifier = self._submit(client, machine="PC-ADMIN", message="issue").get_json()["feedbackId"]
        with mock.patch("services.feedback_admin._sha256", side_effect=AssertionError("must not hash")):
            response = client.get(f"/api/feedback/{identifier}?include_hashes=false")
            self.assertEqual(response.status_code, 200)
            self.assertNotIn("sha256", response.get_json()["attachments"][0])
        full = client.get(f"/api/feedback/{identifier}").get_json()
        self.assertEqual(len(full["attachments"][0]["sha256"]), 64)

    def test_bulk_requires_manage_csrf_and_audits_only_actual_changes(self):
        admin = self.app.test_client()
        login = self._login(admin, "config-admin", "test-secret")
        identifiers = [self._submit(admin, machine="PC-ADMIN", message=f"issue {i}").get_json()["feedbackId"] for i in range(3)]
        payload = {"feedback_ids": identifiers[:2] + ["missing"], "status": "resolved"}
        anonymous = self.app.test_client()
        self.assertEqual(anonymous.put("/api/admin/feedback/status", json=payload).status_code, 401)
        reader = self.app.test_client()
        self._login(reader, "sdk-developer", "developer password 789")
        self.assertEqual(reader.put("/api/admin/feedback/status", json=payload).status_code, 403)
        key = create_api_key(self.cache, name="read-only", scopes="feedback:read", created_by="test")
        self.assertEqual(anonymous.put("/api/admin/feedback/status", json=payload,
                                      headers={"Authorization": f"Bearer {key['key']}"}).status_code, 403)
        headers = {"Origin": "http://localhost", "X-ColorVision-Web": "1"}
        self.assertEqual(admin.put("/api/admin/feedback/status", json=payload, headers=headers).status_code, 403)
        headers["X-CSRF-Token"] = login["csrf_token"]
        result = admin.put("/api/admin/feedback/status", json=payload, headers=headers)
        self.assertEqual(result.status_code, 200)
        self.assertEqual((result.get_json()["changed"], result.get_json()["failed"]), (2, 1))
        again = admin.put("/api/admin/feedback/status", json=payload, headers=headers).get_json()
        self.assertEqual((again["changed"], again["unchanged"]), (0, 2))
        self.assertEqual(admin.get(f"/api/feedback/{identifiers[2]}").get_json()["status"], "new")
        db = self.cache.get_db()
        try:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM audit_log WHERE action = 'feedback_status_update'").fetchone()[0], 2)
        finally:
            db.close()


if __name__ == "__main__":
    unittest.main()

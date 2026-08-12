import unittest

from flask import Flask, jsonify, session

from services.csrf_protection import issue_csrf_token, register_csrf_protection


class CsrfProtectionTests(unittest.TestCase):
    def setUp(self):
        self.app = Flask(__name__)
        self.app.secret_key = "csrf-test-secret"

        @self.app.get("/session")
        def start_session():
            session["user_authenticated"] = True
            return jsonify({"csrf_token": issue_csrf_token()})

        @self.app.post("/write")
        def write():
            return jsonify({"ok": True})

        @self.app.get("/read")
        def read():
            return jsonify({"ok": True})

        register_csrf_protection(self.app)
        self.client = self.app.test_client()

    def test_same_origin_session_write_requires_valid_token(self):
        token = self.client.get("/session").get_json()["csrf_token"]
        browser_headers = {"Origin": "http://localhost", "Sec-Fetch-Site": "same-origin"}

        missing = self.client.post("/write", headers=browser_headers)
        invalid = self.client.post("/write", headers={**browser_headers, "X-CSRF-Token": "bad"})
        accepted = self.client.post("/write", headers={**browser_headers, "X-CSRF-Token": token})

        self.assertEqual(missing.status_code, 403)
        self.assertEqual(invalid.status_code, 403)
        self.assertEqual(accepted.status_code, 200)

    def test_cross_origin_and_cross_site_writes_are_rejected(self):
        by_origin = self.client.post("/write", headers={"Origin": "https://evil.example"})
        by_fetch_metadata = self.client.post("/write", headers={"Sec-Fetch-Site": "cross-site"})

        self.assertEqual(by_origin.status_code, 403)
        self.assertEqual(by_fetch_metadata.status_code, 403)

    def test_headerless_native_clients_and_safe_reads_remain_compatible(self):
        native = self.client.post("/write", headers={"Authorization": "Bearer native-key"})
        self.client.get("/session")
        explicit_auth = self.client.post(
            "/write",
            headers={"Origin": "http://localhost", "Authorization": "Bearer native-key"},
        )
        cross_origin_read = self.client.get("/read", headers={"Origin": "https://evil.example"})

        self.assertEqual(native.status_code, 200)
        self.assertEqual(explicit_auth.status_code, 200)
        self.assertEqual(cross_origin_read.status_code, 200)


if __name__ == "__main__":
    unittest.main()

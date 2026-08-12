import unittest

from flask import Flask

from services.http_security import register_response_security


class HttpSecurityTests(unittest.TestCase):
    def setUp(self):
        self.app = Flask(__name__)

        @self.app.get("/")
        def home():
            return "home"

        @self.app.get("/scgd_general_wpf/")
        def docs():
            return "docs"

        @self.app.get("/api/auth/session")
        def session():
            return {"authenticated": False}

        @self.app.get("/api/admin/custom-cache")
        def custom_cache():
            return "admin", 200, {"Cache-Control": "private, max-age=5"}

        register_response_security(self.app)
        self.client = self.app.test_client()

    def test_spa_policy_blocks_inline_scripts_and_sets_baseline_headers(self):
        response = self.client.get("/")

        self.assertIn("script-src 'self'", response.headers["Content-Security-Policy"])
        self.assertNotIn("script-src 'self' 'unsafe-inline'", response.headers["Content-Security-Policy"])
        self.assertEqual(response.headers["X-Content-Type-Options"], "nosniff")
        self.assertEqual(response.headers["X-Frame-Options"], "SAMEORIGIN")
        self.assertEqual(response.headers["Referrer-Policy"], "same-origin")
        self.assertIn("camera=()", response.headers["Permissions-Policy"])

    def test_docs_policy_allows_static_inline_bootstrap_scripts(self):
        response = self.client.get("/scgd_general_wpf/")

        self.assertIn("script-src 'self' 'unsafe-inline'", response.headers["Content-Security-Policy"])

    def test_sensitive_apis_are_no_store_without_overriding_explicit_cache_policy(self):
        session_response = self.client.get("/api/auth/session")
        custom_response = self.client.get("/api/admin/custom-cache")

        self.assertEqual(session_response.headers["Cache-Control"], "no-store")
        self.assertEqual(custom_response.headers["Cache-Control"], "private, max-age=5")


if __name__ == "__main__":
    unittest.main()

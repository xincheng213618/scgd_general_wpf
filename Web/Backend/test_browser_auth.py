import unittest

from flask import Flask, Response

from routes.browser_auth import (
    apply_basic_auth_challenge,
    is_browser_navigation,
    is_browser_request,
)


class BrowserAuthTests(unittest.TestCase):
    def setUp(self):
        self.app = Flask(__name__)

    def test_headerless_native_request_keeps_basic_challenge(self):
        with self.app.test_request_context("/api/example"):
            response = apply_basic_auth_challenge(Response(status=401), "ColorVision")

            self.assertFalse(is_browser_request())
            self.assertEqual(response.headers["WWW-Authenticate"], 'Basic realm="ColorVision"')

    def test_browser_metadata_suppresses_basic_challenge(self):
        cases = (
            {"X-ColorVision-Web": "1"},
            {"Origin": "http://localhost"},
            {"Sec-Fetch-Site": "same-origin"},
            {"Sec-Fetch-Mode": "cors"},
        )
        for headers in cases:
            with self.subTest(headers=headers), self.app.test_request_context(
                "/api/example", headers=headers,
            ):
                response = apply_basic_auth_challenge(Response(status=401), "ColorVision")

                self.assertTrue(is_browser_request())
                self.assertNotIn("WWW-Authenticate", response.headers)

    def test_browser_navigation_uses_fetch_mode(self):
        with self.app.test_request_context(
            "/download/example", headers={"Sec-Fetch-Mode": "navigate"},
        ):
            self.assertTrue(is_browser_request())
            self.assertTrue(is_browser_navigation())

    def test_browser_navigation_accepts_html_fallback(self):
        with self.app.test_request_context(
            "/download/example", headers={"Accept": "text/html,application/xhtml+xml"},
        ):
            self.assertFalse(is_browser_request())
            self.assertTrue(is_browser_navigation())


if __name__ == "__main__":
    unittest.main()

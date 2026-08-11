import gzip
import json
import unittest

from flask import Flask, jsonify

from services.http_compression import register_response_compression


class HttpCompressionTests(unittest.TestCase):
    def setUp(self):
        app = Flask(__name__)
        app.json.ensure_ascii = False

        @app.get("/large")
        def large():
            return jsonify({"message": "更新说明" * 500})

        @app.get("/small")
        def small():
            return jsonify({"message": "ok"})

        register_response_compression(app, minimum_size=256)
        self.client = app.test_client()

    def test_large_json_uses_utf8_and_negotiates_gzip(self):
        identity = self.client.get("/large")
        compressed = self.client.get(
            "/large", headers={"Accept-Encoding": "br, gzip"}
        )

        self.assertNotIn(b"\\u66f4", identity.data)
        self.assertEqual(compressed.headers["Content-Encoding"], "gzip")
        self.assertIn("Accept-Encoding", compressed.headers["Vary"])
        self.assertLess(len(compressed.data), len(identity.data))
        self.assertEqual(gzip.decompress(compressed.data), identity.data)
        self.assertEqual(
            json.loads(gzip.decompress(compressed.data)), identity.get_json()
        )

    def test_gzip_quality_zero_keeps_identity_representation(self):
        response = self.client.get(
            "/large", headers={"Accept-Encoding": "gzip;q=0, br;q=1"}
        )

        self.assertIsNone(response.headers.get("Content-Encoding"))
        self.assertIn("Accept-Encoding", response.headers["Vary"])

    def test_wildcard_allows_gzip(self):
        response = self.client.get("/large", headers={"Accept-Encoding": "*"})

        self.assertEqual(response.headers["Content-Encoding"], "gzip")

    def test_small_json_is_not_transformed(self):
        response = self.client.get(
            "/small", headers={"Accept-Encoding": "gzip"}
        )

        self.assertIsNone(response.headers.get("Content-Encoding"))
        self.assertNotIn("Accept-Encoding", response.headers.get("Vary", ""))


if __name__ == "__main__":
    unittest.main()

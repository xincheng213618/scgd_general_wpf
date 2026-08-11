import gzip
import os
import tempfile
import unittest
from pathlib import Path

from flask import Flask

from routes import frontend_spa as frontend_spa_module
from routes.frontend_spa import FrontendSpaContext, register_frontend_spa


class FrontendSpaTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.dist = Path(self.temp_dir.name)
        (self.dist / "assets").mkdir()
        (self.dist / "index.html").write_text(
            '<!doctype html><div id="root"></div>',
            encoding="utf-8",
        )
        self.asset = self.dist / "assets" / "app-deadbeef.js"
        self.asset_bytes = b"export default true;" * 200
        self.asset.write_bytes(self.asset_bytes)
        self.gzip_bytes = gzip.compress(self.asset_bytes, compresslevel=9, mtime=0)
        self.brotli_bytes = bytes.fromhex(
            "1b9f0ff8c55396aad2b52c33f3ed55cb6d95c108eae30e323a00c07a62"
        )
        self.asset.with_name(f"{self.asset.name}.gz").write_bytes(self.gzip_bytes)
        self.asset.with_name(f"{self.asset.name}.br").write_bytes(self.brotli_bytes)
        source_stat = self.asset.stat()
        for variant in (
            self.asset.with_name(f"{self.asset.name}.gz"),
            self.asset.with_name(f"{self.asset.name}.br"),
        ):
            os.utime(variant, ns=(source_stat.st_atime_ns, source_stat.st_mtime_ns))
        self.original_context = frontend_spa_module._ctx
        self.responses = []
        app = Flask(__name__)
        app.config["TESTING"] = True
        register_frontend_spa(app, FrontendSpaContext(
            check_auth=lambda: True,
            dist_dir=self.dist,
        ))
        self.client = app.test_client()

    def tearDown(self):
        for response in self.responses:
            response.close()
        frontend_spa_module._ctx = self.original_context
        self.temp_dir.cleanup()

    def test_hashed_assets_are_immutable_and_missing_assets_stay_404(self):
        existing = self.client.get("/assets/app-deadbeef.js")
        missing = self.client.get("/assets/removed-build.js")
        self.responses.extend((existing, missing))

        self.assertEqual(existing.status_code, 200)
        self.assertEqual(
            existing.headers["Cache-Control"],
            "public, max-age=31536000, immutable",
        )
        self.assertEqual(missing.status_code, 404)
        self.assertNotIn(b'<div id="root">', missing.data)

    def test_assets_negotiate_precompressed_representations(self):
        brotli = self.client.get(
            "/assets/app-deadbeef.js",
            headers={"Accept-Encoding": "gzip, br"},
        )
        gzip_response = self.client.get(
            "/assets/app-deadbeef.js",
            headers={"Accept-Encoding": "gzip"},
        )
        identity = self.client.get(
            "/assets/app-deadbeef.js",
            headers={"Accept-Encoding": "gzip;q=0.5, identity;q=1"},
        )
        self.responses.extend((brotli, gzip_response, identity))

        self.assertEqual(brotli.data, self.brotli_bytes)
        self.assertEqual(brotli.headers["Content-Encoding"], "br")
        self.assertEqual(gzip_response.data, self.gzip_bytes)
        self.assertEqual(gzip_response.headers["Content-Encoding"], "gzip")
        self.assertEqual(identity.data, self.asset_bytes)
        self.assertIsNone(identity.headers.get("Content-Encoding"))
        for response in (brotli, gzip_response, identity):
            self.assertEqual(response.mimetype, "application/javascript")
            self.assertIn("Accept-Encoding", response.headers.get("Vary", ""))
            self.assertEqual(
                response.headers["Cache-Control"],
                "public, max-age=31536000, immutable",
            )

    def test_compressed_assets_keep_conditional_get_contract(self):
        compressed = self.client.get(
            "/assets/app-deadbeef.js",
            headers={"Accept-Encoding": "gzip"},
        )
        compressed_etag = compressed.headers["ETag"]
        compressed_last_modified = compressed.headers["Last-Modified"]
        identity = self.client.get(
            "/assets/app-deadbeef.js",
            headers={"Accept-Encoding": "identity"},
        )
        etag_match = self.client.get(
            "/assets/app-deadbeef.js",
            headers={"Accept-Encoding": "gzip", "If-None-Match": compressed_etag},
        )
        modified_match = self.client.get(
            "/assets/app-deadbeef.js",
            headers={
                "Accept-Encoding": "gzip",
                "If-Modified-Since": compressed_last_modified,
            },
        )
        different_representation = self.client.get(
            "/assets/app-deadbeef.js",
            headers={"Accept-Encoding": "identity", "If-None-Match": compressed_etag},
        )
        self.responses.extend((
            compressed,
            identity,
            etag_match,
            modified_match,
            different_representation,
        ))

        self.assertNotEqual(compressed_etag, identity.headers["ETag"])
        self.assertEqual(etag_match.status_code, 304)
        self.assertEqual(modified_match.status_code, 304)
        self.assertEqual(different_representation.status_code, 200)
        self.assertIn("Accept-Encoding", etag_match.headers.get("Vary", ""))

    def test_range_requests_keep_identity_byte_offsets(self):
        response = self.client.get(
            "/assets/app-deadbeef.js",
            headers={"Accept-Encoding": "gzip, br", "Range": "bytes=0-99"},
        )
        self.responses.append(response)

        self.assertEqual(response.status_code, 206)
        self.assertEqual(response.data, self.asset_bytes[:100])
        self.assertEqual(
            response.headers["Content-Range"],
            f"bytes 0-99/{len(self.asset_bytes)}",
        )
        self.assertIsNone(response.headers.get("Content-Encoding"))
        self.assertIn("Accept-Encoding", response.headers.get("Vary", ""))

    def test_spa_html_always_revalidates(self):
        response = self.client.get("/plugins/DemoPlugin")
        self.responses.append(response)

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.headers["Cache-Control"], "no-cache, must-revalidate")


if __name__ == "__main__":
    unittest.main()

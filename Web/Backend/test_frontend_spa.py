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
        self.index_bytes = (
            b'<!doctype html><div id="root"></div>'
            + b"<!-- compressed SPA fallback fixture -->" * 50
        )
        self.index = self.dist / "index.html"
        self.index.write_bytes(self.index_bytes)
        self.index_gzip_bytes = gzip.compress(
            self.index_bytes, compresslevel=9, mtime=0,
        )
        self.index.with_name("index.html.gz").write_bytes(self.index_gzip_bytes)
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

    def test_known_spa_entry_points_support_direct_navigation_and_refresh(self):
        paths = (
            "/",
            "/account",
            "/account?password_change=required",
            "/transfer",
            "/transfer/share/example-token",
            "/browse/Tool",
            "/plugins/example-plugin",
            "/admin/users",
        )

        for path in paths:
            with self.subTest(path=path):
                response = self.client.get(path)
                self.responses.append(response)
                self.assertEqual(response.status_code, 200)
                self.assertIn(b'<div id="root">', response.data)
                self.assertEqual(
                    response.headers["Cache-Control"],
                    "no-cache, must-revalidate",
                )

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
        self.assertIn(identity.mimetype, {"application/javascript", "text/javascript"})
        for response in (brotli, gzip_response, identity):
            self.assertEqual(response.mimetype, identity.mimetype)
            self.assertIn("Accept-Encoding", response.headers.get("Vary", ""))
            self.assertEqual(
                response.headers["Cache-Control"],
                "public, max-age=31536000, immutable",
            )

    def test_accept_encoding_quality_wildcard_and_identity_contract(self):
        cases = (
            ("br;q=0, gzip;q=1", 200, "gzip", self.gzip_bytes),
            ("gzip;q=0, *;q=0.5", 200, "br", self.brotli_bytes),
            ("br;q=0, gzip;q=0, identity;q=1", 200, None, self.asset_bytes),
            ("identity", 200, None, self.asset_bytes),
            ("*", 200, "br", self.brotli_bytes),
            ("*;q=0", 406, None, b""),
            ("br;q=0, gzip;q=0, identity;q=0", 406, None, b""),
        )

        for accept_encoding, status, content_encoding, body in cases:
            with self.subTest(accept_encoding=accept_encoding):
                response = self.client.get(
                    "/assets/app-deadbeef.js",
                    headers={"Accept-Encoding": accept_encoding},
                )
                self.responses.append(response)
                self.assertEqual(response.status_code, status)
                self.assertEqual(response.headers.get("Content-Encoding"), content_encoding)
                self.assertEqual(response.data, body)
                self.assertIn("Accept-Encoding", response.headers.get("Vary", ""))
                if status == 406:
                    self.assertIsNone(response.headers.get("Cache-Control"))

    def test_head_uses_the_same_compressed_representation_without_a_body(self):
        get_response = self.client.get(
            "/assets/app-deadbeef.js",
            headers={"Accept-Encoding": "gzip"},
        )
        head_response = self.client.head(
            "/assets/app-deadbeef.js",
            headers={"Accept-Encoding": "gzip"},
        )
        self.responses.extend((get_response, head_response))

        self.assertEqual(head_response.status_code, 200)
        self.assertEqual(head_response.data, b"")
        self.assertEqual(head_response.headers["Content-Encoding"], "gzip")
        self.assertEqual(
            head_response.headers["Content-Length"], str(len(self.gzip_bytes)),
        )
        self.assertEqual(head_response.headers["ETag"], get_response.headers["ETag"])

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
        self.assertEqual(etag_match.data, b"")
        self.assertEqual(etag_match.headers["ETag"], compressed_etag)
        self.assertEqual(modified_match.status_code, 304)
        self.assertEqual(modified_match.data, b"")
        self.assertEqual(different_representation.status_code, 200)
        self.assertIn("Accept-Encoding", etag_match.headers.get("Vary", ""))

    def test_range_and_if_range_keep_representation_byte_offsets(self):
        identity = self.client.get(
            "/assets/app-deadbeef.js",
            headers={"Accept-Encoding": "identity"},
        )
        matched = self.client.get(
            "/assets/app-deadbeef.js",
            headers={
                "Accept-Encoding": "gzip, br",
                "Range": "bytes=0-99",
                "If-Range": identity.headers["ETag"],
            },
        )
        stale = self.client.get(
            "/assets/app-deadbeef.js",
            headers={
                "Accept-Encoding": "gzip, br",
                "Range": "bytes=0-99",
                "If-Range": '"stale-etag"',
            },
        )
        encoded = self.client.get(
            "/assets/app-deadbeef.js",
            headers={
                "Accept-Encoding": "gzip, identity;q=0",
                "Range": "bytes=0-99",
            },
        )
        self.responses.extend((identity, matched, stale, encoded))

        self.assertEqual(matched.status_code, 206)
        self.assertEqual(matched.data, self.asset_bytes[:100])
        self.assertEqual(
            matched.headers["Content-Range"],
            f"bytes 0-99/{len(self.asset_bytes)}",
        )
        self.assertIsNone(matched.headers.get("Content-Encoding"))
        self.assertEqual(stale.status_code, 200)
        self.assertEqual(stale.data, self.asset_bytes)
        self.assertIsNone(stale.headers.get("Content-Range"))
        self.assertEqual(encoded.status_code, 206)
        self.assertEqual(encoded.data, self.gzip_bytes[:100])
        self.assertEqual(encoded.headers["Content-Encoding"], "gzip")
        self.assertEqual(
            encoded.headers["Content-Range"],
            f"bytes 0-{min(99, len(self.gzip_bytes) - 1)}/{len(self.gzip_bytes)}",
        )
        for response in (matched, stale, encoded):
            self.assertIn("Accept-Encoding", response.headers.get("Vary", ""))

    def test_precompressed_variants_are_not_public_paths(self):
        gzip_path = self.client.get("/assets/app-deadbeef.js.gz")
        brotli_path = self.client.get("/assets/app-deadbeef.js.br")
        self.responses.extend((gzip_path, brotli_path))

        self.assertEqual(gzip_path.status_code, 404)
        self.assertEqual(brotli_path.status_code, 404)
        self.assertNotIn(b'<div id="root">', gzip_path.data)
        self.assertNotIn(b'<div id="root">', brotli_path.data)

    def test_spa_html_always_revalidates(self):
        plugin_response = self.client.get(
            "/plugins/DemoPlugin",
            headers={"Accept-Encoding": "gzip"},
        )
        admin_response = self.client.get(
            "/admin/settings",
            headers={"Accept-Encoding": "gzip"},
        )
        unknown_response = self.client.get("/not-an-application-route")
        self.responses.extend((plugin_response, admin_response, unknown_response))

        for response in (plugin_response, admin_response):
            self.assertEqual(response.status_code, 200)
            self.assertEqual(response.data, self.index_gzip_bytes)
            self.assertEqual(response.headers["Content-Encoding"], "gzip")
            self.assertEqual(
                response.headers["Cache-Control"], "no-cache, must-revalidate",
            )
        self.assertEqual(unknown_response.status_code, 404)


if __name__ == "__main__":
    unittest.main()

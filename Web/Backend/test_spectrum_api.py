import base64
import copy
import hashlib
import io
import unittest
import zipfile
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch

from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import padding, rsa

import app as marketplace_app
from services.spectrum_release import canonical_manifest_bytes


class SpectrumApiTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.private_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)

    def setUp(self):
        self.temp_dir = TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.storage = self.root / "storage"
        self.storage.mkdir()

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)
        self.original_secret_key = marketplace_app.app.secret_key
        self.original_testing = marketplace_app.app.config.get("TESTING", False)

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {"username": "tester", "password": "secret"}
        marketplace_app.CONFIG["secret_key"] = "test-secret-key"
        marketplace_app.app.secret_key = "test-secret-key"
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.init_db()

        self.key_patch = patch(
            "services.spectrum_release._load_public_key",
            return_value=self.private_key.public_key(),
        )
        self.key_patch.start()
        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        self.key_patch.stop()
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.secret_key = self.original_secret_key
        marketplace_app.app.config["TESTING"] = self.original_testing
        self.temp_dir.cleanup()

    @staticmethod
    def _zip_bytes(*, executable=b"spectrum", extra_members=None):
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, "w", zipfile.ZIP_DEFLATED) as archive:
            archive.writestr("Spectrum.exe", executable)
            archive.writestr("Spectrum.dll", b"spectrum-dll")
            archive.writestr("Spectrum.deps.json", b"{}")
            archive.writestr("Spectrum.runtimeconfig.json", b"{}")
            archive.writestr("DLL/runtime.dll", b"runtime")
            for name, content in extra_members or []:
                archive.writestr(name, content)
        return stream.getvalue()

    def _release_parts(
        self,
        version="2.3.3.4",
        *,
        release_notes="修复更新流程",
        package_bytes=None,
        published_at="2026-08-07T04:00:00Z",
    ):
        package_bytes = package_bytes if package_bytes is not None else self._zip_bytes()
        file_name = f"Spectrum{version}.zip"
        manifest = {
            "schemaVersion": 1,
            "productId": "Spectrum",
            "version": version,
            "publishedAtUtc": published_at,
            "releaseNotes": release_notes,
            "package": {
                "fileName": file_name,
                "size": len(package_bytes),
                "sha256": hashlib.sha256(package_bytes).hexdigest(),
            },
        }
        manifest_bytes = canonical_manifest_bytes(manifest)
        signature = self.private_key.sign(
            manifest_bytes,
            padding.PKCS1v15(),
            hashes.SHA256(),
        )
        return {
            "version": version,
            "release_notes": release_notes,
            "file_name": file_name,
            "package": package_bytes,
            "manifest": manifest_bytes,
            "signature": signature,
        }

    @staticmethod
    def _multipart(parts):
        return {
            "Version": parts["version"],
            "ReleaseNotes": parts["release_notes"],
            "Manifest": (io.BytesIO(parts["manifest"]), "manifest.json"),
            "Signature": (io.BytesIO(parts["signature"]), "manifest.sig"),
            "Package": (io.BytesIO(parts["package"]), parts["file_name"]),
        }

    @staticmethod
    def _auth_headers():
        token = base64.b64encode(b"tester:secret").decode("ascii")
        return {"Authorization": f"Basic {token}"}

    def _publish(self, parts, *, authenticated=True):
        return self.client.post(
            "/api/tool/spectrum/publish",
            data=self._multipart(parts),
            headers=self._auth_headers() if authenticated else None,
            content_type="multipart/form-data",
        )

    def test_empty_latest_contract_returns_not_found(self):
        self.assertEqual(self.client.get("/api/tool/spectrum/latest").status_code, 404)
        self.assertEqual(self.client.get("/api/tool/spectrum/latest-version").status_code, 404)

    def test_publish_requires_authentication(self):
        response = self._publish(self._release_parts(), authenticated=False)

        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.get_json()["error"], "Authentication required")
        self.assertIn("Basic", response.headers.get("WWW-Authenticate", ""))

    def test_browser_publish_auth_does_not_trigger_basic_dialog(self):
        response = self.client.post(
            "/api/tool/spectrum/publish",
            data=self._multipart(self._release_parts()),
            headers={
                "X-ColorVision-Web": "1",
            },
            content_type="multipart/form-data",
        )

        self.assertEqual(response.status_code, 401)
        self.assertNotIn("WWW-Authenticate", response.headers)

    def test_publish_exposes_signed_latest_releases_tools_and_range_download(self):
        parts = self._release_parts()

        published = self._publish(parts)

        self.assertEqual(published.status_code, 201)
        payload = published.get_json()
        self.assertTrue(payload["created"])
        self.assertEqual(payload["version"], parts["version"])
        self.assertIsInstance(payload["release"]["size"], int)
        self.assertEqual(payload["release"]["sha256"], hashlib.sha256(parts["package"]).hexdigest())
        release_dir = self.storage / "Spectrum" / "releases" / parts["version"]
        self.assertEqual((self.storage / "Spectrum" / "LATEST_RELEASE").read_text("utf-8"), parts["version"])
        self.assertEqual((release_dir / "manifest.json").read_bytes(), parts["manifest"])
        self.assertEqual((release_dir / "manifest.sig").read_bytes(), parts["signature"])
        self.assertEqual((release_dir / parts["file_name"]).read_bytes(), parts["package"])

        latest = self.client.get("/api/tool/spectrum/latest")
        self.assertEqual(latest.status_code, 200)
        self.assertEqual(set(latest.get_json()), {"manifestBase64", "signatureBase64"})
        self.assertEqual(base64.b64decode(latest.get_json()["manifestBase64"]), parts["manifest"])
        self.assertEqual(base64.b64decode(latest.get_json()["signatureBase64"]), parts["signature"])
        self.assertEqual(latest.headers["Cache-Control"], "no-store")

        latest_version = self.client.get("/api/tool/spectrum/latest-version")
        self.assertEqual(latest_version.get_json(), {"version": parts["version"]})

        releases = self.client.get("/api/tool/spectrum/releases").get_json()
        self.assertEqual(releases["latestVersion"], parts["version"])
        self.assertEqual(releases["count"], 1)
        self.assertEqual(releases["releases"][0]["downloadUrl"], f"/api/tool/spectrum/download/{parts['version']}")

        tools = self.client.get("/api/site/tools").get_json()
        self.assertEqual(tools["spectrum"]["latest"]["version"], parts["version"])
        self.assertEqual(tools["spectrum"]["browseUrl"], "/browse/Spectrum")

        download_url = f"/api/tool/spectrum/download/{parts['version']}"
        ranged = self.client.get(download_url, headers={"Range": "bytes=0-"})
        self.assertEqual(ranged.status_code, 206)
        self.assertEqual(ranged.data, parts["package"])
        self.assertEqual(
            ranged.headers["Content-Range"],
            f"bytes 0-{len(parts['package']) - 1}/{len(parts['package'])}",
        )
        self.assertEqual(ranged.headers["Accept-Ranges"], "bytes")
        ranged.close()

        full = self.client.get(download_url)
        self.assertEqual(full.status_code, 200)
        etag = full.headers["ETag"]
        full.close()
        conditional = self.client.get(download_url, headers={"If-None-Match": etag})
        self.assertEqual(conditional.status_code, 304)
        conditional.close()

    def test_same_signed_package_is_idempotent(self):
        parts = self._release_parts()
        self.assertEqual(self._publish(parts).status_code, 201)

        repeated = self._publish(parts)

        self.assertEqual(repeated.status_code, 200)
        self.assertFalse(repeated.get_json()["created"])
        self.assertEqual(len(list((self.storage / "Spectrum" / "releases").iterdir())), 1)

    def test_same_package_with_regenerated_timestamp_returns_existing_signed_manifest(self):
        package = self._zip_bytes()
        original = self._release_parts(package_bytes=package, published_at="2026-08-07T04:00:00Z")
        retry = self._release_parts(package_bytes=package, published_at="2026-08-07T05:00:00Z")
        self.assertEqual(self._publish(original).status_code, 201)

        repeated = self._publish(retry)

        self.assertEqual(repeated.status_code, 200)
        self.assertFalse(repeated.get_json()["created"])
        returned_manifest = base64.b64decode(repeated.get_json()["latest"]["manifestBase64"])
        self.assertEqual(returned_manifest, original["manifest"])

    def test_same_package_with_different_release_notes_is_rejected(self):
        package = self._zip_bytes()
        original = self._release_parts(package_bytes=package, release_notes="original")
        changed = self._release_parts(package_bytes=package, release_notes="changed")
        self.assertEqual(self._publish(original).status_code, 201)

        response = self._publish(changed)

        self.assertEqual(response.status_code, 409)
        self.assertIn("metadata", response.get_json()["error"])

    def test_same_version_with_different_package_is_rejected(self):
        original = self._release_parts()
        changed = self._release_parts(package_bytes=self._zip_bytes(executable=b"changed"))
        self.assertEqual(self._publish(original).status_code, 201)

        response = self._publish(changed)

        self.assertEqual(response.status_code, 409)
        saved = self.storage / "Spectrum" / "releases" / original["version"] / original["file_name"]
        self.assertEqual(saved.read_bytes(), original["package"])

    def test_idempotent_retry_cannot_move_latest_backwards(self):
        package = self._zip_bytes()
        older = self._release_parts(version="2.3.3.4", package_bytes=package)
        newer = self._release_parts(version="2.3.3.5", package_bytes=package)
        self.assertEqual(self._publish(older).status_code, 201)
        self.assertEqual(self._publish(newer).status_code, 201)

        response = self._publish(older)

        self.assertEqual(response.status_code, 409)
        self.assertEqual((self.storage / "Spectrum" / "LATEST_RELEASE").read_text("utf-8"), "2.3.3.5")

    def test_invalid_signature_is_rejected_without_writing_release(self):
        parts = self._release_parts()
        parts["signature"] = b"not-a-signature"

        response = self._publish(parts)

        self.assertEqual(response.status_code, 400)
        self.assertIn("signature", response.get_json()["error"].lower())
        self.assertFalse((self.storage / "Spectrum" / "releases").exists())

    def test_zip_with_unsafe_path_is_rejected(self):
        unsafe_package = self._zip_bytes(extra_members=[("../outside.dll", b"bad")])
        parts = self._release_parts(package_bytes=unsafe_package)

        response = self._publish(parts)

        self.assertEqual(response.status_code, 400)
        self.assertIn("unsafe path", response.get_json()["error"])
        self.assertFalse((self.storage / "Spectrum" / "LATEST_RELEASE").exists())

    def test_zip_without_root_spectrum_executable_is_rejected(self):
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, "w", zipfile.ZIP_DEFLATED) as archive:
            archive.writestr("bin/Spectrum.exe", b"nested")
        parts = self._release_parts(package_bytes=stream.getvalue())

        response = self._publish(parts)

        self.assertEqual(response.status_code, 400)
        self.assertIn("at its root", response.get_json()["error"])

    def test_zip_without_required_runtime_files_is_rejected(self):
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, "w", zipfile.ZIP_DEFLATED) as archive:
            archive.writestr("Spectrum.exe", b"spectrum")
        parts = self._release_parts(package_bytes=stream.getvalue())

        response = self._publish(parts)

        self.assertEqual(response.status_code, 400)
        self.assertIn("required root files", response.get_json()["error"])

    def test_zip_crc_failure_is_rejected(self):
        stream = io.BytesIO()
        marker = b"CRC-CONTENT-UNIQUE"
        with zipfile.ZipFile(stream, "w", zipfile.ZIP_STORED) as archive:
            archive.writestr("Spectrum.exe", marker)
            archive.writestr("Spectrum.dll", b"spectrum-dll")
            archive.writestr("Spectrum.deps.json", b"{}")
            archive.writestr("Spectrum.runtimeconfig.json", b"{}")
        damaged = bytearray(stream.getvalue())
        marker_offset = damaged.index(marker)
        damaged[marker_offset] ^= 0x01
        parts = self._release_parts(package_bytes=bytes(damaged))

        response = self._publish(parts)

        self.assertEqual(response.status_code, 400)
        self.assertIn("ZIP", response.get_json()["error"])

    def test_noncanonical_signed_manifest_is_rejected(self):
        parts = self._release_parts()
        manifest = parts["manifest"].decode("utf-8")
        parts["manifest"] = (manifest + "\n").encode("utf-8")
        parts["signature"] = self.private_key.sign(
            parts["manifest"],
            padding.PKCS1v15(),
            hashes.SHA256(),
        )

        response = self._publish(parts)

        self.assertEqual(response.status_code, 400)
        self.assertIn("canonical", response.get_json()["error"])


class SpectrumFrontendContractTests(unittest.TestCase):
    def test_tools_page_contains_signed_spectrum_download_card(self):
        frontend_root = Path(__file__).resolve().parents[1] / "Frontend" / "src"
        tools_page = (frontend_root / "pages" / "ToolsPage.tsx").read_text(encoding="utf-8")
        site_types = (frontend_root / "types" / "site.ts").read_text(encoding="utf-8")

        self.assertIn("spectrum-download-card", tools_page)
        self.assertIn("spectrumRelease.downloadUrl", tools_page)
        self.assertIn("interface SpectrumToolCard", site_types)


if __name__ == "__main__":
    unittest.main()

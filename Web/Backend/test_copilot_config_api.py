import base64
import copy
import hashlib
import hmac
import tempfile
import time
import unittest
from pathlib import Path

import app as marketplace_app


class CopilotConfigApiTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.storage = self.root / "storage"
        self.storage.mkdir()

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)
        self.original_testing = marketplace_app.app.config.get("TESTING", False)

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {
            "username": "admin",
            "password": "secret",
        }
        marketplace_app.CONFIG["secret_key"] = "copilot-test-secret"
        marketplace_app.CONFIG["copilot_sync"] = {
            "version_keys": ["copilot-version-key"],
        }
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.init_db()
        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.config["TESTING"] = self.original_testing
        self.temp_dir.cleanup()

    @staticmethod
    def basic_auth():
        value = base64.b64encode(b"admin:secret").decode("ascii")
        return {"Authorization": f"Basic {value}"}

    @staticmethod
    def profile_payload(**overrides):
        payload = {
            "name": "Shared DeepSeek",
            "vendorType": "DeepSeek",
            "providerType": "AnthropicCompatible",
            "baseUrl": "https://api.deepseek.com/anthropic",
            "model": "deepseek-v4-pro",
            "apiKey": "provider-secret-value",
            "allowInsecureHttp": False,
            "reasoningMode": "High",
            "enabled": True,
            "isDefault": True,
            "sortOrder": 10,
        }
        payload.update(overrides)
        return payload

    def create_profile(self, **overrides):
        return self.client.post(
            "/api/admin/copilot/profiles",
            headers=self.basic_auth(),
            json=self.profile_payload(**overrides),
        )

    def create_sync_key(self, scopes="copilot:config:read"):
        response = self.client.post(
            "/api/admin/api-keys",
            headers=self.basic_auth(),
            json={"name": "Copilot Desktop", "scopes": scopes},
        )
        self.assertEqual(response.status_code, 201)
        return response.get_json()["key"]

    @staticmethod
    def device_headers(
        *,
        version_key="copilot-version-key",
        timestamp=None,
        signature_override="",
    ):
        values = {
            "X-ColorVision-Product": "ColorVision",
            "X-ColorVision-Version": "1.4.10.130",
            "X-ColorVision-Device-Id": "A" * 64,
            "X-ColorVision-OS-Version": "10.0.26100.0",
            "X-ColorVision-Architecture": "X64",
            "X-ColorVision-Timestamp": str(
                int(time.time()) if timestamp is None else timestamp
            ),
            "X-ColorVision-Nonce": "0123456789abcdef0123456789abcdef",
        }
        canonical = "\n".join(values.values()).encode("utf-8")
        values["X-ColorVision-Signature"] = (
            signature_override
            or hmac.new(
                version_key.encode("utf-8"),
                canonical,
                hashlib.sha256,
            ).hexdigest()
        )
        return values

    def test_admin_crud_requires_auth_and_never_echoes_provider_key(self):
        unauthorized = self.client.get("/api/admin/copilot/profiles")
        self.assertEqual(unauthorized.status_code, 401)

        created = self.create_profile()
        self.assertEqual(created.status_code, 201)
        profile = created.get_json()
        self.assertTrue(profile["hasApiKey"])
        self.assertNotIn("apiKey", profile)

        listed = self.client.get(
            "/api/admin/copilot/profiles",
            headers=self.basic_auth(),
        )
        self.assertEqual(listed.status_code, 200)
        self.assertEqual(len(listed.get_json()), 1)
        self.assertNotIn("apiKey", listed.get_json()[0])

        updated_payload = self.profile_payload(
            name="Updated Shared Model",
            apiKey="",
            isDefault=False,
        )
        updated = self.client.put(
            f"/api/admin/copilot/profiles/{profile['id']}",
            headers=self.basic_auth(),
            json=updated_payload,
        )
        self.assertEqual(updated.status_code, 200)
        self.assertEqual(updated.get_json()["name"], "Updated Shared Model")
        self.assertTrue(updated.get_json()["hasApiKey"])

        deleted = self.client.delete(
            f"/api/admin/copilot/profiles/{profile['id']}",
            headers=self.basic_auth(),
        )
        self.assertEqual(deleted.status_code, 200)

    def test_scoped_sync_returns_enabled_profile_and_decrypted_key(self):
        created = self.create_profile()
        self.assertEqual(created.status_code, 201)
        sync_key = self.create_sync_key()

        response = self.client.get(
            "/api/copilot/config",
            headers={"Authorization": f"Bearer {sync_key}"},
        )
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.headers["Cache-Control"], "no-store")
        payload = response.get_json()
        self.assertEqual(payload["schemaVersion"], 1)
        self.assertEqual(payload["defaultProfileId"], created.get_json()["id"])
        self.assertEqual(len(payload["profiles"]), 1)
        self.assertEqual(payload["profiles"][0]["apiKey"], "provider-secret-value")
        self.assertEqual(payload["profiles"][0]["reasoningMode"], "High")

        db = marketplace_app.get_db()
        try:
            encrypted = db.execute(
                "SELECT api_key_encrypted FROM copilot_profiles"
            ).fetchone()["api_key_encrypted"]
        finally:
            db.close()
        self.assertNotIn("provider-secret-value", encrypted)
        self.assertTrue(encrypted.startswith("aesgcm:v1:"))

    def test_signed_colorvision_device_can_sync_without_api_key(self):
        created = self.create_profile()
        self.assertEqual(created.status_code, 201)

        response = self.client.get(
            "/api/copilot/config",
            headers=self.device_headers(),
        )

        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(payload["defaultProfileId"], created.get_json()["id"])
        self.assertEqual(payload["profiles"][0]["apiKey"], "provider-secret-value")
        audit = marketplace_app._cache.get_audit_log(
            action="copilot_config_sync",
        )
        self.assertEqual(audit[0]["actor_type"], "device")
        self.assertTrue(audit[0]["actor_id"].startswith("device:"))

    def test_sync_rejects_missing_device_proof_or_invalid_signature(self):
        missing = self.client.get("/api/copilot/config")
        self.assertEqual(missing.status_code, 401)
        self.assertIn("device proof", missing.get_json()["error"])

        invalid = self.client.get(
            "/api/copilot/config",
            headers=self.device_headers(signature_override="0" * 64),
        )
        self.assertEqual(invalid.status_code, 401)
        self.assertIn("signature", invalid.get_json()["error"])

        expired = self.client.get(
            "/api/copilot/config",
            headers=self.device_headers(timestamp=int(time.time()) - 301),
        )
        self.assertEqual(expired.status_code, 401)
        self.assertIn("Expired", expired.get_json()["error"])

    def test_device_sync_fails_closed_when_version_keys_are_not_configured(self):
        marketplace_app.CONFIG["copilot_sync"] = {"version_keys": []}

        response = self.client.get(
            "/api/copilot/config",
            headers=self.device_headers(),
        )

        self.assertEqual(response.status_code, 503)
        self.assertIn("not configured", response.get_json()["error"])

    def test_sync_rejects_insufficient_api_key(self):
        stats_key = self.create_sync_key("stats:read")
        forbidden = self.client.get(
            "/api/copilot/config",
            headers={"Authorization": f"Bearer {stats_key}"},
        )
        self.assertEqual(forbidden.status_code, 403)
        self.assertEqual(forbidden.get_json()["required"], ["copilot:config:read"])

    def test_disabled_profile_is_not_synced(self):
        response = self.create_profile(enabled=False)
        self.assertEqual(response.status_code, 201)
        sync_key = self.create_sync_key()

        synced = self.client.get(
            "/api/copilot/config",
            headers={"Authorization": f"Bearer {sync_key}"},
        )
        self.assertEqual(synced.status_code, 200)
        self.assertEqual(synced.get_json()["profiles"], [])

    def test_remote_http_model_url_requires_explicit_opt_in(self):
        rejected = self.create_profile(baseUrl="http://model.internal/v1")
        self.assertEqual(rejected.status_code, 400)
        self.assertIn("allowInsecureHttp", rejected.get_json()["error"])

        accepted = self.create_profile(
            baseUrl="http://model.internal/v1",
            allowInsecureHttp=True,
        )
        self.assertEqual(accepted.status_code, 201)


if __name__ == "__main__":
    unittest.main()

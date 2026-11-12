from __future__ import annotations

import base64
import json
import os
import tempfile
import time
import unittest
from concurrent.futures import ThreadPoolExecutor
from contextlib import closing
from datetime import datetime
from importlib import import_module
from pathlib import Path
from unittest.mock import patch

from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec, padding, rsa, utils
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from flask import Flask

from app_setup import ColorVisionRequest, RuntimeOverrides, create_app_and_context, register_all_blueprints
from config_loader import DEFAULT_CONFIG
from db.schema_version import ensure_schema_version
from db_cache import CacheManager
from routes.activation_api import register_activation_api
from services.activation import (
    ActivationService, CHALLENGE_SECONDS, HTTP_TOTAL_DECRYPTIONS_PER_MINUTE,
    LEASE_SECONDS, LICENSE_SECONDS, NEW_LICENSES_PER_HOUR,
)


def _base64(value: bytes) -> str:
    return base64.b64encode(value).decode("ascii")


def _key() -> ec.EllipticCurvePrivateKey:
    return ec.generate_private_key(ec.SECP256R1())


def _public_key(key) -> str:
    return _base64(key.public_key().public_bytes(
        serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo,
    ))


def _proof(challenge: dict, key) -> dict:
    der = key.sign(base64.b64decode(challenge["challenge"]), ec.ECDSA(hashes.SHA256()))
    r, s = utils.decode_dss_signature(der)
    return {
        "challenge_id": challenge["challenge_id"],
        "signature": _base64(r.to_bytes(32, "big") + s.to_bytes(32, "big")),
    }


def _seconds(value: str) -> int:
    return int(datetime.fromisoformat(value.replace("Z", "+00:00")).timestamp())


class ActivationTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.cache = CacheManager(Path(directory.name) / "test.sqlite")
        self.cache.init_db()
        with closing(self.cache.get_db()) as db:
            ensure_schema_version(db)
        self.now = 1_800_000_000
        self.app = Flask(__name__)
        self.app.request_class = ColorVisionRequest
        self.app.config["TESTING"] = True
        register_activation_api(self.app, ActivationService(self.cache.get_db, lambda: self.now))
        self.client = self.app.test_client()

    def challenge(self, key, action, *, mode="online", code=None):
        payload = {"public_key": _public_key(key), "action": action}
        if action in {"apply", "activate"}:
            payload.update(device_name="测试设备", mode=mode)
        if code is not None:
            payload["license_code"] = code
        response = self.client.post("/api/activation/challenge", json=payload)
        self.assertEqual(response.status_code, 200, response.json)
        self.assertEqual(response.headers["Cache-Control"], "no-store")
        return response.json

    def execute(self, key, action, **kwargs):
        return self.client.post(
            f"/api/activation/{action}", json=_proof(self.challenge(key, action, **kwargs), key),
        )

    def assert_error(self, response, status, error):
        self.assertEqual(response.status_code, status, response.json)
        self.assertEqual(response.json["error"], error)
        self.assertEqual(response.headers["Cache-Control"], "no-store")

    def test_proofs_are_key_action_time_and_replay_bound(self):
        device = _key()
        challenge = self.challenge(device, "apply")
        signature = _proof(challenge, device)
        self.assert_error(self.client.post("/api/activation/status", json=signature), 400, "invalid_challenge")
        self.assertEqual(self.client.post("/api/activation/apply", json=signature).status_code, 200)
        self.assert_error(self.client.post("/api/activation/apply", json=signature), 400, "invalid_challenge")

        challenge = self.challenge(device, "status")
        self.assert_error(
            self.client.post("/api/activation/status", json=_proof(challenge, _key())), 403, "invalid_signature",
        )
        self.assert_error(
            self.client.post("/api/activation/status", json=_proof(challenge, device)), 400, "invalid_challenge",
        )

        challenge = self.challenge(device, "release")
        self.now += CHALLENGE_SECONDS
        self.assert_error(
            self.client.post("/api/activation/release", json=_proof(challenge, device)), 400, "invalid_challenge",
        )
        self.assertEqual(len(self.execute(device, "status").json["devices"]), 1)

    def test_apply_retry_is_idempotent_and_release_is_self_only(self):
        owner, joined = _key(), _key()
        applied = self.execute(owner, "apply").json
        self.now += 10
        retried = self.execute(owner, "apply").json
        self.assertEqual(retried["license_code"], applied["license_code"])
        self.assertEqual(retried["devices"], applied["devices"])
        self.assert_error(self.execute(owner, "apply", mode="offline"), 409, "release_required")

        enrolled = self.execute(joined, "activate", code=applied["license_code"]).json
        self.assertIsNone(enrolled["license_code"])
        self.assertEqual(len(enrolled["devices"]), 2)
        self.assert_error(self.execute(_key(), "release"), 404, "not_enrolled")

        released = self.execute(owner, "release").json
        self.assertEqual(released, self.execute(owner, "release").json)
        self.assertIsNone(released["enrollment_mode"])
        self.assertEqual([entry["device_id"] for entry in released["devices"]], [enrolled["current_device_id"]])
        self.assert_error(self.execute(owner, "renew"), 409, "release_required")
        reclaimed = self.execute(owner, "apply", mode="offline").json
        self.assertEqual(reclaimed["license_code"], applied["license_code"])
        self.assertEqual(reclaimed["enrollment_mode"], "offline")
        self.assertEqual(len(reclaimed["devices"]), 2)
        self.assertEqual(self.execute(joined, "release").json["devices"], [])
        self.assertEqual(self.execute(joined, "status").json["devices"], [])
        self.assertEqual(len(self.execute(owner, "status").json["devices"]), 1)

    def test_concurrent_admission_only_admits_one_device_for_final_slot(self):
        owner = _key()
        code = self.execute(owner, "apply").json["license_code"]
        self.assertEqual(self.execute(_key(), "activate", code=code).status_code, 200)
        contenders = [_key(), _key()]
        proofs = [_proof(self.challenge(key, "activate", code=code), key) for key in contenders]

        def submit(proof):
            return self.app.test_client().post("/api/activation/activate", json=proof)

        with ThreadPoolExecutor(2) as pool:
            outcomes = sorted(pool.map(submit, proofs), key=lambda result: result.status_code)
        self.assertEqual(outcomes[0].status_code, 200, outcomes[0].json)
        self.assert_error(outcomes[1], 409, "device_limit")
        self.assertEqual(len(self.execute(owner, "status").json["devices"]), 3)

    def test_lease_timeout_and_renewal_preserve_the_device_limit(self):
        owner, shared = _key(), _key()
        code = self.execute(owner, "apply", mode="offline").json["license_code"]
        online = self.execute(shared, "activate", code=code).json
        self.assertIsNone(online["grant"])
        self.assertEqual(len(online["devices"]), 2)
        self.now += LEASE_SECONDS["online"] - 1
        renewed = self.execute(shared, "renew").json
        own_row = next(row for row in renewed["devices"] if row["device_id"] == online["current_device_id"])
        self.assertEqual(_seconds(own_row["expires_at"]), self.now + LEASE_SECONDS["online"])
        self.now += LEASE_SECONDS["online"]
        expired = self.execute(shared, "status").json
        self.assertEqual(expired["enrollment_mode"], "online")
        self.assertEqual(len(expired["devices"]), 1)
        self.assertEqual(self.execute(shared, "renew").status_code, 200)

        self.now += LEASE_SECONDS["online"]
        for _ in range(2):
            self.assertEqual(self.execute(_key(), "activate", mode="offline", code=code).status_code, 200)
        self.assert_error(self.execute(shared, "renew"), 409, "device_limit")

    def test_offline_grant_is_device_bound_and_expires_no_later_than_the_license(self):
        owner = _key()
        applied = self.execute(owner, "apply", mode="offline").json
        grant = applied["grant"]
        key = serialization.load_der_public_key(base64.b64decode(grant["public_key"]))
        payload = base64.b64decode(grant["payload"])
        signature = base64.b64decode(grant["signature"])
        der = utils.encode_dss_signature(int.from_bytes(signature[:32], "big"), int.from_bytes(signature[32:], "big"))
        key.verify(der, payload, ec.ECDSA(hashes.SHA256()))
        with self.assertRaises(InvalidSignature):
            key.verify(der, payload + b" ", ec.ECDSA(hashes.SHA256()))

        claims = json.loads(payload)
        self.assertEqual(claims["version"], 1)
        self.assertEqual(claims["device_id"], applied["current_device_id"])
        self.assertEqual(claims["mode"], "offline")
        self.assertEqual(_seconds(claims["expires_at"]), self.now + LEASE_SECONDS["offline"])
        self.assertEqual(_seconds(applied["license_expires_at"]), self.now + LICENSE_SECONDS)

        self.now += 15
        status = self.execute(owner, "status").json
        self.assertEqual(status["grant"]["payload"], grant["payload"])
        self.assertEqual(status["grant"]["public_key"], grant["public_key"])

        self.now = _seconds(applied["license_expires_at"]) - 1
        self.assertIsNone(self.execute(owner, "status").json["grant"])
        capped = self.execute(owner, "renew").json
        self.assertEqual(json.loads(base64.b64decode(capped["grant"]["payload"]))["expires_at"], applied["license_expires_at"])
        self.now += 1
        self.assert_error(self.execute(owner, "renew"), 409, "license_expired")
        reapplied = self.execute(owner, "apply", mode="offline").json
        self.assertEqual(reapplied["license_code"], applied["license_code"])
        self.assertEqual(_seconds(reapplied["license_expires_at"]), self.now + LICENSE_SECONDS)
        self.assertEqual(len(reapplied["devices"]), 1)

    def test_new_issue_rate_limit_keeps_existing_license_recoverable(self):
        owner, other = _key(), _key()
        issued = self.execute(owner, "apply").json
        with closing(self.cache.get_db()) as db, db:
            db.execute(
                "UPDATE activation_rate_limits SET attempt_count = ? WHERE bucket LIKE 'issue:%'",
                (NEW_LICENSES_PER_HOUR,),
            )
        self.assert_error(self.execute(other, "apply"), 429, "rate_limited")
        self.assertEqual(self.execute(owner, "apply").json["license_code"], issued["license_code"])
        self.now += 3600
        self.assertEqual(self.execute(other, "apply").status_code, 200)

    def test_rejects_bad_request_shapes_and_oversized_body(self):
        key = ec.generate_private_key(ec.SECP384R1())
        self.assert_error(
            self.client.post("/api/activation/challenge", json={"public_key": _public_key(key), "action": "status"}),
            400, "invalid_request",
        )
        self.assert_error(self.client.post("/api/activation/challenge", json=[]), 400, "invalid_request")
        self.assertEqual(
            self.client.post("/api/activation/challenge", json={"public_key": "a" * 5000}).status_code, 413,
        )


class ActivationHttpTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server_key = rsa.generate_private_key(public_exponent=65537, key_size=3072)

    def setUp(self):
        # Full blueprint registration replaces legacy module contexts. Restore
        # them so other route suites keep their own database and auth policy.
        for name in (
            "admin_api", "cvws_api", "frontend_spa", "health_api", "operations_relay",
            "pages", "public_api", "public_pages", "spectrum_api", "transfer",
        ):
            module = import_module(f"routes.{name}")
            self.enterContext(patch.object(module, "_ctx", module._ctx))
        cvws = import_module("routes.cvws_api")
        self.enterContext(patch.object(cvws, "_CVWS_PACKAGE_RE", cvws._CVWS_PACKAGE_RE))
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.path = self.root / "activation-private.pem"
        self.path.write_bytes(self.server_key.private_bytes(
            serialization.Encoding.PEM, serialization.PrivateFormat.PKCS8, serialization.NoEncryption(),
        ))

    def configure(self, *, file=None, environment=""):
        storage = self.root / "storage"
        self.db = self.root / "marketplace.db"
        config = dict(
            DEFAULT_CONFIG, storage_path=str(storage), secret_key="activation-test-key",
            upload_auth={"username": "fixture", "password": "test-only"},
            activation_http_private_key_file=file, access_analytics_enabled=False,
        )
        overrides = RuntimeOverrides(lambda: config, lambda: storage, lambda: self.db)
        app, ctx, services, helpers = create_app_and_context(overrides)
        self.addCleanup(helpers["access_recorder"].close)
        with patch.dict(os.environ, {"COLORVISION_ACTIVATION_HTTP_PRIVATE_KEY_FILE": environment}):
            register_all_blueprints(app, ctx, services, helpers)
        app.config["TESTING"] = True
        self.client = app.test_client()
        self.get_db = helpers["get_db"]

    def envelope(self, action, payload):
        keys = os.urandom(64)
        nonce = os.urandom(12)
        wrapped_key = self.server_key.public_key().encrypt(
            keys, padding.OAEP(mgf=padding.MGF1(hashes.SHA256()), algorithm=hashes.SHA256(), label=None),
        )
        ciphertext = AESGCM(keys[:32]).encrypt(
            nonce, json.dumps({"action": action, "payload": payload}).encode("utf-8"),
            b"ColorVision Activation HTTP v1 request",
        )
        return {"key": _base64(wrapped_key), "nonce": _base64(nonce), "ciphertext": _base64(ciphertext)}, keys[32:]

    def secure_post(self, envelope, response_key):
        response = self.client.post("/api/activation/secure", json=envelope)
        self.assertEqual(response.status_code, 200, response.json)
        self.assertEqual(response.headers["Cache-Control"], "no-store")
        self.assertEqual(set(response.json), {"nonce", "ciphertext"})
        nonce = base64.b64decode(response.json["nonce"])
        self.assertEqual(len(nonce), 12)
        plaintext = AESGCM(response_key).decrypt(
            nonce, base64.b64decode(response.json["ciphertext"]), b"ColorVision Activation HTTP v1 response",
        )
        return response.json["nonce"], json.loads(plaintext)

    def test_secure_apply_hides_license_code_and_replay_error(self):
        self.configure(file=str(self.path))
        key = _key()
        challenge_request = {
            "public_key": _public_key(key), "action": "apply", "device_name": "TPM 工作站", "mode": "offline",
        }
        blocked = self.client.post("/api/activation/challenge", json=challenge_request)
        self.assertEqual(blocked.status_code, 403)
        self.assertEqual(blocked.json["error"], "secure_transport_required")
        _, challenge = self.secure_post(*self.envelope("challenge", challenge_request))
        self.assertEqual(challenge["status"], 200)

        execution = self.envelope("apply", _proof(challenge["payload"], key))
        nonce, issued = self.secure_post(*execution)
        self.assertEqual(issued["status"], 200)
        self.assertEqual(len(issued["payload"]["license_code"]), 26)
        self.assertEqual(issued["payload"]["devices"][0]["device_name"], "TPM 工作站")
        self.assertIsNotNone(issued["payload"]["grant"])
        next_nonce, replay = self.secure_post(*execution)
        self.assertNotEqual(next_nonce, nonce)
        self.assertEqual(replay["status"], 400)
        self.assertEqual(replay["payload"]["error"], "invalid_challenge")

    def test_tamper_and_malformed_envelopes_return_same_outer_error(self):
        self.configure(file="ignored.pem", environment=str(self.path))
        envelope, _ = self.envelope("challenge", {"public_key": _public_key(_key()), "action": "status"})
        valid = self.client.post("/api/activation/secure", json=envelope)
        self.assertEqual(valid.status_code, 200)
        errors = [self.client.post("/api/activation/secure", json=[])]
        for field in envelope:
            mutated = bytearray(base64.b64decode(envelope[field]))
            mutated[-1] ^= 1
            errors.append(self.client.post("/api/activation/secure", json={**envelope, field: _base64(mutated)}))
        invalid_action, _ = self.envelope("unknown", {})
        errors.append(self.client.post("/api/activation/secure", json=invalid_action))
        for response in errors:
            self.assertEqual(response.status_code, 400)
            self.assertEqual(response.json["error"], "invalid_envelope")
            self.assertEqual(response.json, errors[0].json)
        self.assertEqual(
            self.client.post("/api/activation/secure", json={"ciphertext": "a" * 8192}).status_code, 413,
        )

    def test_invalid_key_fails_closed_while_backend_stays_ready(self):
        with self.assertLogs("app_setup", level="ERROR") as logs:
            self.configure(file=str(self.root / "missing.pem"))
        self.assertIn("Encrypted activation unavailable", logs.output[0])
        self.assertEqual(self.client.post("/api/activation/secure", json={}).status_code, 503)
        self.assertEqual(self.client.post("/api/activation/challenge", json={}).status_code, 403)
        self.assertEqual(self.client.get("/api/health").status_code, 200)
        self.assertEqual(self.client.get("/api/ready").status_code, 200)

    def test_relative_key_path_is_not_accepted(self):
        with self.assertLogs("app_setup", level="ERROR") as logs:
            self.configure(file=self.path.name)
        self.assertIn("must be absolute", logs.output[0])
        self.assertEqual(self.client.post("/api/activation/secure", json={}).status_code, 503)
        self.assertEqual(self.client.post("/api/activation/challenge", json={}).status_code, 403)

    def test_unconfigured_keeps_the_original_api_available(self):
        self.configure()
        self.assertEqual(self.client.post("/api/activation/secure", json={}).status_code, 503)
        response = self.client.post(
            "/api/activation/challenge", json={"public_key": _public_key(_key()), "action": "status"},
        )
        self.assertEqual(response.status_code, 200)

    def test_global_rate_limit_is_enforced_before_rsa_decryption(self):
        self.configure(file=str(self.path))
        with closing(self.get_db()) as db, db:
            db.execute(
                "INSERT INTO activation_rate_limits (bucket, expires_at, attempt_count) VALUES ('http:global', ?, ?)",
                (int(time.time()) + 60, HTTP_TOTAL_DECRYPTIONS_PER_MINUTE),
            )
        envelope, _ = self.envelope("challenge", {"public_key": _public_key(_key()), "action": "status"})
        with patch("routes.activation_api.decrypt_request") as decrypt:
            response = self.client.post("/api/activation/secure", json=envelope)
            decrypt.assert_not_called()
        self.assertEqual(response.status_code, 429)
        self.assertEqual(response.json["error"], "rate_limited")

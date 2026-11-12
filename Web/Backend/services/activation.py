"""Optional desktop licenses and device lease operations."""

from __future__ import annotations

import base64
import binascii
import hashlib
import hmac
import json
import re
import secrets
import sqlite3
import time
import uuid
from collections.abc import Callable
from contextlib import closing
from datetime import datetime, timezone

from cryptography.exceptions import InvalidSignature, UnsupportedAlgorithm
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec, utils


ACTIONS = frozenset({"apply", "activate", "status", "renew", "release"})
DEVICE_LIMIT = 3
LICENSE_SECONDS = 365 * 24 * 60 * 60
LEASE_SECONDS = {"offline": 24 * 60 * 60, "online": 5 * 60}
CHALLENGE_SECONDS = 2 * 60
CHALLENGES_PER_MINUTE = 300
NEW_LICENSES_PER_HOUR = 60
HTTP_DECRYPTIONS_PER_MINUTE = 900
HTTP_TOTAL_DECRYPTIONS_PER_MINUTE = 2400


class ActivationError(Exception):
    def __init__(self, code: str, message: str, status: int = 400):
        super().__init__(message)
        self.code = code
        self.status = status


def _decode(value: object, maximum_length: int) -> bytes:
    if isinstance(value, str) and len(value) <= maximum_length:
        try:
            return base64.b64decode(value, validate=True)
        except (ValueError, binascii.Error):
            pass
    raise ActivationError("invalid_request", "Expected a base64 value.")


def _encode(value: bytes) -> str:
    return base64.b64encode(value).decode("ascii")


def _public_key_bytes(key: ec.EllipticCurvePublicKey) -> bytes:
    return key.public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)


def _device_key(value: object) -> bytes:
    der = _decode(value, 256)
    try:
        key = serialization.load_der_public_key(der)
    except (ValueError, UnsupportedAlgorithm):
        raise ActivationError("invalid_request", "Expected a P-256 public key.") from None
    if not isinstance(key, ec.EllipticCurvePublicKey) or not isinstance(key.curve, ec.SECP256R1):
        raise ActivationError("invalid_request", "Expected a P-256 public key.")
    return _public_key_bytes(key)


def _license_hash(code: object) -> str:
    if isinstance(code, str) and len(code) <= 64:
        normalized = code.strip().replace("-", "").upper()
        if re.fullmatch(r"[A-Z2-7]{26}", normalized):
            return hashlib.sha256(normalized.encode("ascii")).hexdigest()
    raise ActivationError("invalid_license", "Invalid license code format.", 404)


def _license_code(code_key: bytes, license_id: str) -> str:
    digest = hmac.digest(code_key, license_id.encode("ascii"), "sha256")
    return base64.b32encode(digest[:16]).decode("ascii").rstrip("=")


def _timestamp(seconds: int) -> str:
    return datetime.fromtimestamp(seconds, timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


class ActivationService:
    def __init__(self, get_db: Callable[[], sqlite3.Connection], now: Callable[[], float] = time.time):
        self.get_db = get_db
        self.now = now

    def limit_http_decryption(self, address: str) -> None:
        source = hashlib.sha256(address.encode("utf-8")).hexdigest()
        with closing(self.get_db()) as db, db:
            db.execute("BEGIN IMMEDIATE")
            now = int(self.now())
            db.execute("DELETE FROM activation_rate_limits WHERE expires_at <= ?", (now,))
            self._use_rate_slot(db, f"http:{source}", now, 60, HTTP_DECRYPTIONS_PER_MINUTE)
            self._use_rate_slot(db, "http:global", now, 60, HTTP_TOTAL_DECRYPTIONS_PER_MINUTE)

    def create_challenge(self, payload: dict, address: str) -> dict:
        action = payload.get("action")
        if not isinstance(action, str) or action not in ACTIONS:
            raise ActivationError("invalid_request", "Unknown activation operation.")
        public_key = _device_key(payload.get("public_key"))
        code_hash = _license_hash(payload.get("license_code")) if action == "activate" else None
        name = mode = None
        if action in {"apply", "activate"}:
            name, mode = payload.get("device_name"), payload.get("mode")
            if not isinstance(name, str) or not name.strip() or len(name) > 80 or not name.isprintable():
                raise ActivationError("invalid_request", "Expected a device name of at most 80 characters.")
            if not isinstance(mode, str) or mode not in LEASE_SECONDS:
                raise ActivationError("invalid_request", "Expected an online or offline activation mode.")
            name = name.strip()

        challenge_id = secrets.token_hex(16)
        challenge = b"ColorVision Activation v1\x00" + secrets.token_bytes(32)
        source = hashlib.sha256(address.encode("utf-8")).hexdigest()
        with closing(self.get_db()) as db, db:
            db.execute("BEGIN IMMEDIATE")
            now = int(self.now())
            db.execute("DELETE FROM activation_challenges WHERE expires_at <= ?", (now,))
            db.execute("DELETE FROM activation_rate_limits WHERE expires_at <= ?", (now,))
            self._use_rate_slot(db, f"challenge:{source}", now, 60, CHALLENGES_PER_MINUTE)
            db.execute(
                """INSERT INTO activation_challenges
                   (id, challenge, action, public_key, device_name, mode, code_hash, source, expires_at)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                (challenge_id, challenge, action, public_key, name, mode, code_hash, source, now + CHALLENGE_SECONDS),
            )
        return {"challenge_id": challenge_id, "challenge": _encode(challenge)}

    def execute(self, action: str, payload: dict) -> dict:
        challenge_id = payload.get("challenge_id")
        if not isinstance(challenge_id, str) or not re.fullmatch(r"[0-9a-f]{32}", challenge_id):
            raise ActivationError("invalid_request", "Invalid challenge identifier.")
        signature = _decode(payload.get("signature"), 88)
        if len(signature) != 64:
            raise ActivationError("invalid_request", "Expected a 64-byte P-256 signature.")

        with closing(self.get_db()) as db, db:
            # The challenge consumption and seat admission are both serialized
            # across worker processes. Failed operations leave seats unchanged.
            db.execute("BEGIN IMMEDIATE")
            now = int(self.now())
            proof = db.execute(
                "SELECT * FROM activation_challenges WHERE id = ? AND action = ? AND expires_at > ?",
                (challenge_id, action, now),
            ).fetchone()
            if proof is None:
                raise ActivationError("invalid_challenge", "The challenge has expired or was already used.")
            db.execute("DELETE FROM activation_challenges WHERE id = ?", (challenge_id,))
            db.execute("SAVEPOINT activation_operation")
            try:
                public_key = serialization.load_der_public_key(proof["public_key"])
                r, s = int.from_bytes(signature[:32], "big"), int.from_bytes(signature[32:], "big")
                public_key.verify(utils.encode_dss_signature(r, s), proof["challenge"], ec.ECDSA(hashes.SHA256()))
                device_id = hashlib.sha256(proof["public_key"]).hexdigest()
                return self._execute_verified(db, proof, device_id, now)
            except InvalidSignature:
                failure = ActivationError("invalid_signature", "Device signature verification failed.", 403)
            except ActivationError as error:
                failure = error
            db.execute("ROLLBACK TO activation_operation")
        raise failure

    @staticmethod
    def _use_rate_slot(db: sqlite3.Connection, bucket: str, now: int, window: int, limit: int) -> None:
        result = db.execute(
            """INSERT INTO activation_rate_limits (bucket, expires_at, attempt_count) VALUES (?, ?, 1)
               ON CONFLICT(bucket) DO UPDATE SET
                   attempt_count = CASE WHEN expires_at <= ? THEN 1 ELSE attempt_count + 1 END,
                   expires_at = CASE WHEN expires_at <= ? THEN excluded.expires_at ELSE expires_at END
               WHERE expires_at <= ? OR attempt_count < ?""",
            (bucket, now + window, now, now, now, limit),
        )
        if result.rowcount == 0:
            raise ActivationError("rate_limited", "Too many activation requests. Try again later.", 429)

    def _execute_verified(self, db: sqlite3.Connection, proof: sqlite3.Row, device_id: str, now: int) -> dict:
        action = proof["action"]
        device = db.execute("SELECT * FROM activation_devices WHERE device_id = ?", (device_id,)).fetchone()
        if action in {"apply", "activate"}:
            license = self._target_license(db, proof, device, device_id, now)
            self._enroll(db, license, device, device_id, proof["device_name"], proof["mode"], now)
        else:
            if device is None:
                raise ActivationError("not_enrolled", "This device has no activation record.", 404)
            license = db.execute("SELECT * FROM activation_licenses WHERE id = ?", (device["license_id"],)).fetchone()
            if action == "renew":
                if device["released_at"] is not None:
                    raise ActivationError("release_required", "This device was released. Activate it again.", 409)
                self._check_capacity(db, license, device_id, now)
                expiry = min(now + LEASE_SECONDS[device["mode"]], license["expires_at"])
                db.execute(
                    "UPDATE activation_devices SET last_seen_at = ?, expires_at = ? WHERE device_id = ?",
                    (now, expiry, device_id),
                )
            elif action == "release":
                db.execute(
                    "UPDATE activation_devices SET released_at = ? WHERE device_id = ? AND released_at IS NULL",
                    (now, device_id),
                )
        return self._snapshot(db, license, device_id, now)

    def _target_license(
        self, db: sqlite3.Connection, proof: sqlite3.Row, device: sqlite3.Row | None, device_id: str, now: int,
    ) -> sqlite3.Row:
        if proof["action"] == "activate":
            license = db.execute("SELECT * FROM activation_licenses WHERE code_hash = ?", (proof["code_hash"],)).fetchone()
            if license is None:
                raise ActivationError("invalid_license", "The license code is unknown.", 404)
            return license

        license = db.execute("SELECT * FROM activation_licenses WHERE owner_device_id = ?", (device_id,)).fetchone()
        if license is not None:
            if license["expires_at"] <= now:
                db.execute(
                    "UPDATE activation_licenses SET expires_at = ? WHERE id = ?",
                    (now + LICENSE_SECONDS, license["id"]),
                )
                return db.execute("SELECT * FROM activation_licenses WHERE id = ?", (license["id"],)).fetchone()
            return license
        if self._active(device, now):
            raise ActivationError("release_required", "Release this device before applying for its own license.", 409)

        self._use_rate_slot(db, f"issue:{proof['source']}", now, 60 * 60, NEW_LICENSES_PER_HOUR)
        keys = db.execute("SELECT code_key FROM activation_secrets WHERE id = 1").fetchone()
        if keys is None:
            key = ec.generate_private_key(ec.SECP256R1())
            der = key.private_bytes(serialization.Encoding.DER, serialization.PrivateFormat.PKCS8, serialization.NoEncryption())
            code_key = secrets.token_bytes(32)
            db.execute("INSERT INTO activation_secrets (id, code_key, signing_key) VALUES (1, ?, ?)", (code_key, der))
        else:
            code_key = keys["code_key"]
        license_id = uuid.uuid4().hex
        code_hash = _license_hash(_license_code(code_key, license_id))
        db.execute(
            """INSERT INTO activation_licenses (id, code_hash, owner_device_id, device_limit, issued_at, expires_at)
               VALUES (?, ?, ?, ?, ?, ?)""",
            (license_id, code_hash, device_id, DEVICE_LIMIT, now, now + LICENSE_SECONDS),
        )
        return db.execute("SELECT * FROM activation_licenses WHERE id = ?", (license_id,)).fetchone()

    @staticmethod
    def _active(device: sqlite3.Row | None, now: int) -> bool:
        return device is not None and device["released_at"] is None and device["expires_at"] > now

    @staticmethod
    def _check_capacity(db: sqlite3.Connection, license: sqlite3.Row, device_id: str, now: int) -> None:
        if license["expires_at"] <= now:
            raise ActivationError("license_expired", "The license has expired.", 409)
        active_other_devices = db.execute(
            """SELECT COUNT(*) FROM activation_devices
               WHERE license_id = ? AND released_at IS NULL AND expires_at > ? AND device_id != ?""",
            (license["id"], now, device_id),
        ).fetchone()[0]
        if active_other_devices >= license["device_limit"]:
            raise ActivationError("device_limit", "All device seats are in use.", 409)

    def _enroll(self, db, license, device, device_id, name, mode, now):
        if self._active(device, now):
            if device["license_id"] == license["id"] and device["mode"] == mode:
                return
            raise ActivationError("release_required", "Release this device before switching its license or mode.", 409)
        self._check_capacity(db, license, device_id, now)
        expiry = min(now + LEASE_SECONDS[mode], license["expires_at"])
        db.execute(
            """INSERT INTO activation_devices
               (device_id, license_id, device_name, mode, activated_at, last_seen_at, expires_at)
               VALUES (?, ?, ?, ?, ?, ?, ?)
               ON CONFLICT(device_id) DO UPDATE SET license_id = excluded.license_id,
                   device_name = excluded.device_name, mode = excluded.mode,
                   activated_at = excluded.activated_at, last_seen_at = excluded.last_seen_at,
                   expires_at = excluded.expires_at, released_at = NULL""",
            (device_id, license["id"], name, mode, now, now, expiry),
        )

    def _snapshot(self, db: sqlite3.Connection, license: sqlite3.Row, device_id: str, now: int) -> dict:
        device = db.execute("SELECT * FROM activation_devices WHERE device_id = ?", (device_id,)).fetchone()
        is_owner = license["owner_device_id"] == device_id
        devices = []
        if license["expires_at"] > now and (is_owner or device["released_at"] is None):
            devices = db.execute(
                """SELECT device_id, device_name, mode, activated_at, last_seen_at, expires_at
                   FROM activation_devices
                   WHERE license_id = ? AND released_at IS NULL AND expires_at > ?
                   ORDER BY activated_at, device_id""",
                (license["id"], now),
            ).fetchall()
        code = grant = None
        keys = db.execute("SELECT * FROM activation_secrets WHERE id = 1").fetchone()
        if is_owner:
            code = _license_code(keys["code_key"], license["id"])
        if self._active(device, now) and license["expires_at"] > now and device["mode"] == "offline":
            grant = self._grant(keys["signing_key"], license, device)
        return {
            "license_code": code,
            "license_expires_at": _timestamp(license["expires_at"]),
            "device_limit": license["device_limit"],
            "current_device_id": device_id,
            "enrollment_mode": device["mode"] if device["released_at"] is None else None,
            "devices": [
                {key: _timestamp(row[key]) if key.endswith("_at") else row[key] for key in row.keys()}
                for row in devices
            ],
            "grant": grant,
            "server_time": _timestamp(now),
        }

    @staticmethod
    def _grant(signing_key: bytes, license: sqlite3.Row, device: sqlite3.Row) -> dict:
        key = serialization.load_der_private_key(signing_key, password=None)
        payload = json.dumps(
            {
                "version": 1,
                "license_id": license["id"],
                "device_id": device["device_id"],
                "mode": device["mode"],
                "issued_at": _timestamp(device["last_seen_at"]),
                "expires_at": _timestamp(device["expires_at"]),
            },
            separators=(",", ":"),
        ).encode("utf-8")
        r, s = utils.decode_dss_signature(key.sign(payload, ec.ECDSA(hashes.SHA256())))
        return {
            "payload": _encode(payload),
            "signature": _encode(r.to_bytes(32, "big") + s.to_bytes(32, "big")),
            "public_key": _encode(_public_key_bytes(key.public_key())),
        }

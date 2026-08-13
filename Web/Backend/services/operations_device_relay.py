"""End-to-end signed Operations relay for paired Android devices.

The fixed download station may be reached over plain HTTP, so this protocol
never sends a bearer secret. Android task intents are signed by the existing
P-256 device key and are verified again by the desktop before execution. Host
sync, polling, and receipts are signed by the desktop Operations certificate.
"""

from __future__ import annotations

import base64
import binascii
import hashlib
import json
import re
import sqlite3
import uuid
from datetime import datetime, timedelta, timezone

from cryptography import x509
from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec, padding, rsa
from cryptography.x509.oid import NameOID


SAFE_ID = re.compile(r"^[A-Za-z0-9_-]{1,64}$")
SAFE_NONCE = re.compile(r"^[A-Za-z0-9_-]{16,128}$")
ALLOWED_CLOCK_SKEW = timedelta(minutes=2)
NONCE_LIFETIME = timedelta(minutes=5)
ALLOWED_DEVICE_TASK_CAPABILITIES = {
    "ops.application.restart": "ops.jobs.create",
    "ops.diagnostics.request": "ops.jobs.create",
    "ops.flow.cancel": "ops.jobs.create",
    "ops.messaging.reconnect": "ops.jobs.create",
    "ops.service.restart": "ops.jobs.create",
    "ops.window.minimize": "ops.window.control",
    "ops.window.show": "ops.window.control",
}
ALLOWED_RECEIPT_STATUSES = {
    "received", "accepted", "awaiting_local_consent", "completed", "failed", "rejected"
}
HOST_SNAPSHOT_ENVELOPE_PREFIX = "colorvision-relay-snapshot-v1"
HOST_RECEIPT_ENVELOPE_PREFIX = "colorvision-relay-receipt-v1"


class DeviceRelayError(ValueError):
    def __init__(self, code: str, status: int = 400):
        super().__init__(code)
        self.code = code
        self.status = status


def _now() -> datetime:
    return datetime.now(timezone.utc)


def _iso(value: datetime | None = None) -> str:
    return (value or _now()).isoformat()


def _safe_id(value, field: str) -> str:
    text = str(value or "").strip()
    if not SAFE_ID.fullmatch(text):
        raise DeviceRelayError(f"invalid_{field}")
    return text


def _bounded_text(value, field: str, maximum: int, *, required: bool = False) -> str:
    text = str(value or "").strip()
    if len(text) > maximum or required and not text:
        raise DeviceRelayError(f"invalid_{field}")
    return text


def _header(headers, name: str) -> str:
    value = str(headers.get(name, "") or "").strip()
    if not value:
        raise DeviceRelayError("signed_headers_required", 401)
    return value


def _decode_base64(value: str, code: str) -> bytes:
    try:
        return base64.b64decode(value, validate=True)
    except (ValueError, binascii.Error) as exc:
        raise DeviceRelayError(code, 401) from exc


def _request_parts(headers):
    timestamp_text = _header(headers, "X-CV-Timestamp")
    nonce = _header(headers, "X-CV-Nonce")
    signature = _decode_base64(_header(headers, "X-CV-Signature"), "invalid_signature_encoding")
    try:
        timestamp = int(timestamp_text)
        request_time = datetime.fromtimestamp(timestamp, timezone.utc)
    except (ValueError, OverflowError, OSError) as exc:
        raise DeviceRelayError("invalid_timestamp", 401) from exc
    if abs(_now() - request_time) > ALLOWED_CLOCK_SKEW:
        raise DeviceRelayError("request_time_out_of_range", 401)
    if not SAFE_NONCE.fullmatch(nonce):
        raise DeviceRelayError("invalid_nonce", 401)
    return timestamp_text, nonce, signature


def _canonical(method: str, path: str, timestamp: str, nonce: str, body: bytes) -> bytes:
    digest = hashlib.sha256(body).hexdigest()
    return "\n".join((method.upper(), path, timestamp, nonce, digest)).encode("utf-8")


def _verify_host_envelope(public_key, prefix: str, envelope, maximum: int) -> tuple[str, str, dict]:
    if not isinstance(envelope, dict) or set(envelope) != {"body", "signature"}:
        raise DeviceRelayError("invalid_host_envelope")
    body_text = str(envelope.get("body") or "")
    if not body_text or len(body_text.encode("utf-8")) > maximum:
        raise DeviceRelayError("invalid_host_envelope")
    signature_text = str(envelope.get("signature") or "")
    signature = _decode_base64(signature_text, "invalid_host_envelope_signature")
    canonical = f"{prefix}\n{body_text}".encode("utf-8")
    try:
        public_key.verify(signature, canonical, padding.PKCS1v15(), hashes.SHA256())
    except InvalidSignature as exc:
        raise DeviceRelayError("invalid_host_envelope_signature", 401) from exc
    try:
        value = json.loads(body_text)
    except json.JSONDecodeError as exc:
        raise DeviceRelayError("invalid_host_envelope") from exc
    if not isinstance(value, dict):
        raise DeviceRelayError("invalid_host_envelope")
    return body_text, signature_text, value


def _json_body(body: bytes, maximum: int = 65536) -> dict:
    if not body or len(body) > maximum:
        raise DeviceRelayError("invalid_request_body")
    try:
        value = json.loads(body)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise DeviceRelayError("invalid_json") from exc
    if not isinstance(value, dict):
        raise DeviceRelayError("json_object_required")
    return value


def _device_public_key(public_key_spki: str) -> ec.EllipticCurvePublicKey:
    encoded = _decode_base64(public_key_spki, "invalid_device_public_key")
    try:
        key = serialization.load_der_public_key(encoded)
    except (ValueError, TypeError) as exc:
        raise DeviceRelayError("invalid_device_public_key") from exc
    if not isinstance(key, ec.EllipticCurvePublicKey) or not isinstance(key.curve, ec.SECP256R1):
        raise DeviceRelayError("invalid_device_public_key")
    return key


def _host_certificate(certificate_der: str, host_id: str):
    encoded = _decode_base64(certificate_der, "invalid_host_certificate")
    try:
        certificate = x509.load_der_x509_certificate(encoded)
    except ValueError as exc:
        raise DeviceRelayError("invalid_host_certificate", 401) from exc
    names = certificate.subject.get_attributes_for_oid(NameOID.COMMON_NAME)
    if len(names) != 1 or names[0].value != f"ColorVision Operations {host_id}":
        raise DeviceRelayError("host_certificate_subject_mismatch", 401)
    if certificate.issuer != certificate.subject:
        raise DeviceRelayError("host_certificate_not_self_signed", 401)
    public_key = certificate.public_key()
    if not isinstance(public_key, rsa.RSAPublicKey) or public_key.key_size < 3072:
        raise DeviceRelayError("invalid_host_certificate_key", 401)
    now = _now()
    not_before = certificate.not_valid_before_utc
    not_after = certificate.not_valid_after_utc
    if now < not_before or now > not_after:
        raise DeviceRelayError("host_certificate_expired", 401)
    try:
        public_key.verify(
            certificate.signature,
            certificate.tbs_certificate_bytes,
            padding.PKCS1v15(),
            certificate.signature_hash_algorithm,
        )
    except InvalidSignature as exc:
        raise DeviceRelayError("host_certificate_not_self_signed", 401) from exc
    return certificate, public_key


class OperationsDeviceRelayService:
    def __init__(self, cache):
        self._cache = cache

    def sync_host(self, host_id: str, path: str, headers, body: bytes) -> dict:
        host_id = _safe_id(host_id, "host_id")
        request = _json_body(body)
        if _safe_id(request.get("hostId"), "host_id") != host_id:
            raise DeviceRelayError("host_id_mismatch")
        certificate_der = _header(headers, "X-CV-Host-Certificate")
        certificate, public_key = _host_certificate(certificate_der, host_id)
        timestamp, nonce, signature = _request_parts(headers)
        try:
            public_key.verify(signature, _canonical("POST", path, timestamp, nonce, body), padding.PKCS1v15(), hashes.SHA256())
        except InvalidSignature as exc:
            raise DeviceRelayError("invalid_host_signature", 401) from exc

        display_name = _bounded_text(request.get("displayName"), "display_name", 120)
        app_version = _bounded_text(request.get("appVersion"), "app_version", 40)
        status = _bounded_text(request.get("status", "online"), "status", 32, required=True)
        capabilities = request.get("capabilities", [])
        snapshot = request.get("snapshot", {})
        devices = request.get("devices", [])
        if (not isinstance(capabilities, list) or len(capabilities) > 200
                or not isinstance(snapshot, dict) or not isinstance(devices, list) or len(devices) > 100):
            raise DeviceRelayError("invalid_host_sync_payload")
        capabilities_json = json.dumps(capabilities, ensure_ascii=False, separators=(",", ":"))
        snapshot_json = json.dumps(snapshot, ensure_ascii=False, separators=(",", ":"))
        if len(capabilities_json) > 32768 or len(snapshot_json) > 32768:
            raise DeviceRelayError("host_sync_payload_too_large")
        normalized_devices = [self._normalize_device(item) for item in devices]
        if len({item["deviceId"] for item in normalized_devices}) != len(normalized_devices):
            raise DeviceRelayError("duplicate_device_id")
        snapshot_body, snapshot_signature, signed_snapshot = _verify_host_envelope(
            public_key, HOST_SNAPSHOT_ENVELOPE_PREFIX, request.get("snapshotEnvelope"), 65536)
        try:
            snapshot_signed_at = int(signed_snapshot.get("signedAt"))
        except (TypeError, ValueError) as exc:
            raise DeviceRelayError("invalid_snapshot_envelope") from exc
        if (set(signed_snapshot) != {"hostId", "appVersion", "status", "capabilities", "snapshot", "signedAt"}
                or signed_snapshot.get("hostId") != host_id
                or signed_snapshot.get("appVersion") != app_version
                or signed_snapshot.get("status") != status
                or signed_snapshot.get("capabilities") != capabilities
                or signed_snapshot.get("snapshot") != snapshot
                or abs(snapshot_signed_at - int(timestamp)) > 5):
            raise DeviceRelayError("snapshot_envelope_mismatch")

        certificate_sha256 = certificate.fingerprint(hashes.SHA256()).hex()
        now = _iso()
        db = self._cache.get_db()
        try:
            with db:
                existing = db.execute(
                    "SELECT certificate_sha256 FROM operations_relay_host_identities WHERE host_id=?",
                    (host_id,),
                ).fetchone()
                if existing and existing["certificate_sha256"] != certificate_sha256:
                    raise DeviceRelayError("host_identity_conflict", 409)
                self._claim_nonce(db, "host", host_id, nonce)
                db.execute(
                    """INSERT INTO operations_relay_host_identities
                       (host_id, certificate_der, certificate_sha256, created_at, updated_at)
                       VALUES (?, ?, ?, ?, ?)
                       ON CONFLICT(host_id) DO UPDATE SET updated_at=excluded.updated_at""",
                    (host_id, certificate_der, certificate_sha256, now, now),
                )
                db.execute(
                    """INSERT INTO operations_hosts
                       (host_id, display_name, app_version, status, capabilities, snapshot,
                        relay_snapshot_body, relay_snapshot_signature,
                        last_seen_at, created_at, updated_at)
                       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                       ON CONFLICT(host_id) DO UPDATE SET display_name=excluded.display_name,
                         app_version=excluded.app_version, status=excluded.status,
                         capabilities=excluded.capabilities, snapshot=excluded.snapshot,
                         relay_snapshot_body=excluded.relay_snapshot_body,
                         relay_snapshot_signature=excluded.relay_snapshot_signature,
                         last_seen_at=excluded.last_seen_at, updated_at=excluded.updated_at""",
                    (host_id, display_name, app_version, status, capabilities_json, snapshot_json,
                     snapshot_body, snapshot_signature, now, now, now),
                )
                db.execute(
                    "UPDATE operations_relay_devices SET revoked_at=?, updated_at=? WHERE host_id=?",
                    (now, now, host_id),
                )
                for device in normalized_devices:
                    db.execute(
                        """INSERT INTO operations_relay_devices
                           (host_id, device_id, display_name, public_key_spki, scopes, approved_at, revoked_at, updated_at)
                           VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                           ON CONFLICT(host_id, device_id) DO UPDATE SET
                             display_name=excluded.display_name,
                             public_key_spki=excluded.public_key_spki,
                             scopes=excluded.scopes,
                             approved_at=excluded.approved_at,
                             revoked_at=excluded.revoked_at,
                             updated_at=excluded.updated_at""",
                        (host_id, device["deviceId"], device["displayName"], device["publicKeySpki"],
                         device["scopes"], device["approvedAt"], device["revokedAt"], now),
                    )
        finally:
            db.close()
        self._cache.write_audit(
            actor_type="operations_host", actor_id=host_id, action="operations.device_relay.sync",
            target_type="operations_host", target_id=host_id,
            detail=json.dumps({"deviceCount": len(normalized_devices)}, separators=(",", ":")),
        )
        return {"ok": True, "hostId": host_id, "serverTime": now, "deviceCount": len(normalized_devices)}

    def poll_tasks(self, host_id: str, path: str, headers, body: bytes) -> dict:
        host_id = _safe_id(host_id, "host_id")
        db = self._cache.get_db()
        try:
            with db:
                self._authenticate_host(db, host_id, "POST", path, headers, body)
                now = _iso()
                rows = db.execute(
                    """SELECT * FROM operations_tasks
                       WHERE host_id=? AND source_type='device'
                         AND status IN ('queued','delivered') AND expires_at>?
                       ORDER BY created_at LIMIT 50""",
                    (host_id, now),
                ).fetchall()
                for row in rows:
                    if row["status"] == "queued":
                        db.execute(
                            "UPDATE operations_tasks SET status='delivered', delivered_at=? WHERE task_id=?",
                            (now, row["task_id"]),
                        )
            tasks = [self._task_for_host(row) for row in rows]
        finally:
            db.close()
        return {"ok": True, "tasks": tasks, "count": len(tasks), "serverTime": _iso()}

    def create_task(self, path: str, headers, body: bytes) -> tuple[dict, int]:
        request = _json_body(body, 16384)
        host_id = _safe_id(request.get("hostId"), "host_id")
        device_id = _safe_id(_header(headers, "X-CV-Device-Id"), "device_id")
        capability_id = str(request.get("capabilityId") or "")
        required_scope = ALLOWED_DEVICE_TASK_CAPABILITIES.get(capability_id)
        if not required_scope:
            raise DeviceRelayError("task_capability_not_allowed")
        payload = request.get("payload", {})
        if not isinstance(payload, dict):
            raise DeviceRelayError("task_payload_not_allowed")
        if capability_id == "ops.application.restart" and payload:
            raise DeviceRelayError("application_restart_payload_not_allowed")
        if capability_id == "ops.service.restart" and payload:
            raise DeviceRelayError("mqtt_restart_payload_not_allowed")
        if any(name in payload for name in ("command", "executablePath", "shell", "script")):
            raise DeviceRelayError("task_payload_not_allowed")
        if capability_id == "ops.window.show" and payload:
            raise DeviceRelayError("window_show_payload_not_allowed")
        if capability_id == "ops.window.minimize" and payload:
            raise DeviceRelayError("window_minimize_payload_not_allowed")
        if capability_id == "ops.messaging.reconnect" and payload:
            raise DeviceRelayError("message_reconnect_payload_not_allowed")
        if capability_id == "ops.flow.cancel" and payload:
            raise DeviceRelayError("flow_cancel_payload_not_allowed")
        if capability_id == "ops.diagnostics.request":
            if not set(payload).issubset({"reason"}):
                raise DeviceRelayError("invalid_diagnostics_payload")
            payload = {"reason": _bounded_text(payload.get("reason"), "diagnostic_reason", 200)}
        idempotency_key = _safe_id(request.get("idempotencyKey"), "idempotency_key")
        try:
            ttl_seconds = max(60, min(int(request.get("ttlSeconds", 900)), 3600))
        except (TypeError, ValueError) as exc:
            raise DeviceRelayError("invalid_ttl_seconds") from exc

        timestamp, nonce, signature = _request_parts(headers)
        db = self._cache.get_db()
        try:
            with db:
                row = db.execute(
                    """SELECT public_key_spki, scopes FROM operations_relay_devices
                       WHERE host_id=? AND device_id=? AND revoked_at IS NULL""",
                    (host_id, device_id),
                ).fetchone()
                if not row:
                    raise DeviceRelayError("unknown_or_revoked_device", 401)
                scopes = json.loads(row["scopes"])
                if required_scope not in scopes:
                    raise DeviceRelayError("device_scope_required", 403)
                public_key = _device_public_key(row["public_key_spki"])
                try:
                    public_key.verify(signature, _canonical("POST", path, timestamp, nonce, body), ec.ECDSA(hashes.SHA256()))
                except InvalidSignature as exc:
                    raise DeviceRelayError("invalid_request_signature", 401) from exc
                self._claim_nonce(db, "device", f"{host_id}:{device_id}", nonce)

                task_id = uuid.uuid4().hex
                created_at = _now()
                expires_at = created_at + timedelta(seconds=ttl_seconds)
                payload_json = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
                body_text = body.decode("utf-8")
                try:
                    db.execute(
                        """INSERT INTO operations_tasks
                           (task_id, host_id, capability_id, payload, status, idempotency_key,
                            created_by, created_at, expires_at, source_type, device_id,
                            request_body, request_timestamp, request_nonce, request_signature)
                           VALUES (?, ?, ?, ?, 'queued', ?, ?, ?, ?, 'device', ?, ?, ?, ?, ?)""",
                        (task_id, host_id, capability_id, payload_json, idempotency_key, device_id,
                         _iso(created_at), _iso(expires_at), device_id, body_text, timestamp, nonce,
                         base64.b64encode(signature).decode("ascii")),
                    )
                except sqlite3.IntegrityError:
                    existing = db.execute(
                        """SELECT task_id, request_body FROM operations_tasks
                           WHERE host_id=? AND idempotency_key=?""",
                        (host_id, idempotency_key),
                    ).fetchone()
                    if not existing or existing["request_body"] != body_text:
                        raise DeviceRelayError("idempotency_conflict", 409)
                    return {"ok": True, "taskId": existing["task_id"], "deduplicated": True}, 200
        finally:
            db.close()
        self._cache.write_audit(
            actor_type="operations_device", actor_id=device_id, action="operations.device_task.create",
            target_type="operations_task", target_id=task_id,
            detail=json.dumps({"hostId": host_id, "capabilityId": capability_id}, separators=(",", ":")),
        )
        return {"ok": True, "taskId": task_id, "status": "queued", "expiresAt": _iso(expires_at)}, 202

    def record_receipt(self, host_id: str, task_id: str, path: str, headers, body: bytes) -> tuple[dict, int]:
        host_id = _safe_id(host_id, "host_id")
        task_id = _safe_id(task_id, "task_id")
        request = _json_body(body, 16384)
        status = str(request.get("status") or "")
        evidence = request.get("evidence", {})
        if status not in ALLOWED_RECEIPT_STATUSES:
            raise DeviceRelayError("invalid_receipt_status")
        if not isinstance(evidence, dict):
            raise DeviceRelayError("invalid_receipt_evidence")
        evidence_json = json.dumps(evidence, ensure_ascii=False, separators=(",", ":"))
        if len(evidence_json) > 8192:
            raise DeviceRelayError("receipt_evidence_too_large")
        receipt_id = uuid.uuid4().hex
        db = self._cache.get_db()
        try:
            with db:
                timestamp, _nonce, _request_signature, public_key = self._authenticate_host(
                    db, host_id, "POST", path, headers, body)
                task = db.execute(
                    """SELECT task_id, idempotency_key FROM operations_tasks
                       WHERE task_id=? AND host_id=? AND source_type='device'""",
                    (task_id, host_id),
                ).fetchone()
                if not task:
                    raise DeviceRelayError("task_not_found", 404)
                receipt_body, receipt_signature, signed_receipt = _verify_host_envelope(
                    public_key, HOST_RECEIPT_ENVELOPE_PREFIX,
                    request.get("receiptEnvelope"), 16384)
                try:
                    receipt_signed_at = int(signed_receipt.get("signedAt"))
                except (TypeError, ValueError) as exc:
                    raise DeviceRelayError("invalid_receipt_envelope") from exc
                if (set(signed_receipt) != {"hostId", "taskId", "idempotencyKey", "status", "evidence", "signedAt"}
                        or signed_receipt.get("hostId") != host_id
                        or signed_receipt.get("taskId") != task_id
                        or signed_receipt.get("idempotencyKey") != task["idempotency_key"]
                        or signed_receipt.get("status") != status
                        or signed_receipt.get("evidence") != evidence
                        or abs(receipt_signed_at - int(timestamp)) > 5):
                    raise DeviceRelayError("receipt_envelope_mismatch")
                now = _iso()
                # A host may retry the exact signed envelope when the first HTTP
                # response is lost.  Treat that as the same receipt; a new signedAt
                # produces a different body and remains a distinct audit record.
                existing_receipt = db.execute(
                    """SELECT receipt_id FROM operations_task_receipts
                       WHERE task_id=? AND host_id=? AND status=?
                         AND relay_receipt_body=?""",
                    (task_id, host_id, status, receipt_body),
                ).fetchone()
                if existing_receipt:
                    return {
                        "ok": True,
                        "receiptId": existing_receipt["receipt_id"],
                        "status": status,
                        "deduplicated": True,
                    }, 200
                db.execute(
                    """INSERT INTO operations_task_receipts
                       (receipt_id, task_id, host_id, status, evidence, created_at,
                        relay_receipt_body, relay_receipt_signature)
                       VALUES (?, ?, ?, ?, ?, ?, ?, ?)""",
                    (receipt_id, task_id, host_id, status, evidence_json, now,
                     receipt_body, receipt_signature),
                )
                terminal = status if status in {"completed", "failed", "rejected"} else "accepted"
                db.execute("UPDATE operations_tasks SET status=? WHERE task_id=?", (terminal, task_id))
        finally:
            db.close()
        return {"ok": True, "receiptId": receipt_id, "status": status}, 201

    def get_snapshot(self, host_id: str, path: str, headers, body: bytes) -> dict:
        host_id = _safe_id(host_id, "host_id")
        db = self._cache.get_db()
        try:
            with db:
                device_id = self._authenticate_device(db, host_id, "POST", path, headers, body)
                host = db.execute(
                    """SELECT hosts.*, identities.certificate_der
                       FROM operations_hosts AS hosts
                       JOIN operations_relay_host_identities AS identities USING(host_id)
                       WHERE hosts.host_id=?""", (host_id,)).fetchone()
                if not host:
                    raise DeviceRelayError("host_not_found", 404)
            return {
                "ok": True,
                "host": {
                    "hostId": host_id,
                    "displayName": host["display_name"],
                    "appVersion": host["app_version"],
                    "status": host["status"],
                    "capabilities": json.loads(host["capabilities"]),
                    "snapshot": json.loads(host["snapshot"]),
                    "lastSeenAt": host["last_seen_at"],
                },
                "deviceId": device_id,
                "serverTime": _iso(),
                "hostCertificateDer": host["certificate_der"],
                "hostEnvelope": {
                    "body": host["relay_snapshot_body"],
                    "signature": host["relay_snapshot_signature"],
                },
            }
        finally:
            db.close()

    def get_task(self, task_id: str, path: str, headers, body: bytes) -> dict:
        task_id = _safe_id(task_id, "task_id")
        request = _json_body(body, 1024)
        host_id = _safe_id(request.get("hostId"), "host_id")
        db = self._cache.get_db()
        try:
            with db:
                device_id = self._authenticate_device(db, host_id, "POST", path, headers, body)
                task = db.execute(
                    """SELECT * FROM operations_tasks
                       WHERE task_id=? AND host_id=? AND device_id=? AND source_type='device'""",
                    (task_id, host_id, device_id),
                ).fetchone()
                if not task:
                    raise DeviceRelayError("task_not_found", 404)
                certificate = db.execute(
                    "SELECT certificate_der FROM operations_relay_host_identities WHERE host_id=?",
                    (host_id,),
                ).fetchone()
                receipts = db.execute(
                    """SELECT status, evidence, created_at, relay_receipt_body, relay_receipt_signature
                       FROM operations_task_receipts WHERE task_id=? ORDER BY created_at""",
                    (task_id,),
                ).fetchall()
            return {
                "ok": True,
                "task": {
                    "taskId": task_id,
                    "capabilityId": task["capability_id"],
                    "status": task["status"],
                    "createdAt": task["created_at"],
                    "expiresAt": task["expires_at"],
                    "receipts": [
                        {
                            "status": row["status"],
                            "evidence": json.loads(row["evidence"]),
                            "createdAt": row["created_at"],
                            "hostEnvelope": {
                                "body": row["relay_receipt_body"],
                                "signature": row["relay_receipt_signature"],
                            } if row["relay_receipt_body"] and row["relay_receipt_signature"] else None,
                        }
                        for row in receipts
                    ],
                },
                "serverTime": _iso(),
                "hostCertificateDer": certificate["certificate_der"] if certificate else "",
            }
        finally:
            db.close()

    def _authenticate_host(self, db, host_id: str, method: str, path: str, headers, body: bytes):
        if _safe_id(_header(headers, "X-CV-Host-Id"), "host_id") != host_id:
            raise DeviceRelayError("host_id_mismatch", 401)
        row = db.execute(
            "SELECT certificate_der FROM operations_relay_host_identities WHERE host_id=?", (host_id,)
        ).fetchone()
        if not row:
            raise DeviceRelayError("unknown_host_identity", 401)
        _certificate, public_key = _host_certificate(row["certificate_der"], host_id)
        timestamp, nonce, signature = _request_parts(headers)
        try:
            public_key.verify(signature, _canonical(method, path, timestamp, nonce, body), padding.PKCS1v15(), hashes.SHA256())
        except InvalidSignature as exc:
            raise DeviceRelayError("invalid_host_signature", 401) from exc
        self._claim_nonce(db, "host", host_id, nonce)
        return timestamp, nonce, signature, public_key

    def _authenticate_device(self, db, host_id: str, method: str, path: str, headers, body: bytes) -> str:
        device_id = _safe_id(_header(headers, "X-CV-Device-Id"), "device_id")
        row = db.execute(
            """SELECT public_key_spki FROM operations_relay_devices
               WHERE host_id=? AND device_id=? AND revoked_at IS NULL""",
            (host_id, device_id),
        ).fetchone()
        if not row:
            raise DeviceRelayError("unknown_or_revoked_device", 401)
        timestamp, nonce, signature = _request_parts(headers)
        try:
            _device_public_key(row["public_key_spki"]).verify(
                signature, _canonical(method, path, timestamp, nonce, body), ec.ECDSA(hashes.SHA256())
            )
        except InvalidSignature as exc:
            raise DeviceRelayError("invalid_request_signature", 401) from exc
        self._claim_nonce(db, "device", f"{host_id}:{device_id}", nonce)
        return device_id

    @staticmethod
    def _claim_nonce(db, principal_type: str, principal_id: str, nonce: str):
        now = _now()
        db.execute("DELETE FROM operations_relay_nonces WHERE expires_at<?", (_iso(now),))
        try:
            db.execute(
                "INSERT INTO operations_relay_nonces VALUES (?, ?, ?, ?)",
                (principal_type, principal_id, nonce, _iso(now + NONCE_LIFETIME)),
            )
        except sqlite3.IntegrityError as exc:
            raise DeviceRelayError("replayed_request", 409) from exc

    @staticmethod
    def _normalize_device(value) -> dict:
        if not isinstance(value, dict):
            raise DeviceRelayError("invalid_device_record")
        device_id = _safe_id(value.get("deviceId"), "device_id")
        display_name = _bounded_text(value.get("displayName"), "device_name", 80, required=True)
        public_key_spki = str(value.get("publicKeySpki") or "")
        _device_public_key(public_key_spki)
        scopes = value.get("scopes", [])
        if (not isinstance(scopes, list) or len(scopes) > 100
                or any(not isinstance(item, str) or not 1 <= len(item) <= 100 for item in scopes)):
            raise DeviceRelayError("invalid_device_scopes")
        approved_at = _bounded_text(value.get("approvedAt"), "approved_at", 64, required=True)
        revoked = value.get("revokedAt")
        revoked_at = None if revoked in (None, "") else _bounded_text(revoked, "revoked_at", 64, required=True)
        return {
            "deviceId": device_id,
            "displayName": display_name,
            "publicKeySpki": public_key_spki,
            "scopes": json.dumps(sorted(set(scopes)), ensure_ascii=False, separators=(",", ":")),
            "approvedAt": approved_at,
            "revokedAt": revoked_at,
        }

    @staticmethod
    def _task_for_host(row) -> dict:
        return {
            "taskId": row["task_id"],
            "capabilityId": row["capability_id"],
            "idempotencyKey": row["idempotency_key"],
            "requestBody": row["request_body"],
            "deviceId": row["device_id"],
            "timestamp": row["request_timestamp"],
            "nonce": row["request_nonce"],
            "signature": row["request_signature"],
            "createdAt": row["created_at"],
            "expiresAt": row["expires_at"],
        }

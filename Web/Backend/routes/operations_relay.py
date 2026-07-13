"""Authenticated outbound Operations relay routes.

The relay stores heartbeats, catalog-bound task intents, receipts, and bounded
support events. It never accepts shell text, executable paths, or ServiceHost
commands.
"""

from __future__ import annotations

import json
import re
import uuid
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone

from flask import Blueprint, jsonify, request

from services.api_key_service import verify_api_key

operations_relay = Blueprint("operations_relay", __name__)

SAFE_ID = re.compile(r"^[A-Za-z0-9_-]{1,64}$")
ALLOWED_TASK_CAPABILITIES = {
    "ops.diagnostics.request",
    "ops.support.message",
    "ops.deployment.verify",
}
ALLOWED_RECEIPT_STATUSES = {"received", "accepted", "awaiting_local_consent", "completed", "failed", "rejected"}
ALLOWED_SUPPORT_EVENTS = {"session.requested", "session.active", "message", "session.closed", "session.failed"}


@dataclass
class OperationsRelayContext:
    cache: object


_ctx: OperationsRelayContext | None = None


def register_operations_relay_routes(app, ctx: OperationsRelayContext):
    global _ctx
    _ctx = ctx
    app.register_blueprint(operations_relay)


def _now() -> datetime:
    return datetime.now(timezone.utc)


def _iso(value: datetime | None = None) -> str:
    return (value or _now()).isoformat()


def _auth(scope: str):
    header = request.headers.get("Authorization", "")
    if not header.startswith("Bearer "):
        return None
    token = header[7:].strip()
    return verify_api_key(_ctx.cache, token, required_scopes=[scope]) if token else None


def _require(scope: str):
    key = _auth(scope)
    if not key:
        return None, (jsonify({"ok": False, "error": "api_key_scope_required", "scope": scope}), 401)
    return key, None


def _json_body(max_bytes: int = 65536):
    if request.content_length is not None and request.content_length > max_bytes:
        raise ValueError("request_too_large")
    body = request.get_json(silent=True)
    if not isinstance(body, dict):
        raise ValueError("json_object_required")
    return body


def _safe_id(value, field: str) -> str:
    text = str(value or "").strip()
    if not SAFE_ID.fullmatch(text):
        raise ValueError(f"invalid_{field}")
    return text


def _bounded_text(value, field: str, maximum: int) -> str:
    text = str(value or "").strip()
    if len(text) > maximum:
        raise ValueError(f"{field}_too_long")
    return text


def _error(exc: ValueError):
    return jsonify({"ok": False, "error": str(exc)}), 400


@operations_relay.route("/api/ops/v1/hosts/<host_id>/heartbeat", methods=["POST"])
def heartbeat(host_id):
    key, denied = _require("ops:relay")
    if denied:
        return denied
    try:
        host_id = _safe_id(host_id, "host_id")
        body = _json_body()
        display_name = _bounded_text(body.get("displayName"), "display_name", 120)
        app_version = _bounded_text(body.get("appVersion"), "app_version", 40)
        status = _bounded_text(body.get("status", "online"), "status", 32)
        capabilities = body.get("capabilities", [])
        snapshot = body.get("snapshot", {})
        if not isinstance(capabilities, list) or len(capabilities) > 200 or not isinstance(snapshot, dict):
            raise ValueError("invalid_heartbeat_payload")
        capabilities_json = json.dumps(capabilities, ensure_ascii=False, separators=(",", ":"))
        snapshot_json = json.dumps(snapshot, ensure_ascii=False, separators=(",", ":"))
        if len(capabilities_json) > 32768 or len(snapshot_json) > 32768:
            raise ValueError("heartbeat_payload_too_large")
    except ValueError as exc:
        return _error(exc)

    now = _iso()
    db = _ctx.cache.get_db()
    try:
        db.execute(
            """INSERT INTO operations_hosts
               (host_id, display_name, app_version, status, capabilities, snapshot, last_seen_at, created_at, updated_at)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
               ON CONFLICT(host_id) DO UPDATE SET display_name=excluded.display_name,
                 app_version=excluded.app_version, status=excluded.status,
                 capabilities=excluded.capabilities, snapshot=excluded.snapshot,
                 last_seen_at=excluded.last_seen_at, updated_at=excluded.updated_at""",
            (host_id, display_name, app_version, status, capabilities_json, snapshot_json, now, now, now),
        )
        db.commit()
    finally:
        db.close()
    _ctx.cache.write_audit(actor_type="api_key", actor_id=str(key["id"]), action="operations.heartbeat",
                           target_type="operations_host", target_id=host_id,
                           detail=json.dumps({"status": status}, separators=(",", ":")))
    return jsonify({"ok": True, "hostId": host_id, "serverTime": now})


@operations_relay.route("/api/ops/v1/hosts/<host_id>/tasks", methods=["GET"])
def poll_tasks(host_id):
    key, denied = _require("ops:relay")
    if denied:
        return denied
    try:
        host_id = _safe_id(host_id, "host_id")
    except ValueError as exc:
        return _error(exc)
    now = _iso()
    db = _ctx.cache.get_db()
    try:
        rows = db.execute(
            """SELECT * FROM operations_tasks
               WHERE host_id=? AND status IN ('queued','delivered') AND expires_at>?
               ORDER BY created_at LIMIT 50""", (host_id, now)).fetchall()
        task_ids = [row["task_id"] for row in rows if row["status"] == "queued"]
        if task_ids:
            placeholders = ",".join("?" for _ in task_ids)
            db.execute(f"UPDATE operations_tasks SET status='delivered', delivered_at=? WHERE task_id IN ({placeholders})",
                       (now, *task_ids))
            db.commit()
        tasks = [{
            "taskId": row["task_id"], "capabilityId": row["capability_id"],
            "payload": json.loads(row["payload"]), "createdAt": row["created_at"],
            "expiresAt": row["expires_at"], "idempotencyKey": row["idempotency_key"],
        } for row in rows]
    finally:
        db.close()
    return jsonify({"ok": True, "tasks": tasks, "count": len(tasks), "serverTime": now})


@operations_relay.route("/api/ops/v1/tasks", methods=["GET", "POST"])
def create_task():
    key, denied = _require("ops:operator")
    if denied:
        return denied
    if request.method == "GET":
        host_filter = request.args.get("hostId", "").strip()
        if host_filter and not SAFE_ID.fullmatch(host_filter):
            return jsonify({"ok": False, "error": "invalid_host_id"}), 400
        db = _ctx.cache.get_db()
        try:
            if host_filter:
                rows = db.execute(
                    "SELECT * FROM operations_tasks WHERE host_id=? ORDER BY created_at DESC LIMIT 500",
                    (host_filter,),
                ).fetchall()
            else:
                rows = db.execute("SELECT * FROM operations_tasks ORDER BY created_at DESC LIMIT 500").fetchall()
            tasks = [{**dict(row), "payload": json.loads(row["payload"])} for row in rows]
        finally:
            db.close()
        return jsonify({"ok": True, "tasks": tasks, "count": len(tasks)})
    try:
        body = _json_body()
        host_id = _safe_id(body.get("hostId"), "host_id")
        capability_id = str(body.get("capabilityId") or "")
        if capability_id not in ALLOWED_TASK_CAPABILITIES:
            raise ValueError("task_capability_not_allowed")
        payload = body.get("payload", {})
        if not isinstance(payload, dict):
            raise ValueError("invalid_task_payload")
        payload_json = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
        if len(payload_json) > 16384 or any(name in payload for name in ("command", "executablePath", "shell", "script")):
            raise ValueError("task_payload_not_allowed")
        idempotency_key = _safe_id(body.get("idempotencyKey") or uuid.uuid4().hex, "idempotency_key")
        ttl_seconds = max(60, min(int(body.get("ttlSeconds", 900)), 3600))
    except (ValueError, TypeError) as exc:
        return _error(ValueError(str(exc)))

    task_id = uuid.uuid4().hex
    created_at = _now()
    expires_at = created_at + timedelta(seconds=ttl_seconds)
    db = _ctx.cache.get_db()
    try:
        host = db.execute("SELECT host_id FROM operations_hosts WHERE host_id=?", (host_id,)).fetchone()
        if not host:
            return jsonify({"ok": False, "error": "host_not_found"}), 404
        try:
            db.execute(
                """INSERT INTO operations_tasks
                   (task_id, host_id, capability_id, payload, status, idempotency_key, created_by, created_at, expires_at)
                   VALUES (?, ?, ?, ?, 'queued', ?, ?, ?, ?)""",
                (task_id, host_id, capability_id, payload_json, idempotency_key, str(key["id"]), _iso(created_at), _iso(expires_at)),
            )
            db.commit()
        except Exception as exc:
            if "UNIQUE" in str(exc).upper():
                existing = db.execute("SELECT task_id FROM operations_tasks WHERE host_id=? AND idempotency_key=?",
                                      (host_id, idempotency_key)).fetchone()
                return jsonify({"ok": True, "taskId": existing["task_id"], "deduplicated": True}), 200
            raise
    finally:
        db.close()
    _ctx.cache.write_audit(actor_type="api_key", actor_id=str(key["id"]), action="operations.task.create",
                           target_type="operations_task", target_id=task_id,
                           detail=json.dumps({"hostId": host_id, "capabilityId": capability_id}, separators=(",", ":")))
    return jsonify({"ok": True, "taskId": task_id, "status": "queued", "expiresAt": _iso(expires_at)}), 202


@operations_relay.route("/api/ops/v1/hosts/<host_id>/tasks/<task_id>/receipts", methods=["POST"])
def task_receipt(host_id, task_id):
    key, denied = _require("ops:relay")
    if denied:
        return denied
    try:
        host_id = _safe_id(host_id, "host_id")
        task_id = _safe_id(task_id, "task_id")
        body = _json_body()
        status = str(body.get("status") or "")
        if status not in ALLOWED_RECEIPT_STATUSES:
            raise ValueError("invalid_receipt_status")
        evidence = body.get("evidence", {})
        if not isinstance(evidence, dict):
            raise ValueError("invalid_receipt_evidence")
        evidence_json = json.dumps(evidence, ensure_ascii=False, separators=(",", ":"))
        if len(evidence_json) > 16384:
            raise ValueError("receipt_evidence_too_large")
    except ValueError as exc:
        return _error(exc)

    receipt_id = uuid.uuid4().hex
    db = _ctx.cache.get_db()
    try:
        task = db.execute("SELECT * FROM operations_tasks WHERE task_id=? AND host_id=?", (task_id, host_id)).fetchone()
        if not task:
            return jsonify({"ok": False, "error": "task_not_found"}), 404
        db.execute("INSERT INTO operations_task_receipts VALUES (?, ?, ?, ?, ?, ?)",
                   (receipt_id, task_id, host_id, status, evidence_json, _iso()))
        db.execute("UPDATE operations_tasks SET status=? WHERE task_id=?",
                   (status if status in {"completed", "failed", "rejected"} else "accepted", task_id))
        db.commit()
    finally:
        db.close()
    return jsonify({"ok": True, "receiptId": receipt_id, "status": status}), 201


@operations_relay.route("/api/ops/v1/hosts", methods=["GET"])
def list_hosts():
    _key, denied = _require("ops:operator")
    if denied:
        return denied
    db = _ctx.cache.get_db()
    try:
        rows = db.execute("SELECT * FROM operations_hosts ORDER BY last_seen_at DESC LIMIT 500").fetchall()
        hosts = [{**dict(row), "capabilities": json.loads(row["capabilities"]), "snapshot": json.loads(row["snapshot"])} for row in rows]
    finally:
        db.close()
    return jsonify({"ok": True, "hosts": hosts, "count": len(hosts)})


@operations_relay.route("/api/ops/v1/receipts", methods=["GET"])
def list_receipts():
    _key, denied = _require("ops:operator")
    if denied:
        return denied
    host_id = request.args.get("hostId", "").strip()
    if host_id and not SAFE_ID.fullmatch(host_id):
        return jsonify({"ok": False, "error": "invalid_host_id"}), 400
    db = _ctx.cache.get_db()
    try:
        if host_id:
            rows = db.execute(
                "SELECT * FROM operations_task_receipts WHERE host_id=? ORDER BY created_at DESC LIMIT 500",
                (host_id,),
            ).fetchall()
        else:
            rows = db.execute(
                "SELECT * FROM operations_task_receipts ORDER BY created_at DESC LIMIT 500"
            ).fetchall()
        receipts = [{**dict(row), "evidence": json.loads(row["evidence"])} for row in rows]
    finally:
        db.close()
    return jsonify({"ok": True, "receipts": receipts, "count": len(receipts)})


@operations_relay.route("/api/ops/v1/support-events", methods=["GET"])
def list_support_events():
    _key, denied = _require("ops:operator")
    if denied:
        return denied
    session_id = request.args.get("sessionId", "").strip()
    if session_id and not SAFE_ID.fullmatch(session_id):
        return jsonify({"ok": False, "error": "invalid_session_id"}), 400
    db = _ctx.cache.get_db()
    try:
        if session_id:
            rows = db.execute(
                "SELECT * FROM operations_support_events WHERE session_id=? ORDER BY created_at LIMIT 500",
                (session_id,),
            ).fetchall()
        else:
            rows = db.execute(
                "SELECT * FROM operations_support_events ORDER BY created_at DESC LIMIT 500"
            ).fetchall()
        events = [{**dict(row), "payload": json.loads(row["payload"])} for row in rows]
    finally:
        db.close()
    return jsonify({"ok": True, "events": events, "count": len(events)})


@operations_relay.route("/api/ops/v1/hosts/<host_id>/support-events", methods=["POST"])
def support_event(host_id):
    key, denied = _require("ops:relay")
    if denied:
        return denied
    try:
        host_id = _safe_id(host_id, "host_id")
        body = _json_body(max_bytes=16384)
        session_id = _safe_id(body.get("sessionId"), "session_id")
        event_type = str(body.get("eventType") or "")
        if event_type not in ALLOWED_SUPPORT_EVENTS:
            raise ValueError("unsupported_support_event")
        payload = body.get("payload", {})
        if not isinstance(payload, dict):
            raise ValueError("invalid_support_payload")
        payload_json = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
        if len(payload_json) > 8192 or any(name in payload for name in ("command", "shell", "script")):
            raise ValueError("support_payload_not_allowed")
    except ValueError as exc:
        return _error(exc)
    event_id = uuid.uuid4().hex
    db = _ctx.cache.get_db()
    try:
        db.execute("INSERT INTO operations_support_events VALUES (?, ?, ?, ?, ?, ?)",
                   (event_id, host_id, session_id, event_type, payload_json, _iso()))
        db.commit()
    finally:
        db.close()
    return jsonify({"ok": True, "eventId": event_id}), 201

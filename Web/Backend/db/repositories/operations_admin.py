"""Sanitized, bounded read model for the Web Operations dashboard."""

from __future__ import annotations

import json
import math
import sqlite3
from collections.abc import Callable
from datetime import datetime, timedelta, timezone
from typing import Any


ONLINE_THRESHOLD_SECONDS = 90
PENDING_TASK_STATUSES = ("queued", "delivered", "accepted")
FAILED_TASK_STATUSES = ("failed", "rejected")


def _json_object(value: Any) -> dict[str, Any]:
    try:
        parsed = json.loads(str(value or "{}"))
    except (TypeError, ValueError, json.JSONDecodeError):
        return {}
    return parsed if isinstance(parsed, dict) else {}


def _json_string_list(value: Any) -> list[str]:
    try:
        parsed = json.loads(str(value or "[]"))
    except (TypeError, ValueError, json.JSONDecodeError):
        return []
    if not isinstance(parsed, list):
        return []
    result: list[str] = []
    for item in parsed:
        text = str(item or "").strip()
        if text and len(text) <= 128 and text not in result:
            result.append(text)
        if len(result) >= 200:
            break
    return result


def _text(value: Any, maximum: int = 120, fallback: str = "") -> str:
    text = str(value or "").strip()
    return text[:maximum] if text else fallback


def _boolean(value: Any) -> bool:
    return value if isinstance(value, bool) else False


def _integer(value: Any, maximum: int = 10**12) -> int:
    if isinstance(value, bool):
        return 0
    try:
        parsed = int(value)
    except (TypeError, ValueError):
        return 0
    return max(0, min(parsed, maximum))


def _number(value: Any, maximum: float = 10**9) -> float:
    if isinstance(value, bool):
        return 0.0
    try:
        parsed = float(value)
    except (TypeError, ValueError):
        return 0.0
    if not math.isfinite(parsed):
        return 0.0
    return round(max(0.0, min(parsed, maximum)), 2)


def _object(value: Any) -> dict[str, Any]:
    return value if isinstance(value, dict) else {}


def _safe_snapshot(value: Any) -> dict[str, Any]:
    """Keep only fields emitted by OperationsSafeSnapshot on the desktop."""
    root = _json_object(value)
    process = _object(root.get("process"))
    window = _object(root.get("mainWindow"))
    secure = _object(root.get("secureOperations"))
    return {
        "application": _text(root.get("application"), 80, "ColorVision"),
        "version": _text(root.get("version"), 40, "unknown"),
        "isRunning": _boolean(root.get("isRunning")),
        "uptimeSeconds": _integer(root.get("uptimeSeconds")),
        "capturedAt": _text(root.get("capturedAt"), 64),
        "process": {"memoryMb": _number(process.get("memoryMb"))},
        "mainWindow": {
            "exists": _boolean(window.get("exists")),
            "state": _text(window.get("state"), 40, "Unknown"),
            "isVisible": _boolean(window.get("isVisible")),
        },
        "secureOperations": {
            "isRunning": _boolean(secure.get("isRunning")),
            "pairedDeviceCount": _integer(secure.get("pairedDeviceCount"), 10000),
            "relayConfigured": _boolean(secure.get("relayConfigured")),
            "relayRunning": _boolean(secure.get("relayRunning")),
        },
    }


def _utc(value: datetime) -> datetime:
    if value.tzinfo is None:
        return value.replace(tzinfo=timezone.utc)
    return value.astimezone(timezone.utc)


def _parse_timestamp(value: Any) -> datetime | None:
    text = str(value or "").strip()
    if not text:
        return None
    try:
        parsed = datetime.fromisoformat(text.replace("Z", "+00:00"))
    except ValueError:
        return None
    return _utc(parsed)


class SqliteOperationsAdminQuery:
    def __init__(self, connection_factory: Callable[[], sqlite3.Connection]):
        self._connection_factory = connection_factory

    def get_overview(
        self,
        *,
        now: datetime,
        host_limit: int,
        activity_limit: int,
    ) -> dict[str, Any]:
        if host_limit < 1 or host_limit > 200:
            raise ValueError("host_limit must be between 1 and 200")
        if activity_limit < 1 or activity_limit > 200:
            raise ValueError("activity_limit must be between 1 and 200")

        generated_at = _utc(now)
        online_cutoff = generated_at - timedelta(seconds=ONLINE_THRESHOLD_SECONDS)
        db = self._connection_factory()
        try:
            host_summary = db.execute(
                """SELECT COUNT(*) AS total,
                          SUM(CASE WHEN last_seen_at>=? THEN 1 ELSE 0 END) AS online
                   FROM operations_hosts""",
                (online_cutoff.isoformat(),),
            ).fetchone()
            task_summary = db.execute(
                """SELECT COUNT(*) AS total,
                          SUM(CASE WHEN status IN ('queued','delivered','accepted') THEN 1 ELSE 0 END) AS pending,
                          SUM(CASE WHEN status IN ('failed','rejected') THEN 1 ELSE 0 END) AS failed,
                          SUM(CASE WHEN source_type='device' THEN 1 ELSE 0 END) AS device
                   FROM operations_tasks"""
            ).fetchone()
            device_summary = db.execute(
                """SELECT COUNT(*) AS total,
                          SUM(CASE WHEN revoked_at IS NULL THEN 1 ELSE 0 END) AS active
                   FROM operations_relay_devices"""
            ).fetchone()
            signed_relay_hosts = db.execute(
                "SELECT COUNT(*) FROM operations_relay_host_identities"
            ).fetchone()[0]
            active_support = db.execute(
                """SELECT COUNT(*) FROM (
                       SELECT e.host_id, e.session_id,
                              (SELECT e2.event_type
                               FROM operations_support_events e2
                               WHERE e2.host_id=e.host_id AND e2.session_id=e.session_id
                                 AND e2.event_type!='message'
                               ORDER BY e2.created_at DESC, e2.rowid DESC LIMIT 1) AS state
                       FROM operations_support_events e
                       GROUP BY e.host_id, e.session_id
                   ) WHERE state='session.active'"""
            ).fetchone()[0]

            host_rows = db.execute(
                """SELECT h.host_id, h.display_name, h.app_version, h.status, h.capabilities,
                          h.snapshot, h.last_seen_at, h.created_at,
                          EXISTS(SELECT 1 FROM operations_relay_host_identities i
                                 WHERE i.host_id=h.host_id) AS signed_relay_ready
                   FROM operations_hosts h
                   ORDER BY h.last_seen_at DESC, h.host_id
                   LIMIT ?""",
                (host_limit,),
            ).fetchall()
            task_rows = db.execute(
                """SELECT t.task_id, t.host_id, h.display_name, t.capability_id,
                          t.status, t.created_at, t.expires_at, t.delivered_at,
                          t.source_type, t.device_id, d.display_name AS device_display_name,
                          (SELECT COUNT(*) FROM operations_task_receipts r
                           WHERE r.task_id=t.task_id) AS receipt_count,
                          (SELECT r.status FROM operations_task_receipts r
                           WHERE r.task_id=t.task_id
                           ORDER BY r.created_at DESC, r.rowid DESC LIMIT 1) AS last_receipt_status,
                          (SELECT r.created_at FROM operations_task_receipts r
                           WHERE r.task_id=t.task_id
                           ORDER BY r.created_at DESC, r.rowid DESC LIMIT 1) AS last_receipt_at
                   FROM operations_tasks t
                   LEFT JOIN operations_hosts h ON h.host_id=t.host_id
                   LEFT JOIN operations_relay_devices d
                     ON d.host_id=t.host_id AND d.device_id=t.device_id
                   ORDER BY t.created_at DESC, t.rowid DESC
                   LIMIT ?""",
                (activity_limit,),
            ).fetchall()
            support_rows = db.execute(
                """SELECT e.host_id, h.display_name, e.session_id,
                          MIN(e.created_at) AS created_at,
                          MAX(e.created_at) AS last_event_at,
                          COUNT(*) AS event_count,
                          SUM(CASE WHEN e.event_type='message' THEN 1 ELSE 0 END) AS message_count,
                          (SELECT e2.event_type
                           FROM operations_support_events e2
                           WHERE e2.host_id=e.host_id AND e2.session_id=e.session_id
                             AND e2.event_type!='message'
                           ORDER BY e2.created_at DESC, e2.rowid DESC LIMIT 1) AS state
                   FROM operations_support_events e
                   LEFT JOIN operations_hosts h ON h.host_id=e.host_id
                   GROUP BY e.host_id, e.session_id
                   ORDER BY last_event_at DESC, e.session_id
                   LIMIT ?""",
                (activity_limit,),
            ).fetchall()
            device_rows = db.execute(
                """SELECT d.host_id, h.display_name AS host_name, d.device_id,
                          d.display_name, d.scopes, d.approved_at, d.revoked_at,
                          d.updated_at
                   FROM operations_relay_devices d
                   LEFT JOIN operations_hosts h ON h.host_id=d.host_id
                   ORDER BY d.revoked_at IS NOT NULL, d.updated_at DESC,
                            d.host_id, d.device_id
                   LIMIT ?""",
                (activity_limit,),
            ).fetchall()
        finally:
            db.close()

        hosts = []
        for row in host_rows:
            last_seen = _parse_timestamp(row["last_seen_at"])
            hosts.append({
                "hostId": str(row["host_id"]),
                "displayName": _text(row["display_name"], 120) or str(row["host_id"]),
                "appVersion": _text(row["app_version"], 40),
                "reportedStatus": _text(row["status"], 32, "unknown"),
                "online": bool(last_seen and last_seen >= online_cutoff),
                "signedRelayReady": bool(row["signed_relay_ready"]),
                "capabilities": _json_string_list(row["capabilities"]),
                "snapshot": _safe_snapshot(row["snapshot"]),
                "lastSeenAt": str(row["last_seen_at"]),
                "createdAt": str(row["created_at"]),
            })

        recent_tasks = []
        for row in task_rows:
            expires_at = _parse_timestamp(row["expires_at"])
            recent_tasks.append({
                "taskId": str(row["task_id"]),
                "hostId": str(row["host_id"]),
                "hostName": _text(row["display_name"], 120) or str(row["host_id"]),
                "capabilityId": _text(row["capability_id"], 128),
                "status": _text(row["status"], 40, "unknown"),
                "sourceType": "device" if row["source_type"] == "device" else "operator",
                "deviceId": _text(row["device_id"], 64) or None,
                "deviceName": _text(row["device_display_name"], 80) or None,
                "createdAt": str(row["created_at"]),
                "expiresAt": str(row["expires_at"]),
                "deliveredAt": str(row["delivered_at"] or "") or None,
                "expired": bool(expires_at and expires_at < generated_at),
                "receiptCount": int(row["receipt_count"] or 0),
                "lastReceiptStatus": _text(row["last_receipt_status"], 40) or None,
                "lastReceiptAt": str(row["last_receipt_at"] or "") or None,
            })

        support_sessions = [{
            "hostId": str(row["host_id"]),
            "hostName": _text(row["display_name"], 120) or str(row["host_id"]),
            "sessionId": str(row["session_id"]),
            "state": _text(row["state"], 40, "unknown"),
            "createdAt": str(row["created_at"]),
            "lastEventAt": str(row["last_event_at"]),
            "eventCount": int(row["event_count"] or 0),
            "messageCount": int(row["message_count"] or 0),
        } for row in support_rows]

        relay_devices = [{
            "hostId": str(row["host_id"]),
            "hostName": _text(row["host_name"], 120) or str(row["host_id"]),
            "deviceId": str(row["device_id"]),
            "displayName": _text(row["display_name"], 80) or str(row["device_id"]),
            "scopes": _json_string_list(row["scopes"]),
            "active": row["revoked_at"] is None,
            "approvedAt": str(row["approved_at"]),
            "revokedAt": str(row["revoked_at"] or "") or None,
            "updatedAt": str(row["updated_at"]),
        } for row in device_rows]

        total_hosts = int(host_summary["total"] or 0)
        online_hosts = int(host_summary["online"] or 0)
        total_devices = int(device_summary["total"] or 0)
        active_devices = int(device_summary["active"] or 0)
        return {
            "generatedAt": generated_at.isoformat(),
            "onlineThresholdSeconds": ONLINE_THRESHOLD_SECONDS,
            "summary": {
                "totalHosts": total_hosts,
                "onlineHosts": online_hosts,
                "staleHosts": max(0, total_hosts - online_hosts),
                "totalTasks": int(task_summary["total"] or 0),
                "pendingTasks": int(task_summary["pending"] or 0),
                "failedTasks": int(task_summary["failed"] or 0),
                "deviceTasks": int(task_summary["device"] or 0),
                "activeSupportSessions": int(active_support or 0),
                "signedRelayHosts": int(signed_relay_hosts or 0),
                "totalRelayDevices": total_devices,
                "activeRelayDevices": active_devices,
                "revokedRelayDevices": max(0, total_devices - active_devices),
            },
            "hosts": hosts,
            "relayDevices": relay_devices,
            "recentTasks": recent_tasks,
            "supportSessions": support_sessions,
        }

"""Persistence and revocation helpers for database-backed browser sessions."""

from __future__ import annotations

import secrets
import sqlite3
from datetime import datetime, timedelta, timezone
from typing import Any

from db_cache import CacheManager


SESSION_TOUCH_INTERVAL = timedelta(minutes=5)


def _now() -> datetime:
    return datetime.now(timezone.utc)


def _now_iso() -> str:
    return _now().isoformat()


def _session_payload(row, current_session_id: str) -> dict[str, Any]:
    return {
        "id": str(row["id"]),
        "ip_address": str(row["ip_address"] or ""),
        "user_agent": str(row["user_agent"] or ""),
        "created_at": row["created_at"],
        "last_seen_at": row["last_seen_at"],
        "is_current": str(row["id"]) == current_session_id,
    }


def create_user_session(
    cache: CacheManager,
    user_id: int,
    *,
    auth_version: int,
    ip_address: str,
    user_agent: str,
) -> str:
    """Create a new active session and return its opaque identifier."""
    now = _now_iso()
    db = cache.get_db()
    try:
        for _ in range(3):
            session_id = secrets.token_urlsafe(32)
            try:
                db.execute(
                    """INSERT INTO user_sessions
                           (id, user_id, auth_version, ip_address, user_agent,
                            created_at, last_seen_at)
                       VALUES (?, ?, ?, ?, ?, ?, ?)""",
                    (
                        session_id,
                        user_id,
                        auth_version,
                        ip_address[:64],
                        user_agent[:300],
                        now,
                        now,
                    ),
                )
                db.commit()
                return session_id
            except sqlite3.IntegrityError:
                continue
        raise RuntimeError("Unable to allocate a login session identifier")
    finally:
        db.close()


def validate_user_session(
    cache: CacheManager,
    session_id: str,
    user_id: int,
    *,
    auth_version: int,
    ip_address: str,
    user_agent: str,
) -> bool:
    """Validate an active session and periodically refresh its activity metadata."""
    db = cache.get_db()
    try:
        row = db.execute(
            """SELECT * FROM user_sessions
               WHERE id = ? AND user_id = ? AND revoked_at IS NULL""",
            (session_id, user_id),
        ).fetchone()
        if not row or int(row["auth_version"] or 0) != auth_version:
            return False

        normalized_ip = ip_address[:64]
        normalized_agent = user_agent[:300]
        should_touch = (
            str(row["ip_address"] or "") != normalized_ip
            or str(row["user_agent"] or "") != normalized_agent
        )
        try:
            last_seen = datetime.fromisoformat(str(row["last_seen_at"]))
            if last_seen.tzinfo is None:
                last_seen = last_seen.replace(tzinfo=timezone.utc)
            should_touch = should_touch or _now() - last_seen >= SESSION_TOUCH_INTERVAL
        except (TypeError, ValueError):
            should_touch = True

        if should_touch:
            db.execute(
                """UPDATE user_sessions
                   SET last_seen_at = ?, ip_address = ?, user_agent = ?
                   WHERE id = ? AND revoked_at IS NULL""",
                (_now_iso(), normalized_ip, normalized_agent, session_id),
            )
            db.commit()
        return True
    finally:
        db.close()


def list_user_sessions(
    cache: CacheManager,
    user_id: int,
    *,
    current_session_id: str,
) -> list[dict[str, Any]]:
    """List active sessions, lazily revoking rows from an older auth version."""
    db = cache.get_db()
    try:
        now = _now_iso()
        db.execute(
            """UPDATE user_sessions
               SET revoked_at = ?, revoke_reason = 'authentication_changed'
               WHERE user_id = ? AND revoked_at IS NULL
                 AND auth_version != COALESCE(
                     (SELECT auth_version FROM users WHERE id = ?), -1
                 )""",
            (now, user_id, user_id),
        )
        rows = db.execute(
            """SELECT * FROM user_sessions
               WHERE user_id = ? AND revoked_at IS NULL
               ORDER BY CASE WHEN id = ? THEN 0 ELSE 1 END,
                        last_seen_at DESC, created_at DESC""",
            (user_id, current_session_id),
        ).fetchall()
        db.commit()
        return [_session_payload(row, current_session_id) for row in rows]
    finally:
        db.close()


def revoke_user_session(
    cache: CacheManager,
    user_id: int,
    session_id: str,
    *,
    current_session_id: str,
    reason: str = "user_revoked",
) -> tuple[bool, str | None]:
    if session_id == current_session_id:
        return False, "current_session"
    db = cache.get_db()
    try:
        cursor = db.execute(
            """UPDATE user_sessions
               SET revoked_at = ?, revoke_reason = ?
               WHERE id = ? AND user_id = ? AND revoked_at IS NULL""",
            (_now_iso(), reason, session_id, user_id),
        )
        db.commit()
        return (True, None) if cursor.rowcount else (False, "session_not_found")
    finally:
        db.close()


def revoke_other_user_sessions(
    cache: CacheManager,
    user_id: int,
    *,
    current_session_id: str,
    reason: str = "user_revoked_others",
) -> int:
    db = cache.get_db()
    try:
        cursor = db.execute(
            """UPDATE user_sessions
               SET revoked_at = ?, revoke_reason = ?
               WHERE user_id = ? AND id != ? AND revoked_at IS NULL""",
            (_now_iso(), reason, user_id, current_session_id),
        )
        db.commit()
        return max(0, cursor.rowcount)
    finally:
        db.close()


def revoke_all_user_sessions(
    cache: CacheManager,
    user_id: int,
    *,
    reason: str,
    auth_version: int | None = None,
) -> int:
    db = cache.get_db()
    try:
        version_clause = " AND auth_version = ?" if auth_version is not None else ""
        parameters: list[Any] = [_now_iso(), reason, user_id]
        if auth_version is not None:
            parameters.append(auth_version)
        cursor = db.execute(
            f"""UPDATE user_sessions
               SET revoked_at = ?, revoke_reason = ?
               WHERE user_id = ? AND revoked_at IS NULL{version_clause}""",
            parameters,
        )
        if auth_version is not None:
            db.execute(
                """UPDATE user_sessions
                   SET revoked_at = ?, revoke_reason = 'authentication_changed'
                   WHERE user_id = ? AND revoked_at IS NULL AND auth_version != ?""",
                (_now_iso(), user_id, auth_version),
            )
        db.commit()
        return max(0, cursor.rowcount)
    finally:
        db.close()


def restore_current_user_session(
    cache: CacheManager,
    user_id: int,
    session_id: str,
    *,
    auth_version: int,
) -> bool:
    """Keep the current browser signed in after its own password reset."""
    db = cache.get_db()
    try:
        cursor = db.execute(
            """UPDATE user_sessions
               SET auth_version = ?, revoked_at = NULL, revoke_reason = '', last_seen_at = ?
               WHERE id = ? AND user_id = ?""",
            (auth_version, _now_iso(), session_id, user_id),
        )
        db.commit()
        return cursor.rowcount > 0
    finally:
        db.close()


def revoke_current_user_session(
    cache: CacheManager,
    user_id: int,
    session_id: str,
    *,
    reason: str = "logout",
) -> bool:
    db = cache.get_db()
    try:
        cursor = db.execute(
            """UPDATE user_sessions
               SET revoked_at = ?, revoke_reason = ?
               WHERE id = ? AND user_id = ? AND revoked_at IS NULL""",
            (_now_iso(), reason, session_id, user_id),
        )
        db.commit()
        return cursor.rowcount > 0
    finally:
        db.close()

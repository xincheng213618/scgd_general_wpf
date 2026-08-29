"""Retention for transient account-security state."""

from __future__ import annotations

import sqlite3
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

from db_cache import CacheManager
from services.login_throttle_service import STALE_ATTEMPT_RETENTION
from services.password_recovery_service import (
    STALE_RECOVERY_RATE_LIMIT_RETENTION,
    expire_password_recovery_requests,
)
from services.registration_rate_limit_service import STALE_REGISTRATION_RETENTION


SESSION_IDLE_TIMEOUT = timedelta(days=30)
SECURITY_HISTORY_RETENTION = timedelta(days=30)
BACKUP_SECURITY_SCRUB_VERSION = 1
TRANSIENT_SECURITY_TABLES = (
    "user_sessions",
    "login_attempts",
    "registration_rate_limits",
    "password_recovery_rate_limits",
    "password_recovery_requests",
)


def _utc_now(value: datetime | None = None) -> datetime:
    current = value or datetime.now(timezone.utc)
    if current.tzinfo is None:
        return current.replace(tzinfo=timezone.utc)
    return current.astimezone(timezone.utc)


def cleanup_account_security_data(
    cache: CacheManager,
    *,
    now: datetime | None = None,
) -> dict[str, Any]:
    """Expire abandoned sessions and bound transient security history."""
    current = _utc_now(now)
    current_iso = current.isoformat()
    idle_cutoff = (current - SESSION_IDLE_TIMEOUT).isoformat()
    history_cutoff = (current - SECURITY_HISTORY_RETENTION).isoformat()
    login_cutoff = (current - STALE_ATTEMPT_RETENTION).isoformat()
    registration_cutoff = (current - STALE_REGISTRATION_RETENTION).isoformat()
    recovery_limit_cutoff = (current - STALE_RECOVERY_RATE_LIMIT_RETENTION).isoformat()
    recovery_expired = expire_password_recovery_requests(cache, now=current)

    db = cache.get_db()
    try:
        db.execute("BEGIN IMMEDIATE")
        sessions_expired = db.execute(
            """UPDATE user_sessions
               SET revoked_at = ?, revoke_reason = 'inactive_expired'
               WHERE revoked_at IS NULL AND last_seen_at < ?""",
            (current_iso, idle_cutoff),
        ).rowcount
        sessions_deleted = db.execute(
            "DELETE FROM user_sessions WHERE revoked_at IS NOT NULL AND revoked_at < ?",
            (history_cutoff,),
        ).rowcount
        login_attempts_deleted = db.execute(
            """DELETE FROM login_attempts
               WHERE last_failed_at < ? AND (locked_until IS NULL OR locked_until <= ?)""",
            (login_cutoff, current_iso),
        ).rowcount
        registration_limits_deleted = db.execute(
            "DELETE FROM registration_rate_limits WHERE last_attempt_at < ?",
            (registration_cutoff,),
        ).rowcount
        password_recovery_limits_deleted = db.execute(
            "DELETE FROM password_recovery_rate_limits WHERE last_attempt_at < ?",
            (recovery_limit_cutoff,),
        ).rowcount
        password_recovery_deleted = db.execute(
            """DELETE FROM password_recovery_requests
               WHERE status = 'resolved'
                 AND COALESCE(resolved_at, last_requested_at) < ?""",
            (history_cutoff,),
        ).rowcount
        db.commit()
    except Exception:
        db.rollback()
        raise
    finally:
        db.close()

    return {
        "session_idle_days": SESSION_IDLE_TIMEOUT.days,
        "history_retention_days": SECURITY_HISTORY_RETENTION.days,
        "sessions_expired": max(0, int(sessions_expired)),
        "sessions_deleted": max(0, int(sessions_deleted)),
        "login_attempts_deleted": max(0, int(login_attempts_deleted)),
        "registration_limits_deleted": max(0, int(registration_limits_deleted)),
        "password_recovery_limits_deleted": max(0, int(password_recovery_limits_deleted)),
        "password_recovery_expired": max(0, int(recovery_expired)),
        "password_recovery_deleted": max(0, int(password_recovery_deleted)),
    }


def scrub_account_security_database(db_path: Path) -> dict[str, Any]:
    """Remove non-restorable authentication state from one SQLite snapshot."""
    path = Path(db_path)
    db = sqlite3.connect(str(path), timeout=15)
    try:
        db.execute("BEGIN IMMEDIATE")
        deleted_by_table: dict[str, int] = {}
        for table_name in TRANSIENT_SECURITY_TABLES:
            table_exists = db.execute(
                "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = ?",
                (table_name,),
            ).fetchone()
            if table_exists is None:
                deleted_by_table[table_name] = 0
                continue
            deleted_by_table[table_name] = max(
                0,
                int(db.execute(f'DELETE FROM "{table_name}"').rowcount or 0),
            )

        accounts_invalidated = 0
        schema_version_exists = db.execute(
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'schema_version'"
        ).fetchone()
        scrub_version = 0
        if schema_version_exists is not None:
            scrub_row = db.execute(
                "SELECT value FROM schema_version WHERE key = 'backup_security_scrub'"
            ).fetchone()
            scrub_version = int(scrub_row[0] or 0) if scrub_row else 0
        user_columns = {
            str(row[1])
            for row in db.execute("PRAGMA table_info(users)").fetchall()
        }
        if "auth_version" in user_columns and scrub_version < BACKUP_SECURITY_SCRUB_VERSION:
            accounts_invalidated = max(
                0,
                int(db.execute(
                    "UPDATE users SET auth_version = COALESCE(auth_version, 0) + 1"
                ).rowcount or 0),
            )
            if schema_version_exists is not None:
                db.execute(
                    """INSERT INTO schema_version (key, value)
                       VALUES ('backup_security_scrub', ?)
                       ON CONFLICT(key) DO UPDATE SET value = excluded.value""",
                    (BACKUP_SECURITY_SCRUB_VERSION,),
                )

        check = db.execute("PRAGMA quick_check").fetchone()
        if not check or str(check[0]).lower() != "ok":
            raise sqlite3.DatabaseError(
                f"backup integrity check failed: {check[0] if check else 'no result'}"
            )
        db.commit()
    except Exception:
        db.rollback()
        raise
    finally:
        db.close()

    return {
        "path": str(path),
        "deleted": sum(deleted_by_table.values()),
        "accounts_invalidated": accounts_invalidated,
        "tables": deleted_by_table,
    }

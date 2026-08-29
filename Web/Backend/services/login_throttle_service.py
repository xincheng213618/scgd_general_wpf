"""Persistent account-wide login throttling with per-source diagnostics."""

from __future__ import annotations

import math
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from typing import Any

from db_cache import CacheManager


MAX_FAILED_ATTEMPTS = 5
FAILURE_WINDOW = timedelta(minutes=15)
LOCK_DURATION = timedelta(minutes=15)
STALE_ATTEMPT_RETENTION = timedelta(days=1)


@dataclass(frozen=True)
class LoginThrottleStatus:
    locked: bool
    failed_count: int
    attempts_remaining: int
    retry_after: int = 0


def _utc_now(value: datetime | None = None) -> datetime:
    current = value or datetime.now(timezone.utc)
    if current.tzinfo is None:
        return current.replace(tzinfo=timezone.utc)
    return current.astimezone(timezone.utc)


def _username_key(username: str) -> str:
    return username.strip().casefold()[:128] or "<empty>"


def _ip_address(ip_address: str) -> str:
    return ip_address.strip()[:64] or "unknown"


def _parse_timestamp(value: str | None) -> datetime | None:
    if not value:
        return None
    try:
        parsed = datetime.fromisoformat(value)
    except ValueError:
        return None
    return _utc_now(parsed)


def _status(failed_count: int, locked_until: str | None, now: datetime) -> LoginThrottleStatus:
    unlock_at = _parse_timestamp(locked_until)
    if unlock_at is not None and unlock_at > now:
        return LoginThrottleStatus(
            locked=True,
            failed_count=failed_count,
            attempts_remaining=0,
            retry_after=max(1, math.ceil((unlock_at - now).total_seconds())),
        )
    return LoginThrottleStatus(
        locked=False,
        failed_count=failed_count,
        attempts_remaining=max(0, MAX_FAILED_ATTEMPTS - failed_count),
    )


def get_login_throttle_status(
    cache: CacheManager,
    username: str,
    *,
    now: datetime | None = None,
) -> LoginThrottleStatus:
    """Return the account-wide status without mutating the attempt window."""
    current = _utc_now(now)
    window_cutoff = (current - FAILURE_WINDOW).isoformat()
    db = cache.get_db()
    try:
        row = db.execute(
            """SELECT
                   COALESCE(SUM(CASE WHEN window_started_at >= ? THEN failed_count ELSE 0 END), 0)
                       AS failed_count,
                   MAX(locked_until) AS locked_until
               FROM login_attempts WHERE username_key = ?""",
            (window_cutoff, _username_key(username)),
        ).fetchone()
        return _status(
            int(row["failed_count"] if row else 0),
            row["locked_until"] if row else None,
            current,
        )
    finally:
        db.close()


def record_login_failure(
    cache: CacheManager,
    username: str,
    ip_address: str,
    *,
    now: datetime | None = None,
) -> LoginThrottleStatus:
    """Atomically add a failed attempt and lock the account at the threshold."""
    current = _utc_now(now)
    current_iso = current.isoformat()
    window_cutoff = (current - FAILURE_WINDOW).isoformat()
    stale_cutoff = (current - STALE_ATTEMPT_RETENTION).isoformat()
    username_key = _username_key(username)
    source_ip = _ip_address(ip_address)
    db = cache.get_db()
    try:
        db.execute("BEGIN IMMEDIATE")
        db.execute(
            """DELETE FROM login_attempts
               WHERE last_failed_at < ? AND (locked_until IS NULL OR locked_until <= ?)""",
            (stale_cutoff, current_iso),
        )
        existing_lock = db.execute(
            """SELECT
                   COALESCE(SUM(CASE WHEN window_started_at >= ? THEN failed_count ELSE 0 END), 0)
                       AS failed_count,
                   MAX(locked_until) AS locked_until
               FROM login_attempts WHERE username_key = ?""",
            (window_cutoff, username_key),
        ).fetchone()
        locked_status = _status(
            int(existing_lock["failed_count"] if existing_lock else 0),
            existing_lock["locked_until"] if existing_lock else None,
            current,
        )
        if locked_status.locked:
            db.commit()
            return locked_status

        db.execute(
            """DELETE FROM login_attempts
               WHERE username_key = ? AND window_started_at < ?
                 AND (locked_until IS NULL OR locked_until <= ?)""",
            (username_key, window_cutoff, current_iso),
        )
        db.execute(
            """INSERT INTO login_attempts
                   (username_key, ip_address, failed_count, window_started_at,
                    last_failed_at, locked_until)
               VALUES (?, ?, 1, ?, ?, NULL)
               ON CONFLICT(username_key, ip_address) DO UPDATE SET
                   failed_count = login_attempts.failed_count + 1,
                   last_failed_at = excluded.last_failed_at,
                   locked_until = NULL""",
            (username_key, source_ip, current_iso, current_iso),
        )
        count_row = db.execute(
            """SELECT COALESCE(SUM(failed_count), 0) AS failed_count
               FROM login_attempts
               WHERE username_key = ? AND window_started_at >= ?""",
            (username_key, window_cutoff),
        ).fetchone()
        failed_count = int(count_row["failed_count"] if count_row else 0)
        locked_until = None
        if failed_count >= MAX_FAILED_ATTEMPTS:
            locked_until = (current + LOCK_DURATION).isoformat()
            db.execute(
                "UPDATE login_attempts SET locked_until = ? WHERE username_key = ?",
                (locked_until, username_key),
            )
        db.commit()
        return _status(failed_count, locked_until, current)
    except Exception:
        db.rollback()
        raise
    finally:
        db.close()


def clear_login_failures(cache: CacheManager, username: str) -> int:
    """Clear every source row after the account proves valid credentials."""
    db = cache.get_db()
    try:
        cursor = db.execute(
            "DELETE FROM login_attempts WHERE username_key = ?",
            (_username_key(username),),
        )
        db.commit()
        return max(0, int(cursor.rowcount))
    finally:
        db.close()


def _login_security_cte() -> str:
    return """
        WITH active_rows AS (
            SELECT username_key, ip_address, failed_count, window_started_at,
                   last_failed_at, locked_until
            FROM login_attempts
            WHERE locked_until > ? OR window_started_at >= ?
        ), grouped AS (
            SELECT username_key,
                   SUM(failed_count) AS failed_count,
                   COUNT(*) AS source_count,
                   MAX(last_failed_at) AS last_failed_at,
                   MAX(locked_until) AS locked_until
            FROM active_rows
            GROUP BY username_key
        ), enriched AS (
            SELECT grouped.*, users.id AS user_id,
                   users.username AS registered_username,
                   users.display_name, users.email, users.role, users.is_active
            FROM grouped
            LEFT JOIN users ON lower(users.username) = grouped.username_key
        )
    """


def _security_entry_payload(
    row,
    *,
    now: datetime,
    configured_admin_username: str,
    sources: list[dict[str, Any]],
) -> dict[str, Any]:
    username_key = str(row["username_key"] or "")
    registered_username = str(row["registered_username"] or "")
    config_match = bool(
        configured_admin_username
        and username_key == configured_admin_username.casefold()
    )
    if registered_username:
        username = registered_username
        account_type = "registered"
    elif config_match:
        username = configured_admin_username
        account_type = "config_admin"
    else:
        username = username_key
        account_type = "unknown"

    locked_until_value = str(row["locked_until"] or "")
    status = _status(int(row["failed_count"] or 0), locked_until_value, now)
    return {
        "username": username,
        "account_type": account_type,
        "user_id": int(row["user_id"]) if row["user_id"] is not None else None,
        "display_name": str(row["display_name"] or ""),
        "email": str(row["email"] or ""),
        "role": str(row["role"] or "") or None,
        "is_active": bool(row["is_active"]) if row["user_id"] is not None else None,
        "failed_count": status.failed_count,
        "attempts_remaining": status.attempts_remaining,
        "source_count": int(row["source_count"] or 0),
        "sources": sources,
        "last_failed_at": str(row["last_failed_at"] or ""),
        "locked": status.locked,
        "locked_until": locked_until_value if status.locked else None,
        "retry_after": status.retry_after,
    }


def get_login_security_page(
    cache: CacheManager,
    *,
    configured_admin_username: str = "",
    query: str = "",
    status: str = "",
    limit: int = 20,
    offset: int = 0,
    now: datetime | None = None,
) -> dict[str, Any]:
    """List active failure windows for administrators, including source detail."""
    current = _utc_now(now)
    current_iso = current.isoformat()
    window_cutoff = (current - FAILURE_WINDOW).isoformat()
    base_parameters: list[Any] = [current_iso, window_cutoff]
    where: list[str] = []
    filter_parameters: list[Any] = []
    normalized_query = query.strip().casefold()
    if normalized_query:
        pattern = f"%{normalized_query}%"
        where.append(
            "(lower(username_key) LIKE ? OR lower(COALESCE(registered_username, '')) LIKE ? "
            "OR lower(COALESCE(display_name, '')) LIKE ? "
            "OR lower(COALESCE(email, '')) LIKE ?)"
        )
        filter_parameters.extend([pattern, pattern, pattern, pattern])
    if status == "locked":
        where.append("locked_until > ?")
        filter_parameters.append(current_iso)
    elif status == "tracking":
        where.append("(locked_until IS NULL OR locked_until <= ?)")
        filter_parameters.append(current_iso)
    where_sql = f" WHERE {' AND '.join(where)}" if where else ""
    cte = _login_security_cte()

    db = cache.get_db()
    try:
        summary_row = db.execute(
            cte
            + """SELECT COUNT(*) AS total,
                        SUM(CASE WHEN locked_until > ? THEN 1 ELSE 0 END) AS locked,
                        SUM(CASE WHEN locked_until IS NULL OR locked_until <= ? THEN 1 ELSE 0 END)
                            AS tracking,
                        COALESCE(SUM(source_count), 0) AS sources
                 FROM enriched""",
            [*base_parameters, current_iso, current_iso],
        ).fetchone()
        total_row = db.execute(
            cte + f"SELECT COUNT(*) AS total FROM enriched{where_sql}",
            [*base_parameters, *filter_parameters],
        ).fetchone()
        rows = db.execute(
            cte
            + f"""SELECT * FROM enriched{where_sql}
                   ORDER BY CASE WHEN locked_until > ? THEN 0 ELSE 1 END,
                            last_failed_at DESC, username_key
                   LIMIT ? OFFSET ?""",
            [
                *base_parameters,
                *filter_parameters,
                current_iso,
                limit,
                offset,
            ],
        ).fetchall()

        sources_by_username: dict[str, list[dict[str, Any]]] = {}
        username_keys = [str(row["username_key"]) for row in rows]
        if username_keys:
            placeholders = ",".join("?" for _ in username_keys)
            source_rows = db.execute(
                f"""SELECT username_key, ip_address, failed_count, last_failed_at
                    FROM login_attempts
                    WHERE username_key IN ({placeholders})
                      AND (locked_until > ? OR window_started_at >= ?)
                    ORDER BY last_failed_at DESC, ip_address""",
                [*username_keys, current_iso, window_cutoff],
            ).fetchall()
            for source in source_rows:
                sources_by_username.setdefault(str(source["username_key"]), []).append({
                    "ip_address": str(source["ip_address"] or ""),
                    "failed_count": int(source["failed_count"] or 0),
                    "last_failed_at": str(source["last_failed_at"] or ""),
                })

        return {
            "items": [
                _security_entry_payload(
                    row,
                    now=current,
                    configured_admin_username=configured_admin_username,
                    sources=sources_by_username.get(str(row["username_key"]), []),
                )
                for row in rows
            ],
            "total": int(total_row["total"] if total_row else 0),
            "limit": limit,
            "offset": offset,
            "summary": {
                "total": int(summary_row["total"] if summary_row else 0),
                "locked": int(summary_row["locked"] or 0) if summary_row else 0,
                "tracking": int(summary_row["tracking"] or 0) if summary_row else 0,
                "sources": int(summary_row["sources"] or 0) if summary_row else 0,
            },
        }
    finally:
        db.close()

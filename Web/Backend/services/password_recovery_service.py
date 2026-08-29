"""Privacy-safe persistence for administrator-assisted password recovery."""

from __future__ import annotations

import math
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from typing import Any

from db_cache import CacheManager


REQUEST_COOLDOWN = timedelta(minutes=1)
REQUEST_EXPIRY = timedelta(days=7)
EXPIRATION_RESOLUTION = "expired"
RECOVERY_SOURCE_ATTEMPT_LIMIT = 10
RECOVERY_SOURCE_ATTEMPT_WINDOW = timedelta(minutes=15)
STALE_RECOVERY_RATE_LIMIT_RETENTION = timedelta(days=1)


def _utc_now(value: datetime | None = None) -> datetime:
    current = value or datetime.now(timezone.utc)
    if current.tzinfo is None:
        return current.replace(tzinfo=timezone.utc)
    return current.astimezone(timezone.utc)


def _now() -> datetime:
    return _utc_now()


def _now_iso() -> str:
    return _now().isoformat()


@dataclass(frozen=True)
class PasswordRecoverySubmission:
    matched: bool
    recorded: bool = False
    user_id: int | None = None
    username: str = ""
    request_count: int = 0


@dataclass(frozen=True)
class PasswordRecoveryRateStatus:
    allowed: bool
    retry_after: int
    attempts_remaining: int
    limit_reached: bool = False


def _parse_timestamp(value: str) -> datetime:
    try:
        return _utc_now(datetime.fromisoformat(value))
    except (TypeError, ValueError):
        return datetime.min.replace(tzinfo=timezone.utc)


def reserve_password_recovery_attempt(
    cache: CacheManager,
    ip_address: str,
    *,
    now: datetime | None = None,
) -> PasswordRecoveryRateStatus:
    """Consume one persistent per-source recovery slot before account lookup."""
    current = _utc_now(now)
    source = ip_address.strip()[:64] or "unknown"
    db = cache.get_db()
    try:
        db.execute("BEGIN IMMEDIATE")
        db.execute(
            "DELETE FROM password_recovery_rate_limits WHERE last_attempt_at < ?",
            ((current - STALE_RECOVERY_RATE_LIMIT_RETENTION).isoformat(),),
        )
        row = db.execute(
            "SELECT * FROM password_recovery_rate_limits WHERE ip_address = ?",
            (source,),
        ).fetchone()
        window_started = (
            _parse_timestamp(str(row["window_started_at"] or ""))
            if row is not None else current
        )
        attempt_count = int(row["attempt_count"] or 0) if row is not None else 0
        if current >= window_started + RECOVERY_SOURCE_ATTEMPT_WINDOW:
            attempt_count = 0
            window_started = current

        if attempt_count >= RECOVERY_SOURCE_ATTEMPT_LIMIT:
            retry_after = max(1, math.ceil(
                (window_started + RECOVERY_SOURCE_ATTEMPT_WINDOW - current).total_seconds()
            ))
            db.commit()
            return PasswordRecoveryRateStatus(
                allowed=False,
                retry_after=retry_after,
                attempts_remaining=0,
            )

        attempt_count += 1
        db.execute(
            """INSERT INTO password_recovery_rate_limits
                   (ip_address, attempt_count, window_started_at, last_attempt_at)
               VALUES (?, ?, ?, ?)
               ON CONFLICT(ip_address) DO UPDATE SET
                   attempt_count = excluded.attempt_count,
                   window_started_at = excluded.window_started_at,
                   last_attempt_at = excluded.last_attempt_at""",
            (
                source,
                attempt_count,
                window_started.isoformat(),
                current.isoformat(),
            ),
        )
        db.commit()
        return PasswordRecoveryRateStatus(
            allowed=True,
            retry_after=0,
            attempts_remaining=max(0, RECOVERY_SOURCE_ATTEMPT_LIMIT - attempt_count),
            limit_reached=attempt_count == RECOVERY_SOURCE_ATTEMPT_LIMIT,
        )
    except Exception:
        db.rollback()
        raise
    finally:
        db.close()


def _pending_payload(row) -> dict[str, Any] | None:
    if row is None:
        return None
    try:
        last_requested_at = datetime.fromisoformat(str(row["last_requested_at"]))
        if last_requested_at.tzinfo is None:
            last_requested_at = last_requested_at.replace(tzinfo=timezone.utc)
        expires_at = (last_requested_at + REQUEST_EXPIRY).isoformat()
    except (TypeError, ValueError):
        expires_at = None
    return {
        "request_count": int(row["request_count"] or 0),
        "first_requested_at": row["first_requested_at"],
        "last_requested_at": row["last_requested_at"],
        "last_ip": str(row["last_ip"] or ""),
        "expires_at": expires_at,
    }


def _expire_pending_requests(db, *, now: datetime, user_id: int | None = None) -> int:
    where_user = " AND user_id = ?" if user_id is not None else ""
    parameters: list[Any] = [
        now.isoformat(),
        EXPIRATION_RESOLUTION,
        (now - REQUEST_EXPIRY).isoformat(),
    ]
    if user_id is not None:
        parameters.append(user_id)
    cursor = db.execute(
        f"""UPDATE password_recovery_requests
            SET status = 'resolved', resolved_at = ?, resolved_by = 'system',
                resolution = ?
            WHERE status = 'pending' AND last_requested_at <= ?{where_user}""",
        parameters,
    )
    return max(0, cursor.rowcount)


def expire_password_recovery_requests(
    cache: CacheManager,
    *,
    now: datetime | None = None,
) -> int:
    """Resolve requests whose seven-day administrator-assistance window elapsed."""
    current = now or _now()
    if current.tzinfo is None:
        current = current.replace(tzinfo=timezone.utc)
    else:
        current = current.astimezone(timezone.utc)
    db = cache.get_db()
    try:
        expired = _expire_pending_requests(db, now=current)
        db.commit()
        return expired
    finally:
        db.close()


def submit_password_recovery_request(
    cache: CacheManager,
    identifier: str,
    *,
    ip_address: str,
) -> PasswordRecoverySubmission:
    """Coalesce a request for an active account without exposing match state."""
    normalized = identifier.strip().lower()
    db = cache.get_db()
    try:
        user = db.execute(
            """SELECT id, username FROM users
               WHERE is_active = 1
                 AND (lower(username) = ? OR lower(email) = ?)
               LIMIT 1""",
            (normalized, normalized),
        ).fetchone()
        if user is None:
            return PasswordRecoverySubmission(matched=False)

        db.execute("BEGIN IMMEDIATE")
        user = db.execute(
            "SELECT id, username FROM users WHERE id = ? AND is_active = 1",
            (user["id"],),
        ).fetchone()
        if user is None:
            db.commit()
            return PasswordRecoverySubmission(matched=False)

        now = _now()
        _expire_pending_requests(db, now=now, user_id=int(user["id"]))
        pending = db.execute(
            """SELECT * FROM password_recovery_requests
               WHERE user_id = ? AND status = 'pending'""",
            (user["id"],),
        ).fetchone()
        if pending is None:
            count = 1
            recorded = True
            now_text = now.isoformat()
            db.execute(
                """INSERT INTO password_recovery_requests
                       (user_id, request_count, first_requested_at,
                        last_requested_at, last_ip, status)
                   VALUES (?, 1, ?, ?, ?, 'pending')""",
                (user["id"], now_text, now_text, ip_address[:64]),
            )
        else:
            count = int(pending["request_count"] or 0)
            recorded = False
            try:
                last_requested = datetime.fromisoformat(str(pending["last_requested_at"]))
                if last_requested.tzinfo is None:
                    last_requested = last_requested.replace(tzinfo=timezone.utc)
            except (TypeError, ValueError):
                last_requested = now - REQUEST_COOLDOWN
            if now - last_requested >= REQUEST_COOLDOWN:
                count = min(999, count + 1)
                recorded = True
                db.execute(
                    """UPDATE password_recovery_requests
                       SET request_count = ?, last_requested_at = ?, last_ip = ?
                       WHERE id = ?""",
                    (count, now.isoformat(), ip_address[:64], pending["id"]),
                )
        db.commit()
        return PasswordRecoverySubmission(
            matched=True,
            recorded=recorded,
            user_id=int(user["id"]),
            username=str(user["username"]),
            request_count=count,
        )
    except Exception:
        db.rollback()
        raise
    finally:
        db.close()


def get_pending_password_recovery(
    cache: CacheManager,
    user_id: int,
) -> dict[str, Any] | None:
    db = cache.get_db()
    try:
        _expire_pending_requests(db, now=_now(), user_id=user_id)
        db.commit()
        row = db.execute(
            """SELECT request_count, first_requested_at, last_requested_at, last_ip
               FROM password_recovery_requests
               WHERE user_id = ? AND status = 'pending'""",
            (user_id,),
        ).fetchone()
        return _pending_payload(row)
    finally:
        db.close()


def resolve_password_recovery_requests(
    cache: CacheManager,
    user_id: int,
    *,
    resolved_by: str,
    resolution: str,
) -> int:
    db = cache.get_db()
    try:
        cursor = db.execute(
            """UPDATE password_recovery_requests
               SET status = 'resolved', resolved_at = ?, resolved_by = ?, resolution = ?
               WHERE user_id = ? AND status = 'pending'""",
            (_now_iso(), resolved_by[:128], resolution[:64], user_id),
        )
        db.commit()
        return max(0, cursor.rowcount)
    finally:
        db.close()

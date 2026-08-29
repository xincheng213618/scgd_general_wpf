"""Persistent public-registration velocity limits with concurrent reservations."""

from __future__ import annotations

import math
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from typing import Any

from db_cache import CacheManager


REGISTRATION_ATTEMPT_LIMIT = 20
REGISTRATION_ATTEMPT_WINDOW = timedelta(minutes=10)
REGISTRATION_SUCCESS_LIMIT = 5
REGISTRATION_SUCCESS_WINDOW = timedelta(hours=1)
STALE_REGISTRATION_RETENTION = timedelta(days=1)


@dataclass(frozen=True)
class RegistrationRateStatus:
    allowed: bool
    retry_after: int
    reason: str
    attempts_remaining: int
    successes_remaining: int
    attempt_limit_reached: bool = False
    success_limit_reached: bool = False


@dataclass(frozen=True)
class RegistrationLimitClearResult:
    ip_address: str
    cleared: bool
    pending_count: int


def _utc_now(value: datetime | None = None) -> datetime:
    current = value or datetime.now(timezone.utc)
    if current.tzinfo is None:
        return current.replace(tzinfo=timezone.utc)
    return current.astimezone(timezone.utc)


def _parse_timestamp(value: str) -> datetime:
    try:
        parsed = datetime.fromisoformat(value)
    except (TypeError, ValueError):
        return datetime.min.replace(tzinfo=timezone.utc)
    return _utc_now(parsed)


def _source_address(value: str) -> str:
    return value.strip()[:64] or "unknown"


def _normalized_counters(row, current: datetime) -> tuple[int, datetime, int, int, datetime]:
    if row is None:
        return 0, current, 0, 0, current
    attempt_started = _parse_timestamp(str(row["attempt_window_started_at"] or ""))
    success_started = _parse_timestamp(str(row["success_window_started_at"] or ""))
    attempt_count = int(row["attempt_count"] or 0)
    success_count = int(row["success_count"] or 0)
    pending_count = int(row["pending_count"] or 0)
    if current >= attempt_started + REGISTRATION_ATTEMPT_WINDOW:
        attempt_count = 0
        attempt_started = current
    if current >= success_started + REGISTRATION_SUCCESS_WINDOW:
        success_count = 0
        pending_count = 0
        success_started = current
    return attempt_count, attempt_started, success_count, pending_count, success_started


def _blocked_status(
    *,
    current: datetime,
    attempt_count: int,
    attempt_started: datetime,
    success_count: int,
    pending_count: int,
    success_started: datetime,
) -> RegistrationRateStatus:
    attempt_blocked = attempt_count >= REGISTRATION_ATTEMPT_LIMIT
    success_blocked = success_count + pending_count >= REGISTRATION_SUCCESS_LIMIT
    retry_candidates: list[float] = []
    reasons: list[str] = []
    if attempt_blocked:
        retry_candidates.append(
            (attempt_started + REGISTRATION_ATTEMPT_WINDOW - current).total_seconds()
        )
        reasons.append("attempt_velocity")
    if success_blocked:
        retry_candidates.append(
            (success_started + REGISTRATION_SUCCESS_WINDOW - current).total_seconds()
        )
        reasons.append("success_velocity")
    return RegistrationRateStatus(
        allowed=not (attempt_blocked or success_blocked),
        retry_after=max(1, math.ceil(max(retry_candidates))) if retry_candidates else 0,
        reason="+".join(reasons),
        attempts_remaining=max(0, REGISTRATION_ATTEMPT_LIMIT - attempt_count),
        successes_remaining=max(
            0,
            REGISTRATION_SUCCESS_LIMIT - success_count - pending_count,
        ),
    )


def reserve_registration_attempt(
    cache: CacheManager,
    ip_address: str,
    *,
    now: datetime | None = None,
) -> RegistrationRateStatus:
    """Reserve one signup slot before validation or password hashing."""
    current = _utc_now(now)
    current_iso = current.isoformat()
    source = _source_address(ip_address)
    db = cache.get_db()
    try:
        db.execute("BEGIN IMMEDIATE")
        db.execute(
            "DELETE FROM registration_rate_limits WHERE last_attempt_at < ?",
            ((current - STALE_REGISTRATION_RETENTION).isoformat(),),
        )
        row = db.execute(
            "SELECT * FROM registration_rate_limits WHERE ip_address = ?",
            (source,),
        ).fetchone()
        (
            attempt_count,
            attempt_started,
            success_count,
            pending_count,
            success_started,
        ) = _normalized_counters(row, current)
        existing_status = _blocked_status(
            current=current,
            attempt_count=attempt_count,
            attempt_started=attempt_started,
            success_count=success_count,
            pending_count=pending_count,
            success_started=success_started,
        )
        if not existing_status.allowed:
            db.commit()
            return existing_status

        attempt_count += 1
        pending_count += 1
        db.execute(
            """INSERT INTO registration_rate_limits
                   (ip_address, attempt_count, attempt_window_started_at,
                    success_count, pending_count, success_window_started_at,
                    last_attempt_at)
               VALUES (?, ?, ?, ?, ?, ?, ?)
               ON CONFLICT(ip_address) DO UPDATE SET
                   attempt_count = excluded.attempt_count,
                   attempt_window_started_at = excluded.attempt_window_started_at,
                   success_count = excluded.success_count,
                   pending_count = excluded.pending_count,
                   success_window_started_at = excluded.success_window_started_at,
                   last_attempt_at = excluded.last_attempt_at""",
            (
                source,
                attempt_count,
                attempt_started.isoformat(),
                success_count,
                pending_count,
                success_started.isoformat(),
                current_iso,
            ),
        )
        db.commit()
        return RegistrationRateStatus(
            allowed=True,
            retry_after=0,
            reason="",
            attempts_remaining=max(0, REGISTRATION_ATTEMPT_LIMIT - attempt_count),
            successes_remaining=max(
                0,
                REGISTRATION_SUCCESS_LIMIT - success_count - pending_count,
            ),
            attempt_limit_reached=attempt_count == REGISTRATION_ATTEMPT_LIMIT,
        )
    except Exception:
        db.rollback()
        raise
    finally:
        db.close()


def finalize_registration_attempt(
    cache: CacheManager,
    ip_address: str,
    *,
    succeeded: bool,
    now: datetime | None = None,
) -> RegistrationRateStatus:
    """Release a reservation and convert it into a successful-signup count."""
    current = _utc_now(now)
    source = _source_address(ip_address)
    db = cache.get_db()
    try:
        db.execute("BEGIN IMMEDIATE")
        row = db.execute(
            "SELECT * FROM registration_rate_limits WHERE ip_address = ?",
            (source,),
        ).fetchone()
        if row is None:
            db.commit()
            return RegistrationRateStatus(True, 0, "", 0, 0)
        (
            attempt_count,
            attempt_started,
            success_count,
            pending_count,
            success_started,
        ) = _normalized_counters(row, current)
        pending_count = max(0, pending_count - 1)
        if succeeded:
            success_count += 1
        db.execute(
            """UPDATE registration_rate_limits
               SET attempt_count = ?, attempt_window_started_at = ?,
                   success_count = ?, pending_count = ?,
                   success_window_started_at = ?
               WHERE ip_address = ?""",
            (
                attempt_count,
                attempt_started.isoformat(),
                success_count,
                pending_count,
                success_started.isoformat(),
                source,
            ),
        )
        db.commit()
        status = _blocked_status(
            current=current,
            attempt_count=attempt_count,
            attempt_started=attempt_started,
            success_count=success_count,
            pending_count=pending_count,
            success_started=success_started,
        )
        return RegistrationRateStatus(
            allowed=status.allowed,
            retry_after=status.retry_after,
            reason=status.reason,
            attempts_remaining=status.attempts_remaining,
            successes_remaining=status.successes_remaining,
            success_limit_reached=(
                succeeded and success_count == REGISTRATION_SUCCESS_LIMIT
            ),
        )
    except Exception:
        db.rollback()
        raise
    finally:
        db.close()


def _registration_security_entry(row, current: datetime) -> dict[str, Any] | None:
    (
        attempt_count,
        attempt_started,
        success_count,
        pending_count,
        success_started,
    ) = _normalized_counters(row, current)
    if attempt_count <= 0 and success_count <= 0 and pending_count <= 0:
        return None
    status = _blocked_status(
        current=current,
        attempt_count=attempt_count,
        attempt_started=attempt_started,
        success_count=success_count,
        pending_count=pending_count,
        success_started=success_started,
    )
    return {
        "ip_address": str(row["ip_address"] or ""),
        "attempt_count": attempt_count,
        "attempt_limit": REGISTRATION_ATTEMPT_LIMIT,
        "attempts_remaining": status.attempts_remaining,
        "attempt_window_expires_at": (
            (attempt_started + REGISTRATION_ATTEMPT_WINDOW).isoformat()
            if attempt_count > 0 else None
        ),
        "success_count": success_count,
        "success_limit": REGISTRATION_SUCCESS_LIMIT,
        "successes_remaining": status.successes_remaining,
        "success_window_expires_at": (
            (success_started + REGISTRATION_SUCCESS_WINDOW).isoformat()
            if success_count > 0 or pending_count > 0 else None
        ),
        "pending_count": pending_count,
        "last_attempt_at": str(row["last_attempt_at"] or ""),
        "blocked": not status.allowed,
        "reason": status.reason,
        "retry_after": status.retry_after,
        "blocked_until": (
            (current + timedelta(seconds=status.retry_after)).isoformat()
            if not status.allowed else None
        ),
    }


def get_registration_security_page(
    cache: CacheManager,
    *,
    query: str = "",
    status: str = "",
    limit: int = 20,
    offset: int = 0,
    now: datetime | None = None,
) -> dict[str, Any]:
    """List live public-registration counters for administrator diagnostics."""
    current = _utc_now(now)
    db = cache.get_db()
    try:
        db.execute("BEGIN IMMEDIATE")
        rows = db.execute(
            "SELECT * FROM registration_rate_limits ORDER BY last_attempt_at DESC, ip_address"
        ).fetchall()
        entries: list[dict[str, Any]] = []
        expired_sources: list[str] = []
        for row in rows:
            entry = _registration_security_entry(row, current)
            if entry is None:
                expired_sources.append(str(row["ip_address"]))
                continue
            entries.append(entry)
        if expired_sources:
            placeholders = ",".join("?" for _ in expired_sources)
            db.execute(
                f"DELETE FROM registration_rate_limits WHERE ip_address IN ({placeholders})",
                expired_sources,
            )
        db.commit()
    except Exception:
        db.rollback()
        raise
    finally:
        db.close()

    entries.sort(key=lambda item: item["last_attempt_at"], reverse=True)
    entries.sort(key=lambda item: item["blocked"], reverse=True)
    summary = {
        "total": len(entries),
        "blocked": sum(1 for item in entries if item["blocked"]),
        "tracking": sum(1 for item in entries if not item["blocked"]),
        "pending": sum(int(item["pending_count"]) for item in entries),
    }
    normalized_query = query.strip().casefold()
    filtered = [
        item for item in entries
        if not normalized_query or normalized_query in item["ip_address"].casefold()
    ]
    if status == "blocked":
        filtered = [item for item in filtered if item["blocked"]]
    elif status == "tracking":
        filtered = [item for item in filtered if not item["blocked"]]
    return {
        "items": filtered[offset:offset + limit],
        "total": len(filtered),
        "limit": limit,
        "offset": offset,
        "summary": summary,
    }


def clear_registration_rate_limit(
    cache: CacheManager,
    ip_address: str,
    *,
    now: datetime | None = None,
) -> RegistrationLimitClearResult:
    """Clear one source's counters without discarding in-flight reservations."""
    current = _utc_now(now)
    source = _source_address(ip_address)
    db = cache.get_db()
    try:
        db.execute("BEGIN IMMEDIATE")
        row = db.execute(
            "SELECT pending_count FROM registration_rate_limits WHERE ip_address = ?",
            (source,),
        ).fetchone()
        if row is None:
            db.commit()
            return RegistrationLimitClearResult(source, False, 0)
        pending_count = max(0, int(row["pending_count"] or 0))
        if pending_count > 0:
            current_iso = current.isoformat()
            db.execute(
                """UPDATE registration_rate_limits
                   SET attempt_count = 0, attempt_window_started_at = ?,
                       success_count = 0, success_window_started_at = ?,
                       last_attempt_at = ?
                   WHERE ip_address = ?""",
                (current_iso, current_iso, current_iso, source),
            )
        else:
            db.execute(
                "DELETE FROM registration_rate_limits WHERE ip_address = ?",
                (source,),
            )
        db.commit()
        return RegistrationLimitClearResult(source, True, pending_count)
    except Exception:
        db.rollback()
        raise
    finally:
        db.close()

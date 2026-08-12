"""Bounded process-local performance diagnostics for the admin portal."""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any


DEFAULT_SLOW_REQUEST_THRESHOLD_MS = 500
SLOW_REQUEST_BUFFER_CAPACITY = 100
PERFORMANCE_SAMPLE_LIMIT = 20


def record_slow_request(
    buffer: list[dict[str, Any]],
    *,
    method: str,
    path: str,
    status: int,
    duration_ms: int,
    recorded_at: datetime | None = None,
    capacity: int = SLOW_REQUEST_BUFFER_CAPACITY,
) -> dict[str, Any]:
    """Append a sanitized sample while keeping the process buffer bounded."""
    if capacity < 1:
        raise ValueError("capacity must be at least 1")
    current = recorded_at or datetime.now(timezone.utc)
    if current.tzinfo is None:
        current = current.replace(tzinfo=timezone.utc)
    sample = {
        "recorded_at": current.astimezone(timezone.utc).isoformat(),
        "method": str(method or "GET").upper()[:16],
        "path": str(path or "/")[:2048],
        "status": max(100, min(int(status), 599)),
        "duration_ms": max(0, int(duration_ms)),
    }
    buffer.append(sample)
    overflow = len(buffer) - capacity
    if overflow > 0:
        del buffer[:overflow]
    return sample


def build_performance_summary(
    *,
    slow_requests: list[dict[str, Any]],
    recent_job_runs: list[dict[str, Any]],
    threshold_ms: int = DEFAULT_SLOW_REQUEST_THRESHOLD_MS,
    buffer_capacity: int = SLOW_REQUEST_BUFFER_CAPACITY,
    process_started_at: datetime | None = None,
    generated_at: datetime | None = None,
    sample_limit: int = PERFORMANCE_SAMPLE_LIMIT,
) -> dict[str, Any]:
    """Shape the existing request buffer and job history for the admin UI."""
    if sample_limit < 1:
        raise ValueError("sample_limit must be at least 1")
    current = generated_at or datetime.now(timezone.utc)
    if current.tzinfo is None:
        current = current.replace(tzinfo=timezone.utc)
    started = process_started_at or current
    if started.tzinfo is None:
        started = started.replace(tzinfo=timezone.utc)
    request_samples = [dict(item) for item in slow_requests[-sample_limit:]]
    slow_jobs = [
        dict(run)
        for run in recent_job_runs[:sample_limit]
        if int(run.get("duration_ms") or 0) >= 1000 or run.get("status") == "error"
    ]
    return {
        "generated_at": current.astimezone(timezone.utc).isoformat(),
        "process_started_at": started.astimezone(timezone.utc).isoformat(),
        "threshold_ms": max(0, int(threshold_ms)),
        "request_buffer_count": len(slow_requests),
        "request_buffer_capacity": max(1, int(buffer_capacity)),
        "slow_requests": request_samples,
        "slow_jobs": slow_jobs,
    }

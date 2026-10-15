"""Bounded process-local performance diagnostics for the admin portal."""

from __future__ import annotations

from datetime import datetime, timezone
from collections import deque
import math
import threading
import time
from typing import Any


DEFAULT_SLOW_REQUEST_THRESHOLD_MS = 500
SLOW_REQUEST_BUFFER_CAPACITY = 100
PERFORMANCE_SAMPLE_LIMIT = 20


def request_category(path: str, *, method: str = "GET", attachment: bool = False) -> str:
    """Separate file delivery from page/API work, including failed transfers."""
    transfer_file = path.startswith("/api/transfer/files/") and method in {"GET", "HEAD", "PUT", "POST"}
    transfer_chunk = path.startswith("/api/transfer/uploads/") and method == "PATCH"
    if (attachment or transfer_file or transfer_chunk
            or path.startswith(("/upload/", "/download/", "/api/packages/"))
            or path.endswith("/download") or "/attachments/" in path):
        return "transfer"
    return "application"


class RequestDiagnostics:
    """Five-minute, per-second counters; no unbounded raw request history."""
    window_seconds = 300
    duration_bounds = (10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 15000)

    def __init__(self, clock=time.monotonic):
        self._clock = clock
        self._started = clock()
        self._lock = threading.Lock()
        self._buckets = deque()
        self._slow = {name: deque(maxlen=SLOW_REQUEST_BUFFER_CAPACITY) for name in ("application", "transfer")}

    def _prune(self, now):
        while self._buckets and self._buckets[0][0] <= now - self.window_seconds:
            self._buckets.popleft()

    def record(self, *, category, status, duration_ms, slow_sample=None):
        with self._lock:
            now = int(self._clock())
            self._prune(now)
            if not self._buckets or self._buckets[-1][0] != now:
                self._buckets.append((now, {}))
            counts = self._buckets[-1][1].setdefault(category, [0, 0, 0, 0, *([0] * (len(self.duration_bounds) + 1))])
            counts[0] += 1
            counts[1] += int(400 <= status < 500)
            counts[2] += int(status >= 500)
            counts[3] += duration_ms
            bucket = next((i for i, upper in enumerate(self.duration_bounds) if duration_ms <= upper), len(self.duration_bounds))
            counts[4 + bucket] += 1
            if slow_sample is not None:
                self._slow[category].append({**slow_sample, "category": category})

    def summary(self):
        with self._lock:
            now = int(self._clock())
            self._prune(now)
            groups = {}
            for category in self._slow:
                totals = [0] * (len(self.duration_bounds) + 5)
                for _, bucket in self._buckets:
                    for i, value in enumerate(bucket.get(category, [])):
                        totals[i] += value
                target, accumulated, p95 = math.ceil(totals[0] * .95), 0, None
                if target:
                    for upper, count in zip((*self.duration_bounds, None), totals[4:]):
                        accumulated += count
                        if accumulated >= target:
                            p95 = upper
                            break
                groups[category] = {
                    "requests": totals[0], "client_errors": totals[1], "server_errors": totals[2],
                    "avg_ms": round(totals[3] / totals[0], 2) if totals[0] else None,
                    "p95_upper_ms": p95,
                    "p95_overflow": bool(target and p95 is None),
                    "slow_requests": [dict(item) for item in list(self._slow[category])[-PERFORMANCE_SAMPLE_LIMIT:]],
                }
            return {"window_seconds": self.window_seconds,
                    "observed_seconds": min(self.window_seconds, max(0, now - int(self._started))),
                    "groups": groups}


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

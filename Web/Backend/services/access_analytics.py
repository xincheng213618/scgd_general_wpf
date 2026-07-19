"""Privacy-friendly access analytics for the ColorVision Web service.

Only aggregate counters and a daily, keyed visitor identifier are persisted.
Raw addresses, query strings, user-agent strings, and referrer paths never enter
the event or database boundary.
"""

from __future__ import annotations

import hashlib
import hmac
import queue
import re
import sqlite3
import threading
import time
from collections import defaultdict
from dataclasses import dataclass
from datetime import date, datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Callable, Protocol, Sequence


CLIENT_TYPES = frozenset({"desktop", "mobile", "tablet", "bot", "other"})
ACCESS_ANALYTICS_TABLES = (
    "access_visitor_daily",
    "access_client_daily",
    "access_route_daily",
    "access_daily",
)
BACKUP_NAME_PATTERN = re.compile(r"marketplace_backup_\d{8}_\d{6}\.db")
EXCLUDED_ROUTES = frozenset({
    "/api/health",
    "/api/ready",
    "/api/stats",
})
EXCLUDED_ROUTE_PREFIXES = (
    "/api/admin/stats/",
    "/api/admin/perf/",
    "/api/v1/admin/analytics/",
    "/assets/",
    "/media/",
    "/brand/",
    "/favicon",
)


@dataclass(frozen=True, slots=True)
class AccessEvent:
    """Sanitized request-completion event accepted by the persistence sink."""

    occurred_at: str
    day: str
    route: str
    method: str
    status_code: int
    duration_ms: int
    response_bytes: int
    client_type: str
    visitor_key: str | None


class AccessEventSink(Protocol):
    """Stable write boundary used by request instrumentation."""

    def submit(
        self,
        event: AccessEvent,
        *,
        db_path: Path,
        synchronous: bool = False,
    ) -> bool: ...

    def status(self) -> dict[str, Any]: ...


class AccessTrafficQuery(Protocol):
    """Stable read boundary used by the admin API."""

    def get_traffic(self, *, days: int, limit: int) -> dict[str, Any]: ...


def normalize_route_template(route_template: str | None) -> str:
    """Normalize a Flask route rule without accepting a raw request path."""
    text = str(route_template or "").strip()
    if not text:
        return "__unmatched__"
    if not text.startswith("/"):
        text = f"/{text}"
    if len(text) > 256:
        return "__oversized_route__"
    if text != "/":
        text = text.rstrip("/")
    return text


def should_record_access(route_template: str | None, method: str) -> bool:
    route = normalize_route_template(route_template)
    if str(method or "").upper() == "OPTIONS":
        return False
    if route in EXCLUDED_ROUTES:
        return False
    return not any(route.startswith(prefix) for prefix in EXCLUDED_ROUTE_PREFIXES)


def classify_user_agent(user_agent: str | None) -> str:
    """Reduce a user-agent string to a deliberately coarse device class."""
    value = str(user_agent or "").lower()
    if not value:
        return "other"
    if any(token in value for token in ("bot", "spider", "crawler", "slurp", "bingpreview")):
        return "bot"
    if any(token in value for token in ("ipad", "tablet", "kindle", "silk/")):
        return "tablet"
    if "android" in value and "mobile" not in value:
        return "tablet"
    if any(token in value for token in ("mobile", "iphone", "ipod", "windows phone", "android")):
        return "mobile"
    if any(token in value for token in ("windows nt", "macintosh", "x11", "linux")):
        return "desktop"
    return "other"


def daily_visitor_key(*, secret_key: str, day: str, remote_addr: str | None) -> str | None:
    """Build a daily unlinkable visitor key; the address is never returned."""
    address = str(remote_addr or "").strip()
    if not address:
        return None
    secret = str(secret_key or "").encode("utf-8")
    message = f"colorvision-access-v1\0{day}\0{address}".encode("utf-8")
    return hmac.new(secret, message, hashlib.sha256).hexdigest()[:24]


def build_access_event(
    *,
    route_template: str | None,
    method: str,
    status_code: int,
    duration_ms: int,
    response_bytes: int = 0,
    secret_key: str,
    remote_addr: str | None,
    user_agent: str | None,
    occurred_at: datetime | None = None,
) -> AccessEvent:
    now = occurred_at or datetime.now(timezone.utc)
    if now.tzinfo is None:
        now = now.replace(tzinfo=timezone.utc)
    now = now.astimezone(timezone.utc)
    day = now.date().isoformat()
    client_type = classify_user_agent(user_agent)
    return AccessEvent(
        occurred_at=now.isoformat(),
        day=day,
        route=normalize_route_template(route_template),
        method=str(method or "GET").upper()[:16],
        status_code=max(100, min(int(status_code), 599)),
        duration_ms=max(0, min(int(duration_ms), 86_400_000)),
        response_bytes=max(0, min(int(response_bytes), 1 << 50)),
        client_type=client_type if client_type in CLIENT_TYPES else "other",
        visitor_key=daily_visitor_key(
            secret_key=secret_key,
            day=day,
            remote_addr=remote_addr,
        ),
    )


@dataclass(frozen=True, slots=True)
class _QueuedAccessEvent:
    db_path: str
    event: AccessEvent


class AccessAnalyticsRecorder:
    """Bounded, non-blocking producer with grouped SQLite batch writes."""

    def __init__(
        self,
        *,
        queue_capacity: int = 4096,
        batch_size: int = 128,
        flush_interval_seconds: float = 0.5,
        background_worker: bool = True,
    ):
        self._capacity = max(1, int(queue_capacity))
        self._batch_size = max(1, int(batch_size))
        self._flush_interval = max(0.05, float(flush_interval_seconds))
        self._queue: queue.Queue[_QueuedAccessEvent] = queue.Queue(maxsize=self._capacity)
        self._background_worker = background_worker
        self._worker: threading.Thread | None = None
        self._worker_lock = threading.Lock()
        self._status_lock = threading.Lock()
        self._stop_event = threading.Event()
        self._dropped = 0
        self._pending = 0
        self._last_error: str | None = None
        self._last_flush_at: str | None = None

    def submit(
        self,
        event: AccessEvent,
        *,
        db_path: Path,
        synchronous: bool = False,
    ) -> bool:
        item = _QueuedAccessEvent(str(Path(db_path)), event)
        if synchronous:
            return self._write_group(item.db_path, [event])

        if self._background_worker:
            self._ensure_worker()
        try:
            # Keep the status lock through enqueue so the worker cannot finish
            # and decrement the item before pending has been incremented.
            with self._status_lock:
                self._queue.put_nowait(item)
                self._pending += 1
            return True
        except queue.Full:
            self._record_drop(1, "access analytics queue is full")
            return False

    def status(self) -> dict[str, Any]:
        with self._status_lock:
            return {
                "pending": self._pending,
                "dropped": self._dropped,
                "lastError": self._last_error,
                "lastFlushAt": self._last_flush_at,
                "capacity": self._capacity,
            }

    def flush(self, timeout_seconds: float = 5.0) -> bool:
        deadline = time.monotonic() + max(0.0, timeout_seconds)
        while time.monotonic() < deadline:
            if self.status()["pending"] == 0:
                return True
            time.sleep(0.01)
        return self.status()["pending"] == 0

    def close(self, timeout_seconds: float = 2.0):
        self._stop_event.set()
        worker = self._worker
        if worker is not None and worker.is_alive():
            worker.join(timeout=max(0.0, timeout_seconds))

    def _ensure_worker(self):
        with self._worker_lock:
            if self._worker is not None and self._worker.is_alive():
                return
            self._stop_event.clear()
            self._worker = threading.Thread(
                target=self._run,
                daemon=True,
                name="access-analytics-writer",
            )
            self._worker.start()

    def _run(self):
        while not self._stop_event.is_set() or not self._queue.empty():
            try:
                first = self._queue.get(timeout=self._flush_interval)
            except queue.Empty:
                continue

            items = [first]
            while len(items) < self._batch_size:
                try:
                    items.append(self._queue.get_nowait())
                except queue.Empty:
                    break

            grouped: dict[str, list[AccessEvent]] = defaultdict(list)
            for item in items:
                grouped[item.db_path].append(item.event)

            try:
                for db_path, events in grouped.items():
                    self._write_group(db_path, events)
            finally:
                with self._status_lock:
                    self._pending = max(0, self._pending - len(items))
                for _ in items:
                    self._queue.task_done()

    def _write_group(self, db_path: str, events: Sequence[AccessEvent]) -> bool:
        try:
            _write_access_batch(Path(db_path), events)
        except Exception as exc:
            self._record_drop(len(events), str(exc))
            return False
        with self._status_lock:
            self._last_error = None
            self._last_flush_at = datetime.now(timezone.utc).isoformat()
        return True

    def _record_drop(self, count: int, error: str):
        with self._status_lock:
            self._dropped += max(0, count)
            self._last_error = str(error)[:500]


def _write_access_batch(db_path: Path, events: Sequence[AccessEvent]):
    if not events:
        return
    db = sqlite3.connect(str(db_path), timeout=15)
    db.row_factory = sqlite3.Row
    try:
        db.execute("PRAGMA journal_mode=WAL")
        db.execute("PRAGMA busy_timeout=5000")
        from db.schema_version import ensure_schema_version

        ensure_schema_version(db)
        with db:
            for event in events:
                _write_access_event(db, event)
    finally:
        db.close()


def _write_access_event(db: sqlite3.Connection, event: AccessEvent):
    date.fromisoformat(event.day)
    error_count = 1 if event.status_code >= 400 else 0
    new_visitor = 0
    if event.visitor_key:
        cursor = db.execute(
            """
            INSERT OR IGNORE INTO access_visitor_daily
                (day, visitor_key, client_type, visits, first_seen_at, last_seen_at)
            VALUES (?, ?, ?, 0, ?, ?)
            """,
            (
                event.day,
                event.visitor_key,
                event.client_type,
                event.occurred_at,
                event.occurred_at,
            ),
        )
        new_visitor = 1 if cursor.rowcount == 1 else 0
        db.execute(
            """
            UPDATE access_visitor_daily
            SET visits = visits + 1, last_seen_at = ?
            WHERE day = ? AND visitor_key = ?
            """,
            (event.occurred_at, event.day, event.visitor_key),
        )

    db.execute(
        """
        INSERT INTO access_daily
            (day, visits, unique_visitors, error_responses, total_duration_ms,
             max_duration_ms, total_response_bytes, updated_at)
        VALUES (?, 1, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(day) DO UPDATE SET
            visits = visits + 1,
            unique_visitors = unique_visitors + excluded.unique_visitors,
            error_responses = error_responses + excluded.error_responses,
            total_duration_ms = total_duration_ms + excluded.total_duration_ms,
            max_duration_ms = max(max_duration_ms, excluded.max_duration_ms),
            total_response_bytes = total_response_bytes + excluded.total_response_bytes,
            updated_at = excluded.updated_at
        """,
        (
            event.day,
            new_visitor,
            error_count,
            event.duration_ms,
            event.duration_ms,
            event.response_bytes,
            event.occurred_at,
        ),
    )
    db.execute(
        """
        INSERT INTO access_route_daily
            (day, route, method, visits, error_responses, total_duration_ms,
             max_duration_ms, total_response_bytes, updated_at)
        VALUES (?, ?, ?, 1, ?, ?, ?, ?, ?)
        ON CONFLICT(day, route, method) DO UPDATE SET
            visits = visits + 1,
            error_responses = error_responses + excluded.error_responses,
            total_duration_ms = total_duration_ms + excluded.total_duration_ms,
            max_duration_ms = max(max_duration_ms, excluded.max_duration_ms),
            total_response_bytes = total_response_bytes + excluded.total_response_bytes,
            updated_at = excluded.updated_at
        """,
        (
            event.day,
            event.route,
            event.method,
            error_count,
            event.duration_ms,
            event.duration_ms,
            event.response_bytes,
            event.occurred_at,
        ),
    )
    db.execute(
        """
        INSERT INTO access_client_daily
            (day, client_type, visits, unique_visitors, error_responses,
             total_duration_ms, updated_at)
        VALUES (?, ?, 1, ?, ?, ?, ?)
        ON CONFLICT(day, client_type) DO UPDATE SET
            visits = visits + 1,
            unique_visitors = unique_visitors + excluded.unique_visitors,
            error_responses = error_responses + excluded.error_responses,
            total_duration_ms = total_duration_ms + excluded.total_duration_ms,
            updated_at = excluded.updated_at
        """,
        (
            event.day,
            event.client_type,
            new_visitor,
            error_count,
            event.duration_ms,
            event.occurred_at,
        ),
    )


class SqliteAccessTrafficQuery:
    def __init__(
        self,
        db_factory: Callable[[], Any],
        *,
        recorder_status: Callable[[], dict[str, Any]] | None = None,
        today_getter: Callable[[], date] | None = None,
    ):
        self._db_factory = db_factory
        self._recorder_status = recorder_status or _empty_recorder_status
        self._today_getter = today_getter or (lambda: datetime.now(timezone.utc).date())

    def get_traffic(self, *, days: int, limit: int) -> dict[str, Any]:
        validate_query_range(days=days, limit=limit)
        today = self._today_getter()
        start = today - timedelta(days=days - 1)
        db = self._db_factory()
        try:
            daily_rows = db.execute(
                """
                SELECT day, visits, unique_visitors, error_responses,
                       total_duration_ms, max_duration_ms, total_response_bytes
                FROM access_daily
                WHERE day BETWEEN ? AND ?
                ORDER BY day
                """,
                (start.isoformat(), today.isoformat()),
            ).fetchall()
            route_rows = db.execute(
                """
                SELECT route, method, SUM(visits) AS visits,
                       SUM(error_responses) AS error_responses,
                       SUM(total_duration_ms) AS total_duration_ms,
                       MAX(max_duration_ms) AS max_duration_ms,
                       SUM(total_response_bytes) AS total_response_bytes
                FROM access_route_daily
                WHERE day BETWEEN ? AND ?
                GROUP BY route, method
                ORDER BY visits DESC, route, method
                LIMIT ?
                """,
                (start.isoformat(), today.isoformat(), limit),
            ).fetchall()
            client_rows = db.execute(
                """
                SELECT client_type, SUM(visits) AS visits,
                       SUM(unique_visitors) AS unique_visitors,
                       SUM(error_responses) AS error_responses,
                       SUM(total_duration_ms) AS total_duration_ms
                FROM access_client_daily
                WHERE day BETWEEN ? AND ?
                GROUP BY client_type
                ORDER BY visits DESC, client_type
                """,
                (start.isoformat(), today.isoformat()),
            ).fetchall()
        finally:
            db.close()

        by_day = {row["day"]: _daily_payload(row) for row in daily_rows}
        daily = []
        for offset in range(days):
            day_text = (start + timedelta(days=offset)).isoformat()
            daily.append(by_day.get(day_text, _zero_daily(day_text)))

        visits = sum(item["visits"] for item in daily)
        unique_visitor_days = sum(item["uniqueVisitors"] for item in daily)
        errors = sum(item["errorResponses"] for item in daily)
        duration = sum(item["totalDurationMs"] for item in daily)
        response_bytes = sum(item["totalResponseBytes"] for item in daily)

        top_routes = []
        for row in route_rows:
            route_visits = int(row["visits"] or 0)
            route_errors = int(row["error_responses"] or 0)
            route_duration = int(row["total_duration_ms"] or 0)
            top_routes.append({
                "route": row["route"],
                "method": row["method"],
                "visits": route_visits,
                "errorResponses": route_errors,
                "errorRate": _percentage(route_errors, route_visits),
                "avgResponseMs": _average(route_duration, route_visits),
                "maxResponseMs": int(row["max_duration_ms"] or 0),
                "responseBytes": int(row["total_response_bytes"] or 0),
            })

        clients = []
        for row in client_rows:
            client_visits = int(row["visits"] or 0)
            client_errors = int(row["error_responses"] or 0)
            client_visitor_days = int(row["unique_visitors"] or 0)
            clients.append({
                "client": row["client_type"],
                "visits": client_visits,
                "uniqueVisitorDays": client_visitor_days,
                "share": _percentage(client_visits, visits),
                "errorResponses": client_errors,
                "avgResponseMs": _average(int(row["total_duration_ms"] or 0), client_visits),
            })

        return {
            "summary": {
                "periodStart": start.isoformat(),
                "periodEnd": today.isoformat(),
                "days": days,
                "visits": visits,
                # Daily HMAC identifiers intentionally rotate, so a multi-day
                # total is visitor-days rather than a cross-day unique count.
                "uniqueVisitorDays": unique_visitor_days,
                "avgResponseMs": _average(duration, visits),
                "errorResponses": errors,
                "errorRate": _percentage(errors, visits),
                "totalResponseBytes": response_bytes,
            },
            "today": daily[-1],
            "daily": daily,
            "topRoutes": top_routes,
            "clients": clients,
            "recorder": self._recorder_status(),
        }


def get_today_access_summary(db: Any, *, today: date | None = None) -> dict[str, Any]:
    day = today or datetime.now(timezone.utc).date()
    try:
        row = db.execute(
            """
            SELECT visits, unique_visitors, error_responses, total_duration_ms
            FROM access_daily WHERE day = ?
            """,
            (day.isoformat(),),
        ).fetchone()
    except sqlite3.OperationalError as exc:
        if "no such table" not in str(exc).lower():
            raise
        row = None
    if row is None:
        return {
            "visitsToday": 0,
            "uniqueVisitorsToday": 0,
            "avgResponseMsToday": 0.0,
            "errorResponsesToday": 0,
        }
    visits = int(row["visits"] or 0)
    return {
        "visitsToday": visits,
        "uniqueVisitorsToday": int(row["unique_visitors"] or 0),
        "avgResponseMsToday": _average(int(row["total_duration_ms"] or 0), visits),
        "errorResponsesToday": int(row["error_responses"] or 0),
    }


def validate_query_range(*, days: int, limit: int):
    if not 1 <= int(days) <= 365:
        raise ValueError("days must be between 1 and 365")
    if not 1 <= int(limit) <= 100:
        raise ValueError("limit must be between 1 and 100")


def parse_bounded_int(
    raw_value: Any,
    *,
    name: str,
    default: int,
    minimum: int,
    maximum: int,
) -> int:
    if raw_value is None or str(raw_value).strip() == "":
        return default
    try:
        value = int(str(raw_value).strip())
    except (TypeError, ValueError) as exc:
        raise ValueError(f"{name} must be an integer") from exc
    if not minimum <= value <= maximum:
        raise ValueError(f"{name} must be between {minimum} and {maximum}")
    return value


def prune_access_analytics(
    db_factory: Callable[[], Any],
    *,
    retention_days: int,
    today: date | None = None,
) -> dict[str, Any]:
    if not 1 <= int(retention_days) <= 3650:
        raise ValueError("retention_days must be between 1 and 3650")
    current_day = today or datetime.now(timezone.utc).date()
    cutoff = current_day - timedelta(days=retention_days - 1)
    db = db_factory()
    try:
        deleted_by_table = _delete_access_rows_before(db, cutoff.isoformat())
    finally:
        db.close()
    return {
        "cutoffDay": cutoff.isoformat(),
        "deleted": sum(deleted_by_table.values()),
        "tables": deleted_by_table,
    }


def prune_access_analytics_database(
    db_path: Path,
    *,
    retention_days: int,
    today: date | None = None,
) -> dict[str, Any]:
    """Apply visitor retention to a SQLite snapshot and verify its integrity."""
    if not 1 <= int(retention_days) <= 3650:
        raise ValueError("retention_days must be between 1 and 3650")
    current_day = today or datetime.now(timezone.utc).date()
    cutoff = current_day - timedelta(days=retention_days - 1)
    db = sqlite3.connect(str(db_path), timeout=15)
    try:
        deleted_by_table = _delete_access_rows_before(db, cutoff.isoformat())
        check = db.execute("PRAGMA quick_check").fetchone()
        if not check or str(check[0]).lower() != "ok":
            raise sqlite3.DatabaseError(
                f"backup integrity check failed: {check[0] if check else 'no result'}"
            )
    finally:
        db.close()
    return {
        "path": str(db_path),
        "cutoffDay": cutoff.isoformat(),
        "deleted": sum(deleted_by_table.values()),
        "tables": deleted_by_table,
    }


def prune_access_analytics_backups(
    directory: Path,
    *,
    retention_days: int,
    today: date | None = None,
) -> dict[str, Any]:
    """Scrub expired access rows from recognized marketplace DB backups."""
    root = Path(directory).resolve()
    results: list[dict[str, Any]] = []
    errors: list[str] = []
    if not root.is_dir():
        return {"backups": 0, "deleted": 0, "results": [], "errors": []}

    for path in sorted(root.glob("marketplace_backup_*.db")):
        if not BACKUP_NAME_PATTERN.fullmatch(path.name) or path.resolve().parent != root:
            continue
        try:
            results.append(prune_access_analytics_database(
                path,
                retention_days=retention_days,
                today=today,
            ))
        except Exception as exc:
            errors.append(f"{path.name}: {exc}")
    return {
        "backups": len(results),
        "deleted": sum(int(item["deleted"]) for item in results),
        "results": results,
        "errors": errors,
    }


def _delete_access_rows_before(db: sqlite3.Connection, cutoff_day: str) -> dict[str, int]:
    existing_tables = {
        str(row[0])
        for row in db.execute(
            "SELECT name FROM sqlite_master WHERE type = 'table'"
        ).fetchall()
    }
    deleted_by_table: dict[str, int] = {}
    with db:
        for table in ACCESS_ANALYTICS_TABLES:
            if table not in existing_tables:
                deleted_by_table[table] = 0
                continue
            cursor = db.execute(f"DELETE FROM {table} WHERE day < ?", (cutoff_day,))
            deleted_by_table[table] = max(0, cursor.rowcount)
    return deleted_by_table


def _daily_payload(row: Any) -> dict[str, Any]:
    visits = int(row["visits"] or 0)
    duration = int(row["total_duration_ms"] or 0)
    errors = int(row["error_responses"] or 0)
    return {
        "day": row["day"],
        "visits": visits,
        "uniqueVisitors": int(row["unique_visitors"] or 0),
        "avgResponseMs": _average(duration, visits),
        "maxResponseMs": int(row["max_duration_ms"] or 0),
        "errorResponses": errors,
        "errorRate": _percentage(errors, visits),
        "totalDurationMs": duration,
        "totalResponseBytes": int(row["total_response_bytes"] or 0),
    }


def _zero_daily(day: str) -> dict[str, Any]:
    return {
        "day": day,
        "visits": 0,
        "uniqueVisitors": 0,
        "avgResponseMs": 0.0,
        "maxResponseMs": 0,
        "errorResponses": 0,
        "errorRate": 0.0,
        "totalDurationMs": 0,
        "totalResponseBytes": 0,
    }


def _average(total: int, count: int) -> float:
    return round(total / count, 2) if count else 0.0


def _percentage(part: int, total: int) -> float:
    return round(part * 100 / total, 2) if total else 0.0


def _empty_recorder_status() -> dict[str, Any]:
    return {"pending": 0, "dropped": 0, "lastError": None}

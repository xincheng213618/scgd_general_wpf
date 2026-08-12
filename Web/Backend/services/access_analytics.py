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
    "web_page_visitor_daily",
    "web_vital_daily",
    "web_page_daily",
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
    "/api/v1/analytics/",
    "/assets/",
    "/media/",
    "/brand/",
    "/favicon",
)
NO_RESPONSE_BODY_STATUS_CODES = frozenset({204, 205, 304})
DEFAULT_REPORTING_UTC_OFFSET_MINUTES = 8 * 60
MIN_REPORTING_UTC_OFFSET_MINUTES = -12 * 60
MAX_REPORTING_UTC_OFFSET_MINUTES = 14 * 60
CALENDAR_OFFSET_KEY = "calendar_utc_offset_minutes"
CALENDAR_EFFECTIVE_AT_KEY = "calendar_boundary_effective_at"
LEGACY_CALENDAR_THROUGH_DAY_KEY = "legacy_calendar_data_through_day"
WEB_NAVIGATION_TYPES = frozenset({"hard", "spa"})
WEB_VITAL_METRICS = ("lcp", "cls", "inp")
WEB_ROUTE_EXACT = frozenset({
    "/",
    "/plugins",
    "/releases",
    "/changelog",
    "/updates",
    "/tools",
    "/browse",
    "/transfer",
    "/login",
    "/admin",
    "/admin/publish",
    "/admin/files",
    "/admin/cache",
    "/admin/jobs",
    "/admin/deployments",
    "/admin/feedback",
    "/admin/users",
    "/admin/api-keys",
    "/admin/copilot",
    "/admin/audit",
    "/admin/traffic",
    "/admin/settings",
})
WEB_PLUGIN_ROUTE = re.compile(r"^/plugins/[A-Za-z0-9._-]{1,128}$")


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


@dataclass(frozen=True, slots=True)
class WebPageViewEvent:
    """Sanitized SPA navigation event; no URL query or referrer is retained."""

    occurred_at: str
    day: str
    route: str
    navigation_type: str
    client_type: str
    visitor_key: str | None


@dataclass(frozen=True, slots=True)
class WebVitalEvent:
    """One bounded Core Web Vital sample for a normalized SPA route."""

    occurred_at: str
    day: str
    route: str
    metric: str
    value: float
    rating: str


AnalyticsEvent = AccessEvent | WebPageViewEvent | WebVitalEvent


class AccessEventSink(Protocol):
    """Stable write boundary used by request instrumentation."""

    def submit(
        self,
        event: AnalyticsEvent,
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


def normalize_web_route(raw_route: Any) -> str:
    """Map a browser pathname to a fixed route template without query data."""
    route = str(raw_route or "").strip()
    if not route or len(route) > 256 or "?" in route or "#" in route:
        raise ValueError("invalid web route")
    if not route.startswith("/") or "\\" in route or "//" in route:
        raise ValueError("invalid web route")
    if route != "/":
        route = route.rstrip("/")
    if route in WEB_ROUTE_EXACT:
        return route
    if WEB_PLUGIN_ROUTE.fullmatch(route):
        return "/plugins/:pluginId"
    if route.startswith("/browse/"):
        return "/browse/*"
    raise ValueError("unsupported web route")


def _web_vital_rating(metric: str, value: float) -> str:
    thresholds = {
        "lcp": (2500.0, 4000.0),
        "cls": (0.1, 0.25),
        "inp": (200.0, 500.0),
    }
    good, poor = thresholds[metric]
    if value <= good:
        return "good"
    if value <= poor:
        return "needs_improvement"
    return "poor"


def build_web_experience_event(
    payload: Any,
    *,
    secret_key: str,
    remote_addr: str | None,
    user_agent: str | None,
    occurred_at: datetime | None = None,
    utc_offset_minutes: int = DEFAULT_REPORTING_UTC_OFFSET_MINUTES,
) -> WebPageViewEvent | WebVitalEvent | None:
    """Validate the exact public ingestion contract and return a safe event."""
    if not isinstance(payload, dict):
        raise ValueError("analytics payload must be an object")
    kind = str(payload.get("kind") or "").strip()
    route = normalize_web_route(payload.get("route"))
    now = occurred_at or datetime.now(timezone.utc)
    if now.tzinfo is None:
        now = now.replace(tzinfo=timezone.utc)
    now = now.astimezone(timezone.utc)
    day = analytics_calendar_day(
        now,
        utc_offset_minutes=utc_offset_minutes,
    ).isoformat()
    client_type = classify_user_agent(user_agent)
    if client_type == "bot":
        return None

    if kind == "page_view":
        if set(payload) != {"kind", "route", "navigationType"}:
            raise ValueError("invalid page view payload")
        navigation_type = str(payload.get("navigationType") or "").strip()
        if navigation_type not in WEB_NAVIGATION_TYPES:
            raise ValueError("invalid navigation type")
        return WebPageViewEvent(
            occurred_at=now.isoformat(),
            day=day,
            route=route,
            navigation_type=navigation_type,
            client_type=client_type if client_type in CLIENT_TYPES else "other",
            visitor_key=daily_visitor_key(
                secret_key=secret_key,
                day=day,
                remote_addr=remote_addr,
            ),
        )

    if kind == "web_vital":
        if set(payload) != {"kind", "route", "metric", "value"}:
            raise ValueError("invalid web vital payload")
        metric = str(payload.get("metric") or "").strip().lower()
        if metric not in WEB_VITAL_METRICS:
            raise ValueError("invalid web vital metric")
        try:
            value = float(payload.get("value"))
        except (TypeError, ValueError) as exc:
            raise ValueError("invalid web vital value") from exc
        upper_bound = 10.0 if metric == "cls" else 120_000.0
        if value < 0 or value > upper_bound or value != value:
            raise ValueError("invalid web vital value")
        value = round(value, 4 if metric == "cls" else 2)
        return WebVitalEvent(
            occurred_at=now.isoformat(),
            day=day,
            route=route,
            metric=metric,
            value=value,
            rating=_web_vital_rating(metric, value),
        )

    raise ValueError("invalid analytics event kind")


def declared_response_body_bytes(
    *,
    method: str,
    status_code: int,
    content_length: Any,
) -> int:
    """Return declared response-body bytes without materializing the body.

    Content-Length on a HEAD response describes the equivalent GET payload, not
    bytes transferred. Informational, 204, 205, and 304 responses likewise do
    not carry a response body and must contribute zero traffic bytes.
    """
    normalized_method = str(method or "GET").upper()
    normalized_status = int(status_code)
    if (
        normalized_method == "HEAD"
        or normalized_status < 200
        or normalized_status in NO_RESPONSE_BODY_STATUS_CODES
    ):
        return 0
    try:
        return max(0, int(str(content_length or "").strip()))
    except (TypeError, ValueError):
        return 0


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


def reporting_utc_offset_minutes(config: dict[str, Any]) -> int:
    """Read and validate the configured calendar offset used for daily aggregates."""
    raw_value = config.get(
        "reporting_utc_offset_minutes",
        DEFAULT_REPORTING_UTC_OFFSET_MINUTES,
    )
    try:
        value = int(str(raw_value).strip())
    except (TypeError, ValueError) as exc:
        raise ValueError(
            "reporting_utc_offset_minutes must be an integer"
        ) from exc
    if not (
        MIN_REPORTING_UTC_OFFSET_MINUTES
        <= value
        <= MAX_REPORTING_UTC_OFFSET_MINUTES
    ):
        raise ValueError(
            "reporting_utc_offset_minutes must be between -720 and 840"
        )
    return value


def analytics_calendar_day(
    value: datetime | None = None,
    *,
    utc_offset_minutes: int = DEFAULT_REPORTING_UTC_OFFSET_MINUTES,
) -> date:
    """Return the configured local calendar day while keeping timestamps in UTC."""
    offset_minutes = _validated_utc_offset_minutes(utc_offset_minutes)
    current = value or datetime.now(timezone.utc)
    if current.tzinfo is None:
        current = current.replace(tzinfo=timezone.utc)
    calendar_timezone = timezone(timedelta(minutes=offset_minutes))
    return current.astimezone(calendar_timezone).date()


def analytics_calendar_day_utc_bounds(
    day: date,
    *,
    utc_offset_minutes: int = DEFAULT_REPORTING_UTC_OFFSET_MINUTES,
) -> tuple[datetime, datetime]:
    """Return the UTC half-open interval for one configured reporting day."""
    offset_minutes = _validated_utc_offset_minutes(utc_offset_minutes)
    calendar_timezone = timezone(timedelta(minutes=offset_minutes))
    start = datetime(day.year, day.month, day.day, tzinfo=calendar_timezone)
    start_utc = start.astimezone(timezone.utc)
    return start_utc, start_utc + timedelta(days=1)


def format_utc_offset(utc_offset_minutes: int) -> str:
    offset_minutes = _validated_utc_offset_minutes(utc_offset_minutes)
    sign = "+" if offset_minutes >= 0 else "-"
    hours, minutes = divmod(abs(offset_minutes), 60)
    return f"UTC{sign}{hours:02d}:{minutes:02d}"


def configure_access_analytics_calendar(
    db: sqlite3.Connection,
    *,
    utc_offset_minutes: int,
    now: datetime | None = None,
) -> dict[str, Any]:
    """Persist a calendar-boundary change and mark aggregates that predate it."""
    offset_minutes = _validated_utc_offset_minutes(utc_offset_minutes)
    current = now or datetime.now(timezone.utc)
    if current.tzinfo is None:
        current = current.replace(tzinfo=timezone.utc)
    current_utc = current.astimezone(timezone.utc)
    rows = {
        str(row[0]): str(row[1])
        for row in db.execute(
            "SELECT key, value FROM access_analytics_metadata"
        ).fetchall()
    }
    configured_offset = rows.get(CALENDAR_OFFSET_KEY)
    effective_at = rows.get(CALENDAR_EFFECTIVE_AT_KEY)
    if configured_offset == str(offset_minutes) and effective_at:
        return _calendar_metadata_payload(rows, offset_minutes)

    has_existing_rows = bool(db.execute(
        "SELECT EXISTS(SELECT 1 FROM access_daily LIMIT 1)"
    ).fetchone()[0])
    legacy_through_day = (
        analytics_calendar_day(
            current_utc,
            utc_offset_minutes=offset_minutes,
        ).isoformat()
        if has_existing_rows
        else ""
    )
    values = {
        CALENDAR_OFFSET_KEY: str(offset_minutes),
        CALENDAR_EFFECTIVE_AT_KEY: current_utc.isoformat(),
        LEGACY_CALENDAR_THROUGH_DAY_KEY: legacy_through_day,
    }
    with db:
        db.executemany(
            """
            INSERT INTO access_analytics_metadata (key, value)
            VALUES (?, ?)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """,
            values.items(),
        )
    return _calendar_metadata_payload(values, offset_minutes)


def read_access_analytics_calendar_metadata(
    db: sqlite3.Connection,
    *,
    utc_offset_minutes: int,
) -> dict[str, Any]:
    """Read calendar metadata without mutating the reporting request."""
    offset_minutes = _validated_utc_offset_minutes(utc_offset_minutes)
    try:
        rows = {
            str(row[0]): str(row[1])
            for row in db.execute(
                "SELECT key, value FROM access_analytics_metadata"
            ).fetchall()
        }
    except sqlite3.OperationalError as exc:
        if "no such table" not in str(exc).lower():
            raise
        rows = {}
    if rows.get(CALENDAR_OFFSET_KEY) != str(offset_minutes):
        rows = {}
    return _calendar_metadata_payload(rows, offset_minutes)


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
    utc_offset_minutes: int = DEFAULT_REPORTING_UTC_OFFSET_MINUTES,
) -> AccessEvent:
    now = occurred_at or datetime.now(timezone.utc)
    if now.tzinfo is None:
        now = now.replace(tzinfo=timezone.utc)
    now = now.astimezone(timezone.utc)
    day = analytics_calendar_day(
        now,
        utc_offset_minutes=utc_offset_minutes,
    ).isoformat()
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
    event: AnalyticsEvent


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
        event: AnalyticsEvent,
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

            grouped: dict[str, list[AnalyticsEvent]] = defaultdict(list)
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

    def _write_group(self, db_path: str, events: Sequence[AnalyticsEvent]) -> bool:
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


def _write_access_batch(db_path: Path, events: Sequence[AnalyticsEvent]):
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
                if isinstance(event, AccessEvent):
                    _write_access_event(db, event)
                elif isinstance(event, WebPageViewEvent):
                    _write_web_page_view(db, event)
                elif isinstance(event, WebVitalEvent):
                    _write_web_vital(db, event)
                else:  # pragma: no cover - guarded by the typed sink boundary
                    raise TypeError("unsupported analytics event")
    finally:
        db.close()


def _write_access_event(db: sqlite3.Connection, event: AccessEvent):
    date.fromisoformat(event.day)
    error_count = 1 if event.status_code >= 400 else 0
    client_error_count = 1 if 400 <= event.status_code < 500 else 0
    server_error_count = 1 if event.status_code >= 500 else 0
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
            (day, visits, unique_visitors, error_responses,
             client_error_responses, server_error_responses, total_duration_ms,
             max_duration_ms, total_response_bytes, updated_at)
        VALUES (?, 1, ?, ?, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(day) DO UPDATE SET
            visits = visits + 1,
            unique_visitors = unique_visitors + excluded.unique_visitors,
            error_responses = error_responses + excluded.error_responses,
            client_error_responses = client_error_responses + excluded.client_error_responses,
            server_error_responses = server_error_responses + excluded.server_error_responses,
            total_duration_ms = total_duration_ms + excluded.total_duration_ms,
            max_duration_ms = max(max_duration_ms, excluded.max_duration_ms),
            total_response_bytes = total_response_bytes + excluded.total_response_bytes,
            updated_at = excluded.updated_at
        """,
        (
            event.day,
            new_visitor,
            error_count,
            client_error_count,
            server_error_count,
            event.duration_ms,
            event.duration_ms,
            event.response_bytes,
            event.occurred_at,
        ),
    )
    db.execute(
        """
        INSERT INTO access_route_daily
            (day, route, method, visits, error_responses,
             client_error_responses, server_error_responses, total_duration_ms,
             max_duration_ms, total_response_bytes, updated_at)
        VALUES (?, ?, ?, 1, ?, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(day, route, method) DO UPDATE SET
            visits = visits + 1,
            error_responses = error_responses + excluded.error_responses,
            client_error_responses = client_error_responses + excluded.client_error_responses,
            server_error_responses = server_error_responses + excluded.server_error_responses,
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
            client_error_count,
            server_error_count,
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
             client_error_responses, server_error_responses, total_duration_ms,
             updated_at)
        VALUES (?, ?, 1, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(day, client_type) DO UPDATE SET
            visits = visits + 1,
            unique_visitors = unique_visitors + excluded.unique_visitors,
            error_responses = error_responses + excluded.error_responses,
            client_error_responses = client_error_responses + excluded.client_error_responses,
            server_error_responses = server_error_responses + excluded.server_error_responses,
            total_duration_ms = total_duration_ms + excluded.total_duration_ms,
            updated_at = excluded.updated_at
        """,
        (
            event.day,
            event.client_type,
            new_visitor,
            error_count,
            client_error_count,
            server_error_count,
            event.duration_ms,
            event.occurred_at,
        ),
    )


def _write_web_page_view(db: sqlite3.Connection, event: WebPageViewEvent):
    date.fromisoformat(event.day)
    new_visitor = 0
    if event.visitor_key:
        cursor = db.execute(
            """
            INSERT OR IGNORE INTO web_page_visitor_daily
                (day, route, visitor_key, page_views, first_seen_at, last_seen_at)
            VALUES (?, ?, ?, 0, ?, ?)
            """,
            (
                event.day,
                event.route,
                event.visitor_key,
                event.occurred_at,
                event.occurred_at,
            ),
        )
        new_visitor = 1 if cursor.rowcount == 1 else 0
        db.execute(
            """
            UPDATE web_page_visitor_daily
            SET page_views = page_views + 1, last_seen_at = ?
            WHERE day = ? AND route = ? AND visitor_key = ?
            """,
            (
                event.occurred_at,
                event.day,
                event.route,
                event.visitor_key,
            ),
        )

    hard_navigation = 1 if event.navigation_type == "hard" else 0
    spa_navigation = 1 if event.navigation_type == "spa" else 0
    db.execute(
        """
        INSERT INTO web_page_daily
            (day, route, page_views, unique_visitors, hard_navigations,
             spa_navigations, updated_at)
        VALUES (?, ?, 1, ?, ?, ?, ?)
        ON CONFLICT(day, route) DO UPDATE SET
            page_views = page_views + 1,
            unique_visitors = unique_visitors + excluded.unique_visitors,
            hard_navigations = hard_navigations + excluded.hard_navigations,
            spa_navigations = spa_navigations + excluded.spa_navigations,
            updated_at = excluded.updated_at
        """,
        (
            event.day,
            event.route,
            new_visitor,
            hard_navigation,
            spa_navigation,
            event.occurred_at,
        ),
    )


def _write_web_vital(db: sqlite3.Connection, event: WebVitalEvent):
    date.fromisoformat(event.day)
    good = 1 if event.rating == "good" else 0
    needs_improvement = 1 if event.rating == "needs_improvement" else 0
    poor = 1 if event.rating == "poor" else 0
    db.execute(
        """
        INSERT INTO web_vital_daily
            (day, route, metric, samples, total_value, max_value, good_samples,
             needs_improvement_samples, poor_samples, updated_at)
        VALUES (?, ?, ?, 1, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(day, route, metric) DO UPDATE SET
            samples = samples + 1,
            total_value = total_value + excluded.total_value,
            max_value = max(max_value, excluded.max_value),
            good_samples = good_samples + excluded.good_samples,
            needs_improvement_samples = needs_improvement_samples
                + excluded.needs_improvement_samples,
            poor_samples = poor_samples + excluded.poor_samples,
            updated_at = excluded.updated_at
        """,
        (
            event.day,
            event.route,
            event.metric,
            event.value,
            event.value,
            good,
            needs_improvement,
            poor,
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
        utc_offset_minutes: int = DEFAULT_REPORTING_UTC_OFFSET_MINUTES,
    ):
        self._db_factory = db_factory
        self._recorder_status = recorder_status or _empty_recorder_status
        self._today_getter = today_getter
        self._utc_offset_minutes = _validated_utc_offset_minutes(utc_offset_minutes)

    def get_traffic(self, *, days: int, limit: int) -> dict[str, Any]:
        validate_query_range(days=days, limit=limit)
        today = (
            self._today_getter()
            if self._today_getter is not None
            else analytics_calendar_day(
                utc_offset_minutes=self._utc_offset_minutes,
            )
        )
        start = today - timedelta(days=days - 1)
        db = self._db_factory()
        try:
            daily_rows = db.execute(
                """
                SELECT day, visits, unique_visitors, error_responses,
                       client_error_responses, server_error_responses,
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
                       SUM(client_error_responses) AS client_error_responses,
                       SUM(server_error_responses) AS server_error_responses,
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
                       SUM(client_error_responses) AS client_error_responses,
                       SUM(server_error_responses) AS server_error_responses,
                       SUM(total_duration_ms) AS total_duration_ms
                FROM access_client_daily
                WHERE day BETWEEN ? AND ?
                GROUP BY client_type
                ORDER BY visits DESC, client_type
                """,
                (start.isoformat(), today.isoformat()),
            ).fetchall()
            web_daily_rows = db.execute(
                """
                SELECT day, SUM(page_views) AS page_views,
                       SUM(hard_navigations) AS hard_navigations,
                       SUM(spa_navigations) AS spa_navigations
                FROM web_page_daily
                WHERE day BETWEEN ? AND ?
                GROUP BY day
                ORDER BY day
                """,
                (start.isoformat(), today.isoformat()),
            ).fetchall()
            web_daily_visitor_rows = db.execute(
                """
                SELECT day, COUNT(DISTINCT visitor_key) AS unique_visitors
                FROM web_page_visitor_daily
                WHERE day BETWEEN ? AND ?
                GROUP BY day
                ORDER BY day
                """,
                (start.isoformat(), today.isoformat()),
            ).fetchall()
            web_page_rows = db.execute(
                """
                SELECT route, SUM(page_views) AS page_views,
                       SUM(unique_visitors) AS unique_visitors,
                       SUM(hard_navigations) AS hard_navigations,
                       SUM(spa_navigations) AS spa_navigations
                FROM web_page_daily
                WHERE day BETWEEN ? AND ?
                GROUP BY route
                ORDER BY page_views DESC, route
                LIMIT ?
                """,
                (start.isoformat(), today.isoformat(), limit),
            ).fetchall()
            web_vital_rows = db.execute(
                """
                SELECT metric, SUM(samples) AS samples,
                       SUM(total_value) AS total_value,
                       MAX(max_value) AS max_value,
                       SUM(good_samples) AS good_samples,
                       SUM(needs_improvement_samples) AS needs_improvement_samples,
                       SUM(poor_samples) AS poor_samples
                FROM web_vital_daily
                WHERE day BETWEEN ? AND ?
                GROUP BY metric
                """,
                (start.isoformat(), today.isoformat()),
            ).fetchall()
            calendar_metadata = read_access_analytics_calendar_metadata(
                db,
                utc_offset_minutes=self._utc_offset_minutes,
            )
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
        summary_client_errors = sum(item["clientErrorResponses"] for item in daily)
        summary_server_errors = sum(item["serverErrorResponses"] for item in daily)
        duration = sum(item["totalDurationMs"] for item in daily)
        response_bytes = sum(item["totalResponseBytes"] for item in daily)
        legacy_through_day = calendar_metadata["legacyCalendarDataThroughDay"]
        has_legacy_calendar_data = bool(
            legacy_through_day
            and any(
                int(row["visits"] or 0) > 0 and str(row["day"]) <= legacy_through_day
                for row in daily_rows
            )
        )

        top_routes = []
        for row in route_rows:
            route_visits = int(row["visits"] or 0)
            route_errors = int(row["error_responses"] or 0)
            route_client_errors = int(row["client_error_responses"] or 0)
            route_server_errors = int(row["server_error_responses"] or 0)
            route_duration = int(row["total_duration_ms"] or 0)
            top_routes.append({
                "route": row["route"],
                "method": row["method"],
                "visits": route_visits,
                "errorResponses": route_errors,
                "errorRate": _percentage(route_errors, route_visits),
                **_classified_error_payload(
                    route_errors,
                    route_client_errors,
                    route_server_errors,
                    route_visits,
                ),
                "avgResponseMs": _average(route_duration, route_visits),
                "maxResponseMs": int(row["max_duration_ms"] or 0),
                "responseBytes": int(row["total_response_bytes"] or 0),
            })

        clients = []
        for row in client_rows:
            client_visits = int(row["visits"] or 0)
            client_total_errors = int(row["error_responses"] or 0)
            client_4xx_errors = int(row["client_error_responses"] or 0)
            client_5xx_errors = int(row["server_error_responses"] or 0)
            client_visitor_days = int(row["unique_visitors"] or 0)
            clients.append({
                "client": row["client_type"],
                "visits": client_visits,
                "uniqueVisitorDays": client_visitor_days,
                "share": _percentage(client_visits, visits),
                "errorResponses": client_total_errors,
                "errorRate": _percentage(client_total_errors, client_visits),
                **_classified_error_payload(
                    client_total_errors,
                    client_4xx_errors,
                    client_5xx_errors,
                    client_visits,
                ),
                "avgResponseMs": _average(int(row["total_duration_ms"] or 0), client_visits),
            })

        web_daily_base = {
            str(row["day"]): {
                "pageViews": int(row["page_views"] or 0),
                "hardNavigations": int(row["hard_navigations"] or 0),
                "spaNavigations": int(row["spa_navigations"] or 0),
            }
            for row in web_daily_rows
        }
        web_daily_visitors = {
            str(row["day"]): int(row["unique_visitors"] or 0)
            for row in web_daily_visitor_rows
        }
        web_daily = []
        for offset in range(days):
            day_text = (start + timedelta(days=offset)).isoformat()
            item = web_daily_base.get(day_text, {
                "pageViews": 0,
                "hardNavigations": 0,
                "spaNavigations": 0,
            })
            web_daily.append({
                "day": day_text,
                **item,
                "uniqueVisitors": web_daily_visitors.get(day_text, 0),
            })

        page_views = sum(item["pageViews"] for item in web_daily)
        page_visitor_days = sum(item["uniqueVisitors"] for item in web_daily)
        hard_navigations = sum(item["hardNavigations"] for item in web_daily)
        spa_navigations = sum(item["spaNavigations"] for item in web_daily)
        top_pages = [{
            "route": str(row["route"]),
            "pageViews": int(row["page_views"] or 0),
            "uniqueVisitorDays": int(row["unique_visitors"] or 0),
            "hardNavigations": int(row["hard_navigations"] or 0),
            "spaNavigations": int(row["spa_navigations"] or 0),
        } for row in web_page_rows]

        vital_by_metric = {str(row["metric"]): row for row in web_vital_rows}
        vital_units = {"lcp": "ms", "cls": "score", "inp": "ms"}
        vitals = []
        for metric in WEB_VITAL_METRICS:
            row = vital_by_metric.get(metric)
            samples = int(row["samples"] or 0) if row else 0
            good_samples = int(row["good_samples"] or 0) if row else 0
            needs_improvement_samples = (
                int(row["needs_improvement_samples"] or 0) if row else 0
            )
            poor_samples = int(row["poor_samples"] or 0) if row else 0
            total_value = float(row["total_value"] or 0) if row else 0.0
            vitals.append({
                "metric": metric.upper(),
                "unit": vital_units[metric],
                "samples": samples,
                "average": round(total_value / samples, 2) if samples else 0.0,
                "maximum": round(float(row["max_value"] or 0), 2) if row else 0.0,
                "goodSamples": good_samples,
                "needsImprovementSamples": needs_improvement_samples,
                "poorSamples": poor_samples,
                "goodRate": _percentage(good_samples, samples),
            })

        return {
            "summary": {
                "periodStart": start.isoformat(),
                "periodEnd": today.isoformat(),
                "days": days,
                **calendar_metadata,
                "hasLegacyCalendarData": has_legacy_calendar_data,
                "visits": visits,
                # Daily HMAC identifiers intentionally rotate, so a multi-day
                # total is visitor-days rather than a cross-day unique count.
                "uniqueVisitorDays": unique_visitor_days,
                "avgResponseMs": _average(duration, visits),
                "errorResponses": errors,
                "errorRate": _percentage(errors, visits),
                **_classified_error_payload(
                    errors,
                    summary_client_errors,
                    summary_server_errors,
                    visits,
                ),
                "totalResponseBytes": response_bytes,
            },
            "today": daily[-1],
            "daily": daily,
            "topRoutes": top_routes,
            "clients": clients,
            "web": {
                "summary": {
                    "pageViews": page_views,
                    "uniqueVisitorDays": page_visitor_days,
                    "hardNavigations": hard_navigations,
                    "spaNavigations": spa_navigations,
                },
                "daily": web_daily,
                "topPages": top_pages,
                "vitals": vitals,
            },
            "recorder": self._recorder_status(),
        }


def get_today_access_summary(
    db: Any,
    *,
    today: date | None = None,
    utc_offset_minutes: int = DEFAULT_REPORTING_UTC_OFFSET_MINUTES,
) -> dict[str, Any]:
    day = today or analytics_calendar_day(
        utc_offset_minutes=utc_offset_minutes,
    )
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
    utc_offset_minutes: int = DEFAULT_REPORTING_UTC_OFFSET_MINUTES,
) -> dict[str, Any]:
    if not 1 <= int(retention_days) <= 3650:
        raise ValueError("retention_days must be between 1 and 3650")
    current_day = today or analytics_calendar_day(
        utc_offset_minutes=utc_offset_minutes,
    )
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
    utc_offset_minutes: int = DEFAULT_REPORTING_UTC_OFFSET_MINUTES,
) -> dict[str, Any]:
    """Apply visitor retention to a SQLite snapshot and verify its integrity."""
    if not 1 <= int(retention_days) <= 3650:
        raise ValueError("retention_days must be between 1 and 3650")
    current_day = today or analytics_calendar_day(
        utc_offset_minutes=utc_offset_minutes,
    )
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
    utc_offset_minutes: int = DEFAULT_REPORTING_UTC_OFFSET_MINUTES,
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
                utc_offset_minutes=utc_offset_minutes,
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
    client_errors = int(row["client_error_responses"] or 0)
    server_errors = int(row["server_error_responses"] or 0)
    return {
        "day": row["day"],
        "visits": visits,
        "uniqueVisitors": int(row["unique_visitors"] or 0),
        "avgResponseMs": _average(duration, visits),
        "maxResponseMs": int(row["max_duration_ms"] or 0),
        "errorResponses": errors,
        "errorRate": _percentage(errors, visits),
        **_classified_error_payload(errors, client_errors, server_errors, visits),
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
        **_classified_error_payload(0, 0, 0, 0),
        "totalDurationMs": 0,
        "totalResponseBytes": 0,
    }


def _average(total: int, count: int) -> float:
    return round(total / count, 2) if count else 0.0


def _percentage(part: int, total: int) -> float:
    return round(part * 100 / total, 2) if total else 0.0


def _classified_error_payload(
    errors: int,
    client_errors: int,
    server_errors: int,
    visits: int,
) -> dict[str, int | float]:
    unclassified_errors = max(0, errors - client_errors - server_errors)
    return {
        "clientErrorResponses": client_errors,
        "clientErrorRate": _percentage(client_errors, visits),
        "serverErrorResponses": server_errors,
        "serverErrorRate": _percentage(server_errors, visits),
        "unclassifiedErrorResponses": unclassified_errors,
        "unclassifiedErrorRate": _percentage(unclassified_errors, visits),
    }


def _validated_utc_offset_minutes(value: int) -> int:
    try:
        offset_minutes = int(value)
    except (TypeError, ValueError) as exc:
        raise ValueError("utc_offset_minutes must be an integer") from exc
    if not (
        MIN_REPORTING_UTC_OFFSET_MINUTES
        <= offset_minutes
        <= MAX_REPORTING_UTC_OFFSET_MINUTES
    ):
        raise ValueError("utc_offset_minutes must be between -720 and 840")
    return offset_minutes


def _calendar_metadata_payload(
    values: dict[str, str],
    utc_offset_minutes: int,
) -> dict[str, Any]:
    return {
        "timeZone": format_utc_offset(utc_offset_minutes),
        "utcOffsetMinutes": utc_offset_minutes,
        "calendarBoundaryEffectiveAt": values.get(CALENDAR_EFFECTIVE_AT_KEY) or None,
        "legacyCalendarDataThroughDay": (
            values.get(LEGACY_CALENDAR_THROUGH_DAY_KEY) or None
        ),
    }


def _empty_recorder_status() -> dict[str, Any]:
    return {"pending": 0, "dropped": 0, "lastError": None}

import base64
import copy
import json
import sqlite3
import tempfile
import time
import unittest
from dataclasses import asdict
from datetime import date, datetime, timedelta, timezone
from pathlib import Path
from types import SimpleNamespace

from flask import Flask, Response, request
from werkzeug.routing import Rule

import app as marketplace_app
from app_setup import register_slow_request_logging
from db_cache import CacheManager
from services.access_analytics import (
    AccessAnalyticsRecorder,
    SqliteAccessTrafficQuery,
    analytics_calendar_day,
    analytics_calendar_day_utc_bounds,
    build_access_event,
    build_web_experience_event,
    classify_user_agent,
    configure_access_analytics_calendar,
    daily_visitor_key,
    declared_response_body_bytes,
    format_utc_offset,
    normalize_web_route,
    parse_bounded_int,
    prune_access_analytics,
    prune_access_analytics_backups,
    reporting_utc_offset_minutes,
    should_record_access,
)


FIXED_NOW = datetime(2026, 7, 18, 9, 30, tzinfo=timezone.utc)


class AccessAnalyticsUnitTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.db_path = self.root / "analytics.db"
        self.cache = CacheManager(self.db_path)
        self.cache.init_db()

    def tearDown(self):
        self.temp_dir.cleanup()

    def _event(
        self,
        *,
        route="/plugins/<plugin_id>",
        status=200,
        duration=10,
        response_bytes=100,
        address="203.0.113.10",
        user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
        occurred_at=FIXED_NOW,
    ):
        return build_access_event(
            route_template=route,
            method="GET",
            status_code=status,
            duration_ms=duration,
            response_bytes=response_bytes,
            secret_key="analytics-test-secret",
            remote_addr=address,
            user_agent=user_agent,
            occurred_at=occurred_at,
        )

    def test_event_boundary_does_not_retain_raw_identifiers(self):
        raw_address = "203.0.113.42"
        raw_user_agent = "SecretBrowser/9.1 (iPhone; InternalBuild/abc)"
        event = self._event(
            address=raw_address,
            user_agent=raw_user_agent,
            route="api/plugins/<plugin_id>/",
        )

        serialized = json.dumps(asdict(event), ensure_ascii=False)
        self.assertEqual(event.route, "/api/plugins/<plugin_id>")
        self.assertEqual(event.client_type, "mobile")
        self.assertNotIn(raw_address, serialized)
        self.assertNotIn(raw_user_agent, serialized)
        self.assertNotIn("InternalBuild", serialized)

    def test_daily_visitor_key_is_stable_only_within_a_day(self):
        first = daily_visitor_key(
            secret_key="secret", day="2026-07-18", remote_addr="203.0.113.10"
        )
        same = daily_visitor_key(
            secret_key="secret", day="2026-07-18", remote_addr="203.0.113.10"
        )
        next_day = daily_visitor_key(
            secret_key="secret", day="2026-07-19", remote_addr="203.0.113.10"
        )
        self.assertEqual(first, same)
        self.assertNotEqual(first, next_day)
        self.assertIsNone(
            daily_visitor_key(secret_key="secret", day="2026-07-18", remote_addr=None)
        )

    def test_reporting_calendar_uses_configured_offset_at_midnight(self):
        before_midnight = self._event(
            occurred_at=datetime(2026, 7, 18, 15, 59, 59, tzinfo=timezone.utc)
        )
        after_midnight = self._event(
            occurred_at=datetime(2026, 7, 18, 16, 0, tzinfo=timezone.utc)
        )

        self.assertEqual(before_midnight.day, "2026-07-18")
        self.assertEqual(after_midnight.day, "2026-07-19")
        self.assertEqual(
            after_midnight.occurred_at,
            "2026-07-18T16:00:00+00:00",
        )
        self.assertNotEqual(before_midnight.visitor_key, after_midnight.visitor_key)
        self.assertEqual(
            analytics_calendar_day(
                datetime(2026, 7, 18, 16, 0, tzinfo=timezone.utc),
                utc_offset_minutes=480,
            ),
            date(2026, 7, 19),
        )
        start, end = analytics_calendar_day_utc_bounds(
            date(2026, 7, 19),
            utc_offset_minutes=480,
        )
        self.assertEqual(start.isoformat(), "2026-07-18T16:00:00+00:00")
        self.assertEqual(end.isoformat(), "2026-07-19T16:00:00+00:00")

    def test_reporting_offset_is_validated_and_formatted(self):
        self.assertEqual(reporting_utc_offset_minutes({}), 480)
        self.assertEqual(
            reporting_utc_offset_minutes({"reporting_utc_offset_minutes": "330"}),
            330,
        )
        self.assertEqual(format_utc_offset(330), "UTC+05:30")
        self.assertEqual(format_utc_offset(-210), "UTC-03:30")
        for invalid in ("bad", -721, 841):
            with self.subTest(invalid=invalid):
                with self.assertRaises(ValueError):
                    reporting_utc_offset_minutes({
                        "reporting_utc_offset_minutes": invalid,
                    })

    def test_user_agent_is_reduced_to_coarse_device_classes(self):
        self.assertEqual(classify_user_agent("Googlebot/2.1"), "bot")
        self.assertEqual(classify_user_agent("Mozilla/5.0 (iPad)"), "tablet")
        self.assertEqual(classify_user_agent("Mozilla/5.0 (Android 13; Tablet)"), "tablet")
        self.assertEqual(classify_user_agent("Mozilla/5.0 (iPhone) Mobile"), "mobile")
        self.assertEqual(classify_user_agent("Mozilla/5.0 (Windows NT 10.0)"), "desktop")
        self.assertEqual(classify_user_agent("CustomClient/1.0"), "other")

    def test_route_exclusions_cover_probes_static_media_and_stats(self):
        for route in (
            "/api/health",
            "/api/ready",
            "/assets/<path:asset_path>",
            "/media/<path:asset_path>",
            "/api/admin/stats/traffic",
            "/api/admin/stats/overview",
            "/api/admin/perf/summary",
            "/api/v1/analytics/events",
        ):
            self.assertFalse(should_record_access(route, "GET"), route)
        self.assertFalse(should_record_access("/api/plugins", "OPTIONS"))
        self.assertTrue(should_record_access("/api/plugins/<plugin_id>", "GET"))

    def test_web_experience_boundary_accepts_only_normalized_routes_and_exact_fields(self):
        self.assertEqual(normalize_web_route("/plugins/ProjectARVRPro"), "/plugins/:pluginId")
        self.assertEqual(normalize_web_route("/browse/Plugins/Camera"), "/browse/*")
        self.assertEqual(normalize_web_route("/admin/traffic/"), "/admin/traffic")
        for invalid in (
            "/unknown",
            "/plugins/private/value/extra",
            "/browse/item?token=secret",
            "//admin/traffic",
        ):
            with self.subTest(invalid=invalid):
                with self.assertRaises(ValueError):
                    normalize_web_route(invalid)

        raw_address = "203.0.113.88"
        raw_agent = "PrivateBrowser/1.0 DeviceSerial=ABC"
        page_view = build_web_experience_event(
            {"kind": "page_view", "route": "/plugins/Demo", "navigationType": "spa"},
            secret_key="analytics-test-secret",
            remote_addr=raw_address,
            user_agent=raw_agent,
            occurred_at=FIXED_NOW,
        )
        self.assertEqual(page_view.route, "/plugins/:pluginId")
        self.assertEqual(page_view.navigation_type, "spa")
        serialized = json.dumps(asdict(page_view), ensure_ascii=False)
        self.assertNotIn(raw_address, serialized)
        self.assertNotIn(raw_agent, serialized)

        with self.assertRaisesRegex(ValueError, "invalid page view payload"):
            build_web_experience_event(
                {
                    "kind": "page_view",
                    "route": "/",
                    "navigationType": "hard",
                    "referrer": "https://private.example/",
                },
                secret_key="secret",
                remote_addr=raw_address,
                user_agent=raw_agent,
            )

    def test_declared_response_body_bytes_obeys_http_no_body_semantics(self):
        self.assertEqual(
            declared_response_body_bytes(
                method="GET", status_code=200, content_length="1048576"
            ),
            1048576,
        )
        for method, status in (
            ("HEAD", 200),
            ("GET", 101),
            ("GET", 204),
            ("GET", 205),
            ("GET", 304),
        ):
            with self.subTest(method=method, status=status):
                self.assertEqual(
                    declared_response_body_bytes(
                        method=method,
                        status_code=status,
                        content_length="1048576",
                    ),
                    0,
                )
        for invalid in (None, "", "invalid", "-10"):
            with self.subTest(content_length=invalid):
                self.assertEqual(
                    declared_response_body_bytes(
                        method="GET", status_code=200, content_length=invalid
                    ),
                    0,
                )

    def test_request_hook_reads_content_length_without_reading_response_body(self):
        events = []

        class Recorder:
            def submit(self, event, **_kwargs):
                events.append(event)
                return True

        class BodyReadForbiddenResponse(Response):
            def get_data(self, *args, **kwargs):
                raise AssertionError("analytics must not materialize a response body")

        test_app = Flask("access-analytics-hook-test")
        test_app.config["TESTING"] = True
        context = SimpleNamespace(
            slow_request_threshold_ms=86_400_000,
            slow_requests=[],
        )
        register_slow_request_logging(test_app, context, Recorder())
        after_request = test_app.after_request_funcs[None][0]

        with test_app.test_request_context("/stream"):
            request._start_time = time.monotonic()
            request.url_rule = Rule("/stream")
            response = BodyReadForbiddenResponse(headers={"Content-Length": "1048576"})
            self.assertIs(after_request(response), response)

        self.assertEqual(len(events), 1)
        self.assertEqual(events[0].response_bytes, 1048576)

    def test_request_hook_does_not_count_head_representation_length_as_traffic(self):
        events = []

        class Recorder:
            def submit(self, event, **_kwargs):
                events.append(event)
                return True

        test_app = Flask("access-analytics-head-hook-test")
        test_app.config["TESTING"] = True
        context = SimpleNamespace(
            slow_request_threshold_ms=86_400_000,
            slow_requests=[],
        )
        register_slow_request_logging(test_app, context, Recorder())
        after_request = test_app.after_request_funcs[None][0]

        with test_app.test_request_context("/download", method="HEAD"):
            request._start_time = time.monotonic()
            request.url_rule = Rule("/download")
            response = Response(headers={"Content-Length": "1048576"})
            self.assertIs(after_request(response), response)

        self.assertEqual(len(events), 1)
        self.assertEqual(events[0].response_bytes, 0)

    def test_synchronous_writer_aggregates_daily_route_client_and_visitors(self):
        recorder = AccessAnalyticsRecorder(background_worker=False)
        events = [
            self._event(duration=10),
            self._event(status=500, duration=20),
            self._event(
                route="/",
                duration=30,
                address="203.0.113.11",
                user_agent="Mozilla/5.0 (iPhone) Mobile",
            ),
        ]
        for event in events:
            self.assertTrue(recorder.submit(event, db_path=self.db_path, synchronous=True))

        query = SqliteAccessTrafficQuery(
            self.cache.get_db,
            recorder_status=recorder.status,
            today_getter=lambda: date(2026, 7, 18),
        )
        payload = query.get_traffic(days=1, limit=10)

        self.assertEqual(payload["summary"]["visits"], 3)
        self.assertEqual(payload["summary"]["uniqueVisitorDays"], 2)
        self.assertNotIn("uniqueVisitors", payload["summary"])
        self.assertEqual(payload["summary"]["errorResponses"], 1)
        self.assertEqual(payload["summary"]["clientErrorResponses"], 0)
        self.assertEqual(payload["summary"]["serverErrorResponses"], 1)
        self.assertEqual(payload["summary"]["serverErrorRate"], 33.33)
        self.assertEqual(payload["summary"]["unclassifiedErrorResponses"], 0)
        self.assertEqual(payload["summary"]["avgResponseMs"], 20.0)
        self.assertEqual(payload["summary"]["totalResponseBytes"], 300)
        self.assertEqual(payload["today"]["visits"], 3)
        self.assertEqual(payload["topRoutes"][0]["route"], "/plugins/<plugin_id>")
        self.assertEqual(payload["topRoutes"][0]["visits"], 2)
        self.assertEqual(payload["topRoutes"][0]["responseBytes"], 200)
        self.assertEqual(payload["topRoutes"][0]["serverErrorResponses"], 1)
        clients = {item["client"]: item for item in payload["clients"]}
        self.assertEqual(clients["desktop"]["visits"], 2)
        self.assertEqual(clients["desktop"]["uniqueVisitorDays"], 1)
        self.assertEqual(clients["desktop"]["serverErrorResponses"], 1)
        self.assertNotIn("uniqueVisitors", clients["desktop"])
        self.assertEqual(clients["mobile"]["visits"], 1)

        db = self.cache.get_db()
        try:
            tables = {
                row[0]
                for row in db.execute(
                    "SELECT name FROM sqlite_master WHERE type = 'table'"
                ).fetchall()
            }
            dump = "\n".join(db.iterdump())
        finally:
            db.close()
        for table in (
            "access_daily",
            "access_route_daily",
            "access_client_daily",
            "access_visitor_daily",
        ):
            self.assertIn(table, tables)
        self.assertNotIn("203.0.113.10", dump)
        self.assertNotIn("Windows NT 10.0", dump)

    def test_web_page_views_and_vitals_are_aggregated_separately(self):
        recorder = AccessAnalyticsRecorder(background_worker=False)
        payloads = [
            {"kind": "page_view", "route": "/", "navigationType": "hard"},
            {"kind": "page_view", "route": "/plugins", "navigationType": "spa"},
            {"kind": "page_view", "route": "/plugins/Demo", "navigationType": "spa"},
            {"kind": "web_vital", "route": "/", "metric": "LCP", "value": 2200},
            {"kind": "web_vital", "route": "/", "metric": "CLS", "value": 0.2},
            {"kind": "web_vital", "route": "/", "metric": "INP", "value": 700},
        ]
        for payload in payloads:
            event = build_web_experience_event(
                payload,
                secret_key="analytics-test-secret",
                remote_addr="203.0.113.10",
                user_agent="Mozilla/5.0 (Windows NT 10.0)",
                occurred_at=FIXED_NOW,
            )
            self.assertIsNotNone(event)
            self.assertTrue(recorder.submit(event, db_path=self.db_path, synchronous=True))

        result = SqliteAccessTrafficQuery(
            self.cache.get_db,
            today_getter=lambda: date(2026, 7, 18),
        ).get_traffic(days=1, limit=10)

        self.assertEqual(result["summary"]["visits"], 0)
        self.assertEqual(result["web"]["summary"], {
            "pageViews": 3,
            "uniqueVisitorDays": 1,
            "hardNavigations": 1,
            "spaNavigations": 2,
        })
        self.assertEqual(result["web"]["daily"][0]["pageViews"], 3)
        pages = {item["route"]: item for item in result["web"]["topPages"]}
        self.assertEqual(pages["/"]["hardNavigations"], 1)
        self.assertEqual(pages["/plugins"]["spaNavigations"], 1)
        self.assertEqual(pages["/plugins/:pluginId"]["spaNavigations"], 1)
        vitals = {item["metric"]: item for item in result["web"]["vitals"]}
        self.assertEqual(vitals["LCP"]["goodSamples"], 1)
        self.assertEqual(vitals["CLS"]["needsImprovementSamples"], 1)
        self.assertEqual(vitals["INP"]["poorSamples"], 1)
        self.assertEqual(vitals["INP"]["average"], 700.0)

        db = self.cache.get_db()
        try:
            dump = "\n".join(db.iterdump())
        finally:
            db.close()
        self.assertNotIn("203.0.113.10", dump)
        self.assertNotIn("Windows NT 10.0", dump)

    def test_query_reports_legacy_combined_errors_as_unclassified(self):
        recorder = AccessAnalyticsRecorder(background_worker=False)
        self.assertTrue(
            recorder.submit(
                self._event(status=404),
                db_path=self.db_path,
                synchronous=True,
            )
        )
        db = self.cache.get_db()
        try:
            with db:
                for table in (
                    "access_daily",
                    "access_route_daily",
                    "access_client_daily",
                ):
                    db.execute(
                        f"UPDATE {table} SET client_error_responses = 0, "
                        "server_error_responses = 0"
                    )
        finally:
            db.close()

        payload = SqliteAccessTrafficQuery(
            self.cache.get_db,
            today_getter=lambda: date(2026, 7, 18),
        ).get_traffic(days=1, limit=10)

        for item in (
            payload["summary"],
            payload["today"],
            payload["topRoutes"][0],
            payload["clients"][0],
        ):
            self.assertEqual(item["errorResponses"], 1)
            self.assertEqual(item["clientErrorResponses"], 0)
            self.assertEqual(item["serverErrorResponses"], 0)
            self.assertEqual(item["unclassifiedErrorResponses"], 1)
            self.assertEqual(item["unclassifiedErrorRate"], 100.0)

    def test_calendar_configuration_marks_existing_aggregates_without_rewriting_them(self):
        recorder = AccessAnalyticsRecorder(background_worker=False)
        self.assertTrue(
            recorder.submit(
                self._event(),
                db_path=self.db_path,
                synchronous=True,
            )
        )
        configured_at = datetime(2026, 7, 18, 16, 30, tzinfo=timezone.utc)
        db = self.cache.get_db()
        try:
            before = tuple(db.execute(
                "SELECT visits, error_responses, total_response_bytes FROM access_daily"
            ).fetchone())
            first = configure_access_analytics_calendar(
                db,
                utc_offset_minutes=480,
                now=configured_at,
            )
            second = configure_access_analytics_calendar(
                db,
                utc_offset_minutes=480,
                now=configured_at + timedelta(hours=1),
            )
            after = tuple(db.execute(
                "SELECT visits, error_responses, total_response_bytes FROM access_daily"
            ).fetchone())
        finally:
            db.close()

        self.assertEqual(before, after)
        self.assertEqual(first, second)
        self.assertEqual(first["timeZone"], "UTC+08:00")
        self.assertEqual(
            first["calendarBoundaryEffectiveAt"],
            "2026-07-18T16:30:00+00:00",
        )
        self.assertEqual(first["legacyCalendarDataThroughDay"], "2026-07-19")

        payload = SqliteAccessTrafficQuery(
            self.cache.get_db,
            today_getter=lambda: date(2026, 7, 19),
            utc_offset_minutes=480,
        ).get_traffic(days=2, limit=10)
        self.assertEqual(payload["summary"]["timeZone"], "UTC+08:00")
        self.assertEqual(payload["summary"]["utcOffsetMinutes"], 480)
        self.assertTrue(payload["summary"]["hasLegacyCalendarData"])
        self.assertEqual(
            payload["summary"]["legacyCalendarDataThroughDay"],
            "2026-07-19",
        )

    def test_fresh_calendar_configuration_does_not_mark_new_data_as_legacy(self):
        fresh_path = self.root / "fresh-calendar.db"
        fresh_cache = CacheManager(fresh_path)
        fresh_cache.init_db()
        db = fresh_cache.get_db()
        try:
            metadata = configure_access_analytics_calendar(
                db,
                utc_offset_minutes=480,
                now=FIXED_NOW,
            )
        finally:
            db.close()
        self.assertIsNone(metadata["legacyCalendarDataThroughDay"])

        recorder = AccessAnalyticsRecorder(background_worker=False)
        self.assertTrue(recorder.submit(
            self._event(),
            db_path=fresh_path,
            synchronous=True,
        ))
        payload = SqliteAccessTrafficQuery(
            fresh_cache.get_db,
            today_getter=lambda: date(2026, 7, 18),
            utc_offset_minutes=480,
        ).get_traffic(days=1, limit=10)
        self.assertFalse(payload["summary"]["hasLegacyCalendarData"])

    def test_async_writer_groups_events_by_submission_database(self):
        second_db_path = self.root / "second.db"
        recorder = AccessAnalyticsRecorder(
            queue_capacity=8,
            batch_size=8,
            flush_interval_seconds=0.05,
        )
        try:
            recorder.submit(self._event(), db_path=self.db_path)
            recorder.submit(
                self._event(address="203.0.113.12"),
                db_path=second_db_path,
            )
            self.assertTrue(recorder.flush(3.0), recorder.status())
        finally:
            recorder.close()

        for path in (self.db_path, second_db_path):
            db = sqlite3.connect(str(path))
            try:
                visits = db.execute("SELECT visits FROM access_daily").fetchone()[0]
            finally:
                db.close()
            self.assertEqual(visits, 1)

    def test_bounded_queue_reports_pending_dropped_and_error(self):
        recorder = AccessAnalyticsRecorder(queue_capacity=1, background_worker=False)
        self.assertTrue(recorder.submit(self._event(), db_path=self.db_path))
        self.assertFalse(recorder.submit(self._event(), db_path=self.db_path))
        status = recorder.status()
        self.assertEqual(status["pending"], 1)
        self.assertEqual(status["dropped"], 1)
        self.assertIn("queue", status["lastError"])

    def test_query_bounds_and_retention(self):
        self.assertEqual(
            parse_bounded_int(None, name="days", default=30, minimum=1, maximum=365),
            30,
        )
        with self.assertRaises(ValueError):
            parse_bounded_int("0", name="days", default=30, minimum=1, maximum=365)
        with self.assertRaises(ValueError):
            parse_bounded_int("bad", name="days", default=30, minimum=1, maximum=365)

        recorder = AccessAnalyticsRecorder(background_worker=False)
        old_event = self._event(
            occurred_at=datetime(2026, 1, 1, 12, 0, tzinfo=timezone.utc)
        )
        current_event = self._event()
        recorder.submit(old_event, db_path=self.db_path, synchronous=True)
        recorder.submit(current_event, db_path=self.db_path, synchronous=True)
        for occurred_at in (
            datetime(2026, 1, 1, 12, 0, tzinfo=timezone.utc),
            FIXED_NOW,
        ):
            for payload in (
                {"kind": "page_view", "route": "/", "navigationType": "hard"},
                {"kind": "web_vital", "route": "/", "metric": "LCP", "value": 1800},
            ):
                recorder.submit(
                    build_web_experience_event(
                        payload,
                        secret_key="analytics-test-secret",
                        remote_addr="203.0.113.10",
                        user_agent="Mozilla/5.0 (Windows NT 10.0)",
                        occurred_at=occurred_at,
                    ),
                    db_path=self.db_path,
                    synchronous=True,
                )

        result = prune_access_analytics(
            self.cache.get_db,
            retention_days=30,
            today=date(2026, 7, 18),
        )
        self.assertGreaterEqual(result["deleted"], 4)
        db = self.cache.get_db()
        try:
            days_by_table = {
                table: [row[0] for row in db.execute(
                    f"SELECT DISTINCT day FROM {table} ORDER BY day"
                ).fetchall()]
                for table in (
                    "access_daily",
                    "web_page_daily",
                    "web_page_visitor_daily",
                    "web_vital_daily",
                )
            }
        finally:
            db.close()
        self.assertTrue(all(days == ["2026-07-18"] for days in days_by_table.values()))

    def test_backup_snapshots_follow_visitor_retention(self):
        recorder = AccessAnalyticsRecorder(background_worker=False)
        recorder.submit(
            self._event(occurred_at=datetime(2026, 1, 1, 12, 0, tzinfo=timezone.utc)),
            db_path=self.db_path,
            synchronous=True,
        )
        recorder.submit(self._event(), db_path=self.db_path, synchronous=True)
        backup_path = self.root / "marketplace_backup_20260718_093000.db"
        self.assertTrue(self.cache.backup_db(backup_path))

        result = prune_access_analytics_backups(
            self.root,
            retention_days=30,
            today=date(2026, 7, 18),
        )

        self.assertEqual(result["errors"], [])
        self.assertEqual(result["backups"], 1)
        backup = sqlite3.connect(str(backup_path))
        try:
            days = [row[0] for row in backup.execute(
                "SELECT day FROM access_visitor_daily ORDER BY day"
            ).fetchall()]
            self.assertEqual(backup.execute("PRAGMA quick_check").fetchone()[0], "ok")
        finally:
            backup.close()
        self.assertEqual(days, ["2026-07-18"])


class AccessAnalyticsApiTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.storage = self.root / "storage"
        (self.storage / "Plugins").mkdir(parents=True)

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_cache_db_path = marketplace_app._cache.db_path
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)
        self.original_testing = marketplace_app.app.config.get("TESTING", False)
        self.original_secret_key = marketplace_app.app.secret_key

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG.update({
            "storage_path": str(self.storage),
            "secret_key": "test-secret-key",
            "upload_auth": {"username": "admin", "password": "secret"},
            "access_analytics_enabled": True,
        })
        marketplace_app.app.secret_key = "test-secret-key"
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.init_db()
        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app._cache._db_path = self.original_cache_db_path
        marketplace_app.app.secret_key = self.original_secret_key
        marketplace_app.app.config["TESTING"] = self.original_testing
        self.temp_dir.cleanup()

    @staticmethod
    def _basic_auth():
        token = base64.b64encode(b"admin:secret").decode("ascii")
        return {"Authorization": f"Basic {token}"}

    def test_request_hook_uses_route_template_and_excludes_sensitive_values(self):
        raw_address = "203.0.113.77"
        raw_agent = "PrivateBrowser/7.7 (iPhone; DeviceSerial=ABC123) Mobile"
        response = self.client.get(
            "/api/plugins/MissingPlugin?private_token=do-not-store",
            headers={"User-Agent": raw_agent, "Referer": "https://example.test/private/path"},
            environ_base={"REMOTE_ADDR": raw_address},
        )
        self.assertEqual(response.status_code, 404)
        expected_response_bytes = int(response.headers.get("Content-Length") or 0)

        for excluded_path in (
            "/api/health",
            "/api/ready",
            "/assets/missing.js",
            "/media/missing.mp4",
        ):
            excluded_response = self.client.get(excluded_path)
            excluded_response.close()

        traffic = self.client.get(
            "/api/admin/stats/traffic?days=1&limit=10",
            headers=self._basic_auth(),
        )
        self.assertEqual(traffic.status_code, 200)
        payload = traffic.get_json()
        self.assertEqual(payload["summary"]["visits"], 1)
        self.assertEqual(payload["summary"]["totalResponseBytes"], expected_response_bytes)
        self.assertEqual(payload["summary"]["errorResponses"], 1)
        self.assertEqual(payload["summary"]["timeZone"], "UTC+08:00")
        self.assertEqual(payload["summary"]["utcOffsetMinutes"], 480)
        self.assertEqual(payload["summary"]["clientErrorResponses"], 1)
        self.assertEqual(payload["summary"]["serverErrorResponses"], 0)
        self.assertEqual(payload["summary"]["unclassifiedErrorResponses"], 0)
        self.assertEqual(payload["topRoutes"][0]["route"], "/api/plugins/<plugin_id>")
        self.assertEqual(payload["topRoutes"][0]["responseBytes"], expected_response_bytes)
        self.assertEqual(payload["topRoutes"][0]["clientErrorResponses"], 1)
        self.assertEqual(payload["clients"][0]["client"], "mobile")

        db = sqlite3.connect(str(marketplace_app.DB_PATH))
        try:
            dump = "\n".join(db.iterdump())
        finally:
            db.close()
        for sensitive in (
            raw_address,
            raw_agent,
            "private_token",
            "do-not-store",
            "/private/path",
            "DeviceSerial",
        ):
            self.assertNotIn(sensitive, dump)

    def test_browser_experience_ingestion_is_same_origin_bounded_and_queryable(self):
        same_origin_headers = {
            "Origin": "http://localhost",
            "Sec-Fetch-Site": "same-origin",
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0)",
        }
        page_view = self.client.post(
            "/api/v1/analytics/events",
            json={
                "kind": "page_view",
                "route": "/plugins/ProjectARVRPro",
                "navigationType": "hard",
            },
            headers=same_origin_headers,
            environ_base={"REMOTE_ADDR": "203.0.113.90"},
        )
        vital = self.client.post(
            "/api/v1/analytics/events",
            json={
                "kind": "web_vital",
                "route": "/plugins/ProjectARVRPro",
                "metric": "LCP",
                "value": 2450.5,
            },
            headers=same_origin_headers,
            environ_base={"REMOTE_ADDR": "203.0.113.90"},
        )
        self.assertEqual(page_view.status_code, 202)
        self.assertTrue(page_view.get_json()["recorded"])
        self.assertEqual(vital.status_code, 202)

        invalid = self.client.post(
            "/api/v1/analytics/events",
            json={
                "kind": "page_view",
                "route": "/",
                "navigationType": "hard",
                "referrer": "https://private.example/path",
            },
            headers=same_origin_headers,
        )
        foreign = self.client.post(
            "/api/v1/analytics/events",
            json={"kind": "page_view", "route": "/", "navigationType": "hard"},
            headers={"Origin": "https://foreign.example", "Sec-Fetch-Site": "cross-site"},
        )
        self.assertEqual(invalid.status_code, 400)
        self.assertEqual(foreign.status_code, 403)

        traffic = self.client.get(
            "/api/admin/stats/traffic?days=1&limit=10",
            headers=self._basic_auth(),
        )
        self.assertEqual(traffic.status_code, 200)
        web = traffic.get_json()["web"]
        self.assertEqual(web["summary"]["pageViews"], 1)
        self.assertEqual(web["topPages"][0]["route"], "/plugins/:pluginId")
        self.assertEqual(web["vitals"][0]["metric"], "LCP")
        self.assertEqual(web["vitals"][0]["samples"], 1)

        db = sqlite3.connect(str(marketplace_app.DB_PATH))
        try:
            dump = "\n".join(db.iterdump())
        finally:
            db.close()
        for sensitive in (
            "203.0.113.90",
            "ProjectARVRPro",
            "private.example",
        ):
            self.assertNotIn(sensitive, dump)

    def test_traffic_requires_stats_read_scope(self):
        from services.api_key_service import create_api_key

        stats_key = create_api_key(
            marketplace_app._cache,
            name="stats-reader",
            scopes="stats:read",
            created_by="test",
        )["key"]
        cache_key = create_api_key(
            marketplace_app._cache,
            name="cache-reader",
            scopes="cache:read",
            created_by="test",
        )["key"]

        unauthenticated = self.client.get("/api/admin/stats/traffic")
        allowed = self.client.get(
            "/api/admin/stats/traffic",
            headers={"Authorization": f"Bearer {stats_key}"},
        )
        forbidden = self.client.get(
            "/api/admin/stats/traffic",
            headers={"Authorization": f"Bearer {cache_key}"},
        )
        self.assertEqual(unauthenticated.status_code, 401)
        self.assertEqual(allowed.status_code, 200)
        self.assertEqual(forbidden.status_code, 403)

    def test_traffic_query_rejects_invalid_ranges(self):
        for query in ("days=0", "days=366", "days=bad", "limit=0", "limit=101"):
            with self.subTest(query=query):
                response = self.client.get(
                    f"/api/admin/stats/traffic?{query}",
                    headers=self._basic_auth(),
                )
                self.assertEqual(response.status_code, 400)

    def test_stats_overview_preserves_fields_and_adds_today_traffic(self):
        self.client.get("/api/plugins")
        response = self.client.get(
            "/api/admin/stats/overview",
            headers=self._basic_auth(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        for old_field in (
            "totalDownloads",
            "downloadsToday",
            "pluginCount",
            "packageCount",
            "latestReleaseVersion",
            "pluginCatalogCached",
            "dbSizeBytes",
        ):
            self.assertIn(old_field, payload)
        self.assertEqual(payload["visitsToday"], 1)
        self.assertIn("uniqueVisitorsToday", payload)
        self.assertIn("avgResponseMsToday", payload)
        self.assertIn("errorResponsesToday", payload)

    def test_stats_overview_uses_reporting_day_for_downloads_today(self):
        offset = reporting_utc_offset_minutes(marketplace_app.CONFIG)
        reporting_day = analytics_calendar_day(utc_offset_minutes=offset)
        start, _ = analytics_calendar_day_utc_bounds(
            reporting_day,
            utc_offset_minutes=offset,
        )
        db = sqlite3.connect(str(marketplace_app.DB_PATH))
        try:
            with db:
                db.execute(
                    "INSERT INTO download_log (plugin_id, version, downloaded_at) "
                    "VALUES (?, ?, ?)",
                    ("before-day", "1.0", (start - timedelta(seconds=1)).strftime("%Y-%m-%d %H:%M:%S")),
                )
                db.execute(
                    "INSERT INTO download_log (plugin_id, version, downloaded_at) "
                    "VALUES (?, ?, ?)",
                    ("inside-day", "1.0", start.strftime("%Y-%m-%d %H:%M:%S")),
                )
        finally:
            db.close()

        response = self.client.get(
            "/api/admin/stats/overview",
            headers=self._basic_auth(),
        )

        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(payload["totalDownloads"], 2)
        self.assertEqual(payload["downloadsToday"], 1)

    def test_testing_requests_follow_current_db_path_without_crossing_databases(self):
        first_db_path = Path(marketplace_app.DB_PATH)
        self.client.get("/api/plugins?database=first")

        second_db_path = self.root / "second-marketplace.db"
        marketplace_app.DB_PATH = second_db_path
        marketplace_app.init_db()
        self.client.get("/api/plugins?database=second")

        counts = []
        for path in (first_db_path, second_db_path):
            db = sqlite3.connect(str(path))
            try:
                row = db.execute("SELECT SUM(visits) FROM access_daily").fetchone()
                counts.append(int(row[0] or 0))
            finally:
                db.close()
        self.assertEqual(counts, [1, 1])


if __name__ == "__main__":
    unittest.main()

from __future__ import annotations

import sqlite3
import unittest

from db.schema_version import CURRENT_SCHEMA_VERSION, ensure_schema_version


class SchemaVersionTests(unittest.TestCase):
    def test_v6_removes_historical_head_bytes_from_route_and_daily_totals(self):
        db = sqlite3.connect(":memory:")
        db.row_factory = sqlite3.Row
        try:
            db.executescript(
                """
                CREATE TABLE schema_version (key TEXT PRIMARY KEY, value INTEGER NOT NULL);
                INSERT INTO schema_version VALUES ('version', 5);
                CREATE TABLE access_daily (
                    day TEXT PRIMARY KEY,
                    total_response_bytes INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE access_route_daily (
                    day TEXT NOT NULL,
                    route TEXT NOT NULL,
                    method TEXT NOT NULL,
                    total_response_bytes INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (day, route, method)
                );
                INSERT INTO access_daily VALUES ('2026-08-10', 1000);
                INSERT INTO access_daily VALUES ('2026-08-11', 500);
                INSERT INTO access_daily VALUES ('2026-08-12', 200);
                INSERT INTO access_route_daily VALUES ('2026-08-10', '/download', 'GET', 600);
                INSERT INTO access_route_daily VALUES ('2026-08-10', '/download', 'HEAD', 400);
                INSERT INTO access_route_daily VALUES ('2026-08-11', '/download', 'HEAD', 700);
                INSERT INTO access_route_daily VALUES ('2026-08-12', '/download', 'GET', 200);
                """
            )

            self.assertEqual(ensure_schema_version(db), CURRENT_SCHEMA_VERSION)
            daily = dict(db.execute(
                "SELECT day, total_response_bytes FROM access_daily ORDER BY day"
            ).fetchall())
            routes = {
                (row["day"], row["method"]): row["total_response_bytes"]
                for row in db.execute(
                    "SELECT day, method, total_response_bytes FROM access_route_daily"
                ).fetchall()
            }

            self.assertEqual(daily, {
                "2026-08-10": 600,
                "2026-08-11": 0,
                "2026-08-12": 200,
            })
            self.assertEqual(routes[("2026-08-10", "GET")], 600)
            self.assertEqual(routes[("2026-08-10", "HEAD")], 0)
            self.assertEqual(routes[("2026-08-11", "HEAD")], 0)
            version = db.execute(
                "SELECT value FROM schema_version WHERE key='version'"
            ).fetchone()[0]
            self.assertEqual(version, CURRENT_SCHEMA_VERSION)

            db.execute("UPDATE schema_version SET value=5 WHERE key='version'")
            db.commit()
            ensure_schema_version(db)
            rerun_daily = dict(db.execute(
                "SELECT day, total_response_bytes FROM access_daily ORDER BY day"
            ).fetchall())
            self.assertEqual(rerun_daily, daily)
        finally:
            db.close()

    def test_v7_preserves_legacy_error_totals_without_guessing_classification(self):
        db = sqlite3.connect(":memory:")
        db.row_factory = sqlite3.Row
        try:
            db.executescript(
                """
                CREATE TABLE schema_version (key TEXT PRIMARY KEY, value INTEGER NOT NULL);
                INSERT INTO schema_version VALUES ('version', 6);
                CREATE TABLE access_daily (
                    day TEXT PRIMARY KEY,
                    error_responses INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE access_route_daily (
                    day TEXT NOT NULL,
                    route TEXT NOT NULL,
                    method TEXT NOT NULL,
                    error_responses INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (day, route, method)
                );
                CREATE TABLE access_client_daily (
                    day TEXT NOT NULL,
                    client_type TEXT NOT NULL,
                    error_responses INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (day, client_type)
                );
                INSERT INTO access_daily VALUES ('2026-08-12', 7);
                INSERT INTO access_route_daily VALUES ('2026-08-12', '/legacy', 'GET', 5);
                INSERT INTO access_client_daily VALUES ('2026-08-12', 'desktop', 3);
                """
            )

            self.assertEqual(ensure_schema_version(db), CURRENT_SCHEMA_VERSION)
            for table, expected_errors in (
                ("access_daily", 7),
                ("access_route_daily", 5),
                ("access_client_daily", 3),
            ):
                with self.subTest(table=table):
                    row = db.execute(
                        f"SELECT error_responses, client_error_responses, "
                        f"server_error_responses FROM {table}"
                    ).fetchone()
                    self.assertEqual(row["error_responses"], expected_errors)
                    self.assertEqual(row["client_error_responses"], 0)
                    self.assertEqual(row["server_error_responses"], 0)

            ensure_schema_version(db)
            self.assertEqual(
                tuple(db.execute(
                    "SELECT error_responses, client_error_responses, "
                    "server_error_responses FROM access_daily"
                ).fetchone()),
                (7, 0, 0),
            )
        finally:
            db.close()

    def test_v8_creates_empty_access_analytics_metadata(self):
        db = sqlite3.connect(":memory:")
        db.row_factory = sqlite3.Row
        try:
            db.executescript(
                """
                CREATE TABLE schema_version (key TEXT PRIMARY KEY, value INTEGER NOT NULL);
                INSERT INTO schema_version VALUES ('version', 7);
                """
            )

            self.assertEqual(ensure_schema_version(db), CURRENT_SCHEMA_VERSION)
            table = db.execute(
                "SELECT name FROM sqlite_master "
                "WHERE type = 'table' AND name = 'access_analytics_metadata'"
            ).fetchone()
            self.assertIsNotNone(table)
            self.assertEqual(
                db.execute("SELECT COUNT(*) FROM access_analytics_metadata").fetchone()[0],
                0,
            )
            ensure_schema_version(db)
            self.assertEqual(
                db.execute("SELECT COUNT(*) FROM access_analytics_metadata").fetchone()[0],
                0,
            )
        finally:
            db.close()

    def test_v9_recovers_running_jobs_and_enforces_single_flight(self):
        db = sqlite3.connect(":memory:")
        db.row_factory = sqlite3.Row
        try:
            db.executescript(
                """
                CREATE TABLE schema_version (key TEXT PRIMARY KEY, value INTEGER NOT NULL);
                INSERT INTO schema_version VALUES ('version', 8);
                CREATE TABLE job_runs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    job_id TEXT NOT NULL,
                    status TEXT DEFAULT 'running',
                    started_at TEXT NOT NULL,
                    finished_at TEXT,
                    duration_ms INTEGER DEFAULT 0,
                    summary TEXT,
                    error TEXT
                );
                INSERT INTO job_runs (job_id, status, started_at)
                VALUES ('cache_cleanup', 'running', '2026-08-10T01:00:00+00:00');
                INSERT INTO job_runs (job_id, status, started_at)
                VALUES ('cache_cleanup', 'running', '2026-08-10T01:00:01+00:00');
                """
            )

            self.assertEqual(ensure_schema_version(db), CURRENT_SCHEMA_VERSION)
            recovered = db.execute(
                "SELECT status, finished_at, duration_ms, summary, error "
                "FROM job_runs ORDER BY id"
            ).fetchall()
            self.assertEqual([row["status"] for row in recovered], ["interrupted", "running"])
            self.assertTrue(recovered[0]["finished_at"])
            self.assertGreaterEqual(recovered[0]["duration_ms"], 0)
            self.assertIn("service restart", recovered[0]["summary"])
            self.assertIn("service process stopped", recovered[0]["error"])
            self.assertIsNone(recovered[1]["finished_at"])
            indexes = {
                row["name"]
                for row in db.execute("PRAGMA index_list(job_runs)").fetchall()
            }
            self.assertIn("idx_job_runs_single_running", indexes)
            self.assertIn("idx_job_runs_job_status_id", indexes)
            with self.assertRaises(sqlite3.IntegrityError):
                db.execute(
                    "INSERT INTO job_runs (job_id, status, started_at) "
                    "VALUES ('cache_cleanup', 'running', '2026-08-12T00:00:01+00:00')"
                )
        finally:
            db.close()


if __name__ == "__main__":
    unittest.main()

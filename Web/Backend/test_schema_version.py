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

    def test_v10_persists_api_key_descriptions_and_indexes_audit_actor(self):
        db = sqlite3.connect(":memory:")
        db.row_factory = sqlite3.Row
        try:
            db.executescript(
                """
                CREATE TABLE schema_version (key TEXT PRIMARY KEY, value INTEGER NOT NULL);
                INSERT INTO schema_version VALUES ('version', 9);
                CREATE TABLE api_keys (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    key_prefix TEXT UNIQUE NOT NULL,
                    key_hash TEXT NOT NULL,
                    scopes TEXT DEFAULT '',
                    created_by TEXT,
                    created_at TEXT NOT NULL,
                    expires_at TEXT,
                    last_used_at TEXT,
                    revoked_at TEXT,
                    is_active INTEGER DEFAULT 1
                );
                CREATE TABLE audit_log (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    actor_type TEXT,
                    actor_id TEXT,
                    action TEXT NOT NULL,
                    target_type TEXT,
                    target_id TEXT,
                    ip TEXT,
                    user_agent TEXT,
                    detail TEXT,
                    created_at TEXT NOT NULL
                );
                """
            )

            self.assertEqual(ensure_schema_version(db), CURRENT_SCHEMA_VERSION)

            columns = {
                row["name"] for row in db.execute("PRAGMA table_info(api_keys)").fetchall()
            }
            indexes = {
                row["name"] for row in db.execute("PRAGMA index_list(audit_log)").fetchall()
            }
            self.assertIn("description", columns)
            self.assertIn("idx_audit_actor", indexes)
            ensure_schema_version(db)
            self.assertEqual(
                db.execute(
                    "SELECT COUNT(*) FROM sqlite_master "
                    "WHERE type = 'index' AND name = 'idx_audit_actor'"
                ).fetchone()[0],
                1,
            )
        finally:
            db.close()

    def test_v11_versions_account_authentication_state(self):
        db = sqlite3.connect(":memory:")
        db.row_factory = sqlite3.Row
        try:
            db.executescript(
                """
                CREATE TABLE schema_version (key TEXT PRIMARY KEY, value INTEGER NOT NULL);
                INSERT INTO schema_version VALUES ('version', 10);
                CREATE TABLE users (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    username TEXT UNIQUE NOT NULL,
                    password_hash TEXT NOT NULL,
                    role TEXT DEFAULT 'user',
                    is_active INTEGER DEFAULT 1,
                    created_at TEXT NOT NULL,
                    updated_at TEXT,
                    last_login_at TEXT
                );
                INSERT INTO users (username, password_hash, created_at)
                VALUES ('legacy-user', 'hash', '2026-08-12T00:00:00+00:00');
                """
            )

            self.assertEqual(ensure_schema_version(db), CURRENT_SCHEMA_VERSION)
            columns = {
                row["name"] for row in db.execute("PRAGMA table_info(users)").fetchall()
            }
            self.assertIn("auth_version", columns)
            self.assertEqual(
                db.execute(
                    "SELECT auth_version FROM users WHERE username = 'legacy-user'"
                ).fetchone()[0],
                0,
            )
            ensure_schema_version(db)
            self.assertEqual(
                sum(
                    row["name"] == "auth_version"
                    for row in db.execute("PRAGMA table_info(users)").fetchall()
                ),
                1,
            )
        finally:
            db.close()

    def test_v12_adds_aggregate_only_web_experience_tables(self):
        db = sqlite3.connect(":memory:")
        db.row_factory = sqlite3.Row
        try:
            db.executescript(
                """
                CREATE TABLE schema_version (key TEXT PRIMARY KEY, value INTEGER NOT NULL);
                INSERT INTO schema_version VALUES ('version', 11);
                """
            )

            self.assertEqual(ensure_schema_version(db), CURRENT_SCHEMA_VERSION)
            tables = {
                row["name"]
                for row in db.execute(
                    "SELECT name FROM sqlite_master WHERE type = 'table'"
                ).fetchall()
            }
            self.assertTrue({
                "web_page_daily",
                "web_page_visitor_daily",
                "web_vital_daily",
            }.issubset(tables))
            page_columns = {
                row["name"]
                for row in db.execute("PRAGMA table_info(web_page_daily)").fetchall()
            }
            vital_columns = {
                row["name"]
                for row in db.execute("PRAGMA table_info(web_vital_daily)").fetchall()
            }
            self.assertIn("spa_navigations", page_columns)
            self.assertIn("poor_samples", vital_columns)

            ensure_schema_version(db)
            self.assertEqual(
                db.execute(
                    "SELECT COUNT(*) FROM sqlite_master "
                    "WHERE type = 'table' AND name LIKE 'web_%'"
                ).fetchone()[0],
                3,
            )
        finally:
            db.close()

    def test_v14_adds_signed_relay_response_envelopes_idempotently(self):
        db = sqlite3.connect(":memory:")
        db.row_factory = sqlite3.Row
        try:
            db.executescript(
                """
                CREATE TABLE schema_version (key TEXT PRIMARY KEY, value INTEGER NOT NULL);
                INSERT INTO schema_version VALUES ('version', 13);
                CREATE TABLE operations_hosts (host_id TEXT PRIMARY KEY);
                CREATE TABLE operations_task_receipts (receipt_id TEXT PRIMARY KEY);
                """
            )

            self.assertEqual(ensure_schema_version(db), CURRENT_SCHEMA_VERSION)
            host_columns = {
                row["name"] for row in db.execute("PRAGMA table_info(operations_hosts)")
            }
            receipt_columns = {
                row["name"]
                for row in db.execute("PRAGMA table_info(operations_task_receipts)")
            }
            self.assertTrue({
                "relay_snapshot_body", "relay_snapshot_signature"
            }.issubset(host_columns))
            self.assertTrue({
                "relay_receipt_body", "relay_receipt_signature"
            }.issubset(receipt_columns))

            ensure_schema_version(db)
            self.assertEqual(
                len([
                    row for row in db.execute("PRAGMA table_info(operations_hosts)")
                    if row["name"].startswith("relay_snapshot_")
                ]),
                2,
            )
        finally:
            db.close()

    def test_v15_adds_encrypted_window_snapshot_metadata_without_ciphertext(self):
        db = sqlite3.connect(":memory:")
        db.row_factory = sqlite3.Row
        try:
            db.executescript(
                """
                PRAGMA foreign_keys=ON;
                CREATE TABLE schema_version (key TEXT PRIMARY KEY, value INTEGER NOT NULL);
                INSERT INTO schema_version VALUES ('version', 14);
                CREATE TABLE operations_tasks (task_id TEXT PRIMARY KEY);
                """
            )

            self.assertEqual(ensure_schema_version(db), CURRENT_SCHEMA_VERSION)
            columns = {
                row["name"]: row["type"]
                for row in db.execute(
                    "PRAGMA table_info(operations_relay_window_snapshots)"
                )
            }
            self.assertEqual(set(columns), {
                "task_id", "host_id", "device_id", "job_id", "sealed_sha256",
                "sealed_bytes", "captured_at", "expires_at", "created_at",
            })
            self.assertNotIn("BLOB", {value.upper() for value in columns.values()})
            indexes = {
                row["name"] for row in db.execute(
                    "PRAGMA index_list(operations_relay_window_snapshots)"
                )
            }
            self.assertIn("idx_ops_relay_window_snapshots_expiry", indexes)

            ensure_schema_version(db)
            self.assertEqual(db.execute(
                "SELECT COUNT(*) FROM sqlite_master "
                "WHERE type='table' AND name='operations_relay_window_snapshots'"
            ).fetchone()[0], 1)
        finally:
            db.close()


if __name__ == "__main__":
    unittest.main()

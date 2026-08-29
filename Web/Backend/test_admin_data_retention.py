"""Retention contracts for administrator audit data and DB snapshots."""

from __future__ import annotations

import sqlite3
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path
from unittest.mock import patch

from db_cache import CacheManager
from services.admin_data_retention import (
    list_manual_db_backups,
    parse_admin_retention_config,
    prune_audit_log,
    prune_audit_log_backups,
    prune_manual_db_backups,
    run_admin_data_retention,
    scrub_account_security_backups,
)


class AdminDataRetentionTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.root = Path(self._temp.name)
        self.cache = CacheManager(self.root / "marketplace.db")
        self.cache.init_db()

    def tearDown(self):
        self._temp.cleanup()

    def _insert_audit(self, created_at: str, action: str):
        db = self.cache.get_db()
        try:
            db.execute(
                "INSERT INTO audit_log (action, created_at) VALUES (?, ?)",
                (action, created_at),
            )
            db.commit()
        finally:
            db.close()

    def _insert_transient_account_security_state(self):
        timestamp = "2026-08-12T10:00:00+00:00"
        db = self.cache.get_db()
        try:
            user_id = db.execute(
                """INSERT INTO users
                       (username, password_hash, role, is_active, created_at)
                   VALUES ('backup-user', 'restorable-password-hash', 'user', 1, ?)""",
                (timestamp,),
            ).lastrowid
            db.execute(
                """INSERT INTO user_sessions
                       (id, user_id, auth_version, ip_address, user_agent,
                        created_at, last_seen_at)
                   VALUES ('restored-session', ?, 0, '192.0.2.10',
                           'private browser', ?, ?)""",
                (user_id, timestamp, timestamp),
            )
            db.execute(
                """INSERT INTO login_attempts
                       (username_key, ip_address, failed_count,
                        window_started_at, last_failed_at)
                   VALUES ('backup-user', '192.0.2.11', 2, ?, ?)""",
                (timestamp, timestamp),
            )
            db.execute(
                """INSERT INTO registration_rate_limits
                       (ip_address, attempt_count, attempt_window_started_at,
                        success_count, pending_count, success_window_started_at,
                        last_attempt_at)
                   VALUES ('192.0.2.12', 1, ?, 1, 0, ?, ?)""",
                (timestamp, timestamp, timestamp),
            )
            db.execute(
                """INSERT INTO password_recovery_rate_limits
                       (ip_address, attempt_count, window_started_at, last_attempt_at)
                   VALUES ('192.0.2.14', 1, ?, ?)""",
                (timestamp, timestamp),
            )
            db.execute(
                """INSERT INTO password_recovery_requests
                       (user_id, first_requested_at, last_requested_at, last_ip)
                   VALUES (?, ?, ?, '192.0.2.13')""",
                (user_id, timestamp, timestamp),
            )
            db.commit()
        finally:
            db.close()

    def test_audit_retention_uses_strict_utc_cutoff(self):
        self._insert_audit("2026-07-12T23:59:59+00:00", "expired")
        self._insert_audit("2026-07-13T00:00:00+00:00", "at-cutoff")
        self._insert_audit("2026-08-01T00:00:00+00:00", "current")

        result = prune_audit_log(
            self.cache.get_db,
            retention_days=30,
            now=datetime(2026, 8, 12, tzinfo=timezone.utc),
        )

        self.assertEqual(result["deleted"], 1)
        self.assertEqual(result["cutoff"], "2026-07-13T00:00:00+00:00")
        db = self.cache.get_db()
        try:
            actions = [row[0] for row in db.execute(
                "SELECT action FROM audit_log ORDER BY id"
            ).fetchall()]
        finally:
            db.close()
        self.assertEqual(actions, ["at-cutoff", "current"])

    def test_audit_retention_delete_uses_created_at_index(self):
        db = self.cache.get_db()
        try:
            plan = db.execute(
                "EXPLAIN QUERY PLAN DELETE FROM audit_log WHERE created_at < ?",
                ("2026-01-01T00:00:00+00:00",),
            ).fetchall()
        finally:
            db.close()
        details = " ".join(str(row[3]) for row in plan)
        self.assertIn("idx_audit_created", details)

    def test_snapshot_audit_retention_preserves_integrity(self):
        self._insert_audit("2000-01-01T00:00:00+00:00", "expired")
        self._insert_audit("2026-08-01T00:00:00+00:00", "current")
        backup = self.root / "marketplace_backup_20260812_120000.db"
        self.assertTrue(self.cache.backup_db(backup))

        result = prune_audit_log_backups(
            self.root,
            retention_days=30,
            now=datetime(2026, 8, 12, tzinfo=timezone.utc),
        )

        self.assertEqual(result["errors"], [])
        self.assertEqual(result["backups"], 1)
        self.assertEqual(result["deleted"], 1)
        db = sqlite3.connect(str(backup))
        try:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM audit_log").fetchone()[0], 1)
            self.assertEqual(db.execute("PRAGMA quick_check").fetchone()[0], "ok")
        finally:
            db.close()

    def test_security_scrub_cleans_existing_backups_without_touching_live_state(self):
        self._insert_transient_account_security_state()
        backup = self.root / "marketplace_backup_20260812_120000.db"
        self.assertTrue(self.cache.backup_db(backup))

        result = scrub_account_security_backups(self.root)

        self.assertEqual(result["errors"], [])
        self.assertEqual(result["backups"], 1)
        self.assertEqual(result["deleted"], 5)
        self.assertEqual(result["accountsInvalidated"], 1)
        self.assertEqual(
            result["results"][0]["tables"],
            {
                "user_sessions": 1,
                "login_attempts": 1,
                "registration_rate_limits": 1,
                "password_recovery_rate_limits": 1,
                "password_recovery_requests": 1,
            },
        )
        snapshot = sqlite3.connect(str(backup))
        try:
            for table_name in result["results"][0]["tables"]:
                self.assertEqual(
                    snapshot.execute(f'SELECT COUNT(*) FROM "{table_name}"').fetchone()[0],
                    0,
                )
            self.assertEqual(
                snapshot.execute("SELECT auth_version FROM users").fetchone()[0],
                1,
            )
            self.assertEqual(snapshot.execute("PRAGMA quick_check").fetchone()[0], "ok")
        finally:
            snapshot.close()

        repeated = scrub_account_security_backups(self.root)
        self.assertEqual(repeated["deleted"], 0)
        self.assertEqual(repeated["accountsInvalidated"], 0)

        live = self.cache.get_db()
        try:
            self.assertEqual(
                live.execute("SELECT auth_version FROM users").fetchone()[0],
                0,
            )
            for table_name in result["results"][0]["tables"]:
                self.assertEqual(
                    live.execute(f'SELECT COUNT(*) FROM "{table_name}"').fetchone()[0],
                    1,
                )
        finally:
            live.close()

    def test_manual_backup_retention_protects_current_and_unclassified_files(self):
        recognized = []
        for second in range(1, 5):
            path = self.root / f"marketplace_backup_20260812_12000{second}.db"
            path.write_bytes(bytes([second]) * second)
            recognized.append(path)
        unclassified = self.root / "marketplace_backup_manual.db"
        unclassified.write_bytes(b"manual")

        result = prune_manual_db_backups(
            self.root,
            keep_count=2,
            protected_paths=(recognized[0],),
        )

        self.assertEqual(result["status"], "success")
        self.assertEqual(result["beforeCount"], 4)
        self.assertEqual(result["afterCount"], 3)
        self.assertEqual(result["removedCount"], 1)
        self.assertEqual(result["removedBytes"], 2)
        self.assertEqual(result["preservedUnclassified"], 1)
        self.assertTrue(recognized[0].exists())
        self.assertFalse(recognized[1].exists())
        self.assertTrue(recognized[2].exists())
        self.assertTrue(recognized[3].exists())
        self.assertTrue(unclassified.exists())

    def test_manual_backup_inventory_is_sorted_and_path_free(self):
        older = self.root / "marketplace_backup_20260811_120000.db"
        newer = self.root / "marketplace_backup_20260812_130000.db"
        older.write_bytes(b"old")
        newer.write_bytes(b"newer")
        (self.root / "marketplace_backup_manual.db").write_bytes(b"ignored")

        result = list_manual_db_backups(self.root)

        self.assertEqual([item["name"] for item in result], [newer.name, older.name])
        self.assertEqual(result[0]["created_at"], "2026-08-12T13:00:00+00:00")
        self.assertEqual(result[0]["size_bytes"], 5)
        self.assertNotIn("path", result[0])

    def test_retention_config_is_bounded(self):
        self.assertEqual(parse_admin_retention_config({}), (365, 10))
        self.assertEqual(
            parse_admin_retention_config({
                "audit_log_retention_days": "90",
                "admin_db_backup_keep_count": "5",
            }),
            (90, 5),
        )
        with self.assertRaisesRegex(ValueError, "audit_log_retention_days"):
            parse_admin_retention_config({"audit_log_retention_days": 0})
        with self.assertRaisesRegex(ValueError, "admin_db_backup_keep_count"):
            parse_admin_retention_config({"admin_db_backup_keep_count": 1})

    def test_invalid_snapshot_is_reported_and_protected_during_rotation(self):
        invalid = self.root / "marketplace_backup_20000101_000000.db"
        invalid.write_bytes(b"not sqlite")
        for second in range(1, 4):
            self.assertTrue(self.cache.backup_db(
                self.root / f"marketplace_backup_20260812_12000{second}.db"
            ))

        result = run_admin_data_retention(
            self.cache.get_db,
            self.root,
            {
                "audit_log_retention_days": 30,
                "admin_db_backup_keep_count": 2,
            },
            now=datetime(2026, 8, 12, tzinfo=timezone.utc),
        )

        self.assertEqual(result["status"], "error")
        self.assertEqual(len(result["errors"]), 1)
        self.assertTrue(invalid.exists())
        self.assertEqual(result["backupFiles"]["removedCount"], 1)
        self.assertEqual(len(list(self.root.glob("marketplace_backup_*.db"))), 3)

    def test_scheduler_registers_and_runs_complete_admin_retention(self):
        from services.scheduler import ensure_default_jobs, run_job_now

        ensure_default_jobs(self.cache)
        self._insert_audit("2000-01-01T00:00:00+00:00", "expired")
        self._insert_audit("2999-01-01T00:00:00+00:00", "current")
        for second in range(1, 4):
            self.assertTrue(self.cache.backup_db(
                self.root / f"marketplace_backup_20260812_12000{second}.db"
            ))

        result = run_job_now(
            self.cache,
            self.root,
            lambda: {
                "audit_log_retention_days": 30,
                "admin_db_backup_keep_count": 2,
            },
            self.cache.get_db,
            "admin_data_retention",
        )

        self.assertIsNotNone(self.cache.jobs.get("admin_data_retention"))
        self.assertEqual(result["status"], "success")
        self.assertIn("Pruned 1 audit rows", result["summary"])
        self.assertIn("removed 1 old database backups", result["summary"])
        self.assertEqual(len(list(self.root.glob("marketplace_backup_*.db"))), 2)
        db = self.cache.get_db()
        try:
            self.assertEqual(db.execute(
                "SELECT COUNT(*) FROM audit_log WHERE action = 'expired'"
            ).fetchone()[0], 0)
        finally:
            db.close()

    def test_scheduler_registers_and_runs_daily_database_backup(self):
        from services.database_backup import create_database_backup
        from services.scheduler import ensure_default_jobs, run_job_now

        config = {
            "access_analytics_retention_days": 30,
            "reporting_utc_offset_minutes": 480,
            "audit_log_retention_days": 30,
            "admin_db_backup_keep_count": 3,
        }
        ensure_default_jobs(self.cache)
        backup_job = self.cache.jobs.get("database_backup")
        self.assertIsNotNone(backup_job)
        self.assertEqual(backup_job["job_type"], "database_backup")
        self.assertEqual(backup_job["interval_seconds"], 86400)

        self._insert_audit("2000-01-01T00:00:00+00:00", "expired")
        self._insert_audit("2999-01-01T00:00:00+00:00", "current")
        self._insert_transient_account_security_state()
        fixed_now = datetime(2026, 8, 12, 12, 0, tzinfo=timezone.utc)
        first = create_database_backup(self.cache, config, now=fixed_now)
        second = create_database_backup(self.cache, config, now=fixed_now)
        self.assertEqual(first["backup_name"], "marketplace_backup_20260812_120000.db")
        self.assertEqual(second["backup_name"], "marketplace_backup_20260812_120001.db")
        self.assertGreater(first["backup_size_bytes"], 0)
        self.assertEqual(first["security_rows_deleted"], 5)
        self.assertEqual(second["security_rows_deleted"], 5)
        self.assertEqual(first["security_accounts_invalidated"], 1)
        self.assertEqual(second["security_accounts_invalidated"], 1)

        snapshot = sqlite3.connect(first["backup_path"])
        try:
            actions = [row[0] for row in snapshot.execute(
                "SELECT action FROM audit_log ORDER BY id"
            ).fetchall()]
            security_counts = {
                table_name: snapshot.execute(
                    f'SELECT COUNT(*) FROM "{table_name}"'
                ).fetchone()[0]
                for table_name in (
                    "user_sessions",
                    "login_attempts",
                    "registration_rate_limits",
                    "password_recovery_rate_limits",
                    "password_recovery_requests",
                )
            }
            restored_user = snapshot.execute(
                "SELECT username, password_hash, auth_version FROM users"
            ).fetchone()
            self.assertEqual(snapshot.execute("PRAGMA quick_check").fetchone()[0], "ok")
        finally:
            snapshot.close()
        self.assertEqual(actions, ["current"])
        self.assertEqual(set(security_counts.values()), {0})
        self.assertEqual(
            restored_user,
            ("backup-user", "restorable-password-hash", 1),
        )

        result = run_job_now(
            self.cache,
            self.root,
            lambda: config,
            self.cache.get_db,
            "database_backup",
        )
        self.assertEqual(result["status"], "success")
        self.assertIn("Created marketplace_backup_", result["summary"])
        self.assertIn("scrubbed 5 transient security rows", result["summary"])
        self.assertEqual(len(list_manual_db_backups(self.root)), 3)

    def test_new_backup_is_removed_when_security_scrub_fails(self):
        from services.database_backup import create_database_backup

        with patch(
            "services.account_security_cleanup.scrub_account_security_database",
            side_effect=sqlite3.DatabaseError("security scrub failed"),
        ):
            with self.assertRaisesRegex(
                RuntimeError,
                "new snapshot failed privacy cleanup",
            ):
                create_database_backup(
                    self.cache,
                    {
                        "access_analytics_retention_days": 30,
                        "audit_log_retention_days": 30,
                        "admin_db_backup_keep_count": 3,
                    },
                    now=datetime(2026, 8, 12, 12, 0, tzinfo=timezone.utc),
                )

        self.assertEqual(list(self.root.glob("marketplace_backup_*.db")), [])


if __name__ == "__main__":
    unittest.main()

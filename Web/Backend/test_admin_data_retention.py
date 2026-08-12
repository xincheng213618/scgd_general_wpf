"""Retention contracts for administrator audit data and DB snapshots."""

from __future__ import annotations

import sqlite3
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path

from db_cache import CacheManager
from services.admin_data_retention import (
    list_manual_db_backups,
    parse_admin_retention_config,
    prune_audit_log,
    prune_audit_log_backups,
    prune_manual_db_backups,
    run_admin_data_retention,
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


if __name__ == "__main__":
    unittest.main()

"""Repository contracts for scheduled jobs and job-run history."""

from __future__ import annotations

import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path

from db_cache import CacheManager


class JobRepositoryTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.cache = CacheManager(Path(self._temp.name) / "marketplace.db")
        self.cache.init_db()
        self.jobs = self.cache.jobs
        self.defaults = [
            {
                "id": "z-last",
                "name": "Last",
                "job_type": "test",
                "interval_seconds": 60,
                "config": "{}",
            },
            {
                "id": "a-first",
                "name": "First",
                "job_type": "test",
                "interval_seconds": 0,
                "config": "{}",
            },
        ]
        self.jobs.ensure_defaults(self.defaults, "2026-08-10T00:00:00+00:00")

    def tearDown(self):
        self._temp.cleanup()

    def test_list_is_sorted_and_keeps_null_latest_run(self):
        jobs = self.jobs.list_with_latest_runs()

        self.assertEqual([job["id"] for job in jobs], ["a-first", "z-last"])
        self.assertIsNone(jobs[0]["updated_at"])
        self.assertIsNone(jobs[0]["latest_run"])

    def test_list_attaches_only_latest_run(self):
        first = self.jobs.start_run("a-first", "2026-08-10T01:00:00+00:00")
        self.jobs.complete_run(
            first,
            "a-first",
            status="success",
            finished_at="2026-08-10T01:00:01+00:00",
            duration_ms=1000,
            summary="first",
            error="",
            now_epoch_seconds=0,
        )
        latest = self.jobs.start_run("a-first", "2026-08-10T02:00:00+00:00")

        job = self.jobs.list_with_latest_runs()[0]

        self.assertEqual(job["latest_run"]["id"], latest)
        self.assertEqual(job["latest_run"]["status"], "running")

    def test_enable_disable_is_transactional_and_reports_missing_job(self):
        self.assertTrue(
            self.jobs.set_enabled(
                "a-first", False, "2026-08-10T03:00:00+00:00"
            )
        )
        self.assertEqual(self.jobs.get("a-first")["enabled"], 0)
        self.assertEqual(
            self.jobs.get("a-first")["updated_at"],
            "2026-08-10T03:00:00+00:00",
        )
        self.assertFalse(
            self.jobs.set_enabled(
                "missing", True, "2026-08-10T03:00:01+00:00"
            )
        )

    def test_complete_run_updates_history_and_next_run_in_one_transaction(self):
        run_id = self.jobs.start_run("z-last", "2026-08-10T04:00:00+00:00")
        self.jobs.complete_run(
            run_id,
            "z-last",
            status="error",
            finished_at="2026-08-10T04:00:02+00:00",
            duration_ms=2000,
            summary="failed",
            error="boom",
            now_epoch_seconds=0,
        )

        run = self.jobs.recent_runs(1)[0]
        job = self.jobs.get("z-last")
        self.assertEqual(run["status"], "error")
        self.assertEqual(run["error"], "boom")
        self.assertEqual(run["duration_ms"], 2000)
        self.assertEqual(
            job["next_run_at"],
            datetime.fromtimestamp(60, tz=timezone.utc).isoformat(),
        )


if __name__ == "__main__":
    unittest.main()

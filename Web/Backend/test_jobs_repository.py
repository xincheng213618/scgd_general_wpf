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
        self.assertEqual(
            jobs[0]["run_counts"],
            {"total": 0, "success": 0, "error": 0, "interrupted": 0, "running": 0},
        )

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
        self.assertEqual(job["run_counts"]["total"], 2)
        self.assertEqual(job["run_counts"]["success"], 1)
        self.assertEqual(job["run_counts"]["running"], 1)

    def test_start_run_is_single_flight_per_job(self):
        first = self.jobs.start_run("a-first", "2026-08-10T01:00:00+00:00")
        duplicate = self.jobs.start_run("a-first", "2026-08-10T01:00:01+00:00")

        self.assertIsInstance(first, int)
        self.assertIsNone(duplicate)
        self.assertEqual(self.jobs.list_runs_page(
            "a-first", status="running", limit=20, offset=0
        )["total"], 1)

    def test_run_history_page_filters_and_paginates(self):
        for index, status in enumerate(("success", "error", "success"), start=1):
            run_id = self.jobs.start_run(
                "a-first", f"2026-08-10T0{index}:00:00+00:00"
            )
            self.assertIsNotNone(run_id)
            self.jobs.complete_run(
                run_id,
                "a-first",
                status=status,
                finished_at=f"2026-08-10T0{index}:00:01+00:00",
                duration_ms=index * 100,
                summary=f"run {index}",
                error="boom" if status == "error" else "",
                now_epoch_seconds=0,
            )

        page = self.jobs.list_runs_page(
            "a-first", status="success", limit=1, offset=1
        )

        self.assertEqual(page["total"], 2)
        self.assertEqual(page["limit"], 1)
        self.assertEqual(page["offset"], 1)
        self.assertEqual([item["summary"] for item in page["items"]], ["run 1"])

    def test_recover_running_runs_marks_abandoned_records_interrupted(self):
        run_id = self.jobs.start_run("a-first", "2026-08-10T01:00:00+00:00")

        recovered = self.jobs.recover_running_runs("2026-08-10T01:00:02+00:00")
        run = self.jobs.list_runs_page(
            "a-first", status="interrupted", limit=20, offset=0
        )["items"][0]

        self.assertEqual(recovered, 1)
        self.assertEqual(run["id"], run_id)
        self.assertEqual(run["status"], "interrupted")
        self.assertEqual(run["finished_at"], "2026-08-10T01:00:02+00:00")
        self.assertGreaterEqual(run["duration_ms"], 1999)
        self.assertIn("service process stopped", run["error"])
        self.assertIsInstance(
            self.jobs.start_run("a-first", "2026-08-10T01:00:03+00:00"),
            int,
        )

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

    def test_prune_history_keeps_latest_and_running_runs(self):
        db = self.cache.get_db()
        try:
            rows = [
                ("a-first", "success", "2026-01-01T00:00:00+00:00"),
                ("a-first", "running", "2026-01-02T00:00:00+00:00"),
                ("a-first", "error", "2026-01-03T00:00:00+00:00"),
                ("z-last", "success", "2026-01-01T00:00:00+00:00"),
                ("z-last", "success", "2026-08-01T00:00:00+00:00"),
            ]
            db.executemany(
                "INSERT INTO job_runs (job_id, status, started_at) VALUES (?, ?, ?)",
                rows,
            )
            db.commit()
        finally:
            db.close()

        deleted = self.jobs.prune_history_before("2026-07-01T00:00:00+00:00")

        runs = sorted(self.jobs.recent_runs(10), key=lambda run: run["id"])
        self.assertEqual(deleted, 2)
        self.assertEqual(
            [(run["job_id"], run["status"]) for run in runs],
            [
                ("a-first", "running"),
                ("a-first", "error"),
                ("z-last", "success"),
            ],
        )

    def test_scheduler_registers_and_runs_configured_retention(self):
        from services.scheduler import ensure_default_jobs, run_job_now

        ensure_default_jobs(self.cache)

        db = self.cache.get_db()
        try:
            db.execute(
                "INSERT INTO job_runs (job_id, status, started_at) VALUES (?, ?, ?)",
                ("a-first", "success", "2000-01-01T00:00:00+00:00"),
            )
            db.execute(
                "INSERT INTO job_runs (job_id, status, started_at) VALUES (?, ?, ?)",
                ("a-first", "success", "2999-01-01T00:00:00+00:00"),
            )
            db.commit()
        finally:
            db.close()

        result = run_job_now(
            self.cache,
            Path(self._temp.name),
            lambda: {"job_run_retention_days": 45},
            self.cache.get_db,
            "job_history_retention",
        )

        self.assertIsNotNone(self.jobs.get("job_history_retention"))
        self.assertEqual(result["status"], "success")
        self.assertIn("Pruned 1 job runs", result["summary"])
        self.assertIn("45 days retained", result["summary"])

    def test_scheduler_skips_duplicate_manual_run(self):
        from services.scheduler import ensure_default_jobs, run_job_now

        ensure_default_jobs(self.cache)
        active_run = self.jobs.start_run(
            "cache_cleanup", "2026-08-10T01:00:00+00:00"
        )

        result = run_job_now(
            self.cache,
            Path(self._temp.name),
            lambda: {},
            self.cache.get_db,
            "cache_cleanup",
        )

        self.assertIsNotNone(active_run)
        self.assertEqual(result["status"], "skipped")
        self.assertIsNone(result["run_id"])
        self.assertEqual(result["error"], "Job is already running")


if __name__ == "__main__":
    unittest.main()

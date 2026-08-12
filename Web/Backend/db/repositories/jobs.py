"""SQLite adapter for scheduled jobs and job-run history."""

from __future__ import annotations

import sqlite3
from collections.abc import Callable
from datetime import datetime, timezone
from typing import Any


class SqliteJobRepository:
    def __init__(self, connection_factory: Callable[[], sqlite3.Connection]):
        self._connection_factory = connection_factory

    def ensure_defaults(self, jobs: list[dict[str, Any]], now: str) -> None:
        db = self._connection_factory()
        try:
            for job in jobs:
                db.execute(
                    """INSERT OR IGNORE INTO scheduled_jobs
                       (id, name, job_type, enabled, interval_seconds, next_run_at, config, created_at)
                       VALUES (?, ?, ?, 1, ?, ?, ?, ?)""",
                    (
                        job["id"],
                        job["name"],
                        job["job_type"],
                        job["interval_seconds"],
                        now,
                        job["config"],
                        now,
                    ),
                )
            db.commit()
        except Exception as exc:
            db.rollback()
            print(f"[scheduler] ensure_default_jobs failed: {exc}")
        finally:
            db.close()

    def list_with_latest_runs(self) -> list[dict[str, Any]]:
        db = self._connection_factory()
        try:
            rows = db.execute("SELECT * FROM scheduled_jobs ORDER BY id").fetchall()
            jobs = [dict(row) for row in rows]
            for job in jobs:
                run = db.execute(
                    "SELECT * FROM job_runs WHERE job_id = ? ORDER BY id DESC LIMIT 1",
                    (job["id"],),
                ).fetchone()
                job["latest_run"] = dict(run) if run else None
            return jobs
        finally:
            db.close()

    def get(self, job_id: str) -> dict[str, Any] | None:
        db = self._connection_factory()
        try:
            row = db.execute(
                "SELECT * FROM scheduled_jobs WHERE id = ?",
                (job_id,),
            ).fetchone()
            return dict(row) if row else None
        finally:
            db.close()

    def set_enabled(self, job_id: str, enabled: bool, updated_at: str) -> bool:
        db = self._connection_factory()
        try:
            cursor = db.execute(
                "UPDATE scheduled_jobs SET enabled = ?, updated_at = ? WHERE id = ?",
                (1 if enabled else 0, updated_at, job_id),
            )
            db.commit()
            return cursor.rowcount != 0
        except Exception:
            db.rollback()
            raise
        finally:
            db.close()

    def start_run(self, job_id: str, started_at: str) -> int:
        db = self._connection_factory()
        try:
            cursor = db.execute(
                "INSERT INTO job_runs (job_id, status, started_at) VALUES (?, 'running', ?)",
                (job_id, started_at),
            )
            db.commit()
            return int(cursor.lastrowid)
        except Exception:
            db.rollback()
            raise
        finally:
            db.close()

    def complete_run(
        self,
        run_id: int,
        job_id: str,
        *,
        status: str,
        finished_at: str,
        duration_ms: int,
        summary: str,
        error: str,
        now_epoch_seconds: float,
    ) -> None:
        db = self._connection_factory()
        try:
            db.execute(
                """UPDATE job_runs SET status = ?, finished_at = ?, duration_ms = ?,
                                         summary = ?, error = ?
                   WHERE id = ?""",
                (status, finished_at, duration_ms, summary, error, run_id),
            )
            job = db.execute(
                "SELECT interval_seconds FROM scheduled_jobs WHERE id = ?",
                (job_id,),
            ).fetchone()
            if job and job["interval_seconds"] and job["interval_seconds"] > 0:
                next_run_at = datetime.fromtimestamp(
                    now_epoch_seconds + job["interval_seconds"],
                    tz=timezone.utc,
                ).isoformat()
                db.execute(
                    "UPDATE scheduled_jobs SET next_run_at = ?, updated_at = ? WHERE id = ?",
                    (next_run_at, finished_at, job_id),
                )
            db.commit()
        except Exception as exc:
            db.rollback()
            print(f"[scheduler] failed to update job run: {exc}")
        finally:
            db.close()

    def list_enabled(self) -> list[dict[str, Any]]:
        db = self._connection_factory()
        try:
            rows = db.execute(
                "SELECT * FROM scheduled_jobs WHERE enabled = 1"
            ).fetchall()
            return [dict(row) for row in rows]
        finally:
            db.close()

    def has_successful_run(self, job_id: str) -> bool:
        db = self._connection_factory()
        try:
            row = db.execute(
                """SELECT COUNT(*) AS cnt FROM job_runs
                   WHERE job_id = ? AND status = 'success'""",
                (job_id,),
            ).fetchone()
            return bool(row and row["cnt"] > 0)
        finally:
            db.close()

    def recent_runs(self, limit: int) -> list[dict[str, Any]]:
        db = self._connection_factory()
        try:
            rows = db.execute(
                "SELECT * FROM job_runs ORDER BY id DESC LIMIT ?",
                (limit,),
            ).fetchall()
            return [dict(row) for row in rows]
        finally:
            db.close()

    def prune_history_before(self, cutoff: str) -> int:
        """Delete old completed runs while keeping every job's latest state."""
        db = self._connection_factory()
        try:
            cursor = db.execute(
                """DELETE FROM job_runs
                   WHERE started_at < ?
                     AND status <> 'running'
                     AND id NOT IN (
                         SELECT MAX(id) FROM job_runs GROUP BY job_id
                     )""",
                (cutoff,),
            )
            db.commit()
            return cursor.rowcount
        except Exception:
            db.rollback()
            raise
        finally:
            db.close()

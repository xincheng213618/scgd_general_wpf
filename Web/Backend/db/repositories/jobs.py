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
                    """INSERT INTO scheduled_jobs
                       (id, name, job_type, enabled, interval_seconds, next_run_at, config, created_at)
                       VALUES (?, ?, ?, 1, ?, ?, ?, ?)
                       ON CONFLICT(id) DO UPDATE SET
                           name = excluded.name,
                           job_type = excluded.job_type""",
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
            run_counts: dict[str, dict[str, int]] = {}
            for row in db.execute(
                """SELECT job_id, status, COUNT(*) AS count
                   FROM job_runs GROUP BY job_id, status"""
            ).fetchall():
                counts = run_counts.setdefault(
                    row["job_id"],
                    {"total": 0, "success": 0, "error": 0, "interrupted": 0, "running": 0},
                )
                count = int(row["count"] or 0)
                counts["total"] += count
                if row["status"] in counts:
                    counts[row["status"]] += count
            for job in jobs:
                run = db.execute(
                    "SELECT * FROM job_runs WHERE job_id = ? ORDER BY id DESC LIMIT 1",
                    (job["id"],),
                ).fetchone()
                job["latest_run"] = dict(run) if run else None
                job["run_counts"] = run_counts.get(
                    job["id"],
                    {"total": 0, "success": 0, "error": 0, "interrupted": 0, "running": 0},
                )
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

    def start_run(self, job_id: str, started_at: str) -> int | None:
        db = self._connection_factory()
        try:
            cursor = db.execute(
                "INSERT INTO job_runs (job_id, status, started_at) VALUES (?, 'running', ?)",
                (job_id, started_at),
            )
            db.commit()
            return int(cursor.lastrowid)
        except sqlite3.IntegrityError:
            db.rollback()
            running = db.execute(
                "SELECT 1 FROM job_runs WHERE job_id = ? AND status = 'running' LIMIT 1",
                (job_id,),
            ).fetchone()
            if running is not None:
                return None
            raise
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

    def list_runs_page(
        self,
        job_id: str,
        *,
        status: str | None,
        limit: int,
        offset: int,
    ) -> dict[str, Any]:
        db = self._connection_factory()
        try:
            where = "job_id = ?"
            parameters: list[Any] = [job_id]
            if status:
                where += " AND status = ?"
                parameters.append(status)
            total = int(db.execute(
                f"SELECT COUNT(*) FROM job_runs WHERE {where}",
                parameters,
            ).fetchone()[0])
            rows = db.execute(
                f"""SELECT * FROM job_runs WHERE {where}
                    ORDER BY id DESC LIMIT ? OFFSET ?""",
                [*parameters, limit, offset],
            ).fetchall()
            return {
                "items": [dict(row) for row in rows],
                "total": total,
                "limit": limit,
                "offset": offset,
            }
        finally:
            db.close()

    def recover_running_runs(self, finished_at: str) -> int:
        db = self._connection_factory()
        try:
            cursor = db.execute(
                """
                UPDATE job_runs
                SET status = 'interrupted',
                    finished_at = ?,
                    duration_ms = MAX(
                        0,
                        COALESCE(
                            CAST((julianday(?) - julianday(started_at)) * 86400000 AS INTEGER),
                            duration_ms,
                            0
                        )
                    ),
                    summary = CASE
                        WHEN COALESCE(summary, '') = '' THEN 'Interrupted by service restart'
                        ELSE summary
                    END,
                    error = CASE
                        WHEN COALESCE(error, '') = '' THEN 'The previous service process stopped before this run completed.'
                        ELSE error
                    END
                WHERE status = 'running'
                """,
                (finished_at, finished_at),
            )
            db.commit()
            return int(cursor.rowcount)
        except Exception:
            db.rollback()
            raise
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

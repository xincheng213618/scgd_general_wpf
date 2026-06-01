"""
Lightweight task scheduler for ColorVision Marketplace.

Provides periodic background jobs for:
  - plugin_index_check: verify Plugins directory signature
  - cache_cleanup: delete expired cache_entry rows
  - startup_index_check: ensure plugin_index is populated
"""

from __future__ import annotations

import threading
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

from db_cache import CacheManager, now_iso


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


# ---------------------------------------------------------------------------
# Job registry
# ---------------------------------------------------------------------------

DEFAULT_JOBS = [
    {
        "id": "plugin_index_check",
        "name": "Plugin Index Check",
        "job_type": "index_check",
        "interval_seconds": 300,
        "config": "{}",
    },
    {
        "id": "release_index_check",
        "name": "Release Index Check",
        "job_type": "index_check",
        "interval_seconds": 600,
        "config": "{}",
    },
    {
        "id": "update_index_check",
        "name": "Update Index Check",
        "job_type": "index_check",
        "interval_seconds": 600,
        "config": "{}",
    },
    {
        "id": "tool_index_check",
        "name": "Tool Index Check",
        "job_type": "index_check",
        "interval_seconds": 600,
        "config": "{}",
    },
    {
        "id": "cache_cleanup",
        "name": "Cache Cleanup",
        "job_type": "cache_cleanup",
        "interval_seconds": 3600,
        "config": "{}",
    },
    {
        "id": "startup_index_check",
        "name": "Startup Index Check",
        "job_type": "startup_check",
        "interval_seconds": 0,  # run once
        "config": "{}",
    },
]


def ensure_default_jobs(cache: CacheManager):
    """Register default scheduled jobs if they don't exist."""
    now = _now_iso()
    db = cache.get_db()
    try:
        for job in DEFAULT_JOBS:
            db.execute(
                """INSERT OR IGNORE INTO scheduled_jobs
                   (id, name, job_type, enabled, interval_seconds, next_run_at, config, created_at)
                   VALUES (?, ?, ?, 1, ?, ?, ?, ?)""",
                (job["id"], job["name"], job["job_type"], job["interval_seconds"],
                 now, job["config"], now),
            )
        db.commit()
    except Exception as exc:
        print(f"[scheduler] ensure_default_jobs failed: {exc}")
    finally:
        db.close()


# ---------------------------------------------------------------------------
# Job execution
# ---------------------------------------------------------------------------

def run_job_now(
    cache: CacheManager,
    storage: Path,
    config_getter: Callable[[], dict[str, Any]],
    get_db: Callable[[], Any],
    job_id: str,
) -> dict[str, Any]:
    """Execute a job immediately and record the run."""
    started = time.monotonic()
    started_at = _now_iso()

    # Record job run start
    db = cache.get_db()
    try:
        run_row = db.execute(
            "INSERT INTO job_runs (job_id, status, started_at) VALUES (?, 'running', ?)",
            (job_id, started_at),
        )
        run_id = run_row.lastrowid
        db.commit()
    finally:
        db.close()

    status = "success"
    summary = ""
    error = ""

    try:
        if job_id == "plugin_index_check":
            summary = _run_plugin_index_check(cache, storage, get_db)
        elif job_id == "release_index_check":
            summary = _run_artifact_index_check(cache, storage, "releases")
        elif job_id == "update_index_check":
            summary = _run_artifact_index_check(cache, storage, "updates")
        elif job_id == "tool_index_check":
            summary = _run_artifact_index_check(cache, storage, "tools")
        elif job_id == "cache_cleanup":
            summary = _run_cache_cleanup(cache)
        elif job_id == "startup_index_check":
            summary = _run_startup_check(cache, storage, get_db)
        else:
            summary = f"Unknown job type: {job_id}"
            status = "error"
    except Exception as exc:
        status = "error"
        error = str(exc)
        summary = f"Job failed: {exc}"
        print(f"[scheduler] job {job_id} failed: {exc}")

    elapsed_ms = int((time.monotonic() - started) * 1000)
    finished_at = _now_iso()

    # Update job run record
    db = cache.get_db()
    try:
        db.execute(
            """UPDATE job_runs SET status = ?, finished_at = ?, duration_ms = ?,
                                     summary = ?, error = ?
               WHERE id = ?""",
            (status, finished_at, elapsed_ms, summary, error, run_id),
        )
        # Update next_run_at for recurring jobs
        job_row = db.execute("SELECT interval_seconds FROM scheduled_jobs WHERE id = ?", (job_id,)).fetchone()
        if job_row and job_row["interval_seconds"] and job_row["interval_seconds"] > 0:
            next_run = datetime.fromtimestamp(
                time.time() + job_row["interval_seconds"], tz=timezone.utc
            ).isoformat()
            db.execute(
                "UPDATE scheduled_jobs SET next_run_at = ?, updated_at = ? WHERE id = ?",
                (next_run, finished_at, job_id),
            )
        db.commit()
    except Exception as exc:
        print(f"[scheduler] failed to update job run: {exc}")
    finally:
        db.close()

    return {
        "job_id": job_id,
        "run_id": run_id,
        "status": status,
        "duration_ms": elapsed_ms,
        "summary": summary,
        "error": error,
    }


def _run_plugin_index_check(cache: CacheManager, storage: Path, get_db: Callable) -> str:
    """Lightweight check: compare plugin directory signature with index state."""
    from plugin_marketplace import plugin_catalog_signature

    current_sig = plugin_catalog_signature(storage)

    db = cache.get_db()
    try:
        row = db.execute("SELECT signature FROM index_state WHERE scope = 'plugins'").fetchone()
        stored_sig = row["signature"] if row else ""
    finally:
        db.close()

    if current_sig == stored_sig:
        return "No changes detected"

    # Signatures differ - trigger full refresh
    from services.plugin_index import refresh_all_plugin_index
    download_counts: dict[str, int] = {}
    try:
        from download_stats import get_download_counts
        download_counts = get_download_counts(get_db)
    except Exception:
        pass

    result = refresh_all_plugin_index(cache, storage, download_counts=download_counts)
    return f"Refreshed: {result['indexed_count']} indexed, {result['deleted_count']} deleted"


def _run_artifact_index_check(cache: CacheManager, storage: Path, scope: str) -> str:
    """Lightweight check: compare artifact directory signature with index state."""
    from services.artifact_index import (
        get_index_state,
        refresh_release_index,
        refresh_update_index,
        refresh_tool_index,
        _release_signature,
        _update_signature,
        _tool_signature,
    )

    sig_fn_map = {
        "releases": _release_signature,
        "updates": _update_signature,
        "tools": _tool_signature,
    }
    refresh_fn_map = {
        "releases": refresh_release_index,
        "updates": refresh_update_index,
        "tools": refresh_tool_index,
    }

    sig_fn = sig_fn_map.get(scope)
    refresh_fn = refresh_fn_map.get(scope)
    if not sig_fn or not refresh_fn:
        return f"Unknown scope: {scope}"

    current_sig = sig_fn(storage)
    state = get_index_state(cache, scope)
    stored_sig = state.get("signature", "") if state else ""

    if current_sig == stored_sig:
        return f"{scope}: No changes detected"

    result = refresh_fn(cache, storage)
    return f"{scope}: Refreshed {result['indexed_count']} items in {result['duration_ms']}ms"


def _run_cache_cleanup(cache: CacheManager) -> str:
    deleted = cache.cleanup_expired_cache()
    return f"Cleaned {deleted} expired cache entries"


def _run_startup_check(cache: CacheManager, storage: Path, get_db: Callable) -> str:
    """If plugin_index is empty, do a full refresh. Also check artifact indexes."""
    from services.plugin_index import is_plugin_index_populated
    from services.artifact_index import startup_check_all_indexes

    parts = []

    # Plugin index
    if not is_plugin_index_populated(cache):
        from services.plugin_index import refresh_all_plugin_index
        download_counts: dict[str, int] = {}
        try:
            from download_stats import get_download_counts
            download_counts = get_download_counts(get_db)
        except Exception:
            pass
        result = refresh_all_plugin_index(cache, storage, download_counts=download_counts)
        parts.append(f"plugins: {result['indexed_count']}")
    else:
        parts.append("plugins: already populated")

    # Artifact indexes
    artifact_summary = startup_check_all_indexes(cache, storage)
    parts.append(artifact_summary)

    return "; ".join(parts)


# ---------------------------------------------------------------------------
# Background scheduler thread
# ---------------------------------------------------------------------------

class SchedulerThread(threading.Thread):
    """Background thread that runs scheduled jobs."""

    def __init__(
        self,
        cache: CacheManager,
        storage_getter: Callable[[], Path],
        config_getter: Callable[[], dict[str, Any]],
        get_db: Callable[[], Any],
    ):
        super().__init__(daemon=True, name="marketplace-scheduler")
        self._cache = cache
        self._storage_getter = storage_getter
        self._config_getter = config_getter
        self._get_db = get_db
        self._stop_event = threading.Event()

    def stop(self):
        self._stop_event.set()

    def run(self):
        # Run startup check immediately
        try:
            run_job_now(
                self._cache,
                self._storage_getter(),
                self._config_getter,
                self._get_db,
                "startup_index_check",
            )
        except Exception as exc:
            print(f"[scheduler] startup check failed: {exc}")

        while not self._stop_event.is_set():
            try:
                self._tick()
            except Exception as exc:
                print(f"[scheduler] tick error: {exc}")

            # Sleep in small increments so we can stop quickly
            for _ in range(30):
                if self._stop_event.is_set():
                    return
                time.sleep(1)

    def _tick(self):
        now = datetime.now(timezone.utc)
        db = self._cache.get_db()
        try:
            rows = db.execute(
                "SELECT * FROM scheduled_jobs WHERE enabled = 1"
            ).fetchall()
        finally:
            db.close()

        for row in rows:
            job = dict(row)
            next_run = job.get("next_run_at")
            if next_run:
                try:
                    next_dt = datetime.fromisoformat(next_run)
                    if next_dt.tzinfo is None:
                        from datetime import timezone as tz
                        next_dt = next_dt.replace(tzinfo=tz.utc)
                    if now < next_dt:
                        continue
                except (ValueError, TypeError):
                    pass

            # Skip startup check after first run
            if job["id"] == "startup_index_check":
                db2 = self._cache.get_db()
                try:
                    run_count = db2.execute(
                        "SELECT COUNT(*) AS cnt FROM job_runs WHERE job_id = 'startup_index_check' AND status = 'success'"
                    ).fetchone()
                    if run_count and run_count["cnt"] > 0:
                        continue
                finally:
                    db2.close()

            try:
                run_job_now(
                    self._cache,
                    self._storage_getter(),
                    self._config_getter,
                    self._get_db,
                    job["id"],
                )
            except Exception as exc:
                print(f"[scheduler] job {job['id']} failed: {exc}")

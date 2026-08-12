"""
Schema version tracking for ColorVision Marketplace.

Provides a lightweight migration mechanism:
  - Each schema change gets a version number
  - Current version stored in SQLite metadata table
  - Migrations run idempotently on startup
"""

from __future__ import annotations

import sqlite3
from typing import Any

CURRENT_SCHEMA_VERSION = 12


def ensure_schema_version(db: sqlite3.Connection) -> int:
    """Ensure schema_version table exists and run pending migrations.

    Returns the current schema version after migrations.
    """
    db.execute("""
        CREATE TABLE IF NOT EXISTS schema_version (
            key TEXT PRIMARY KEY,
            value INTEGER NOT NULL
        )
    """)
    db.commit()

    row = db.execute("SELECT value FROM schema_version WHERE key = 'version'").fetchone()
    current = row["value"] if row else 0

    if current < CURRENT_SCHEMA_VERSION:
        _run_migrations(db, current)
        db.execute(
            "INSERT INTO schema_version (key, value) VALUES ('version', ?) "
            "ON CONFLICT(key) DO UPDATE SET value = excluded.value",
            (CURRENT_SCHEMA_VERSION,),
        )
        db.commit()

    return CURRENT_SCHEMA_VERSION


def _run_migrations(db: sqlite3.Connection, from_version: int):
    """Run schema migrations from from_version to CURRENT_SCHEMA_VERSION."""
    if from_version < 1:
        _migration_v1(db)
    if from_version < 2:
        _migration_v2(db)
    if from_version < 3:
        _migration_v3(db)
    if from_version < 4:
        _migration_v4(db)
    if from_version < 5:
        _migration_v5(db)
    if from_version < 6:
        _migration_v6(db)
    if from_version < 7:
        _migration_v7(db)
    if from_version < 8:
        _migration_v8(db)
    if from_version < 9:
        _migration_v9(db)
    if from_version < 10:
        _migration_v10(db)
    if from_version < 11:
        _migration_v11(db)
    if from_version < 12:
        _migration_v12(db)


def _migration_v1(db: sqlite3.Connection):
    """v1: Initial schema — all tables already created by CacheManager.init_db()."""
    pass  # Tables are created by init_db(); this is the baseline.


def _migration_v2(db: sqlite3.Connection):
    """v2: Add extended fields to job_runs for observability."""
    _add_column_if_missing(db, "job_runs", "scanned_count INTEGER DEFAULT 0")
    _add_column_if_missing(db, "job_runs", "changed_count INTEGER DEFAULT 0")


def _migration_v3(db: sqlite3.Connection):
    """v3: Align plugin_index with the current plugin detail read-model."""
    _add_column_if_missing(db, "plugin_index", "readme TEXT DEFAULT ''")
    _add_column_if_missing(db, "plugin_index", "changelog TEXT DEFAULT ''")
    _add_column_if_missing(db, "plugin_index", "source_manifest_path TEXT")
    _add_column_if_missing(db, "plugin_index", "source_archive_path TEXT")


def _migration_v4(db: sqlite3.Connection):
    """v4: Add privacy-preserving, daily access analytics aggregates."""
    db.executescript(
        """
        CREATE TABLE IF NOT EXISTS access_daily (
            day                 TEXT PRIMARY KEY,
            visits              INTEGER NOT NULL DEFAULT 0,
            unique_visitors     INTEGER NOT NULL DEFAULT 0,
            error_responses     INTEGER NOT NULL DEFAULT 0,
            client_error_responses INTEGER NOT NULL DEFAULT 0,
            server_error_responses INTEGER NOT NULL DEFAULT 0,
            total_duration_ms   INTEGER NOT NULL DEFAULT 0,
            max_duration_ms     INTEGER NOT NULL DEFAULT 0,
            total_response_bytes INTEGER NOT NULL DEFAULT 0,
            updated_at          TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS access_route_daily (
            day                 TEXT NOT NULL,
            route               TEXT NOT NULL,
            method              TEXT NOT NULL,
            visits              INTEGER NOT NULL DEFAULT 0,
            error_responses     INTEGER NOT NULL DEFAULT 0,
            client_error_responses INTEGER NOT NULL DEFAULT 0,
            server_error_responses INTEGER NOT NULL DEFAULT 0,
            total_duration_ms   INTEGER NOT NULL DEFAULT 0,
            max_duration_ms     INTEGER NOT NULL DEFAULT 0,
            total_response_bytes INTEGER NOT NULL DEFAULT 0,
            updated_at          TEXT NOT NULL,
            PRIMARY KEY (day, route, method)
        );
        CREATE INDEX IF NOT EXISTS idx_access_route_day
            ON access_route_daily(day);

        CREATE TABLE IF NOT EXISTS access_client_daily (
            day                 TEXT NOT NULL,
            client_type         TEXT NOT NULL,
            visits              INTEGER NOT NULL DEFAULT 0,
            unique_visitors     INTEGER NOT NULL DEFAULT 0,
            error_responses     INTEGER NOT NULL DEFAULT 0,
            client_error_responses INTEGER NOT NULL DEFAULT 0,
            server_error_responses INTEGER NOT NULL DEFAULT 0,
            total_duration_ms   INTEGER NOT NULL DEFAULT 0,
            updated_at          TEXT NOT NULL,
            PRIMARY KEY (day, client_type)
        );
        CREATE INDEX IF NOT EXISTS idx_access_client_day
            ON access_client_daily(day);

        CREATE TABLE IF NOT EXISTS access_visitor_daily (
            day                 TEXT NOT NULL,
            visitor_key         TEXT NOT NULL,
            client_type         TEXT NOT NULL,
            visits              INTEGER NOT NULL DEFAULT 0,
            first_seen_at       TEXT NOT NULL,
            last_seen_at        TEXT NOT NULL,
            PRIMARY KEY (day, visitor_key)
        );
        CREATE INDEX IF NOT EXISTS idx_access_visitor_client_day
            ON access_visitor_daily(day, client_type);
        """
    )


def _migration_v5(db: sqlite3.Connection):
    """v5: Add centrally managed Copilot model profiles."""
    db.executescript(
        """
        CREATE TABLE IF NOT EXISTS copilot_profiles (
            id                  TEXT PRIMARY KEY,
            name                TEXT NOT NULL,
            vendor_type         TEXT NOT NULL,
            provider_type       TEXT NOT NULL,
            base_url            TEXT NOT NULL,
            model               TEXT NOT NULL,
            api_key_encrypted   TEXT NOT NULL,
            allow_insecure_http INTEGER NOT NULL DEFAULT 0,
            reasoning_mode      TEXT NOT NULL DEFAULT 'Default',
            is_enabled          INTEGER NOT NULL DEFAULT 1,
            is_default          INTEGER NOT NULL DEFAULT 0,
            sort_order          INTEGER NOT NULL DEFAULT 0,
            created_at          TEXT NOT NULL,
            updated_at          TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_copilot_profiles_order
            ON copilot_profiles(is_enabled, sort_order, name);
        """
    )


def _migration_v6(db: sqlite3.Connection):
    """v6: Remove impossible response-body bytes from historical HEAD traffic."""
    db.execute(
        """
        UPDATE access_daily
        SET total_response_bytes = MAX(
            0,
            total_response_bytes - COALESCE((
                SELECT SUM(route.total_response_bytes)
                FROM access_route_daily AS route
                WHERE route.day = access_daily.day
                  AND UPPER(route.method) = 'HEAD'
            ), 0)
        )
        WHERE EXISTS (
            SELECT 1
            FROM access_route_daily AS route
            WHERE route.day = access_daily.day
              AND UPPER(route.method) = 'HEAD'
              AND route.total_response_bytes != 0
        )
        """
    )
    db.execute(
        """UPDATE access_route_daily
           SET total_response_bytes = 0
           WHERE UPPER(method) = 'HEAD' AND total_response_bytes != 0"""
    )


def _migration_v7(db: sqlite3.Connection):
    """v7: Classify future 4xx and 5xx responses without guessing history."""
    for table in ("access_daily", "access_route_daily", "access_client_daily"):
        _add_column_if_missing(
            db,
            table,
            "client_error_responses INTEGER NOT NULL DEFAULT 0",
        )
        _add_column_if_missing(
            db,
            table,
            "server_error_responses INTEGER NOT NULL DEFAULT 0",
        )


def _migration_v8(db: sqlite3.Connection):
    """v8: Persist the configured access-analytics calendar boundary."""
    db.execute(
        """
        CREATE TABLE IF NOT EXISTS access_analytics_metadata (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        )
        """
    )


def _migration_v9(db: sqlite3.Connection):
    """v9: Repair duplicate running rows and enforce per-job single flight."""
    table = db.execute(
        "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'job_runs'"
    ).fetchone()
    if table is None:
        return
    columns = {
        row[1]
        for row in db.execute("PRAGMA table_info(job_runs)").fetchall()
    }
    required_columns = {
        "job_id",
        "status",
        "started_at",
        "finished_at",
        "duration_ms",
        "summary",
        "error",
    }
    if not required_columns.issubset(columns):
        return

    finished_at = "strftime('%Y-%m-%dT%H:%M:%f+00:00', 'now')"
    db.execute(
        f"""
        UPDATE job_runs
        SET status = 'interrupted',
            finished_at = COALESCE(finished_at, {finished_at}),
            duration_ms = MAX(
                0,
                COALESCE(
                    CAST((julianday({finished_at}) - julianday(started_at)) * 86400000 AS INTEGER),
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
          AND id NOT IN (
              SELECT MAX(id) FROM job_runs
              WHERE status = 'running'
              GROUP BY job_id
          )
        """
    )
    db.execute(
        """CREATE UNIQUE INDEX IF NOT EXISTS idx_job_runs_single_running
           ON job_runs(job_id) WHERE status = 'running'"""
    )
    db.execute(
        """CREATE INDEX IF NOT EXISTS idx_job_runs_job_status_id
           ON job_runs(job_id, status, id DESC)"""
    )


def _migration_v10(db: sqlite3.Connection):
    """v10: Persist API key descriptions and index their audit identity."""
    _add_column_if_missing(db, "api_keys", "description TEXT DEFAULT ''")
    audit_table = db.execute(
        "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'audit_log'"
    ).fetchone()
    if audit_table is not None:
        db.execute(
            """CREATE INDEX IF NOT EXISTS idx_audit_actor
               ON audit_log(actor_type, actor_id, id DESC)"""
        )


def _migration_v11(db: sqlite3.Connection):
    """v11: Version account authentication state for session revocation."""
    _add_column_if_missing(db, "users", "auth_version INTEGER NOT NULL DEFAULT 0")


def _migration_v12(db: sqlite3.Connection):
    """v12: Add aggregate-only SPA page views and Core Web Vitals."""
    db.executescript(
        """
        CREATE TABLE IF NOT EXISTS web_page_daily (
            day                 TEXT NOT NULL,
            route               TEXT NOT NULL,
            page_views          INTEGER NOT NULL DEFAULT 0,
            unique_visitors     INTEGER NOT NULL DEFAULT 0,
            hard_navigations    INTEGER NOT NULL DEFAULT 0,
            spa_navigations     INTEGER NOT NULL DEFAULT 0,
            updated_at          TEXT NOT NULL,
            PRIMARY KEY (day, route)
        );
        CREATE INDEX IF NOT EXISTS idx_web_page_daily_day
            ON web_page_daily(day);

        CREATE TABLE IF NOT EXISTS web_page_visitor_daily (
            day                 TEXT NOT NULL,
            route               TEXT NOT NULL,
            visitor_key         TEXT NOT NULL,
            page_views          INTEGER NOT NULL DEFAULT 0,
            first_seen_at       TEXT NOT NULL,
            last_seen_at        TEXT NOT NULL,
            PRIMARY KEY (day, route, visitor_key)
        );
        CREATE INDEX IF NOT EXISTS idx_web_page_visitor_day
            ON web_page_visitor_daily(day);

        CREATE TABLE IF NOT EXISTS web_vital_daily (
            day                 TEXT NOT NULL,
            route               TEXT NOT NULL,
            metric              TEXT NOT NULL,
            samples             INTEGER NOT NULL DEFAULT 0,
            total_value         REAL NOT NULL DEFAULT 0,
            max_value           REAL NOT NULL DEFAULT 0,
            good_samples        INTEGER NOT NULL DEFAULT 0,
            needs_improvement_samples INTEGER NOT NULL DEFAULT 0,
            poor_samples        INTEGER NOT NULL DEFAULT 0,
            updated_at          TEXT NOT NULL,
            PRIMARY KEY (day, route, metric)
        );
        CREATE INDEX IF NOT EXISTS idx_web_vital_daily_day
            ON web_vital_daily(day);
        """
    )


def _add_column_if_missing(db: sqlite3.Connection, table: str, column_def: str):
    """Add a column to a table if it doesn't already exist.

    Only suppresses 'duplicate column name' and 'no such table' errors;
    re-raises other errors.
    """
    try:
        db.execute(f"ALTER TABLE {table} ADD COLUMN {column_def}")
    except sqlite3.OperationalError as exc:
        msg = str(exc).lower()
        if "duplicate column" in msg:
            pass  # Column already exists — idempotent
        elif "no such table" in msg:
            pass  # Table not yet created — will be created by init_db
        else:
            raise

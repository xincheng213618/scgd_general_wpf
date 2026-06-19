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

CURRENT_SCHEMA_VERSION = 3


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

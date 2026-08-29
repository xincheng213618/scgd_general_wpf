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

CURRENT_SCHEMA_VERSION = 27


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
    if from_version < 13:
        _migration_v13(db)
    if from_version < 14:
        _migration_v14(db)
    if from_version < 15:
        _migration_v15(db)
    if from_version < 16:
        _migration_v16(db)
    if from_version < 17:
        _migration_v17(db)
    if from_version < 18:
        _migration_v18(db)
    if from_version < 19:
        _migration_v19(db)
    if from_version < 20:
        _migration_v20(db)
    if from_version < 21:
        _migration_v21(db)
    if from_version < 22:
        _migration_v22(db)
    if from_version < 23:
        _migration_v23(db)
    if from_version < 24:
        _migration_v24(db)
    if from_version < 25:
        _migration_v25(db)
    if from_version < 26:
        _migration_v26(db)
    if from_version < 27:
        _migration_v27(db)


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


def _migration_v13(db: sqlite3.Connection):
    """v13: Add device-signed Operations relay identities and task envelopes."""
    _add_column_if_missing(db, "operations_tasks", "source_type TEXT NOT NULL DEFAULT 'operator'")
    _add_column_if_missing(db, "operations_tasks", "device_id TEXT")
    _add_column_if_missing(db, "operations_tasks", "request_body TEXT")
    _add_column_if_missing(db, "operations_tasks", "request_timestamp TEXT")
    _add_column_if_missing(db, "operations_tasks", "request_nonce TEXT")
    _add_column_if_missing(db, "operations_tasks", "request_signature TEXT")
    db.executescript(
        """
        CREATE TABLE IF NOT EXISTS operations_relay_host_identities (
            host_id            TEXT PRIMARY KEY,
            certificate_der    TEXT NOT NULL,
            certificate_sha256 TEXT NOT NULL,
            created_at         TEXT NOT NULL,
            updated_at         TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS operations_relay_devices (
            host_id         TEXT NOT NULL,
            device_id       TEXT NOT NULL,
            display_name    TEXT NOT NULL,
            public_key_spki TEXT NOT NULL,
            scopes          TEXT NOT NULL DEFAULT '[]',
            approved_at     TEXT NOT NULL,
            revoked_at      TEXT,
            updated_at      TEXT NOT NULL,
            PRIMARY KEY (host_id, device_id)
        );
        CREATE INDEX IF NOT EXISTS idx_ops_relay_devices_active
            ON operations_relay_devices(host_id, revoked_at);

        CREATE TABLE IF NOT EXISTS operations_relay_nonces (
            principal_type TEXT NOT NULL,
            principal_id   TEXT NOT NULL,
            nonce          TEXT NOT NULL,
            expires_at     TEXT NOT NULL,
            PRIMARY KEY (principal_type, principal_id, nonce)
        );
        CREATE INDEX IF NOT EXISTS idx_ops_relay_nonces_expiry
            ON operations_relay_nonces(expires_at);
        """
    )


def _migration_v14(db: sqlite3.Connection):
    """v14: Persist host-signed relay snapshots and task receipts."""
    _add_column_if_missing(db, "operations_hosts", "relay_snapshot_body TEXT")
    _add_column_if_missing(db, "operations_hosts", "relay_snapshot_signature TEXT")
    _add_column_if_missing(db, "operations_task_receipts", "relay_receipt_body TEXT")
    _add_column_if_missing(db, "operations_task_receipts", "relay_receipt_signature TEXT")


def _migration_v15(db: sqlite3.Connection):
    """v15: Index short-lived encrypted window snapshots stored outside SQLite."""
    db.executescript(
        """
        CREATE TABLE IF NOT EXISTS operations_relay_window_snapshots (
            task_id       TEXT PRIMARY KEY,
            host_id       TEXT NOT NULL,
            device_id     TEXT NOT NULL,
            job_id        TEXT NOT NULL,
            sealed_sha256 TEXT NOT NULL,
            sealed_bytes  INTEGER NOT NULL,
            captured_at   TEXT NOT NULL,
            expires_at    TEXT NOT NULL,
            created_at    TEXT NOT NULL,
            FOREIGN KEY(task_id) REFERENCES operations_tasks(task_id)
        );
        CREATE INDEX IF NOT EXISTS idx_ops_relay_window_snapshots_expiry
            ON operations_relay_window_snapshots(expires_at);
        """
    )


def _migration_v16(db: sqlite3.Connection):
    """v16: Add exact error aggregates and repair the historical v15 branch split."""
    # The Web feature branch briefly used schema version 15 for
    # access_error_daily while develop used it for relay window snapshots.
    # Re-running the develop migration is idempotent and makes either v15
    # database shape converge before the version advances.
    _migration_v15(db)
    db.executescript(
        """
        CREATE TABLE IF NOT EXISTS access_error_daily (
            day         TEXT NOT NULL,
            route       TEXT NOT NULL,
            method      TEXT NOT NULL,
            status_code INTEGER NOT NULL CHECK (status_code BETWEEN 400 AND 599),
            responses   INTEGER NOT NULL DEFAULT 0,
            updated_at  TEXT NOT NULL,
            PRIMARY KEY (day, route, method, status_code)
        );
        CREATE INDEX IF NOT EXISTS idx_access_error_daily_day
            ON access_error_daily(day);
        """
    )


def _migration_v17(db: sqlite3.Connection):
    """v17: Add database-backed roles and permissions for Web accounts."""
    db.executescript(
        """
        CREATE TABLE IF NOT EXISTS roles (
            code        TEXT PRIMARY KEY,
            name        TEXT NOT NULL,
            description TEXT NOT NULL DEFAULT '',
            is_system   INTEGER NOT NULL DEFAULT 1,
            created_at  TEXT NOT NULL,
            updated_at  TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS permissions (
            code        TEXT PRIMARY KEY,
            name        TEXT NOT NULL,
            description TEXT NOT NULL DEFAULT '',
            category    TEXT NOT NULL DEFAULT '',
            sort_order  INTEGER NOT NULL DEFAULT 0,
            created_at  TEXT NOT NULL,
            updated_at  TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS role_permissions (
            role_code      TEXT NOT NULL,
            permission_code TEXT NOT NULL,
            granted_at     TEXT NOT NULL,
            PRIMARY KEY (role_code, permission_code),
            FOREIGN KEY(role_code) REFERENCES roles(code),
            FOREIGN KEY(permission_code) REFERENCES permissions(code)
        );
        CREATE INDEX IF NOT EXISTS idx_role_permissions_permission
            ON role_permissions(permission_code, role_code);
        """
    )
    from services.permission_service import seed_permission_catalog

    seed_permission_catalog(db)


def _migration_v18(db: sqlite3.Connection):
    """v18: Add editable account profile metadata."""
    users_table = db.execute(
        "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'users'"
    ).fetchone()
    if users_table is None:
        return

    _add_column_if_missing(db, "users", "display_name TEXT NOT NULL DEFAULT ''")
    _add_column_if_missing(db, "users", "email TEXT NOT NULL DEFAULT ''")
    db.execute(
        """CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email_unique
           ON users(lower(email)) WHERE trim(email) != ''"""
    )


def _migration_v19(db: sqlite3.Connection):
    """v19: Track independently revocable browser login sessions."""
    db.executescript(
        """
        CREATE TABLE IF NOT EXISTS user_sessions (
            id            TEXT PRIMARY KEY,
            user_id       INTEGER NOT NULL,
            auth_version  INTEGER NOT NULL DEFAULT 0,
            ip_address    TEXT NOT NULL DEFAULT '',
            user_agent    TEXT NOT NULL DEFAULT '',
            created_at    TEXT NOT NULL,
            last_seen_at  TEXT NOT NULL,
            revoked_at    TEXT,
            revoke_reason TEXT NOT NULL DEFAULT '',
            FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS idx_user_sessions_active
            ON user_sessions(user_id, revoked_at, last_seen_at DESC);
        """
    )


def _migration_v20(db: sqlite3.Connection):
    """v20: Index audit targets for user-scoped activity timelines."""
    audit_table = db.execute(
        "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'audit_log'"
    ).fetchone()
    if audit_table is not None:
        db.execute(
            """CREATE INDEX IF NOT EXISTS idx_audit_target
               ON audit_log(target_type, target_id, id DESC)"""
        )


def _migration_v21(db: sqlite3.Connection):
    """v21: Persist account-wide login throttling with per-source detail."""
    db.executescript(
        """
        CREATE TABLE IF NOT EXISTS login_attempts (
            username_key      TEXT NOT NULL,
            ip_address        TEXT NOT NULL,
            failed_count      INTEGER NOT NULL DEFAULT 0,
            window_started_at TEXT NOT NULL,
            last_failed_at    TEXT NOT NULL,
            locked_until      TEXT,
            PRIMARY KEY (username_key, ip_address)
        );
        CREATE INDEX IF NOT EXISTS idx_login_attempts_expiry
            ON login_attempts(locked_until, last_failed_at);
        """
    )


def _migration_v22(db: sqlite3.Connection):
    """v22: Persist per-source public-registration velocity limits."""
    db.executescript(
        """
        CREATE TABLE IF NOT EXISTS registration_rate_limits (
            ip_address                TEXT PRIMARY KEY,
            attempt_count             INTEGER NOT NULL DEFAULT 0,
            attempt_window_started_at TEXT NOT NULL,
            success_count             INTEGER NOT NULL DEFAULT 0,
            pending_count             INTEGER NOT NULL DEFAULT 0,
            success_window_started_at TEXT NOT NULL,
            last_attempt_at           TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_registration_rate_limits_stale
            ON registration_rate_limits(last_attempt_at);
        """
    )


def _migration_v23(db: sqlite3.Connection):
    """v23: Track administrator-issued passwords that must be replaced."""
    _add_column_if_missing(
        db,
        "users",
        "must_change_password INTEGER NOT NULL DEFAULT 0",
    )


def _migration_v24(db: sqlite3.Connection):
    """v24: Persist how each database-backed account was created."""
    _add_column_if_missing(
        db,
        "users",
        "account_origin TEXT NOT NULL DEFAULT 'legacy'",
    )
    users_table = db.execute(
        "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'users'"
    ).fetchone()
    if users_table is not None:
        db.execute(
            """CREATE INDEX IF NOT EXISTS idx_users_account_origin
               ON users(account_origin, id DESC)"""
        )


def _migration_v25(db: sqlite3.Connection):
    """v25: Persist privacy-safe, administrator-assisted password recovery."""
    db.executescript(
        """
        CREATE TABLE IF NOT EXISTS password_recovery_requests (
            id                 INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id            INTEGER NOT NULL,
            request_count      INTEGER NOT NULL DEFAULT 1,
            first_requested_at TEXT NOT NULL,
            last_requested_at  TEXT NOT NULL,
            last_ip            TEXT NOT NULL DEFAULT '',
            status             TEXT NOT NULL DEFAULT 'pending'
                               CHECK(status IN ('pending', 'resolved')),
            resolved_at        TEXT,
            resolved_by        TEXT NOT NULL DEFAULT '',
            resolution         TEXT NOT NULL DEFAULT '',
            FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS idx_password_recovery_pending_user
            ON password_recovery_requests(user_id) WHERE status = 'pending';
        CREATE INDEX IF NOT EXISTS idx_password_recovery_status_requested
            ON password_recovery_requests(status, last_requested_at DESC);
        """
    )


def _migration_v26(db: sqlite3.Connection):
    """v26: Track when each database account password was last set."""
    _add_column_if_missing(db, "users", "password_changed_at TEXT")
    users_table = db.execute(
        "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'users'"
    ).fetchone()
    if users_table is not None:
        db.execute(
            """UPDATE users SET password_changed_at = created_at
               WHERE password_changed_at IS NULL AND created_at IS NOT NULL"""
        )


def _migration_v27(db: sqlite3.Connection):
    """v27: Persist per-source password-recovery velocity limits."""
    db.executescript(
        """
        CREATE TABLE IF NOT EXISTS password_recovery_rate_limits (
            ip_address        TEXT PRIMARY KEY,
            attempt_count     INTEGER NOT NULL DEFAULT 0,
            window_started_at TEXT NOT NULL,
            last_attempt_at   TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_password_recovery_rate_limits_stale
            ON password_recovery_rate_limits(last_attempt_at);
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

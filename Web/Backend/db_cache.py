"""
Database and cache helpers for ColorVision Marketplace.

Provides SQLite connection management, schema initialization,
and a key-value cache layer backed by the cache_entry table.
"""

from __future__ import annotations

import json
import sqlite3
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def now_ts() -> int:
    """Current UTC timestamp as integer seconds."""
    return int(datetime.now(timezone.utc).timestamp())


def now_iso() -> str:
    """Current UTC time as ISO 8601 string."""
    return datetime.now(timezone.utc).isoformat()


class CacheManager:
    """SQLite-backed key-value cache with TTL and optional signature invalidation."""

    def __init__(self, db_path: Path):
        self._db_path = db_path

    @property
    def db_path(self) -> Path:
        return self._db_path

    def get_db(self) -> sqlite3.Connection:
        db = sqlite3.connect(str(self._db_path), timeout=15)
        db.row_factory = sqlite3.Row
        db.execute("PRAGMA journal_mode=WAL")
        db.execute("PRAGMA busy_timeout=5000")
        db.execute("PRAGMA foreign_keys=ON")
        return db

    def init_db(self):
        db = self.get_db()
        db.executescript(
            """
            CREATE TABLE IF NOT EXISTS download_log (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                plugin_id   TEXT    NOT NULL,
                version     TEXT    NOT NULL,
                client_ip   TEXT,
                client_ver  TEXT,
                downloaded_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_dl_plugin ON download_log(plugin_id);
            CREATE INDEX IF NOT EXISTS idx_dl_time   ON download_log(downloaded_at);
            CREATE TABLE IF NOT EXISTS cache_entry (
                key         TEXT PRIMARY KEY,
                value       TEXT NOT NULL,
                signature   TEXT NOT NULL DEFAULT '',
                expires_at  INTEGER NOT NULL,
                updated_at  TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_cache_expiry ON cache_entry(expires_at);

            -- Plugin index: persistent read-model for plugin catalog
            CREATE TABLE IF NOT EXISTS plugin_index (
                plugin_id               TEXT PRIMARY KEY,
                name                    TEXT,
                description             TEXT,
                author                  TEXT,
                category                TEXT,
                latest_version          TEXT,
                requires_version        TEXT,
                url                     TEXT,
                has_icon                INTEGER DEFAULT 0,
                current_package_count   INTEGER DEFAULT 0,
                historical_package_count INTEGER DEFAULT 0,
                total_downloads         INTEGER DEFAULT 0,
                modified                TEXT,
                readme                  TEXT DEFAULT '',
                changelog               TEXT DEFAULT '',
                source_manifest_path    TEXT,
                source_archive_path     TEXT,
                file_signature          TEXT,
                indexed_at              TEXT,
                is_deleted              INTEGER DEFAULT 0
            );

            -- Package index: persistent read-model for plugin package versions
            CREATE TABLE IF NOT EXISTS package_index (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                plugin_id       TEXT NOT NULL,
                version         TEXT NOT NULL,
                filename        TEXT,
                relative_path   TEXT UNIQUE,
                size            INTEGER DEFAULT 0,
                source          TEXT,
                modified        TEXT,
                file_hash       TEXT,
                file_signature  TEXT,
                indexed_at      TEXT,
                is_deleted      INTEGER DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_pkg_plugin ON package_index(plugin_id);
            CREATE INDEX IF NOT EXISTS idx_pkg_deleted ON package_index(is_deleted);

            -- Release index: persistent read-model for app release artifacts
            CREATE TABLE IF NOT EXISTS release_index (
                relative_path   TEXT PRIMARY KEY,
                version         TEXT NOT NULL,
                filename        TEXT,
                size            INTEGER DEFAULT 0,
                kind            TEXT,
                kind_label      TEXT,
                era             TEXT,
                era_label       TEXT,
                source          TEXT,
                major_minor     TEXT,
                branch          TEXT,
                modified        TEXT,
                modified_display TEXT,
                display_title   TEXT,
                file_signature  TEXT,
                indexed_at      TEXT,
                is_deleted      INTEGER DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_rel_deleted ON release_index(is_deleted);
            CREATE INDEX IF NOT EXISTS idx_rel_version ON release_index(version);

            -- Update index: persistent read-model for incremental update packages
            CREATE TABLE IF NOT EXISTS update_index (
                filename        TEXT PRIMARY KEY,
                version         TEXT NOT NULL,
                branch          TEXT,
                fix             INTEGER DEFAULT 0,
                size            INTEGER DEFAULT 0,
                modified        TEXT,
                relative_path   TEXT,
                file_signature  TEXT,
                indexed_at      TEXT,
                is_deleted      INTEGER DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_upd_deleted ON update_index(is_deleted);
            CREATE INDEX IF NOT EXISTS idx_upd_version ON update_index(version);

            -- Tool index: persistent read-model for Tool directory contents
            CREATE TABLE IF NOT EXISTS tool_index (
                relative_path   TEXT PRIMARY KEY,
                name            TEXT,
                filename        TEXT,
                size            INTEGER DEFAULT 0,
                modified        TEXT,
                modified_display TEXT,
                is_dir          INTEGER DEFAULT 0,
                file_count      INTEGER DEFAULT 0,
                file_signature  TEXT,
                indexed_at      TEXT,
                is_deleted      INTEGER DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_tool_deleted ON tool_index(is_deleted);

            -- Index state: tracks refresh status per scope
            CREATE TABLE IF NOT EXISTS index_state (
                scope           TEXT PRIMARY KEY,
                signature       TEXT,
                status          TEXT DEFAULT 'ready',
                last_started_at TEXT,
                last_finished_at TEXT,
                last_error      TEXT,
                item_count      INTEGER DEFAULT 0,
                duration_ms     INTEGER DEFAULT 0
            );

            -- Users: admin/operator/viewer accounts
            CREATE TABLE IF NOT EXISTS users (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                username        TEXT UNIQUE NOT NULL,
                password_hash   TEXT NOT NULL,
                role            TEXT DEFAULT 'admin',
                is_active       INTEGER DEFAULT 1,
                created_at      TEXT NOT NULL,
                updated_at      TEXT,
                last_login_at   TEXT
            );

            -- API keys: lifecycle-managed keys with scopes
            CREATE TABLE IF NOT EXISTS api_keys (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                name            TEXT NOT NULL,
                key_prefix      TEXT UNIQUE NOT NULL,
                key_hash        TEXT NOT NULL,
                scopes          TEXT DEFAULT '',
                created_by      TEXT,
                created_at      TEXT NOT NULL,
                expires_at      TEXT,
                last_used_at    TEXT,
                revoked_at      TEXT,
                is_active       INTEGER DEFAULT 1
            );

            -- Audit log: records all admin operations
            CREATE TABLE IF NOT EXISTS audit_log (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                actor_type      TEXT,
                actor_id        TEXT,
                action          TEXT NOT NULL,
                target_type     TEXT,
                target_id       TEXT,
                ip              TEXT,
                user_agent      TEXT,
                detail          TEXT,
                created_at      TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_audit_action ON audit_log(action);
            CREATE INDEX IF NOT EXISTS idx_audit_created ON audit_log(created_at);

            -- Operations relay: outbound-only host control plane. Tasks are catalog-bound,
            -- immutable intents and never contain arbitrary commands.
            CREATE TABLE IF NOT EXISTS operations_hosts (
                host_id         TEXT PRIMARY KEY,
                display_name    TEXT,
                app_version     TEXT,
                status          TEXT DEFAULT 'unknown',
                capabilities    TEXT DEFAULT '[]',
                snapshot        TEXT DEFAULT '{}',
                last_seen_at    TEXT NOT NULL,
                created_at      TEXT NOT NULL,
                updated_at      TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_ops_hosts_seen ON operations_hosts(last_seen_at);

            CREATE TABLE IF NOT EXISTS operations_tasks (
                task_id         TEXT PRIMARY KEY,
                host_id         TEXT NOT NULL,
                capability_id   TEXT NOT NULL,
                payload         TEXT NOT NULL DEFAULT '{}',
                status          TEXT NOT NULL DEFAULT 'queued',
                idempotency_key TEXT NOT NULL,
                created_by      TEXT,
                created_at      TEXT NOT NULL,
                expires_at      TEXT NOT NULL,
                delivered_at    TEXT,
                UNIQUE(host_id, idempotency_key)
            );
            CREATE INDEX IF NOT EXISTS idx_ops_tasks_host_status ON operations_tasks(host_id, status, created_at);

            CREATE TABLE IF NOT EXISTS operations_task_receipts (
                receipt_id      TEXT PRIMARY KEY,
                task_id         TEXT NOT NULL,
                host_id         TEXT NOT NULL,
                status          TEXT NOT NULL,
                evidence        TEXT NOT NULL DEFAULT '{}',
                created_at      TEXT NOT NULL,
                FOREIGN KEY(task_id) REFERENCES operations_tasks(task_id)
            );
            CREATE INDEX IF NOT EXISTS idx_ops_receipts_task ON operations_task_receipts(task_id, created_at);

            CREATE TABLE IF NOT EXISTS operations_support_events (
                event_id        TEXT PRIMARY KEY,
                host_id         TEXT NOT NULL,
                session_id      TEXT NOT NULL,
                event_type      TEXT NOT NULL,
                payload         TEXT NOT NULL DEFAULT '{}',
                created_at      TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_ops_support_session ON operations_support_events(session_id, created_at);

            -- Scheduled jobs: persistent job definitions
            CREATE TABLE IF NOT EXISTS scheduled_jobs (
                id              TEXT PRIMARY KEY,
                name            TEXT NOT NULL,
                job_type        TEXT NOT NULL,
                enabled         INTEGER DEFAULT 1,
                interval_seconds INTEGER DEFAULT 300,
                next_run_at     TEXT,
                config          TEXT DEFAULT '{}',
                created_at      TEXT NOT NULL,
                updated_at      TEXT
            );

            -- Job runs: execution history
            CREATE TABLE IF NOT EXISTS job_runs (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                job_id          TEXT NOT NULL,
                status          TEXT DEFAULT 'running',
                started_at      TEXT NOT NULL,
                finished_at     TEXT,
                duration_ms     INTEGER DEFAULT 0,
                summary         TEXT,
                error           TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_job_runs_job ON job_runs(job_id);
            CREATE INDEX IF NOT EXISTS idx_job_runs_status ON job_runs(status);
        """
        )
        db.commit()
        db.close()

    def set_cache_entry(self, key: str, value: Any, *, ttl_seconds: int, signature: str = ""):
        try:
            payload = json.dumps(value, ensure_ascii=False, separators=(",", ":"))
            db = self.get_db()
            db.execute(
                """
                INSERT INTO cache_entry (key, value, signature, expires_at, updated_at)
                VALUES (?, ?, ?, ?, datetime('now'))
                ON CONFLICT(key) DO UPDATE SET
                    value = excluded.value,
                    signature = excluded.signature,
                    expires_at = excluded.expires_at,
                    updated_at = datetime('now')
                """,
                (key, payload, signature, now_ts() + ttl_seconds),
            )
            db.commit()
            db.close()
        except Exception as exc:
            print(f"[cache] set_cache_entry failed for '{key}': {exc}")

    def get_cache_entry(self, key: str, *, signature: str | None = None) -> dict | None:
        try:
            db = self.get_db()
            row = db.execute(
                "SELECT value, signature, expires_at, updated_at FROM cache_entry WHERE key = ?",
                (key,),
            ).fetchone()
            db.close()
        except Exception as exc:
            print(f"[cache] get_cache_entry failed for '{key}': {exc}")
            return None

        if not row or row["expires_at"] <= now_ts():
            return None
        if signature is not None and row["signature"] != signature:
            return None

        try:
            value = json.loads(row["value"])
        except (TypeError, json.JSONDecodeError):
            return None

        return {
            "value": value,
            "updated_at": row["updated_at"],
            "expires_at": row["expires_at"],
            "signature": row["signature"],
        }

    def invalidate_cache_prefix(self, prefix: str):
        try:
            db = self.get_db()
            db.execute("DELETE FROM cache_entry WHERE key LIKE ?", (f"{prefix}%",))
            db.commit()
            db.close()
        except Exception as exc:
            print(f"[cache] invalidate_cache_prefix failed for '{prefix}': {exc}")

    def refresh_related_caches(
        self,
        *,
        plugin_id: str | None = None,
        relative_path: str = "",
        invalidate_plugin_catalog: bool = True,
    ):
        """Invalidate all caches that may be affected by a file change."""
        self.invalidate_cache_prefix("storage_overview:")
        self.invalidate_cache_prefix("app_releases:")
        self.invalidate_cache_prefix("home_release_snapshot:")
        self.invalidate_cache_prefix("home_tool_preview:")
        self.invalidate_cache_prefix("release_timeline:")

        top_level = Path(relative_path).parts[0] if relative_path else ""
        if top_level:
            self.invalidate_cache_prefix(f"dir_file_count:{top_level}")

        if plugin_id:
            if invalidate_plugin_catalog:
                self.invalidate_cache_prefix("plugin_catalog:")
            self.invalidate_cache_prefix("plugin_summary:")
            self.invalidate_cache_prefix("plugin_detail:")
            self.invalidate_cache_prefix(f"dir_file_count:Plugins/{plugin_id}")

        normalized_relative = Path(relative_path).as_posix().strip()
        if normalized_relative:
            self.invalidate_cache_prefix(f"plugin_package_hash:v1:{normalized_relative}")
            self.invalidate_cache_prefix(f"plugin_archive_meta:v1:{normalized_relative}")

    # -------------------------------------------------------------------
    # Audit log helpers
    # -------------------------------------------------------------------

    def write_audit(
        self,
        *,
        actor_type: str = "system",
        actor_id: str = "",
        action: str,
        target_type: str = "",
        target_id: str = "",
        ip: str = "",
        user_agent: str = "",
        detail: str = "",
    ):
        try:
            db = self.get_db()
            db.execute(
                """INSERT INTO audit_log
                   (actor_type, actor_id, action, target_type, target_id, ip, user_agent, detail, created_at)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                (actor_type, actor_id, action, target_type, target_id, ip, user_agent, detail, now_iso()),
            )
            db.commit()
            db.close()
        except Exception as exc:
            print(f"[audit_log] write failed: {exc}")

    def get_audit_log(
        self,
        *,
        action: str | None = None,
        actor: str | None = None,
        target: str | None = None,
        since: str | None = None,
        until: str | None = None,
        limit: int = 100,
        offset: int = 0,
    ) -> list[dict[str, Any]]:
        try:
            db = self.get_db()
            conditions: list[str] = []
            params: list[Any] = []

            if action:
                conditions.append("action = ?")
                params.append(action)
            if actor:
                conditions.append("(actor_id LIKE ? OR actor_type LIKE ?)")
                params.extend([f"%{actor}%", f"%{actor}%"])
            if target:
                conditions.append("(target_id LIKE ? OR target_type LIKE ?)")
                params.extend([f"%{target}%", f"%{target}%"])
            if since:
                conditions.append("created_at >= ?")
                params.append(since)
            if until:
                conditions.append("created_at <= ?")
                params.append(until)

            where = " AND ".join(conditions) if conditions else "1=1"
            params.extend([limit, offset])

            rows = db.execute(
                f"SELECT * FROM audit_log WHERE {where} ORDER BY id DESC LIMIT ? OFFSET ?",
                params,
            ).fetchall()
            db.close()
            return [dict(r) for r in rows]
        except Exception:
            return []

    # -------------------------------------------------------------------
    # Index state helpers
    # -------------------------------------------------------------------

    def get_all_index_states(self) -> dict[str, dict[str, Any]]:
        """Return all index_state rows keyed by scope."""
        try:
            db = self.get_db()
            rows = db.execute("SELECT * FROM index_state").fetchall()
            db.close()
            return {row["scope"]: dict(row) for row in rows}
        except Exception:
            return {}

    # -------------------------------------------------------------------
    # Cache cleanup
    # -------------------------------------------------------------------

    def cleanup_expired_cache(self) -> int:
        """Delete expired cache_entry rows. Returns count deleted."""
        try:
            db = self.get_db()
            cursor = db.execute(
                "DELETE FROM cache_entry WHERE expires_at <= ?", (now_ts(),)
            )
            deleted = cursor.rowcount
            db.commit()
            db.close()
            return deleted
        except Exception as exc:
            print(f"[cache] cleanup failed: {exc}")
            return 0

    # -------------------------------------------------------------------
    # DB status helpers
    # -------------------------------------------------------------------

    def backup_db(self, dest_path: Path) -> bool:
        """Create a backup of the database file. Returns True on success."""
        import shutil
        try:
            shutil.copy2(str(self._db_path), str(dest_path))
            return True
        except Exception as exc:
            print(f"[db] backup failed: {exc}")
            return False

    def get_db_status(self) -> dict[str, Any]:
        """Return database status information for admin dashboard."""
        status: dict[str, Any] = {}
        try:
            db = self.get_db()
            status["db_path"] = str(self._db_path)
            try:
                status["db_size_bytes"] = self._db_path.stat().st_size
            except OSError:
                status["db_size_bytes"] = 0

            row = db.execute("SELECT COUNT(*) AS cnt FROM cache_entry").fetchone()
            status["cache_entry_count"] = row["cnt"] if row else 0

            row = db.execute(
                "SELECT COUNT(*) AS cnt FROM cache_entry WHERE expires_at <= ?", (now_ts(),)
            ).fetchone()
            status["expired_cache_entry_count"] = row["cnt"] if row else 0

            row = db.execute("SELECT COUNT(*) AS cnt FROM plugin_index WHERE is_deleted = 0").fetchone()
            status["plugin_index_count"] = row["cnt"] if row else 0

            row = db.execute("SELECT COUNT(*) AS cnt FROM package_index WHERE is_deleted = 0").fetchone()
            status["package_index_count"] = row["cnt"] if row else 0

            row = db.execute("SELECT COUNT(*) AS cnt FROM release_index WHERE is_deleted = 0").fetchone()
            status["release_index_count"] = row["cnt"] if row else 0

            row = db.execute("SELECT COUNT(*) AS cnt FROM update_index WHERE is_deleted = 0").fetchone()
            status["update_index_count"] = row["cnt"] if row else 0

            row = db.execute("SELECT COUNT(*) AS cnt FROM tool_index WHERE is_deleted = 0").fetchone()
            status["tool_index_count"] = row["cnt"] if row else 0

            row = db.execute("SELECT * FROM index_state WHERE scope = 'plugins'").fetchone()
            status["plugin_index_state"] = dict(row) if row else None

            # All index states for dashboard
            rows = db.execute("SELECT * FROM index_state").fetchall()
            status["index_states"] = {row["scope"]: dict(row) for row in rows}

            db.close()
        except Exception as exc:
            status["error"] = str(exc)
        return status


# ---------------------------------------------------------------------------
# Cache key constants and TTL values
# ---------------------------------------------------------------------------

OVERVIEW_CACHE_KEY = "storage_overview:v2"
APP_RELEASES_CACHE_KEY = "app_releases:v1"
OVERVIEW_CACHE_TTL_SECONDS = 300          # 5 min
APP_RELEASES_CACHE_TTL_SECONDS = 300      # 5 min
DIRECTORY_COUNT_CACHE_TTL_SECONDS = 300   # 5 min
PLUGIN_INFO_CACHE_TTL_SECONDS = 86400     # 24 h
CHANGELOG_ANALYSIS_CACHE_KEY = "app_changelog:v2"
CHANGELOG_ANALYSIS_CACHE_TTL_SECONDS = 3600  # 1 h
MARKDOWN_RENDER_CACHE_TTL_SECONDS = 3600     # 1 h
HOME_RELEASES_SNAPSHOT_CACHE_KEY = "home_release_snapshot:v1"
HOME_RELEASES_SNAPSHOT_TTL_SECONDS = 300     # 5 min
HOME_TOOL_PREVIEW_CACHE_KEY = "home_tool_preview:v1"
HOME_TOOL_PREVIEW_TTL_SECONDS = 300          # 5 min
RELEASE_TIMELINE_CACHE_KEY = "release_timeline:v1"
RELEASE_TIMELINE_CACHE_TTL_SECONDS = 3600    # 1 h

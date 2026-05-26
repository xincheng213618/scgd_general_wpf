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


class CacheManager:
    """SQLite-backed key-value cache with TTL and optional signature invalidation."""

    def __init__(self, db_path: Path):
        self._db_path = db_path

    def get_db(self) -> sqlite3.Connection:
        db = sqlite3.connect(str(self._db_path))
        db.row_factory = sqlite3.Row
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
        except Exception:
            pass

    def get_cache_entry(self, key: str, *, signature: str | None = None) -> dict | None:
        try:
            db = self.get_db()
            row = db.execute(
                "SELECT value, signature, expires_at, updated_at FROM cache_entry WHERE key = ?",
                (key,),
            ).fetchone()
            db.close()
        except Exception:
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
        except Exception:
            pass

    def refresh_related_caches(self, *, plugin_id: str | None = None, relative_path: str = ""):
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
            self.invalidate_cache_prefix("plugin_catalog:")
            self.invalidate_cache_prefix("plugin_summary:")
            self.invalidate_cache_prefix("plugin_detail:")
            self.invalidate_cache_prefix(f"dir_file_count:Plugins/{plugin_id}")

        normalized_relative = Path(relative_path).as_posix().strip()
        if normalized_relative:
            self.invalidate_cache_prefix(f"plugin_package_hash:v1:{normalized_relative}")
            self.invalidate_cache_prefix(f"plugin_archive_meta:v1:{normalized_relative}")


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

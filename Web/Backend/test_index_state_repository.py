"""Contract tests for the shared index-state repository."""

from __future__ import annotations

import sqlite3
import tempfile
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from db_cache import CacheManager


class IndexStateRepositoryTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.db_path = Path(self._temp.name) / "marketplace.db"
        self.cache = CacheManager(self.db_path)
        self.cache.init_db()
        self.repository = self.cache.index_states

    def tearDown(self):
        self._temp.cleanup()

    def test_refresh_lifecycle_preserves_timestamps_and_clears_last_error(self):
        self.repository.update(
            "plugins",
            status="refreshing",
            signature="snapshot-a",
            started_at="2026-08-10T01:00:00+00:00",
            error="old failure",
        )
        self.repository.update(
            "plugins",
            status="ready",
            finished_at="2026-08-10T01:00:01+00:00",
            item_count=7,
            duration_ms=1000,
        )

        state = self.repository.get("plugins")
        self.assertEqual(state["status"], "ready")
        self.assertEqual(state["signature"], "snapshot-a")
        self.assertEqual(state["last_started_at"], "2026-08-10T01:00:00+00:00")
        self.assertEqual(state["last_finished_at"], "2026-08-10T01:00:01+00:00")
        self.assertEqual(state["last_error"], "")
        self.assertEqual(state["item_count"], 7)

    def test_failed_refresh_records_diagnostic_error_and_finish_time(self):
        self.repository.update(
            "releases",
            status="error",
            finished_at="2026-08-10T02:00:00+00:00",
            item_count=3,
            duration_ms=25,
            error="storage unavailable",
        )

        state = self.repository.get("releases")
        self.assertEqual(state["status"], "error")
        self.assertEqual(state["last_error"], "storage unavailable")
        self.assertEqual(state["last_finished_at"], "2026-08-10T02:00:00+00:00")
        self.assertEqual(state["item_count"], 3)

    def test_concurrent_scope_updates_commit_independently(self):
        scopes = ("plugins", "releases", "updates", "tools")

        def update(scope: str):
            self.repository.update(
                scope,
                status="ready",
                signature=f"sig-{scope}",
                item_count=len(scope),
            )

        with ThreadPoolExecutor(max_workers=len(scopes)) as pool:
            list(pool.map(update, scopes))

        states = self.repository.get_many(scopes)
        for scope in scopes:
            self.assertEqual(states[scope]["signature"], f"sig-{scope}")
            self.assertEqual(states[scope]["item_count"], len(scope))

    def test_v2_database_migrates_before_repository_records_failure(self):
        legacy_path = Path(self._temp.name) / "legacy.db"
        db = sqlite3.connect(legacy_path)
        db.executescript(
            """
            CREATE TABLE schema_version (key TEXT PRIMARY KEY, value INTEGER NOT NULL);
            INSERT INTO schema_version (key, value) VALUES ('version', 2);
            CREATE TABLE plugin_index (plugin_id TEXT PRIMARY KEY, name TEXT);
            """
        )
        db.commit()
        db.close()

        legacy_cache = CacheManager(legacy_path)
        legacy_cache.init_db()
        legacy_cache.index_states.update(
            "plugins",
            status="error",
            finished_at="2026-08-10T03:00:00+00:00",
            error="legacy refresh failure",
        )

        state = legacy_cache.index_states.get("plugins")
        self.assertEqual(state["last_error"], "legacy refresh failure")
        db = legacy_cache.get_db()
        columns = {row["name"] for row in db.execute("PRAGMA table_info(plugin_index)")}
        db.close()
        self.assertTrue(
            {"readme", "changelog", "source_manifest_path", "source_archive_path"}
            <= columns
        )


if __name__ == "__main__":
    unittest.main()

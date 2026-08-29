from __future__ import annotations

import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path

from db_cache import CacheManager
from services.login_throttle_service import (
    FAILURE_WINDOW,
    LOCK_DURATION,
    MAX_FAILED_ATTEMPTS,
    clear_login_failures,
    get_login_security_page,
    get_login_throttle_status,
    record_login_failure,
)


class LoginThrottleServiceTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.cache = CacheManager(Path(self.temp_dir.name) / "marketplace.db")
        self.cache.init_db()
        self.now = datetime(2026, 8, 29, 8, 0, tzinfo=timezone.utc)

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_failures_are_aggregated_across_source_addresses(self):
        for index in range(MAX_FAILED_ATTEMPTS - 1):
            status = record_login_failure(
                self.cache,
                "Mixed-Case-User",
                f"10.0.0.{index + 1}",
                now=self.now + timedelta(seconds=index),
            )
            self.assertFalse(status.locked)
            self.assertEqual(status.failed_count, index + 1)

        locked = record_login_failure(
            self.cache,
            "mixed-case-user",
            "10.0.0.99",
            now=self.now + timedelta(seconds=4),
        )
        self.assertTrue(locked.locked)
        self.assertEqual(locked.failed_count, MAX_FAILED_ATTEMPTS)
        self.assertEqual(locked.retry_after, int(LOCK_DURATION.total_seconds()))

        another_source = record_login_failure(
            self.cache,
            "MIXED-CASE-USER",
            "192.0.2.20",
            now=self.now + timedelta(seconds=5),
        )
        self.assertTrue(another_source.locked)
        self.assertEqual(another_source.failed_count, MAX_FAILED_ATTEMPTS)
        self.assertEqual(
            another_source.retry_after,
            int(LOCK_DURATION.total_seconds()) - 1,
        )

        db = self.cache.get_db()
        try:
            self.assertEqual(
                db.execute("SELECT COUNT(*) FROM login_attempts").fetchone()[0],
                MAX_FAILED_ATTEMPTS,
            )
        finally:
            db.close()

    def test_success_clear_removes_every_source_row(self):
        record_login_failure(self.cache, "user", "10.0.0.1", now=self.now)
        record_login_failure(self.cache, "user", "10.0.0.2", now=self.now)
        clear_login_failures(self.cache, "USER")

        status = get_login_throttle_status(self.cache, "user", now=self.now)
        self.assertFalse(status.locked)
        self.assertEqual(status.failed_count, 0)
        self.assertEqual(status.attempts_remaining, MAX_FAILED_ATTEMPTS)

    def test_expired_observation_window_starts_a_fresh_count(self):
        for index in range(MAX_FAILED_ATTEMPTS - 1):
            record_login_failure(
                self.cache,
                "window-user",
                "10.0.0.1",
                now=self.now + timedelta(seconds=index),
            )

        later = self.now + FAILURE_WINDOW + timedelta(seconds=5)
        status = record_login_failure(
            self.cache,
            "window-user",
            "10.0.0.2",
            now=later,
        )
        self.assertFalse(status.locked)
        self.assertEqual(status.failed_count, 1)
        self.assertEqual(status.attempts_remaining, MAX_FAILED_ATTEMPTS - 1)

    def test_admin_security_page_groups_sources_and_identifies_account_types(self):
        from services.auth_service import create_user

        user, error = create_user(
            self.cache,
            "tracked-user",
            "correct-horse-1",
            display_name="跟踪账号",
            email="tracked@example.com",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(user)
        record_login_failure(self.cache, "tracked-user", "10.0.0.1", now=self.now)
        record_login_failure(self.cache, "tracked-user", "10.0.0.2", now=self.now)
        for index in range(MAX_FAILED_ATTEMPTS):
            record_login_failure(
                self.cache,
                "config-admin",
                f"192.0.2.{index + 1}",
                now=self.now + timedelta(seconds=index),
            )

        page = get_login_security_page(
            self.cache,
            configured_admin_username="Config-Admin",
            limit=10,
            offset=0,
            now=self.now + timedelta(seconds=5),
        )
        self.assertEqual(page["total"], 2)
        self.assertEqual(page["summary"], {
            "total": 2,
            "locked": 1,
            "tracking": 1,
            "sources": 7,
        })
        self.assertEqual(page["items"][0]["username"], "Config-Admin")
        self.assertEqual(page["items"][0]["account_type"], "config_admin")
        self.assertTrue(page["items"][0]["locked"])
        self.assertEqual(len(page["items"][0]["sources"]), MAX_FAILED_ATTEMPTS)

        tracked = get_login_security_page(
            self.cache,
            query="跟踪账号",
            status="tracking",
            limit=10,
            offset=0,
            now=self.now + timedelta(seconds=5),
        )
        self.assertEqual(tracked["total"], 1)
        item = tracked["items"][0]
        self.assertEqual(item["username"], "tracked-user")
        self.assertEqual(item["account_type"], "registered")
        self.assertEqual(item["user_id"], user["id"])
        self.assertEqual(item["source_count"], 2)
        self.assertEqual(item["failed_count"], 2)

    def test_admin_security_page_excludes_expired_windows(self):
        record_login_failure(self.cache, "expired-user", "10.0.0.1", now=self.now)
        page = get_login_security_page(
            self.cache,
            now=self.now + FAILURE_WINDOW + timedelta(seconds=1),
        )
        self.assertEqual(page["items"], [])
        self.assertEqual(page["summary"]["total"], 0)


if __name__ == "__main__":
    unittest.main()

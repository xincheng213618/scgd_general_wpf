from __future__ import annotations

import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path

from db_cache import CacheManager
from services.registration_rate_limit_service import (
    REGISTRATION_ATTEMPT_LIMIT,
    REGISTRATION_ATTEMPT_WINDOW,
    REGISTRATION_SUCCESS_LIMIT,
    REGISTRATION_SUCCESS_WINDOW,
    clear_registration_rate_limit,
    finalize_registration_attempt,
    get_registration_security_page,
    reserve_registration_attempt,
)


class RegistrationRateLimitServiceTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.cache = CacheManager(Path(self.temp_dir.name) / "marketplace.db")
        self.cache.init_db()
        self.now = datetime(2026, 8, 29, 9, 0, tzinfo=timezone.utc)

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_success_velocity_blocks_the_next_registration(self):
        for index in range(REGISTRATION_SUCCESS_LIMIT):
            reserved = reserve_registration_attempt(
                self.cache,
                "10.0.0.1",
                now=self.now + timedelta(seconds=index),
            )
            self.assertTrue(reserved.allowed)
            finalized = finalize_registration_attempt(
                self.cache,
                "10.0.0.1",
                succeeded=True,
                now=self.now + timedelta(seconds=index),
            )

        self.assertTrue(finalized.success_limit_reached)
        blocked = reserve_registration_attempt(
            self.cache,
            "10.0.0.1",
            now=self.now + timedelta(seconds=10),
        )
        self.assertFalse(blocked.allowed)
        self.assertEqual(blocked.reason, "success_velocity")
        self.assertGreater(blocked.retry_after, 0)

        later = reserve_registration_attempt(
            self.cache,
            "10.0.0.1",
            now=self.now + REGISTRATION_SUCCESS_WINDOW + timedelta(seconds=1),
        )
        self.assertTrue(later.allowed)

    def test_pending_reservations_prevent_concurrent_quota_overrun(self):
        reservations = [
            reserve_registration_attempt(self.cache, "192.0.2.1", now=self.now)
            for _ in range(REGISTRATION_SUCCESS_LIMIT)
        ]
        self.assertTrue(all(item.allowed for item in reservations))
        self.assertEqual(reservations[-1].successes_remaining, 0)

        blocked = reserve_registration_attempt(self.cache, "192.0.2.1", now=self.now)
        self.assertFalse(blocked.allowed)
        self.assertEqual(blocked.reason, "success_velocity")

        finalize_registration_attempt(
            self.cache,
            "192.0.2.1",
            succeeded=False,
            now=self.now,
        )
        available_again = reserve_registration_attempt(
            self.cache,
            "192.0.2.1",
            now=self.now,
        )
        self.assertTrue(available_again.allowed)

    def test_attempt_velocity_limits_repeated_invalid_submissions(self):
        for index in range(REGISTRATION_ATTEMPT_LIMIT):
            reserved = reserve_registration_attempt(
                self.cache,
                "198.51.100.4",
                now=self.now + timedelta(seconds=index),
            )
            self.assertTrue(reserved.allowed)
            finalize_registration_attempt(
                self.cache,
                "198.51.100.4",
                succeeded=False,
                now=self.now + timedelta(seconds=index),
            )

        self.assertTrue(reserved.attempt_limit_reached)
        blocked = reserve_registration_attempt(
            self.cache,
            "198.51.100.4",
            now=self.now + timedelta(seconds=30),
        )
        self.assertFalse(blocked.allowed)
        self.assertEqual(blocked.reason, "attempt_velocity")

        later = reserve_registration_attempt(
            self.cache,
            "198.51.100.4",
            now=self.now + REGISTRATION_ATTEMPT_WINDOW + timedelta(seconds=1),
        )
        self.assertTrue(later.allowed)

    def test_registration_security_page_supports_status_filter_and_clear(self):
        for index in range(REGISTRATION_ATTEMPT_LIMIT):
            reserve_registration_attempt(
                self.cache,
                "198.51.100.20",
                now=self.now + timedelta(seconds=index),
            )
            finalize_registration_attempt(
                self.cache,
                "198.51.100.20",
                succeeded=False,
                now=self.now + timedelta(seconds=index),
            )
        reserve_registration_attempt(self.cache, "203.0.113.8", now=self.now)
        finalize_registration_attempt(
            self.cache,
            "203.0.113.8",
            succeeded=True,
            now=self.now,
        )

        page = get_registration_security_page(
            self.cache,
            now=self.now + timedelta(seconds=30),
        )
        self.assertEqual(page["summary"], {
            "total": 2,
            "blocked": 1,
            "tracking": 1,
            "pending": 0,
        })
        self.assertEqual(page["items"][0]["ip_address"], "198.51.100.20")
        self.assertTrue(page["items"][0]["blocked"])
        self.assertEqual(page["items"][0]["reason"], "attempt_velocity")
        self.assertEqual(page["items"][1]["success_count"], 1)

        filtered = get_registration_security_page(
            self.cache,
            query="203.0.113",
            status="tracking",
            now=self.now + timedelta(seconds=30),
        )
        self.assertEqual(filtered["total"], 1)
        self.assertEqual(filtered["items"][0]["ip_address"], "203.0.113.8")

        cleared = clear_registration_rate_limit(
            self.cache,
            "198.51.100.20",
            now=self.now + timedelta(seconds=30),
        )
        self.assertTrue(cleared.cleared)
        self.assertEqual(cleared.pending_count, 0)
        self.assertTrue(reserve_registration_attempt(
            self.cache,
            "198.51.100.20",
            now=self.now + timedelta(seconds=31),
        ).allowed)
        self.assertFalse(clear_registration_rate_limit(
            self.cache,
            "192.0.2.99",
            now=self.now,
        ).cleared)

    def test_clear_preserves_in_flight_registration_reservations(self):
        for _ in range(2):
            self.assertTrue(reserve_registration_attempt(
                self.cache,
                "192.0.2.45",
                now=self.now,
            ).allowed)

        cleared = clear_registration_rate_limit(
            self.cache,
            "192.0.2.45",
            now=self.now + timedelta(seconds=1),
        )
        self.assertTrue(cleared.cleared)
        self.assertEqual(cleared.pending_count, 2)
        pending_page = get_registration_security_page(
            self.cache,
            now=self.now + timedelta(seconds=1),
        )
        self.assertEqual(pending_page["items"][0]["attempt_count"], 0)
        self.assertEqual(pending_page["items"][0]["pending_count"], 2)

        for _ in range(2):
            finalize_registration_attempt(
                self.cache,
                "192.0.2.45",
                succeeded=True,
                now=self.now + timedelta(seconds=2),
            )
        finalized_page = get_registration_security_page(
            self.cache,
            now=self.now + timedelta(seconds=3),
        )
        self.assertEqual(finalized_page["items"][0]["success_count"], 2)
        self.assertEqual(finalized_page["items"][0]["pending_count"], 0)


if __name__ == "__main__":
    unittest.main()

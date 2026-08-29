from __future__ import annotations

import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path

from db_cache import CacheManager
from services.auth_service import create_user, query_users, set_user_active
from services.password_recovery_service import (
    EXPIRATION_RESOLUTION,
    RECOVERY_SOURCE_ATTEMPT_LIMIT,
    RECOVERY_SOURCE_ATTEMPT_WINDOW,
    REQUEST_EXPIRY,
    get_pending_password_recovery,
    resolve_password_recovery_requests,
    reserve_password_recovery_attempt,
    submit_password_recovery_request,
)


class PasswordRecoveryServiceTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.cache = CacheManager(Path(self.temp_dir.name) / "marketplace.db")
        self.cache.init_db()

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_source_velocity_limit_is_persistent_and_resets_after_window(self):
        current = datetime(2026, 8, 29, 10, 0, tzinfo=timezone.utc)
        for index in range(RECOVERY_SOURCE_ATTEMPT_LIMIT):
            status = reserve_password_recovery_attempt(
                self.cache,
                "198.51.100.40",
                now=current + timedelta(seconds=index),
            )
            self.assertTrue(status.allowed)

        self.assertTrue(status.limit_reached)
        self.assertEqual(status.attempts_remaining, 0)
        blocked = reserve_password_recovery_attempt(
            self.cache,
            "198.51.100.40",
            now=current + timedelta(seconds=30),
        )
        self.assertFalse(blocked.allowed)
        self.assertGreater(blocked.retry_after, 0)

        another_source = reserve_password_recovery_attempt(
            self.cache,
            "198.51.100.41",
            now=current + timedelta(seconds=30),
        )
        self.assertTrue(another_source.allowed)
        resumed = reserve_password_recovery_attempt(
            self.cache,
            "198.51.100.40",
            now=current + RECOVERY_SOURCE_ATTEMPT_WINDOW + timedelta(seconds=1),
        )
        self.assertTrue(resumed.allowed)
        self.assertEqual(
            resumed.attempts_remaining,
            RECOVERY_SOURCE_ATTEMPT_LIMIT - 1,
        )

    def test_requests_are_coalesced_and_can_be_resolved(self):
        user, error = create_user(
            self.cache,
            "recovery-user",
            "correct-horse-1",
            email="Recovery@Example.com",
        )
        self.assertIsNone(error)
        self.assertIsNotNone(user)

        first = submit_password_recovery_request(
            self.cache,
            "RECOVERY@example.com",
            ip_address="192.0.2.10",
        )
        self.assertTrue(first.matched)
        self.assertTrue(first.recorded)
        self.assertEqual(first.user_id, user["id"])
        self.assertEqual(first.request_count, 1)

        duplicate = submit_password_recovery_request(
            self.cache,
            "recovery-user",
            ip_address="192.0.2.11",
        )
        self.assertTrue(duplicate.matched)
        self.assertFalse(duplicate.recorded)
        self.assertEqual(duplicate.request_count, 1)
        pending = get_pending_password_recovery(self.cache, user["id"])
        self.assertEqual(pending["request_count"], 1)
        self.assertEqual(pending["last_ip"], "192.0.2.10")

        db = self.cache.get_db()
        try:
            db.execute(
                """UPDATE password_recovery_requests
                   SET last_requested_at = ?
                   WHERE user_id = ? AND status = 'pending'""",
                ((datetime.now(timezone.utc) - timedelta(minutes=2)).isoformat(), user["id"]),
            )
            db.commit()
        finally:
            db.close()

        repeated = submit_password_recovery_request(
            self.cache,
            "recovery-user",
            ip_address="192.0.2.12",
        )
        self.assertEqual(repeated.request_count, 2)
        self.assertTrue(repeated.recorded)
        self.assertEqual(
            get_pending_password_recovery(self.cache, user["id"])["last_ip"],
            "192.0.2.12",
        )

        self.assertEqual(resolve_password_recovery_requests(
            self.cache,
            user["id"],
            resolved_by="admin",
            resolution="administrator_password_reset",
        ), 1)
        self.assertIsNone(get_pending_password_recovery(self.cache, user["id"]))
        self.assertEqual(resolve_password_recovery_requests(
            self.cache,
            user["id"],
            resolved_by="admin",
            resolution="administrator_password_reset",
        ), 0)

    def test_expired_request_leaves_pending_queue_and_can_be_resubmitted(self):
        user, error = create_user(
            self.cache,
            "expired-recovery",
            "correct-horse-1",
        )
        self.assertIsNone(error)
        self.assertTrue(submit_password_recovery_request(
            self.cache,
            "expired-recovery",
            ip_address="192.0.2.30",
        ).recorded)

        expired_at = datetime.now(timezone.utc) - REQUEST_EXPIRY - timedelta(minutes=1)
        db = self.cache.get_db()
        try:
            db.execute(
                """UPDATE password_recovery_requests SET last_requested_at = ?
                   WHERE user_id = ? AND status = 'pending'""",
                (expired_at.isoformat(), user["id"]),
            )
            db.commit()
        finally:
            db.close()

        pending = query_users(
            self.cache,
            password_recovery_pending=True,
            limit=20,
            offset=0,
        )
        self.assertEqual(pending["total"], 0)
        self.assertEqual(pending["summary"]["pending_password_recovery"], 0)
        self.assertIsNone(get_pending_password_recovery(self.cache, user["id"]))

        db = self.cache.get_db()
        try:
            expired = db.execute(
                """SELECT status, resolved_by, resolution
                   FROM password_recovery_requests WHERE user_id = ?""",
                (user["id"],),
            ).fetchone()
            self.assertEqual(expired["status"], "resolved")
            self.assertEqual(expired["resolved_by"], "system")
            self.assertEqual(expired["resolution"], EXPIRATION_RESOLUTION)
        finally:
            db.close()

        resubmitted = submit_password_recovery_request(
            self.cache,
            "expired-recovery",
            ip_address="192.0.2.31",
        )
        self.assertTrue(resubmitted.recorded)
        self.assertEqual(resubmitted.request_count, 1)
        self.assertEqual(get_pending_password_recovery(
            self.cache,
            user["id"],
        )["last_ip"], "192.0.2.31")

        db = self.cache.get_db()
        try:
            db.execute(
                """UPDATE password_recovery_requests SET last_requested_at = ?
                   WHERE user_id = ? AND status = 'pending'""",
                (expired_at.isoformat(), user["id"]),
            )
            db.commit()
            self.assertEqual(db.execute(
                "SELECT COUNT(*) FROM password_recovery_requests WHERE user_id = ?",
                (user["id"],),
            ).fetchone()[0], 2)
        finally:
            db.close()

        from services.scheduler import DEFAULT_JOBS, _run_password_recovery_cleanup

        cleanup_job = next(
            item for item in DEFAULT_JOBS if item["id"] == "password_recovery_cleanup"
        )
        self.assertEqual(cleanup_job["interval_seconds"], 3600)
        self.assertIn(
            "Expired 1 password recovery requests",
            _run_password_recovery_cleanup(self.cache),
        )
        self.assertIsNone(get_pending_password_recovery(self.cache, user["id"]))

    def test_unknown_and_inactive_accounts_do_not_create_requests(self):
        inactive, error = create_user(
            self.cache,
            "inactive-recovery",
            "correct-horse-1",
        )
        self.assertIsNone(error)
        _, error = set_user_active(self.cache, inactive["id"], active=False)
        self.assertIsNone(error)

        self.assertFalse(submit_password_recovery_request(
            self.cache,
            "missing@example.com",
            ip_address="192.0.2.20",
        ).matched)
        self.assertFalse(submit_password_recovery_request(
            self.cache,
            "inactive-recovery",
            ip_address="192.0.2.21",
        ).matched)

        db = self.cache.get_db()
        try:
            self.assertEqual(db.execute(
                "SELECT COUNT(*) FROM password_recovery_requests"
            ).fetchone()[0], 0)
        finally:
            db.close()


if __name__ == "__main__":
    unittest.main()

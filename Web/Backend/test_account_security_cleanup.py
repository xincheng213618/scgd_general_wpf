"""Account-security retention behavior."""

from __future__ import annotations

import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path

from db_cache import CacheManager
from services.account_security_cleanup import cleanup_account_security_data
from services.auth_service import create_user


class AccountSecurityCleanupTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.cache = CacheManager(Path(self.temp_dir.name) / "marketplace.db")
        self.cache.init_db()

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_cleanup_expires_idle_sessions_and_bounds_security_history(self):
        current = datetime(2026, 8, 29, 12, 0, tzinfo=timezone.utc)
        old = (current - timedelta(days=31)).isoformat()
        recent = (current - timedelta(days=1)).isoformat()
        user, error = create_user(self.cache, "cleanup-user", "correct-horse-1")
        self.assertIsNone(error)
        recovery_user, error = create_user(
            self.cache,
            "cleanup-recovery-user",
            "correct-horse-2",
        )
        self.assertIsNone(error)

        db = self.cache.get_db()
        try:
            db.executemany(
                """INSERT INTO user_sessions
                       (id, user_id, auth_version, created_at, last_seen_at,
                        revoked_at, revoke_reason)
                   VALUES (?, ?, 0, ?, ?, ?, ?)""",
                [
                    ("idle", user["id"], old, old, None, ""),
                    ("active", user["id"], recent, recent, None, ""),
                    ("revoked-old", user["id"], old, old, old, "logout"),
                    ("revoked-recent", user["id"], recent, recent, recent, "logout"),
                ],
            )
            db.executemany(
                """INSERT INTO login_attempts
                       (username_key, ip_address, failed_count, window_started_at,
                        last_failed_at, locked_until)
                   VALUES (?, ?, 1, ?, ?, NULL)""",
                [
                    ("old", "192.0.2.1", old, old),
                    ("recent", "192.0.2.2", recent, recent),
                ],
            )
            db.executemany(
                """INSERT INTO registration_rate_limits
                       (ip_address, attempt_count, attempt_window_started_at,
                        success_count, pending_count, success_window_started_at,
                        last_attempt_at)
                   VALUES (?, 1, ?, 0, 0, ?, ?)""",
                [
                    ("192.0.2.3", old, old, old),
                    ("192.0.2.4", recent, recent, recent),
                ],
            )
            db.executemany(
                """INSERT INTO password_recovery_rate_limits
                       (ip_address, attempt_count, window_started_at, last_attempt_at)
                   VALUES (?, 1, ?, ?)""",
                [
                    ("192.0.2.5", old, old),
                    ("192.0.2.6", recent, recent),
                ],
            )
            db.execute(
                """INSERT INTO password_recovery_requests
                       (user_id, request_count, first_requested_at, last_requested_at,
                        status, resolved_at, resolved_by, resolution)
                   VALUES (?, 1, ?, ?, 'resolved', ?, 'admin', 'password_reset')""",
                (user["id"], old, old, old),
            )
            db.execute(
                """INSERT INTO password_recovery_requests
                       (user_id, request_count, first_requested_at, last_requested_at, status)
                   VALUES (?, 1, ?, ?, 'pending')""",
                (recovery_user["id"], old, old),
            )
            db.commit()
        finally:
            db.close()

        result = cleanup_account_security_data(self.cache, now=current)

        self.assertEqual(
            result,
            {
                "session_idle_days": 30,
                "history_retention_days": 30,
                "sessions_expired": 1,
                "sessions_deleted": 1,
                "login_attempts_deleted": 1,
                "registration_limits_deleted": 1,
                "password_recovery_limits_deleted": 1,
                "password_recovery_expired": 1,
                "password_recovery_deleted": 1,
            },
        )
        db = self.cache.get_db()
        try:
            sessions = {
                row["id"]: (row["revoked_at"], row["revoke_reason"])
                for row in db.execute(
                    "SELECT id, revoked_at, revoke_reason FROM user_sessions"
                ).fetchall()
            }
            self.assertEqual(set(sessions), {"idle", "active", "revoked-recent"})
            self.assertEqual(sessions["idle"][1], "inactive_expired")
            self.assertIsNotNone(sessions["idle"][0])
            self.assertIsNone(sessions["active"][0])
            self.assertEqual(
                db.execute("SELECT COUNT(*) FROM login_attempts").fetchone()[0],
                1,
            )
            self.assertEqual(
                db.execute("SELECT COUNT(*) FROM registration_rate_limits").fetchone()[0],
                1,
            )
            self.assertEqual(
                db.execute("SELECT COUNT(*) FROM password_recovery_rate_limits").fetchone()[0],
                1,
            )
            recoveries = db.execute(
                "SELECT status, resolved_by, resolution FROM password_recovery_requests"
            ).fetchall()
            self.assertEqual(len(recoveries), 1)
            self.assertEqual(recoveries[0]["status"], "resolved")
            self.assertEqual(recoveries[0]["resolved_by"], "system")
            self.assertEqual(recoveries[0]["resolution"], "expired")
        finally:
            db.close()


if __name__ == "__main__":
    unittest.main()

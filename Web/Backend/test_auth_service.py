from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from werkzeug.security import generate_password_hash

from db_cache import CacheManager, now_iso
from services.auth_service import (
    MAX_PASSWORD_LENGTH,
    MIN_PASSWORD_LENGTH,
    change_user_password,
    create_user,
    delete_inactive_user,
    require_user_password_change,
    reset_user_password,
    validate_password,
    verify_user_credentials,
)


class AuthServicePasswordPolicyTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.cache = CacheManager(Path(self.temp_dir.name) / "marketplace.db")
        self.cache.init_db()

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_new_passwords_use_unicode_code_point_length_boundaries(self):
        self.assertEqual(MIN_PASSWORD_LENGTH, 15)
        self.assertEqual(MAX_PASSWORD_LENGTH, 128)
        self.assertIn("15", validate_password("a" * 14) or "")
        self.assertIsNone(validate_password("a" * 15))
        self.assertIsNone(validate_password("口" * 15))
        self.assertIsNone(validate_password("🔐" * 15))
        self.assertIsNone(validate_password("a" * 128))
        self.assertIn("128", validate_password("a" * 129) or "")

    def test_account_creation_rejects_passwords_outside_the_policy(self):
        short_user, short_error = create_user(self.cache, "short-user", "a" * 14)
        self.assertIsNone(short_user)
        self.assertIn("15", short_error or "")

        long_user, long_error = create_user(self.cache, "long-user", "a" * 129)
        self.assertIsNone(long_user)
        self.assertIn("128", long_error or "")

        accepted, accepted_error = create_user(
            self.cache,
            "passphrase-user",
            "容易记忆的 长密码短语 123",
        )
        self.assertIsNone(accepted_error)
        self.assertIsNotNone(accepted)
        self.assertEqual(accepted["account_origin"], "legacy")

        invalid_origin, invalid_origin_error = create_user(
            self.cache,
            "invalid-origin-user",
            "correct-horse-1",
            account_origin="imported",
        )
        self.assertIsNone(invalid_origin)
        self.assertEqual(invalid_origin_error, "无效账号来源")

    def test_existing_short_password_hashes_remain_usable_for_login(self):
        legacy_password = "old1234"
        db = self.cache.get_db()
        try:
            db.execute(
                """INSERT INTO users
                       (username, password_hash, role, is_active, created_at, updated_at)
                   VALUES (?, ?, 'user', 1, ?, ?)""",
                (
                    "legacy-user",
                    generate_password_hash(legacy_password),
                    now_iso(),
                    now_iso(),
                ),
            )
            db.commit()
        finally:
            db.close()

        authenticated = verify_user_credentials(
            self.cache,
            "legacy-user",
            legacy_password,
        )
        self.assertIsNotNone(authenticated)
        self.assertEqual(authenticated["username"], "legacy-user")

    def test_temporary_password_state_is_persisted_and_cleared_by_self_service_change(self):
        created, error = create_user(
            self.cache,
            "temporary-user",
            "correct-horse-1",
            must_change_password=True,
        )
        self.assertIsNone(error)
        self.assertTrue(created["must_change_password"])

        reset, error = reset_user_password(
            self.cache,
            created["id"],
            password="correct-horse-2",
        )
        self.assertIsNone(error)
        self.assertTrue(reset["must_change_password"])

        changed, error = change_user_password(
            self.cache,
            created["id"],
            current_password="correct-horse-2",
            new_password="correct-horse-3",
        )
        self.assertIsNone(error)
        self.assertFalse(changed["must_change_password"])

    def test_temporary_password_cannot_clear_change_gate_without_changing_secret(self):
        created, error = create_user(
            self.cache,
            "unchanged-temporary-user",
            "correct-horse-1",
            must_change_password=True,
        )
        self.assertIsNone(error)

        changed, error = change_user_password(
            self.cache,
            created["id"],
            current_password="correct-horse-1",
            new_password="correct-horse-1",
        )

        self.assertIsNone(changed)
        self.assertEqual(error, "新密码不能与当前密码相同")
        unchanged = verify_user_credentials(
            self.cache,
            "unchanged-temporary-user",
            "correct-horse-1",
        )
        self.assertIsNotNone(unchanged)
        self.assertTrue(unchanged["must_change_password"])
        self.assertEqual(unchanged["auth_version"], created["auth_version"])

    def test_password_change_time_tracks_secret_replacement_only(self):
        created, error = create_user(
            self.cache,
            "password-age-user",
            "correct-horse-1",
        )
        self.assertIsNone(error)
        self.assertEqual(created["password_changed_at"], created["created_at"])

        baseline = "2000-01-01T00:00:00+00:00"
        db = self.cache.get_db()
        try:
            db.execute(
                "UPDATE users SET password_changed_at = ? WHERE id = ?",
                (baseline, created["id"]),
            )
            db.commit()
        finally:
            db.close()

        required, error = require_user_password_change(self.cache, created["id"])
        self.assertIsNone(error)
        self.assertEqual(required["password_changed_at"], baseline)

        reset, error = reset_user_password(
            self.cache,
            created["id"],
            password="correct-horse-2",
        )
        self.assertIsNone(error)
        self.assertNotEqual(reset["password_changed_at"], baseline)

        db = self.cache.get_db()
        try:
            db.execute(
                "UPDATE users SET password_changed_at = ? WHERE id = ?",
                (baseline, created["id"]),
            )
            db.commit()
        finally:
            db.close()

        changed, error = change_user_password(
            self.cache,
            created["id"],
            current_password="correct-horse-2",
            new_password="correct-horse-3",
        )
        self.assertIsNone(error)
        self.assertNotEqual(changed["password_changed_at"], baseline)

    def test_password_change_can_be_required_without_replacing_the_current_password(self):
        created, error = create_user(
            self.cache,
            "existing-password-user",
            "correct-horse-1",
        )
        self.assertIsNone(error)
        self.assertFalse(created["must_change_password"])

        updated, error = require_user_password_change(self.cache, created["id"])

        self.assertIsNone(error)
        self.assertTrue(updated["must_change_password"])
        self.assertEqual(updated["auth_version"], created["auth_version"] + 1)
        authenticated = verify_user_credentials(
            self.cache,
            "existing-password-user",
            "correct-horse-1",
        )
        self.assertIsNotNone(authenticated)
        self.assertTrue(authenticated["must_change_password"])

    def test_active_account_cannot_be_permanently_deleted(self):
        created, error = create_user(
            self.cache,
            "active-delete-user",
            "correct-horse-1",
        )
        self.assertIsNone(error)

        deleted, error = delete_inactive_user(self.cache, created["id"])

        self.assertIsNone(deleted)
        self.assertEqual(error, "account_must_be_disabled")
        self.assertIsNotNone(verify_user_credentials(
            self.cache,
            "active-delete-user",
            "correct-horse-1",
        ))

    def test_disabled_account_deletion_cascades_private_security_state(self):
        created, error = create_user(
            self.cache,
            "disabled-delete-user",
            "correct-horse-1",
            account_origin="self_registered",
        )
        self.assertIsNone(error)
        timestamp = now_iso()
        db = self.cache.get_db()
        try:
            db.execute("UPDATE users SET is_active = 0 WHERE id = ?", (created["id"],))
            db.execute(
                """INSERT INTO user_sessions
                       (id, user_id, auth_version, created_at, last_seen_at)
                   VALUES ('delete-session', ?, 0, ?, ?)""",
                (created["id"], timestamp, timestamp),
            )
            db.execute(
                """INSERT INTO password_recovery_requests
                       (user_id, first_requested_at, last_requested_at)
                   VALUES (?, ?, ?)""",
                (created["id"], timestamp, timestamp),
            )
            db.executemany(
                """INSERT INTO login_attempts
                       (username_key, ip_address, failed_count,
                        window_started_at, last_failed_at)
                   VALUES ('disabled-delete-user', ?, 1, ?, ?)""",
                [
                    ("192.0.2.20", timestamp, timestamp),
                    ("192.0.2.21", timestamp, timestamp),
                ],
            )
            db.commit()
        finally:
            db.close()

        deleted, error = delete_inactive_user(self.cache, created["id"])

        self.assertIsNone(error)
        self.assertEqual(deleted, {
            "id": created["id"],
            "username": "disabled-delete-user",
            "role": "user",
            "account_origin": "self_registered",
            "sessions_deleted": 1,
            "password_recovery_requests_deleted": 1,
            "login_failure_sources_cleared": 2,
        })
        db = self.cache.get_db()
        try:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM users").fetchone()[0], 0)
            self.assertEqual(db.execute("SELECT COUNT(*) FROM user_sessions").fetchone()[0], 0)
            self.assertEqual(
                db.execute("SELECT COUNT(*) FROM password_recovery_requests").fetchone()[0],
                0,
            )
            self.assertEqual(db.execute("SELECT COUNT(*) FROM login_attempts").fetchone()[0], 0)
        finally:
            db.close()

        missing, error = delete_inactive_user(self.cache, created["id"])
        self.assertIsNone(missing)
        self.assertEqual(error, "user_not_found")


if __name__ == "__main__":
    unittest.main()

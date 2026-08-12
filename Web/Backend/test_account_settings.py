import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from services.account_settings import (
    get_account_settings,
    is_public_registration_enabled,
    persist_account_settings,
    validate_account_settings_payload,
)


class AccountSettingsTests(unittest.TestCase):
    def test_absent_or_invalid_policy_fails_closed(self):
        self.assertFalse(is_public_registration_enabled({}))
        self.assertFalse(is_public_registration_enabled({"public_registration_enabled": "true"}))
        self.assertEqual(
            get_account_settings({"public_registration_enabled": True}),
            {"public_registration_enabled": True},
        )

    def test_payload_requires_the_exact_boolean_setting(self):
        self.assertEqual(
            validate_account_settings_payload({"public_registration_enabled": True}),
            {"public_registration_enabled": True},
        )
        for payload in (
            None,
            {},
            {"public_registration_enabled": 1},
            {"public_registration_enabled": False, "secret_key": "no"},
        ):
            with self.subTest(payload=payload), self.assertRaises(ValueError):
                validate_account_settings_payload(payload)

    def test_atomic_persistence_preserves_unexposed_configuration(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "config.json"
            existing = {
                "secret_key": "keep-me",
                "storage_path": "D:/ColorVision",
                "upload_auth": {"username": "admin", "password": "keep-me-too"},
                "future_option": {"enabled": True},
            }
            path.write_text(json.dumps(existing), encoding="utf-8")
            active = dict(existing)

            result = persist_account_settings(
                path,
                active,
                {"public_registration_enabled": True},
            )
            persisted = json.loads(path.read_text(encoding="utf-8"))

            self.assertEqual(result["changed"], ["public_registration_enabled"])
            self.assertTrue(active["public_registration_enabled"])
            self.assertEqual(persisted["secret_key"], "keep-me")
            self.assertEqual(persisted["upload_auth"]["password"], "keep-me-too")
            self.assertEqual(persisted["future_option"], {"enabled": True})
            self.assertTrue(persisted["public_registration_enabled"])

    def test_failed_replace_does_not_change_live_config(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "config.json"
            path.write_text("{}", encoding="utf-8")
            active: dict[str, object] = {}

            with mock.patch(
                "services.config_persistence.os.replace",
                side_effect=OSError("replace failed"),
            ), self.assertRaises(OSError):
                persist_account_settings(
                    path,
                    active,
                    {"public_registration_enabled": True},
                )

            self.assertNotIn("public_registration_enabled", active)
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), {})
            self.assertEqual(list(Path(directory).glob("*.tmp")), [])


if __name__ == "__main__":
    unittest.main()

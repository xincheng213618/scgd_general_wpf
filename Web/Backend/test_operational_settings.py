import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from services.operational_settings import (
    OPERATIONAL_RETENTION_SETTINGS,
    get_operational_retention_settings,
    operational_retention_limits,
    persist_operational_retention_settings,
    validate_operational_retention_payload,
)


class OperationalSettingsTests(unittest.TestCase):
    def setUp(self):
        self.values = {
            name: spec.default for name, spec in OPERATIONAL_RETENTION_SETTINGS.items()
        }

    def test_effective_values_and_limits_are_allowlisted(self):
        config = {
            "secret_key": "not-for-the-api",
            "app_release_keep_count": "7",
            "audit_log_retention_days": 99999,
        }

        values = get_operational_retention_settings(config)

        self.assertEqual(set(values), set(OPERATIONAL_RETENTION_SETTINGS))
        self.assertEqual(values["app_release_keep_count"], 7)
        self.assertEqual(values["audit_log_retention_days"], 365)
        self.assertNotIn("secret_key", values)
        self.assertEqual(operational_retention_limits()["admin_db_backup_keep_count"], {
            "minimum": 2,
            "maximum": 1000,
        })

    def test_payload_requires_exact_allowlist_and_integer_values(self):
        self.assertEqual(
            validate_operational_retention_payload({"values": self.values}),
            self.values,
        )

        invalid_payloads = [
            None,
            {"values": {**self.values, "secret_key": 1}},
            {"values": {k: v for k, v in self.values.items() if k != "job_run_retention_days"}},
            {"values": {**self.values, "app_release_keep_count": True}},
            {"values": {**self.values, "access_analytics_retention_days": 0}},
        ]
        for payload in invalid_payloads:
            with self.subTest(payload=payload), self.assertRaises(ValueError):
                validate_operational_retention_payload(payload)

    def test_atomic_persistence_preserves_unexposed_configuration(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "config.json"
            existing = {
                "secret_key": "keep-me",
                "storage_path": "D:/ColorVision",
                "upload_auth": {"username": "admin", "password": "keep-me-too"},
                "copilot_sync": {"version_keys": ["stable"]},
                "future_option": {"enabled": True},
                **self.values,
            }
            path.write_text(json.dumps(existing), encoding="utf-8")
            active = dict(existing)
            next_values = {**self.values, "job_run_retention_days": 45}

            result = persist_operational_retention_settings(path, active, next_values)
            persisted = json.loads(path.read_text(encoding="utf-8"))

            self.assertEqual(result["changed"], ["job_run_retention_days"])
            self.assertEqual(active["job_run_retention_days"], 45)
            self.assertEqual(persisted["secret_key"], "keep-me")
            self.assertEqual(persisted["upload_auth"]["password"], "keep-me-too")
            self.assertEqual(persisted["copilot_sync"], {"version_keys": ["stable"]})
            self.assertEqual(persisted["future_option"], {"enabled": True})
            self.assertEqual(persisted["job_run_retention_days"], 45)
            self.assertEqual(list(Path(directory).glob("*.tmp")), [])

    def test_failed_replace_does_not_change_live_config_or_leave_temp_file(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "config.json"
            path.write_text("{}", encoding="utf-8")
            active = dict(self.values)
            next_values = {**self.values, "job_run_retention_days": 45}

            with mock.patch(
                "services.operational_settings.os.replace",
                side_effect=OSError("replace failed"),
            ), self.assertRaises(OSError):
                persist_operational_retention_settings(path, active, next_values)

            self.assertEqual(active, self.values)
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), {})
            self.assertEqual(list(Path(directory).glob("*.tmp")), [])


if __name__ == "__main__":
    unittest.main()

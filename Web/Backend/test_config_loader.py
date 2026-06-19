import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from config_loader import (
    DEFAULT_CONFIG,
    DEFAULT_SECRET_KEY,
    DEFAULT_UPLOAD_AUTH,
    MAX_FEEDBACK_FIELD_LENGTH,
    MAX_FEEDBACK_FILES,
    MAX_UPLOAD_SIZE_BYTES,
    get_upload_auth,
    load_config,
    validate_runtime_config,
)


class ConfigLoaderTests(unittest.TestCase):
    def test_default_config_has_required_keys(self):
        self.assertIn("storage_path", DEFAULT_CONFIG)
        self.assertIn("host", DEFAULT_CONFIG)
        self.assertIn("port", DEFAULT_CONFIG)
        self.assertIn("secret_key", DEFAULT_CONFIG)
        self.assertIn("upload_auth", DEFAULT_CONFIG)

    def test_max_upload_size_is_500mb(self):
        self.assertEqual(MAX_UPLOAD_SIZE_BYTES, 500 * 1024 * 1024)

    def test_max_feedback_files_is_10(self):
        self.assertEqual(MAX_FEEDBACK_FILES, 10)

    def test_max_feedback_field_length_is_4000(self):
        self.assertEqual(MAX_FEEDBACK_FIELD_LENGTH, 4000)

    def test_default_secret_key_is_placeholder(self):
        self.assertEqual(DEFAULT_SECRET_KEY, "change-this-in-production")

    def test_default_upload_auth_is_admin(self):
        self.assertEqual(DEFAULT_UPLOAD_AUTH, {"username": "admin", "password": "admin"})

    def test_load_config_returns_defaults_when_no_file(self):
        with patch("config_loader.BASE_DIR", Path("/nonexistent")):
            config = load_config()
        self.assertEqual(config["host"], "0.0.0.0")
        self.assertEqual(config["port"], 9998)

    def test_load_config_merges_json_over_defaults(self):
        with tempfile.TemporaryDirectory() as td:
            cfg_path = Path(td) / "config.json"
            cfg_path.write_text(json.dumps({"port": 1234, "debug": True}))
            with patch("config_loader.BASE_DIR", Path(td)):
                config = load_config()
        self.assertEqual(config["port"], 1234)
        self.assertTrue(config["debug"])
        self.assertEqual(config["host"], "0.0.0.0")  # default preserved

    def test_load_config_merges_upload_auth(self):
        with tempfile.TemporaryDirectory() as td:
            cfg_path = Path(td) / "config.json"
            cfg_path.write_text(json.dumps({"upload_auth": {"username": "u1"}}))
            with patch("config_loader.BASE_DIR", Path(td)):
                config = load_config()
        self.assertEqual(config["upload_auth"]["username"], "u1")
        self.assertEqual(config["upload_auth"]["password"], "admin")  # default preserved

    def test_get_upload_auth_extracts_credentials(self):
        config = {"upload_auth": {"username": "testuser", "password": "testpass"}}
        username, password = get_upload_auth(config)
        self.assertEqual(username, "testuser")
        self.assertEqual(password, "testpass")

    def test_get_upload_auth_handles_missing_key(self):
        username, password = get_upload_auth({})
        self.assertEqual(username, "")
        self.assertEqual(password, "")

    def test_get_upload_auth_handles_non_dict(self):
        username, password = get_upload_auth({"upload_auth": "invalid"})
        self.assertEqual(username, "")
        self.assertEqual(password, "")

    def test_validate_runtime_config_rejects_default_secret_key(self):
        config = {"secret_key": DEFAULT_SECRET_KEY, "upload_auth": {"username": "u", "password": "p"}}
        issues = validate_runtime_config(config)
        self.assertTrue(any("secret_key" in i for i in issues))

    def test_validate_runtime_config_rejects_default_upload_auth(self):
        config = {"secret_key": "custom-key", "upload_auth": dict(DEFAULT_UPLOAD_AUTH)}
        issues = validate_runtime_config(config)
        self.assertTrue(any("upload_auth" in i for i in issues))

    def test_validate_runtime_config_accepts_custom_config(self):
        config = {"secret_key": "my-secret", "upload_auth": {"username": "u", "password": "p"}}
        issues = validate_runtime_config(config)
        self.assertEqual(len(issues), 0)


if __name__ == "__main__":
    unittest.main()

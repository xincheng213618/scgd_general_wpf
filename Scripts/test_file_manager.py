import os
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from backend_client import DEFAULT_UPLOAD_URL
from file_manager import FileManager


class FileManagerTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.file_path = Path(self.temp_dir.name) / "demo.bin"
        self.file_path.write_bytes(b"payload")
        self.original_env = {
            "COLORVISION_UPLOAD_URL": os.environ.get("COLORVISION_UPLOAD_URL"),
            "COLORVISION_UPLOAD_USERNAME": os.environ.get("COLORVISION_UPLOAD_USERNAME"),
            "COLORVISION_UPLOAD_PASSWORD": os.environ.get("COLORVISION_UPLOAD_PASSWORD"),
        }

    def tearDown(self):
        for key, value in self.original_env.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value
        self.temp_dir.cleanup()

    def test_upload_file_skips_when_credentials_are_missing(self):
        os.environ.pop("COLORVISION_UPLOAD_USERNAME", None)
        os.environ.pop("COLORVISION_UPLOAD_PASSWORD", None)
        manager = FileManager(base_url="http://example.com:9998", username="", password="")

        with patch("builtins.print") as mocked_print:
            result = manager.upload_file(self.file_path, "ColorVision")

        self.assertFalse(result)
        self.assertTrue(mocked_print.called)
        self.assertIn("Remote upload skipped", mocked_print.call_args[0][0])

    def test_upload_file_uses_environment_credentials(self):
        os.environ["COLORVISION_UPLOAD_URL"] = "http://example.com:9998"
        os.environ["COLORVISION_UPLOAD_USERNAME"] = "tester"
        os.environ["COLORVISION_UPLOAD_PASSWORD"] = "secret"
        manager = FileManager()

        with (
            patch("file_manager.preflight_remote_upload", return_value=True) as mocked_preflight,
            patch("file_manager.authenticated_upload_file", return_value=True) as mocked_upload,
        ):
            result = manager.upload_file(self.file_path, "ColorVision/Update")

        self.assertTrue(result)
        mocked_preflight.assert_called_once()
        mocked_upload.assert_called_once()
        _, settings = mocked_upload.call_args[0]
        self.assertEqual(settings.base_url, "http://example.com:9998")
        self.assertEqual(settings.folder_name, "ColorVision/Update")
        self.assertEqual(settings.username, "tester")
        self.assertEqual(settings.password, "secret")

    def test_upload_file_stops_when_preflight_fails(self):
        manager = FileManager(base_url="http://example.com:9998", username="xincheng", password="xincheng")

        with (
            patch("file_manager.preflight_remote_upload", return_value=False) as mocked_preflight,
            patch("file_manager.authenticated_upload_file") as mocked_upload,
        ):
            result = manager.upload_file(self.file_path, "ColorVision/Update")

        self.assertFalse(result)
        mocked_preflight.assert_called_once()
        mocked_upload.assert_not_called()

    def test_file_manager_uses_default_upload_url(self):
        os.environ.pop("COLORVISION_UPLOAD_URL", None)
        manager = FileManager(username="user", password="pass")
        self.assertEqual(manager.base_url, DEFAULT_UPLOAD_URL)

    def test_file_manager_uses_default_credentials_for_current_setup(self):
        os.environ.pop("COLORVISION_UPLOAD_USERNAME", None)
        os.environ.pop("COLORVISION_UPLOAD_PASSWORD", None)
        manager = FileManager()
        self.assertEqual(manager.username, "xincheng")
        self.assertEqual(manager.password, "xincheng")


if __name__ == "__main__":
    unittest.main()


import os
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import publish_plugin


class PublishPluginTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.package_file = Path(self.temp_dir.name) / "Spectrum-1.0.0.1.cvxp"
        self.package_file.write_bytes(b"payload")
        self.original_env = {
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

    def _args(self, **overrides):
        base = {
            "plugin_id": "Spectrum",
            "version": "1.0.0.1",
            "file": str(self.package_file),
            "name": None,
            "description": None,
            "author": None,
            "category": None,
            "requires": None,
            "changelog": None,
            "icon": None,
            "api_url": "http://example.com:9998",
            "username": None,
            "password": None,
        }
        base.update(overrides)
        return SimpleNamespace(**base)

    def test_publish_plugin_requires_auth(self):
        with self.assertRaises(SystemExit) as ctx:
            publish_plugin.publish_plugin(self._args(username="", password=""))
        self.assertEqual(ctx.exception.code, 2)

    def test_publish_plugin_uses_environment_auth(self):
        os.environ["COLORVISION_UPLOAD_USERNAME"] = "tester"
        os.environ["COLORVISION_UPLOAD_PASSWORD"] = "secret"

        class Response:
            status_code = 201
            text = '{"ok": true}'

            @staticmethod
            def json():
                return {"ok": True}

        with (
            patch("publish_plugin.preflight_remote_upload", return_value=True) as mocked_preflight,
            patch("publish_plugin.post_multipart_with_auth", return_value=Response()) as mocked_post,
        ):
                publish_plugin.publish_plugin(self._args())

        mocked_preflight.assert_called_once()
        _, kwargs = mocked_post.call_args
        self.assertEqual(kwargs["username"], "tester")
        self.assertEqual(kwargs["password"], "secret")
        self.assertEqual(
            (kwargs["connect_timeout"], kwargs["read_timeout"]),
            (publish_plugin.DEFAULT_CONNECT_TIMEOUT, publish_plugin.DEFAULT_PUBLISH_READ_TIMEOUT),
        )


if __name__ == "__main__":
    unittest.main()






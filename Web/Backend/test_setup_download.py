"""Public download choices, isolated from application config and databases."""

import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock, patch

from flask import Flask

from page_contexts import build_compact_releases_page_context
from routes import pages
from services.artifact_delivery import ArtifactDeliveryService
from services.setup_download import SETUP_RELATIVE_PATH, get_download_assistant
from storage_paths import normalize_relative_path, resolve_storage_file, storage_target


class SetupDownloadTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.storage = Path(self.temp.name).resolve()
        self.target = self.storage / SETUP_RELATIVE_PATH
        self.target.parent.mkdir(parents=True)
        self.installer = {"relative_path": "ColorVision-1.2.3.4.exe", "version": "1.2.3.4", "size": 12345}
        self.app_info = {
            "latest_version": "1.2.3.4",
            "latest_release": self.installer,
            "current_preview": [self.installer],
            "current_releases": [self.installer],
            "archive_timeline_groups": [],
        }
        self.services = Mock()
        self.services.get_request_compact_home_app_info.return_value = None
        self.services.get_request_home_app_info.return_value = self.app_info
        self.services.get_request_release_app_info.return_value = self.app_info
        self.services.get_request_compact_release_page.return_value = None
        self.services.get_storage_overview_context.return_value = ([], {}, {})
        self.services.get_request_home_tool_preview.return_value = {"items": [], "summary": {}}
        self.services.resolve_storage_file.side_effect = lambda path: resolve_storage_file(self.storage, path)
        self.app = Flask(__name__)
        self.app.config.update(TESTING=True, SECRET_KEY="isolated-download-test")
        self.app.register_blueprint(pages.pages)
        self.context_patch = patch.object(pages, "_ctx", SimpleNamespace(
            storage=self.storage, cache=None, services=self.services,
            active_config={}, artifact_delivery=ArtifactDeliveryService(),
            normalize_relative_path=normalize_relative_path,
            storage_target=lambda path: storage_target(self.storage, path),
        ))
        self.context_patch.start()
        self.addCleanup(self.context_patch.stop)
        self.client = self.app.test_client()

    def assert_choices(self, expected):
        for indexed in (False, True):
            self.services.get_request_compact_home_app_info.return_value = self.app_info if indexed else None
            indexed_payload = build_compact_releases_page_context(self.app_info)
            self.services.get_request_compact_release_page.return_value = indexed_payload if indexed else None
            for path in ("/api/site/home", "/api/site/home?view=compact", "/api/site/releases", "/api/site/releases?view=compact"):
                with self.subTest(indexed=indexed, path=path):
                    response = self.client.get(path)
                    self.assertEqual(response.status_code, 200)
                    payload = response.get_json()
                    self.assertEqual(payload["download_assistant"], expected)
                    self.assertEqual(payload["app_info"]["latest_release"], self.installer)
                    self.assertEqual(payload["app_info"]["latest_version"], "1.2.3.4")
            self.assertNotIn("download_assistant", indexed_payload)

    def test_missing_assistant_keeps_full_installer_available(self):
        self.assert_choices(None)

    def test_assistant_publication_replacement_and_removal_are_visible_without_index_rebuild(self):
        self.target.write_bytes(b"setup fixture")
        self.assert_choices({"relative_path": SETUP_RELATIVE_PATH, "size": 13})
        self.target.write_bytes(b"replacement fixture")
        self.assert_choices({"relative_path": SETUP_RELATIVE_PATH, "size": 19})
        self.target.unlink()
        self.assert_choices(None)

    def test_empty_files_and_directories_are_not_downloads(self):
        self.target.touch()
        self.assertIsNone(get_download_assistant(self.storage))
        self.target.unlink()
        self.target.mkdir()
        self.assertIsNone(get_download_assistant(self.storage))

    def test_both_download_choices_deliver_their_own_file_anonymously(self):
        self.target.write_bytes(b"assistant fixture")
        installer_path = self.storage / self.installer["relative_path"]
        installer_path.write_bytes(b"full installer fixture")
        for path, expected in ((SETUP_RELATIVE_PATH, b"assistant fixture"), (installer_path.name, b"full installer fixture")):
            with self.subTest(path=path), self.client.get("/download/" + path, buffered=True) as response:
                self.assertEqual(response.status_code, 200)
                self.assertEqual(response.data, expected)
                self.assertIn(Path(path).name, response.headers["Content-Disposition"])

    def test_symlink_is_not_advertised(self):
        private_file = self.storage / "private.exe"
        private_file.write_bytes(b"private")
        try:
            self.target.symlink_to(private_file)
        except OSError as exc:
            self.skipTest(f"Symbolic links unavailable: {exc}")
        self.assertIsNone(get_download_assistant(self.storage))


if __name__ == "__main__":
    unittest.main()

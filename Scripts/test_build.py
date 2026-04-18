import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import build


def always_fail_upload(_file_path, _settings) -> bool:
    return False


class DummyProgress:
    def __init__(self, *args, **kwargs):
        self.total = kwargs.get("total", 0)
        self.updated = 0

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False

    def update(self, amount):
        self.updated += amount


class DummyResponse:
    def __init__(self, status_code=201, text=""):
        self.status_code = status_code
        self.text = text


class DummySession:
    def __init__(self, response):
        self.response = response
        self.calls = []

    def put(self, url, data, auth=None, timeout=None, headers=None):
        payload = b"".join(data)
        self.calls.append(
            {
                "url": url,
                "payload": payload,
                "auth": auth,
                "timeout": timeout,
                "headers": headers,
            }
        )
        return self.response


class MultiResponseSession:
    def __init__(self, responses):
        self.responses = list(responses)
        self.get_calls = []

    def get(self, url, timeout=None):
        self.get_calls.append({"url": url, "timeout": timeout})
        return self.responses.pop(0)


class BuildScriptTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_extract_version_from_filename(self):
        self.assertEqual(
            build.extract_version_from_filename("ColorVision-1.2.3.4.exe"),
            "1.2.3.4",
        )
        self.assertIsNone(build.extract_version_from_filename("ColorVision-latest.exe"))

    def test_get_latest_file_prefers_versioned_installer_files(self):
        setup_dir = self.root / "setup"
        setup_dir.mkdir()
        (setup_dir / "notes.txt").write_text("ignore", encoding="utf-8")
        older = setup_dir / "ColorVision-1.0.0.0.exe"
        older.write_bytes(b"old")
        newer = setup_dir / "ColorVision-2.0.0.0.exe"
        newer.write_bytes(b"new")

        latest = build.get_latest_file(setup_dir)
        self.assertEqual(latest, newer)

    def test_upload_file_uses_basic_auth_and_streams_payload(self):
        package_file = self.root / "ColorVision-1.2.3.4.exe"
        package_file.write_bytes(b"payload")
        session = DummySession(DummyResponse(status_code=201))
        settings = build.RemoteUploadSettings(
            base_url="http://example.com:9998",
            folder_name="ColorVision",
            username="tester",
            password="secret",
        )

        result = build.upload_file(
            package_file,
            settings,
            session=session,
            progress_factory=DummyProgress,
        )

        self.assertTrue(result)
        self.assertEqual(len(session.calls), 1)
        self.assertEqual(
            session.calls[0]["url"],
            "http://example.com:9998/upload/ColorVision/ColorVision-1.2.3.4.exe",
        )
        self.assertEqual(session.calls[0]["payload"], b"payload")
        self.assertEqual(session.calls[0]["auth"], ("tester", "secret"))
        self.assertEqual(session.calls[0]["headers"], {"Content-Type": "application/octet-stream"})

    def test_preflight_remote_upload_passes_when_backend_is_ready(self):
        settings = build.RemoteUploadSettings(
            base_url="http://example.com:9998",
            folder_name="ColorVision",
            username="tester",
            password="secret",
        )
        session = MultiResponseSession(
            [
                SimpleNamespace(status_code=200, text='{"status":"ok"}', json=lambda: {"status": "ok"}),
                SimpleNamespace(status_code=200, text='{"ready":true}', json=lambda: {"ready": True, "issues": []}),
            ]
        )

        result = build.preflight_remote_upload(settings, session=session)

        self.assertTrue(result)
        self.assertEqual(
            [call["url"] for call in session.get_calls],
            ["http://example.com:9998/api/health", "http://example.com:9998/api/ready"],
        )

    def test_preflight_remote_upload_fails_when_backend_is_not_ready(self):
        settings = build.RemoteUploadSettings(
            base_url="http://example.com:9998",
            folder_name="ColorVision",
            username="tester",
            password="secret",
        )
        session = MultiResponseSession(
            [
                SimpleNamespace(status_code=200, text='{"status":"ok"}', json=lambda: {"status": "ok"}),
                SimpleNamespace(
                    status_code=503,
                    text='{"ready":false}',
                    json=lambda: {"ready": False, "issues": ["upload authentication is not configured"]},
                ),
            ]
        )

        result = build.preflight_remote_upload(settings, session=session)

        self.assertFalse(result)

    def test_publish_primary_release_does_not_update_version_when_upload_fails(self):
        changelog_src = self.root / "CHANGELOG.md"
        changelog_src.write_text("hello", encoding="utf-8")
        package_file = self.root / "ColorVision-2.0.0.0.exe"
        package_file.write_bytes(b"payload")

        settings = build.RemoteUploadSettings(
            base_url="http://example.com:9998",
            folder_name="ColorVision",
            username="tester",
            password="secret",
            enabled=True,
        )

        with patch("build.backend_fetch_latest_version", return_value="1.0.0.0"):
            result = build.publish_primary_release(
                "2.0.0.0",
                package_file,
                changelog_src,
                settings,
                upload_func=always_fail_upload,
            )

        self.assertFalse(result)

    def test_publish_primary_release_uploads_all_artifacts(self):
        changelog_src = self.root / "CHANGELOG.md"
        changelog_src.write_text("hello", encoding="utf-8")
        package_file = self.root / "ColorVision-2.0.0.0.exe"
        package_file.write_bytes(b"payload")

        settings = build.RemoteUploadSettings(
            base_url="http://example.com:9998",
            folder_name="ColorVision",
            username="tester",
            password="secret",
            enabled=True,
        )

        upload_calls = []

        def fake_upload(file_path, s, **kwargs):
            upload_calls.append(Path(file_path).name)
            return True

        with (
            patch("build.backend_fetch_latest_version", return_value="1.0.0.0"),
            patch("build.backend_upload_file", side_effect=fake_upload),
            patch("build.backend_upload_content", return_value=True) as mock_content,
        ):
            result = build.publish_primary_release(
                "2.0.0.0",
                package_file,
                changelog_src,
                settings,
                upload_func=fake_upload,
            )

        self.assertTrue(result)
        self.assertIn("CHANGELOG.md", upload_calls)
        mock_content.assert_called_once_with("2.0.0.0", "LATEST_RELEASE", settings)

    def test_main_aborts_before_build_when_preflight_fails(self):
        args = SimpleNamespace(
            project="ColorVision",
            skip_build=False,
            skip_remote_upload=False,
            upload_url="http://example.com:9998",
            upload_folder="ColorVision",
            upload_user="tester",
            upload_password="secret",
            connect_timeout=10,
            read_timeout=30,
            upload_retries=2,
            latest_file=None,
            setup_files_dir=None,
        )

        project = build.ProjectConfig(
            name="ColorVision",
            msbuild_path=Path("msbuild.exe"),
            solution_path=Path("build.sln"),
            advanced_installer_path=Path("AdvancedInstaller.com"),
            aip_path=Path("ColorVision.aip"),
            setup_files_dir=self.root,
            changelog_src=self.root / "CHANGELOG.md",
            wechat_target_directory=self.root,
        )

        with (
            patch("build.parse_args", return_value=args),
            patch("build.build_projects", return_value={"ColorVision": project}),
            patch("build.preflight_remote_upload", return_value=False),
            patch("build.rebuild_project") as mocked_rebuild,
        ):
            exit_code = build.main()

        self.assertEqual(exit_code, 2)
        mocked_rebuild.assert_not_called()

    def test_main_uses_shared_default_credentials_when_not_explicitly_provided(self):
        args = SimpleNamespace(
            project="ColorVision",
            skip_build=True,
            skip_remote_upload=False,
            upload_url=None,
            upload_folder="ColorVision",
            upload_user=None,
            upload_password=None,
            connect_timeout=10,
            read_timeout=30,
            upload_retries=2,
            latest_file=str(self.root / "ColorVision-2.0.0.0.exe"),
            setup_files_dir=None,
        )
        latest_file = Path(args.latest_file)
        latest_file.write_bytes(b"payload")
        changelog_path = self.root / "CHANGELOG.md"
        changelog_path.write_text("hello", encoding="utf-8")
        project = build.ProjectConfig(
            name="ColorVision",
            msbuild_path=Path("msbuild.exe"),
            solution_path=Path("build.sln"),
            advanced_installer_path=Path("AdvancedInstaller.com"),
            aip_path=Path("ColorVision.aip"),
            setup_files_dir=self.root,
            changelog_src=changelog_path,
            wechat_target_directory=self.root,
        )

        captured_settings = {}

        def fake_preflight(settings, session=None):
            captured_settings["settings"] = settings
            return False

        with (
            patch("build.parse_args", return_value=args),
            patch("build.build_projects", return_value={"ColorVision": project}),
            patch("build.preflight_remote_upload", side_effect=fake_preflight),
        ):
            exit_code = build.main()

        self.assertEqual(exit_code, 2)
        self.assertEqual(captured_settings["settings"].username, "xincheng")
        self.assertEqual(captured_settings["settings"].password, "xincheng")


if __name__ == "__main__":
    unittest.main()



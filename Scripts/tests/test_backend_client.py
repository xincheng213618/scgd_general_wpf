import unittest
from unittest import mock

from Scripts import backend_client


class UploadFileToFolderTests(unittest.TestCase):
    def test_missing_credentials_stop_before_preflight(self):
        with mock.patch.object(backend_client, "preflight_remote_upload") as preflight:
            result = backend_client.upload_file_to_folder(
                "package.cvx",
                "ColorVision/Update",
                username="",
                password="",
            )

        self.assertFalse(result)
        preflight.assert_not_called()

    def test_preflight_and_upload_share_resolved_settings(self):
        session = object()
        progress_factory = object()
        with (
            mock.patch.object(backend_client, "preflight_remote_upload", return_value=True) as preflight,
            mock.patch.object(backend_client, "upload_file", return_value=True) as upload,
        ):
            result = backend_client.upload_file_to_folder(
                "package.cvx",
                "ColorVision/Update",
                base_url="http://example.test:9998/",
                username="user",
                password="password",
                session=session,
                progress_factory=progress_factory,
            )

        self.assertTrue(result)
        settings = preflight.call_args.args[0]
        self.assertEqual("http://example.test:9998", settings.base_url)
        self.assertEqual("ColorVision/Update", settings.folder_name)
        self.assertEqual("user", settings.username)
        self.assertEqual("password", settings.password)
        preflight.assert_called_once_with(settings, session=session)
        upload.assert_called_once_with(
            "package.cvx",
            settings,
            session=session,
            progress_factory=progress_factory,
        )

    def test_failed_preflight_does_not_upload(self):
        with (
            mock.patch.object(backend_client, "preflight_remote_upload", return_value=False),
            mock.patch.object(backend_client, "upload_file") as upload,
        ):
            result = backend_client.upload_file_to_folder(
                "package.cvx",
                "ColorVision/Update",
                username="user",
                password="password",
            )

        self.assertFalse(result)
        upload.assert_not_called()


class UploadContentTests(unittest.TestCase):
    class RequestsStub:
        RequestException = RuntimeError

    def test_text_content_uses_http_put_and_retries_server_errors(self):
        failed = mock.Mock(status_code=500, text="temporary")
        succeeded = mock.Mock(status_code=201, text="")
        session = mock.Mock()
        session.put.side_effect = [failed, succeeded]
        settings = backend_client.RemoteUploadSettings(
            base_url="http://example.test:9998",
            folder_name="ColorVision",
            username="user",
            password="password",
            max_retries=2,
        )

        with (
            mock.patch.object(backend_client, "get_requests_module", return_value=self.RequestsStub),
            mock.patch.object(backend_client.time, "sleep"),
        ):
            result = backend_client.upload_content(
                "1.2.3.4",
                "LATEST_RELEASE",
                settings,
                session=session,
            )

        self.assertTrue(result)
        self.assertEqual(2, session.put.call_count)
        _, kwargs = session.put.call_args
        self.assertEqual(b"1.2.3.4", kwargs["data"])
        self.assertEqual("text/plain; charset=utf-8", kwargs["headers"]["Content-Type"])
        self.assertEqual(("user", "password"), kwargs["auth"])


if __name__ == "__main__":
    unittest.main()

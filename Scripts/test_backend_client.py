import os
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

import backend_client


class DummyProgress:
    def __init__(self, *args, **kwargs):
        self.updated = 0

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False

    def update(self, amount):
        self.updated += amount


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


class BackendClientTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.file_path = Path(self.temp_dir.name) / "demo.bin"
        self.file_path.write_bytes(b"payload")
        self.original_env = {
            "COLORVISION_UPLOAD_URL": os.environ.get("COLORVISION_UPLOAD_URL"),
            "COLORVISION_UPLOAD_USERNAME": os.environ.get("COLORVISION_UPLOAD_USERNAME"),
            "COLORVISION_UPLOAD_PASSWORD": os.environ.get("COLORVISION_UPLOAD_PASSWORD"),
            "COLORVISION_UPLOAD_USE_SYSTEM_PROXY": os.environ.get("COLORVISION_UPLOAD_USE_SYSTEM_PROXY"),
        }

    def tearDown(self):
        for key, value in self.original_env.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value
        self.temp_dir.cleanup()

    def test_resolve_upload_credentials_uses_environment(self):
        os.environ["COLORVISION_UPLOAD_USERNAME"] = "tester"
        os.environ["COLORVISION_UPLOAD_PASSWORD"] = "secret"

        username, password = backend_client.resolve_upload_credentials()

        self.assertEqual((username, password), ("tester", "secret"))

    def test_resolve_upload_credentials_uses_current_default_values(self):
        os.environ.pop("COLORVISION_UPLOAD_USERNAME", None)
        os.environ.pop("COLORVISION_UPLOAD_PASSWORD", None)

        username, password = backend_client.resolve_upload_credentials()

        self.assertEqual((username, password), ("xincheng", "xincheng"))

    def test_resolve_upload_base_url_uses_environment(self):
        os.environ["COLORVISION_UPLOAD_URL"] = "http://example.com:9998/"
        self.assertEqual(backend_client.resolve_upload_base_url(), "http://example.com:9998")

    def test_create_http_session_disables_system_proxy_by_default(self):
        class FakeSession:
            def __init__(self):
                self.trust_env = True
                self.proxies = {"http": "http://127.0.0.1:10808"}

        class FakeRequests:
            @staticmethod
            def Session():
                return FakeSession()

        os.environ.pop("COLORVISION_UPLOAD_USE_SYSTEM_PROXY", None)

        session = backend_client.create_http_session(requests_module=FakeRequests)

        self.assertFalse(session.trust_env)
        self.assertEqual(session.proxies, {})

    def test_create_http_session_can_reenable_system_proxy_from_environment(self):
        class FakeSession:
            def __init__(self):
                self.trust_env = True
                self.proxies = {"http": "http://127.0.0.1:10808"}

        class FakeRequests:
            @staticmethod
            def Session():
                return FakeSession()

        os.environ["COLORVISION_UPLOAD_USE_SYSTEM_PROXY"] = "1"

        session = backend_client.create_http_session(requests_module=FakeRequests)

        self.assertTrue(session.trust_env)
        self.assertEqual(session.proxies, {"http": "http://127.0.0.1:10808"})

    def test_build_upload_url_normalizes_windows_separators(self):
        url = backend_client.build_upload_url(
            "http://example.com:9998",
            r"ColorVision\Update",
            "ColorVision-Update-[1.2.3.4].cvx",
        )

        self.assertEqual(
            url,
            "http://example.com:9998/upload/ColorVision/Update/ColorVision-Update-%5B1.2.3.4%5D.cvx",
        )

    def test_upload_file_uses_tuple_auth(self):
        session = DummySession(SimpleNamespace(status_code=201, text=""))
        settings = backend_client.RemoteUploadSettings(
            base_url="http://example.com:9998",
            folder_name="ColorVision",
            username="tester",
            password="secret",
        )

        result = backend_client.upload_file(
            self.file_path,
            settings,
            session=session,
            progress_factory=DummyProgress,
        )

        self.assertTrue(result)
        self.assertEqual(session.calls[0]["auth"], ("tester", "secret"))

    def test_preflight_allows_legacy_server_when_health_endpoint_is_missing(self):
        class Session:
            def get(self, url, timeout=None):
                return SimpleNamespace(status_code=404, text='{"error":"not found"}')

        settings = backend_client.RemoteUploadSettings(
            base_url="http://example.com:9998",
            folder_name="ColorVision",
            username="tester",
            password="secret",
        )

        result = backend_client.preflight_remote_upload(settings, session=Session())

        self.assertTrue(result)

    def test_preflight_allows_legacy_server_when_ready_endpoint_is_missing(self):
        class Session:
            def __init__(self):
                self.calls = 0

            def get(self, url, timeout=None):
                self.calls += 1
                if self.calls == 1:
                    return SimpleNamespace(status_code=200, text='{"status":"ok"}', json=lambda: {"status": "ok"})
                return SimpleNamespace(status_code=404, text='{"error":"not found"}')

        settings = backend_client.RemoteUploadSettings(
            base_url="http://example.com:9998",
            folder_name="ColorVision",
            username="tester",
            password="secret",
        )

        result = backend_client.preflight_remote_upload(settings, session=Session())

        self.assertTrue(result)

    def test_post_multipart_with_auth_forwards_timeout_and_credentials(self):
        class Session:
            def __init__(self):
                self.kwargs = None

            def post(self, *args, **kwargs):
                self.kwargs = kwargs
                return SimpleNamespace(status_code=201, text="ok")

        session = Session()
        response = backend_client.post_multipart_with_auth(
            "http://example.com:9998/api/packages/publish",
            data={"PluginId": "Spectrum"},
            files={"package": ("demo.cvxp", object(), "application/octet-stream")},
            username="tester",
            password="secret",
            connect_timeout=5,
            read_timeout=15,
            session=session,
        )

        self.assertEqual(response.status_code, 201)
        self.assertEqual(session.kwargs["auth"], ("tester", "secret"))
        self.assertEqual(session.kwargs["timeout"], (5, 15))


if __name__ == "__main__":
    unittest.main()



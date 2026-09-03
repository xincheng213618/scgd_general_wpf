import importlib.util
import json
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest import mock

import requests


SDK_SCRIPTS = Path(__file__).resolve().parents[2] / "SDK" / "ColorVision.PluginKit" / "scripts"
spec = importlib.util.spec_from_file_location("shared_manifest", SDK_SCRIPTS / "shared_manifest.py")
shared = importlib.util.module_from_spec(spec)
with mock.patch.dict(sys.modules, {"shared_manifest": shared}):
    spec.loader.exec_module(shared)
    spec = importlib.util.spec_from_file_location("sdk_package_cvxp", SDK_SCRIPTS / "package_cvxp.py")
    sdk = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(sdk)


class SharedManifestTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="pluginkit-manifest-tests-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.version = "1.4.14.1"
        self.framework = "net10.0-windows"
        self.platform = "x64"
        self.url = shared.manifest_url("https://example.test", self.version, self.framework, self.platform)
        self.data = {
            "version": 1, "host_version": self.version, "framework": self.framework, "platform": self.platform,
            "shared_files": ["ColorVision.UI.dll", "runtimes/win-x64/native/opencv_calib3d4140.dll"],
        }
        self.raw = json.dumps(self.data).encode()
        self.cache = shared.cache_path_for(self.url, self.root, self.version, self.framework, self.platform)

    def resolve(self, **kwargs):
        return shared.resolve_remote_manifest(self.url, self.root, self.version, self.framework, self.platform, **kwargs)

    def response(self, *, status=200, raw=None):
        session = mock.MagicMock()
        session.__enter__.return_value = session
        response = session.get.return_value.__enter__.return_value
        response.status_code = status
        response.iter_content.return_value = [self.raw if raw is None else raw]
        return session

    def seed_cache(self):
        shared.write_cache(self.cache, self.raw)

    def test_default_url_is_versioned_public_tool_path(self):
        self.assertEqual("https://example.test/download/Tool/PluginKit/shared-files/1.4.14.1/net10.0-windows-x64.json", self.url)

    def test_rejects_unpinned_versions_unsafe_platforms_and_urls(self):
        for version in ("latest", "1.4", "../../file", 123):
            with self.subTest(version=version), self.assertRaises(ValueError):
                shared.manifest_url("https://example.test", version, self.framework, self.platform)
        for url in ("file:///tmp/manifest.json", "https://user:pass@example.test/a", "https://example.test/#part", "example.test"):
            with self.subTest(url=url), self.assertRaises(ValueError):
                shared.validate_url(url)
        with self.assertRaises(ValueError):
            shared.manifest_url("https://example.test", self.version, "../framework", self.platform)

    def test_rejects_identity_mismatch(self):
        for key, value in (("host_version", "1.4.14.2"), ("framework", "net8.0-windows"), ("platform", "ARM64"), ("version", 2), ("version", True)):
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                shared.validate_manifest({**self.data, key: value}, host_version=self.version)

    def test_normalizes_windows_paths_and_rejects_malformed_lists(self):
        self.assertEqual({"folder/Host.dll"}, shared.validate_manifest(["folder\\Host.dll"]))
        for paths in ([], None, "Host.dll", [42], ["../Host.dll"], ["/Host.dll"], ["C:\\Host.dll"], ["folder/../Host.dll"], ["Host.dll:ads"], ["*.dll"], ["folder//Host.dll"], ["folder/Host.dll "], ["\x00Host.dll"]):
            with self.subTest(paths=paths), self.assertRaises(ValueError):
                shared.validate_manifest({"shared_files": paths})

    def test_download_validates_then_caches_without_credentials_or_redirects(self):
        session = self.response()
        with mock.patch("requests.Session", return_value=session):
            self.assertEqual(self.cache, self.resolve())
        self.assertEqual(self.raw, self.cache.read_bytes())
        self.assertFalse(session.trust_env)
        self.assertNotIn("auth", session.get.call_args.kwargs)
        self.assertFalse(session.get.call_args.kwargs["allow_redirects"])

    def test_cache_is_scoped_to_remote_source_and_host(self):
        other_source = shared.cache_path_for(self.url.replace("example.test", "other.test"), self.root, self.version, self.framework, self.platform)
        other_host = shared.cache_path_for(self.url, self.root, "1.4.14.2", self.framework, self.platform)
        self.assertNotEqual(self.cache, other_source)
        self.assertNotEqual(self.cache, other_host)

    def test_network_failure_uses_matching_valid_cache(self):
        self.seed_cache()
        with mock.patch("requests.Session", side_effect=requests.ConnectionError):
            self.assertEqual(self.cache, self.resolve())

    def test_network_failure_does_not_use_missing_or_corrupt_cache(self):
        for raw in (None, b"invalid", json.dumps({**self.data, "host_version": "1.4.14.2"}).encode()):
            if raw is not None:
                shared.write_cache(self.cache, raw)
            with self.subTest(raw=raw), mock.patch("requests.Session", side_effect=requests.ConnectionError), self.assertRaises(RuntimeError):
                self.resolve()

    def test_server_unavailable_uses_cache_but_404_or_redirect_does_not(self):
        self.seed_cache()
        with mock.patch("requests.Session", return_value=self.response(status=503)):
            self.assertEqual(self.cache, self.resolve())
        for status in (404, 401, 302):
            with self.subTest(status=status), mock.patch("requests.Session", return_value=self.response(status=status)), self.assertRaises(ValueError):
                self.resolve()

    def test_bad_response_never_replaces_cache_or_silently_falls_back(self):
        self.seed_cache()
        for raw in (b"not json", json.dumps({**self.data, "platform": "ARM64"}).encode(), b"x" * (shared.MAX_MANIFEST_BYTES + 1)):
            with self.subTest(size=len(raw)), mock.patch("requests.Session", return_value=self.response(raw=raw)), self.assertRaises(ValueError):
                self.resolve()
            self.assertEqual(self.raw, self.cache.read_bytes())

    def test_offline_requires_matching_cache_and_never_connects(self):
        with mock.patch("requests.Session") as factory:
            with self.assertRaises(FileNotFoundError):
                self.resolve(offline=True)
            self.seed_cache()
            self.assertEqual(self.cache, self.resolve(offline=True))
            factory.assert_not_called()

    def test_explicit_local_missing_path_never_falls_back(self):
        with self.assertRaises(FileNotFoundError):
            sdk.resolve_shared_files_path(self.root / "missing.json")

    def test_legacy_resolution_uses_embedded_runtime_path(self):
        self.seed_cache()
        with mock.patch.object(sdk, "DEFAULT_SHARED_FILES", self.cache):
            self.assertEqual(self.cache, sdk.resolve_shared_files_path(None))

    def test_cli_check_only_has_no_build_package_or_upload(self):
        self.seed_cache()
        argv = ["cvplugin", "--shared-files", str(self.cache), "--target-host-version", self.version, "--check-shared-files", "--build"]
        with mock.patch.object(sys, "argv", argv), mock.patch.object(sdk, "run_build_step") as build, mock.patch.object(sdk, "package_plugin") as package, mock.patch.object(sdk, "upload_file") as upload, mock.patch.object(sdk, "resolve_remote_manifest") as remote:
            sdk.main()
        for operation in (build, package, upload, remote):
            operation.assert_not_called()

    def test_cli_remote_check_uses_config_target_and_relative_cache(self):
        self.seed_cache()
        config = self.root / "config.json"
        config.write_text(json.dumps({"targetHostVersion": self.version, "sharedFilesCacheDir": "cache", "uploadUrl": "https://example.test"}))
        with mock.patch.object(sys, "argv", ["cvplugin", "--config", str(config), "--check-shared-files", "--offline"]), mock.patch.object(sdk, "resolve_remote_manifest", return_value=self.cache) as remote:
            sdk.main()
        remote.assert_called_once_with(self.url, self.root / "cache", self.version, self.framework, self.platform, offline=True)

    def test_cli_remote_url_without_target_is_rejected(self):
        with mock.patch.object(sys, "argv", ["cvplugin", "--shared-files-url", self.url, "--check-shared-files"]), self.assertRaises(ValueError):
            sdk.main()

    def test_build_only_does_not_require_or_download_manifest(self):
        with mock.patch.object(sys, "argv", ["cvplugin", "--build-only"]), mock.patch.object(sdk, "run_build_step") as build, mock.patch.object(sdk, "resolve_remote_manifest") as remote, mock.patch.object(sdk, "resolve_shared_files_path") as local:
            sdk.main()
        build.assert_called_once()
        remote.assert_not_called()
        local.assert_not_called()

    def test_package_strips_only_selected_host_files_and_preserves_private_runtime(self):
        src = self.root / "output"
        for name in (*self.data["shared_files"], "Pattern.dll", "runtimes/win-x64/native/opencv_calib3d4130.dll"):
            path = src / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"fixture")
        package = self.root / "Pattern.cvxp"
        sdk.package_plugin(src, src, shared.validate_manifest(self.data, host_version=self.version), package, "Pattern")
        with zipfile.ZipFile(package) as archive:
            self.assertIsNone(archive.testzip())
            self.assertIn("Pattern/Pattern.dll", archive.namelist())
            self.assertIn("Pattern/runtimes/win-x64/native/opencv_calib3d4130.dll", archive.namelist())
            self.assertNotIn("Pattern/ColorVision.UI.dll", archive.namelist())
            self.assertEqual(self.data["shared_files"], json.loads(archive.read("Pattern/stripped_files.json")))


if __name__ == "__main__":
    unittest.main()

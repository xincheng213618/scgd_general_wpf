"""Opt-in tests of the bundled executable using only a loopback HTTP server."""

import hashlib
import json
import os
import subprocess
import sys
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class PluginKitExecutableTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        configured = os.environ.get("COLORVISION_TEST_CVPLUGIN_EXE")
        if os.name != "nt" or not configured or not Path(configured).is_file():
            raise unittest.SkipTest("Set COLORVISION_TEST_CVPLUGIN_EXE to an existing cvplugin.exe on Windows.")
        cls.executable = Path(configured).resolve()

    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory(prefix="pluginkit-executable-tests-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.version = "1.4.14.1"
        self.relative_url = f"/download/Tool/PluginKit/shared-files/{self.version}/net10.0-windows-x64.json"
        self.data = {
            "version": 1, "host_version": self.version, "framework": "net10.0-windows", "platform": "x64",
            "shared_files": ["Host.dll"],
        }
        self.response_body = json.dumps(self.data).encode("utf-8")
        self.response_status = 200
        self.requests: list[tuple[str, str, str | None]] = []
        owner = self

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                owner.requests.append(("GET", self.path, self.headers.get("Authorization")))
                status = owner.response_status if self.path == owner.relative_url else 404
                self.send_response(status)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(owner.response_body)))
                self.end_headers()
                self.wfile.write(owner.response_body)

            def do_PUT(self):
                owner.requests.append(("PUT", self.path, self.headers.get("Authorization")))
                self.send_error(405)

            def do_POST(self):
                owner.requests.append(("POST", self.path, self.headers.get("Authorization")))
                self.send_error(405)

            def log_message(self, *args):
                pass

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.server.daemon_threads = True
        self.thread = threading.Thread(target=self.server.serve_forever, kwargs={"poll_interval": 0.05}, daemon=True)
        self.thread.start()
        self.addCleanup(self.stop_server)
        base_url = f"http://127.0.0.1:{self.server.server_port}"
        full_url = base_url + self.relative_url
        self.cache = self.root / "cache" / hashlib.sha256(full_url.encode("utf-8")).hexdigest() / self.version / "net10.0-windows-x64.json"
        self.build_marker = self.root / "build-was-executed.txt"
        build_command = subprocess.list2cmdline([
            sys.executable, "-c", f"from pathlib import Path; Path({str(self.build_marker)!r}).write_text('built')",
        ])
        self.config = self.root / "pluginkit.config.json"
        self.config_data = {
            "targetHostVersion": self.version,
            "sharedFilesCacheDir": "cache",
            "uploadUrl": base_url,
            "username": "fixture-user",
            "password": "fixture-password",
            "buildCommand": build_command,
            "buildWorkingDir": str(self.root),
            "buildEnabled": True,
            "uploadEnabled": False,
            "outputDir": "packages",
        }
        self.config.write_text(json.dumps(self.config_data), encoding="utf-8")

    def stop_server(self) -> None:
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=3)

    def run_executable(self, *args: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [str(self.executable), "--config", str(self.config), *args],
            cwd=self.root, capture_output=True, text=True, encoding="utf-8", errors="replace",
            timeout=45, creationflags=subprocess.CREATE_NO_WINDOW,
        )

    def assert_success(self, result: subprocess.CompletedProcess[str]) -> None:
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def assert_check_only_side_effects(self) -> None:
        self.assertFalse(self.build_marker.exists(), "check-only executed the configured build command")
        self.assertFalse((self.root / "packages").exists())
        self.assertEqual([], list(self.root.rglob("*.cvxp")))
        self.assertTrue(all(request == ("GET", self.relative_url, None) for request in self.requests), self.requests)

    def test_check_only_downloads_refreshes_and_offline_makes_no_request(self) -> None:
        first = self.run_executable("--check-shared-files", "--build")
        self.assert_success(first)
        self.assertIn("Shared file count: 1", first.stdout)
        self.assertEqual(self.response_body, self.cache.read_bytes())
        self.assertEqual(1, len(self.requests))

        self.response_body = json.dumps({**self.data, "shared_files": ["Host.dll", "New.dll"]}).encode("utf-8")
        second = self.run_executable("--check-shared-files", "--build")
        self.assert_success(second)
        self.assertIn("Shared file count: 2", second.stdout)
        self.assertEqual(self.response_body, self.cache.read_bytes())
        self.assertEqual(2, len(self.requests))

        self.response_status = 404
        offline = self.run_executable("--check-shared-files", "--offline", "--build")
        self.assert_success(offline)
        self.assertIn("Shared file count: 2", offline.stdout)
        self.assertEqual(2, len(self.requests))
        self.assert_check_only_side_effects()

    def test_wrong_host_metadata_and_404_do_not_fall_back_to_valid_cache(self) -> None:
        self.assert_success(self.run_executable("--check-shared-files", "--build"))
        valid_cache = self.cache.read_bytes()
        self.response_body = json.dumps({**self.data, "host_version": "1.4.14.2"}).encode("utf-8")
        mismatch = self.run_executable("--check-shared-files", "--build")
        self.assertNotEqual(0, mismatch.returncode)
        self.assertIn("host_version mismatch", mismatch.stdout + mismatch.stderr)
        self.assertEqual(valid_cache, self.cache.read_bytes())

        self.response_status = 404
        missing = self.run_executable("--check-shared-files", "--build")
        self.assertNotEqual(0, missing.returncode)
        self.assertIn("HTTP 404", missing.stdout + missing.stderr)
        self.assertEqual(valid_cache, self.cache.read_bytes())
        self.assertEqual(3, len(self.requests))
        self.assert_check_only_side_effects()

    def test_build_only_does_not_connect_or_require_manifest(self) -> None:
        self.response_status = 404
        self.assert_success(self.run_executable("--build-only"))
        self.assertTrue(self.build_marker.exists())
        self.assertEqual([], self.requests)
        self.assertFalse(self.cache.exists())
        self.assertFalse((self.root / "packages").exists())

    def test_legacy_config_uses_embedded_manifest_without_network(self) -> None:
        del self.config_data["targetHostVersion"]
        self.config.write_text(json.dumps(self.config_data), encoding="utf-8")
        result = self.run_executable("--check-shared-files", "--build")
        self.assert_success(result)
        self.assertIn("legacy local/embedded manifest", result.stdout)
        self.assertIn("Shared file count:", result.stdout)
        self.assertEqual([], self.requests)
        self.assert_check_only_side_effects()


if __name__ == "__main__":
    unittest.main()

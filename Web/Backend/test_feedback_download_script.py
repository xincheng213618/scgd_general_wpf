from __future__ import annotations

import hashlib
import json
import os
import shutil
import subprocess
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


PAYLOAD = b"PK\x03\x04isolated-diagnostics-zip"
FEEDBACK_ID = "20260916_143000_BJT_ARVR-PC_a1b2c3d4e5f6"


class _FeedbackHandler(BaseHTTPRequestHandler):
    truncate_download = False

    def do_GET(self):
        if self.headers.get("Authorization") != "Bearer isolated-test-key":
            self.send_response(401)
            self.end_headers()
            return
        if self.path == f"/api/feedback/{FEEDBACK_ID}":
            payload = {
                "feedback_id": FEEDBACK_ID,
                "machine_name": "ARVR-PC",
                "created_at": "2026-09-16T06:30:00Z",
                "message": "fixture",
                "attachments": [{
                    "name": "diagnostics.zip",
                    "size_bytes": len(PAYLOAD),
                    "sha256": hashlib.sha256(PAYLOAD).hexdigest(),
                    "modified_at": "2026-09-16T06:30:00+00:00",
                }],
            }
            self._json(payload)
            return
        if self.path.startswith("/api/feedback?limit="):
            self._json({"items": [
                {"feedback_id": "20260917_010000_incomplete", "created_at": "2026-09-17T01:00:00Z", "metadata_valid": False},
                {"feedback_id": FEEDBACK_ID, "created_at": "2026-09-16T06:30:00Z", "machine_name": "ARVR-PC", "metadata_valid": True},
            ], "total": 2})
            return
        if self.path == f"/api/feedback/{FEEDBACK_ID}/attachments/diagnostics.zip":
            body = PAYLOAD[:-4] if self.truncate_download else PAYLOAD
            self.send_response(200)
            self.send_header("Content-Type", "application/zip")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        self.send_response(404)
        self.end_headers()

    def _json(self, payload):
        body = json.dumps(payload).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *_args):
        pass


@unittest.skipUnless(shutil.which("pwsh"), "PowerShell 7 is required for the downloader test")
class FeedbackDownloadScriptTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.root = Path(self._temp.name)
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), _FeedbackHandler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.base_url = f"http://127.0.0.1:{self.server.server_port}"
        self.script = Path(__file__).resolve().parents[2] / "Scripts" / "download_feedback.ps1"
        self.environment = {**os.environ, "COLORVISION_FEEDBACK_API_KEY": "isolated-test-key", "COLORVISION_FEEDBACK_ALLOW_HTTP": "0"}

    def tearDown(self):
        _FeedbackHandler.truncate_download = False
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=5)
        self._temp.cleanup()

    def _run(self, *arguments):
        command = "& '" + str(self.script).replace("'", "''") + "' "
        command += " ".join(value if value.startswith("-") else "'" + value.replace("'", "''") + "'" for value in arguments)
        return subprocess.run(
            ["pwsh", "-NoProfile", "-Command", command + " | ConvertTo-Json -Depth 20"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            env=self.environment,
            timeout=30,
        )

    def test_download_is_atomic_verified_and_idempotent(self):
        output = self.root / "downloads"
        arguments = (
            "-BaseUrl", self.base_url,
            "-FeedbackId", FEEDBACK_ID,
            "-OutputDirectory", str(output),
            "-AllowInsecureLocalhost",
        )
        first = self._run(*arguments)
        self.assertEqual(first.returncode, 0, first.stderr)
        target = output / FEEDBACK_ID / "diagnostics.zip"
        self.assertEqual(target.read_bytes(), PAYLOAD)
        self.assertTrue((output / FEEDBACK_ID / "feedback-manifest.json").is_file())
        self.assertEqual(list((output / FEEDBACK_ID).glob("*.part.*")), [])

        second = self._run(*arguments)
        self.assertEqual(second.returncode, 0, second.stderr)
        self.assertEqual(target.read_bytes(), PAYLOAD)

    def test_truncated_download_never_becomes_completed_file(self):
        _FeedbackHandler.truncate_download = True
        output = self.root / "failed"
        result = self._run(
            "-BaseUrl", self.base_url,
            "-FeedbackId", FEEDBACK_ID,
            "-OutputDirectory", str(output),
            "-AllowInsecureLocalhost",
        )

        self.assertNotEqual(result.returncode, 0)
        feedback_dir = output / FEEDBACK_ID
        self.assertFalse((feedback_dir / "diagnostics.zip").exists())
        self.assertFalse((feedback_dir / "feedback-manifest.json").exists())
        self.assertEqual(list(feedback_dir.glob("*.part.*")), [])

    def test_list_is_explicit_and_does_not_select_a_global_latest_record(self):
        result = self._run(
            "-BaseUrl", self.base_url,
            "-List",
            "-Query", "fixture",
            "-AllowInsecureLocalhost",
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(FEEDBACK_ID, result.stdout)

    def test_latest_remote_downloads_and_reports_the_selected_machine_and_time(self):
        result = self._run(
            "-Latest", "-Source", "Remote", "-BaseUrl", self.base_url,
            "-OutputDirectory", str(self.root / "latest"), "-AllowInsecureLocalhost",
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("ARVR-PC", result.stdout)
        self.assertIn("2026-09-16 14:30:00", result.stdout)
        self.assertEqual((self.root / "latest" / FEEDBACK_ID / "diagnostics.zip").read_bytes(), PAYLOAD)

    def test_latest_local_uses_metadata_not_mtime_and_needs_no_api_key(self):
        share = self.root / "share"
        for identifier, instant, machine in (
            ("20260919_010000_older", "2026-09-19T16:00:00+08:00", "PC-OLD"),
            ("20260919_090000_newer", "2026-09-19T09:00:00Z", "PC-NEW"),
        ):
            directory = share / identifier
            directory.mkdir(parents=True)
            (directory / "feedback.json").write_text(json.dumps({"createdAt": instant, "machineInfo": f"{machine} / Windows"}), encoding="utf-8")
        os.utime(share / "20260919_010000_older", (2000000000, 2000000000))
        (share / "20260920_090000_incomplete").mkdir()
        self.environment.pop("COLORVISION_FEEDBACK_API_KEY")
        result = self._run("-Latest", "-Source", "Local", "-LocalRoot", str(share))
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("20260919_090000_newer", result.stdout)
        self.assertIn("PC-NEW", result.stdout)
        self.assertIn("2026-09-19 17:00:00", result.stdout)
        filtered = self._run("-Latest", "-Source", "Local", "-LocalRoot", str(share), "-Machine", "PC-OLD")
        self.assertEqual(filtered.returncode, 0, filtered.stderr)
        self.assertIn("20260919_010000_older", filtered.stdout)

    def test_explicit_http_opt_in_works_and_unapproved_http_is_rejected(self):
        allowed = self._run("-List", "-BaseUrl", self.base_url, "-AllowInsecureHttp")
        self.assertEqual(allowed.returncode, 0, allowed.stderr)
        denied = self._run("-List", "-BaseUrl", self.base_url)
        self.assertNotEqual(denied.returncode, 0)
        self.assertIn("HTTP requires explicit", denied.stderr)
        unsupported = self._run("-List", "-BaseUrl", "ftp://example.invalid")
        self.assertNotEqual(unsupported.returncode, 0)


if __name__ == "__main__":
    unittest.main()

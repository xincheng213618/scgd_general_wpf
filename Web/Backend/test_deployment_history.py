from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from services.deployment_history import query_deployment_history


class DeploymentHistoryTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.storage = Path(self.temp_dir.name)
        records = [
            {
                "timestamp": "2026-08-10T12:00:00+08:00",
                "status": "failed",
                "source": "origin",
                "target_commit": "a" * 40,
                "backup_path": r"D:\ColorVision\web-deploy-backups\20260810-120000",
                "runtime_log_path": r"D:\ColorVision\Logs\Web\ColorVisionWeb.log",
                "server": "PRIVATE-NAS",
                "error": r"Frontend build failed at D:\private\source with exit code 1.",
                "recovery": ["removed_staged_frontend", r"recovery_failed: D:\private\source"],
            },
            {
                "timestamp": "2026-08-11T12:00:00+08:00",
                "status": "already_current",
                "commit": "b" * 40,
                "health": "ok",
                "ready": True,
            },
            {
                "timestamp": "2026-08-12T12:00:00+08:00",
                "status": "success",
                "source": "git_bundle",
                "previous_commit": "b" * 40,
                "deployed_commit": "c" * 40,
                "frontend_build": "success",
                "backend_targeted_tests": "passed",
                "history_retention": {
                    "status": "success",
                    "keep_records": 500,
                    "before_count": 499,
                    "after_count": 500,
                    "removed_count": 0,
                },
            },
        ]
        lines = [json.dumps(record) for record in records]
        lines.extend(("{broken", "[]"))
        (self.storage / "web-deploy-history.jsonl").write_text(
            "\n".join(lines) + "\n",
            encoding="utf-8",
        )
        with (self.storage / "web-deploy-history.jsonl").open("ab") as stream:
            stream.write(b"\xff\n")

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_latest_first_pagination_and_summary(self):
        result = query_deployment_history(self.storage, limit=2)

        self.assertEqual(result["total"], 3)
        self.assertEqual([entry["status"] for entry in result["entries"]], ["success", "already_current"])
        self.assertEqual(result["summary"]["records"], 3)
        self.assertEqual(result["summary"]["malformed_records"], 3)
        self.assertEqual(result["summary"]["retention_limit"], 500)
        self.assertEqual(result["summary"]["statuses"], {"already_current": 1, "failed": 1, "success": 1})

    def test_filters_and_does_not_expose_private_paths_or_server(self):
        result = query_deployment_history(
            self.storage,
            status="failed",
            source="origin",
            commit="aaaa",
        )

        self.assertEqual(result["total"], 1)
        entry = result["entries"][0]
        self.assertEqual(entry["backup_name"], "20260810-120000")
        self.assertEqual(entry["failure_reason"], "frontend_build")
        self.assertEqual(entry["recovery"], ["removed_staged_frontend", "recovery_failed"])
        serialized = json.dumps(result)
        self.assertNotIn("D:\\", serialized)
        self.assertNotIn("PRIVATE-NAS", serialized)
        self.assertNotIn("runtime_log_path", serialized)
        self.assertNotIn("server", serialized)
        self.assertNotIn("error", entry)

    def test_missing_history_is_empty_and_query_bounds_are_enforced(self):
        missing = self.storage / "missing"
        missing.mkdir()
        result = query_deployment_history(missing)
        self.assertEqual(result["entries"], [])
        self.assertEqual(result["total"], 0)
        with self.assertRaises(ValueError):
            query_deployment_history(self.storage, limit=0)
        with self.assertRaises(ValueError):
            query_deployment_history(self.storage, offset=-1)


if __name__ == "__main__":
    unittest.main()

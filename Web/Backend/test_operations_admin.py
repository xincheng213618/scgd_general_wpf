from __future__ import annotations

import base64
import copy
import json
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path

import app as marketplace_app


class OperationsAdminTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.storage = self.root / "storage"
        self.storage.mkdir()
        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)
        self.original_secret = marketplace_app.app.secret_key
        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {
            "username": "admin",
            "password": "secret",
        }
        marketplace_app.CONFIG["secret_key"] = "operations-admin-test"
        marketplace_app.app.secret_key = "operations-admin-test"
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.init_db()
        self.client = marketplace_app.app.test_client()
        self._seed_operations()

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.secret_key = self.original_secret
        self.temp_dir.cleanup()

    @staticmethod
    def _auth():
        token = base64.b64encode(b"admin:secret").decode()
        return {"Authorization": f"Basic {token}"}

    def _seed_operations(self):
        now = datetime.now(timezone.utc)
        recent = now.isoformat()
        stale = (now - timedelta(minutes=5)).isoformat()
        snapshot = json.dumps({
            "application": "ColorVision",
            "version": "1.4.12.40",
            "isRunning": True,
            "uptimeSeconds": 3600,
            "capturedAt": recent,
            "process": {"memoryMb": 512.5, "processId": 9988},
            "mainWindow": {"exists": True, "state": "Normal", "isVisible": True},
            "secureOperations": {
                "isRunning": True,
                "pairedDeviceCount": 2,
                "relayConfigured": True,
                "relayRunning": True,
            },
            "endpoint": "https://private.example/relay",
            "userName": "private-user",
        })
        db = marketplace_app._cache.get_db()
        try:
            db.executemany(
                """INSERT INTO operations_hosts
                   (host_id, display_name, app_version, status, capabilities,
                    snapshot, last_seen_at, created_at, updated_at)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                [
                    ("host-online", "Inspection 1", "1.4.12.40", "online",
                     json.dumps(["ops.status.read", "ops.diagnostics.bundle.create"]),
                     snapshot, recent, stale, recent),
                    ("host-stale", "Inspection 2", "1.4.12.39", "online",
                     "not-json", "not-json", stale, stale, stale),
                ],
            )
            db.executemany(
                """INSERT INTO operations_tasks
                   (task_id, host_id, capability_id, payload, status,
                    idempotency_key, created_by, created_at, expires_at, delivered_at)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                [
                    ("task-pending", "host-online", "ops.diagnostics.request",
                     json.dumps({"reason": "private support reason"}), "queued",
                     "pending-1", "1", recent, (now + timedelta(minutes=10)).isoformat(), None),
                    ("task-failed", "host-online", "ops.deployment.verify",
                     json.dumps({"releasePath": "D:/private/release"}), "failed",
                     "failed-1", "1", stale, recent, stale),
                ],
            )
            db.execute(
                "INSERT INTO operations_task_receipts VALUES (?, ?, ?, ?, ?, ?)",
                ("receipt-1", "task-failed", "host-online", "failed",
                 json.dumps({"error": "private machine path"}), recent),
            )
            db.executemany(
                "INSERT INTO operations_support_events VALUES (?, ?, ?, ?, ?, ?)",
                [
                    ("event-1", "host-online", "session-active", "session.requested", "{}", stale),
                    ("event-2", "host-online", "session-active", "session.active", "{}", recent),
                    ("event-3", "host-online", "session-active", "message",
                     json.dumps({"text": "private support message"}), recent),
                    ("event-4", "host-stale", "session-closed", "session.requested", "{}", stale),
                    ("event-5", "host-stale", "session-closed", "session.closed", "{}", recent),
                ],
            )
            db.commit()
        finally:
            db.close()

    def test_overview_is_admin_only_bounded_and_sanitized(self):
        self.assertEqual(
            self.client.get("/api/admin/operations/overview").status_code,
            401,
        )
        response = self.client.get(
            "/api/admin/operations/overview?hostLimit=20&activityLimit=20",
            headers=self._auth(),
        )
        self.assertEqual(response.status_code, 200)
        result = response.get_json()
        self.assertEqual(result["summary"], {
            "activeSupportSessions": 1,
            "failedTasks": 1,
            "onlineHosts": 1,
            "pendingTasks": 1,
            "staleHosts": 1,
            "totalHosts": 2,
            "totalTasks": 2,
        })
        self.assertTrue(result["hosts"][0]["online"])
        self.assertFalse(result["hosts"][1]["online"])
        self.assertEqual(result["hosts"][0]["snapshot"]["process"], {"memoryMb": 512.5})
        failed_task = next(
            item for item in result["recentTasks"] if item["taskId"] == "task-failed"
        )
        self.assertEqual(failed_task["receiptCount"], 1)
        self.assertEqual(result["supportSessions"][0]["messageCount"], 1)

        serialized = json.dumps(result, ensure_ascii=False)
        for sensitive in (
            "private.example",
            "private-user",
            "private support reason",
            "private machine path",
            "private support message",
            "processId",
            "releasePath",
        ):
            self.assertNotIn(sensitive, serialized)

    def test_overview_rejects_unbounded_limits(self):
        too_many = self.client.get(
            "/api/admin/operations/overview?hostLimit=201",
            headers=self._auth(),
        )
        invalid = self.client.get(
            "/api/admin/operations/overview?activityLimit=nope",
            headers=self._auth(),
        )
        self.assertEqual(too_many.status_code, 400)
        self.assertEqual(invalid.status_code, 400)


if __name__ == "__main__":
    unittest.main()

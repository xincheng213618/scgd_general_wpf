import copy
import tempfile
import unittest
from pathlib import Path

import app as marketplace_app
from services.api_key_service import create_api_key


class OperationsRelayTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.storage = self.root / "storage"
        self.storage.mkdir()
        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.init_db()
        self.client = marketplace_app.app.test_client()
        self.relay_key = create_api_key(
            marketplace_app._cache, name="relay", scopes="ops:relay", created_by="test"
        )["key"]
        self.operator_key = create_api_key(
            marketplace_app._cache, name="operator", scopes="ops:operator", created_by="test"
        )["key"]

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        self.temp_dir.cleanup()

    def auth(self, key):
        return {"Authorization": f"Bearer {key}"}

    def heartbeat(self, host_id="host-1"):
        return self.client.post(
            f"/api/ops/v1/hosts/{host_id}/heartbeat",
            headers=self.auth(self.relay_key),
            json={
                "displayName": "Line 1",
                "appVersion": "1.4.10.4",
                "status": "online",
                "capabilities": ["ops.status.read"],
                "snapshot": {"healthy": True},
            },
        )

    def test_relay_routes_require_scoped_api_keys(self):
        response = self.client.post("/api/ops/v1/hosts/host-1/heartbeat", json={})
        self.assertEqual(response.status_code, 401)

        wrong_scope = self.client.get(
            "/api/ops/v1/hosts", headers=self.auth(self.relay_key)
        )
        self.assertEqual(wrong_scope.status_code, 401)

    def test_heartbeat_task_poll_and_receipt_round_trip(self):
        self.assertEqual(self.heartbeat().status_code, 200)
        created = self.client.post(
            "/api/ops/v1/tasks",
            headers=self.auth(self.operator_key),
            json={
                "hostId": "host-1",
                "capabilityId": "ops.diagnostics.request",
                "payload": {"reason": "field support"},
                "idempotencyKey": "diag-1",
            },
        )
        self.assertEqual(created.status_code, 202)
        task_id = created.get_json()["taskId"]

        polled = self.client.get(
            "/api/ops/v1/hosts/host-1/tasks", headers=self.auth(self.relay_key)
        )
        self.assertEqual(polled.status_code, 200)
        self.assertEqual(polled.get_json()["tasks"][0]["taskId"], task_id)

        receipt = self.client.post(
            f"/api/ops/v1/hosts/host-1/tasks/{task_id}/receipts",
            headers=self.auth(self.relay_key),
            json={"status": "awaiting_local_consent", "evidence": {"jobId": "local-job"}},
        )
        self.assertEqual(receipt.status_code, 201)

    def test_task_catalog_rejects_privileged_or_command_payloads(self):
        self.heartbeat()
        privileged = self.client.post(
            "/api/ops/v1/tasks",
            headers=self.auth(self.operator_key),
            json={"hostId": "host-1", "capabilityId": "ops.service.restart", "payload": {}},
        )
        self.assertEqual(privileged.status_code, 400)
        self.assertEqual(privileged.get_json()["error"], "task_capability_not_allowed")

        command = self.client.post(
            "/api/ops/v1/tasks",
            headers=self.auth(self.operator_key),
            json={
                "hostId": "host-1",
                "capabilityId": "ops.support.message",
                "payload": {"command": "whoami"},
            },
        )
        self.assertEqual(command.status_code, 400)
        self.assertEqual(command.get_json()["error"], "task_payload_not_allowed")

    def test_support_events_are_bounded_and_do_not_accept_commands(self):
        allowed = self.client.post(
            "/api/ops/v1/hosts/host-1/support-events",
            headers=self.auth(self.relay_key),
            json={"sessionId": "session-1", "eventType": "message", "payload": {"text": "Check cable"}},
        )
        self.assertEqual(allowed.status_code, 201)

        denied = self.client.post(
            "/api/ops/v1/hosts/host-1/support-events",
            headers=self.auth(self.relay_key),
            json={"sessionId": "session-1", "eventType": "message", "payload": {"shell": "cmd.exe"}},
        )
        self.assertEqual(denied.status_code, 400)


if __name__ == "__main__":
    unittest.main()

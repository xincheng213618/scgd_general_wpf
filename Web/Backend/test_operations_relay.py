import copy
import base64
import hashlib
import json
import os
import tempfile
import unittest
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path
from unittest.mock import patch

import app as marketplace_app
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec, padding, rsa
from cryptography.x509.oid import NameOID
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

        audit = marketplace_app._cache.get_audit_log_page(
            actor="key:",
            limit=20,
            offset=0,
        )
        operations_entries = [
            item for item in audit["entries"]
            if item["action"] in {"operations.heartbeat", "operations.task.create"}
        ]
        self.assertEqual(len(operations_entries), 2)
        self.assertTrue(all(item["actor_id"].startswith("key:") for item in operations_entries))

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
        self.assertEqual(self.heartbeat().status_code, 200)
        requested = self.client.post(
            "/api/ops/v1/hosts/host-1/support-events",
            headers=self.auth(self.relay_key),
            json={"sessionId": "session-1", "eventType": "session.requested", "payload": {}},
        )
        self.assertEqual(requested.status_code, 201)
        active = self.client.post(
            "/api/ops/v1/hosts/host-1/support-events",
            headers=self.auth(self.relay_key),
            json={"sessionId": "session-1", "eventType": "session.active", "payload": {}},
        )
        self.assertEqual(active.status_code, 201)
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

    def test_support_message_tasks_require_an_active_session_and_exact_payload(self):
        self.assertEqual(self.heartbeat().status_code, 200)
        inactive = self.client.post(
            "/api/ops/v1/tasks",
            headers=self.auth(self.operator_key),
            json={
                "hostId": "host-1",
                "capabilityId": "ops.support.message",
                "payload": {"sessionId": "session-2", "text": "Check cable"},
            },
        )
        self.assertEqual(inactive.status_code, 409)
        self.assertEqual(inactive.get_json()["error"], "support_session_not_active")

        for event_type in ("session.requested", "session.active"):
            response = self.client.post(
                "/api/ops/v1/hosts/host-1/support-events",
                headers=self.auth(self.relay_key),
                json={"sessionId": "session-2", "eventType": event_type, "payload": {}},
            )
            self.assertEqual(response.status_code, 201)

        accepted = self.client.post(
            "/api/ops/v1/tasks",
            headers=self.auth(self.operator_key),
            json={
                "hostId": "host-1",
                "capabilityId": "ops.support.message",
                "payload": {"sessionId": "session-2", "text": "Check cable"},
            },
        )
        self.assertEqual(accepted.status_code, 202)

        extra_field = self.client.post(
            "/api/ops/v1/tasks",
            headers=self.auth(self.operator_key),
            json={
                "hostId": "host-1",
                "capabilityId": "ops.support.message",
                "payload": {"sessionId": "session-2", "text": "Check cable", "path": "C:/private"},
            },
        )
        self.assertEqual(extra_field.status_code, 400)
        self.assertEqual(extra_field.get_json()["error"], "invalid_support_message_payload")

    def test_device_signed_relay_round_trip_without_bearer_secret(self):
        identity = self.device_relay_identity()
        synced = self.sync_device_relay(identity)
        self.assertEqual(synced.status_code, 200)
        self.assertEqual(synced.get_json()["deviceCount"], 1)

        snapshot_path = f"/api/ops/v1/device-relay/hosts/{identity['host_id']}/snapshot"
        snapshot_body = self.json_bytes({})
        snapshot = self.client.post(
            snapshot_path,
            data=snapshot_body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", snapshot_path, snapshot_body),
        )
        self.assertEqual(snapshot.status_code, 200)
        self.assertTrue(snapshot.get_json()["host"]["snapshot"]["isRunning"])
        self.assertEqual(
            json.loads(snapshot.get_json()["hostEnvelope"]["body"])["hostId"],
            identity["host_id"],
        )

        create_path = "/api/ops/v1/device-relay/tasks"
        task_body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.window.show",
            "payload": {},
            "idempotencyKey": "show-main-window-1",
            "ttlSeconds": 300,
        })
        task_headers = self.device_headers(identity, "POST", create_path, task_body)
        created = self.client.post(
            create_path, data=task_body, content_type="application/json", headers=task_headers
        )
        self.assertEqual(created.status_code, 202)
        task_id = created.get_json()["taskId"]

        poll_path = f"/api/ops/v1/device-relay/hosts/{identity['host_id']}/tasks"
        poll_body = self.json_bytes({})
        polled = self.client.post(
            poll_path,
            data=poll_body,
            content_type="application/json",
            headers=self.host_headers(identity, "POST", poll_path, poll_body),
        )
        self.assertEqual(polled.status_code, 200)
        relay_task = polled.get_json()["tasks"][0]
        self.assertEqual(relay_task["taskId"], task_id)
        self.assertEqual(relay_task["requestBody"].encode("utf-8"), task_body)
        self.assertEqual(relay_task["signature"], task_headers["X-CV-Signature"])

        receipt_path = (
            f"/api/ops/v1/device-relay/hosts/{identity['host_id']}"
            f"/tasks/{task_id}/receipts"
        )
        receipt_status = "completed"
        receipt_evidence = {"actionId": "ops.window.show"}
        receipt_signed_at = int(datetime.now(timezone.utc).timestamp())
        receipt_envelope_body = self.json_bytes({
            "hostId": identity["host_id"],
            "taskId": task_id,
            "idempotencyKey": "show-main-window-1",
            "status": receipt_status,
            "evidence": receipt_evidence,
            "signedAt": receipt_signed_at,
        }).decode("utf-8")
        receipt_body = self.json_bytes({
            "status": receipt_status,
            "evidence": receipt_evidence,
            "receiptEnvelope": self.host_envelope(
                identity, "colorvision-relay-receipt-v1", receipt_envelope_body
            ),
        })
        receipt = self.client.post(
            receipt_path,
            data=receipt_body,
            content_type="application/json",
            headers=self.host_headers(identity, "POST", receipt_path, receipt_body),
        )
        self.assertEqual(receipt.status_code, 201)

        status_path = f"/api/ops/v1/device-relay/tasks/{task_id}"
        status_body = self.json_bytes({"hostId": identity["host_id"]})
        status = self.client.post(
            status_path,
            data=status_body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", status_path, status_body),
        )
        self.assertEqual(status.status_code, 200)
        self.assertEqual(status.get_json()["task"]["status"], "completed")
        self.assertEqual(status.get_json()["task"]["receipts"][0]["status"], "completed")
        self.assertEqual(
            status.get_json()["task"]["receipts"][0]["hostEnvelope"]["body"],
            receipt_envelope_body,
        )

    def test_device_relay_rejects_tampering_replay_and_revocation(self):
        identity = self.device_relay_identity()
        self.assertEqual(self.sync_device_relay(identity).status_code, 200)
        path = "/api/ops/v1/device-relay/tasks"
        body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.diagnostics.request",
            "payload": {"reason": "field support"},
            "idempotencyKey": "diagnostic-1",
        })
        headers = self.device_headers(identity, "POST", path, body)
        tampered = body.replace(b"field support", b"silent exploit")
        rejected = self.client.post(path, data=tampered, content_type="application/json", headers=headers)
        self.assertEqual(rejected.status_code, 401)
        self.assertEqual(rejected.get_json()["error"], "invalid_request_signature")

        created = self.client.post(path, data=body, content_type="application/json", headers=headers)
        self.assertEqual(created.status_code, 202)
        replayed = self.client.post(path, data=body, content_type="application/json", headers=headers)
        self.assertEqual(replayed.status_code, 409)
        self.assertEqual(replayed.get_json()["error"], "replayed_request")

        revoked_at = datetime.now(timezone.utc).isoformat()
        self.assertEqual(self.sync_device_relay(identity, revoked_at=revoked_at).status_code, 200)
        fresh_body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.window.show",
            "payload": {},
            "idempotencyKey": "show-after-revoke",
        })
        revoked = self.client.post(
            path,
            data=fresh_body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, fresh_body),
        )
        self.assertEqual(revoked.status_code, 401)
        self.assertEqual(revoked.get_json()["error"], "unknown_or_revoked_device")

    def test_device_relay_accepts_only_empty_bounded_actions(self):
        identity = self.device_relay_identity()
        self.assertEqual(self.sync_device_relay(identity).status_code, 200)
        path = "/api/ops/v1/device-relay/tasks"

        for capability_id, idempotency_key in (
            ("ops.window.show", "show-window"),
            ("ops.window.minimize", "minimize-window"),
            ("ops.messaging.reconnect", "reconnect-message-channel"),
            ("ops.flow.cancel", "cancel-current-flow"),
            ("ops.application.restart", "restart-application"),
            ("ops.service.restart", "restart-mqtt-service"),
        ):
            body = self.json_bytes({
                "hostId": identity["host_id"],
                "capabilityId": capability_id,
                "payload": {},
                "idempotencyKey": idempotency_key,
            })
            response = self.client.post(
                path,
                data=body,
                content_type="application/json",
                headers=self.device_headers(identity, "POST", path, body),
            )
            self.assertEqual(response.status_code, 202)

        payload_body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.window.minimize",
            "payload": {"title": "another window"},
            "idempotencyKey": "minimize-with-payload",
        })
        rejected = self.client.post(
            path,
            data=payload_body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, payload_body),
        )
        self.assertEqual(rejected.status_code, 400)
        self.assertEqual(rejected.get_json()["error"], "window_minimize_payload_not_allowed")

        reconnect_payload_body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.messaging.reconnect",
            "payload": {"endpoint": "other-broker"},
            "idempotencyKey": "reconnect-with-payload",
        })
        reconnect_rejected = self.client.post(
            path,
            data=reconnect_payload_body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, reconnect_payload_body),
        )
        self.assertEqual(reconnect_rejected.status_code, 400)
        self.assertEqual(
            reconnect_rejected.get_json()["error"],
            "message_reconnect_payload_not_allowed",
        )

        cancel_payload_body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.flow.cancel",
            "payload": {"flowId": "remote-selection"},
            "idempotencyKey": "cancel-with-payload",
        })
        cancel_rejected = self.client.post(
            path,
            data=cancel_payload_body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, cancel_payload_body),
        )
        self.assertEqual(cancel_rejected.status_code, 400)
        self.assertEqual(
            cancel_rejected.get_json()["error"],
            "flow_cancel_payload_not_allowed",
        )

        restart_payload_body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.application.restart",
            "payload": {"executablePath": "other.exe"},
            "idempotencyKey": "restart-with-payload",
        })
        restart_rejected = self.client.post(
            path,
            data=restart_payload_body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, restart_payload_body),
        )
        self.assertEqual(restart_rejected.status_code, 400)
        self.assertEqual(
            restart_rejected.get_json()["error"],
            "application_restart_payload_not_allowed",
        )

        for index, payload in enumerate((
            {"serviceId": "mosquitto"},
            {"command": "restart"},
            {"path": "another-service"},
        )):
            mqtt_restart_body = self.json_bytes({
                "hostId": identity["host_id"],
                "capabilityId": "ops.service.restart",
                "payload": payload,
                "idempotencyKey": f"mqtt-restart-with-payload-{index}",
            })
            mqtt_restart_rejected = self.client.post(
                path,
                data=mqtt_restart_body,
                content_type="application/json",
                headers=self.device_headers(identity, "POST", path, mqtt_restart_body),
            )
            self.assertEqual(mqtt_restart_rejected.status_code, 400)
            self.assertEqual(
                mqtt_restart_rejected.get_json()["error"],
                "mqtt_restart_payload_not_allowed",
            )

    def test_device_relay_mqtt_restart_is_idempotent_and_rejects_conflicts(self):
        identity = self.device_relay_identity()
        self.assertEqual(self.sync_device_relay(identity).status_code, 200)
        path = "/api/ops/v1/device-relay/tasks"
        body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.service.restart",
            "payload": {},
            "idempotencyKey": "restart-mqtt-idempotent",
            "ttlSeconds": 300,
        })

        created = self.client.post(
            path,
            data=body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, body),
        )
        self.assertEqual(created.status_code, 202)
        task_id = created.get_json()["taskId"]

        duplicate = self.client.post(
            path,
            data=body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, body),
        )
        self.assertEqual(duplicate.status_code, 200)
        self.assertTrue(duplicate.get_json()["deduplicated"])
        self.assertEqual(duplicate.get_json()["taskId"], task_id)

        conflicting_body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.service.restart",
            "payload": {},
            "idempotencyKey": "restart-mqtt-idempotent",
            "ttlSeconds": 600,
        })
        conflict = self.client.post(
            path,
            data=conflicting_body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, conflicting_body),
        )
        self.assertEqual(conflict.status_code, 409)
        self.assertEqual(conflict.get_json()["error"], "idempotency_conflict")

    def test_failure_evidence_task_requires_empty_payload_scope_and_is_idempotent(self):
        identity = self.device_relay_identity()
        self.assertEqual(self.sync_device_relay(identity).status_code, 200)
        path = "/api/ops/v1/device-relay/tasks"
        body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.diagnostics.failures.read",
            "payload": {},
            "idempotencyKey": "failure-evidence-idempotent",
            "ttlSeconds": 300,
        })

        missing_scope = self.client.post(
            path,
            data=body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, body),
        )
        self.assertEqual(missing_scope.status_code, 403)
        self.assertEqual(missing_scope.get_json()["error"], "device_scope_required")

        scopes = ["ops.window.control", "ops.jobs.create", "ops.diagnostics.read"]
        self.assertEqual(self.sync_device_relay(identity, scopes=scopes).status_code, 200)
        created = self.client.post(
            path,
            data=body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, body),
        )
        self.assertEqual(created.status_code, 202)
        task_id = created.get_json()["taskId"]

        duplicate = self.client.post(
            path,
            data=body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, body),
        )
        self.assertEqual(duplicate.status_code, 200)
        self.assertTrue(duplicate.get_json()["deduplicated"])
        self.assertEqual(duplicate.get_json()["taskId"], task_id)

        conflicting_body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.diagnostics.failures.read",
            "payload": {},
            "idempotencyKey": "failure-evidence-idempotent",
            "ttlSeconds": 600,
        })
        conflict = self.client.post(
            path,
            data=conflicting_body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, conflicting_body),
        )
        self.assertEqual(conflict.status_code, 409)
        self.assertEqual(conflict.get_json()["error"], "idempotency_conflict")

        for index, payload in enumerate((
            {"serviceId": "event-log"},
            {"command": "wevtutil"},
            {"path": "C:\\Windows"},
            {"rawLog": "secret"},
            {"file": "dump.dmp"},
            [],
            None,
        )):
            rejected_body = self.json_bytes({
                "hostId": identity["host_id"],
                "capabilityId": "ops.diagnostics.failures.read",
                "payload": payload,
                "idempotencyKey": f"failure-evidence-payload-{index}",
            })
            rejected = self.client.post(
                path,
                data=rejected_body,
                content_type="application/json",
                headers=self.device_headers(identity, "POST", path, rejected_body),
            )
            self.assertEqual(rejected.status_code, 400)
            self.assertEqual(
                rejected.get_json()["error"], "failure_evidence_payload_not_allowed"
            )

        missing_payload_body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.diagnostics.failures.read",
            "idempotencyKey": "failure-evidence-missing-payload",
        })
        missing_payload = self.client.post(
            path,
            data=missing_payload_body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, missing_payload_body),
        )
        self.assertEqual(missing_payload.status_code, 400)
        self.assertEqual(
            missing_payload.get_json()["error"], "failure_evidence_payload_not_allowed"
        )

        snapshot_path = f"/api/ops/v1/device-relay/hosts/{identity['host_id']}/snapshot"
        snapshot_body = self.json_bytes({})
        snapshot = self.client.post(
            snapshot_path,
            data=snapshot_body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", snapshot_path, snapshot_body),
        )
        self.assertEqual(snapshot.status_code, 200)
        self.assertIn(
            "ops.diagnostics.failures.read", snapshot.get_json()["host"]["capabilities"]
        )

        self.assertEqual(self.heartbeat("legacy-host").status_code, 200)
        legacy = self.client.post(
            "/api/ops/v1/tasks",
            headers=self.auth(self.operator_key),
            json={
                "hostId": "legacy-host",
                "capabilityId": "ops.diagnostics.failures.read",
                "payload": {},
            },
        )
        self.assertEqual(legacy.status_code, 400)
        self.assertEqual(legacy.get_json()["error"], "task_capability_not_allowed")

    def test_failure_evidence_completed_receipt_requires_exact_signed_schema(self):
        identity = self.device_relay_identity()
        scopes = ["ops.window.control", "ops.jobs.create", "ops.diagnostics.read"]
        self.assertEqual(self.sync_device_relay(identity, scopes=scopes).status_code, 200)
        task_id = self.create_failure_evidence_task(identity, "failure-evidence-receipt")
        valid = self.failure_evidence()

        invalid_values = []
        for extra_name in ("rawLog", "path", "file"):
            value = copy.deepcopy(valid)
            value[extra_name] = "forbidden"
            invalid_values.append(value)
        missing = copy.deepcopy(valid)
        missing.pop("dumpCount")
        invalid_values.append(missing)
        for name, value in (
            ("kind", "failure-evidence-v2"),
            ("eventLogAvailable", 1),
            ("windowDays", 6),
            ("windowDays", 7.0),
            ("crashCount", -1),
            ("hangCount", 1000),
            ("dumpCount", True),
            ("latestEventAt", "not-an-iso-time"),
            ("windowStartedAt", None),
            ("observedAt", "2026-08-13T12:00:00"),
            ("observedAt", "2026-08-13 12:00:00+00:00"),
            ("observedAt", "2026-W33-4T12:00:00+00:00"),
            ("observedAt", "2026-08-13T12:00:00.1234567890Z"),
            ("observedAt", "2026-08-13T12:00:00+00:99"),
            ("observedAt", "2026-08-13T12:00:00+18:01"),
            ("observedAt", "2026-08-13T12:00:00+19:00"),
        ):
            invalid = copy.deepcopy(valid)
            invalid[name] = value
            invalid_values.append(invalid)

        reversed_window = copy.deepcopy(valid)
        reversed_window["windowStartedAt"] = "2026-08-14T00:00:00+00:00"
        invalid_values.append(reversed_window)
        outside_window = copy.deepcopy(valid)
        outside_window["latestDumpAt"] = "2026-08-20T00:00:00+00:00"
        outside_window["latestEvidenceAt"] = outside_window["latestDumpAt"]
        invalid_values.append(outside_window)
        wrong_latest = copy.deepcopy(valid)
        wrong_latest["latestEvidenceAt"] = wrong_latest["latestEventAt"]
        invalid_values.append(wrong_latest)
        no_evidence_with_counts = copy.deepcopy(valid)
        no_evidence_with_counts["hasEvidence"] = False
        invalid_values.append(no_evidence_with_counts)
        no_evidence_with_latest = copy.deepcopy(valid)
        no_evidence_with_latest["hasEvidence"] = False
        for name in (
            "failureEventCount", "crashCount", "hangCount", "managedRuntimeFailureCount",
            "windowsErrorReportCount", "dumpCount",
        ):
            no_evidence_with_latest[name] = 0
        invalid_values.append(no_evidence_with_latest)
        evidence_without_counts = copy.deepcopy(valid)
        evidence_without_counts["hasEvidence"] = True
        for name in (
            "failureEventCount", "crashCount", "hangCount", "managedRuntimeFailureCount",
            "windowsErrorReportCount", "dumpCount",
        ):
            evidence_without_counts[name] = 0
        evidence_without_counts["latestEventAt"] = None
        evidence_without_counts["latestDumpAt"] = None
        evidence_without_counts["latestEvidenceAt"] = None
        invalid_values.append(evidence_without_counts)
        category_without_total = copy.deepcopy(evidence_without_counts)
        category_without_total["hasEvidence"] = False
        category_without_total["crashCount"] = 1
        invalid_values.append(category_without_total)
        event_count_without_time = copy.deepcopy(valid)
        event_count_without_time["latestEventAt"] = None
        invalid_values.append(event_count_without_time)
        event_time_without_count = copy.deepcopy(valid)
        event_time_without_count["failureEventCount"] = 0
        invalid_values.append(event_time_without_count)
        dump_count_without_time = copy.deepcopy(valid)
        dump_count_without_time["latestDumpAt"] = None
        dump_count_without_time["latestEvidenceAt"] = dump_count_without_time["latestEventAt"]
        invalid_values.append(dump_count_without_time)
        dump_time_without_count = copy.deepcopy(valid)
        dump_time_without_count["dumpCount"] = 0
        invalid_values.append(dump_time_without_count)

        for evidence in invalid_values:
            response = self.post_failure_evidence_receipt(
                identity,
                task_id,
                "failure-evidence-receipt",
                "completed",
                evidence,
            )
            self.assertEqual(response.status_code, 400)
            self.assertEqual(response.get_json()["error"], "invalid_failure_evidence")

        invalid_signature = self.post_failure_evidence_receipt(
            identity,
            task_id,
            "failure-evidence-receipt",
            "completed",
            valid,
            corrupt_envelope_signature=True,
        )
        self.assertEqual(invalid_signature.status_code, 401)
        self.assertEqual(
            invalid_signature.get_json()["error"], "invalid_host_envelope_signature"
        )

        accepted = self.post_failure_evidence_receipt(
            identity,
            task_id,
            "failure-evidence-receipt",
            "completed",
            valid,
        )
        self.assertEqual(accepted.status_code, 201)

        empty_task_id = self.create_failure_evidence_task(
            identity, "failure-evidence-empty-receipt"
        )
        empty = copy.deepcopy(valid)
        empty["hasEvidence"] = False
        for name in (
            "failureEventCount", "crashCount", "hangCount", "managedRuntimeFailureCount",
            "windowsErrorReportCount", "dumpCount",
        ):
            empty[name] = 0
        empty["latestEventAt"] = None
        empty["latestDumpAt"] = None
        empty["latestEvidenceAt"] = None
        empty_accepted = self.post_failure_evidence_receipt(
            identity,
            empty_task_id,
            "failure-evidence-empty-receipt",
            "completed",
            empty,
        )
        self.assertEqual(empty_accepted.status_code, 201)

        for status in ("received", "accepted", "awaiting_local_consent", "rejected"):
            rejected = self.post_failure_evidence_receipt(
                identity,
                task_id,
                "failure-evidence-receipt",
                status,
                {},
            )
            self.assertEqual(rejected.status_code, 400)
            self.assertEqual(rejected.get_json()["error"], "invalid_failure_evidence")

    def test_failure_evidence_failed_receipt_has_exact_error_schema(self):
        identity = self.device_relay_identity()
        scopes = ["ops.window.control", "ops.jobs.create", "ops.diagnostics.read"]
        self.assertEqual(self.sync_device_relay(identity, scopes=scopes).status_code, 200)
        task_id = self.create_failure_evidence_task(identity, "failure-evidence-failed")
        exact = {
            "kind": "failure-evidence-error-v1",
            "code": "failure_evidence_unavailable",
        }
        accepted = self.post_failure_evidence_receipt(
            identity, task_id, "failure-evidence-failed", "failed", exact
        )
        self.assertEqual(accepted.status_code, 201)

        for invalid in (
            {**exact, "path": "C:\\Windows"},
            {**exact, "rawLog": "access denied"},
            {"kind": "failure-evidence-error-v1", "code": "other"},
            self.failure_evidence(),
        ):
            rejected = self.post_failure_evidence_receipt(
                identity, task_id, "failure-evidence-failed", "failed", invalid
            )
            self.assertEqual(rejected.status_code, 400)
            self.assertEqual(rejected.get_json()["error"], "invalid_failure_evidence")

    def test_host_signed_terminal_receipt_retry_is_idempotent(self):
        identity = self.device_relay_identity()
        self.assertEqual(self.sync_device_relay(identity).status_code, 200)
        task_path = "/api/ops/v1/device-relay/tasks"
        task_body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.service.restart",
            "payload": {},
            "idempotencyKey": "restart-mqtt-receipt-retry",
        })
        created = self.client.post(
            task_path,
            data=task_body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", task_path, task_body),
        )
        self.assertEqual(created.status_code, 202)
        task_id = created.get_json()["taskId"]

        receipt_path = (
            f"/api/ops/v1/device-relay/hosts/{identity['host_id']}"
            f"/tasks/{task_id}/receipts"
        )
        signed_at = int(datetime.now(timezone.utc).timestamp())
        evidence = {"evidenceId": "servicehost:request-1"}
        receipt_envelope_body = self.json_bytes({
            "hostId": identity["host_id"],
            "taskId": task_id,
            "idempotencyKey": "restart-mqtt-receipt-retry",
            "status": "completed",
            "evidence": evidence,
            "signedAt": signed_at,
        }).decode("utf-8")
        receipt_body = self.json_bytes({
            "status": "completed",
            "evidence": evidence,
            "receiptEnvelope": self.host_envelope(
                identity, "colorvision-relay-receipt-v1", receipt_envelope_body
            ),
        })
        headers = self.host_headers(
            identity, "POST", receipt_path, receipt_body, timestamp=signed_at
        )

        first = self.client.post(
            receipt_path, data=receipt_body, content_type="application/json", headers=headers
        )
        self.assertEqual(first.status_code, 201)
        first_receipt_id = first.get_json()["receiptId"]

        retry_headers = self.host_headers(
            identity, "POST", receipt_path, receipt_body, timestamp=signed_at
        )
        retry = self.client.post(
            receipt_path,
            data=receipt_body,
            content_type="application/json",
            headers=retry_headers,
        )
        self.assertEqual(retry.status_code, 200)
        self.assertTrue(retry.get_json()["deduplicated"])
        self.assertEqual(retry.get_json()["receiptId"], first_receipt_id)

        db = marketplace_app._cache.get_db()
        try:
            self.assertEqual(
                db.execute(
                    "SELECT COUNT(*) FROM operations_task_receipts WHERE task_id=?",
                    (task_id,),
                ).fetchone()[0],
                1,
            )
        finally:
            db.close()

    @staticmethod
    def json_bytes(value):
        return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")

    @staticmethod
    def p256_public_key_spki(key):
        return base64.b64encode(key.public_key().public_bytes(
            serialization.Encoding.DER,
            serialization.PublicFormat.SubjectPublicKeyInfo,
        )).decode("ascii")

    def create_window_snapshot_task(
            self, identity, idempotency_key, *, payload=None, ttl=300):
        recipient_key = ec.generate_private_key(ec.SECP256R1())
        if payload is None:
            payload = {
                "scheme": "p256-hkdf-sha256-aes256gcm-v1",
                "recipientPublicKeySpki": self.p256_public_key_spki(recipient_key),
            }
        body_value = {
            "hostId": identity["host_id"],
            "capabilityId": "ops.window.snapshot.capture",
            "payload": payload,
            "idempotencyKey": idempotency_key,
        }
        if ttl is not None:
            body_value["ttlSeconds"] = ttl
        path = "/api/ops/v1/device-relay/tasks"
        body = self.json_bytes(body_value)
        response = self.client.post(
            path,
            data=body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, body),
        )
        return response, recipient_key

    def window_snapshot_evidence(self, sealed, *, captured_at=None, expires_at=None):
        captured_at = captured_at or datetime.now(timezone.utc)
        expires_at = expires_at or captured_at + timedelta(minutes=4)
        host_ephemeral_key = ec.generate_private_key(ec.SECP256R1())
        return {
            "kind": "window-snapshot-encrypted-v1",
            "scheme": "p256-hkdf-sha256-aes256gcm-v1",
            "jobId": uuid.uuid4().hex,
            "hostEphemeralPublicKeySpki": self.p256_public_key_spki(host_ephemeral_key),
            "sealedSha256": hashlib.sha256(sealed).hexdigest(),
            "sealedBytes": len(sealed),
            "capturedAt": captured_at.isoformat(),
            "expiresAt": expires_at.isoformat(),
        }

    def upload_window_snapshot(
            self, identity, task_id, idempotency_key, sealed, *, evidence=None,
            signed_at=None, corrupt_envelope_signature=False,
            corrupt_request_signature=False, extra_metadata=None):
        evidence = copy.deepcopy(evidence or self.window_snapshot_evidence(sealed))
        signed_at = signed_at if signed_at is not None else int(
            datetime.now(timezone.utc).timestamp())
        receipt_body = self.json_bytes({
            "hostId": identity["host_id"],
            "taskId": task_id,
            "idempotencyKey": idempotency_key,
            "status": "completed",
            "evidence": evidence,
            "signedAt": signed_at,
        }).decode("utf-8")
        envelope = self.host_envelope(
            identity, "colorvision-relay-receipt-v1", receipt_body
        )
        if corrupt_envelope_signature:
            envelope["signature"] = base64.b64encode(b"invalid").decode("ascii")
        metadata = {
            "status": "completed",
            "evidence": evidence,
            "receiptEnvelope": envelope,
        }
        if extra_metadata:
            metadata.update(extra_metadata)
        metadata_header = base64.b64encode(self.json_bytes(metadata)).decode("ascii")
        path = (
            f"/api/ops/v1/device-relay/hosts/{identity['host_id']}"
            f"/tasks/{task_id}/window-snapshot"
        )
        signed_body = b"not-the-uploaded-ciphertext" if corrupt_request_signature else sealed
        headers = self.host_headers(
            identity, "POST", path, signed_body, timestamp=signed_at
        )
        headers["X-CV-Receipt-Metadata"] = metadata_header
        response = self.client.post(
            path,
            data=sealed,
            content_type="application/octet-stream",
            headers=headers,
        )
        return response, metadata_header, evidence

    def download_window_snapshot(self, identity, task_id, *, timestamp=None):
        path = f"/api/ops/v1/device-relay/tasks/{task_id}/window-snapshot"
        body = self.json_bytes({"hostId": identity["host_id"]})
        return self.client.post(
            path,
            data=body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, body, timestamp=timestamp),
        )

    def consume_window_snapshot(
            self, identity, task_id, sealed_sha256, *, timestamp=None):
        path = f"/api/ops/v1/device-relay/tasks/{task_id}/window-snapshot/consume"
        body = self.json_bytes({
            "hostId": identity["host_id"],
            "sealedSha256": sealed_sha256,
        })
        return self.client.post(
            path,
            data=body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, body, timestamp=timestamp),
        )

    def create_failure_evidence_task(self, identity, idempotency_key):
        path = "/api/ops/v1/device-relay/tasks"
        body = self.json_bytes({
            "hostId": identity["host_id"],
            "capabilityId": "ops.diagnostics.failures.read",
            "payload": {},
            "idempotencyKey": idempotency_key,
        })
        response = self.client.post(
            path,
            data=body,
            content_type="application/json",
            headers=self.device_headers(identity, "POST", path, body),
        )
        self.assertEqual(response.status_code, 202)
        return response.get_json()["taskId"]

    @staticmethod
    def failure_evidence():
        observed_at = datetime(2026, 8, 13, 12, 0, tzinfo=timezone.utc)
        event_at = observed_at - timedelta(hours=2)
        dump_at = observed_at - timedelta(hours=1)
        return {
            "kind": "failure-evidence-v1",
            "eventLogAvailable": True,
            "dumpFolderAvailable": True,
            "eventScanLimited": False,
            "dumpScanLimited": False,
            "hasEvidence": True,
            "windowDays": 7,
            "failureEventCount": 1,
            "crashCount": 1,
            "hangCount": 0,
            "managedRuntimeFailureCount": 0,
            "windowsErrorReportCount": 0,
            "dumpCount": 1,
            "latestEventAt": event_at.isoformat(),
            "latestDumpAt": dump_at.isoformat(),
            "latestEvidenceAt": dump_at.isoformat(),
            "windowStartedAt": (observed_at - timedelta(days=7)).isoformat(),
            "observedAt": observed_at.isoformat(),
        }

    def post_failure_evidence_receipt(
            self, identity, task_id, idempotency_key, status, evidence,
            *, corrupt_envelope_signature=False):
        path = (
            f"/api/ops/v1/device-relay/hosts/{identity['host_id']}"
            f"/tasks/{task_id}/receipts"
        )
        signed_at = int(datetime.now(timezone.utc).timestamp())
        envelope_body = self.json_bytes({
            "hostId": identity["host_id"],
            "taskId": task_id,
            "idempotencyKey": idempotency_key,
            "status": status,
            "evidence": evidence,
            "signedAt": signed_at,
        }).decode("utf-8")
        envelope = self.host_envelope(
            identity, "colorvision-relay-receipt-v1", envelope_body
        )
        if corrupt_envelope_signature:
            envelope["signature"] = base64.b64encode(b"invalid-signature").decode("ascii")
        body = self.json_bytes({
            "status": status,
            "evidence": evidence,
            "receiptEnvelope": envelope,
        })
        return self.client.post(
            path,
            data=body,
            content_type="application/json",
            headers=self.host_headers(identity, "POST", path, body, timestamp=signed_at),
        )

    def device_relay_identity(self):
        host_id = uuid.uuid4().hex
        host_key = rsa.generate_private_key(public_exponent=65537, key_size=3072)
        subject = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, f"ColorVision Operations {host_id}")])
        now = datetime.now(timezone.utc)
        certificate = (
            x509.CertificateBuilder()
            .subject_name(subject)
            .issuer_name(subject)
            .public_key(host_key.public_key())
            .serial_number(x509.random_serial_number())
            .not_valid_before(now - timedelta(minutes=1))
            .not_valid_after(now + timedelta(days=30))
            .sign(host_key, hashes.SHA256())
        )
        device_key = ec.generate_private_key(ec.SECP256R1())
        return {
            "host_id": host_id,
            "host_key": host_key,
            "certificate": base64.b64encode(
                certificate.public_bytes(serialization.Encoding.DER)
            ).decode("ascii"),
            "device_id": uuid.uuid4().hex,
            "device_key": device_key,
            "public_key": base64.b64encode(
                device_key.public_key().public_bytes(
                    serialization.Encoding.DER,
                    serialization.PublicFormat.SubjectPublicKeyInfo,
                )
            ).decode("ascii"),
        }

    def sync_device_relay(self, identity, revoked_at=None, scopes=None):
        path = f"/api/ops/v1/device-relay/hosts/{identity['host_id']}/sync"
        app_version = "1.4.10.4"
        status = "online"
        capabilities = [
            "ops.window.show",
            "ops.window.minimize",
            "ops.messaging.reconnect",
            "ops.flow.cancel",
            "ops.application.restart",
            "ops.service.restart",
            "ops.window.snapshot.capture",
            "ops.diagnostics.failures.read",
            "ops.diagnostics.request",
        ]
        snapshot = {"isRunning": True, "mainWindow": {"state": "Normal"}}
        signed_at = int(datetime.now(timezone.utc).timestamp())
        snapshot_envelope_body = self.json_bytes({
            "hostId": identity["host_id"],
            "appVersion": app_version,
            "status": status,
            "capabilities": capabilities,
            "snapshot": snapshot,
            "signedAt": signed_at,
        }).decode("utf-8")
        body = self.json_bytes({
            "hostId": identity["host_id"],
            "displayName": "Line 1",
            "appVersion": app_version,
            "status": status,
            "capabilities": capabilities,
            "snapshot": snapshot,
            "snapshotEnvelope": self.host_envelope(
                identity, "colorvision-relay-snapshot-v1", snapshot_envelope_body
            ),
            "devices": [{
                "deviceId": identity["device_id"],
                "displayName": "Test phone",
                "publicKeySpki": identity["public_key"],
                "scopes": scopes if scopes is not None else [
                    "ops.window.control", "ops.jobs.create"
                ],
                "approvedAt": datetime.now(timezone.utc).isoformat(),
                "revokedAt": revoked_at,
            }],
        })
        headers = self.host_headers(identity, "POST", path, body)
        headers["X-CV-Host-Certificate"] = identity["certificate"]
        return self.client.post(path, data=body, content_type="application/json", headers=headers)

    @staticmethod
    def host_envelope(identity, prefix, body_text):
        signature = identity["host_key"].sign(
            f"{prefix}\n{body_text}".encode("utf-8"),
            padding.PKCS1v15(),
            hashes.SHA256(),
        )
        return {
            "body": body_text,
            "signature": base64.b64encode(signature).decode("ascii"),
        }

    @staticmethod
    def signed_canonical(method, path, timestamp, nonce, body):
        return "\n".join((
            method.upper(), path, timestamp, nonce, hashlib.sha256(body).hexdigest()
        )).encode("utf-8")

    def host_headers(self, identity, method, path, body, timestamp=None):
        timestamp = str(timestamp if timestamp is not None else int(
            datetime.now(timezone.utc).timestamp()))
        nonce = uuid.uuid4().hex
        signature = identity["host_key"].sign(
            self.signed_canonical(method, path, timestamp, nonce, body),
            padding.PKCS1v15(),
            hashes.SHA256(),
        )
        return {
            "X-CV-Host-Id": identity["host_id"],
            "X-CV-Timestamp": timestamp,
            "X-CV-Nonce": nonce,
            "X-CV-Signature": base64.b64encode(signature).decode("ascii"),
        }

    def device_headers(self, identity, method, path, body, timestamp=None):
        timestamp = str(timestamp if timestamp is not None else int(
            datetime.now(timezone.utc).timestamp()))
        nonce = uuid.uuid4().hex
        signature = identity["device_key"].sign(
            self.signed_canonical(method, path, timestamp, nonce, body),
            ec.ECDSA(hashes.SHA256()),
        )
        return {
            "X-CV-Device-Id": identity["device_id"],
            "X-CV-Timestamp": timestamp,
            "X-CV-Nonce": nonce,
            "X-CV-Signature": base64.b64encode(signature).decode("ascii"),
        }


if __name__ == "__main__":
    unittest.main()

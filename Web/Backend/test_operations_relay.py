import copy
import base64
import hashlib
import json
import tempfile
import unittest
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path

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

        with self.cache.get_db() as db:
            self.assertEqual(
                db.execute(
                    "SELECT COUNT(*) FROM operations_task_receipts WHERE task_id=?",
                    (task_id,),
                ).fetchone()[0],
                1,
            )

    @staticmethod
    def json_bytes(value):
        return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")

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

    def sync_device_relay(self, identity, revoked_at=None):
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
                "scopes": ["ops.window.control", "ops.jobs.create"],
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

    def device_headers(self, identity, method, path, body):
        timestamp = str(int(datetime.now(timezone.utc).timestamp()))
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

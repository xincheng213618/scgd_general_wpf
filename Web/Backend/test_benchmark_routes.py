from __future__ import annotations

import base64
import copy
import re
import tempfile
import unittest
from contextlib import closing
from datetime import datetime
from pathlib import Path
from types import SimpleNamespace
from unittest import mock
from uuid import uuid4

from flask import Flask

from app_setup import ColorVisionRequest
from db_cache import CacheManager
from routes import admin_api
from routes.benchmark_api import register_benchmark_api
from routes.request_context import current_request_context
from services.api_key_service import create_api_key, revoke_api_key
from services.auth_policy import AuthPolicy
from services.benchmark_service import REFERENCE_MEDIANS


class BenchmarkRouteTests(unittest.TestCase):
    def setUp(self):
        isolated = tempfile.TemporaryDirectory()
        self.addCleanup(isolated.cleanup)
        self.cache = CacheManager(Path(isolated.name) / "isolated.db")
        self.cache.init_db()
        self.upload_key = create_api_key(self.cache, name="bench test", scopes="benchmark:submit")
        app = Flask(__name__)
        app.config.update(TESTING=True, SECRET_KEY="test-only-session")
        app.request_class = ColorVisionRequest
        context = SimpleNamespace(
            cache=self.cache,
            auth_policy=AuthPolicy(self.cache, lambda: ("config-admin", "test-only-password")),
            request_context_factory=current_request_context,
        )
        patch = mock.patch.object(admin_api, "_ctx", admin_api._ctx)
        patch.start()
        self.addCleanup(patch.stop)
        admin_api.register_admin_api_routes(app, context)
        register_benchmark_api(app, self.cache)
        self.client = app.test_client()
        self.device_id = str(uuid4())

    @staticmethod
    def bearer(key):
        return {"Authorization": "Bearer " + key["key"]}

    def use_session(self, role):
        with self.client.session_transaction() as session:
            session.clear()
            session.update(username=role, role=role, user_authenticated=True, authenticated=role == "admin")

    @staticmethod
    def desktop_token(value: int) -> str:
        return "cvbench_" + base64.urlsafe_b64encode(bytes([value]) * 32).rstrip(b"=").decode("ascii")

    def payload(self, *, device_id=None, alias="一号测试位", second=1, speed_factors=(1, 1, 1, 1)):
        return {
            "schemaVersion": 1,
            "deviceId": device_id or self.device_id,
            "deviceName": alias,
            "run": {
                "id": str(uuid4()),
                "workloadVersion": "cv-host-cpu-memory-v1",
                "measuredAtUtc": f"2026-11-21T06:00:{second:02d}.1234567Z",
                "appStartedAtUtc": "2026-11-21T05:55:00Z",
                "approximateSystemStartedAtUtc": "2026-11-20T07:00:00Z",
                "appVersion": "1.4.17.148",
                "runtime": ".NET 10.0",
                "logicalProcessors": 24,
                "availableMemoryMiBBefore": 32768,
                "frameWidth": 9568,
                "frameHeight": 6380,
                "metrics": [
                    {"id": name, "samplesMilliseconds": [base * factor * ((90 + n) / 100) for n in range(20, -1, -1)]}
                    for (name, base), factor in zip(REFERENCE_MEDIANS.items(), speed_factors)
                ],
            },
        }

    def submit(self, payload, key=None):
        return self.client.post("/api/benchmarks/runs", json=payload, headers=self.bearer(key or self.upload_key))

    def submit_desktop(self, payload, token=None, addr="198.51.100.10"):
        return self.client.post(
            "/api/benchmarks/runs", json=payload,
            headers={"Authorization": "Bearer " + (token or self.desktop_token(1))},
            environ_overrides={"REMOTE_ADDR": addr},
        )

    def test_submit_requires_explicit_bearer_scope_and_read_is_private(self):
        body = self.payload()
        self.assertEqual(self.client.post("/api/benchmarks/runs", json=body).status_code, 401)
        basic = base64.b64encode(b"config-admin:test-only-password").decode("ascii")
        self.assertEqual(self.client.post("/api/benchmarks/runs", json=body, headers={
            "Authorization": "Basic " + basic,
        }).status_code, 401)

        other = create_api_key(self.cache, name="not upload", scopes="stats:read")
        self.assertEqual(self.submit(body, other).status_code, 403)
        self.assertEqual(self.submit(body).status_code, 201)

        leaderboard = "/api/admin/benchmarks/leaderboard"
        history = "/api/admin/benchmarks/devices/" + self.device_id
        for path in (leaderboard, history):
            self.assertEqual(self.client.get(path).status_code, 401)
            self.assertEqual(self.client.get(path, headers=self.bearer(self.upload_key)).status_code, 403)
        self.use_session("user")
        for path in (leaderboard, history):
            self.assertEqual(self.client.get(path).status_code, 403)
        self.use_session("admin")
        for path in (leaderboard, history):
            response = self.client.get(path)
            self.assertEqual(response.status_code, 200)
            self.assertEqual(response.headers["Cache-Control"], "no-store")
        self.assertEqual(self.client.post("/api/benchmarks/runs", json=self.payload()).status_code, 401)

        self.assertTrue(revoke_api_key(self.cache, self.upload_key["id"]))
        self.assertEqual(self.submit(body).status_code, 401)

    def test_scores_recomputed_and_hardware_metadata_does_not_affect_score(self):
        body = self.payload(speed_factors=(0.5, 0.5, 1, 2))
        body["run"].update(cpuModel="CPU model", gpuModels=["GPU A", "GPU B"], totalMemoryMiB=65536,
                           overallScore=9000, appProcessId=1234)
        for metric in body["run"]["metrics"]:
            metric.update(medianMilliseconds=0.01, p95Milliseconds=0.01, score=9000)
        submitted = self.submit(body)
        self.assertEqual(submitted.status_code, 201)
        expected = 100 * 2**0.25
        self.assertAlmostEqual(submitted.json["overallScore"], expected)
        self.assertEqual(submitted.json["scoringVersion"], "cv-host-score-v1")

        self.use_session("admin")
        leaderboard = self.client.get("/api/admin/benchmarks/leaderboard").json
        self.assertEqual(leaderboard["referenceName"], "XIN")
        entry = leaderboard["devices"][0]
        self.assertAlmostEqual(entry["score"], expected)
        run = entry["best"]
        self.assertEqual(entry["latest"], run)
        self.assertEqual(run["cpuModel"], "CPU model")
        self.assertEqual(run["gpuModels"], ["GPU A", "GPU B"])
        self.assertEqual(run["totalMemoryMiB"], 65536)
        self.assertNotIn("appProcessId", run)
        self.assertEqual(run["measuredAtUtc"], "2026-11-21T06:00:01.123456+00:00")
        first = run["metrics"][0]
        self.assertAlmostEqual(first["medianMilliseconds"], REFERENCE_MEDIANS["memory-copy"] / 2)
        self.assertAlmostEqual(first["p95Milliseconds"], REFERENCE_MEDIANS["memory-copy"] * 0.545)
        self.assertAlmostEqual(first["timeRatio"], 0.5)
        self.assertAlmostEqual(first["score"], 200)

    def test_dotnet_fractional_timestamps_and_offsets_are_normalized(self):
        class Python310DateTime(datetime):
            @classmethod
            def fromisoformat(cls, value):
                fraction = re.search(r"\.(\d+)", value)
                if value.endswith("Z") or (fraction and len(fraction[1]) not in (3, 6)):
                    raise ValueError("Invalid ISO format for Python 3.10")
                return super().fromisoformat(value)

        body = self.payload()
        body["run"].update(
            measuredAtUtc="2026-11-21T06:00:01.1234567+00:00",
            appStartedAtUtc="2026-11-21T11:25:00.4+05:30",
            approximateSystemStartedAtUtc="2026-11-20T03:30:00.1-03:30",
        )
        # Exercise the strict parser even when the test runner has newer Python.
        with mock.patch("services.benchmark_service.datetime", Python310DateTime):
            submitted = self.submit(body)
        self.assertEqual(submitted.status_code, 201, submitted.json)

        self.use_session("admin")
        run = self.client.get("/api/admin/benchmarks/devices/" + self.device_id).json["runs"][0]
        self.assertEqual(run["measuredAtUtc"], "2026-11-21T06:00:01.123456+00:00")
        self.assertEqual(run["appStartedAtUtc"], "2026-11-21T05:55:00.400000+00:00")
        self.assertEqual(run["approximateSystemStartedAtUtc"], "2026-11-20T07:00:00.100000+00:00")

    def test_retry_is_idempotent_and_rejects_cross_key_or_modified_payload(self):
        body = self.payload()
        created = self.submit(body)
        self.assertEqual(created.status_code, 201)
        retry = copy.deepcopy(body)
        retry["run"]["metrics"].reverse()
        replay = self.submit(retry)
        self.assertEqual(replay.status_code, 200)
        self.assertEqual(replay.json, created.json)

        retry["run"]["metrics"][0]["samplesMilliseconds"][0] /= 2
        conflict = self.submit(retry)
        self.assertEqual(conflict.status_code, 409)
        self.assertEqual(conflict.json["code"], "run_conflict")
        retry["deviceId"] = str(uuid4())
        self.assertEqual(self.submit(retry).json["code"], "run_conflict")
        other = create_api_key(self.cache, name="other station", scopes="benchmark:submit")
        self.assertEqual(self.submit(self.payload(), other).json["code"], "device_identity_conflict")
        self.use_session("admin")
        self.assertEqual(self.client.get("/api/admin/benchmarks/devices/" + self.device_id).json["runCount"], 1)

    def test_best_and_latest_are_distinct_and_history_is_in_measurement_order(self):
        fast = self.payload(second=1, speed_factors=(0.5, 0.5, 0.5, 0.5))
        slow = self.payload(second=3, speed_factors=(2, 2, 2, 2))
        middle = self.payload(second=2)
        desk = self.payload(device_id=str(uuid4()), alias="另一工位", speed_factors=(0.75, 0.75, 0.75, 0.75))
        for body in (fast, slow, middle, desk):
            self.assertEqual(self.submit(body).status_code, 201)
        self.use_session("admin")
        devices = self.client.get("/api/admin/benchmarks/leaderboard").json["devices"]
        self.assertEqual([entry["deviceId"] for entry in devices], [self.device_id, desk["deviceId"]])
        self.assertEqual(devices[0]["best"]["id"], fast["run"]["id"])
        self.assertEqual(devices[0]["latest"]["id"], slow["run"]["id"])
        self.assertAlmostEqual(devices[0]["score"], 200)
        history = self.client.get("/api/admin/benchmarks/devices/" + self.device_id).json
        self.assertEqual(history["runCount"], 3)
        self.assertEqual([run["id"] for run in history["runs"]], [body["run"]["id"] for body in (slow, middle, fast)])
        self.assertEqual(self.client.get("/api/admin/benchmarks/devices/" + str(uuid4())).status_code, 404)
        self.assertNotIn("totalMemoryMiB", history["runs"][0])

    def test_invalid_inputs_are_rejected_before_creating_a_device(self):
        for invalid in (float("nan"), float("inf"), 0, -1, True, 5e-324):
            with self.subTest(sample=invalid):
                body = self.payload()
                body["run"]["metrics"][0]["samplesMilliseconds"] = [invalid] * 21
                self.assertEqual(self.submit(body).status_code, 400)
        for field, invalid in (
            ("workloadVersion", "other"), ("frameWidth", 30), ("measuredAtUtc", "2026-11-20T06:00:00"),
            ("gpuModels", "gpu"), ("logicalProcessors", False),
        ):
            with self.subTest(field=field):
                body = self.payload()
                body["run"][field] = invalid
                self.assertEqual(self.submit(body).status_code, 400)
        body = self.payload()
        body["run"]["metrics"][0]["samplesMilliseconds"].pop()
        self.assertEqual(self.submit(body).status_code, 400)
        body["deviceName"] = "X" * 16_384
        self.assertEqual(self.submit(body).status_code, 413)
        self.use_session("admin")
        self.assertEqual(self.client.get("/api/admin/benchmarks/leaderboard").json["devices"], [])

    def test_no_login_credential_binds_one_device_and_is_only_stored_hashed(self):
        token = self.desktop_token(1)
        body = self.payload()
        response = self.submit_desktop(body, token)
        self.assertEqual(response.status_code, 201, response.json)
        replay = self.submit_desktop(body, token)
        self.assertEqual(replay.status_code, 200)
        self.assertEqual(replay.json, response.json)
        self.assertEqual(self.submit_desktop(body, self.desktop_token(2)).json["code"], "device_identity_conflict")
        self.assertEqual(self.submit_desktop(self.payload(device_id=str(uuid4())), token).json["code"], "device_identity_conflict")
        self.assertEqual(self.submit(body).json["code"], "device_identity_conflict")
        # The final two padding bits are zero in the canonical Base64 spelling.
        noncanonical = self.desktop_token(0)[:-1] + "B"
        self.assertEqual(self.submit_desktop(self.payload(), noncanonical).status_code, 401)
        with closing(self.cache.get_db()) as db:
            owner = db.execute("SELECT owner_key_id, owner_token_hash FROM benchmark_devices").fetchone()
            self.assertEqual(owner["owner_key_id"], 0)
            self.assertRegex(owner["owner_token_hash"], r"^[0-9a-f]{64}$")
            self.assertNotIn(token, " ".join(row[0] for row in db.execute("SELECT bucket FROM benchmark_submit_rate_limits")))
            self.assertEqual(db.execute("SELECT COUNT(*) FROM benchmark_runs").fetchone()[0], 1)

    def test_comparison_only_includes_opted_in_device_representatives(self):
        legacy = self.payload(device_id=str(uuid4()), alias="legacy private alias")
        original = self.submit(legacy)
        self.assertEqual(original.status_code, 201)
        self.assertFalse(original.json["publishedComparison"])
        anonymous_private = self.payload(second=3, speed_factors=(0.25, 0.25, 0.25, 0.25))
        self.assertEqual(self.submit_desktop(anonymous_private).status_code, 201)
        path = "/api/benchmarks/comparison"
        self.assertEqual(self.client.get(path).json["references"], [])

        # Exact re-share by the same owner can explicitly publish a legacy run.
        published = {**legacy, "publishComparison": True}
        self.assertEqual(self.submit_desktop(published).json["code"], "device_identity_conflict")
        self.assertEqual(self.client.get(path).json["references"], [])
        re_shared = self.submit(published)
        self.assertEqual(re_shared.status_code, 200)
        self.assertTrue(re_shared.json["publishedComparison"])
        self.assertEqual(self.submit(published).json, re_shared.json)
        self.assertEqual(self.submit(legacy).json, re_shared.json)
        self.assertEqual(len(self.client.get(path).json["references"]), 1)
        for second, factor in ((1, 0.5), (2, 1)):
            body = self.payload(alias="never disclosed alias", second=second, speed_factors=(factor,) * 4)
            body["publishComparison"] = True
            body["run"].update(cpuModel="Intel Core fixture", gpuModels=["NVIDIA fixture"], totalMemoryMiB=65_536)
            self.assertEqual(self.submit_desktop(body).status_code, 201)

        # Legacy scoped uploaders can still explicitly opt in on a fresh run.
        scoped = self.payload(device_id=legacy["deviceId"], alias="legacy private alias", second=2)
        scoped["publishComparison"] = True
        self.assertEqual(self.submit(scoped).status_code, 201)

        response = self.client.get(path)
        self.assertEqual(response.status_code, 200)
        board = response.json
        self.assertEqual(set(board), {
            "workloadVersion", "scoringVersion", "referenceName", "referenceScore",
            "references", "exampleReferences", "selectedRun",
        })
        self.assertEqual(board["referenceScore"], 100)
        self.assertIsNone(board["selectedRun"])
        refs = board["references"]
        self.assertEqual(len(refs), 2)
        self.assertEqual(set(refs[0]), {
            "label", "cpuModel", "gpuModels", "totalMemoryMiB", "overallScore", "latestScore", "previousScore",
        })
        self.assertAlmostEqual(refs[0]["overallScore"], 200)
        self.assertAlmostEqual(refs[0]["latestScore"], 100)
        self.assertAlmostEqual(refs[0]["previousScore"], 200)
        self.assertEqual(refs[0]["cpuModel"], "Intel Core fixture")
        self.assertEqual(refs[0]["gpuModels"], ["NVIDIA fixture"])
        self.assertEqual(refs[0]["totalMemoryMiB"], 65_536)
        self.assertNotIn(self.device_id, response.get_data(as_text=True))
        self.assertNotIn("private alias", response.get_data(as_text=True))
        self.assertNotIn("never disclosed alias", response.get_data(as_text=True))
        self.assertEqual(self.client.get(path, query_string={"excludeDeviceId": self.device_id}).json["references"], refs[1:])
        self.assertEqual(self.client.get(path, query_string={"excludeDeviceId": "bad"}).status_code, 400)
        self.assertEqual(self.client.get("/api/admin/benchmarks/leaderboard").status_code, 401)

    def test_example_profiles_do_not_depend_on_or_write_benchmark_records(self):
        response = self.client.get("/api/benchmarks/comparison")
        self.assertEqual(response.headers["Cache-Control"], "no-store")
        self.assertEqual(response.json["references"], [])
        self.assertIsNone(response.json["selectedRun"])
        examples = response.json["exampleReferences"]
        self.assertEqual(len(examples), 3)
        for example in examples:
            self.assertIn("合成示例", example["label"])
            self.assertEqual(set(example), {"label", "cpuModel", "gpuModels", "totalMemoryMiB", "overallScore"})
        with closing(self.cache.get_db()) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM benchmark_devices").fetchone()[0], 0)
            self.assertEqual(db.execute("SELECT COUNT(*) FROM benchmark_runs").fetchone()[0], 0)

    def test_pinned_run_is_masked_and_restricted_to_explicit_current_public_runs(self):
        best = self.payload(second=1, speed_factors=(0.5,) * 4)
        best["publishComparison"] = True
        latest = self.payload(alias="private station alias", second=2)
        latest["publishComparison"] = True
        latest["run"].update(cpuModel="CPU from pinned run", totalMemoryMiB=32_768)
        private = self.payload(second=3, speed_factors=(0.25,) * 4)
        outdated = self.payload(second=4, speed_factors=(0.25,) * 4)
        outdated["publishComparison"] = True
        for body in (best, latest, private, outdated):
            self.assertEqual(self.submit_desktop(body).status_code, 201)
        with closing(self.cache.get_db()) as db, db:
            db.execute("UPDATE benchmark_runs SET scoring_version = ? WHERE run_id = ?", ("legacy", outdated["run"]["id"]))

        path = "/api/benchmarks/comparison"
        ordinary = self.client.get(path).json["references"][0]
        for key, score in (("overallScore", 200), ("latestScore", 100), ("previousScore", 200)):
            self.assertAlmostEqual(ordinary[key], score)
        response = self.client.get(path, query_string={
            "excludeDeviceId": self.device_id, "includeRunId": latest["run"]["id"],
        })
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json["references"], [])
        run = response.json["selectedRun"]
        self.assertEqual(set(run), {"label", "cpuModel", "gpuModels", "totalMemoryMiB", "overallScore"})
        self.assertAlmostEqual(run["overallScore"], 100)
        self.assertEqual(run["cpuModel"], "CPU from pinned run")
        self.assertNotIn(latest["run"]["id"], response.get_data(as_text=True))
        self.assertNotIn("private station alias", response.get_data(as_text=True))

        for run_id in (private["run"]["id"], outdated["run"]["id"], str(uuid4())):
            self.assertIsNone(self.client.get(path, query_string={"includeRunId": run_id}).json["selectedRun"])
        invalid = self.client.get(path, query_string={"includeRunId": "bad"})
        self.assertEqual(invalid.status_code, 400)
        self.assertEqual(invalid.json["code"], "invalid_run_id")

    def test_comparison_is_capped_and_non_public_legacy_behavior_unchanged(self):
        for _ in range(14):
            body = self.payload(device_id=str(uuid4()))
            body["publishComparison"] = True
            self.assertEqual(self.submit(body).status_code, 201)
        references = self.client.get("/api/benchmarks/comparison").json["references"]
        self.assertEqual(len(references), 12)
        for reference in references:
            self.assertEqual(reference["latestScore"], reference["overallScore"])
            self.assertIsNone(reference["previousScore"])

        implausible = self.payload(device_id=str(uuid4()), speed_factors=(0.001,) * 4)
        self.assertEqual(self.submit_desktop(implausible).status_code, 400)
        self.assertEqual(self.submit(implausible).status_code, 201)
        invalid_flag = self.payload(device_id=str(uuid4()))
        invalid_flag["publishComparison"] = "true"
        self.assertEqual(self.submit_desktop(invalid_flag).status_code, 400)

    def test_account_free_rate_limits_count_invalid_attempts_and_reset(self):
        with mock.patch.multiple("db.repositories.benchmarks", SUBMIT_TOKEN_LIMIT=2, SUBMIT_SOURCE_LIMIT=3, SUBMIT_GLOBAL_LIMIT=4):
            with mock.patch("db.repositories.benchmarks.time", return_value=601):
                self.assertEqual(self.submit_desktop({"invalid": True}).status_code, 400)
                self.assertEqual(self.submit_desktop(self.payload()).status_code, 201)
                limited = self.submit_desktop(self.payload())
                self.assertEqual(limited.status_code, 429)
                self.assertEqual(limited.headers["Retry-After"], "599")
                self.assertEqual(self.submit_desktop(self.payload(device_id=str(uuid4())), self.desktop_token(2)).status_code, 201)
                self.assertEqual(self.submit_desktop(self.payload(device_id=str(uuid4())), self.desktop_token(3)).status_code, 429)
                self.assertEqual(self.submit_desktop(self.payload(device_id=str(uuid4())), self.desktop_token(3), "198.51.100.11").status_code, 201)
                self.assertEqual(self.submit_desktop(self.payload(device_id=str(uuid4())), self.desktop_token(4), "198.51.100.12").status_code, 429)
                self.assertEqual(self.submit(self.payload(device_id=str(uuid4()))).status_code, 201)
            with mock.patch("db.repositories.benchmarks.time", return_value=1200):
                self.assertEqual(self.submit_desktop(self.payload()).status_code, 201)


if __name__ == "__main__":
    unittest.main()

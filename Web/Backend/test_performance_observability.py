import unittest
from datetime import datetime, timedelta, timezone

from services.performance_observability import (
    build_performance_summary,
    record_slow_request,
    RequestDiagnostics,
    request_category,
)


class PerformanceObservabilityTests(unittest.TestCase):
    def test_recent_counters_expire_errors_and_use_all_requests_for_percentiles(self):
        clock = [1000.0]
        diagnostics = RequestDiagnostics(clock=lambda: clock[0])
        diagnostics.record(category="application", status=500, duration_ms=1400)
        clock[0] += 1
        for _ in range(19):
            diagnostics.record(category="application", status=200, duration_ms=5)
        group = diagnostics.summary()["groups"]["application"]
        self.assertEqual((20, 1, 10), (group["requests"], group["server_errors"], group["p95_upper_ms"]))
        clock[0] = 1300
        group = diagnostics.summary()["groups"]["application"]
        self.assertEqual((19, 0), (group["requests"], group["server_errors"]))
        clock[0] += 1
        self.assertEqual(diagnostics.summary()["groups"]["application"]["requests"], 0)

    def test_transfers_do_not_evict_application_samples_and_overflow_is_explicit(self):
        diagnostics = RequestDiagnostics()
        sample = {"path": "/api/admin/feedback", "duration_ms": 700}
        diagnostics.record(category="application", status=200, duration_ms=700, slow_sample=sample)
        for _ in range(120):
            diagnostics.record(category="transfer", status=200, duration_ms=20000, slow_sample={"path": "/upload/file"})
        groups = diagnostics.summary()["groups"]
        self.assertEqual(len(groups["application"]["slow_requests"]), 1)
        self.assertEqual(len(groups["transfer"]["slow_requests"]), 20)
        self.assertTrue(groups["transfer"]["p95_overflow"])
        self.assertEqual(request_category("/api/admin/stats/overview"), "application")
        self.assertEqual(request_category("/admin"), "application")
        self.assertEqual(request_category("/api/transfer/files"), "application")
        self.assertEqual(request_category("/api/transfer/uploads/id"), "application")
        self.assertEqual(request_category("/api/transfer/uploads/id", method="PATCH"), "transfer")
        self.assertEqual(request_category("/api/transfer/files/file.zip", method="DELETE"), "application")
        self.assertEqual(request_category("/api/transfer/files/file.zip"), "transfer")
        self.assertEqual(request_category("/api/app/releases/1.0/download"), "transfer")
        self.assertEqual(request_category("/api/feedback/id/attachments/log.zip"), "transfer")

    def test_record_slow_request_adds_utc_timestamp_and_bounds_buffer(self):
        buffer = []
        base = datetime(2026, 8, 12, 10, 0, tzinfo=timezone.utc)

        for index in range(3):
            record_slow_request(
                buffer,
                method="get",
                path=f"/api/items/{index}",
                status=200,
                duration_ms=500 + index,
                recorded_at=base + timedelta(seconds=index),
                capacity=2,
            )

        self.assertEqual(len(buffer), 2)
        self.assertEqual([item["path"] for item in buffer], ["/api/items/1", "/api/items/2"])
        self.assertEqual(buffer[0]["method"], "GET")
        self.assertEqual(buffer[0]["recorded_at"], "2026-08-12T10:00:01+00:00")

    def test_record_slow_request_rejects_invalid_capacity(self):
        with self.assertRaisesRegex(ValueError, "capacity must be at least 1"):
            record_slow_request(
                [],
                method="GET",
                path="/",
                status=200,
                duration_ms=500,
                capacity=0,
            )

    def test_build_performance_summary_filters_recent_jobs_and_reports_scope(self):
        generated_at = datetime(2026, 8, 12, 11, 30, tzinfo=timezone.utc)
        requests = [
            {"recorded_at": f"2026-08-12T11:00:0{index}+00:00", "path": f"/{index}"}
            for index in range(3)
        ]
        runs = [
            {"id": 4, "status": "success", "duration_ms": 1500},
            {"id": 3, "status": "error", "duration_ms": 20},
            {"id": 2, "status": "success", "duration_ms": 999},
        ]

        payload = build_performance_summary(
            slow_requests=requests,
            recent_job_runs=runs,
            threshold_ms=750,
            buffer_capacity=100,
            process_started_at=datetime(2026, 8, 12, 9, 0, tzinfo=timezone.utc),
            generated_at=generated_at,
            sample_limit=2,
        )

        self.assertEqual(payload["generated_at"], "2026-08-12T11:30:00+00:00")
        self.assertEqual(payload["process_started_at"], "2026-08-12T09:00:00+00:00")
        self.assertEqual(payload["threshold_ms"], 750)
        self.assertEqual(payload["request_buffer_count"], 3)
        self.assertEqual(payload["request_buffer_capacity"], 100)
        self.assertEqual([item["path"] for item in payload["slow_requests"]], ["/1", "/2"])
        self.assertEqual([item["id"] for item in payload["slow_jobs"]], [4, 3])

    def test_build_performance_summary_rejects_invalid_limit(self):
        with self.assertRaisesRegex(ValueError, "sample_limit must be at least 1"):
            build_performance_summary(
                slow_requests=[],
                recent_job_runs=[],
                sample_limit=0,
            )


if __name__ == "__main__":
    unittest.main()

import unittest
from datetime import datetime, timedelta, timezone

from services.performance_observability import (
    build_performance_summary,
    record_slow_request,
)


class PerformanceObservabilityTests(unittest.TestCase):
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

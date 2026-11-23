"""SQLite storage for submitted host benchmark runs and the leaderboard."""

from __future__ import annotations

import json
import sqlite3
from collections.abc import Callable
from contextlib import closing
from datetime import datetime, timezone
from time import time

from services.benchmark_service import (
    BenchmarkSubmission, PUBLIC_COMPARISON_LIMIT, REFERENCE_NAME, SCORING_VERSION, WORKLOAD_VERSION,
    example_benchmark_profiles, public_benchmark_profile,
)


SUBMIT_WINDOW_SECONDS = 600
SUBMIT_TOKEN_LIMIT = 8
SUBMIT_SOURCE_LIMIT = 40
SUBMIT_GLOBAL_LIMIT = 240


class BenchmarkConflictError(Exception):
    pass


class SqliteBenchmarkRepository:
    def __init__(self, connection_factory: Callable[[], sqlite3.Connection]):
        self._connection_factory = connection_factory

    def submit(self, key_id: int | None, submission: BenchmarkSubmission, *, token_hash: str | None = None) -> tuple[dict, bool, bool]:
        if (key_id is None) == (token_hash is None):
            raise ValueError("Exactly one benchmark credential is required")
        owner_key_id = key_id if key_id is not None else 0
        received_at = datetime.now(timezone.utc).isoformat(timespec="microseconds")
        run = {**submission.run, "receivedAtUtc": received_at}
        with closing(self._connection_factory()) as db, db:
            db.execute("BEGIN IMMEDIATE")
            device = db.execute(
                "SELECT owner_key_id, owner_token_hash FROM benchmark_devices WHERE device_id = ?",
                (submission.device_id,),
            ).fetchone()
            if device is not None and (device["owner_key_id"] != owner_key_id or device["owner_token_hash"] != token_hash):
                raise BenchmarkConflictError("device_identity_conflict")
            if device is None and token_hash is not None:
                other_device = db.execute(
                    "SELECT 1 FROM benchmark_devices WHERE owner_token_hash = ?",
                    (token_hash,),
                ).fetchone()
                if other_device is not None:
                    raise BenchmarkConflictError("device_identity_conflict")

            existing = db.execute(
                "SELECT device_id, content_hash, publish_comparison, run_json FROM benchmark_runs WHERE run_id = ?",
                (run["id"],),
            ).fetchone()
            if existing is not None:
                if existing["device_id"] != submission.device_id or existing["content_hash"] != submission.content_hash:
                    raise BenchmarkConflictError("run_conflict")
                was_public = bool(existing["publish_comparison"])
                if submission.publish_comparison and not was_public:
                    db.execute("UPDATE benchmark_runs SET publish_comparison = 1 WHERE run_id = ?", (run["id"],))
                return json.loads(existing["run_json"]), False, was_public or submission.publish_comparison

            db.execute(
                """INSERT INTO benchmark_devices (device_id, device_name, owner_key_id, owner_token_hash) VALUES (?, ?, ?, ?)
                   ON CONFLICT(device_id) DO UPDATE SET device_name = excluded.device_name""",
                (submission.device_id, submission.device_name, owner_key_id, token_hash),
            )
            db.execute(
                """INSERT INTO benchmark_runs (
                     run_id, device_id, workload_version, scoring_version, measured_at_utc, received_at_utc,
                     overall_score, content_hash, run_json, publish_comparison
                   ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                (run["id"], submission.device_id, run["workloadVersion"], run["scoringVersion"],
                 run["measuredAtUtc"], received_at, run["overallScore"], submission.content_hash,
                 json.dumps(run, ensure_ascii=False, separators=(",", ":"), allow_nan=False),
                 int(submission.publish_comparison)),
            )
            return run, True, submission.publish_comparison

    def charge_anonymous_attempt(self, token_hash: str, source_hash: str) -> int:
        """Return the retry delay in seconds, or zero after consuming an attempt."""
        now = int(time())
        window = now - now % SUBMIT_WINDOW_SECONDS
        limits = {
            "global": SUBMIT_GLOBAL_LIMIT,
            "source:" + source_hash: SUBMIT_SOURCE_LIMIT,
            "token:" + token_hash: SUBMIT_TOKEN_LIMIT,
        }
        with closing(self._connection_factory()) as db, db:
            db.execute("BEGIN IMMEDIATE")
            active = db.execute(
                """SELECT bucket, attempt_count FROM benchmark_submit_rate_limits
                   WHERE window_start = ? AND bucket IN (?, ?, ?)""",
                (window, *limits),
            ).fetchall()
            if any(row["attempt_count"] >= limits[row["bucket"]] for row in active):
                return window + SUBMIT_WINDOW_SECONDS - now
            db.execute("DELETE FROM benchmark_submit_rate_limits WHERE window_start < ?", (window,))
            db.executemany(
                """INSERT INTO benchmark_submit_rate_limits (bucket, window_start, attempt_count) VALUES (?, ?, 1)
                   ON CONFLICT(bucket) DO UPDATE SET
                       attempt_count = CASE WHEN window_start = excluded.window_start THEN attempt_count + 1 ELSE 1 END,
                       window_start = excluded.window_start""",
                ((bucket, window) for bucket in limits),
            )
        return 0

    def comparison(self, exclude_device_id: str | None, include_run_id: str | None = None) -> dict:
        references = []
        selected_run = None
        with closing(self._connection_factory()) as db, db:
            db.execute("BEGIN")
            rows = db.execute(
                """WITH ranked AS (
                       SELECT r.overall_score, r.run_id,
                              ROW_NUMBER() OVER (
                                  PARTITION BY r.device_id
                                  ORDER BY r.overall_score DESC, r.measured_at_utc DESC, r.received_at_utc DESC, r.run_id DESC
                              ) AS device_rank
                         FROM benchmark_runs r
                        WHERE r.workload_version = ? AND r.scoring_version = ? AND r.publish_comparison = 1
                          AND (? IS NULL OR r.device_id != ?)
                   ), selected AS (
                       SELECT run_id, overall_score FROM ranked WHERE device_rank = 1
                       ORDER BY overall_score DESC, run_id LIMIT ?
                   )
                   SELECT r.device_id, r.run_json, s.overall_score FROM selected s
                   JOIN benchmark_runs r ON r.run_id = s.run_id
                   ORDER BY s.overall_score DESC, s.run_id""",
                (WORKLOAD_VERSION, SCORING_VERSION, exclude_device_id, exclude_device_id, PUBLIC_COMPARISON_LIMIT),
            ).fetchall()
            for row in rows:
                recent = db.execute(
                    """SELECT overall_score FROM benchmark_runs
                       WHERE device_id = ? AND workload_version = ? AND scoring_version = ? AND publish_comparison = 1
                       ORDER BY measured_at_utc DESC, received_at_utc DESC, run_id DESC LIMIT 2""",
                    (row["device_id"], WORKLOAD_VERSION, SCORING_VERSION),
                ).fetchall()
                profile = public_benchmark_profile(json.loads(row["run_json"]), row["overall_score"])
                profile.update(
                    latestScore=recent[0]["overall_score"],
                    previousScore=recent[1]["overall_score"] if len(recent) > 1 else None,
                )
                references.append(profile)
            if include_run_id is not None:
                selected = db.execute(
                    """SELECT run_json, overall_score FROM benchmark_runs
                       WHERE run_id = ? AND workload_version = ? AND scoring_version = ? AND publish_comparison = 1""",
                    (include_run_id, WORKLOAD_VERSION, SCORING_VERSION),
                ).fetchone()
                if selected is not None:
                    selected_run = public_benchmark_profile(json.loads(selected["run_json"]), selected["overall_score"])
        return {
            "workloadVersion": WORKLOAD_VERSION,
            "scoringVersion": SCORING_VERSION,
            "referenceName": REFERENCE_NAME,
            "referenceScore": 100,
            "references": references,
            "exampleReferences": example_benchmark_profiles(),
            "selectedRun": selected_run,
        }

    def leaderboard(self) -> dict:
        with closing(self._connection_factory()) as db:
            rows = db.execute(
                """WITH ranked AS (
                       SELECT r.device_id, d.device_name, r.run_json,
                              COUNT(*) OVER (PARTITION BY r.device_id) AS run_count,
                              ROW_NUMBER() OVER (
                                  PARTITION BY r.device_id
                                  ORDER BY r.overall_score DESC, r.measured_at_utc DESC, r.received_at_utc DESC, r.run_id DESC
                              ) AS best_rank,
                              ROW_NUMBER() OVER (
                                  PARTITION BY r.device_id
                                  ORDER BY r.measured_at_utc DESC, r.received_at_utc DESC, r.run_id DESC
                              ) AS latest_rank
                         FROM benchmark_runs r
                         JOIN benchmark_devices d ON r.device_id = d.device_id
                        WHERE r.workload_version = ? AND r.scoring_version = ?
                   )
                   SELECT * FROM ranked WHERE best_rank = 1 OR latest_rank = 1""",
                (WORKLOAD_VERSION, SCORING_VERSION),
            ).fetchall()
        devices = {}
        for row in rows:
            entry = devices.setdefault(row["device_id"], {
                "deviceId": row["device_id"],
                "deviceName": row["device_name"],
                "runCount": row["run_count"],
            })
            run = json.loads(row["run_json"])
            if row["best_rank"] == 1:
                entry.update(best=run, score=run["overallScore"])
            if row["latest_rank"] == 1:
                entry["latest"] = run
        return {
            "workloadVersion": WORKLOAD_VERSION,
            "scoringVersion": SCORING_VERSION,
            "referenceName": REFERENCE_NAME,
            "devices": sorted(devices.values(), key=lambda entry: (-entry["score"], entry["deviceName"].casefold(), entry["deviceId"])),
        }

    def device_history(self, device_id: str) -> dict | None:
        with closing(self._connection_factory()) as db, db:
            db.execute("BEGIN")
            device = db.execute(
                "SELECT device_name FROM benchmark_devices WHERE device_id = ?",
                (device_id,),
            ).fetchone()
            if device is None:
                return None
            rows = db.execute(
                """SELECT run_json FROM benchmark_runs WHERE device_id = ?
                   ORDER BY measured_at_utc DESC, received_at_utc DESC, run_id DESC""",
                (device_id,),
            ).fetchall()
        return {
            "deviceId": device_id,
            "deviceName": device["device_name"],
            "runCount": len(rows),
            "runs": [json.loads(row["run_json"]) for row in rows],
        }

"""Persistence port for scheduled jobs and their run history."""

from __future__ import annotations

from typing import Any, Protocol


class JobRepository(Protocol):
    def ensure_defaults(self, jobs: list[dict[str, Any]], now: str) -> None: ...

    def list_with_latest_runs(self) -> list[dict[str, Any]]: ...

    def get(self, job_id: str) -> dict[str, Any] | None: ...

    def set_enabled(self, job_id: str, enabled: bool, updated_at: str) -> bool: ...

    def start_run(self, job_id: str, started_at: str) -> int: ...

    def complete_run(
        self,
        run_id: int,
        job_id: str,
        *,
        status: str,
        finished_at: str,
        duration_ms: int,
        summary: str,
        error: str,
        now_epoch_seconds: float,
    ) -> None: ...

    def list_enabled(self) -> list[dict[str, Any]]: ...

    def has_successful_run(self, job_id: str) -> bool: ...

    def recent_runs(self, limit: int) -> list[dict[str, Any]]: ...

    def prune_history_before(self, cutoff: str) -> int: ...

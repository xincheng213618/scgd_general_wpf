"""Persistence boundary for Operations support-session state."""

from __future__ import annotations

from typing import Literal, Protocol


SupportEventResult = Literal[
    "created",
    "deduplicated",
    "host_not_found",
    "support_session_not_requested",
    "support_session_not_active",
    "support_session_not_found",
]


class OperationsSupportStore(Protocol):
    def latest_state(self, host_id: str, session_id: str) -> str | None: ...

    def record_event(
        self,
        *,
        event_id: str,
        host_id: str,
        session_id: str,
        event_type: str,
        payload_json: str,
        created_at: str,
    ) -> SupportEventResult: ...

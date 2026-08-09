"""Port for recording and reading artifact-index refresh state."""

from __future__ import annotations

from typing import Any, Protocol


class IndexStateRepository(Protocol):
    """Persistence boundary for observable index refresh state."""

    def update(
        self,
        scope: str,
        *,
        status: str = "ready",
        signature: str = "",
        started_at: str = "",
        finished_at: str = "",
        item_count: int = 0,
        duration_ms: int = 0,
        error: str = "",
    ) -> None: ...

    def get(self, scope: str) -> dict[str, Any] | None: ...

    def get_many(self, scopes: tuple[str, ...]) -> dict[str, dict[str, Any]]: ...

    def get_all(self) -> dict[str, dict[str, Any]]: ...

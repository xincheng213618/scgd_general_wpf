"""Read-only contract for the Web Operations overview."""

from __future__ import annotations

from datetime import datetime
from typing import Any, Protocol


class OperationsAdminQuery(Protocol):
    def get_overview(
        self,
        *,
        now: datetime,
        host_limit: int,
        activity_limit: int,
    ) -> dict[str, Any]: ...

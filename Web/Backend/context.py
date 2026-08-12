"""
MarketplaceContext — lightweight dependency container.

All routes and services receive dependencies through this context
instead of importing module-level globals from app.py.
"""

from __future__ import annotations

import sqlite3
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

from db_cache import CacheManager
from services.auth_policy import AuthPolicy
from services.performance_observability import DEFAULT_SLOW_REQUEST_THRESHOLD_MS
from services.request_context import RequestContext


@dataclass
class MarketplaceContext:
    """Central dependency container for the marketplace application.

    Runtime accessors are supplied by the composition root. The core context
    therefore has no dependency on the executable ``app`` compatibility shell.
    """

    # Core config
    config: dict[str, Any]
    _storage: Path
    db_path: Path
    cache: CacheManager
    storage_getter: Callable[[], Path]
    config_getter: Callable[[], dict[str, Any]]
    db_path_getter: Callable[[], Path]

    # DB helpers
    get_db: Callable[[], sqlite3.Connection]
    init_db: Callable[[], None]

    # Path helpers
    is_safe_id: Callable[[str], bool]
    is_safe_version: Callable[[str], bool]
    sanitize_filename: Callable[[str], str]
    normalize_relative_path: Callable[[str], str]
    storage_target: Callable[[str], Path]

    # Cache helpers
    get_cache_entry: Callable[..., dict[str, Any] | None]
    set_cache_entry: Callable[..., None]
    invalidate_cache_prefix: Callable[[str], None]
    refresh_related_caches: Callable[..., None]

    # Upload auth
    get_upload_auth: Callable[[], tuple[str, str]]
    auth_policy: AuthPolicy
    request_context_factory: Callable[[], RequestContext]
    access_recorder: Any

    # Service layer (populated after construction)
    services: Any = None  # MarketplaceDataService
    artifact_delivery: Any = None  # ArtifactDeliveryService
    human_size: Callable[[int], str] = lambda s: f"{s} B"
    render_markdown: Callable[[str | None], Any] = lambda t: str(t or "")
    render_markdown_cached: Callable[..., Any] = lambda **kw: ""

    # Request-scoped helpers
    is_api_request: Callable[[], bool] = lambda: False
    json_error: Callable[..., Any] = lambda msg, code, **kw: {"error": msg, "status": code}

    # Slow request tracking
    slow_requests: list = field(default_factory=list)
    slow_request_threshold_ms: int = DEFAULT_SLOW_REQUEST_THRESHOLD_MS
    process_started_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))

    @property
    def storage(self) -> Path:
        return self.storage_getter()

    @storage.setter
    def storage(self, value: Path):
        self._storage = value

    @property
    def active_config(self) -> dict[str, Any]:
        return self.config_getter()

    @property
    def active_db_path(self) -> Path:
        return self.db_path_getter()

    @staticmethod
    def get_request_username(request_context: RequestContext) -> str:
        """Return the explicitly resolved audit actor identity."""
        return request_context.actor.actor_id or "system"

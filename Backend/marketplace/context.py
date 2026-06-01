"""
MarketplaceContext — lightweight dependency container.

All routes and services receive dependencies through this context
instead of importing module-level globals from app.py.
"""

from __future__ import annotations

import sqlite3
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable

from db_cache import CacheManager


@dataclass
class MarketplaceContext:
    """Central dependency container for the marketplace application.

    The storage property reads from the app module's STORAGE global
    so that test mutations are always reflected.
    """

    # Core config
    config: dict[str, Any]
    _storage: Path
    db_path: Path
    cache: CacheManager

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

    # Service layer (populated after construction)
    services: Any = None  # MarketplaceDataService
    human_size: Callable[[int], str] = lambda s: f"{s} B"
    render_markdown: Callable[[str | None], Any] = lambda t: str(t or "")
    render_markdown_cached: Callable[..., Any] = lambda **kw: ""

    # Request-scoped helpers
    is_api_request: Callable[[], bool] = lambda: False
    json_error: Callable[..., Any] = lambda msg, code, **kw: {"error": msg, "status": code}

    # Slow request tracking
    slow_requests: list = field(default_factory=list)
    slow_request_threshold_ms: int = 500

    @property
    def storage(self) -> Path:
        """Read storage from app module globals so test mutations are reflected."""
        try:
            import app as _app
            return _app.STORAGE
        except ImportError:
            return self._storage

    @storage.setter
    def storage(self, value: Path):
        self._storage = value

    def get_request_username(self) -> str:
        """Get the current request username from session or auth."""
        from flask import request, session
        if session.get("username"):
            return session["username"]
        auth = request.authorization
        if auth and auth.username:
            return auth.username
        return "system"

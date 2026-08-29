"""Framework-neutral request identity passed explicitly into services."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from threading import RLock
from typing import Any, Callable


@dataclass(frozen=True)
class Actor:
    actor_type: str = "anonymous"
    actor_id: str = ""
    auth_method: str = "none"
    role: str = ""
    scopes: frozenset[str] = frozenset()
    authenticated: bool = False
    is_admin: bool = False


class RequestValueCache:
    """Small request-owned memoization store without a Flask dependency."""

    def __init__(self):
        self._values: dict[str, Any] = {}
        self._lock = RLock()

    def get_or_load(self, key: str, loader: Callable[[], Any]) -> Any:
        with self._lock:
            if key not in self._values:
                self._values[key] = loader()
            return self._values[key]


@dataclass(frozen=True)
class RequestContext:
    method: str = ""
    path: str = ""
    remote_addr: str | None = None
    user_agent: str = ""
    client_version: str = ""
    session_authenticated: bool = False
    session_user_authenticated: bool = False
    session_must_change_password: bool = False
    session_username: str = ""
    session_role: str = ""
    basic_username: str = ""
    basic_password: str = field(default="", repr=False)
    bearer_token: str = field(default="", repr=False)
    actor: Actor = Actor()
    values: RequestValueCache = field(default_factory=RequestValueCache, compare=False, repr=False)

    @property
    def is_api_request(self) -> bool:
        return self.path.startswith("/api/")

    def with_actor(self, actor: Actor) -> "RequestContext":
        return replace(self, actor=actor)

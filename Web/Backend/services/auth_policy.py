"""Framework-neutral authentication and authorization policy."""

from __future__ import annotations

import hmac
from dataclasses import dataclass
from typing import Callable

from db_cache import CacheManager
from services.request_context import Actor, RequestContext


@dataclass(frozen=True)
class AuthorizationDecision:
    allowed: bool
    reason: str
    principal: Actor

    @property
    def forbidden(self) -> bool:
        return self.reason == "insufficient_scope"


class AuthPolicy:
    """Authenticate Session, Basic and Bearer credentials, then authorize scopes."""

    def __init__(
        self,
        cache: CacheManager,
        get_upload_auth: Callable[[], tuple[str, str]],
    ):
        self._cache = cache
        self._get_upload_auth = get_upload_auth
        from services.permission_service import ensure_permission_catalog

        ensure_permission_catalog(cache)

    def authorize(
        self,
        context: RequestContext,
        required_scopes: list[str] | None = None,
        *,
        allow_admin_session: bool = True,
        allow_user_session: bool = False,
        allow_basic: bool = True,
        allow_bearer: bool = True,
    ) -> AuthorizationDecision:
        password_change_principal = Actor(
            actor_type="user",
            actor_id=context.session_username or "system",
            auth_method="session",
            role=context.session_role or "user",
            authenticated=True,
            is_admin=(context.session_role == "admin"),
        )

        if (
            allow_admin_session
            and context.session_authenticated
            and not context.session_must_change_password
        ):
            return self._allowed(
                Actor(
                    actor_type="user",
                    actor_id=context.session_username or "system",
                    auth_method="session",
                    role=context.session_role or "admin",
                    authenticated=True,
                    is_admin=True,
                )
            )

        if (
            allow_user_session
            and context.session_user_authenticated
            and not context.session_must_change_password
        ):
            from services.permission_service import get_role_permission_codes

            role = context.session_role or "user"
            principal = Actor(
                actor_type="user",
                actor_id=context.session_username or "system",
                auth_method="session",
                role=role,
                scopes=get_role_permission_codes(self._cache, role),
                authenticated=True,
                is_admin=role == "admin",
            )
            required = set(required_scopes or ())
            if required and "admin:*" not in principal.scopes and not required <= principal.scopes:
                return AuthorizationDecision(False, "insufficient_scope", principal)
            return self._allowed(principal)

        if allow_basic and context.basic_username:
            expected_username, expected_password = self._get_upload_auth()
            if (
                expected_username
                and expected_password
                and hmac.compare_digest(context.basic_username, expected_username)
                and hmac.compare_digest(context.basic_password, expected_password)
            ):
                return self._allowed(
                    Actor(
                        actor_type="user",
                        actor_id=context.basic_username,
                        auth_method="basic",
                        role="admin",
                        authenticated=True,
                        is_admin=True,
                    )
                )

        if allow_bearer and context.bearer_token:
            principal = self._authenticate_api_key(context.bearer_token)
            if principal is not None:
                required = set(required_scopes or ())
                if required and "admin:*" not in principal.scopes and not required <= principal.scopes:
                    return AuthorizationDecision(False, "insufficient_scope", principal)
                return self._allowed(principal)

        if (
            context.session_must_change_password
            and (context.session_authenticated or context.session_user_authenticated)
        ):
            return AuthorizationDecision(
                False,
                "password_change_required",
                password_change_principal,
            )

        return AuthorizationDecision(False, "unauthenticated", Actor())

    def _authenticate_api_key(self, token: str) -> Actor | None:
        try:
            from services.api_key_service import api_key_actor_id, verify_api_key

            key_info = verify_api_key(self._cache, token, required_scopes=None)
        except Exception:
            return None
        if not key_info:
            return None
        scopes = frozenset(
            scope.strip()
            for scope in str(key_info.get("scopes") or "").split(",")
            if scope.strip()
        )
        return Actor(
            actor_type="api_key",
            actor_id=api_key_actor_id(key_info),
            auth_method="bearer",
            role="api_key",
            scopes=scopes,
            authenticated=True,
            is_admin="admin:*" in scopes,
        )

    @staticmethod
    def _allowed(principal: Actor) -> AuthorizationDecision:
        return AuthorizationDecision(True, "allowed", principal)

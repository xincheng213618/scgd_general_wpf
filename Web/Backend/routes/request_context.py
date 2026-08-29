"""Flask adapter that builds the framework-neutral request context."""

from __future__ import annotations

from flask import request, session

from services.request_context import RequestContext

_CONTEXT_KEY = "colorvision.request_context"


def current_request_context() -> RequestContext:
    cached = request.environ.get(_CONTEXT_KEY)
    if isinstance(cached, RequestContext):
        return cached

    authorization = request.authorization
    basic_username = ""
    basic_password = ""
    if authorization and (authorization.type or "").lower() == "basic":
        basic_username = authorization.username or ""
        basic_password = authorization.password or ""

    header = request.headers.get("Authorization", "")
    bearer_token = header[7:].strip() if header.startswith("Bearer ") else ""
    context = RequestContext(
        method=request.method,
        path=request.path,
        remote_addr=request.remote_addr,
        user_agent=request.headers.get("User-Agent", "")[:200],
        client_version=request.headers.get("X-Client-Version", ""),
        session_authenticated=bool(session.get("authenticated")),
        session_user_authenticated=bool(session.get("user_authenticated")),
        session_must_change_password=bool(session.get("must_change_password")),
        session_username=str(session.get("username") or ""),
        session_role=str(session.get("role") or ""),
        basic_username=basic_username,
        basic_password=basic_password,
        bearer_token=bearer_token,
    )
    request.environ[_CONTEXT_KEY] = context
    return context


def set_authenticated_request_context(context: RequestContext) -> None:
    request.environ[_CONTEXT_KEY] = context

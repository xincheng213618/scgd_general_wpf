"""Flask response/decorator adapters for the shared AuthPolicy."""

from __future__ import annotations

from functools import wraps
from typing import Any, Callable

from flask import current_app, redirect, request, session, url_for

from routes.browser_auth import apply_basic_auth_challenge
from services.auth_policy import AuthPolicy
from routes.request_context import current_request_context, set_authenticated_request_context


def check_web_session_auth() -> bool:
    return bool(session.get("authenticated"))


def require_web_auth(view_func):
    @wraps(view_func)
    def wrapper(*args, **kwargs):
        if not check_web_session_auth():
            try:
                login_url = url_for("public_pages.login_page", next=request.url)
            except Exception:
                login_url = url_for("login_page", next=request.url)
            return redirect(login_url)
        return view_func(*args, **kwargs)

    return wrapper


def make_require_upload_auth(
    auth_policy: AuthPolicy,
    json_error: Callable[..., Any],
):
    def _unauthorized_response():
        if request.path.startswith("/api/"):
            response = json_error("Authentication required", 401)
        else:
            response = current_app.response_class("Authentication required", status=401)
        return apply_basic_auth_challenge(response, "ColorVision Marketplace")

    def require_upload_auth(view_func):
        @wraps(view_func)
        def wrapper(*args, **kwargs):
            request_context = current_request_context()
            decision = auth_policy.authorize(
                request_context,
                ["plugin:publish"],
                allow_admin_session=False,
            )
            if not decision.allowed:
                return _unauthorized_response()
            set_authenticated_request_context(request_context.with_actor(decision.principal))
            return view_func(*args, **kwargs)

        return wrapper

    return require_upload_auth

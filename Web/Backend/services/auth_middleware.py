"""
Authentication middleware for ColorVision Marketplace.

Provides decorators and helpers for:
  - Bearer API Key authentication
  - Basic Auth authentication
  - Web session authentication
  - Admin endpoint scope enforcement
"""

from __future__ import annotations

import hmac
from functools import wraps
from typing import Any, Callable

from flask import redirect, request, session, url_for


def check_web_session_auth() -> bool:
    """Return True if the current Flask session is authenticated."""
    return bool(session.get("authenticated"))


def require_web_auth(view_func):
    """Decorator that requires web session auth, redirecting to login page on failure."""
    @wraps(view_func)
    def wrapper(*args, **kwargs):
        if not check_web_session_auth():
            # Try blueprint-qualified name first, fall back to plain name
            try:
                login_url = url_for("public_pages.login_page", next=request.url)
            except Exception:
                login_url = url_for("login_page", next=request.url)
            return redirect(login_url)
        return view_func(*args, **kwargs)
    return wrapper


def make_require_upload_auth(
    cache: Any,
    get_upload_auth: Callable[[], tuple[str, str]],
    json_error: Callable[..., Any],
):
    """Create a require_upload_auth decorator with injected dependencies."""

    def _unauthorized_response():
        if request.path.startswith("/api/"):
            response = json_error("Authentication required", 401)
        else:
            from flask import current_app
            response = current_app.response_class("Authentication required", status=401)
        response.headers["WWW-Authenticate"] = 'Basic realm="ColorVision Marketplace"'
        return response

    def require_upload_auth(view_func):
        @wraps(view_func)
        def wrapper(*args, **kwargs):
            # Check Bearer API Key first
            auth_header = request.headers.get("Authorization", "")
            if auth_header.startswith("Bearer "):
                token = auth_header[7:].strip()
                if token:
                    try:
                        from services.api_key_service import verify_api_key
                        key_info = verify_api_key(cache, token, required_scopes=["plugin:publish"])
                        if key_info:
                            return view_func(*args, **kwargs)
                    except Exception:
                        pass

            # Fall back to Basic Auth
            expected_username, expected_password = get_upload_auth()
            auth = request.authorization
            if (
                not auth
                or (auth.type or "").lower() != "basic"
                or not hmac.compare_digest(auth.username or "", expected_username)
                or not hmac.compare_digest(auth.password or "", expected_password)
            ):
                return _unauthorized_response()
            return view_func(*args, **kwargs)

        return wrapper

    return require_upload_auth

"""CSRF protection for browser-originated state-changing requests."""

from __future__ import annotations

import hmac
import secrets
from urllib.parse import urlsplit

from flask import jsonify, request, session


_CSRF_SESSION_KEY = "_csrf_token"
_SAFE_METHODS = frozenset({"GET", "HEAD", "OPTIONS"})
_SAFE_FETCH_SITES = frozenset({"same-origin", "none"})


def issue_csrf_token() -> str:
    """Return the current session token, creating a cryptographically random one."""
    token = str(session.get(_CSRF_SESSION_KEY) or "")
    if not token:
        token = secrets.token_urlsafe(32)
        session[_CSRF_SESSION_KEY] = token
    return token


def _normalized_origin(value: str) -> tuple[str, str, int | None] | None:
    try:
        parsed = urlsplit(value)
        if parsed.scheme not in {"http", "https"} or not parsed.hostname:
            return None
        return parsed.scheme.lower(), parsed.hostname.lower(), parsed.port
    except ValueError:
        return None


def _request_origin() -> tuple[str, str, int | None]:
    parsed = urlsplit(request.host_url)
    return parsed.scheme.lower(), (parsed.hostname or "").lower(), parsed.port


def _csrf_error(message: str):
    return jsonify({"error": message, "status": 403}), 403


def register_csrf_protection(app) -> None:
    """Protect real browser writes while preserving headerless native API clients."""

    @app.before_request
    def _protect_browser_write():
        if request.method in _SAFE_METHODS:
            return None

        origin_header = request.headers.get("Origin", "").strip()
        fetch_site = request.headers.get("Sec-Fetch-Site", "").strip().lower()
        is_browser_request = bool(origin_header or fetch_site)

        if origin_header:
            origin = _normalized_origin(origin_header)
            if origin is None or origin != _request_origin():
                return _csrf_error("Cross-origin state-changing request rejected")
        elif fetch_site and fetch_site not in _SAFE_FETCH_SITES:
            return _csrf_error("Cross-site state-changing request rejected")

        # Aggregate-only browser telemetry is intentionally accepted from the
        # same origin during pagehide, where Beacon cannot attach a CSRF token.
        # The endpoint has an exact, bounded payload contract and stores no raw
        # URL, referrer, address, or user-agent value.
        if request.path == "/api/v1/analytics/events":
            return None

        if not is_browser_request:
            return None

        has_explicit_authorization = bool(request.headers.get("Authorization", "").strip())
        has_session_auth = bool(session.get("authenticated") or session.get("user_authenticated"))
        if not has_session_auth or has_explicit_authorization:
            return None

        expected = str(session.get(_CSRF_SESSION_KEY) or "")
        supplied = request.headers.get("X-CSRF-Token", "")
        if not expected or not supplied or not hmac.compare_digest(expected, supplied):
            return _csrf_error("CSRF token missing or invalid")
        return None

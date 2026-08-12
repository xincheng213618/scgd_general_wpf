"""Browser-aware HTTP authentication responses for route adapters."""

from __future__ import annotations

from flask import request


_BROWSER_REQUEST_HEADERS = (
    "X-ColorVision-Web",
    "Origin",
    "Sec-Fetch-Site",
    "Sec-Fetch-Mode",
)


def is_browser_request() -> bool:
    """Return whether browser request metadata is present."""
    return any(request.headers.get(name, "").strip() for name in _BROWSER_REQUEST_HEADERS)


def is_browser_navigation() -> bool:
    if request.headers.get("Sec-Fetch-Mode", "").strip().lower() == "navigate":
        return True
    return "text/html" in request.headers.get("Accept", "").lower()


def apply_basic_auth_challenge(response, realm: str):
    """Keep Basic discovery for native clients without triggering browser dialogs."""
    if not is_browser_request():
        response.headers["WWW-Authenticate"] = f'Basic realm="{realm}"'
    return response

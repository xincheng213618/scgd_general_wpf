"""HTTP response hardening shared by every Flask route."""

from __future__ import annotations

from flask import request


_SPA_CONTENT_SECURITY_POLICY = "; ".join((
    "default-src 'self'",
    "base-uri 'self'",
    "object-src 'none'",
    "frame-ancestors 'self'",
    "form-action 'self'",
    "script-src 'self'",
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: https:",
    "font-src 'self' data:",
    "media-src 'self'",
    "connect-src 'self'",
    "worker-src 'self' blob:",
))
_DOCS_CONTENT_SECURITY_POLICY = _SPA_CONTENT_SECURITY_POLICY.replace(
    "script-src 'self'",
    "script-src 'self' 'unsafe-inline'",
)
_SENSITIVE_API_PREFIXES = (
    "/api/admin/",
    "/api/auth/",
    "/api/ops/",
    "/api/transfer/",
)


def content_security_policy(path: str) -> str:
    """Allow the static VitePress bootstrap scripts only on the docs surface."""
    if path == "/scgd_general_wpf" or path.startswith("/scgd_general_wpf/"):
        return _DOCS_CONTENT_SECURITY_POLICY
    return _SPA_CONTENT_SECURITY_POLICY


def register_response_security(app) -> None:
    """Apply browser security headers without overriding route-specific policy."""

    @app.after_request
    def _secure_response(response):
        response.headers.setdefault("Content-Security-Policy", content_security_policy(request.path))
        response.headers.setdefault("X-Content-Type-Options", "nosniff")
        response.headers.setdefault("X-Frame-Options", "SAMEORIGIN")
        response.headers.setdefault("Referrer-Policy", "same-origin")
        response.headers.setdefault(
            "Permissions-Policy",
            "camera=(), microphone=(), geolocation=(), payment=(), usb=()",
        )
        if request.path.startswith(_SENSITIVE_API_PREFIXES):
            response.headers.setdefault("Cache-Control", "no-store")
        return response

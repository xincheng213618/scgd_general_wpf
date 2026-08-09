"""Deprecated import shim for Flask authentication adapters.

Remove after repository-wide and external consumers import ``routes.auth_adapters``.
This module intentionally contains no Flask request/session access.
"""

from routes.auth_adapters import (
    check_web_session_auth,
    make_require_upload_auth as _make_require_upload_auth,
    require_web_auth,
)
from services.auth_policy import AuthPolicy


def make_require_upload_auth(cache, get_upload_auth, json_error):
    """Compatibility wrapper for the historical three-argument factory."""
    return _make_require_upload_auth(AuthPolicy(cache, get_upload_auth), json_error)


__all__ = ["check_web_session_auth", "make_require_upload_auth", "require_web_auth"]

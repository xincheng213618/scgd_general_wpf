"""
Public page routes for ColorVision Marketplace.

Handles login/logout and serves as the registration point for
web-facing HTML page routes.
"""

from __future__ import annotations

import hmac
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable

from flask import Blueprint, redirect, render_template, request, session, url_for

from db_cache import CacheManager


@dataclass(frozen=True)
class PublicPageContext:
    cache: CacheManager
    storage: Path
    config: dict[str, Any]
    get_upload_auth: Callable[[], tuple[str, str]]
    check_web_session_auth: Callable[[], bool]


public_pages = Blueprint("public_pages", __name__)

_ctx: PublicPageContext | None = None


def _get_ctx() -> PublicPageContext:
    if _ctx is None:
        raise RuntimeError("Public pages not initialized")
    return _ctx


def _safe_next_url(raw: str | None) -> str | None:
    """Validate that a next URL is a safe internal path. Returns None if unsafe."""
    if not raw:
        return None
    if raw.startswith("/") and not raw.startswith("//"):
        return raw
    return None


@public_pages.route("/login", methods=["GET", "POST"])
def login_page():
    """Web login page using Flask session (not Basic Auth)."""
    ctx = _get_ctx()
    if ctx.check_web_session_auth():
        next_url = _safe_next_url(request.args.get("next")) or url_for("upload_page")
        return redirect(next_url)

    error = None
    if request.method == "POST":
        username = request.form.get("username", "").strip()
        password = request.form.get("password", "")
        expected_username, expected_password = ctx.get_upload_auth()
        if (
            hmac.compare_digest(username, expected_username)
            and hmac.compare_digest(password, expected_password)
        ):
            session["authenticated"] = True
            session["username"] = username
            next_url = (
                _safe_next_url(request.form.get("next"))
                or _safe_next_url(request.args.get("next"))
                or url_for("upload_page")
            )
            return redirect(next_url)
        # Log failed login attempt (without leaking credentials)
        ctx.cache.write_audit(
            actor_type="anonymous",
            actor_id=username or "",
            action="login_failed",
            target_type="session",
            detail="Invalid credentials",
            ip=request.remote_addr or "",
            user_agent=request.headers.get("User-Agent", "")[:200],
        )
        error = "用户名或密码错误"

    next_url = _safe_next_url(request.args.get("next")) or ""
    return render_template("login.html", error=error, next_url=next_url)


@public_pages.route("/logout", methods=["GET"])
def logout_page():
    """Clear the web session and redirect to home."""
    session.clear()
    return redirect(url_for("index"))


def register_public_pages(app, ctx: PublicPageContext):
    global _ctx
    _ctx = ctx
    app.register_blueprint(public_pages)

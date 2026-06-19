"""
Authentication routes for the React Web frontend.

The login UI is rendered by the SPA. These routes only manage Flask session
state and keep the historical /login and /logout URLs usable.
"""

from __future__ import annotations

import hmac
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable
from urllib.parse import urlencode

from flask import Blueprint, current_app, jsonify, redirect, request, send_from_directory, session

from db_cache import CacheManager


@dataclass(frozen=True)
class PublicPageContext:
    cache: CacheManager
    storage: Path
    config: dict[str, Any]
    get_upload_auth: Callable[[], tuple[str, str]]
    check_web_session_auth: Callable[[], bool]
    dist_dir: Path


public_pages = Blueprint("public_pages", __name__)

_ctx: PublicPageContext | None = None


def _get_ctx() -> PublicPageContext:
    if _ctx is None:
        raise RuntimeError("Public pages not initialized")
    return _ctx


def _safe_next_url(raw: str | None) -> str:
    if raw and raw.startswith("/") and not raw.startswith("//"):
        return raw
    return "/admin"


def _serve_spa_index():
    dist = _get_ctx().dist_dir
    if not dist.exists():
        if current_app.config.get("TESTING"):
            return (
                '<!doctype html><html lang="zh-CN"><body><div id="root"></div></body></html>',
                200,
                {"Content-Type": "text/html; charset=utf-8"},
            )
        return (
            "Web frontend has not been built. Run `npm install` and `npm run build` in Web/Frontend.",
            503,
            {"Content-Type": "text/plain; charset=utf-8"},
        )
    return send_from_directory(dist, "index.html")


def _login(username: str, password: str):
    ctx = _get_ctx()
    expected_username, expected_password = ctx.get_upload_auth()
    if (
        hmac.compare_digest(username, expected_username)
        and hmac.compare_digest(password, expected_password)
    ):
        session["authenticated"] = True
        session["username"] = username
        return True

    ctx.cache.write_audit(
        actor_type="anonymous",
        actor_id=username or "",
        action="login_failed",
        target_type="session",
        detail="Invalid credentials",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return False


@public_pages.route("/login", methods=["GET", "POST"])
def login_page():
    if request.method == "GET":
        return _serve_spa_index()

    if request.is_json:
        data = request.get_json(silent=True) or {}
        username = str(data.get("username", "")).strip()
        password = str(data.get("password", ""))
        next_url = _safe_next_url(str(data.get("next", "") or request.args.get("next", "")))
        if _login(username, password):
            return jsonify({"authenticated": True, "username": username, "next": next_url})
        return jsonify({"error": "用户名或密码错误", "status": 401}), 401

    username = request.form.get("username", "").strip()
    password = request.form.get("password", "")
    next_url = _safe_next_url(request.form.get("next") or request.args.get("next"))
    if _login(username, password):
        return redirect(next_url)
    return redirect(f"/login?{urlencode({'next': next_url})}")


@public_pages.route("/api/auth/session", methods=["GET"])
def api_auth_session():
    return jsonify({
        "authenticated": bool(session.get("authenticated")),
        "username": session.get("username", ""),
    })


@public_pages.route("/api/auth/login", methods=["POST"])
def api_auth_login():
    data = request.get_json(silent=True) or {}
    username = str(data.get("username", "")).strip()
    password = str(data.get("password", ""))
    next_url = _safe_next_url(str(data.get("next", "") or ""))
    if _login(username, password):
        return jsonify({"authenticated": True, "username": username, "next": next_url})
    return jsonify({"error": "用户名或密码错误", "status": 401}), 401


@public_pages.route("/api/auth/logout", methods=["POST"])
def api_auth_logout():
    session.clear()
    return jsonify({"authenticated": False})


@public_pages.route("/logout", methods=["GET"])
def logout_page():
    session.clear()
    return redirect("/")


def register_public_pages(app, ctx: PublicPageContext):
    global _ctx
    _ctx = ctx
    app.register_blueprint(public_pages)

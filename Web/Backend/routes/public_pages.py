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


def _session_payload() -> dict[str, Any]:
    is_admin = bool(session.get("authenticated"))
    is_user = bool(session.get("user_authenticated") or is_admin)
    return {
        "authenticated": is_user,
        "is_admin": is_admin,
        "username": session.get("username", ""),
        "role": session.get("role", "admin" if is_admin else ("user" if is_user else "")),
    }


def _set_login_session(user: dict[str, Any]) -> dict[str, Any]:
    role = str(user.get("role") or "user")
    is_admin = role == "admin"
    session.clear()
    session["user_authenticated"] = True
    session["username"] = str(user.get("username") or "")
    session["role"] = role
    if user.get("id") is not None:
        session["user_id"] = user["id"]
    if is_admin:
        session["authenticated"] = True
    return _session_payload()


def _redirect_for_role(next_url: str, payload: dict[str, Any]) -> str:
    if not payload.get("is_admin") and next_url.startswith("/admin"):
        return "/transfer"
    return next_url


def _login(username: str, password: str) -> dict[str, Any] | None:
    ctx = _get_ctx()

    try:
        from services.auth_service import verify_user_credentials
        user = verify_user_credentials(ctx.cache, username, password)
        if user:
            return _set_login_session(user)
    except Exception:
        pass

    expected_username, expected_password = ctx.get_upload_auth()
    if (
        expected_username
        and expected_password
        and hmac.compare_digest(username, expected_username)
        and hmac.compare_digest(password, expected_password)
    ):
        return _set_login_session({"username": username, "role": "admin"})

    ctx.cache.write_audit(
        actor_type="anonymous",
        actor_id=username or "",
        action="login_failed",
        target_type="session",
        detail="Invalid credentials",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return None


@public_pages.route("/login", methods=["GET", "POST"])
def login_page():
    if request.method == "GET":
        return _serve_spa_index()

    if request.is_json:
        data = request.get_json(silent=True) or {}
        username = str(data.get("username", "")).strip()
        password = str(data.get("password", ""))
        next_url = _safe_next_url(str(data.get("next", "") or request.args.get("next", "")))
        payload = _login(username, password)
        if payload:
            payload["next"] = _redirect_for_role(next_url, payload)
            return jsonify(payload)
        return jsonify({"error": "用户名或密码错误", "status": 401}), 401

    username = request.form.get("username", "").strip()
    password = request.form.get("password", "")
    next_url = _safe_next_url(request.form.get("next") or request.args.get("next"))
    payload = _login(username, password)
    if payload:
        return redirect(_redirect_for_role(next_url, payload))
    return redirect(f"/login?{urlencode({'next': next_url})}")


@public_pages.route("/register", methods=["GET"])
def register_page():
    return _serve_spa_index()


@public_pages.route("/api/auth/session", methods=["GET"])
def api_auth_session():
    return jsonify(_session_payload())


@public_pages.route("/api/auth/login", methods=["POST"])
def api_auth_login():
    data = request.get_json(silent=True) or {}
    username = str(data.get("username", "")).strip()
    password = str(data.get("password", ""))
    next_url = _safe_next_url(str(data.get("next", "") or ""))
    payload = _login(username, password)
    if payload:
        payload["next"] = _redirect_for_role(next_url, payload)
        return jsonify(payload)
    return jsonify({"error": "用户名或密码错误", "status": 401}), 401


@public_pages.route("/api/auth/register", methods=["POST"])
def api_auth_register():
    data = request.get_json(silent=True) or {}
    username = str(data.get("username", "")).strip()
    password = str(data.get("password", ""))
    next_url = _safe_next_url(str(data.get("next", "") or "/transfer"))

    expected_username, _ = _get_ctx().get_upload_auth()
    if expected_username and username.lower() == expected_username.lower():
        return jsonify({"error": "该用户名保留给管理员", "status": 400}), 400

    from services.auth_service import create_user
    user, error = create_user(_get_ctx().cache, username, password, role="user")
    if error or not user:
        return jsonify({"error": error or "注册失败", "status": 400}), 400

    payload = _set_login_session(user)
    payload["next"] = _redirect_for_role(next_url, payload)
    _get_ctx().cache.write_audit(
        actor_type="user",
        actor_id=payload.get("username", ""),
        action="user_register",
        target_type="user",
        target_id=payload.get("username", ""),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify(payload), 201


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

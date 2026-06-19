"""
React frontend static hosting.

All user-facing pages are rendered by the Vite build in Web/Frontend/dist.
Flask keeps API and file routes separate and falls back to index.html only for
known application routes.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Callable
from urllib.parse import urlencode

from flask import Blueprint, current_app, redirect, request, send_from_directory


@dataclass(frozen=True)
class FrontendSpaContext:
    check_auth: Callable[[], bool]
    dist_dir: Path


frontend_spa = Blueprint("frontend_spa", __name__)

_ctx: FrontendSpaContext | None = None


def _get_ctx() -> FrontendSpaContext:
    if _ctx is None:
        raise RuntimeError("Frontend SPA not initialized")
    return _ctx


def _current_internal_path() -> str:
    return request.full_path.rstrip("?")


def _serve_spa():
    ctx = _get_ctx()
    dist = ctx.dist_dir
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


def _serve_asset(asset_path: str):
    ctx = _get_ctx()
    dist = ctx.dist_dir
    target = dist / asset_path
    if target.is_file():
        return send_from_directory(dist, asset_path)
    return _serve_spa()


@frontend_spa.route("/assets/<path:asset_path>")
def assets(asset_path: str):
    return _serve_asset(f"assets/{asset_path}")


@frontend_spa.route("/favicon.svg")
def favicon():
    return _serve_asset("favicon.svg")


@frontend_spa.route("/admin")
@frontend_spa.route("/admin/")
@frontend_spa.route("/admin/<path:spa_path>")
def admin_spa(spa_path: str = ""):
    if not _get_ctx().check_auth():
        return redirect(f"/login?{urlencode({'next': _current_internal_path()})}")
    return _serve_spa()


@frontend_spa.route("/")
@frontend_spa.route("/plugins")
@frontend_spa.route("/plugins/<path:spa_path>")
@frontend_spa.route("/releases")
@frontend_spa.route("/changelog")
@frontend_spa.route("/updates")
@frontend_spa.route("/tools")
@frontend_spa.route("/browse")
@frontend_spa.route("/browse/<path:spa_path>")
def site_spa(spa_path: str = ""):
    return _serve_spa()


def register_frontend_spa(app, ctx: FrontendSpaContext):
    global _ctx
    _ctx = ctx
    app.register_blueprint(frontend_spa)

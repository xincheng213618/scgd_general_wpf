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

from flask import Blueprint, abort, current_app, redirect, request, send_from_directory


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
    response = send_from_directory(dist, "index.html", max_age=0)
    # The HTML names hashed chunks, so it must revalidate on every navigation
    # while those immutable chunks can be cached aggressively.
    response.headers["Cache-Control"] = "no-cache, must-revalidate"
    return response


def _serve_asset(asset_path: str, *, immutable: bool = False):
    ctx = _get_ctx()
    dist = ctx.dist_dir
    target = dist / asset_path
    if target.is_file():
        response = send_from_directory(
            dist,
            asset_path,
            max_age=31_536_000 if immutable else 3_600,
        )
        if immutable:
            response.headers["Cache-Control"] = "public, max-age=31536000, immutable"
        return response
    # A missing hashed Vite chunk must stay a real miss. Returning index.html
    # with status 200 makes dynamic import treat HTML as JavaScript and leaves
    # an already-open tab on a blank screen after a rolling deployment.
    abort(404)


@frontend_spa.route("/assets/<path:asset_path>")
def assets(asset_path: str):
    return _serve_asset(f"assets/{asset_path}", immutable=True)


@frontend_spa.route("/brand/<path:asset_path>")
def brand_assets(asset_path: str):
    return _serve_asset(f"brand/{asset_path}")


@frontend_spa.route("/media/<path:asset_path>")
def media_assets(asset_path: str):
    return _serve_asset(f"media/{asset_path}")


@frontend_spa.route("/favicon.svg")
def favicon():
    return _serve_asset("favicon.svg")


@frontend_spa.route("/favicon.ico")
def favicon_ico():
    return _serve_asset("brand/colorvision.ico")


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
@frontend_spa.route("/transfer")
@frontend_spa.route("/browse")
@frontend_spa.route("/browse/<path:spa_path>")
def site_spa(spa_path: str = ""):
    return _serve_spa()


def register_frontend_spa(app, ctx: FrontendSpaContext):
    global _ctx
    _ctx = ctx
    app.register_blueprint(frontend_spa)

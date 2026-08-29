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
_PRECOMPRESSED_SUFFIXES = (("br", ".br"), ("gzip", ".gz"))
_PRECOMPRESSED_FILE_SUFFIXES = tuple(suffix for _, suffix in _PRECOMPRESSED_SUFFIXES)


def _get_ctx() -> FrontendSpaContext:
    if _ctx is None:
        raise RuntimeError("Frontend SPA not initialized")
    return _ctx


def _current_internal_path() -> str:
    return request.full_path.rstrip("?")


def _identity_is_acceptable() -> bool:
    header = request.headers.get("Accept-Encoding")
    if not header:
        return True
    qualities = {
        encoding.lower(): quality
        for encoding, quality in request.accept_encodings
    }
    if "identity" in qualities:
        return qualities["identity"] > 0
    return qualities.get("*") != 0


def _send_static_file(dist: Path, asset_path: str, *, max_age: int):
    """Serve a build artifact, preferring an available precompressed variant."""
    available_encodings = [
        encoding
        for encoding, suffix in _PRECOMPRESSED_SUFFIXES
        if (dist / f"{asset_path}{suffix}").is_file()
    ]
    has_variants = bool(available_encodings)
    selected_encoding = None
    selected_path = asset_path
    identity_is_acceptable = _identity_is_acceptable()

    # Preserve the existing byte-range contract against the identity file.
    # If identity is explicitly refused, the range correctly addresses the
    # selected encoded representation instead.
    should_negotiate = has_variants and (
        not request.headers.get("Range") or not identity_is_acceptable
    )
    if should_negotiate:
        best_match = request.accept_encodings.best_match(
            [
                *available_encodings,
                *(("identity",) if identity_is_acceptable else ()),
            ],
        )
        if best_match in available_encodings:
            selected_encoding = best_match
            selected_path = f"{asset_path}{'.br' if best_match == 'br' else '.gz'}"
    if selected_encoding is None and not identity_is_acceptable:
        response = current_app.make_response(("", 406))
        response.vary.add("Accept-Encoding")
        return response

    response = send_from_directory(
        dist,
        selected_path,
        download_name=Path(asset_path).name,
        max_age=max_age,
    )
    if has_variants:
        response.vary.add("Accept-Encoding")
    if selected_encoding:
        response.headers["Content-Encoding"] = selected_encoding
    return response


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
    response = _send_static_file(dist, "index.html", max_age=0)
    # The HTML names hashed chunks, so it must revalidate on every navigation
    # while those immutable chunks can be cached aggressively.
    response.headers["Cache-Control"] = "no-cache, must-revalidate"
    return response


def _serve_asset(asset_path: str, *, immutable: bool = False):
    ctx = _get_ctx()
    dist = ctx.dist_dir
    if asset_path.endswith(_PRECOMPRESSED_FILE_SUFFIXES):
        abort(404)
    target = dist / asset_path
    if target.is_file():
        response = _send_static_file(
            dist, asset_path, max_age=31_536_000 if immutable else 3_600,
        )
        if immutable and response.status_code in (200, 206, 304):
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
@frontend_spa.route("/transfer/share/<path:spa_path>")
@frontend_spa.route("/account")
@frontend_spa.route("/browse")
@frontend_spa.route("/browse/<path:spa_path>")
def site_spa(spa_path: str = ""):
    return _serve_spa()


def register_frontend_spa(app, ctx: FrontendSpaContext):
    global _ctx
    _ctx = ctx
    app.register_blueprint(frontend_spa)

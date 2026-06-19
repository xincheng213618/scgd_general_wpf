"""
Page routes and app-level API routes for ColorVision Marketplace.

Handles: /, /releases, /changelog, /updates, /tools, /plugins, /browse,
         /upload, /download, /api/app/*, plugin icon.
"""

from __future__ import annotations

import hashlib
import hmac
from pathlib import Path
from typing import Any

from flask import Blueprint, abort, jsonify, redirect, render_template, request, send_from_directory, session, url_for

pages = Blueprint("pages", __name__)

_app_mod = None
_SERVICES = None


def register_pages(app, services):
    global _app_mod, _SERVICES
    _app_mod = __import__("app")
    _SERVICES = services
    app.register_blueprint(pages)


def _storage():
    return _app_mod.STORAGE


def _cache():
    return _app_mod._cache


def _services():
    return _SERVICES


def _is_transfer_storage_path(relative_path: str) -> bool:
    try:
        from transfer_files import is_transfer_storage_path
        target = _app_mod._storage_target(relative_path)
        return is_transfer_storage_path(_storage(), _app_mod.CONFIG, target)
    except Exception:
        return False


def _has_transfer_auth() -> bool:
    if session.get("authenticated"):
        return True

    auth = request.authorization
    if auth and (auth.type or "").lower() == "basic" and auth.username and auth.password:
        expected_username, expected_password = _app_mod._get_upload_auth()
        if (
            expected_username
            and expected_password
            and hmac.compare_digest(auth.username, expected_username)
            and hmac.compare_digest(auth.password, expected_password)
        ):
            return True

    auth_header = request.headers.get("Authorization", "")
    if auth_header.startswith("Bearer "):
        token = auth_header[7:].strip()
        if token:
            from services.api_key_service import verify_api_key
            from transfer_files import TRANSFER_FILE_SCOPE
            return verify_api_key(_cache(), token, required_scopes=[TRANSFER_FILE_SCOPE]) is not None

    return False


def _require_transfer_auth_for_storage_path(relative_path: str, *, challenge: bool):
    if not _is_transfer_storage_path(relative_path):
        return None
    if _has_transfer_auth():
        return None
    if challenge:
        response = _app_mod.app.response_class("Authentication required", status=401)
        response.headers["WWW-Authenticate"] = 'Basic realm="ColorVision Transfer"'
        return response
    return redirect(url_for("public_pages.login_page", next=request.url))


# -------------------------------------------------------------------
# Home page
# -------------------------------------------------------------------

@pages.route("/")
def index():
    from page_contexts import build_index_page_context
    return render_template("index.html", **build_index_page_context(
        _storage(),
        get_app_info=_services().get_request_home_app_info,
        get_storage_overview_context=_services().get_storage_overview_context,
        get_tool_preview=_services().get_request_home_tool_preview,
        cache_manager=_cache(),
    ))


# -------------------------------------------------------------------
# Release / changelog / update / tool pages
# -------------------------------------------------------------------

@pages.route("/releases")
def releases_page():
    from page_contexts import build_releases_page_context
    return render_template("releases.html", **build_releases_page_context(
        _services().get_request_release_app_info(),
        major_minor=request.args.get("major_minor", ""),
        branch=request.args.get("branch", ""),
        kind=request.args.get("kind", ""),
        era=request.args.get("era", ""),
    ))


@pages.route("/changelog")
def changelog_page():
    return render_template("changelog.html", app_info=_services().get_request_changelog_app_info())


@pages.route("/updates")
def updates_page():
    from page_contexts import build_updates_page_context
    return render_template("updates.html", **build_updates_page_context(_storage(), cache_manager=_cache()))


@pages.route("/tools")
def tools_page():
    from page_contexts import build_tools_page_context
    return render_template("tools.html", **build_tools_page_context(_storage(), cache_manager=_cache()))


@pages.route("/download/<path:relative_path>")
def download_storage_file(relative_path):
    normalized = _app_mod._normalize_relative_path(relative_path)
    auth_result = _require_transfer_auth_for_storage_path(normalized, challenge=True)
    if auth_result is not None:
        return auth_result
    target = _services().resolve_storage_file(normalized)
    return send_from_directory(str(target.parent), target.name, as_attachment=True)


# -------------------------------------------------------------------
# Plugin pages
# -------------------------------------------------------------------

@pages.route("/plugins")
def plugins_page():
    from catalog_view_models import DEFAULT_HTML_PAGE_SIZE, build_plugin_catalog_page_context
    page = max(1, int(request.args.get("page", 1)))
    page_size = min(60, max(1, int(request.args.get("pageSize", DEFAULT_HTML_PAGE_SIZE))))
    return render_template("plugins.html", **build_plugin_catalog_page_context(
        _services().get_request_plugin_catalog(),
        keyword=request.args.get("q", ""),
        category=request.args.get("category", ""),
        author=request.args.get("author", ""),
        sort_by=request.args.get("sort", "updated"),
        page=page, page_size=page_size,
    ))


@pages.route("/plugins/<plugin_id>")
def plugin_detail_page(plugin_id):
    if not _app_mod._is_safe_id(plugin_id):
        abort(404)
    info = _services().get_plugin_info(plugin_id, download_counts=_services().get_request_download_counts())
    if not info:
        abort(404)
    return render_template("plugin_detail.html", plugin=info)


@pages.route("/plugins/<plugin_id>/icon")
def plugin_icon(plugin_id):
    from email.utils import formatdate, parsedate_to_datetime
    from plugin_marketplace import load_plugin_icon_payload
    if not _app_mod._is_safe_id(plugin_id):
        abort(404)
    storage = _storage()
    icon_path = storage / "Plugins" / plugin_id / "PackageIcon.png"
    last_ts = 0.0
    if icon_path.is_file():
        try:
            last_ts = icon_path.stat().st_mtime
        except OSError:
            pass
    payload_bytes, ct = load_plugin_icon_payload(storage, plugin_id)
    if payload_bytes is None or not ct:
        abort(404)
    etag = hashlib.md5(payload_bytes).hexdigest()
    inm = request.headers.get("If-None-Match", "")
    if inm and inm.strip('"') == etag:
        return _app_mod.app.response_class(status=304)
    if last_ts > 0:
        ims = request.headers.get("If-Modified-Since", "")
        if ims:
            try:
                if last_ts <= parsedate_to_datetime(ims).timestamp():
                    return _app_mod.app.response_class(status=304)
            except (ValueError, TypeError):
                pass
    resp = _app_mod.app.response_class(payload_bytes, mimetype=ct)
    resp.headers["ETag"] = f'"{etag}"'
    if last_ts > 0:
        resp.headers["Last-Modified"] = formatdate(last_ts, usegmt=True)
    resp.headers["Cache-Control"] = "public, max-age=3600"
    return resp


# -------------------------------------------------------------------
# Upload page
# -------------------------------------------------------------------

@pages.route("/upload", methods=["GET", "POST"])
def upload_page():
    from flask import session, redirect, url_for
    if not session.get("authenticated"):
        try:
            return redirect(url_for("public_pages.login_page", next=request.url))
        except Exception:
            return redirect(url_for("login_page", next=request.url))

    from page_contexts import build_upload_page_context
    from package_publish import PackageValidationError, finalize_plugin_publish, save_package_file, validate_html_upload_request
    from plugin_marketplace import prewarm_plugin_metadata

    storage = _storage()
    svc = _services()
    config = _app_mod.CONFIG
    keep = int(config.get("plugin_package_keep_count", 3) or 3)

    if request.method == "GET":
        return render_template("upload.html", **build_upload_page_context(
            message=None, error=None,
            max_upload_size_bytes=_app_mod.MAX_UPLOAD_SIZE_BYTES,
            plugin_package_keep_count=keep,
        ))

    file = request.files.get("package")
    pid = request.form.get("plugin_id", "")
    try:
        req = validate_html_upload_request(
            file, pid,
            sanitize_filename=_app_mod._sanitize_filename,
            validate_plugin_id=_app_mod._is_safe_id,
            validate_version=_app_mod._is_safe_version,
        )
        save_result = save_package_file(
            storage, file, req,
            validate_plugin_id=_app_mod._is_safe_id,
            read_text_file=svc._read_text_file,
            version_tuple=lambda v: tuple(int(x) for x in v.split(".") if x.isdigit()),
            reconcile_plugin_package_history=svc.reconcile_plugin_package_history,
        )
        finalize_plugin_publish(
            storage, plugin_id=req.plugin_id, version=req.version,
            refresh_related_caches=_app_mod._refresh_related_caches,
            prewarm_plugin_metadata=prewarm_plugin_metadata,
            get_download_counts=svc.get_download_counts,
            get_cache_entry=_app_mod._get_cache_entry,
            set_cache_entry=_app_mod._set_cache_entry,
            ttl_seconds=_app_mod.PLUGIN_INFO_CACHE_TTL_SECONDS,
        )
        from services.storage_events import _refresh_plugin_index
        _refresh_plugin_index(
            _cache(), storage, f"Plugins/{req.plugin_id}",
            get_download_counts=svc.get_download_counts,
            get_cache_entry=_app_mod._get_cache_entry,
            set_cache_entry=_app_mod._set_cache_entry,
            ttl_seconds=_app_mod.PLUGIN_INFO_CACHE_TTL_SECONDS,
            get_request_username=_app_mod._ctx.get_request_username,
        )
    except PackageValidationError as exc:
        return render_template("upload.html", **build_upload_page_context(
            message=None, error=str(exc),
            max_upload_size_bytes=_app_mod.MAX_UPLOAD_SIZE_BYTES,
            plugin_package_keep_count=keep,
        ))

    msg = f"上传成功: {req.safe_filename} → Plugins/{req.plugin_id}/"
    if save_result.moved_packages:
        msg += f"，并自动归档 {len(save_result.moved_packages)} 个旧版本"
    return render_template("upload.html", **build_upload_page_context(
        message=msg, error=None,
        max_upload_size_bytes=_app_mod.MAX_UPLOAD_SIZE_BYTES,
        plugin_package_keep_count=keep,
    ))


# -------------------------------------------------------------------
# Browse
# -------------------------------------------------------------------

@pages.route("/browse")
@pages.route("/browse/<path:subpath>")
def browse_page(subpath=""):
    from page_contexts import build_browse_page_context
    storage = _storage()
    normalized = _app_mod._normalize_relative_path(subpath)
    auth_result = _require_transfer_auth_for_storage_path(normalized, challenge=False)
    if auth_result is not None:
        return auth_result
    target = _app_mod._storage_target(normalized)
    if not target.exists():
        abort(404)
    try:
        target.resolve().relative_to(storage.resolve())
    except ValueError:
        abort(403)
    if target.is_file():
        return send_from_directory(str(target.parent), target.name)
    limit = max(1, min(1000, int(request.args.get("limit", 200))))
    offset = max(0, int(request.args.get("offset", 0)))
    return render_template("browse.html",
        **build_browse_page_context(storage, normalized, limit=limit, offset=offset))


# -------------------------------------------------------------------
# App-level API
# -------------------------------------------------------------------

@pages.route("/api/app/latest-version")
def api_app_latest_version():
    version = _services()._read_text_file(_storage() / "LATEST_RELEASE") or ""
    return jsonify({"version": version.strip()})


@pages.route("/api/app/changelog")
def api_app_changelog():
    changelog = _services()._read_text_file(_storage() / "CHANGELOG.md")
    if not changelog:
        return jsonify({"error": "CHANGELOG.md not found"}), 404
    return _app_mod.app.response_class(changelog, content_type="text/plain; charset=utf-8")


@pages.route("/api/app/releases/<version>/download")
def api_app_release_download(version):
    if not _app_mod._is_safe_version(version):
        return jsonify({"error": "Invalid version format"}), 400
    svc = _services()
    candidates = [
        i for i in svc.scan_app_release_artifacts()
        if str(i.get("version", "")).strip() == version and str(i.get("kind", "")).upper() == "EXE"
    ]
    if not candidates:
        return jsonify({"error": f"Installer for version {version} not found"}), 404
    best = max(candidates, key=lambda i: (i.get("source") == "current", str(i.get("modified", ""))))
    target = svc.resolve_storage_file(str(best.get("relative_path", "")))
    return send_from_directory(str(target.parent), target.name, as_attachment=True)


@pages.route("/api/app/updates/<version>/download")
def api_app_incremental_download(version):
    if not _app_mod._is_safe_version(version):
        return jsonify({"error": "Invalid version format"}), 400
    from update_retention import repair_update_storage_layout
    storage = _storage()
    repair_update_storage_layout(storage)
    target = storage / "Update" / f"ColorVision-Update-[{version}].cvx"
    if not target.is_file():
        return jsonify({"error": f"Incremental package for version {version} not found"}), 404
    return send_from_directory(str(target.parent), target.name, as_attachment=True)

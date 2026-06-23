"""
Site data and download routes for the Web frontend.

The React application owns all user-facing HTML routes. This module exposes the
JSON payloads and file-serving endpoints that those pages need.
"""

from __future__ import annotations

import hmac
import hashlib

from flask import Blueprint, abort, jsonify, request, send_from_directory, session

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
    if session.get("authenticated") or session.get("user_authenticated"):
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


def _transfer_auth_challenge():
    response = _app_mod.app.response_class("Authentication required", status=401)
    response.headers["WWW-Authenticate"] = 'Basic realm="ColorVision Transfer"'
    return response


def _require_transfer_auth_for_storage_path(relative_path: str):
    if not _is_transfer_storage_path(relative_path):
        return None
    if _has_transfer_auth():
        return None
    return _transfer_auth_challenge()


def _parse_int(name: str, *, default: int, minimum: int, maximum: int):
    raw = request.args.get(name)
    if raw is None or str(raw).strip() == "":
        return default
    try:
        value = int(str(raw).strip())
    except (TypeError, ValueError):
        abort(400, description=f"Invalid integer parameter: {name}")
    return max(minimum, min(maximum, value))


def _latest_version_payload() -> tuple[str, str]:
    from services.app_latest_version_cache import get_latest_version_cached, latest_version_etag
    version = get_latest_version_cached(_storage())
    return version, latest_version_etag(_storage(), version)


@pages.route("/api/site/home")
def api_site_home():
    from page_contexts import build_index_page_context

    return jsonify(build_index_page_context(
        _storage(),
        get_app_info=_services().get_request_home_app_info,
        get_storage_overview_context=_services().get_storage_overview_context,
        get_tool_preview=_services().get_request_home_tool_preview,
        cache_manager=_cache(),
    ))


@pages.route("/api/site/releases")
def api_site_releases():
    from page_contexts import build_releases_page_context

    return jsonify(build_releases_page_context(
        _services().get_request_release_app_info(),
        major_minor=request.args.get("major_minor", ""),
        branch=request.args.get("branch", ""),
        kind=request.args.get("kind", ""),
        era=request.args.get("era", ""),
    ))


@pages.route("/api/site/changelog")
def api_site_changelog():
    return jsonify({"app_info": _services().get_request_changelog_app_info()})


@pages.route("/api/site/updates")
def api_site_updates():
    from page_contexts import build_updates_page_context

    return jsonify(build_updates_page_context(_storage(), cache_manager=_cache()))


@pages.route("/api/site/tools")
def api_site_tools():
    from page_contexts import build_tools_page_context

    return jsonify(build_tools_page_context(_storage(), cache_manager=_cache()))


@pages.route("/api/site/browse")
@pages.route("/api/site/browse/<path:subpath>")
def api_site_browse(subpath: str = ""):
    from page_contexts import build_browse_page_context

    normalized = _app_mod._normalize_relative_path(subpath)
    auth_result = _require_transfer_auth_for_storage_path(normalized)
    if auth_result is not None:
        return auth_result

    storage = _storage()
    target = _app_mod._storage_target(normalized)
    if not target.exists():
        abort(404)
    try:
        target.resolve().relative_to(storage.resolve())
    except ValueError:
        abort(403)
    if target.is_file():
        return jsonify({
            "is_file": True,
            "name": target.name,
            "subpath": normalized,
            "download_url": f"/download/{normalized}",
        })

    limit = _parse_int("limit", default=200, minimum=1, maximum=1000)
    offset = _parse_int("offset", default=0, minimum=0, maximum=100000)
    payload = build_browse_page_context(storage, normalized, limit=limit, offset=offset)
    payload["is_file"] = False
    return jsonify(payload)


@pages.route("/api/site/upload/context")
def api_site_upload_context():
    from page_contexts import build_upload_page_context

    keep = int(_app_mod.CONFIG.get("plugin_package_keep_count", 3) or 3)
    return jsonify(build_upload_page_context(
        message=None,
        error=None,
        max_upload_size_bytes=_app_mod.MAX_UPLOAD_SIZE_BYTES,
        plugin_package_keep_count=keep,
    ))


@pages.route("/download/<path:relative_path>")
def download_storage_file(relative_path):
    normalized = _app_mod._normalize_relative_path(relative_path)
    auth_result = _require_transfer_auth_for_storage_path(normalized)
    if auth_result is not None:
        return auth_result
    target = _services().resolve_storage_file(normalized)
    return send_from_directory(str(target.parent), target.name, as_attachment=True)


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


@pages.route("/api/app/latest-version")
def api_app_latest_version():
    version, etag = _latest_version_payload()
    if request.headers.get("If-None-Match", "").strip('"') == etag:
        response = _app_mod.app.response_class(status=304)
        response.headers["ETag"] = f'"{etag}"'
        response.headers["Cache-Control"] = "public, max-age=30"
        return response

    response = jsonify({"version": version})
    response.headers["ETag"] = f'"{etag}"'
    response.headers["Cache-Control"] = "public, max-age=30"
    return response


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

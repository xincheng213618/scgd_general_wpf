"""
CVWindowsService routes for ColorVision Marketplace.

Handles /api/tool/cvwindowsservice/* for the React Web frontend.
"""

from __future__ import annotations

import hmac
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from flask import Blueprint, jsonify, request, send_from_directory, session

cvws_api = Blueprint("cvws_api", __name__)

_app_mod = None
_CVWS_PACKAGE_RE = None


def register_cvws_api(app, ctx):
    global _app_mod, _CVWS_PACKAGE_RE
    _app_mod = __import__("app")
    from cvwindowsservice_publish import CVWS_PACKAGE_RE
    _CVWS_PACKAGE_RE = CVWS_PACKAGE_RE
    app.register_blueprint(cvws_api)


def _get_storage():
    return _app_mod.STORAGE


def _scan_cvwindowsservice_packages():
    storage = _get_storage()
    tool_dir = storage / "Tool" / "CVWindowsService"
    if not tool_dir.is_dir():
        return []
    packages = []
    for entry in tool_dir.iterdir():
        if not entry.is_file():
            continue
        m = _CVWS_PACKAGE_RE.match(entry.name)
        if not m:
            continue
        try:
            stat = entry.stat()
            dt = datetime.fromtimestamp(stat.st_mtime, tz=timezone.utc)
            packages.append({
                "fileName": entry.name, "version": m.group("version"),
                "suffix": m.group("suffix") or "", "size": stat.st_size,
                "sizeText": _app_mod.human_size(stat.st_size),
                "modified": dt.isoformat(), "modifiedDisplay": dt.strftime("%Y-%m-%d %H:%M"),
                "downloadUrl": f"/download/Tool/CVWindowsService/{entry.name}",
            })
        except OSError:
            pass
    packages.sort(key=lambda p: tuple(int(x) for x in p["version"].split(".")), reverse=True)
    return packages


def _cvws_cache_signature(tool_dir, latest_version):
    try:
        return f"{latest_version.strip()}|{int(tool_dir.stat().st_mtime)}"
    except OSError:
        return f"{latest_version.strip()}|0"


def _get_cvwindowsservice_releases_payload():
    storage = _get_storage()
    tool_dir = storage / "Tool" / "CVWindowsService"
    lr = tool_dir / "LATEST_RELEASE"
    latest = lr.read_text(encoding="utf-8").strip() if lr.exists() else ""
    sig = _cvws_cache_signature(tool_dir, latest)
    cached = _app_mod._get_cache_entry(_app_mod.CVWS_RELEASES_CACHE_KEY, signature=sig)
    if cached and isinstance(cached.get("value"), dict):
        return cached["value"]
    packages = _scan_cvwindowsservice_packages()
    payload = {"latestVersion": latest, "packages": packages, "count": len(packages)}
    _app_mod._set_cache_entry(_app_mod.CVWS_RELEASES_CACHE_KEY, payload,
                               ttl_seconds=_app_mod.CVWS_RELEASES_CACHE_TTL_SECONDS, signature=sig)
    return payload


def _json_auth_error():
    response = jsonify({"error": "Authentication required", "status": 401})
    response.status_code = 401
    response.headers["WWW-Authenticate"] = 'Basic realm="ColorVision Marketplace"'
    return response


def _has_cvws_publish_auth() -> bool:
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
            return verify_api_key(_app_mod._cache, token, required_scopes=["release:publish"]) is not None

    return False


@cvws_api.route("/api/tool/cvwindowsservice/latest-version")
def api_cvwindowsservice_latest_version():
    payload = _get_cvwindowsservice_releases_payload()
    version = str(payload.get("latestVersion", "")).strip()
    if not version:
        return jsonify({"error": "LATEST_RELEASE not found"}), 404
    return jsonify({"version": version})


@cvws_api.route("/api/tool/cvwindowsservice/releases")
def api_cvwindowsservice_releases():
    return jsonify(_get_cvwindowsservice_releases_payload())


@cvws_api.route("/api/tool/cvwindowsservice/context")
def api_cvwindowsservice_context():
    from cvwindowsservice_publish import build_cvws_page_context

    storage = _get_storage()
    read_text = lambda p: p.read_text(encoding="utf-8").strip() if p.exists() else None
    payload = build_cvws_page_context(
        storage,
        scan_packages=_scan_cvwindowsservice_packages,
        read_text_file=read_text,
        human_size=_app_mod.human_size,
    )
    payload.pop("human_size", None)
    return jsonify(payload)


@cvws_api.route("/api/tool/cvwindowsservice/download/<version>")
def api_cvwindowsservice_download(version):
    storage = _get_storage()
    if not _app_mod._is_safe_version(version):
        return jsonify({"error": "Invalid version format"}), 400
    tool_dir = storage / "Tool" / "CVWindowsService"
    if not tool_dir.is_dir():
        return jsonify({"error": "CVWindowsService directory not found"}), 404
    matches = []
    for entry in tool_dir.iterdir():
        if not entry.is_file():
            continue
        m = _CVWS_PACKAGE_RE.match(entry.name)
        if m and m.group("version") == version:
            try:
                suffix = int(m.group("suffix") or "0")
            except ValueError:
                suffix = 0
            matches.append((suffix, entry))
    best = max(matches, key=lambda x: x[0])[1] if matches else None
    if best is None:
        return jsonify({"error": f"Package for version {version} not found"}), 404
    return send_from_directory(str(best.parent), best.name, as_attachment=True)


@cvws_api.route("/api/tool/cvwindowsservice/publish", methods=["POST"])
def api_cvwindowsservice_publish():
    if not _has_cvws_publish_auth():
        return _json_auth_error()

    from cvwindowsservice_publish import (
        infer_version_from_filename,
        is_official_filename, save_cvws_package, update_cvws_latest_release,
        validate_version as validate_cvws_version,
    )
    storage = _get_storage()

    file = request.files.get("package")
    version = request.form.get("version", "").strip()
    set_latest = request.form.get("set_latest", "true").lower() in ("1", "true", "yes", "on")

    if not file or not getattr(file, "filename", ""):
        return jsonify({"error": "请选择要上传的文件"}), 400
    if not file.filename.lower().endswith(".zip"):
        return jsonify({"error": "只允许上传 .zip 文件"}), 400
    if not version:
        version = infer_version_from_filename(file.filename) or ""
    if not version:
        err = "无法从文件名解析版本号" if is_official_filename(file.filename) else "文件名不符合规则"
        return jsonify({"error": err}), 400
    if not validate_cvws_version(version):
        return jsonify({"error": f"版本号格式不正确: {version}"}), 400

    target_dir = storage / "Tool" / "CVWindowsService"
    try:
        result = save_cvws_package(file, target_dir, version,
                                    original_filename=file.filename if is_official_filename(file.filename) else None)
    except OSError as exc:
        return jsonify({"error": f"保存文件失败: {exc}"}), 500

    if set_latest:
        update_cvws_latest_release(target_dir, version)

    _app_mod._cache.invalidate_cache_prefix("cvws_releases:")
    _app_mod._cache.invalidate_cache_prefix("home_tool_preview:")
    _app_mod._cache.invalidate_cache_prefix("storage_overview:")
    from services.storage_events import on_storage_change
    on_storage_change(_app_mod._cache, storage, "Tool/CVWindowsService")

    lr = target_dir / "LATEST_RELEASE"
    latest_now = lr.read_text(encoding="utf-8").strip() if lr.exists() else ""
    msg = f"上传成功: {result.saved_filename} (版本 {result.version})"
    if set_latest:
        msg += f"，已更新 LATEST_RELEASE → {latest_now}"

    return jsonify({
        "message": msg,
        "latestVersion": latest_now,
        "result": {
            "savedFilename": result.saved_filename,
            "version": result.version,
            "isLatest": set_latest,
        },
        "releases": _get_cvwindowsservice_releases_payload(),
    }), 201

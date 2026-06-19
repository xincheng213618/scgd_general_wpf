"""
Large transfer file routes.

These endpoints are intentionally limited to a single configured folder and
stream request bodies directly to disk so multi-GB files do not hit the normal
package upload size limit.
"""

from __future__ import annotations

from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Callable
from urllib.parse import quote

from flask import Blueprint, jsonify, redirect, render_template, request, send_from_directory, session, url_for

from db_cache import CacheManager
from transfer_files import (
    TRANSFER_FILE_SCOPE,
    TransferFileError,
    delete_transfer_file,
    list_transfer_files,
    resolve_transfer_file,
    stream_transfer_upload,
    transfer_root,
)


@dataclass(frozen=True)
class TransferRouteContext:
    cache: CacheManager
    storage_getter: Callable[[], Path]
    config_getter: Callable[[], dict[str, Any]]
    check_auth: Callable[[list[str] | None], bool]
    human_size: Callable[[int], str]


transfer_routes = Blueprint("transfer_routes", __name__)

_ctx: TransferRouteContext | None = None


def _get_ctx() -> TransferRouteContext:
    if _ctx is None:
        raise RuntimeError("Transfer routes not initialized")
    return _ctx


def _actor_type() -> str:
    if session.get("authenticated"):
        return "user"
    auth_header = request.headers.get("Authorization", "")
    if auth_header.startswith("Bearer "):
        return "api_key"
    auth = request.authorization
    if auth and auth.type and auth.type.lower() == "basic":
        return "user"
    return "system"


def _actor_id() -> str:
    if session.get("username"):
        return session["username"]
    auth_header = request.headers.get("Authorization", "")
    if auth_header.startswith("Bearer "):
        token = auth_header[7:].strip()
        parts = token.split("_", 2)
        if len(parts) >= 2:
            return f"key:{parts[1]}"
        return "key:unknown"
    auth = request.authorization
    if auth and auth.username:
        return auth.username
    return "system"


def _json_error(message: str, status_code: int):
    response = jsonify({"error": message, "status": status_code})
    response.status_code = status_code
    if status_code == 401:
        response.headers["WWW-Authenticate"] = 'Basic realm="ColorVision Transfer"'
    return response


def _require_transfer_auth(*, api: bool):
    ctx = _get_ctx()
    if ctx.check_auth([TRANSFER_FILE_SCOPE]):
        return None
    if api:
        return _json_error("Authentication required", 401)
    return redirect(url_for("public_pages.login_page", next=request.url))


def _root() -> Path:
    ctx = _get_ctx()
    return transfer_root(ctx.storage_getter(), ctx.config_getter())


def _write_audit(action: str, *, target_id: str = "", detail: str = "") -> None:
    ctx = _get_ctx()
    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action=action,
        target_type="transfer_file",
        target_id=target_id,
        detail=detail,
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )


@transfer_routes.route("/transfer", methods=["GET"])
def transfer_page():
    auth_result = _require_transfer_auth(api=False)
    if auth_result is not None:
        return auth_result

    ctx = _get_ctx()
    root = _root()
    try:
        files = list_transfer_files(root)
    except TransferFileError as exc:
        return render_template(
            "transfer.html",
            error=exc.message,
            transfer_root=root,
            files=[],
            summary={"file_count": 0, "total_size": 0},
        ), exc.status_code

    total_size = sum(item.size for item in files)
    return render_template(
        "transfer.html",
        error=None,
        transfer_root=root,
        files=files,
        summary={"file_count": len(files), "total_size": total_size},
        human_size=ctx.human_size,
    )


@transfer_routes.route("/api/transfer/files", methods=["GET"])
def api_list_transfer_files():
    auth_result = _require_transfer_auth(api=True)
    if auth_result is not None:
        return auth_result

    root = _root()
    try:
        files = list_transfer_files(root)
    except TransferFileError as exc:
        return _json_error(exc.message, exc.status_code)

    return jsonify({
        "root": str(root),
        "files": [asdict(item) for item in files],
        "total_size": sum(item.size for item in files),
    })


@transfer_routes.route("/api/transfer/files/<path:filename>", methods=["GET", "PUT", "POST", "DELETE"])
def api_transfer_file(filename: str):
    auth_result = _require_transfer_auth(api=True)
    if auth_result is not None:
        return auth_result

    root = _root()
    try:
        if request.method == "GET":
            target = resolve_transfer_file(root, filename)
            if not target.is_file():
                return _json_error("File not found", 404)
            return send_from_directory(str(root), target.name, as_attachment=True)

        if request.method in ("PUT", "POST"):
            stream = request.environ.get("wsgi.input")
            if stream is None:
                return _json_error("Request body is required", 400)
            result = stream_transfer_upload(root, filename, stream)
            _write_audit(
                "transfer_upload",
                target_id=result.name,
                detail=f"bytes={result.bytes_written} replaced={result.replaced}",
            )
            return jsonify({
                "name": result.name,
                "bytes_written": result.bytes_written,
                "replaced": result.replaced,
                "download_url": f"/api/transfer/files/{quote(result.name)}",
            }), 200 if result.replaced else 201

        deleted = delete_transfer_file(root, filename)
        _write_audit("transfer_delete", target_id=deleted.name)
        return jsonify({"deleted": deleted.name})
    except TransferFileError as exc:
        return _json_error(exc.message, exc.status_code)


def register_transfer_routes(app, ctx: TransferRouteContext):
    global _ctx
    _ctx = ctx
    app.register_blueprint(transfer_routes)

"""
Large transfer file routes.

These endpoints are intentionally limited to a single configured folder and
stream request bodies directly to disk so multi-GB files do not hit the normal
package upload size limit.
"""

from __future__ import annotations

from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable
from urllib.parse import quote, urlencode
from uuid import UUID

from flask import Blueprint, jsonify, redirect, request
from werkzeug.wsgi import get_input_stream

from db_cache import CacheManager
from routes.browser_auth import apply_basic_auth_challenge
from routes.artifact_delivery import deliver_artifact
from routes.request_context import current_request_context
from services.artifact_delivery import ArtifactDeliveryService, ArtifactDownloadEvent
from transfer_files import (
    ANONYMOUS_TRANSFER_OWNER_TYPE,
    TRANSFER_FILE_SCOPE,
    TRANSFER_RESUME_CHUNK_SIZE,
    TRANSFER_RESUME_MAX_CHUNK_SIZE,
    TransferFileError,
    TransferUploadSession,
    append_transfer_upload,
    create_or_resume_transfer_upload,
    delete_transfer_file,
    get_or_create_transfer_share,
    get_transfer_share,
    get_transfer_upload_session,
    get_anonymous_transfer_max_bytes,
    is_anonymous_transfer_upload_enabled,
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
    artifact_delivery: ArtifactDeliveryService


transfer_routes = Blueprint("transfer_routes", __name__)

_ctx: TransferRouteContext | None = None


def _get_ctx() -> TransferRouteContext:
    if _ctx is None:
        raise RuntimeError("Transfer routes not initialized")
    return _ctx


def _actor_type() -> str:
    return current_request_context().actor.actor_type or "system"


def _actor_id() -> str:
    return current_request_context().actor.actor_id or "system"


def _json_error(message: str, status_code: int, **details):
    response = jsonify({"error": message, "status": status_code, **details})
    response.status_code = status_code
    if status_code == 401:
        apply_basic_auth_challenge(response, "ColorVision Transfer")
    return response


def _transfer_auth_error():
    context = current_request_context()
    if context.session_user_authenticated:
        if context.session_must_change_password:
            return _json_error(
                "Password change required",
                403,
                code="password_change_required",
                next="/account?password_change=required",
            )
        return _json_error(
            "Insufficient scope",
            403,
            code="insufficient_scope",
            required=[TRANSFER_FILE_SCOPE],
        )
    return _json_error("Authentication required", 401)


def _current_internal_path() -> str:
    return request.full_path.rstrip("?")


def _require_transfer_auth(*, api: bool):
    ctx = _get_ctx()
    if ctx.check_auth([TRANSFER_FILE_SCOPE]):
        return None
    if api:
        return _transfer_auth_error()
    return redirect(f"/login?{urlencode({'next': _current_internal_path()})}")


def _root() -> Path:
    ctx = _get_ctx()
    return transfer_root(ctx.storage_getter(), ctx.config_getter())


def _write_audit(
    action: str,
    *,
    target_id: str = "",
    detail: str = "",
    actor_type: str | None = None,
    actor_id: str | None = None,
) -> None:
    ctx = _get_ctx()
    ctx.cache.write_audit(
        actor_type=actor_type or _actor_type(),
        actor_id=actor_id or _actor_id(),
        action=action,
        target_type="transfer_file",
        target_id=target_id,
        detail=detail,
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )


def _resumable_upload_owner():
    ctx = _get_ctx()
    if ctx.check_auth([TRANSFER_FILE_SCOPE]):
        return (_actor_type(), _actor_id()), None

    if current_request_context().session_user_authenticated:
        return None, _transfer_auth_error()

    if request.headers.get("Authorization", "").strip():
        return None, _json_error("Authentication required", 401)
    if not is_anonymous_transfer_upload_enabled(ctx.config_getter()):
        return None, _json_error("Authentication required", 401)

    raw_client_id = request.headers.get("X-Transfer-Client", "").strip()
    try:
        client_id = str(UUID(raw_client_id))
    except (ValueError, AttributeError):
        return None, _json_error("X-Transfer-Client must be a valid UUID", 400)
    if raw_client_id.lower() != client_id:
        return None, _json_error("X-Transfer-Client must be a valid UUID", 400)
    return (ANONYMOUS_TRANSFER_OWNER_TYPE, client_id), None


def _upload_session_payload(session: TransferUploadSession) -> dict[str, Any]:
    expires_at = datetime.fromtimestamp(session.expires_at, tz=timezone.utc).isoformat() \
        if session.expires_at > 0 else None
    return {
        "upload_id": session.upload_id,
        "name": session.filename,
        "total_size": session.total_size,
        "offset": session.offset,
        "complete": session.complete,
        "replaced": session.replaced,
        "download_url": session.download_url,
        "share_url": session.share_url,
        "expires_at": expires_at,
        "temporary": session.expires_at > 0,
        "chunk_size": TRANSFER_RESUME_CHUNK_SIZE,
    }


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


@transfer_routes.route("/api/transfer/uploads", methods=["POST"])
def api_create_transfer_upload():
    owner, auth_result = _resumable_upload_owner()
    if auth_result is not None or owner is None:
        return auth_result

    payload = request.get_json(silent=True)
    if not isinstance(payload, dict):
        return _json_error("JSON request body is required", 400)
    if owner[0] == ANONYMOUS_TRANSFER_OWNER_TYPE:
        try:
            declared_size = int(payload.get("total_size", -1))
        except (TypeError, ValueError):
            declared_size = -1
        if declared_size > get_anonymous_transfer_max_bytes(_get_ctx().config_getter()):
            return _json_error("Anonymous upload exceeds the configured file size limit", 413)
    try:
        session = create_or_resume_transfer_upload(
            _root(),
            str(payload.get("filename", "")),
            payload.get("total_size", -1),
            str(payload.get("fingerprint", "")),
            owner_type=owner[0],
            owner_id=owner[1],
        )
        if session.complete and session.total_size == 0:
            _write_audit(
                "transfer_upload",
                target_id=session.filename,
                detail=f"bytes=0 replaced={session.replaced} resumable=true",
                actor_type=owner[0],
                actor_id=owner[1],
            )
        return jsonify(_upload_session_payload(session)), 200 if session.offset > 0 or session.complete else 201
    except TransferFileError as exc:
        return _json_error(exc.message, exc.status_code)


@transfer_routes.route("/api/transfer/uploads/<upload_id>", methods=["GET", "PATCH"])
def api_transfer_upload_session(upload_id: str):
    owner, auth_result = _resumable_upload_owner()
    if auth_result is not None or owner is None:
        return auth_result

    root = _root()
    try:
        if request.method == "GET":
            session = get_transfer_upload_session(
                root,
                upload_id,
                owner_type=owner[0],
                owner_id=owner[1],
            )
            return jsonify(_upload_session_payload(session))

        raw_offset = request.headers.get("Upload-Offset", "")
        try:
            offset = int(raw_offset)
        except (TypeError, ValueError):
            return _json_error("Upload-Offset header is required", 400)
        if request.content_length is not None and request.content_length > TRANSFER_RESUME_MAX_CHUNK_SIZE:
            return _json_error("Upload chunk is too large", 413)
        result = append_transfer_upload(
            root,
            upload_id,
            offset,
            get_input_stream(request.environ),
            owner_type=owner[0],
            owner_id=owner[1],
        )
        if result.newly_completed:
            session = result.session
            _write_audit(
                "transfer_upload",
                target_id=session.filename,
                detail=f"bytes={session.total_size} replaced={session.replaced} resumable=true",
                actor_type=owner[0],
                actor_id=owner[1],
            )
        return jsonify(_upload_session_payload(result.session))
    except TransferFileError as exc:
        return _json_error(exc.message, exc.status_code)


@transfer_routes.route("/api/transfer/shares/<token>", methods=["GET"])
def api_transfer_share(token: str):
    try:
        return jsonify(asdict(get_transfer_share(_root(), token)))
    except TransferFileError as exc:
        return _json_error(exc.message, exc.status_code)


@transfer_routes.route("/api/transfer/shares/<token>/download", methods=["GET"])
def api_download_transfer_share(token: str):
    try:
        share = get_transfer_share(_root(), token)
        target = resolve_transfer_file(_root(), share.name)
        return deliver_artifact(
            _get_ctx().artifact_delivery,
            target,
            request_method=request.method,
            event=ArtifactDownloadEvent(
                artifact_type="transfer_share",
                artifact_id=share.token,
                relative_path=f"Transfer/{share.name}",
            ),
        )
    except TransferFileError as exc:
        return _json_error(exc.message, exc.status_code)


@transfer_routes.route("/api/transfer/files/<path:filename>", methods=["GET", "PUT", "POST", "DELETE"])
def api_transfer_file(filename: str):
    auth_result = _require_transfer_auth(api=True)
    if auth_result is not None:
        return auth_result

    root = _root()
    try:
        if request.method in {"GET", "HEAD"}:
            target = resolve_transfer_file(root, filename)
            if not target.is_file():
                return _json_error("File not found", 404)
            return deliver_artifact(
                _get_ctx().artifact_delivery,
                target,
                request_method=request.method,
                event=ArtifactDownloadEvent(
                    artifact_type="transfer",
                    artifact_id=target.name,
                    relative_path=f"Transfer/{target.name}",
                ),
            )

        if request.method in ("PUT", "POST"):
            # Stop at Content-Length on keep-alive connections without applying the
            # global upload-size limit that the transfer endpoint intentionally bypasses.
            stream = get_input_stream(request.environ)
            result = stream_transfer_upload(root, filename, stream)
            share = get_or_create_transfer_share(root, result.name)
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
                "share_url": share.share_url,
                "expires_at": None,
                "temporary": False,
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

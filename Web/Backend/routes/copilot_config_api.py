"""Admin CRUD and scoped desktop sync endpoints for Copilot profiles."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Callable

from flask import Blueprint, jsonify, request

from db_cache import CacheManager
from services.api_key_service import verify_api_key
from services.copilot_config_service import CopilotConfigService


@dataclass(frozen=True)
class CopilotConfigApiContext:
    cache: CacheManager
    config_getter: Callable[[], dict[str, Any]]


copilot_admin_api = Blueprint(
    "copilot_admin_api",
    __name__,
    url_prefix="/api/admin/copilot",
)
copilot_client_api = Blueprint(
    "copilot_client_api",
    __name__,
    url_prefix="/api/copilot",
)

_ctx: CopilotConfigApiContext | None = None


def register_copilot_config_api_routes(app, ctx: CopilotConfigApiContext):
    global _ctx
    _ctx = ctx
    app.register_blueprint(copilot_admin_api)
    app.register_blueprint(copilot_client_api)


def _service() -> CopilotConfigService:
    if _ctx is None:
        raise RuntimeError("Copilot configuration API not initialized")
    return CopilotConfigService(
        _ctx.cache,
        lambda: str(_ctx.config_getter().get("secret_key", "")),
    )


def _audit(action: str, profile_id: str = "", detail: str = ""):
    if _ctx is None:
        return
    from routes.admin_api import _actor_id, _actor_type

    _ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action=action,
        target_type="copilot_profile",
        target_id=profile_id,
        detail=detail,
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )


@copilot_admin_api.route("/profiles", methods=["GET"])
def list_profiles():
    response = jsonify(_service().list_admin_profiles())
    response.headers["Cache-Control"] = "no-store"
    return response


@copilot_admin_api.route("/profiles", methods=["POST"])
def create_profile():
    try:
        result = _service().create_profile(request.get_json(silent=True) or {})
    except ValueError as exc:
        return jsonify({"error": str(exc), "status": 400}), 400
    _audit("copilot_profile_create", result["id"], f"Created '{result['name']}'")
    response = jsonify(result)
    response.status_code = 201
    response.headers["Cache-Control"] = "no-store"
    return response


@copilot_admin_api.route("/profiles/<profile_id>", methods=["PUT"])
def update_profile(profile_id: str):
    try:
        result = _service().update_profile(
            profile_id,
            request.get_json(silent=True) or {},
        )
    except ValueError as exc:
        return jsonify({"error": str(exc), "status": 400}), 400
    if result is None:
        return jsonify({"error": "Copilot profile not found", "status": 404}), 404
    _audit("copilot_profile_update", result["id"], f"Updated '{result['name']}'")
    response = jsonify(result)
    response.headers["Cache-Control"] = "no-store"
    return response


@copilot_admin_api.route("/profiles/<profile_id>", methods=["DELETE"])
def delete_profile(profile_id: str):
    try:
        deleted = _service().delete_profile(profile_id)
    except ValueError as exc:
        return jsonify({"error": str(exc), "status": 400}), 400
    if not deleted:
        return jsonify({"error": "Copilot profile not found", "status": 404}), 404
    _audit("copilot_profile_delete", profile_id)
    return jsonify({"status": "deleted", "id": profile_id})


def _require_sync_key():
    if _ctx is None:
        raise RuntimeError("Copilot configuration API not initialized")
    auth_header = request.headers.get("Authorization", "")
    if not auth_header.startswith("Bearer "):
        return None, (jsonify({"error": "Bearer API key required", "status": 401}), 401)

    token = auth_header[7:].strip()
    key_info = verify_api_key(
        _ctx.cache,
        token,
        required_scopes=["copilot:config:read"],
    )
    if key_info:
        return key_info, None

    valid_key = verify_api_key(_ctx.cache, token, required_scopes=None)
    if valid_key:
        return None, (
            jsonify({
                "error": "Insufficient scope",
                "required": ["copilot:config:read"],
                "status": 403,
            }),
            403,
        )
    return None, (jsonify({"error": "Invalid or expired API key", "status": 401}), 401)


@copilot_client_api.route("/config", methods=["GET"])
def get_synced_config():
    key_info, error = _require_sync_key()
    if error is not None:
        return error

    result = _service().list_client_profiles()
    if _ctx is not None:
        _ctx.cache.write_audit(
            actor_type="api_key",
            actor_id=f"key:{key_info['key_prefix']}",
            action="copilot_config_sync",
            target_type="copilot_profile",
            detail=f"Returned {len(result['profiles'])} enabled profile(s), revision {result['revision']}",
            ip=request.remote_addr or "",
            user_agent=request.headers.get("User-Agent", "")[:200],
        )
    response = jsonify(result)
    response.headers["Cache-Control"] = "no-store"
    response.headers["Pragma"] = "no-cache"
    return response

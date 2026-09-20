"""
Public REST API routes — supplementary endpoints.

Routes in marketplace_api_routes.py handle /api/plugins/* and /api/packages/*.
This module handles /api/stats, /api/feedback, and legacy file serving.
"""

from __future__ import annotations

from flask import Blueprint, abort, current_app, jsonify, request, send_file, session

from context import MarketplaceContext
from routes.artifact_delivery import deliver_artifact
from services.artifact_delivery import ArtifactDownloadEvent


public_api = Blueprint("public_api", __name__)

_ctx: MarketplaceContext | None = None


def _get_ctx() -> MarketplaceContext:
    if _ctx is None:
        raise RuntimeError("Public API not initialized")
    return _ctx


def register_public_api(app, ctx: MarketplaceContext):
    global _ctx
    _ctx = ctx
    app.register_blueprint(public_api)


def _verified_feedback_owner() -> tuple[int | None, str]:
    """Return only a database-backed account identity verified for this request."""
    ctx = _get_ctx()
    request_context = ctx.request_context_factory()
    decision = ctx.auth_policy.authorize(
        request_context,
        [],
        allow_user_session=True,
        allow_basic=False,
        allow_bearer=False,
    )
    if not decision.allowed or decision.principal.auth_method != "session":
        return None, ""
    try:
        user_id = int(session.get("user_id"))
    except (TypeError, ValueError):
        return None, ""
    if user_id <= 0 or not request_context.session_user_authenticated:
        return None, ""
    return user_id, decision.principal.actor_id


def _feedback_reader_access():
    """Authenticate a feedback reader and calculate its filesystem view."""
    ctx = _get_ctx()
    request_context = ctx.request_context_factory()
    decision = ctx.auth_policy.authorize(
        request_context,
        [],
        allow_user_session=True,
    )
    if not decision.allowed:
        if decision.reason == "password_change_required":
            return None, jsonify({
                "error": "Password change required",
                "code": "password_change_required",
                "next": "/account?password_change=required",
                "status": 403,
            }), 403
        return None, jsonify({"error": "Authentication required", "status": 401}), 401

    principal = decision.principal
    from routes.request_context import set_authenticated_request_context
    set_authenticated_request_context(request_context.with_actor(principal))
    if principal.auth_method == "session":
        if principal.is_admin:
            return {"owner_user_id": None, "scope": "all", "can_manage": True}, None, None
        try:
            user_id = int(session.get("user_id"))
        except (TypeError, ValueError):
            user_id = 0
        if user_id <= 0:
            return None, jsonify({"error": "Database account required", "status": 403}), 403
        if principal.role == "developer" and (
            "feedback:read" in principal.scopes or "feedback:manage" in principal.scopes
        ):
            return {
                "owner_user_id": None,
                "scope": "all",
                "can_manage": "feedback:manage" in principal.scopes,
            }, None, None
        return {"owner_user_id": user_id, "scope": "own", "can_manage": False}, None, None

    if principal.auth_method == "basic" or principal.is_admin:
        return {"owner_user_id": None, "scope": "all", "can_manage": True}, None, None
    if principal.auth_method == "bearer" and (
        "feedback:read" in principal.scopes or "feedback:manage" in principal.scopes
    ):
        return {
            "owner_user_id": None,
            "scope": "all",
            "can_manage": "feedback:manage" in principal.scopes or "admin:*" in principal.scopes,
        }, None, None
    return None, jsonify({
        "error": "Insufficient scope",
        "code": "insufficient_scope",
        "required": ["feedback:read"],
        "status": 403,
    }), 403


@public_api.route("/api/stats", methods=["GET"])
def api_stats():
    ctx = _get_ctx()
    from download_stats import build_stats_payload
    return jsonify(build_stats_payload(ctx.get_db))


@public_api.route("/api/feedback", methods=["POST"])
def api_feedback():
    ctx = _get_ctx()
    from feedback_service import FeedbackValidationError, save_feedback
    from config_loader import MAX_FEEDBACK_FIELD_LENGTH, MAX_FEEDBACK_FILES
    from download_stats import hash_ip
    if ctx.request_context_factory().session_must_change_password:
        return jsonify({
            "error": "Password change required",
            "code": "password_change_required",
            "next": "/account?password_change=required",
            "status": 403,
        }), 403
    try:
        owner_user_id, owner_username = _verified_feedback_owner()
        result = save_feedback(
            ctx.storage, form=request.form, files=request.files,
            remote_addr=request.remote_addr,
            max_feedback_files=MAX_FEEDBACK_FILES,
            max_feedback_field_length=MAX_FEEDBACK_FIELD_LENGTH,
            sanitize_filename=ctx.sanitize_filename,
            hash_ip=hash_ip,
            owner_user_id=owner_user_id,
            owner_username=owner_username,
        )
    except FeedbackValidationError as exc:
        return jsonify({"error": exc.message}), 400
    return jsonify({"feedbackId": result.feedback_id, "message": "Feedback received"}), 201


@public_api.route("/api/feedback", methods=["GET"])
def api_feedback_inbox():
    from services.feedback_admin import query_feedback

    access, error_response, status_code = _feedback_reader_access()
    if error_response is not None:
        return error_response, status_code
    try:
        limit = int(request.args.get("limit", 20))
        offset = int(request.args.get("offset", 0))
        result = query_feedback(
            _get_ctx().storage,
            status=request.args.get("status", "").strip() or None,
            query=request.args.get("query", "").strip() or None,
            limit=limit,
            offset=offset,
            owner_user_id=access["owner_user_id"],
            machine=request.args.get("machine", "").strip() or None,
            app_version=request.args.get("app_version", "").strip() or None,
            created_from=request.args.get("created_from", "").strip() or None,
            created_to=request.args.get("created_to", "").strip() or None,
        )
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400
    result["access"] = {"scope": access["scope"], "can_manage": access["can_manage"]}
    return jsonify(result)


@public_api.route("/api/feedback/<feedback_id>", methods=["GET"])
def api_feedback_detail(feedback_id: str):
    from services.feedback_admin import get_feedback_detail

    access, error_response, status_code = _feedback_reader_access()
    if error_response is not None:
        return error_response, status_code
    try:
        result = get_feedback_detail(
            _get_ctx().storage,
            feedback_id,
            owner_user_id=access["owner_user_id"],
            include_hashes=request.args.get("include_hashes") != "false",
        )
    except FileNotFoundError:
        return jsonify({"error": "Feedback not found"}), 404
    result["access"] = {"scope": access["scope"], "can_manage": access["can_manage"]}
    return jsonify(result)


@public_api.route("/api/feedback/<feedback_id>/attachments/<path:filename>", methods=["GET"])
def api_feedback_attachment(feedback_id: str, filename: str):
    from services.feedback_admin import resolve_feedback_attachment

    access, error_response, status_code = _feedback_reader_access()
    if error_response is not None:
        return error_response, status_code
    ctx = _get_ctx()
    try:
        target = resolve_feedback_attachment(
            ctx.storage,
            feedback_id,
            filename,
            owner_user_id=access["owner_user_id"],
        )
    except FileNotFoundError:
        return jsonify({"error": "Attachment not found"}), 404
    principal = ctx.request_context_factory().actor
    ctx.cache.write_audit(
        actor_type=principal.actor_type,
        actor_id=principal.actor_id,
        action="feedback_attachment_download",
        target_type="feedback",
        target_id=feedback_id,
        detail=f"attachment={target.name}",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return send_file(
        target,
        as_attachment=True,
        download_name=f"{feedback_id}__{target.name}",
    )


@public_api.route("/api/v1/analytics/events", methods=["POST"])
def api_web_experience_event():
    """Queue one aggregate-only SPA page-view or Web Vital event."""
    ctx = _get_ctx()
    if request.content_length is not None and request.content_length > 4096:
        return jsonify({"error": "Analytics payload too large"}), 413

    from services.access_analytics import (
        build_web_experience_event,
        reporting_utc_offset_minutes,
    )

    try:
        config = ctx.active_config
        event = build_web_experience_event(
            request.get_json(silent=True),
            secret_key=str(config.get("secret_key", "")),
            remote_addr=request.remote_addr,
            user_agent=request.headers.get("User-Agent", ""),
            utc_offset_minutes=reporting_utc_offset_minutes(config),
        )
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400

    if event is None:
        return jsonify({"accepted": True, "recorded": False}), 202
    accepted = ctx.access_recorder.submit(
        event,
        db_path=ctx.active_db_path,
        synchronous=bool(current_app.config.get("TESTING")),
    )
    if not accepted:
        return jsonify({"error": "Analytics recorder is busy"}), 503
    return jsonify({"accepted": True, "recorded": True}), 202


@public_api.route("/D%3A/ColorVision/Plugins/<path:filepath>", methods=["GET"])
def legacy_plugin_files(filepath):
    ctx = _get_ctx()
    target = ctx.storage / "Plugins" / filepath
    try:
        target.resolve().relative_to(ctx.storage.resolve())
    except ValueError:
        abort(403)
    if not target.exists():
        abort(404)
    if target.is_file():
        return deliver_artifact(
            ctx.artifact_delivery,
            target,
            request_method=request.method,
            event=ArtifactDownloadEvent(
                artifact_type="plugin",
                artifact_id=filepath,
                relative_path=f"Plugins/{filepath}",
            ),
            as_attachment=False,
        )
    abort(404)


@public_api.route("/D%3A/ColorVision/<path:filepath>", methods=["GET"])
def legacy_files(filepath):
    ctx = _get_ctx()
    from services.public_storage import is_public_storage_path
    from update_retention import repair_update_storage_layout
    normalized = ctx.normalize_relative_path(filepath)
    if not is_public_storage_path(normalized):
        abort(404)
    full_path = ctx.storage_target(normalized)
    if normalized.startswith("Update/") and not full_path.exists():
        repair_update_storage_layout(ctx.storage)
        full_path = ctx.storage_target(normalized)
    try:
        full_path.resolve().relative_to(ctx.storage.resolve())
    except ValueError:
        abort(403)
    if not full_path.exists():
        abort(404)
    if full_path.is_file():
        return deliver_artifact(
            ctx.artifact_delivery,
            full_path,
            request_method=request.method,
            event=ArtifactDownloadEvent(
                artifact_type="storage",
                artifact_id=normalized,
                relative_path=normalized,
            ),
            as_attachment=False,
        )
    abort(404)

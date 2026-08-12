"""
Public REST API routes — supplementary endpoints.

Routes in marketplace_api_routes.py handle /api/plugins/* and /api/packages/*.
This module handles /api/stats, /api/feedback, and legacy file serving.
"""

from __future__ import annotations

from flask import Blueprint, abort, current_app, jsonify, request

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
    try:
        result = save_feedback(
            ctx.storage, form=request.form, files=request.files,
            remote_addr=request.remote_addr,
            max_feedback_files=MAX_FEEDBACK_FILES,
            max_feedback_field_length=MAX_FEEDBACK_FIELD_LENGTH,
            sanitize_filename=ctx.sanitize_filename,
            hash_ip=hash_ip,
        )
    except FeedbackValidationError as exc:
        return jsonify({"error": exc.message}), 400
    return jsonify({"feedbackId": result.feedback_id, "message": "Feedback received"}), 201


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

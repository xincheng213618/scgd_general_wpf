"""
Public REST API routes — supplementary endpoints.

Routes in marketplace_api_routes.py handle /api/plugins/* and /api/packages/*.
This module handles /api/stats, /api/feedback, and legacy file serving.
"""

from __future__ import annotations

from flask import Blueprint, abort, jsonify, request, send_from_directory

from context import MarketplaceContext


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
        return send_from_directory(str(target.parent), target.name)
    abort(404)


@public_api.route("/D%3A/ColorVision/<path:filepath>", methods=["GET"])
def legacy_files(filepath):
    ctx = _get_ctx()
    from update_retention import repair_update_storage_layout
    full_path = ctx.storage / filepath
    if filepath.replace("\\", "/").startswith("Update/") and not full_path.exists():
        repair_update_storage_layout(ctx.storage)
        full_path = ctx.storage / filepath
    try:
        full_path.resolve().relative_to(ctx.storage.resolve())
    except ValueError:
        abort(403)
    if not full_path.exists():
        abort(404)
    if full_path.is_file():
        return send_from_directory(str(full_path.parent), full_path.name)
    abort(404)

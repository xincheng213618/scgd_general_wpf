"""
Health and readiness API routes for ColorVision Marketplace.
"""

from __future__ import annotations

from flask import Blueprint, jsonify

health_api = Blueprint("health_api", __name__)

_ctx = None


def register_health_api(app, ctx):
    global _ctx
    _ctx = ctx
    app.register_blueprint(health_api)


@health_api.route("/api/health", methods=["GET"])
def api_health():
    from runtime_health import build_health_payload
    return jsonify(build_health_payload(
        storage=_ctx.storage, db_path=_ctx.active_db_path, config=_ctx.active_config,
    ))


@health_api.route("/api/ready", methods=["GET"])
def api_ready():
    from runtime_health import build_ready_payload
    payload = build_ready_payload(
        storage=_ctx.storage, db_path=_ctx.active_db_path, config=_ctx.active_config,
        get_db=_ctx.get_db, get_upload_auth=_ctx.get_upload_auth,
        cache_manager=_ctx.cache,
    )
    return jsonify(payload), (200 if payload["ready"] else 503)

"""
Health and readiness API routes for ColorVision Marketplace.
"""

from __future__ import annotations

from flask import Blueprint, jsonify

health_api = Blueprint("health_api", __name__)

_app_mod = None


def register_health_api(app, ctx):
    global _app_mod
    _app_mod = __import__("app")
    app.register_blueprint(health_api)


@health_api.route("/api/health", methods=["GET"])
def api_health():
    from runtime_health import build_health_payload
    return jsonify(build_health_payload(
        storage=_app_mod.STORAGE, db_path=_app_mod.DB_PATH, config=_app_mod.CONFIG,
    ))


@health_api.route("/api/ready", methods=["GET"])
def api_ready():
    from runtime_health import build_ready_payload
    payload = build_ready_payload(
        storage=_app_mod.STORAGE, db_path=_app_mod.DB_PATH, config=_app_mod.CONFIG,
        get_db=_app_mod.get_db, get_upload_auth=_app_mod._get_upload_auth,
        cache_manager=_app_mod._cache,
    )
    return jsonify(payload), (200 if payload["ready"] else 503)

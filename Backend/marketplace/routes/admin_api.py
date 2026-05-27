"""
Admin API routes for ColorVision Marketplace.

Provides management endpoints for cache, index, jobs, audit log, and stats.
All endpoints require authentication (session, Basic Auth, or Bearer API Key).

Per-endpoint scope requirements:
  - GET  /cache/status        → cache:read
  - POST /cache/cleanup       → cache:refresh
  - POST /index/plugins/*     → cache:refresh
  - GET  /jobs                → jobs:read
  - POST /jobs/*/run          → jobs:write
  - POST /jobs/*/enable       → jobs:write
  - POST /jobs/*/disable      → jobs:write
  - GET  /audit-log           → admin:*
  - GET  /stats/overview      → stats:read
  - GET  /api-keys            → admin:*
  - POST /api-keys            → admin:*
  - POST /api-keys/*/revoke   → admin:*
  - POST /api-keys/*/rotate   → admin:*
  - GET  /api-keys/*/usage    → admin:*

admin:* grants access to all endpoints.
Session/Basic Auth always has full access.
"""

from __future__ import annotations

import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

from flask import Blueprint, jsonify, request

from db_cache import CacheManager, now_iso


# Per-endpoint scope requirements
ENDPOINT_SCOPES: dict[str, list[str]] = {
    "cache_status": ["cache:read"],
    "cache_cleanup": ["cache:refresh"],
    "refresh_all_plugins": ["cache:refresh"],
    "refresh_single_plugin": ["cache:refresh"],
    "list_jobs": ["jobs:read"],
    "run_job": ["jobs:write"],
    "enable_job": ["jobs:write"],
    "disable_job": ["jobs:write"],
    "audit_log": ["admin:*"],
    "stats_overview": ["stats:read"],
    "list_api_keys": ["admin:*"],
    "create_api_key": ["admin:*"],
    "revoke_api_key": ["admin:*"],
    "rotate_api_key": ["admin:*"],
    "api_key_usage": ["admin:*"],
}


@dataclass(frozen=True)
class AdminApiContext:
    cache: CacheManager
    storage_getter: Callable[[], Path]
    config_getter: Callable[[], dict[str, Any]]
    get_db: Callable[[], Any]
    check_auth: Callable[[list[str] | None], bool]
    require_auth_decorator: Any
    refresh_plugin_index: Callable[..., Any]
    refresh_all_plugin_index: Callable[..., Any]
    get_plugin_index_state: Callable[..., Any]
    is_plugin_index_populated: Callable[..., bool]
    get_plugin_catalog_from_index: Callable[..., Any]
    human_size: Callable[[int], str]


admin_api = Blueprint("admin_api", __name__, url_prefix="/api/admin")

_ctx: AdminApiContext | None = None


def _get_ctx() -> AdminApiContext:
    if _ctx is None:
        raise RuntimeError("Admin API not initialized")
    return _ctx


def _require_admin_auth(required_scopes: list[str] | None = None):
    """Check authentication for admin endpoints with optional scope check."""
    ctx = _get_ctx()
    if not ctx.check_auth(required_scopes):
        return jsonify({"error": "Authentication required", "status": 401}), 401
    return None


def register_admin_api_routes(app, ctx: AdminApiContext):
    global _ctx
    _ctx = ctx
    app.register_blueprint(admin_api)

    @app.before_request
    def _check_admin_auth():
        """Require auth for all /api/admin requests with per-endpoint scopes."""
        if request.path.startswith("/api/admin/"):
            # Determine required scopes from the matched endpoint
            endpoint = request.endpoint or ""
            func_name = endpoint.split(".")[-1] if "." in endpoint else endpoint
            required = ENDPOINT_SCOPES.get(func_name)
            result = _require_admin_auth(required)
            if result is not None:
                return result


# ---------------------------------------------------------------------------
# Cache management
# ---------------------------------------------------------------------------

@admin_api.route("/cache/status", methods=["GET"])
def cache_status():
    ctx = _get_ctx()
    status = ctx.cache.get_db_status()
    storage = ctx.storage_getter()

    status["storage_path"] = str(storage)
    status["plugins_dir_exists"] = (storage / "Plugins").is_dir()

    # Check if plugin catalog cache exists
    cached = ctx.cache.get_cache_entry("plugin_catalog:v1")
    status["plugin_catalog_cached"] = cached is not None
    if cached:
        try:
            status["plugin_catalog_item_count"] = len(cached.get("value", []))
        except Exception:
            status["plugin_catalog_item_count"] = 0

    return jsonify(status)


@admin_api.route("/cache/cleanup", methods=["POST"])
def cache_cleanup():
    ctx = _get_ctx()
    deleted = ctx.cache.cleanup_expired_cache()

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="cache_cleanup",
        target_type="cache_entry",
        detail=f"Deleted {deleted} expired entries",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )

    return jsonify({"deleted_count": deleted})


# ---------------------------------------------------------------------------
# Plugin index management
# ---------------------------------------------------------------------------

@admin_api.route("/index/plugins/refresh", methods=["POST"])
def refresh_all_plugins():
    ctx = _get_ctx()
    started = time.monotonic()

    download_counts: dict[str, int] = {}
    try:
        from download_stats import get_download_counts
        download_counts = get_download_counts(ctx.get_db)
    except Exception:
        pass

    result = ctx.refresh_all_plugin_index(
        ctx.cache,
        ctx.storage_getter(),
        download_counts=download_counts,
    )

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="index_refresh_all",
        target_type="plugin_index",
        detail=f"indexed={result['indexed_count']} deleted={result['deleted_count']} errors={len(result['errors'])}",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )

    return jsonify(result)


@admin_api.route("/index/plugins/<plugin_id>/refresh", methods=["POST"])
def refresh_single_plugin(plugin_id: str):
    ctx = _get_ctx()

    from storage_paths import is_safe_id
    if not is_safe_id(plugin_id):
        return jsonify({"error": "Invalid plugin_id"}), 400

    started = time.monotonic()

    download_counts: dict[str, int] = {}
    try:
        from download_stats import get_download_counts
        download_counts = get_download_counts(ctx.get_db)
    except Exception:
        pass

    result = ctx.refresh_plugin_index(
        ctx.cache,
        ctx.storage_getter(),
        plugin_id,
        download_counts=download_counts,
    )

    elapsed_ms = int((time.monotonic() - started) * 1000)

    if result is None:
        ctx.cache.write_audit(
            actor_type=_actor_type(),
            actor_id=_actor_id(),
            action="index_refresh_plugin",
            target_type="plugin_index",
            target_id=plugin_id,
            detail="Plugin not found, marked deleted",
            ip=request.remote_addr or "",
            user_agent=request.headers.get("User-Agent", "")[:200],
        )
        return jsonify({
            "pluginId": plugin_id,
            "status": "not_found",
            "durationMs": elapsed_ms,
        })

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="index_refresh_plugin",
        target_type="plugin_index",
        target_id=plugin_id,
        detail=f"Refreshed in {elapsed_ms}ms",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )

    return jsonify({
        "pluginId": plugin_id,
        "status": "ok",
        "durationMs": elapsed_ms,
    })


# ---------------------------------------------------------------------------
# Jobs management
# ---------------------------------------------------------------------------

@admin_api.route("/jobs", methods=["GET"])
def list_jobs():
    ctx = _get_ctx()
    db = ctx.get_db()
    try:
        rows = db.execute("SELECT * FROM scheduled_jobs ORDER BY id").fetchall()
        jobs = [dict(r) for r in rows]

        # Attach latest run info
        for job in jobs:
            run = db.execute(
                "SELECT * FROM job_runs WHERE job_id = ? ORDER BY id DESC LIMIT 1",
                (job["id"],),
            ).fetchone()
            job["latest_run"] = dict(run) if run else None

        return jsonify(jobs)
    except Exception as exc:
        return jsonify({"error": str(exc)}), 500
    finally:
        db.close()


@admin_api.route("/jobs/<job_id>/run", methods=["POST"])
def run_job(job_id: str):
    ctx = _get_ctx()
    db = ctx.get_db()
    try:
        row = db.execute("SELECT * FROM scheduled_jobs WHERE id = ?", (job_id,)).fetchone()
        if not row:
            return jsonify({"error": "Job not found"}), 404
    finally:
        db.close()

    from services.scheduler import run_job_now
    result = run_job_now(ctx.cache, ctx.storage_getter(), ctx.config_getter, ctx.get_db, job_id)

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="job_run",
        target_type="scheduled_job",
        target_id=job_id,
        detail=f"Manual run: {result.get('status', 'unknown')}",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )

    return jsonify(result)


@admin_api.route("/jobs/<job_id>/enable", methods=["POST"])
def enable_job(job_id: str):
    ctx = _get_ctx()
    db = ctx.get_db()
    try:
        cursor = db.execute(
            "UPDATE scheduled_jobs SET enabled = 1, updated_at = ? WHERE id = ?",
            (now_iso(), job_id),
        )
        if cursor.rowcount == 0:
            return jsonify({"error": "Job not found"}), 404
        db.commit()
    finally:
        db.close()

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="job_enable",
        target_type="scheduled_job",
        target_id=job_id,
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )

    return jsonify({"status": "enabled", "job_id": job_id})


@admin_api.route("/jobs/<job_id>/disable", methods=["POST"])
def disable_job(job_id: str):
    ctx = _get_ctx()
    db = ctx.get_db()
    try:
        cursor = db.execute(
            "UPDATE scheduled_jobs SET enabled = 0, updated_at = ? WHERE id = ?",
            (now_iso(), job_id),
        )
        if cursor.rowcount == 0:
            return jsonify({"error": "Job not found"}), 404
        db.commit()
    finally:
        db.close()

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="job_disable",
        target_type="scheduled_job",
        target_id=job_id,
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )

    return jsonify({"status": "disabled", "job_id": job_id})


# ---------------------------------------------------------------------------
# Audit log
# ---------------------------------------------------------------------------

@admin_api.route("/audit-log", methods=["GET"])
def audit_log():
    ctx = _get_ctx()
    action = request.args.get("action", "").strip() or None
    limit = min(int(request.args.get("limit", 100)), 500)
    offset = int(request.args.get("offset", 0))

    entries = ctx.cache.get_audit_log(action=action, limit=limit, offset=offset)
    return jsonify({"entries": entries, "limit": limit, "offset": offset})


# ---------------------------------------------------------------------------
# Stats overview
# ---------------------------------------------------------------------------

@admin_api.route("/stats/overview", methods=["GET"])
def stats_overview():
    ctx = _get_ctx()
    db = ctx.get_db()
    try:
        stats: dict[str, Any] = {}

        row = db.execute("SELECT COUNT(*) AS cnt FROM download_log").fetchone()
        stats["totalDownloads"] = row["cnt"] if row else 0

        row = db.execute(
            "SELECT COUNT(*) AS cnt FROM download_log WHERE downloaded_at >= date('now')"
        ).fetchone()
        stats["downloadsToday"] = row["cnt"] if row else 0

        row = db.execute("SELECT COUNT(*) AS cnt FROM plugin_index WHERE is_deleted = 0").fetchone()
        stats["pluginCount"] = row["cnt"] if row else 0

        row = db.execute("SELECT COUNT(*) AS cnt FROM package_index WHERE is_deleted = 0").fetchone()
        stats["packageCount"] = row["cnt"] if row else 0

        # Latest release version
        storage = ctx.storage_getter()
        try:
            latest = (storage / "LATEST_RELEASE").read_text(encoding="utf-8").strip()
        except (OSError, UnicodeDecodeError):
            latest = ""
        stats["latestReleaseVersion"] = latest

        # Cache hit status
        cached = ctx.cache.get_cache_entry("plugin_catalog:v1")
        stats["pluginCatalogCached"] = cached is not None

        # DB size
        try:
            stats["dbSizeBytes"] = ctx.cache._db_path.stat().st_size
        except OSError:
            stats["dbSizeBytes"] = 0

        return jsonify(stats)
    except Exception as exc:
        return jsonify({"error": str(exc)}), 500
    finally:
        db.close()


# ---------------------------------------------------------------------------
# API Key management
# ---------------------------------------------------------------------------

@admin_api.route("/api-keys", methods=["GET"])
def list_api_keys():
    from services.api_key_service import list_api_keys as _list_keys
    ctx = _get_ctx()
    keys = _list_keys(ctx.cache)
    return jsonify(keys)


@admin_api.route("/api-keys", methods=["POST"])
def create_api_key():
    from services.api_key_service import create_api_key as _create_key
    ctx = _get_ctx()
    data = request.get_json(silent=True) or {}
    name = (data.get("name") or "").strip()
    if not name:
        return jsonify({"error": "name is required"}), 400

    scopes = data.get("scopes", "")
    if isinstance(scopes, list):
        scopes = ",".join(scopes)
    expires_at = data.get("expires_at")

    result = _create_key(
        ctx.cache,
        name=name,
        scopes=scopes,
        created_by=_actor_id(),
        expires_at=expires_at,
    )

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="api_key_create",
        target_type="api_key",
        target_id=str(result["id"]),
        detail=f"Created key '{name}' with prefix '{result['key_prefix']}'",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )

    return jsonify(result), 201


@admin_api.route("/api-keys/<int:key_id>/revoke", methods=["POST"])
def revoke_api_key(key_id: int):
    from services.api_key_service import revoke_api_key as _revoke_key
    ctx = _get_ctx()
    success = _revoke_key(ctx.cache, key_id)
    if not success:
        return jsonify({"error": "Key not found or already revoked"}), 404

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="api_key_revoke",
        target_type="api_key",
        target_id=str(key_id),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )

    return jsonify({"status": "revoked", "id": key_id})


@admin_api.route("/api-keys/<int:key_id>/rotate", methods=["POST"])
def rotate_api_key(key_id: int):
    from services.api_key_service import rotate_api_key as _rotate_key
    ctx = _get_ctx()
    result = _rotate_key(ctx.cache, key_id, created_by=_actor_id())
    if not result:
        return jsonify({"error": "Key not found"}), 404

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="api_key_rotate",
        target_type="api_key",
        target_id=str(key_id),
        detail=f"Rotated to new key with prefix '{result['key_prefix']}'",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )

    return jsonify(result), 201


@admin_api.route("/api-keys/<int:key_id>/usage", methods=["GET"])
def api_key_usage(key_id: int):
    from services.api_key_service import get_api_key_usage as _get_usage
    ctx = _get_ctx()
    usage = _get_usage(ctx.cache, key_id)
    if not usage:
        return jsonify({"error": "Key not found"}), 404
    return jsonify(usage)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _actor_type() -> str:
    from flask import session
    if session.get("authenticated"):
        return "user"
    auth = request.authorization
    if auth and auth.type and auth.type.lower() == "basic":
        return "user"
    return "system"


def _actor_id() -> str:
    from flask import session
    if session.get("username"):
        return session["username"]
    auth = request.authorization
    if auth and auth.username:
        return auth.username
    return "system"

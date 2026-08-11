"""
Admin API routes for ColorVision Marketplace.

Provides management endpoints for cache, index, jobs, audit log, and stats.
All endpoints require authentication (session, Basic Auth, or Bearer API Key).

Per-endpoint scope requirements:
  - GET  /cache/status        → cache:read
  - POST /cache/cleanup       → cache:refresh
  - POST /index/plugins/*     → cache:refresh
  - POST /index/docs/refresh  → cache:refresh
  - GET  /jobs                → jobs:read
  - POST /jobs/*/run          → jobs:write
  - POST /jobs/*/enable       → jobs:write
  - POST /jobs/*/disable      → jobs:write
  - GET  /audit-log           → admin:*
  - GET  /stats/overview      → stats:read
  - GET  /docs/status         → cache:read
  - GET  /publish/integrity   → stats:read
  - GET  /api-keys            → admin:*
  - POST /api-keys            → admin:*
  - POST /api-keys/*/revoke   → admin:*
  - POST /api-keys/*/rotate   → admin:*
  - GET  /api-keys/*/usage    → admin:*
  - *    /copilot/profiles    → admin:*

admin:* grants access to all endpoints.
Session/Basic Auth always has full access.
Transfer file endpoints use file:transfer.
"""

from __future__ import annotations

import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

from flask import Blueprint, jsonify, request

from db_cache import CacheManager, now_iso
from ports.jobs import JobRepository
from routes.request_context import current_request_context, set_authenticated_request_context
from services.auth_policy import AuthPolicy
from services.request_context import RequestContext


# Per-endpoint scope requirements
ENDPOINT_SCOPES: dict[str, list[str]] = {
    "cache_status": ["cache:read"],
    "cache_cleanup": ["cache:refresh"],
    "refresh_all_plugins": ["cache:refresh"],
    "refresh_single_plugin": ["cache:refresh"],
    "refresh_all_releases": ["cache:refresh"],
    "refresh_all_updates": ["cache:refresh"],
    "refresh_all_tools": ["cache:refresh"],
    "refresh_docs_index": ["cache:refresh"],
    "refresh_all_indexes": ["cache:refresh"],
    "index_status": ["cache:read"],
    "backup_db": ["admin:*"],
    "list_jobs": ["jobs:read"],
    "run_job": ["jobs:write"],
    "enable_job": ["jobs:write"],
    "disable_job": ["jobs:write"],
    "audit_log": ["admin:*"],
    "stats_overview": ["stats:read"],
    "traffic_stats": ["stats:read"],
    "list_api_keys": ["admin:*"],
    "create_api_key": ["admin:*"],
    "revoke_api_key": ["admin:*"],
    "rotate_api_key": ["admin:*"],
    "api_key_usage": ["admin:*"],
    "perf_summary": ["stats:read"],
    "docs_status": ["cache:read"],
    "publish_integrity": ["stats:read"],
    "list_profiles": ["admin:*"],
    "create_profile": ["admin:*"],
    "update_profile": ["admin:*"],
    "delete_profile": ["admin:*"],
}


@dataclass(frozen=True)
class AdminApiContext:
    cache: CacheManager
    jobs: JobRepository
    storage_getter: Callable[[], Path]
    config_getter: Callable[[], dict[str, Any]]
    get_db: Callable[[], Any]
    auth_policy: AuthPolicy
    request_context_factory: Callable[[], RequestContext]
    refresh_plugin_index: Callable[..., Any]
    refresh_all_plugin_index: Callable[..., Any]
    get_plugin_index_state: Callable[..., Any]
    is_plugin_index_populated: Callable[..., bool]
    get_plugin_catalog_from_index: Callable[..., Any]
    human_size: Callable[[int], str]
    get_slow_requests: Callable[[], list[dict[str, Any]]] | None = None
    get_access_recorder_status: Callable[[], dict[str, Any]] | None = None


admin_api = Blueprint("admin_api", __name__, url_prefix="/api/admin")

_ctx: AdminApiContext | None = None


def _get_ctx() -> AdminApiContext:
    if _ctx is None:
        raise RuntimeError("Admin API not initialized")
    return _ctx


def _require_admin_auth(required_scopes: list[str] | None = None):
    """Check authentication for admin endpoints with optional scope check."""
    ctx = _get_ctx()
    request_context = ctx.request_context_factory()
    scopes_to_check = required_scopes or ["admin:*"]
    decision = ctx.auth_policy.authorize(request_context, scopes_to_check)
    if decision.allowed:
        set_authenticated_request_context(request_context.with_actor(decision.principal))
        return None
    if decision.forbidden:
        ctx.cache.write_audit(
            actor_type=decision.principal.actor_type,
            actor_id=decision.principal.actor_id,
            action="auth_forbidden",
            target_type="admin_endpoint",
            detail=f"Insufficient scope. Required: {required_scopes}",
            ip=request_context.remote_addr or "",
            user_agent=request_context.user_agent,
        )
        return jsonify({"error": "Insufficient scope", "required": required_scopes, "status": 403}), 403

    ctx.cache.write_audit(
        actor_type="anonymous",
        actor_id="",
        action="auth_unauthorized",
        target_type="admin_endpoint",
        detail=f"Path: {request_context.path}",
        ip=request_context.remote_addr or "",
        user_agent=request_context.user_agent,
    )
    return jsonify({"error": "Authentication required", "status": 401}), 401


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


@admin_api.route("/docs/status", methods=["GET"])
def docs_status():
    from services.docs_site import build_docs_status

    return jsonify(build_docs_status(_get_ctx().cache))


@admin_api.route("/publish/integrity", methods=["GET"])
def publish_integrity():
    ctx = _get_ctx()
    from services.publish_integrity import build_publish_integrity_report

    return jsonify(build_publish_integrity_report(ctx.storage_getter(), ctx.cache))


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
# Artifact index management
# ---------------------------------------------------------------------------

@admin_api.route("/index/releases/refresh", methods=["POST"])
def refresh_all_releases():
    ctx = _get_ctx()
    from services.artifact_index import refresh_release_index
    result = refresh_release_index(ctx.cache, ctx.storage_getter())

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="index_refresh_releases",
        target_type="release_index",
        detail=f"indexed={result['indexed_count']} errors={len(result['errors'])}",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify(result)


@admin_api.route("/index/updates/refresh", methods=["POST"])
def refresh_all_updates():
    ctx = _get_ctx()
    from services.artifact_index import refresh_update_index
    result = refresh_update_index(ctx.cache, ctx.storage_getter())

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="index_refresh_updates",
        target_type="update_index",
        detail=f"indexed={result['indexed_count']} errors={len(result['errors'])}",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify(result)


@admin_api.route("/index/tools/refresh", methods=["POST"])
def refresh_all_tools():
    ctx = _get_ctx()
    from services.artifact_index import refresh_tool_index
    result = refresh_tool_index(ctx.cache, ctx.storage_getter())

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="index_refresh_tools",
        target_type="tool_index",
        detail=f"indexed={result['indexed_count']} errors={len(result['errors'])}",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify(result)


@admin_api.route("/index/docs/refresh", methods=["POST"])
def refresh_docs_index():
    ctx = _get_ctx()
    from services.docs_site import refresh_docs_index as _refresh_docs_index

    result = _refresh_docs_index(ctx.cache)

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="index_refresh_docs",
        target_type="docs_index",
        detail=f"indexed={result.get('indexed_count', 0)} errors={len(result.get('errors', []))}",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify(result)


@admin_api.route("/index/refresh-all", methods=["POST"])
def refresh_all_indexes():
    ctx = _get_ctx()
    from services.artifact_index import refresh_all_indexes as _refresh_all
    from services.plugin_index import refresh_all_plugin_index
    from services.docs_site import refresh_docs_index as _refresh_docs_index

    results = {}

    # Plugin index
    download_counts: dict[str, int] = {}
    try:
        from download_stats import get_download_counts
        download_counts = get_download_counts(ctx.get_db)
    except Exception:
        pass
    plugin_result = refresh_all_plugin_index(ctx.cache, ctx.storage_getter(), download_counts=download_counts)
    results["plugins"] = plugin_result

    # Artifact indexes
    artifact_results = _refresh_all(ctx.cache, ctx.storage_getter())
    results.update(artifact_results["results"])

    # Docs index
    results["docs"] = _refresh_docs_index(ctx.cache)

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="index_refresh_all",
        target_type="all_indexes",
        detail=f"All indexes refreshed",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )

    return jsonify(results)


@admin_api.route("/index/status", methods=["GET"])
def index_status():
    ctx = _get_ctx()
    from services.artifact_index import get_all_index_states_summary
    summary = get_all_index_states_summary(ctx.cache)
    return jsonify(summary)


# ---------------------------------------------------------------------------
# DB backup
# ---------------------------------------------------------------------------

@admin_api.route("/backup/db", methods=["POST"])
def backup_db():
    ctx = _get_ctx()
    from datetime import datetime, timezone
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    backup_path = ctx.cache.db_path.parent / f"marketplace_backup_{timestamp}.db"

    success = ctx.cache.backup_db(backup_path)
    if not success:
        return jsonify({"error": "Backup failed"}), 500

    # Backups participate in the same privacy retention contract as the live
    # database; otherwise an old snapshot could retain visitor identifiers
    # after the scheduled live cleanup has removed them.
    try:
        from services.access_analytics import prune_access_analytics_database

        config = ctx.config_getter()
        prune_access_analytics_database(
            backup_path,
            retention_days=int(config.get("access_analytics_retention_days", 90) or 90),
        )
    except Exception as exc:
        backup_path.unlink(missing_ok=True)
        return jsonify({"error": f"Backup privacy cleanup failed: {exc}"}), 500

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="db_backup",
        target_type="database",
        detail=f"Backup to {backup_path.name}",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )

    return jsonify({
        "status": "ok",
        "backup_path": str(backup_path),
        "backup_size_bytes": backup_path.stat().st_size if backup_path.exists() else 0,
    })


# ---------------------------------------------------------------------------
# Jobs management
# ---------------------------------------------------------------------------

@admin_api.route("/jobs", methods=["GET"])
def list_jobs():
    ctx = _get_ctx()
    try:
        return jsonify(ctx.jobs.list_with_latest_runs())
    except Exception as exc:
        return jsonify({"error": str(exc)}), 500


@admin_api.route("/jobs/<job_id>/run", methods=["POST"])
def run_job(job_id: str):
    ctx = _get_ctx()
    if ctx.jobs.get(job_id) is None:
        return jsonify({"error": "Job not found"}), 404

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
    if not ctx.jobs.set_enabled(job_id, True, now_iso()):
        return jsonify({"error": "Job not found"}), 404

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
    if not ctx.jobs.set_enabled(job_id, False, now_iso()):
        return jsonify({"error": "Job not found"}), 404

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
    actor = request.args.get("actor", "").strip() or None
    target = request.args.get("target", "").strip() or None
    since = request.args.get("since", "").strip() or None
    until = request.args.get("until", "").strip() or None
    limit = min(int(request.args.get("limit", 100)), 500)
    offset = int(request.args.get("offset", 0))

    entries = ctx.cache.get_audit_log(
        action=action, actor=actor, target=target,
        since=since, until=until,
        limit=limit, offset=offset,
    )
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

        storage = ctx.storage_getter()
        from services.app_latest_version_cache import get_latest_version_cached
        latest = get_latest_version_cached(storage)
        stats["latestReleaseVersion"] = latest

        # Cache hit status
        cached = ctx.cache.get_cache_entry("plugin_catalog:v1")
        stats["pluginCatalogCached"] = cached is not None

        # DB size
        try:
            stats["dbSizeBytes"] = ctx.cache._db_path.stat().st_size
        except OSError:
            stats["dbSizeBytes"] = 0

        from services.access_analytics import get_today_access_summary
        stats.update(get_today_access_summary(db))

        return jsonify(stats)
    except Exception as exc:
        return jsonify({"error": str(exc)}), 500
    finally:
        db.close()


@admin_api.route("/stats/traffic", methods=["GET"])
def traffic_stats():
    """Return privacy-preserving access aggregates for the admin dashboard."""
    from services.access_analytics import (
        SqliteAccessTrafficQuery,
        parse_bounded_int,
    )

    try:
        days = parse_bounded_int(
            request.args.get("days"),
            name="days",
            default=30,
            minimum=1,
            maximum=365,
        )
        limit = parse_bounded_int(
            request.args.get("limit"),
            name="limit",
            default=10,
            minimum=1,
            maximum=100,
        )
    except ValueError as exc:
        return jsonify({"error": str(exc), "status": 400}), 400

    ctx = _get_ctx()
    query = SqliteAccessTrafficQuery(
        ctx.get_db,
        recorder_status=ctx.get_access_recorder_status,
    )
    return jsonify(query.get_traffic(days=days, limit=limit))


# ---------------------------------------------------------------------------
# API Key management
# ---------------------------------------------------------------------------

@admin_api.route("/api-keys", methods=["GET"])
def list_api_keys():
    from services.api_key_service import list_api_keys as _list_keys
    ctx = _get_ctx()
    keys = _list_keys(ctx.cache)
    return jsonify(keys)


# Allowed scopes for API key creation
ALLOWED_SCOPES = {
    "admin:*",
    "cache:read",
    "cache:refresh",
    "jobs:read",
    "jobs:write",
    "stats:read",
    "plugin:read",
    "plugin:publish",
    "release:publish",
    "file:transfer",
    "ops:relay",
    "ops:operator",
    "copilot:config:read",
}


def validate_scopes(scopes_str: str) -> tuple[list[str], list[str]]:
    """Validate scopes against ALLOWED_SCOPES. Returns (valid, invalid)."""
    requested = {s.strip() for s in scopes_str.split(",") if s.strip()}
    invalid = sorted(requested - ALLOWED_SCOPES)
    valid = sorted(requested & ALLOWED_SCOPES)
    return valid, invalid


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

    # Validate scopes against whitelist
    if scopes:
        _, invalid = validate_scopes(scopes)
        if invalid:
            return jsonify({
                "error": f"Invalid scopes: {', '.join(invalid)}",
                "allowed_scopes": sorted(ALLOWED_SCOPES),
            }), 400

    description = (data.get("description") or "").strip()
    expires_at = data.get("expires_at")

    # Default expiry suggestion: 90 days from now
    if not expires_at:
        from datetime import timedelta
        default_expiry = datetime.now(timezone.utc) + timedelta(days=90)
        expires_at = default_expiry.isoformat()

    result = _create_key(
        ctx.cache,
        name=name,
        scopes=scopes,
        created_by=_actor_id(),
        expires_at=expires_at,
    )

    # Store description if provided
    if description:
        db = ctx.cache.get_db()
        try:
            db.execute(
                "UPDATE api_keys SET name = ? WHERE id = ?",
                (f"{name} ({description})" if description else name, result["id"]),
            )
            db.commit()
        finally:
            db.close()
        result["description"] = description

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
    return current_request_context().actor.actor_type or "system"


def _actor_id() -> str:
    return current_request_context().actor.actor_id or "system"


# ---------------------------------------------------------------------------
# Performance summary
# ---------------------------------------------------------------------------

@admin_api.route("/perf/summary", methods=["GET"])
def perf_summary():
    ctx = _get_ctx()
    try:
        # Recent slow requests from in-memory buffer
        slow_requests = []
        if ctx.get_slow_requests:
            slow_requests = ctx.get_slow_requests()[-20:]

        # Recent slow job runs
        slow_jobs = []
        for r in ctx.jobs.recent_runs(20):
            if r.get("duration_ms", 0) >= 1000 or r.get("status") == "error":
                slow_jobs.append(r)

        return jsonify({
            "slow_requests": slow_requests,
            "slow_jobs": slow_jobs,
            "threshold_ms": 500,
        })
    except Exception as exc:
        return jsonify({"error": str(exc)}), 500

"""
Admin API routes for ColorVision Marketplace.

Provides management endpoints for cache, index, jobs, audit log, deployments, and stats.
All endpoints require authentication (session, Basic Auth, or Bearer API Key).

Per-endpoint scope requirements:
  - GET  /cache/status        → cache:read
  - POST /cache/cleanup       → cache:refresh
  - POST /index/plugins/*     → cache:refresh
  - POST /index/docs/refresh  → cache:refresh
  - GET  /jobs                → jobs:read
  - GET  /jobs/*/runs         → jobs:read
  - POST /jobs/*/run          → jobs:write
  - POST /jobs/*/enable       → jobs:write
  - POST /jobs/*/disable      → jobs:write
  - GET  /audit-log           → audit:read
  - GET  /deployments         → deployments:read
  - GET  /operations/overview → operations:manage
  - GET  /feedback            → feedback:manage
  - GET  /feedback/*          → feedback:manage
  - PUT  /feedback/*/status   → feedback:manage
  - GET  /stats/overview      → stats:read
  - GET  /docs/status         → cache:read
  - GET  /publish/integrity   → stats:read
  - *    /api-keys            → api_keys:manage
  - *    /settings/*          → settings:manage
  - *    /users/*             → users:manage
  - *    /login-security      → users:manage
  - *    /registration-security → users:manage
  - *    /permissions         → permissions:manage
  - *    /roles/*/permissions → permissions:manage
  - *    /copilot/profiles    → copilot:manage

admin:* grants API keys access to all endpoints. Existing administrator
sessions and Basic Auth retain full access. Registered-user sessions are
checked against the database-backed role permission matrix.
Transfer file endpoints use file:transfer.
"""

from __future__ import annotations

import hmac
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

from flask import Blueprint, current_app, jsonify, request, send_file, session

from db_cache import CacheManager, now_iso
from ports.jobs import JobRepository
from ports.operations_admin import OperationsAdminQuery
from routes.request_context import current_request_context, set_authenticated_request_context
from services.auth_policy import AuthPolicy
from services.api_key_service import (
    ALLOWED_SCOPES,
    DEFAULT_API_KEY_SCOPES,
    list_api_key_scope_definitions,
    validate_api_key_scopes,
)
from services.deployment_history import query_deployment_history
from services.performance_observability import (
    DEFAULT_SLOW_REQUEST_THRESHOLD_MS,
    SLOW_REQUEST_BUFFER_CAPACITY,
    build_performance_summary,
)
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
    "list_db_backups": ["backups:manage"],
    "backup_db": ["backups:manage"],
    "list_jobs": ["jobs:read"],
    "list_job_runs": ["jobs:read"],
    "run_job": ["jobs:write"],
    "enable_job": ["jobs:write"],
    "disable_job": ["jobs:write"],
    "audit_log": ["audit:read"],
    "deployment_history": ["deployments:read"],
    "operations_overview": ["operations:manage"],
    "feedback_inbox": ["feedback:manage"],
    "feedback_detail": ["feedback:manage"],
    "feedback_attachment": ["feedback:manage"],
    "update_feedback_status": ["feedback:manage"],
    "stats_overview": ["stats:read"],
    "traffic_stats": ["stats:read"],
    "list_users": ["users:manage"],
    "user_details": ["users:manage"],
    "create_user_account": ["users:manage"],
    "delete_user_account": ["users:manage"],
    "update_user_profile": ["users:manage"],
    "update_user_role": ["users:manage"],
    "reset_user_password": ["users:manage"],
    "require_user_password_change": ["users:manage"],
    "revoke_user_sessions": ["users:manage"],
    "bulk_user_security_action": ["users:manage"],
    "enable_user": ["users:manage"],
    "disable_user": ["users:manage"],
    "list_login_security": ["users:manage"],
    "unlock_login_security": ["users:manage"],
    "list_registration_security": ["users:manage"],
    "clear_registration_security": ["users:manage"],
    "list_permissions": ["permissions:manage"],
    "update_role_permissions": ["permissions:manage"],
    "list_api_keys": ["api_keys:manage"],
    "api_key_scopes": ["api_keys:manage"],
    "create_api_key": ["api_keys:manage"],
    "revoke_api_key": ["api_keys:manage"],
    "rotate_api_key": ["api_keys:manage"],
    "api_key_usage": ["api_keys:manage"],
    "perf_summary": ["stats:read"],
    "docs_status": ["cache:read"],
    "publish_integrity": ["stats:read"],
    "list_profiles": ["copilot:manage"],
    "create_profile": ["copilot:manage"],
    "update_profile": ["copilot:manage"],
    "delete_profile": ["copilot:manage"],
    "get_retention_settings": ["settings:manage"],
    "update_retention_settings": ["settings:manage"],
    "get_account_settings": ["settings:manage"],
    "update_account_settings": ["settings:manage"],
}


@dataclass(frozen=True)
class AdminApiContext:
    cache: CacheManager
    jobs: JobRepository
    storage_getter: Callable[[], Path]
    config_getter: Callable[[], dict[str, Any]]
    config_path_getter: Callable[[], Path]
    get_db: Callable[[], Any]
    auth_policy: AuthPolicy
    request_context_factory: Callable[[], RequestContext]
    operations_admin: OperationsAdminQuery
    refresh_plugin_index: Callable[..., Any]
    refresh_all_plugin_index: Callable[..., Any]
    get_plugin_index_state: Callable[..., Any]
    is_plugin_index_populated: Callable[..., bool]
    get_plugin_catalog_from_index: Callable[..., Any]
    human_size: Callable[[int], str]
    get_slow_requests: Callable[[], list[dict[str, Any]]] | None = None
    get_access_recorder_status: Callable[[], dict[str, Any]] | None = None
    slow_request_threshold_ms: int = DEFAULT_SLOW_REQUEST_THRESHOLD_MS
    slow_request_buffer_capacity: int = SLOW_REQUEST_BUFFER_CAPACITY
    process_started_at: datetime | None = None


admin_api = Blueprint("admin_api", __name__, url_prefix="/api/admin")

_ctx: AdminApiContext | None = None


def _get_ctx() -> AdminApiContext:
    if _ctx is None:
        raise RuntimeError("Admin API not initialized")
    return _ctx


def _query_int_arg(
    name: str,
    default: int,
    *,
    minimum: int,
    maximum: int | None = None,
) -> int:
    try:
        value = int(request.args.get(name, default))
    except (TypeError, ValueError) as exc:
        raise ValueError(f"{name} must be an integer") from exc
    if value < minimum:
        raise ValueError(f"{name} must be at least {minimum}")
    if maximum is not None and value > maximum:
        raise ValueError(f"{name} must be at most {maximum}")
    return value


def _require_admin_auth(required_scopes: list[str] | None = None):
    """Check authentication for admin endpoints with optional scope check."""
    ctx = _get_ctx()
    request_context = ctx.request_context_factory()
    scopes_to_check = required_scopes or ["admin:*"]
    decision = ctx.auth_policy.authorize(
        request_context,
        scopes_to_check,
        allow_user_session=True,
    )
    if decision.allowed:
        set_authenticated_request_context(request_context.with_actor(decision.principal))
        return None
    if decision.reason == "password_change_required":
        return jsonify({
            "error": "Password change required",
            "code": "password_change_required",
            "next": "/account?password_change=required",
            "status": 403,
        }), 403
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
        return jsonify({
            "error": "Insufficient scope",
            "code": "insufficient_scope",
            "required": required_scopes,
            "status": 403,
        }), 403

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
# Operational settings
# ---------------------------------------------------------------------------

@admin_api.route("/settings/accounts", methods=["GET"])
def get_account_settings():
    from services.account_settings import get_account_settings as _get_account_settings

    return jsonify({
        **_get_account_settings(_get_ctx().config_getter()),
        "restart_required": False,
    })


@admin_api.route("/settings/accounts", methods=["PUT"])
def update_account_settings():
    from services.account_settings import (
        persist_account_settings,
        validate_account_settings_payload,
    )

    ctx = _get_ctx()
    try:
        values = validate_account_settings_payload(request.get_json(silent=True))
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400

    try:
        result = persist_account_settings(
            ctx.config_path_getter(),
            ctx.config_getter(),
            values,
        )
    except (OSError, ValueError):
        return jsonify({"error": "Unable to persist account settings"}), 500

    if result["changed"]:
        name = result["changed"][0]
        ctx.cache.write_audit(
            actor_type=_actor_type(),
            actor_id=_actor_id(),
            action="account_settings_update",
            target_type="configuration",
            target_id=name,
            detail=f"{name}: {str(result['before'][name]).lower()} -> {str(values[name]).lower()}",
            ip=request.remote_addr or "",
            user_agent=request.headers.get("User-Agent", "")[:200],
        )

    return jsonify({
        "status": "updated" if result["changed"] else "unchanged",
        **result["values"],
        "changed": result["changed"],
        "restart_required": False,
    })

@admin_api.route("/settings/retention", methods=["GET"])
def get_retention_settings():
    from services.operational_settings import (
        get_operational_retention_settings,
        operational_retention_limits,
    )

    return jsonify({
        "values": get_operational_retention_settings(_get_ctx().config_getter()),
        "limits": operational_retention_limits(),
        "restart_required": False,
    })


@admin_api.route("/settings/retention", methods=["PUT"])
def update_retention_settings():
    from services.operational_settings import (
        operational_retention_limits,
        persist_operational_retention_settings,
        validate_operational_retention_payload,
    )

    ctx = _get_ctx()
    try:
        values = validate_operational_retention_payload(request.get_json(silent=True))
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400

    try:
        result = persist_operational_retention_settings(
            ctx.config_path_getter(),
            ctx.config_getter(),
            values,
        )
    except (OSError, ValueError):
        return jsonify({"error": "Unable to persist operational settings"}), 500

    if result["changed"]:
        detail = ", ".join(
            f"{name}: {result['before'][name]} -> {values[name]}"
            for name in result["changed"]
        )
        ctx.cache.write_audit(
            actor_type=_actor_type(),
            actor_id=_actor_id(),
            action="retention_settings_update",
            target_type="operational_settings",
            detail=detail,
            ip=request.remote_addr or "",
            user_agent=request.headers.get("User-Agent", "")[:200],
        )

    return jsonify({
        "status": "updated" if result["changed"] else "unchanged",
        "values": result["values"],
        "limits": operational_retention_limits(),
        "changed": result["changed"],
        "restart_required": False,
    })


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

@admin_api.route("/backup/db", methods=["GET"])
def list_db_backups():
    ctx = _get_ctx()
    from services.admin_data_retention import (
        list_manual_db_backups,
        parse_admin_retention_config,
    )

    _, keep_count = parse_admin_retention_config(ctx.config_getter())
    backups = list_manual_db_backups(ctx.cache.db_path.parent)
    return jsonify({
        "backups": backups,
        "count": len(backups),
        "keep_count": keep_count,
    })


@admin_api.route("/backup/db", methods=["POST"])
def backup_db():
    ctx = _get_ctx()
    from services.database_backup import create_database_backup

    try:
        result = create_database_backup(ctx.cache, ctx.config_getter())
    except Exception as exc:
        return jsonify({"error": str(exc)}), 500

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="db_backup",
        target_type="database",
        detail=(
            f"Backup to {result['backup_name']}; "
            f"scrubbed {result['security_rows_deleted']} transient security row(s); "
            f"removed {result['backup_retention']['removedCount']} old backup(s)"
        ),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )

    public_result = dict(result)
    public_result.pop("backup_path", None)
    return jsonify(public_result)


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


@admin_api.route("/jobs/<job_id>/runs", methods=["GET"])
def list_job_runs(job_id: str):
    ctx = _get_ctx()
    if ctx.jobs.get(job_id) is None:
        return jsonify({"error": "Job not found"}), 404

    status = request.args.get("status", "").strip() or None
    if status not in {None, "success", "error", "running", "interrupted"}:
        return jsonify({"error": "status must be success, error, running, or interrupted"}), 400
    try:
        limit = _query_int_arg("limit", 20, minimum=1, maximum=100)
        offset = _query_int_arg("offset", 0, minimum=0)
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400

    return jsonify(ctx.jobs.list_runs_page(
        job_id,
        status=status,
        limit=limit,
        offset=offset,
    ))


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

    return jsonify(result), (409 if result.get("status") == "skipped" else 200)


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
    try:
        limit = _query_int_arg("limit", 100, minimum=1, maximum=500)
        offset = _query_int_arg("offset", 0, minimum=0)
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400

    page = ctx.cache.get_audit_log_page(
        action=action, actor=actor, target=target,
        since=since, until=until,
        limit=limit, offset=offset,
    )
    return jsonify({**page, "limit": limit, "offset": offset})


# ---------------------------------------------------------------------------
# Deployment history
# ---------------------------------------------------------------------------

@admin_api.route("/deployments", methods=["GET"])
def deployment_history():
    ctx = _get_ctx()
    try:
        limit = _query_int_arg("limit", 20, minimum=1, maximum=100)
        offset = _query_int_arg("offset", 0, minimum=0)
        result = query_deployment_history(
            ctx.storage_getter(),
            status=request.args.get("status"),
            source=request.args.get("source"),
            commit=request.args.get("commit"),
            limit=limit,
            offset=offset,
        )
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400
    return jsonify(result)


# ---------------------------------------------------------------------------
# Operations overview
# ---------------------------------------------------------------------------

@admin_api.route("/operations/overview", methods=["GET"])
def operations_overview():
    ctx = _get_ctx()
    try:
        host_limit = _query_int_arg("hostLimit", 100, minimum=1, maximum=200)
        activity_limit = _query_int_arg("activityLimit", 100, minimum=1, maximum=200)
        result = ctx.operations_admin.get_overview(
            now=datetime.now(timezone.utc),
            host_limit=host_limit,
            activity_limit=activity_limit,
        )
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400
    return jsonify(result)


# ---------------------------------------------------------------------------
# Feedback inbox
# ---------------------------------------------------------------------------

@admin_api.route("/feedback", methods=["GET"])
def feedback_inbox():
    from services.feedback_admin import query_feedback

    ctx = _get_ctx()
    try:
        limit = _query_int_arg("limit", 20, minimum=1, maximum=100)
        offset = _query_int_arg("offset", 0, minimum=0)
        result = query_feedback(
            ctx.storage_getter(),
            status=request.args.get("status", "").strip() or None,
            query=request.args.get("query", "").strip() or None,
            limit=limit,
            offset=offset,
        )
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400
    return jsonify(result)


@admin_api.route("/feedback/<feedback_id>", methods=["GET"])
def feedback_detail(feedback_id: str):
    from services.feedback_admin import get_feedback_detail

    try:
        return jsonify(get_feedback_detail(_get_ctx().storage_getter(), feedback_id))
    except FileNotFoundError:
        return jsonify({"error": "Feedback not found"}), 404


@admin_api.route("/feedback/<feedback_id>/attachments/<path:filename>", methods=["GET"])
def feedback_attachment(feedback_id: str, filename: str):
    from services.feedback_admin import resolve_feedback_attachment

    ctx = _get_ctx()
    try:
        target = resolve_feedback_attachment(ctx.storage_getter(), feedback_id, filename)
    except FileNotFoundError:
        return jsonify({"error": "Attachment not found"}), 404
    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="feedback_attachment_download",
        target_type="feedback",
        target_id=feedback_id,
        detail="diagnostic attachment downloaded",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return send_file(target, as_attachment=True, download_name=target.name)


@admin_api.route("/feedback/<feedback_id>/status", methods=["PUT"])
def update_feedback_status(feedback_id: str):
    from services.feedback_admin import (
        update_feedback_status as _update_feedback_status,
        validate_feedback_status_payload,
    )

    ctx = _get_ctx()
    try:
        status = validate_feedback_status_payload(request.get_json(silent=True))
        result = _update_feedback_status(ctx.storage_getter(), feedback_id, status)
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400
    except FileNotFoundError:
        return jsonify({"error": "Feedback not found"}), 404
    except OSError:
        return jsonify({"error": "Unable to persist feedback status"}), 500

    changed = result.pop("changed")
    before = result.pop("before")
    if changed:
        ctx.cache.write_audit(
            actor_type=_actor_type(),
            actor_id=_actor_id(),
            action="feedback_status_update",
            target_type="feedback",
            target_id=feedback_id,
            detail=f"status: {before} -> {status}",
            ip=request.remote_addr or "",
            user_agent=request.headers.get("User-Agent", "")[:200],
        )
    return jsonify(result)


# ---------------------------------------------------------------------------
# Stats overview
# ---------------------------------------------------------------------------

@admin_api.route("/stats/overview", methods=["GET"])
def stats_overview():
    ctx = _get_ctx()
    db = ctx.get_db()
    try:
        from services.access_analytics import (
            analytics_calendar_day,
            analytics_calendar_day_utc_bounds,
            get_today_access_summary,
            reporting_utc_offset_minutes,
        )

        utc_offset_minutes = reporting_utc_offset_minutes(ctx.config_getter())
        reporting_day = analytics_calendar_day(
            utc_offset_minutes=utc_offset_minutes,
        )
        day_start_utc, day_end_utc = analytics_calendar_day_utc_bounds(
            reporting_day,
            utc_offset_minutes=utc_offset_minutes,
        )
        stats: dict[str, Any] = {}

        row = db.execute("SELECT COUNT(*) AS cnt FROM download_log").fetchone()
        stats["totalDownloads"] = row["cnt"] if row else 0

        row = db.execute(
            """
            SELECT COUNT(*) AS cnt FROM download_log
            WHERE downloaded_at >= ? AND downloaded_at < ?
            """,
            (
                day_start_utc.strftime("%Y-%m-%d %H:%M:%S"),
                day_end_utc.strftime("%Y-%m-%d %H:%M:%S"),
            ),
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

        stats.update(get_today_access_summary(
            db,
            utc_offset_minutes=utc_offset_minutes,
        ))

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
        reporting_utc_offset_minutes,
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
    utc_offset_minutes = reporting_utc_offset_minutes(ctx.config_getter())
    query = SqliteAccessTrafficQuery(
        ctx.get_db,
        recorder_status=ctx.get_access_recorder_status,
        utc_offset_minutes=utc_offset_minutes,
    )
    return jsonify(query.get_traffic(days=days, limit=limit))


# ---------------------------------------------------------------------------
# User management
# ---------------------------------------------------------------------------

def _configured_admin_username() -> str:
    upload_auth = _get_ctx().config_getter().get("upload_auth") or {}
    if not isinstance(upload_auth, dict):
        return ""
    return str(upload_auth.get("username") or "").strip()


def _is_config_admin_user(user: dict[str, Any]) -> bool:
    configured = _configured_admin_username()
    return bool(
        configured
        and configured.casefold() == str(user.get("username") or "").casefold()
    )


def _config_admin_management_error():
    return jsonify({
        "error": "配置管理员由服务配置维护，不能通过用户管理修改",
        "status": 409,
    }), 409


def _admin_user_payload(user: dict[str, Any]) -> dict[str, Any]:
    payload = dict(user)
    payload.pop("auth_version", None)
    payload["is_config_admin"] = _is_config_admin_user(user)
    return payload


def _is_current_session_user(user: dict[str, Any]) -> bool:
    principal = current_request_context().actor
    session_user_id = session.get("user_id")
    try:
        same_user_id = int(session_user_id) == int(user.get("id"))
    except (TypeError, ValueError):
        same_user_id = False
    return (
        principal.auth_method == "session"
        and same_user_id
        and principal.actor_id.casefold() == str(user.get("username") or "").casefold()
    )


@admin_api.route("/users", methods=["GET"])
def list_users():
    from services.auth_service import list_users as _list_users, query_users

    # Preserve the original array response for callers that do not request
    # pagination. The administration UI always sends limit/offset.
    if not request.args:
        users = _list_users(_get_ctx().cache)
        for user in users:
            user["is_current"] = _is_current_session_user(user)
        return jsonify([_admin_user_payload(user) for user in users])

    query = str(request.args.get("q") or "").strip()
    role = str(request.args.get("role") or "").strip()
    status = str(request.args.get("status") or "").strip()
    account_origin = str(request.args.get("origin") or "").strip()
    password_state = str(request.args.get("password_state") or "").strip()
    recovery_state = str(request.args.get("recovery_state") or "").strip()
    sort_by = str(request.args.get("sort_by") or "").strip()
    sort_order = str(request.args.get("sort_order") or "").strip()
    if len(query) > 100:
        return jsonify({"error": "q must be at most 100 characters"}), 400
    if role not in {"", "admin", "user"}:
        return jsonify({"error": "role must be 'admin' or 'user'"}), 400
    if status not in {"", "active", "inactive"}:
        return jsonify({"error": "status must be 'active' or 'inactive'"}), 400
    if account_origin not in {
        "", "self_registered", "administrator_created", "legacy",
    }:
        return jsonify({"error": "Unsupported account origin"}), 400
    if password_state not in {"", "pending", "ready"}:
        return jsonify({"error": "password_state must be 'pending' or 'ready'"}), 400
    if recovery_state not in {"", "pending", "none"}:
        return jsonify({"error": "recovery_state must be 'pending' or 'none'"}), 400
    if sort_by not in {
        "", "username", "display_name", "email", "role", "account_origin", "is_active",
        "active_session_count", "created_at", "last_login_at",
        "password_recovery_requested_at",
    }:
        return jsonify({"error": "Unsupported user sort field"}), 400
    if sort_order not in {"", "asc", "desc"}:
        return jsonify({"error": "sort_order must be 'asc' or 'desc'"}), 400
    try:
        limit = _query_int_arg("limit", 20, minimum=1, maximum=100)
        offset = _query_int_arg("offset", 0, minimum=0)
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400

    result = query_users(
        _get_ctx().cache,
        query=query,
        role=role,
        account_origin=account_origin,
        active=True if status == "active" else False if status == "inactive" else None,
        password_change_required=(
            True if password_state == "pending"
            else False if password_state == "ready"
            else None
        ),
        password_recovery_pending=(
            True if recovery_state == "pending"
            else False if recovery_state == "none"
            else None
        ),
        sort_by=sort_by,
        sort_order=sort_order or "desc",
        limit=limit,
        offset=offset,
    )
    for user in result["items"]:
        user["is_current"] = _is_current_session_user(user)
    result["items"] = [_admin_user_payload(user) for user in result["items"]]
    return jsonify(result)


@admin_api.route("/users/<int:user_id>/details", methods=["GET"])
def user_details(user_id: int):
    """Return one managed account with active sessions and scoped activity."""
    from services.account_activity_service import get_account_activity_page
    from services.auth_service import get_user_by_id
    from services.permission_service import describe_permissions, get_role_permission_codes
    from services.password_recovery_service import get_pending_password_recovery
    from services.session_service import list_user_sessions

    try:
        activity_limit = _query_int_arg("activity_limit", 8, minimum=1, maximum=50)
        activity_offset = _query_int_arg("activity_offset", 0, minimum=0)
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400

    ctx = _get_ctx()
    target = get_user_by_id(ctx.cache, user_id)
    if not target:
        return jsonify({"error": "User not found"}), 404

    current_session_id = (
        str(session.get("login_session_id") or "")
        if _is_current_session_user(target)
        else ""
    )
    sessions = list_user_sessions(
        ctx.cache,
        user_id,
        current_session_id=current_session_id,
    )
    target["active_session_count"] = len(sessions)
    target["is_current"] = _is_current_session_user(target)
    permission_codes = get_role_permission_codes(ctx.cache, str(target.get("role") or "user"))
    return jsonify({
        "user": _admin_user_payload(target),
        "sessions": {
            "items": sessions,
            "total": len(sessions),
        },
        "permissions": describe_permissions(permission_codes),
        "password_recovery": get_pending_password_recovery(ctx.cache, user_id),
        "activity": get_account_activity_page(
            ctx.cache,
            username=str(target.get("username") or ""),
            user_id=user_id,
            limit=activity_limit,
            offset=activity_offset,
        ),
    })


@admin_api.route("/login-security", methods=["GET"])
def list_login_security():
    from services.login_throttle_service import get_login_security_page

    query = str(request.args.get("q") or "").strip()
    status = str(request.args.get("status") or "").strip()
    if len(query) > 100:
        return jsonify({"error": "q must be at most 100 characters"}), 400
    if status not in {"", "locked", "tracking"}:
        return jsonify({"error": "status must be 'locked' or 'tracking'"}), 400
    try:
        limit = _query_int_arg("limit", 20, minimum=1, maximum=100)
        offset = _query_int_arg("offset", 0, minimum=0)
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400

    return jsonify(get_login_security_page(
        _get_ctx().cache,
        configured_admin_username=_configured_admin_username(),
        query=query,
        status=status,
        limit=limit,
        offset=offset,
    ))


@admin_api.route("/login-security/unlock", methods=["POST"])
def unlock_login_security():
    from services.auth_service import get_user_by_username
    from services.login_throttle_service import clear_login_failures

    data = request.get_json(silent=True) or {}
    username = str(data.get("username") or "").strip()
    if not username:
        return jsonify({"error": "username is required"}), 400
    if len(username) > 128:
        return jsonify({"error": "username must be at most 128 characters"}), 400

    ctx = _get_ctx()
    target = get_user_by_username(ctx.cache, username)
    cleared_sources = clear_login_failures(ctx.cache, username)
    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="login_throttle_unlock",
        target_type="user" if target else "login_throttle",
        target_id=str(target["id"] if target else username),
        detail=f"username={username};cleared_sources={cleared_sources}",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify({
        "status": "unlocked",
        "username": str(target["username"] if target else username),
        "cleared_sources": cleared_sources,
    })


@admin_api.route("/registration-security", methods=["GET"])
def list_registration_security():
    from services.registration_rate_limit_service import get_registration_security_page

    query = str(request.args.get("q") or "").strip()
    status = str(request.args.get("status") or "").strip()
    if len(query) > 64:
        return jsonify({"error": "q must be at most 64 characters"}), 400
    if status not in {"", "blocked", "tracking"}:
        return jsonify({"error": "status must be 'blocked' or 'tracking'"}), 400
    try:
        limit = _query_int_arg("limit", 20, minimum=1, maximum=100)
        offset = _query_int_arg("offset", 0, minimum=0)
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400

    return jsonify(get_registration_security_page(
        _get_ctx().cache,
        query=query,
        status=status,
        limit=limit,
        offset=offset,
    ))


@admin_api.route("/registration-security/clear", methods=["POST"])
def clear_registration_security():
    from services.registration_rate_limit_service import clear_registration_rate_limit

    data = request.get_json(silent=True) or {}
    ip_address = str(data.get("ip_address") or "").strip()
    if not ip_address:
        return jsonify({"error": "ip_address is required"}), 400
    if len(ip_address) > 64:
        return jsonify({"error": "ip_address must be at most 64 characters"}), 400

    result = clear_registration_rate_limit(_get_ctx().cache, ip_address)
    _get_ctx().cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="registration_throttle_clear",
        target_type="registration",
        target_id=result.ip_address,
        detail=(
            f"ip_address={result.ip_address};cleared={str(result.cleared).lower()};"
            f"pending_count={result.pending_count}"
        ),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify({
        "status": "cleared" if result.cleared else "unchanged",
        "ip_address": result.ip_address,
        "cleared": result.cleared,
        "pending_count": result.pending_count,
    })


@admin_api.route("/users", methods=["POST"])
def create_user_account():
    from services.auth_service import create_user

    ctx = _get_ctx()
    data = request.get_json(silent=True) or {}
    username = str(data.get("username") or "").strip()
    password = str(data.get("password") or "")
    role = str(data.get("role") or "user")
    display_name = str(data.get("display_name") or "")
    email = str(data.get("email") or "")
    if (
        _configured_admin_username()
        and username.casefold() == _configured_admin_username().casefold()
    ):
        return _config_admin_management_error()
    user, error = create_user(
        ctx.cache,
        username,
        password,
        role=role,
        display_name=display_name,
        email=email,
        must_change_password=True,
        account_origin="administrator_created",
    )
    if error or not user:
        return jsonify({"error": error or "Account creation failed"}), 400

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="user_create",
        target_type="user",
        target_id=str(user["id"]),
        detail=(
            f"username={user['username']};role={user['role']};"
            "must_change_password=true"
        ),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify(_admin_user_payload(user)), 201


@admin_api.route("/users/<int:user_id>/profile", methods=["PUT"])
def update_user_profile(user_id: int):
    from services.auth_service import get_user_by_id, update_user_profile as _update_profile

    ctx = _get_ctx()
    target = get_user_by_id(ctx.cache, user_id)
    if not target:
        return jsonify({"error": "User not found"}), 404
    if _is_config_admin_user(target):
        return _config_admin_management_error()
    data = request.get_json(silent=True) or {}
    updated, error = _update_profile(
        ctx.cache,
        user_id,
        display_name=str(data.get("display_name") or ""),
        email=str(data.get("email") or ""),
    )
    if error == "user_not_found":
        return jsonify({"error": "User not found"}), 404
    if error == "profile_update_failed":
        return jsonify({"error": "User profile update failed"}), 500
    if error or not updated:
        return jsonify({"error": error or "User profile update failed"}), 400

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="user_profile_update",
        target_type="user",
        target_id=str(user_id),
        detail=f"username={updated['username']};fields=display_name,email",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify(_admin_user_payload(updated))


@admin_api.route("/users/<int:user_id>", methods=["DELETE"])
def delete_user_account(user_id: int):
    """Permanently delete a disabled database account after target confirmation."""
    from services.auth_service import delete_inactive_user, get_user_by_id

    ctx = _get_ctx()
    target = get_user_by_id(ctx.cache, user_id)
    if not target:
        return jsonify({"error": "User not found"}), 404
    if _is_config_admin_user(target):
        return _config_admin_management_error()
    if _is_current_session_user(target):
        return jsonify({"error": "The current session account cannot be deleted"}), 409

    data = request.get_json(silent=True) or {}
    confirmed_username = str(data.get("username") or "").strip()
    if not confirmed_username:
        return jsonify({"error": "username confirmation is required"}), 400
    if not hmac.compare_digest(
        confirmed_username.casefold().encode("utf-8"),
        str(target["username"]).casefold().encode("utf-8"),
    ):
        return jsonify({"error": "username confirmation does not match"}), 400

    deleted, error = delete_inactive_user(ctx.cache, user_id)
    if error == "account_must_be_disabled":
        return jsonify({
            "error": "Account must be disabled before deletion",
            "code": error,
        }), 409
    if error == "user_not_found":
        return jsonify({"error": "User not found"}), 404
    if error or not deleted:
        return jsonify({"error": "User deletion failed"}), 500

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="user_delete",
        target_type="user",
        target_id=str(user_id),
        detail=(
            f"username={deleted['username']};role={deleted['role']};"
            f"sessions_deleted={deleted['sessions_deleted']};"
            f"recovery_requests_deleted={deleted['password_recovery_requests_deleted']};"
            f"cleared_sources={deleted['login_failure_sources_cleared']}"
        ),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify({"status": "deleted", **deleted})


def _set_user_status(user_id: int, *, active: bool):
    from services.auth_service import get_user_by_id, set_user_active
    from services.login_throttle_service import clear_login_failures
    from services.password_recovery_service import resolve_password_recovery_requests
    from services.session_service import revoke_all_user_sessions

    ctx = _get_ctx()
    target = get_user_by_id(ctx.cache, user_id)
    if not target:
        return jsonify({"error": "User not found"}), 404
    if _is_config_admin_user(target):
        return _config_admin_management_error()

    is_current_session = _is_current_session_user(target)
    if not active and is_current_session:
        return jsonify({"error": "The current session account cannot be disabled"}), 409

    updated, error = set_user_active(ctx.cache, user_id, active=active)
    if error == "last_active_admin":
        return jsonify({"error": "The last active administrator cannot be disabled"}), 409
    if error or not updated:
        return jsonify({"error": "User update failed"}), 500

    action = "user_enable" if active else "user_disable"
    revoked_sessions = revoke_all_user_sessions(ctx.cache, user_id, reason=action)
    cleared_sources = clear_login_failures(ctx.cache, str(updated["username"]))
    recovery_requests_resolved = 0
    if not active:
        recovery_requests_resolved = resolve_password_recovery_requests(
            ctx.cache,
            user_id,
            resolved_by=_actor_id(),
            resolution="account_disabled",
        )
    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action=action,
        target_type="user",
        target_id=str(user_id),
        detail=(
            f"username={updated['username']};sessions_revoked={revoked_sessions};"
            f"cleared_sources={cleared_sources};"
            f"recovery_requests_resolved={recovery_requests_resolved}"
        ),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    payload = _admin_user_payload(updated)
    payload["sessions_revoked"] = revoked_sessions
    payload["login_failure_sources_cleared"] = cleared_sources
    payload["password_recovery_requests_resolved"] = recovery_requests_resolved
    return jsonify(payload)


@admin_api.route("/users/<int:user_id>/enable", methods=["POST"])
def enable_user(user_id: int):
    return _set_user_status(user_id, active=True)


@admin_api.route("/users/<int:user_id>/disable", methods=["POST"])
def disable_user(user_id: int):
    return _set_user_status(user_id, active=False)


@admin_api.route("/users/<int:user_id>/role", methods=["PUT"])
def update_user_role(user_id: int):
    from services.auth_service import get_user_by_id, set_user_role

    ctx = _get_ctx()
    target = get_user_by_id(ctx.cache, user_id)
    if not target:
        return jsonify({"error": "User not found"}), 404
    if _is_config_admin_user(target):
        return _config_admin_management_error()
    if _is_current_session_user(target):
        return jsonify({"error": "The current session account role cannot be changed"}), 409

    data = request.get_json(silent=True) or {}
    role = str(data.get("role") or "")
    updated, error = set_user_role(ctx.cache, user_id, role=role)
    if error == "invalid_role":
        return jsonify({"error": "role must be 'admin' or 'user'"}), 400
    if error == "last_active_admin":
        return jsonify({"error": "The last active administrator cannot be demoted"}), 409
    if error == "user_not_found":
        return jsonify({"error": "User not found"}), 404
    if error or not updated:
        return jsonify({"error": "User role update failed"}), 500

    from services.session_service import revoke_all_user_sessions

    revoked_sessions = revoke_all_user_sessions(ctx.cache, user_id, reason="role_changed")
    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="user_role_update",
        target_type="user",
        target_id=str(user_id),
        detail=(
            f"username={updated['username']};old_role={target['role']};"
            f"new_role={updated['role']};sessions_revoked={revoked_sessions}"
        ),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify(_admin_user_payload(updated))


@admin_api.route("/users/<int:user_id>/password", methods=["POST"])
def reset_user_password(user_id: int):
    from services.auth_service import get_user_by_id, reset_user_password as _reset_password
    from services.login_throttle_service import clear_login_failures

    ctx = _get_ctx()
    target = get_user_by_id(ctx.cache, user_id)
    if not target:
        return jsonify({"error": "User not found"}), 404
    if _is_config_admin_user(target):
        return _config_admin_management_error()

    current_session_preserved = _is_current_session_user(target)
    data = request.get_json(silent=True) or {}
    password = str(data.get("password") or "")
    updated, error = _reset_password(
        ctx.cache,
        user_id,
        password=password,
        require_change=not current_session_preserved,
    )
    if error == "user_not_found":
        return jsonify({"error": "User not found"}), 404
    if error in {"password_service_unavailable", "password_reset_failed"}:
        return jsonify({"error": "Password reset failed"}), 500
    if error or not updated:
        return jsonify({"error": error or "Password reset failed"}), 400

    from services.session_service import (
        create_user_session,
        revoke_all_user_sessions,
        restore_current_user_session,
    )

    revoked_sessions = revoke_all_user_sessions(ctx.cache, user_id, reason="password_reset")
    restored_current_session = False
    if current_session_preserved:
        session["auth_version"] = int(updated.get("auth_version") or 0)
        session["must_change_password"] = bool(updated.get("must_change_password"))
        login_session_id = str(session.get("login_session_id") or "")
        if login_session_id:
            restored_current_session = restore_current_user_session(
                ctx.cache,
                user_id,
                login_session_id,
                auth_version=int(updated.get("auth_version") or 0),
            )
        if not restored_current_session:
            session["login_session_id"] = create_user_session(
                ctx.cache,
                user_id,
                auth_version=int(updated.get("auth_version") or 0),
                ip_address=request.remote_addr or "",
                user_agent=request.headers.get("User-Agent", ""),
            )

    recovery_requests_resolved = 0
    try:
        from services.password_recovery_service import resolve_password_recovery_requests

        recovery_requests_resolved = resolve_password_recovery_requests(
            ctx.cache,
            user_id,
            resolved_by=_actor_id(),
            resolution="administrator_password_reset",
        )
    except Exception:
        current_app.logger.exception("Unable to resolve password recovery after admin reset")
    cleared_sources = clear_login_failures(ctx.cache, str(updated["username"]))

    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="user_password_reset",
        target_type="user",
        target_id=str(user_id),
        detail=(
            f"username={updated['username']};sessions_invalidated=true;"
            f"sessions_revoked={max(0, revoked_sessions - (1 if restored_current_session else 0))};"
            f"must_change_password={str(updated['must_change_password']).lower()}"
            f";recovery_requests_resolved={recovery_requests_resolved};"
            f"cleared_sources={cleared_sources}"
        ),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    payload = _admin_user_payload(updated)
    payload["sessions_invalidated"] = True
    payload["current_session_preserved"] = current_session_preserved
    payload["password_recovery_requests_resolved"] = recovery_requests_resolved
    payload["login_failure_sources_cleared"] = cleared_sources
    return jsonify(payload)


@admin_api.route("/users/<int:user_id>/password-change-required", methods=["POST"])
def require_user_password_change(user_id: int):
    """Expire active sessions and require a self-service password change."""
    from services.auth_service import (
        get_user_by_id,
        require_user_password_change as _require_password_change,
    )
    from services.session_service import revoke_all_user_sessions
    from services.login_throttle_service import clear_login_failures

    ctx = _get_ctx()
    target = get_user_by_id(ctx.cache, user_id)
    if not target:
        return jsonify({"error": "User not found"}), 404
    if _is_config_admin_user(target):
        return _config_admin_management_error()
    if _is_current_session_user(target):
        return jsonify({
            "error": "The current session account cannot be forced to change password",
            "status": 409,
        }), 409

    updated, error = _require_password_change(ctx.cache, user_id)
    if error == "user_not_found":
        return jsonify({"error": "User not found"}), 404
    if error or not updated:
        return jsonify({"error": "Password change requirement failed"}), 500

    revoked_sessions = revoke_all_user_sessions(
        ctx.cache,
        user_id,
        reason="administrator_password_change_required",
    )
    cleared_sources = clear_login_failures(ctx.cache, str(updated["username"]))
    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="user_password_change_required",
        target_type="user",
        target_id=str(user_id),
        detail=(
            f"username={updated['username']};must_change_password=true;"
            f"sessions_revoked={revoked_sessions};cleared_sources={cleared_sources}"
        ),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    payload = _admin_user_payload(updated)
    payload["sessions_invalidated"] = True
    payload["sessions_revoked"] = revoked_sessions
    payload["login_failure_sources_cleared"] = cleared_sources
    return jsonify(payload)


@admin_api.route("/users/<int:user_id>/sessions/revoke", methods=["POST"])
def revoke_user_sessions(user_id: int):
    """Force a managed account offline without changing its status or password."""
    from services.auth_service import get_user_by_id
    from services.session_service import revoke_all_user_sessions

    ctx = _get_ctx()
    target = get_user_by_id(ctx.cache, user_id)
    if not target:
        return jsonify({"error": "User not found"}), 404
    if _is_config_admin_user(target):
        return _config_admin_management_error()
    if _is_current_session_user(target):
        return jsonify({"error": "The current session account cannot be forced offline"}), 409

    revoked = revoke_all_user_sessions(
        ctx.cache,
        user_id,
        reason="administrator_forced_logout",
        auth_version=int(target.get("auth_version") or 0),
    )
    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="user_sessions_force_revoke",
        target_type="user",
        target_id=str(user_id),
        detail=f"username={target['username']};sessions_revoked={revoked}",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify({
        "status": "revoked",
        "user_id": user_id,
        "username": target["username"],
        "revoked": revoked,
    })


@admin_api.route("/users/bulk-security", methods=["POST"])
def bulk_user_security_action():
    """Apply a bounded security action and report each account outcome."""
    from services.auth_service import (
        get_user_by_id,
        require_user_password_change as _require_password_change,
    )
    from services.session_service import revoke_all_user_sessions
    from services.login_throttle_service import clear_login_failures

    data = request.get_json(silent=True) or {}
    action = str(data.get("action") or "")
    raw_user_ids = data.get("user_ids")
    if action not in {"force_logout", "require_password_change"}:
        return jsonify({
            "error": "action must be 'force_logout' or 'require_password_change'",
        }), 400
    if not isinstance(raw_user_ids, list) or not raw_user_ids:
        return jsonify({"error": "user_ids must be a non-empty integer array"}), 400
    if len(raw_user_ids) > 100:
        return jsonify({"error": "user_ids cannot contain more than 100 items"}), 400
    if any(type(user_id) is not int or user_id <= 0 for user_id in raw_user_ids):
        return jsonify({"error": "user_ids must contain positive integers"}), 400

    user_ids = list(dict.fromkeys(raw_user_ids))
    ctx = _get_ctx()
    results: list[dict[str, Any]] = []
    total_sessions_revoked = 0
    total_login_failure_sources_cleared = 0
    for user_id in user_ids:
        target = get_user_by_id(ctx.cache, user_id)
        if not target:
            results.append({
                "user_id": user_id,
                "username": "",
                "status": "failed",
                "code": "user_not_found",
                "error": "账号不存在",
            })
            continue
        if _is_config_admin_user(target):
            results.append({
                "user_id": user_id,
                "username": target["username"],
                "status": "failed",
                "code": "config_admin_managed",
                "error": "配置管理员由服务配置维护",
            })
            continue
        if _is_current_session_user(target):
            results.append({
                "user_id": user_id,
                "username": target["username"],
                "status": "failed",
                "code": "current_session_account",
                "error": "不能批量处置当前登录账号",
            })
            continue

        try:
            if action == "require_password_change":
                updated, error = _require_password_change(ctx.cache, user_id)
                if error or not updated:
                    results.append({
                        "user_id": user_id,
                        "username": target["username"],
                        "status": "failed",
                        "code": error or "password_change_requirement_failed",
                        "error": "要求改密失败",
                    })
                    continue
                revoked = revoke_all_user_sessions(
                    ctx.cache,
                    user_id,
                    reason="administrator_password_change_required",
                )
                cleared_sources = clear_login_failures(
                    ctx.cache,
                    str(target["username"]),
                )
                audit_action = "user_password_change_required"
                audit_detail = (
                    f"username={target['username']};must_change_password=true;"
                    f"sessions_revoked={revoked};cleared_sources={cleared_sources};bulk=true"
                )
            else:
                cleared_sources = 0
                revoked = revoke_all_user_sessions(
                    ctx.cache,
                    user_id,
                    reason="administrator_forced_logout",
                    auth_version=int(target.get("auth_version") or 0),
                )
                audit_action = "user_sessions_force_revoke"
                audit_detail = (
                    f"username={target['username']};sessions_revoked={revoked};bulk=true"
                )
        except Exception:
            current_app.logger.exception(
                "Bulk user security action failed: action=%s user_id=%s",
                action,
                user_id,
            )
            results.append({
                "user_id": user_id,
                "username": target["username"],
                "status": "failed",
                "code": "operation_failed",
                "error": "安全操作执行失败",
            })
            continue

        total_sessions_revoked += revoked
        total_login_failure_sources_cleared += cleared_sources
        results.append({
            "user_id": user_id,
            "username": target["username"],
            "status": "succeeded",
            "sessions_revoked": revoked,
            "login_failure_sources_cleared": cleared_sources,
        })
        ctx.cache.write_audit(
            actor_type=_actor_type(),
            actor_id=_actor_id(),
            action=audit_action,
            target_type="user",
            target_id=str(user_id),
            detail=audit_detail,
            ip=request.remote_addr or "",
            user_agent=request.headers.get("User-Agent", "")[:200],
        )

    succeeded = sum(item["status"] == "succeeded" for item in results)
    failed = len(results) - succeeded
    ctx.cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="user_bulk_security_action",
        target_type="user_batch",
        target_id=action,
        detail=(
            f"action={action};requested={len(user_ids)};succeeded={succeeded};"
            f"failed={failed};sessions_revoked={total_sessions_revoked};"
            f"cleared_sources={total_login_failure_sources_cleared}"
        ),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify({
        "action": action,
        "requested": len(user_ids),
        "succeeded": succeeded,
        "failed": failed,
        "sessions_revoked": total_sessions_revoked,
        "login_failure_sources_cleared": total_login_failure_sources_cleared,
        "results": results,
    })


# ---------------------------------------------------------------------------
# Role permission management
# ---------------------------------------------------------------------------

@admin_api.route("/permissions", methods=["GET"])
def list_permissions():
    from services.permission_service import list_permission_matrix

    return jsonify(list_permission_matrix(_get_ctx().cache))


@admin_api.route("/roles/<role>/permissions", methods=["PUT"])
def update_role_permissions(role: str):
    from services.permission_service import replace_role_permissions

    data = request.get_json(silent=True) or {}
    permissions = data.get("permissions")
    expected_revision = data.get("expected_revision")
    if not isinstance(permissions, list) or any(not isinstance(item, str) for item in permissions):
        return jsonify({"error": "permissions must be a string array"}), 400
    if expected_revision is not None and (
        not isinstance(expected_revision, str)
        or len(expected_revision) != 64
        or any(character not in "0123456789abcdef" for character in expected_revision)
    ):
        return jsonify({"error": "expected_revision must be a lowercase SHA-256 value"}), 400

    result, error = replace_role_permissions(
        _get_ctx().cache,
        role,
        permissions,
        expected_revision=expected_revision,
    )
    if error == "administrator_permissions_are_fixed":
        return jsonify({"error": "The existing administrator permissions are fixed"}), 409
    if error == "role_not_found":
        return jsonify({"error": "Role not found"}), 404
    if error and error.startswith("invalid_permissions:"):
        return jsonify({"error": error}), 400
    if error == "permission_revision_conflict":
        return jsonify({
            "error": "权限配置已被其他管理员更新，请刷新后重试",
            "code": "permission_revision_conflict",
        }), 409
    if error or result is None:
        return jsonify({"error": "Permission update failed"}), 500

    _get_ctx().cache.write_audit(
        actor_type=_actor_type(),
        actor_id=_actor_id(),
        action="role_permissions_update",
        target_type="role",
        target_id=role,
        detail=(
            f"added_permissions={','.join(result['change']['added']) or '-'};"
            f"removed_permissions={','.join(result['change']['removed']) or '-'};"
            f"affected_active_members={result['change']['affected_active_members']};"
            f"revision={result['change']['revision']}"
        ),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify(result)


# ---------------------------------------------------------------------------
# API Key management
# ---------------------------------------------------------------------------

@admin_api.route("/api-keys", methods=["GET"])
def list_api_keys():
    from services.api_key_service import list_api_keys as _list_keys
    ctx = _get_ctx()
    keys = _list_keys(ctx.cache)
    return jsonify(keys)


def validate_scopes(scopes_str: str) -> tuple[list[str], list[str]]:
    """Validate scopes against ALLOWED_SCOPES. Returns (valid, invalid)."""
    return validate_api_key_scopes(scopes_str)


@admin_api.route("/api-keys/scopes", methods=["GET"])
def api_key_scopes():
    return jsonify({
        "items": list_api_key_scope_definitions(),
        "default_scopes": list(DEFAULT_API_KEY_SCOPES),
    })


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

    try:
        result = _create_key(
            ctx.cache,
            name=name,
            description=description,
            scopes=scopes,
            created_by=_actor_id(),
            expires_at=expires_at,
        )
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400

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
    try:
        result = _rotate_key(ctx.cache, key_id, created_by=_actor_id())
    except ValueError as exc:
        return jsonify({"error": str(exc)}), 400
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
        slow_requests = ctx.get_slow_requests() if ctx.get_slow_requests else []
        return jsonify(build_performance_summary(
            slow_requests=slow_requests,
            recent_job_runs=ctx.jobs.recent_runs(20),
            threshold_ms=ctx.slow_request_threshold_ms,
            buffer_capacity=ctx.slow_request_buffer_capacity,
            process_started_at=ctx.process_started_at,
        ))
    except Exception as exc:
        return jsonify({"error": str(exc)}), 500

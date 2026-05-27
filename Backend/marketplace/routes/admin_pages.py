"""
Admin page routes for ColorVision Marketplace.

Provides HTML pages for:
  - /admin — overview dashboard
  - /admin/cache — cache and index management
  - /admin/api-keys — API key lifecycle
  - /admin/jobs — scheduled job management
  - /admin/audit — audit log viewer
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable

from flask import Blueprint, abort, redirect, render_template, request, session, url_for

from db_cache import CacheManager


@dataclass(frozen=True)
class AdminPageContext:
    cache: CacheManager
    storage_getter: Callable[[], Path]
    config_getter: Callable[[], dict[str, Any]]
    get_db: Callable[[], Any]
    check_auth: Callable[[], bool]
    human_size: Callable[[int], str]


admin_pages = Blueprint("admin_pages", __name__, url_prefix="/admin")

_ctx: AdminPageContext | None = None


def _get_ctx() -> AdminPageContext:
    if _ctx is None:
        raise RuntimeError("Admin pages not initialized")
    return _ctx


def _require_admin():
    ctx = _get_ctx()
    if not ctx.check_auth():
        return redirect(url_for("login_page", next=request.url))
    return None


@admin_pages.before_request
def _check_auth():
    result = _require_admin()
    if result is not None:
        return result


@admin_pages.route("/")
def overview():
    ctx = _get_ctx()
    status = ctx.cache.get_db_status()
    storage = ctx.storage_getter()

    from services.plugin_index import get_plugin_index_state
    index_state = get_plugin_index_state(ctx.cache)

    from services.scheduler import ensure_default_jobs
    ensure_default_jobs(ctx.cache)

    db = ctx.get_db()
    try:
        jobs = db.execute("SELECT * FROM scheduled_jobs ORDER BY id").fetchall()
        recent_runs = db.execute(
            "SELECT * FROM job_runs ORDER BY id DESC LIMIT 5"
        ).fetchall()
        recent_audit = db.execute(
            "SELECT * FROM audit_log ORDER BY id DESC LIMIT 10"
        ).fetchall()
    finally:
        db.close()

    return render_template(
        "admin_overview.html",
        status=status,
        storage=storage,
        index_state=index_state,
        jobs=[dict(j) for j in jobs],
        recent_runs=[dict(r) for r in recent_runs],
        recent_audit=[dict(a) for a in recent_audit],
        human_size=ctx.human_size,
    )


@admin_pages.route("/cache")
def cache_page():
    ctx = _get_ctx()
    status = ctx.cache.get_db_status()
    storage = ctx.storage_getter()

    from services.plugin_index import get_plugin_index_state
    index_state = get_plugin_index_state(ctx.cache)

    return render_template(
        "admin_cache.html",
        status=status,
        storage=storage,
        index_state=index_state,
        human_size=ctx.human_size,
    )


@admin_pages.route("/api-keys")
def api_keys_page():
    ctx = _get_ctx()
    from services.api_key_service import list_api_keys
    keys = list_api_keys(ctx.cache)
    return render_template("admin_api_keys.html", keys=keys)


@admin_pages.route("/jobs")
def jobs_page():
    ctx = _get_ctx()
    db = ctx.get_db()
    try:
        jobs = db.execute("SELECT * FROM scheduled_jobs ORDER BY id").fetchall()
        job_list = []
        for job in jobs:
            j = dict(job)
            runs = db.execute(
                "SELECT * FROM job_runs WHERE job_id = ? ORDER BY id DESC LIMIT 5",
                (j["id"],),
            ).fetchall()
            j["recent_runs"] = [dict(r) for r in runs]
            job_list.append(j)
    finally:
        db.close()

    return render_template("admin_jobs.html", jobs=job_list)


@admin_pages.route("/audit")
def audit_page():
    ctx = _get_ctx()
    action = request.args.get("action", "").strip() or None
    limit = min(int(request.args.get("limit", 100)), 500)
    offset = int(request.args.get("offset", 0))

    entries = ctx.cache.get_audit_log(action=action, limit=limit, offset=offset)
    return render_template(
        "admin_audit.html",
        entries=entries,
        action_filter=action or "",
        limit=limit,
        offset=offset,
    )


def register_admin_pages(app, ctx: AdminPageContext):
    global _ctx
    _ctx = ctx
    app.register_blueprint(admin_pages)

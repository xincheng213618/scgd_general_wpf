"""
Application setup — service layer initialization, helpers, and context creation.

Extracted from app.py to keep the main module as a thin assembly layer.
"""

from __future__ import annotations

import json
import sqlite3
from pathlib import Path
from typing import Any

from flask import Flask, jsonify, request
from markupsafe import Markup
from werkzeug.exceptions import HTTPException

from config_loader import (
    DEFAULT_SECRET_KEY, DEFAULT_UPLOAD_AUTH, MAX_UPLOAD_SIZE_BYTES,
    get_upload_auth as get_upload_auth_impl, load_config,
    validate_runtime_config as validate_runtime_config_impl,
)
from db_cache import (
    APP_RELEASES_CACHE_KEY, APP_RELEASES_CACHE_TTL_SECONDS,
    CHANGELOG_ANALYSIS_CACHE_KEY, CHANGELOG_ANALYSIS_CACHE_TTL_SECONDS,
    DIRECTORY_COUNT_CACHE_TTL_SECONDS,
    HOME_RELEASES_SNAPSHOT_CACHE_KEY, HOME_RELEASES_SNAPSHOT_TTL_SECONDS,
    HOME_TOOL_PREVIEW_CACHE_KEY, HOME_TOOL_PREVIEW_TTL_SECONDS,
    MARKDOWN_RENDER_CACHE_TTL_SECONDS,
    OVERVIEW_CACHE_KEY, OVERVIEW_CACHE_TTL_SECONDS,
    PLUGIN_INFO_CACHE_TTL_SECONDS,
    RELEASE_TIMELINE_CACHE_KEY, RELEASE_TIMELINE_CACHE_TTL_SECONDS,
    CacheManager,
)
from context import MarketplaceContext
from storage_paths import (
    is_safe_id, is_safe_version,
    normalize_relative_path, sanitize_filename,
    storage_target as storage_target_impl,
)

try:
    import markdown as _markdown_mod
except ImportError:
    _markdown_mod = None


def human_size(size_bytes: int) -> str:
    for unit in ("B", "KB", "MB", "GB"):
        if abs(size_bytes) < 1024:
            return f"{size_bytes:.1f} {unit}"
        size_bytes /= 1024
    return f"{size_bytes:.1f} TB"


def render_markdown(text: str | None) -> Markup:
    if not text:
        return Markup("")
    if _markdown_mod is None:
        return Markup(f'<pre style="white-space:pre-wrap;margin:0;">{Markup.escape(text)}</pre>')
    return Markup(_markdown_mod.markdown(text, extensions=["extra", "sane_lists", "nl2br"], output_format="html5"))


def _dynamic_storage():
    """Read storage from app.STORAGE so test mutations are reflected."""
    import app as _app
    return _app.STORAGE


def _dynamic_config():
    """Read config from app.CONFIG so test mutations are reflected."""
    import app as _app
    return _app.CONFIG


def create_app_and_context():
    """Create Flask app, CacheManager, services, and context.

    Returns (app, ctx, SERVICES) where ctx is a MarketplaceContext.
    """
    from db.schema_version import ensure_schema_version
    from marketplace_services import MarketplaceCacheSettings, MarketplaceDataService
    from services.auth_middleware import check_web_session_auth, make_require_upload_auth

    base_dir = Path(__file__).resolve().parent
    config = load_config()
    storage = Path(config["storage_path"])

    app = Flask(__name__, static_folder=None)
    app.secret_key = config["secret_key"]
    app.config["MAX_CONTENT_LENGTH"] = MAX_UPLOAD_SIZE_BYTES

    db_path = base_dir / "marketplace.db"
    cache = CacheManager(db_path)
    cache.init_db()

    conn = cache.get_db()
    ensure_schema_version(conn)
    conn.close()

    # Helpers — get_db reads from app.DB_PATH so test mutations are reflected
    def get_db():
        import app as _app
        db = sqlite3.connect(str(_app.DB_PATH))
        db.row_factory = sqlite3.Row
        return db

    def get_upload_auth():
        import app as _app
        return get_upload_auth_impl(_app.CONFIG)

    def json_error(message, status_code, **details):
        payload = {"error": message, "status": status_code}
        if details:
            payload["details"] = details
        resp = jsonify(payload)
        resp.status_code = status_code
        return resp

    def render_markdown_cached(*, cache_key, signature, text):
        cached = cache.get_cache_entry(cache_key, signature=signature)
        if cached:
            return Markup(str(cached["value"]))
        rendered = render_markdown(text)
        cache.set_cache_entry(cache_key, str(rendered), ttl_seconds=MARKDOWN_RENDER_CACHE_TTL_SECONDS, signature=signature)
        return rendered

    def read_text_file(p: Path) -> str | None:
        try:
            return p.read_text(encoding="utf-8").strip()
        except (OSError, UnicodeDecodeError):
            return None

    require_upload_auth = make_require_upload_auth(cache, get_upload_auth, json_error)

    # Service layer — storage_getter reads from app.STORAGE so test mutations are reflected
    services = MarketplaceDataService(
        storage_getter=_dynamic_storage,
        config_getter=_dynamic_config,
        get_cache_entry=cache.get_cache_entry,
        set_cache_entry=cache.set_cache_entry,
        refresh_related_caches=cache.refresh_related_caches,
        get_db=get_db,
        read_text_file=read_text_file,
        render_markdown_cached=render_markdown_cached,
        cache_settings=MarketplaceCacheSettings(
            overview_cache_key=OVERVIEW_CACHE_KEY,
            overview_cache_ttl_seconds=OVERVIEW_CACHE_TTL_SECONDS,
            app_releases_cache_key=APP_RELEASES_CACHE_KEY,
            app_releases_cache_ttl_seconds=APP_RELEASES_CACHE_TTL_SECONDS,
            directory_count_cache_ttl_seconds=DIRECTORY_COUNT_CACHE_TTL_SECONDS,
            plugin_info_cache_ttl_seconds=PLUGIN_INFO_CACHE_TTL_SECONDS,
            changelog_analysis_cache_key=CHANGELOG_ANALYSIS_CACHE_KEY,
            changelog_analysis_cache_ttl_seconds=CHANGELOG_ANALYSIS_CACHE_TTL_SECONDS,
            home_releases_snapshot_cache_key=HOME_RELEASES_SNAPSHOT_CACHE_KEY,
            home_releases_snapshot_ttl_seconds=HOME_RELEASES_SNAPSHOT_TTL_SECONDS,
            home_tool_preview_cache_key=HOME_TOOL_PREVIEW_CACHE_KEY,
            home_tool_preview_ttl_seconds=HOME_TOOL_PREVIEW_TTL_SECONDS,
            release_timeline_cache_key=RELEASE_TIMELINE_CACHE_KEY,
            release_timeline_cache_ttl_seconds=RELEASE_TIMELINE_CACHE_TTL_SECONDS,
        ),
        cache_manager=cache,
    )

    ctx = MarketplaceContext(
        config=config, _storage=storage, db_path=db_path, cache=cache,
        get_db=get_db, init_db=lambda: None,
        is_safe_id=is_safe_id, is_safe_version=is_safe_version,
        sanitize_filename=sanitize_filename, normalize_relative_path=normalize_relative_path,
        storage_target=lambda rp: storage_target_impl(storage, rp),
        get_cache_entry=cache.get_cache_entry, set_cache_entry=cache.set_cache_entry,
        invalidate_cache_prefix=cache.invalidate_cache_prefix,
        refresh_related_caches=cache.refresh_related_caches,
        get_upload_auth=get_upload_auth,
        services=services, human_size=human_size, render_markdown=render_markdown,
        render_markdown_cached=render_markdown_cached,
        json_error=json_error,
    )

    return app, ctx, services, {
        "config": config, "storage": storage, "db_path": db_path, "cache": cache,
        "get_db": get_db, "get_upload_auth": get_upload_auth, "json_error": json_error,
        "require_upload_auth": require_upload_auth, "read_text_file": read_text_file,
        "render_markdown_cached": render_markdown_cached,
    }


def register_error_handlers(app):
    from werkzeug.exceptions import HTTPException

    @app.errorhandler(HTTPException)
    def handle_http_exception(exc: HTTPException):
        if request.path.startswith("/api/"):
            payload = {"error": exc.description or exc.name, "status": exc.code or 500}
            resp = jsonify(payload)
            resp.status_code = exc.code or 500
            return resp
        return exc


def register_slow_request_logging(app, ctx: MarketplaceContext):
    import time as _time

    @app.before_request
    def _start_timer():
        request._start_time = _time.monotonic()

    @app.after_request
    def _log_slow_request(response):
        start = getattr(request, "_start_time", None)
        if start is not None:
            duration_ms = int((_time.monotonic() - start) * 1000)
            if duration_ms >= ctx.slow_request_threshold_ms:
                print(f"[slow] {request.method} {request.path} → {response.status_code} ({duration_ms}ms)")
                ctx.slow_requests.append({
                    "method": request.method, "path": request.path,
                    "status": response.status_code, "duration_ms": duration_ms,
                })
                if len(ctx.slow_requests) > 100:
                    ctx.slow_requests.pop(0)
        return response


def register_all_blueprints(app, ctx, services, helpers):
    """Register all blueprints on the app."""
    from routes.public_pages import PublicPageContext, register_public_pages
    from routes.health_api import register_health_api
    from routes.public_api import register_public_api
    from routes.pages import register_pages
    from routes.cvws_api import register_cvws_api
    from routes.admin_api import AdminApiContext, register_admin_api_routes
    from routes.docs_site import register_docs_site
    from routes.frontend_spa import FrontendSpaContext, register_frontend_spa
    from marketplace_api_routes import MarketplaceApiRouteContext, register_marketplace_api_routes
    from catalog_view_models import (
        ALLOWED_CATALOG_SORTS, ALLOWED_CATALOG_SORT_ORDERS,
        build_plugin_search_api_result, build_plugin_detail_api_result,
        collect_catalog_categories, normalize_catalog_sort_name,
    )
    from package_publish import extract_package_version as _extract_pkg_ver
    from plugin_marketplace import prewarm_plugin_metadata
    from services.storage_events import _refresh_plugin_index

    cache = helpers["cache"]
    config = helpers["config"]
    storage = _dynamic_storage()  # Always reads from app.STORAGE

    # Public pages (login/logout)
    register_public_pages(app, PublicPageContext(
        cache=cache, storage=storage, config=config,
        get_upload_auth=helpers["get_upload_auth"],
        check_web_session_auth=lambda: __import__("flask").session.get("authenticated", False),
        dist_dir=Path(__file__).resolve().parents[1] / "Frontend" / "dist",
    ))

    # Health
    register_health_api(app, ctx)

    # Public API (stats, feedback, legacy)
    register_public_api(app, ctx)

    # Page routes
    register_pages(app, services)

    # CVWindowsService
    register_cvws_api(app, ctx)

    # Marketplace API (plugin search, publish, download)
    def _refresh_plugin_index_on_publish(plugin_id):
        _refresh_plugin_index(
            cache, _dynamic_storage(), f"Plugins/{plugin_id}",
            get_download_counts=services.get_download_counts,
            get_cache_entry=cache.get_cache_entry,
            set_cache_entry=cache.set_cache_entry,
            ttl_seconds=PLUGIN_INFO_CACHE_TTL_SECONDS,
            get_request_username=ctx.get_request_username,
        )

    from flask import request as _req
    register_marketplace_api_routes(app, MarketplaceApiRouteContext(
        get_storage=_dynamic_storage,
        max_upload_size_bytes=MAX_UPLOAD_SIZE_BYTES,
        parse_int_arg=lambda *a, **kw: _parse_int_arg(_req, *a, **kw),
        normalize_catalog_sort_name=normalize_catalog_sort_name,
        allowed_catalog_sorts=ALLOWED_CATALOG_SORTS,
        allowed_catalog_sort_orders=ALLOWED_CATALOG_SORT_ORDERS,
        build_plugin_search_api_result=build_plugin_search_api_result,
        build_plugin_detail_api_result=lambda info, *, icon_url_builder: build_plugin_detail_api_result(
            info,
            icon_url_builder=icon_url_builder,
            render_markdown=helpers["render_markdown_cached"],
        ),
        collect_catalog_categories=collect_catalog_categories,
        get_request_plugin_catalog=lambda: services.get_request_plugin_catalog(),
        build_plugin_icon_url=lambda pid: f"/plugins/{pid}/icon",
        get_plugin_info=services.get_plugin_info,
        get_request_download_counts=services.get_request_download_counts,
        read_text_file=helpers["read_text_file"],
        is_safe_id=is_safe_id, is_safe_version=is_safe_version,
        sanitize_filename=sanitize_filename,
        version_tuple=lambda v: tuple(int(x) for x in v.split(".") if x.isdigit()),
        extract_package_version=lambda fn, pid: _extract_pkg_ver(fn, pid, sanitize_filename=sanitize_filename, validate_version=is_safe_version),
        load_manifest=lambda p: json.loads(p.read_text(encoding="utf-8")) if p.exists() else {},
        refresh_related_caches=cache.refresh_related_caches,
        get_download_counts=services.get_download_counts,
        get_cache_entry=cache.get_cache_entry,
        set_cache_entry=cache.set_cache_entry,
        record_download=lambda pid, ver: services.record_download(pid, ver),
        normalize_relative_path=normalize_relative_path,
        is_root_release_file=lambda p: p.parent == storage and p.suffix.lower() in (".exe", ".zip", ".rar"),
        reconcile_app_release_history=services.reconcile_app_release_history,
        reconcile_plugin_package_history=services.reconcile_plugin_package_history,
        require_upload_auth=helpers["require_upload_auth"],
        refresh_plugin_index_on_publish=_refresh_plugin_index_on_publish,
        cache=cache,
    ))

    # Admin API
    import hmac as _hmac

    def _check_admin_auth(required_scopes=None):
        from flask import session as _session
        if _session.get("authenticated"):
            return True
        auth_header = _req.headers.get("Authorization", "")
        if auth_header.startswith("Bearer "):
            token = auth_header[7:].strip()
            if token:
                from services.api_key_service import verify_api_key
                scopes = required_scopes or ["admin:*"]
                if verify_api_key(cache, token, required_scopes=scopes):
                    return True
        auth = _req.authorization
        if auth and (auth.type or "").lower() == "basic" and auth.username and auth.password:
            eu, ep = helpers["get_upload_auth"]()
            if eu and ep and _hmac.compare_digest(auth.username, eu) and _hmac.compare_digest(auth.password, ep):
                return True
        return False

    def _check_transfer_auth(required_scopes=None):
        from flask import session as _session
        if _session.get("authenticated") or _session.get("user_authenticated"):
            return True
        auth_header = _req.headers.get("Authorization", "")
        if auth_header.startswith("Bearer "):
            token = auth_header[7:].strip()
            if token:
                from services.api_key_service import verify_api_key
                scopes = required_scopes or ["file:transfer"]
                if verify_api_key(cache, token, required_scopes=scopes):
                    return True
        auth = _req.authorization
        if auth and (auth.type or "").lower() == "basic" and auth.username and auth.password:
            eu, ep = helpers["get_upload_auth"]()
            if eu and ep and _hmac.compare_digest(auth.username, eu) and _hmac.compare_digest(auth.password, ep):
                return True
        return False

    from routes.transfer import TransferRouteContext, register_transfer_routes
    register_transfer_routes(app, TransferRouteContext(
        cache=cache, storage_getter=_dynamic_storage, config_getter=_dynamic_config,
        check_auth=_check_transfer_auth, human_size=human_size,
    ))

    register_admin_api_routes(app, AdminApiContext(
        cache=cache, storage_getter=_dynamic_storage, config_getter=_dynamic_config,
        get_db=helpers["get_db"], check_auth=_check_admin_auth,
        require_auth_decorator=helpers["require_upload_auth"],
        refresh_plugin_index=lambda c, s, pid, **kw: __import__("services.plugin_index", fromlist=["refresh_plugin_index"]).refresh_plugin_index(c, s, pid, **kw),
        refresh_all_plugin_index=lambda c, s, **kw: __import__("services.plugin_index", fromlist=["refresh_all_plugin_index"]).refresh_all_plugin_index(c, s, **kw),
        get_plugin_index_state=lambda c: __import__("services.plugin_index", fromlist=["get_plugin_index_state"]).get_plugin_index_state(c),
        is_plugin_index_populated=lambda c: __import__("services.plugin_index", fromlist=["is_plugin_index_populated"]).is_plugin_index_populated(c),
        get_plugin_catalog_from_index=lambda c, dc: __import__("services.plugin_index", fromlist=["get_plugin_catalog_from_index"]).get_plugin_catalog_from_index(c, dc),
        human_size=human_size,
        get_slow_requests=lambda: ctx.slow_requests,
    ))

    from routes.operations_relay import OperationsRelayContext, register_operations_relay_routes
    register_operations_relay_routes(app, OperationsRelayContext(cache=cache))

    register_docs_site(app)

    register_frontend_spa(app, FrontendSpaContext(
        check_auth=_check_admin_auth,
        dist_dir=Path(__file__).resolve().parents[1] / "Frontend" / "dist",
    ))

    try:
        from services.app_latest_version_cache import (
            warm_latest_version_cache,
            warm_plugin_latest_versions_cache,
        )
        warm_latest_version_cache(_dynamic_storage())
        warm_plugin_latest_versions_cache(_dynamic_storage(), cache)
        from services.docs_site import get_docs_index
        get_docs_index(cache, refresh_if_missing=True)
    except Exception as exc:
        print(f"[version_cache] startup warm failed: {exc}")


def _parse_int_arg(req, *names, default, minimum=None, maximum=None):
    raw = None
    for name in names:
        if name in req.args:
            raw = req.args.get(name)
            break
    if raw is None or str(raw).strip() == "":
        value = default
    else:
        try:
            value = int(str(raw).strip())
        except (TypeError, ValueError):
            from flask import abort
            abort(400, description=f"Invalid integer parameter: {names[0]}")
    if minimum is not None and value < minimum:
        from flask import abort
        abort(400, description=f"{names[0]} must be >= {minimum}")
    if maximum is not None and value > maximum:
        from flask import abort
        abort(400, description=f"{names[0]} must be <= {maximum}")
    return value

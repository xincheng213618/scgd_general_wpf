"""
Application setup — service layer initialization, helpers, and context creation.

Extracted from app.py to keep the main module as a thin assembly layer.
"""

from __future__ import annotations

import atexit
import sqlite3
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable

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


@dataclass
class RuntimeState:
    """Mutable runtime values owned by the composition root."""

    config: dict[str, Any]
    storage: Path
    db_path: Path


@dataclass(frozen=True)
class RuntimeOverrides:
    """Outermost compatibility adapter for legacy ``app`` mutations.

    Remove after external consumers stop assigning ``app.CONFIG``,
    ``app.STORAGE`` and ``app.DB_PATH`` directly.
    """

    config: Callable[[], dict[str, Any] | None]
    storage: Callable[[], Path | None]
    db_path: Callable[[], Path | None]


def create_app_and_context(runtime_overrides: RuntimeOverrides | None = None):
    """Create Flask app, CacheManager, services, and context.

    Returns (app, ctx, SERVICES) where ctx is a MarketplaceContext.
    """
    from db.schema_version import ensure_schema_version
    from marketplace_services import MarketplaceCacheSettings, MarketplaceDataService
    from routes.auth_adapters import check_web_session_auth, make_require_upload_auth
    from routes.request_context import current_request_context
    from services.auth_policy import AuthPolicy

    base_dir = Path(__file__).resolve().parent
    config = load_config()
    storage = Path(config["storage_path"])

    app = Flask(__name__, static_folder=None)
    app.secret_key = config["secret_key"]
    app.config["MAX_CONTENT_LENGTH"] = MAX_UPLOAD_SIZE_BYTES
    app.config["SESSION_COOKIE_HTTPONLY"] = True
    app.config["SESSION_COOKIE_SAMESITE"] = "Lax"
    # JSON is UTF-8 on the wire. Escaping every Chinese character only inflates
    # public API responses without adding browser compatibility.
    app.json.ensure_ascii = False

    db_path = base_dir / "marketplace.db"
    runtime = RuntimeState(config=config, storage=storage, db_path=db_path)

    def active_config() -> dict[str, Any]:
        override = runtime_overrides.config() if runtime_overrides else None
        return override if override is not None else runtime.config

    def active_storage() -> Path:
        override = runtime_overrides.storage() if runtime_overrides else None
        return Path(override) if override is not None else runtime.storage

    def active_db_path() -> Path:
        override = runtime_overrides.db_path() if runtime_overrides else None
        return Path(override) if override is not None else runtime.db_path

    cache = CacheManager(db_path)
    cache.init_db()

    from services.access_analytics import (
        AccessAnalyticsRecorder,
        configure_access_analytics_calendar,
        reporting_utc_offset_minutes,
    )

    analytics_utc_offset_minutes = reporting_utc_offset_minutes(config)
    conn = cache.get_db()
    ensure_schema_version(conn)
    configure_access_analytics_calendar(
        conn,
        utc_offset_minutes=analytics_utc_offset_minutes,
    )
    conn.close()

    access_recorder = AccessAnalyticsRecorder(
        queue_capacity=int(config.get("access_analytics_queue_size", 4096) or 4096),
        batch_size=int(config.get("access_analytics_batch_size", 128) or 128),
        flush_interval_seconds=float(
            config.get("access_analytics_flush_interval_seconds", 0.5) or 0.5
        ),
    )
    atexit.register(access_recorder.close)

    def get_db():
        db = sqlite3.connect(str(active_db_path()))
        db.row_factory = sqlite3.Row
        return db

    def get_upload_auth():
        return get_upload_auth_impl(active_config())

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

    auth_policy = AuthPolicy(cache, get_upload_auth)
    require_upload_auth = make_require_upload_auth(auth_policy, json_error)

    services = MarketplaceDataService(
        storage_getter=active_storage,
        config_getter=active_config,
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
        storage_getter=active_storage, config_getter=active_config,
        db_path_getter=active_db_path,
        get_db=get_db, init_db=lambda: None,
        is_safe_id=is_safe_id, is_safe_version=is_safe_version,
        sanitize_filename=sanitize_filename, normalize_relative_path=normalize_relative_path,
        storage_target=lambda rp: storage_target_impl(active_storage(), rp),
        get_cache_entry=cache.get_cache_entry, set_cache_entry=cache.set_cache_entry,
        invalidate_cache_prefix=cache.invalidate_cache_prefix,
        refresh_related_caches=cache.refresh_related_caches,
        get_upload_auth=get_upload_auth,
        auth_policy=auth_policy,
        request_context_factory=current_request_context,
        services=services, human_size=human_size, render_markdown=render_markdown,
        render_markdown_cached=render_markdown_cached,
        json_error=json_error,
    )

    return app, ctx, services, {
        "config": config, "storage": storage, "db_path": db_path, "cache": cache,
        "get_db": get_db, "get_upload_auth": get_upload_auth, "json_error": json_error,
        "require_upload_auth": require_upload_auth, "read_text_file": read_text_file,
        "auth_policy": auth_policy, "request_context_factory": current_request_context,
        "check_web_session_auth": check_web_session_auth,
        "runtime": runtime, "active_config": active_config,
        "active_storage": active_storage, "active_db_path": active_db_path,
        "render_markdown_cached": render_markdown_cached,
        "access_recorder": access_recorder,
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


def register_slow_request_logging(app, ctx: MarketplaceContext, access_recorder=None):
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
            if access_recorder is not None:
                try:
                    from services.access_analytics import (
                        build_access_event,
                        declared_response_body_bytes,
                        reporting_utc_offset_minutes,
                        should_record_access,
                    )

                    route_rule = request.url_rule.rule if request.url_rule is not None else None
                    config = getattr(ctx, "active_config", None) or load_config()
                    if (
                        config.get("access_analytics_enabled", True)
                        and should_record_access(route_rule, request.method)
                    ):
                        content_length = declared_response_body_bytes(
                            method=request.method,
                            status_code=response.status_code,
                            content_length=response.headers.get("Content-Length"),
                        )
                        event = build_access_event(
                            route_template=route_rule,
                            method=request.method,
                            status_code=response.status_code,
                            duration_ms=duration_ms,
                            response_bytes=content_length,
                            secret_key=str(config.get("secret_key", "")),
                            remote_addr=request.remote_addr,
                            user_agent=request.headers.get("User-Agent", ""),
                            utc_offset_minutes=reporting_utc_offset_minutes(
                                config
                            ),
                        )
                        access_recorder.submit(
                            event,
                            db_path=getattr(
                                ctx,
                                "active_db_path",
                                Path(__file__).resolve().parent / "marketplace.db",
                            ),
                            synchronous=bool(app.config.get("TESTING")),
                        )
                except Exception as exc:
                    print(f"[access_analytics] request event dropped: {exc}")
        return response


def register_all_blueprints(app, ctx, services, helpers):
    """Register all blueprints on the app."""
    from routes.public_pages import PublicPageContext, register_public_pages
    from routes.health_api import register_health_api
    from routes.public_api import register_public_api
    from routes.pages import register_pages
    from routes.cvws_api import register_cvws_api
    from routes.spectrum_api import register_spectrum_api
    from routes.admin_api import AdminApiContext, register_admin_api_routes
    from routes.copilot_config_api import (
        CopilotConfigApiContext,
        register_copilot_config_api_routes,
    )
    from routes.docs_site import register_docs_site
    from routes.frontend_spa import FrontendSpaContext, register_frontend_spa
    from marketplace_api_routes import MarketplaceApiRouteContext, register_marketplace_api_routes
    from services.marketplace_api import (
        MarketplaceApiServices,
        MarketplaceCatalogService,
        MarketplacePackageService,
        MarketplaceStorageService,
    )
    from services.artifact_delivery import ArtifactDeliveryService

    cache = helpers["cache"]
    config = helpers["config"]
    storage = ctx.storage
    artifact_delivery = ArtifactDeliveryService()
    ctx.artifact_delivery = artifact_delivery

    # Public pages (login/logout)
    register_public_pages(app, PublicPageContext(
        cache=cache, storage=storage, config=config,
        get_upload_auth=helpers["get_upload_auth"],
        check_web_session_auth=helpers["check_web_session_auth"],
        dist_dir=Path(__file__).resolve().parents[1] / "Frontend" / "dist",
    ))

    # Health
    register_health_api(app, ctx)

    # Public API (stats, feedback, legacy)
    register_public_api(app, ctx)

    # Page routes
    register_pages(app, ctx)

    # CVWindowsService
    register_cvws_api(app, ctx)

    # Standalone Spectrum release/update contract
    register_spectrum_api(app, ctx)

    # Marketplace API (plugin search, publish, download)
    marketplace_storage = MarketplaceStorageService(lambda: ctx.storage)
    marketplace_api_services = MarketplaceApiServices(
        catalog=MarketplaceCatalogService(
            services,
            cache,
            storage_getter=lambda: ctx.storage,
            render_markdown=helpers["render_markdown_cached"],
        ),
        packages=MarketplacePackageService(
            services,
            cache,
            marketplace_storage,
            max_upload_size_bytes=MAX_UPLOAD_SIZE_BYTES,
        ),
        storage=marketplace_storage,
        delivery=artifact_delivery,
    )
    register_marketplace_api_routes(app, MarketplaceApiRouteContext(
        services=marketplace_api_services,
        require_upload_auth=helpers["require_upload_auth"],
        request_context_factory=helpers["request_context_factory"],
    ))

    def _check_admin_auth(required_scopes=None):
        from routes.request_context import set_authenticated_request_context

        request_context = helpers["request_context_factory"]()
        decision = helpers["auth_policy"].authorize(
            request_context,
            required_scopes or ["admin:*"],
        )
        if decision.allowed:
            set_authenticated_request_context(request_context.with_actor(decision.principal))
        return decision.allowed

    def _check_transfer_auth(required_scopes=None):
        from routes.request_context import set_authenticated_request_context

        request_context = helpers["request_context_factory"]()
        decision = helpers["auth_policy"].authorize(
            request_context,
            required_scopes or ["file:transfer"],
            allow_user_session=True,
        )
        if decision.allowed:
            set_authenticated_request_context(request_context.with_actor(decision.principal))
        return decision.allowed

    from routes.transfer import TransferRouteContext, register_transfer_routes
    register_transfer_routes(app, TransferRouteContext(
        cache=cache, storage_getter=lambda: ctx.storage,
        config_getter=lambda: ctx.active_config,
        check_auth=_check_transfer_auth, human_size=human_size,
        artifact_delivery=artifact_delivery,
    ))

    register_admin_api_routes(app, AdminApiContext(
        cache=cache, jobs=cache.jobs,
        storage_getter=lambda: ctx.storage,
        config_getter=lambda: ctx.active_config,
        get_db=helpers["get_db"],
        auth_policy=helpers["auth_policy"],
        request_context_factory=helpers["request_context_factory"],
        refresh_plugin_index=lambda c, s, pid, **kw: __import__("services.plugin_index", fromlist=["refresh_plugin_index"]).refresh_plugin_index(c, s, pid, **kw),
        refresh_all_plugin_index=lambda c, s, **kw: __import__("services.plugin_index", fromlist=["refresh_all_plugin_index"]).refresh_all_plugin_index(c, s, **kw),
        get_plugin_index_state=lambda c: __import__("services.plugin_index", fromlist=["get_plugin_index_state"]).get_plugin_index_state(c),
        is_plugin_index_populated=lambda c: __import__("services.plugin_index", fromlist=["is_plugin_index_populated"]).is_plugin_index_populated(c),
        get_plugin_catalog_from_index=lambda c, dc: __import__("services.plugin_index", fromlist=["get_plugin_catalog_from_index"]).get_plugin_catalog_from_index(c, dc),
        human_size=human_size,
        get_slow_requests=lambda: ctx.slow_requests,
        get_access_recorder_status=helpers["access_recorder"].status,
    ))
    register_copilot_config_api_routes(app, CopilotConfigApiContext(
        cache=cache,
        config_getter=lambda: ctx.active_config,
    ))

    from db.repositories.operations_support import SqliteOperationsSupportStore
    from routes.operations_relay import OperationsRelayContext, register_operations_relay_routes
    register_operations_relay_routes(app, OperationsRelayContext(
        cache=cache,
        support_store=SqliteOperationsSupportStore(cache.get_db),
    ))

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
        warm_latest_version_cache(ctx.storage)
        warm_plugin_latest_versions_cache(ctx.storage, cache)
        from services.docs_site import get_docs_index
        get_docs_index(cache, refresh_if_missing=True)
    except Exception as exc:
        print(f"[version_cache] startup warm failed: {exc}")

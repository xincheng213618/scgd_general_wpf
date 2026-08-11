"""
ColorVision Plugin Marketplace — Python/Flask Backend

Thin assembly layer. All logic lives in:
  - app_setup.py    — initialization, service creation, blueprint registration
  - context.py      — dependency container
  - cli.py          — CLI commands
  - routes/         — route blueprints
  - services/       — business logic
  - db/             — database schema and migrations

Run:
  python app.py                        # uses config.json
  python app.py --storage /path/to/dir # override storage path
"""

import json
from pathlib import Path

# Deprecated compatibility globals. Core composition receives these through
# explicit outer accessors and never imports this module. Remove them only when
# tests, publishing scripts and external WSGI consumers stop mutating them.
LEGACY_APP_COMPAT_REMOVAL = (
    "Remove after repository tests, publishing scripts, and external WSGI "
    "consumers no longer import app globals or thin helper re-exports."
)
CONFIG = None
STORAGE = None
DB_PATH = None

# ---------------------------------------------------------------------------
# Create app, services, and context via app_setup
# ---------------------------------------------------------------------------

from app_setup import (
    create_app_and_context, register_error_handlers,
    register_slow_request_logging, register_all_blueprints,
    human_size, render_markdown, RuntimeOverrides,
)
from db_cache import CacheManager
from services.http_compression import register_response_compression

app, _ctx, SERVICES, _helpers = create_app_and_context(RuntimeOverrides(
    config=lambda: CONFIG,
    storage=lambda: STORAGE,
    db_path=lambda: DB_PATH,
))

# ---------------------------------------------------------------------------
# Module-level globals (kept for test backward compatibility)
# ---------------------------------------------------------------------------

CONFIG = _helpers["runtime"].config
STORAGE = _helpers["runtime"].storage
DB_PATH = _helpers["runtime"].db_path
_cache = _ctx.cache

# Re-export for tests that mutate these
MAX_UPLOAD_SIZE_BYTES = app.config["MAX_CONTENT_LENGTH"]
DEFAULT_SECRET_KEY = __import__("config_loader").DEFAULT_SECRET_KEY
DEFAULT_UPLOAD_AUTH = __import__("config_loader").DEFAULT_UPLOAD_AUTH
MAX_FEEDBACK_FILES = __import__("config_loader").MAX_FEEDBACK_FILES
PLUGIN_INFO_CACHE_TTL_SECONDS = __import__("db_cache").PLUGIN_INFO_CACHE_TTL_SECONDS
CVWS_RELEASES_CACHE_KEY = "cvws_releases:v1"
CVWS_RELEASES_CACHE_TTL_SECONDS = 180

# Deprecated, stateless compatibility wrappers. New code must use MarketplaceContext
# or a typed service. The removal condition is LEGACY_APP_COMPAT_REMOVAL above.
def get_db():
    return _helpers["get_db"]()

def init_db():
    _cache._db_path = DB_PATH
    _cache.init_db()

def _set_cache_entry(key, value, *, ttl_seconds, signature=""):
    _cache.set_cache_entry(key, value, ttl_seconds=ttl_seconds, signature=signature)

def _get_cache_entry(key, *, signature=None):
    return _cache.get_cache_entry(key, signature=signature)

def _invalidate_cache_prefix(prefix):
    _cache.invalidate_cache_prefix(prefix)

def _refresh_related_caches(*, plugin_id=None, relative_path="", invalidate_plugin_catalog=True):
    _cache.refresh_related_caches(plugin_id=plugin_id, relative_path=relative_path,
                                   invalidate_plugin_catalog=invalidate_plugin_catalog)

def _get_upload_auth():
    return _helpers["get_upload_auth"]()

def _json_error(message, status_code, **details):
    return _helpers["json_error"](message, status_code, **details)

def _validate_runtime_config(config):
    from config_loader import validate_runtime_config as _vrc
    return _vrc(config, default_secret_key=DEFAULT_SECRET_KEY, default_upload_auth=DEFAULT_UPLOAD_AUTH)

def _load_manifest(manifest_path):
    from package_publish import load_manifest
    return load_manifest(manifest_path)

def _storage_target(relative_path):
    from storage_paths import storage_target as _st
    return _st(STORAGE, relative_path)

def _is_safe_id(value):
    from storage_paths import is_safe_id
    return is_safe_id(value)

def _is_safe_version(value):
    from storage_paths import is_safe_version
    return is_safe_version(value)

def _sanitize_filename(fn):
    from storage_paths import sanitize_filename
    return sanitize_filename(fn)

def _normalize_relative_path(rp):
    from storage_paths import normalize_relative_path
    return normalize_relative_path(rp)

require_upload_auth = _helpers["require_upload_auth"]

# ---------------------------------------------------------------------------
# Register middleware and blueprints
# ---------------------------------------------------------------------------

register_error_handlers(app)
register_slow_request_logging(app, _ctx, _helpers["access_recorder"])
register_response_compression(app)
register_all_blueprints(app, _ctx, SERVICES, _helpers)

# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    from cli import build_parser, handle_cli_args
    from services.scheduler import ensure_default_jobs, SchedulerThread
    from services.auth_service import ensure_admin_user
    import os as _os

    parser = build_parser()
    args = parser.parse_args()

    if args.storage:
        STORAGE = Path(args.storage)
    if args.port:
        CONFIG["port"] = args.port
    if args.debug:
        CONFIG["debug"] = True

    is_debug = CONFIG.get("debug", False)
    if not is_debug:
        try:
            from services.runtime_logging import install_runtime_logging
            runtime_log_path = install_runtime_logging(STORAGE)
            print(f"[runtime] Persistent log: {runtime_log_path}")
        except Exception as exc:
            print(f"[runtime] Persistent logging unavailable: {exc}")

    try:
        from services.app_latest_version_cache import (
            warm_latest_version_cache,
            warm_plugin_latest_versions_cache,
        )
        warm_latest_version_cache(STORAGE)
        warm_plugin_latest_versions_cache(STORAGE, _cache)
    except Exception as exc:
        print(f"[version_cache] startup warm failed: {exc}")

    handle_cli_args(args, cache=_cache, storage=STORAGE, config=CONFIG, get_db=get_db,
                    validate_runtime_config=_validate_runtime_config,
                    reconcile_app_release_history=SERVICES.reconcile_app_release_history,
                    reconcile_all_plugin_package_histories=SERVICES.reconcile_all_plugin_package_histories,
                    prune_update_packages=lambda s: __import__("update_retention").prune_update_packages(s))

    print(f"Storage path: {STORAGE}")
    print(f"Listening on: http://{CONFIG['host']}:{CONFIG['port']}")

    ensure_default_jobs(_cache)
    ensure_admin_user(_cache, CONFIG)

    scheduler_enabled = CONFIG.get("scheduler_enabled", True)
    is_reloader = _os.environ.get("WERKZEUG_RUN_MAIN") == "true"
    if scheduler_enabled and (not is_debug or is_reloader):
        _scheduler = SchedulerThread(_cache, lambda: STORAGE, lambda: CONFIG, get_db)
        _scheduler.start()
        print("[scheduler] Background scheduler started")

    if is_debug:
        print("WARNING: Running in debug mode. Do not use in production.")
        app.run(host=CONFIG["host"], port=CONFIG["port"], debug=True)
    else:
        app.run(host=CONFIG["host"], port=CONFIG["port"])

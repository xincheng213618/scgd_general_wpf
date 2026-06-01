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

# ---------------------------------------------------------------------------
# Create app, services, and context via app_setup
# ---------------------------------------------------------------------------

from app_setup import (
    create_app_and_context, register_error_handlers,
    register_slow_request_logging, register_all_blueprints,
    human_size, render_markdown,
)
from db_cache import CacheManager

app, _ctx, SERVICES, _helpers = create_app_and_context()

# ---------------------------------------------------------------------------
# Module-level globals (kept for test backward compatibility)
# ---------------------------------------------------------------------------

CONFIG = _ctx.config
STORAGE = _ctx._storage
DB_PATH = _ctx.db_path
_cache = _ctx.cache

# Re-export for tests that mutate these
MAX_UPLOAD_SIZE_BYTES = app.config["MAX_CONTENT_LENGTH"]
DEFAULT_SECRET_KEY = __import__("config_loader").DEFAULT_SECRET_KEY
DEFAULT_UPLOAD_AUTH = __import__("config_loader").DEFAULT_UPLOAD_AUTH
MAX_FEEDBACK_FILES = __import__("config_loader").MAX_FEEDBACK_FILES
PLUGIN_INFO_CACHE_TTL_SECONDS = __import__("db_cache").PLUGIN_INFO_CACHE_TTL_SECONDS
CVWS_RELEASES_CACHE_KEY = "cvws_releases:v1"
CVWS_RELEASES_CACHE_TTL_SECONDS = 180

# Thin wrappers
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
register_slow_request_logging(app, _ctx)
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
    is_debug = CONFIG.get("debug", False)
    if scheduler_enabled and (not is_debug or is_reloader):
        _scheduler = SchedulerThread(_cache, lambda: STORAGE, lambda: CONFIG, get_db)
        _scheduler.start()
        print("[scheduler] Background scheduler started")

    if is_debug:
        print("WARNING: Running in debug mode. Do not use in production.")
        app.run(host=CONFIG["host"], port=CONFIG["port"], debug=True)
    else:
        app.run(host=CONFIG["host"], port=CONFIG["port"])

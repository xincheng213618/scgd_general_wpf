"""
CLI argument parsing and command execution for ColorVision Marketplace.

Extracted from app.py to keep the Flask application module focused on routes.
"""

from __future__ import annotations

import argparse
from pathlib import Path
from typing import Any, Callable

from db_cache import CacheManager


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="ColorVision Plugin Marketplace")
    parser.add_argument("--storage", help="Override storage path")
    parser.add_argument("--port", type=int, help="Override port")
    parser.add_argument("--debug", action="store_true", help="Enable debug mode")
    parser.add_argument("--reconcile-history", action="store_true",
                        help="Move old root ColorVision release packages into History and exit")
    parser.add_argument("--reconcile-plugin-history", action="store_true",
                        help="Move old plugin .cvxp packages into History/Plugins and exit")
    parser.add_argument("--prune-updates", action="store_true",
                        help="Prune incremental update packages")
    parser.add_argument("--refresh-index", action="store_true",
                        help="Refresh the full plugin index and exit")
    parser.add_argument("--refresh-plugin-index",
                        help="Refresh the index for a single plugin ID and exit")
    parser.add_argument("--refresh-all-indexes", action="store_true",
                        help="Refresh all indexes (plugins, releases, updates, tools) and exit")
    parser.add_argument("--cleanup-cache", action="store_true",
                        help="Clean up expired cache entries and exit")
    parser.add_argument("--run-job",
                        help="Run a specific scheduled job by ID and exit")
    parser.add_argument("--create-api-key",
                        help="Create an API key with the given name and exit")
    parser.add_argument("--scopes", default="admin:*",
                        help="Scopes for --create-api-key (comma-separated, default: admin:*)")
    return parser


def handle_cli_args(
    args: argparse.Namespace,
    *,
    cache: CacheManager,
    storage: Path,
    config: dict[str, Any],
    get_db: Callable,
    validate_runtime_config: Callable,
    reconcile_app_release_history: Callable,
    reconcile_all_plugin_package_histories: Callable,
    prune_update_packages: Callable,
) -> bool:
    """Handle CLI arguments that cause early exit.

    Returns True if the server should continue starting, False if an early
    exit command was handled (SystemExit will be raised).
    """
    if args.storage:
        storage = Path(args.storage)
    if args.port:
        config["port"] = args.port
    if args.debug:
        config["debug"] = True

    issues = validate_runtime_config(config)
    if issues:
        if config.get("debug"):
            print("WARNING: Insecure configuration detected (debug mode allows startup):")
            for issue in issues:
                print(f"  - {issue}")
        else:
            print("Refusing to start with insecure production configuration:")
            for issue in issues:
                print(f"  - {issue}")
            raise SystemExit(2)

    if args.reconcile_history:
        moved = reconcile_app_release_history()
        print(f"Reconciled {len(moved)} file(s) into History")
        for item in moved[:20]:
            print(f"  {item['from']} -> {item['to']}")
        raise SystemExit(0)

    if args.reconcile_plugin_history:
        result = reconcile_all_plugin_package_histories()
        moved_count = sum(len(items) for items in result.values())
        print(f"Reconciled {moved_count} plugin package(s) across {len(result)} plugin(s)")
        for plugin_id, items in list(result.items())[:20]:
            print(f"[{plugin_id}] {len(items)} moved")
            for item in items[:5]:
                print(f"  {item['from']} -> {item['to']}")
        raise SystemExit(0)

    if args.prune_updates:
        result = prune_update_packages(storage)
        print(f"Retained {len(result['retained'])} update package(s)")
        print(f"Deleted {len(result['deleted'])} update package(s)")
        for item in result["deleted"][:20]:
            print(f"  removed {item}")
        raise SystemExit(0)

    if args.refresh_index:
        from services.plugin_index import refresh_all_plugin_index
        print("Refreshing full plugin index...")
        result = refresh_all_plugin_index(cache, storage)
        print(f"Indexed: {result['indexed_count']}, Deleted: {result['deleted_count']}, "
              f"Duration: {result['duration_ms']}ms, Errors: {len(result['errors'])}")
        for err in result["errors"]:
            print(f"  ERROR: {err}")
        raise SystemExit(0)

    if args.refresh_plugin_index:
        from services.plugin_index import refresh_plugin_index
        plugin_id = args.refresh_plugin_index
        print(f"Refreshing index for plugin: {plugin_id}")
        result = refresh_plugin_index(cache, storage, plugin_id)
        if result:
            print(f"OK: {result.get('name', plugin_id)} v{result.get('latest_version', '?')}")
        else:
            print(f"Plugin not found: {plugin_id}")
        raise SystemExit(0)

    if args.refresh_all_indexes:
        from services.storage_events import refresh_all_scopes
        print("Refreshing all indexes...")
        results = refresh_all_scopes(cache, storage)
        for scope, result in results.items():
            if isinstance(result, dict) and "indexed_count" in result:
                print(f"  {scope}: {result['indexed_count']} items, {result.get('duration_ms', 0)}ms")
        raise SystemExit(0)

    if args.cleanup_cache:
        deleted = cache.cleanup_expired_cache()
        print(f"Cleaned up {deleted} expired cache entry(ies)")
        raise SystemExit(0)

    if args.run_job:
        from services.scheduler import run_job_now
        job_id = args.run_job
        print(f"Running job: {job_id}")
        result = run_job_now(cache, storage, lambda: config, get_db, job_id)
        print(f"Status: {result['status']}")
        print(f"Duration: {result['duration_ms']}ms")
        if result.get("summary"):
            print(f"Summary: {result['summary']}")
        if result.get("error"):
            print(f"Error: {result['error']}")
        raise SystemExit(0)

    if args.create_api_key:
        from services.api_key_service import create_api_key
        from routes.admin_api import validate_scopes, ALLOWED_SCOPES
        name = args.create_api_key
        scopes = args.scopes
        if scopes:
            _, invalid = validate_scopes(scopes)
            if invalid:
                print(f"ERROR: Invalid scopes: {', '.join(invalid)}")
                print(f"Allowed scopes: {', '.join(sorted(ALLOWED_SCOPES))}")
                raise SystemExit(1)
        print(f"Creating API key: {name}")
        print(f"Scopes: {scopes}")
        result = create_api_key(cache, name=name, scopes=scopes, created_by="cli")
        print(f"\nKey ID: {result['id']}")
        print(f"Key: {result['key']}")
        print(f"Prefix: {result['key_prefix']}")
        print(f"\nIMPORTANT: Save this key now. It will not be shown again.")
        raise SystemExit(0)

    return True  # No early exit, continue with server startup

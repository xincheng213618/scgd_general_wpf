"""
Storage event handlers for ColorVision Marketplace.

Provides a unified dispatcher for post-upload / post-publish index refresh.
Called after files are written to storage to keep SQLite indexes in sync.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any


def on_storage_change(
    cache: Any,
    storage: Path,
    normalized_path: str,
    *,
    get_download_counts: Any = None,
    get_cache_entry: Any = None,
    set_cache_entry: Any = None,
    ttl_seconds: int = 86400,
    get_request_username: Any = None,
):
    """Dispatch index refresh based on which storage area changed.

    Call this after any successful file write (upload, publish, reconcile).
    """
    normalized = normalized_path.replace("\\", "/").strip("/")
    parts = normalized.split("/") if normalized else []
    top_dir = parts[0] if parts else ""

    if normalized == "LATEST_RELEASE":
        _refresh_latest_version_cache(storage)
        return

    if top_dir == "Plugins":
        _refresh_plugin_index(
            cache, storage, normalized,
            get_download_counts=get_download_counts,
            get_cache_entry=get_cache_entry,
            set_cache_entry=set_cache_entry,
            ttl_seconds=ttl_seconds,
            get_request_username=get_request_username,
        )
    elif top_dir == "Update":
        _refresh_artifact_scope(cache, storage, "updates")
    elif top_dir == "Tool":
        _refresh_artifact_scope(cache, storage, "tools")
    else:
        _refresh_artifact_scope(cache, storage, "releases")


def _refresh_artifact_scope(cache: Any, storage: Path, scope: str):
    """Refresh a single artifact index scope."""
    try:
        from services.artifact_index import (
            refresh_release_index,
            refresh_update_index,
            refresh_tool_index,
        )
        fn_map = {
            "releases": refresh_release_index,
            "updates": refresh_update_index,
            "tools": refresh_tool_index,
        }
        fn = fn_map.get(scope)
        if fn:
            fn(cache, storage)
    except Exception as exc:
        print(f"[storage_events] {scope} index refresh failed: {exc}")


def _refresh_latest_version_cache(storage: Path):
    try:
        from services.app_latest_version_cache import refresh_latest_version_cache
        refresh_latest_version_cache(storage)
    except Exception as exc:
        print(f"[storage_events] latest version cache refresh failed: {exc}")


def _refresh_plugin_index(
    cache: Any,
    storage: Path,
    normalized_path: str,
    *,
    get_download_counts: Any = None,
    get_cache_entry: Any = None,
    set_cache_entry: Any = None,
    ttl_seconds: int = 86400,
    get_request_username: Any = None,
):
    """Refresh plugin index for a specific plugin after publish."""
    parts = normalized_path.replace("\\", "/").split("/")
    plugin_id = parts[1] if len(parts) >= 2 and parts[0] == "Plugins" else None
    if not plugin_id:
        return

    try:
        from services.plugin_index import refresh_plugin_index
        from plugin_marketplace import prewarm_plugin_metadata

        download_counts = get_download_counts() if get_download_counts else {}
        refresh_plugin_index(cache, storage, plugin_id, download_counts=download_counts)

        if get_cache_entry and set_cache_entry:
            prewarm_plugin_metadata(
                storage, plugin_id, "",
                download_counts=download_counts,
                get_cache_entry=get_cache_entry,
                set_cache_entry=set_cache_entry,
                ttl_seconds=ttl_seconds,
            )

        actor = get_request_username() if get_request_username else "system"
        cache.write_audit(
            actor_type="user",
            actor_id=actor,
            action="index_refresh_plugin",
            target_type="plugin_index",
            target_id=plugin_id,
            detail="Auto-refreshed after publish/upload",
        )
    except Exception as exc:
        print(f"[storage_events] plugin index refresh failed for {plugin_id}: {exc}")


def refresh_all_scopes(cache: Any, storage: Path):
    """Refresh all artifact indexes. Used by CLI and admin endpoints."""
    from services.artifact_index import refresh_all_indexes
    from services.plugin_index import refresh_all_plugin_index

    results = {}
    plugin_result = refresh_all_plugin_index(cache, storage)
    results["plugins"] = plugin_result

    artifact_result = refresh_all_indexes(cache, storage)
    results.update(artifact_result["results"])

    return results

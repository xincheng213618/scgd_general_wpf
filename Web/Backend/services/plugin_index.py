"""
Plugin index service for ColorVision Marketplace.

Maintains persistent plugin_index and package_index tables in SQLite
so that API requests read from the database instead of scanning the
file system on every call.

Three sync triggers:
  1. Startup:   refresh_all_plugin_index if index is empty
  2. Publish:   refresh_plugin_index(plugin_id) after upload
  3. Periodic:  lightweight signature check → targeted refresh
"""

from __future__ import annotations

import time
from datetime import datetime, timezone
from pathlib import Path
from threading import Lock
from typing import Any, Callable

from db_cache import CacheManager, now_iso


_PLUGIN_FULL_REFRESH_LOCK = Lock()


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


# ---------------------------------------------------------------------------
# Disk scanning (reuses plugin_marketplace internals)
# ---------------------------------------------------------------------------

def build_plugin_summary_from_disk(
    storage: Path,
    plugin_id: str,
    *,
    download_counts: dict[str, int] | None = None,
) -> dict[str, Any] | None:
    """Scan a single plugin directory and return summary dict."""
    from plugin_marketplace import (
        get_plugin_summary,
        plugin_signature,
        plugin_summary_signature,
        scan_plugin_package_sets,
        read_text_file,
        _load_manifest,
        _get_archive_metadata_for_plugin,
        is_safe_plugin_id,
    )

    if not is_safe_plugin_id(plugin_id):
        return None

    plugin_dir = storage / "Plugins" / plugin_id
    if not plugin_dir.is_dir():
        return None

    if download_counts is None:
        download_counts = {}

    # Build summary directly (no cache layer)
    manifest = _load_manifest(plugin_dir / "manifest.json")
    latest_version = read_text_file(plugin_dir / "LATEST_RELEASE") or ""
    archive_metadata: dict[str, Any] = {}
    if not manifest:
        # Try reading from archive - but we don't have cache callbacks here,
        # so do a direct read without caching
        from plugin_marketplace import _select_preferred_package_path, _read_archive_metadata
        package_path = _select_preferred_package_path(storage, plugin_id, latest_version)
        if package_path:
            try:
                from plugin_marketplace import _archive_member_map, _read_zip_text_file
                import zipfile, json
                stat = package_path.stat()
                relative_path = package_path.relative_to(storage).as_posix()
                with zipfile.ZipFile(package_path) as archive:
                    members = _archive_member_map(archive)
                    manifest_name = members.get("manifest.json")
                    readme_name = members.get("readme.md")
                    changelog_name = members.get("changelog.md")
                    icon_name = members.get("packageicon.png")
                    if manifest_name:
                        manifest_text = _read_zip_text_file(archive, manifest_name)
                        if manifest_text:
                            try:
                                raw = json.loads(manifest_text)
                                from plugin_marketplace import _normalize_manifest_keys
                                manifest = dict(_normalize_manifest_keys(raw))
                            except json.JSONDecodeError:
                                manifest = {}
                    archive_metadata = {
                        "has_icon": bool(icon_name),
                        "readme": _read_zip_text_file(archive, readme_name) if readme_name else "",
                        "changelog": _read_zip_text_file(archive, changelog_name) if changelog_name else "",
                        "sourceArchive": relative_path,
                    }
            except Exception:
                manifest = {}

    current_packages, historical_packages = scan_plugin_package_sets(
        storage, plugin_id, include_hash=False,
    )
    latest_package = current_packages[0] if current_packages else None
    modified = (
        latest_package["modified"]
        if latest_package
        else _dir_mtime_iso(plugin_dir)
    )

    return {
        "id": manifest.get("id", plugin_id),
        "plugin_id": plugin_id,
        "name": manifest.get("name", plugin_id),
        "description": manifest.get("description", ""),
        "author": manifest.get("author", ""),
        "url": manifest.get("url", ""),
        "version": latest_version or (latest_package["version"] if latest_package else ""),
        "latest_version": latest_version or (latest_package["version"] if latest_package else ""),
        "requires": manifest.get("requires", ""),
        "requires_version": manifest.get("requires", ""),
        "category": manifest.get("category", ""),
        "has_icon": (plugin_dir / "PackageIcon.png").exists() or bool(archive_metadata.get("has_icon")),
        "total_downloads": download_counts.get(plugin_id, 0),
        "modified": modified,
        "current_package_count": len(current_packages),
        "historical_package_count": len(historical_packages),
        "current_packages": current_packages,
        "historical_packages": historical_packages,
        "readme": read_text_file(plugin_dir / "README.md") or str(archive_metadata.get("readme") or ""),
        "changelog": read_text_file(plugin_dir / "CHANGELOG.md") or str(archive_metadata.get("changelog") or ""),
        "source_manifest_path": str(plugin_dir / "manifest.json") if (plugin_dir / "manifest.json").exists() else "",
        "source_archive_path": str(archive_metadata.get("sourceArchive", "")),
    }


def _dir_mtime_iso(path: Path) -> str:
    try:
        return datetime.fromtimestamp(path.stat().st_mtime, tz=timezone.utc).isoformat()
    except OSError:
        return _now_iso()


def _prepare_package_hashes(
    cache: CacheManager,
    storage: Path,
    plugin_id: str,
    packages: list[dict[str, Any]],
) -> None:
    """Populate hashes before the package-index write transaction starts."""
    db = cache.get_db()
    try:
        rows = db.execute(
            """SELECT relative_path, file_hash, file_signature
               FROM package_index WHERE plugin_id = ?""",
            (plugin_id,),
        ).fetchall()
        existing = {
            str(row["relative_path"]): (str(row["file_hash"] or ""), str(row["file_signature"] or ""))
            for row in rows
        }
    finally:
        db.close()

    from plugin_marketplace import _compute_file_hash

    for pkg in packages:
        relative_path = str(pkg.get("relative_path") or "")
        if not relative_path:
            continue
        file_path = storage / relative_path
        try:
            stat = file_path.stat()
        except OSError:
            pkg["fileSignature"] = ""
            continue

        signature = f"{stat.st_mtime_ns}:{stat.st_size}"
        pkg["fileSignature"] = signature
        previous_hash, previous_signature = existing.get(relative_path, ("", ""))
        if previous_hash and previous_signature == signature:
            pkg["fileHash"] = previous_hash
            continue

        file_hash = _compute_file_hash(file_path)
        if file_hash:
            pkg["fileHash"] = file_hash


# ---------------------------------------------------------------------------
# Index refresh: single plugin
# ---------------------------------------------------------------------------

def refresh_plugin_index(
    cache: CacheManager,
    storage: Path,
    plugin_id: str,
    *,
    download_counts: dict[str, int] | None = None,
) -> dict[str, Any] | None:
    """Refresh plugin_index + package_index for a single plugin.

    Returns the summary dict, or None if the plugin directory is missing
    (in which case it marks the index entry as deleted).
    """
    started = time.monotonic()
    plugin_dir = storage / "Plugins" / plugin_id

    if not plugin_dir.is_dir():
        # Mark as deleted in index
        _mark_plugin_deleted(cache, plugin_id, storage=storage)
        return None

    summary = build_plugin_summary_from_disk(storage, plugin_id, download_counts=download_counts)
    if not summary:
        _mark_plugin_deleted(cache, plugin_id, storage=storage)
        return None

    packages = list(summary.get("current_packages", [])) + list(summary.get("historical_packages", []))
    _prepare_package_hashes(cache, storage, plugin_id, packages)

    now = _now_iso()
    signature = _plugin_dir_signature(storage, plugin_id)

    db = cache.get_db()
    try:
        db.execute(
            """INSERT INTO plugin_index
               (plugin_id, name, description, author, category, latest_version,
                requires_version, url, has_icon, current_package_count,
                historical_package_count, total_downloads, modified,
                readme, changelog,
                source_manifest_path, source_archive_path, file_signature,
                indexed_at, is_deleted)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0)
               ON CONFLICT(plugin_id) DO UPDATE SET
                   name = excluded.name,
                   description = excluded.description,
                   author = excluded.author,
                   category = excluded.category,
                   latest_version = excluded.latest_version,
                   requires_version = excluded.requires_version,
                   url = excluded.url,
                   has_icon = excluded.has_icon,
                   current_package_count = excluded.current_package_count,
                   historical_package_count = excluded.historical_package_count,
                   total_downloads = excluded.total_downloads,
                   modified = excluded.modified,
                   readme = excluded.readme,
                   changelog = excluded.changelog,
                   source_manifest_path = excluded.source_manifest_path,
                   source_archive_path = excluded.source_archive_path,
                   file_signature = excluded.file_signature,
                   indexed_at = excluded.indexed_at,
                   is_deleted = 0
            """,
            (
                plugin_id,
                summary.get("name", ""),
                summary.get("description", ""),
                summary.get("author", ""),
                summary.get("category", ""),
                summary.get("latest_version", ""),
                summary.get("requires_version", ""),
                summary.get("url", ""),
                1 if summary.get("has_icon") else 0,
                summary.get("current_package_count", 0),
                summary.get("historical_package_count", 0),
                summary.get("total_downloads", 0),
                summary.get("modified", ""),
                summary.get("readme", ""),
                summary.get("changelog", ""),
                summary.get("source_manifest_path", ""),
                summary.get("source_archive_path", ""),
                signature,
                now,
            ),
        )

        # Update package_index: mark old entries deleted, insert fresh
        db.execute(
            "UPDATE package_index SET is_deleted = 1, indexed_at = ? WHERE plugin_id = ?",
            (now, plugin_id),
        )
        for pkg in summary.get("current_packages", []):
            _upsert_package(db, plugin_id, pkg, "current", now, storage)
        for pkg in summary.get("historical_packages", []):
            _upsert_package(db, plugin_id, pkg, "archive", now, storage)

        db.commit()
    except Exception as exc:
        db.rollback()
        print(f"[plugin_index] refresh failed for {plugin_id}: {exc}")
        raise
    finally:
        db.close()

    # Invalidate related caches
    cache.invalidate_cache_prefix("plugin_catalog:")
    cache.invalidate_cache_prefix(f"plugin_summary:v1:{plugin_id}")
    cache.invalidate_cache_prefix(f"plugin_detail:v1:{plugin_id}")
    try:
        from services.app_latest_version_cache import set_plugin_latest_version_cache
        set_plugin_latest_version_cache(storage, plugin_id, str(summary.get("latest_version", "")))
    except Exception:
        pass

    elapsed_ms = int((time.monotonic() - started) * 1000)
    summary["_refresh_ms"] = elapsed_ms
    return summary


def _upsert_package(
    db: Any,
    plugin_id: str,
    pkg: dict[str, Any],
    source: str,
    now: str,
    storage: Path,
):
    """Insert or update a single row in package_index."""
    relative_path = pkg.get("relative_path", "")
    if not relative_path:
        return
    db.execute(
        """INSERT INTO package_index
           (plugin_id, version, filename, relative_path, size, source,
            modified, file_hash, file_signature, indexed_at, is_deleted)
           VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0)
           ON CONFLICT(relative_path) DO UPDATE SET
               plugin_id = excluded.plugin_id,
               version = excluded.version,
               filename = excluded.filename,
               size = excluded.size,
               source = excluded.source,
               modified = excluded.modified,
               file_hash = CASE
                   WHEN excluded.file_hash != '' THEN excluded.file_hash
                   WHEN excluded.file_signature != ''
                        AND excluded.file_signature != package_index.file_signature THEN ''
                   ELSE package_index.file_hash
               END,
               file_signature = CASE WHEN excluded.file_signature != '' THEN excluded.file_signature ELSE package_index.file_signature END,
               indexed_at = excluded.indexed_at,
               is_deleted = 0
        """,
        (
            plugin_id,
            pkg.get("version", ""),
            pkg.get("filename", ""),
            relative_path,
            pkg.get("size", 0),
            pkg.get("source", source),
            pkg.get("modified", ""),
            pkg.get("fileHash", ""),
            pkg.get("fileSignature", ""),
            now,
        ),
    )


def _mark_plugin_deleted(cache: CacheManager, plugin_id: str, *, storage: Path | None = None):
    now = _now_iso()
    db = cache.get_db()
    try:
        # Insert or update to mark as deleted
        db.execute(
            """INSERT INTO plugin_index (plugin_id, name, is_deleted, indexed_at)
               VALUES (?, ?, 1, ?)
               ON CONFLICT(plugin_id) DO UPDATE SET is_deleted = 1, indexed_at = ?""",
            (plugin_id, plugin_id, now, now),
        )
        db.execute(
            "UPDATE package_index SET is_deleted = 1, indexed_at = ? WHERE plugin_id = ?",
            (now, plugin_id),
        )
        db.commit()
    except Exception as exc:
        print(f"[plugin_index] mark deleted failed for {plugin_id}: {exc}")
    finally:
        db.close()
    if storage is not None:
        try:
            from services.app_latest_version_cache import set_plugin_latest_version_cache
            set_plugin_latest_version_cache(storage, plugin_id, None)
        except Exception:
            pass


def _plugin_dir_signature(storage: Path, plugin_id: str) -> str:
    """Lightweight signature for a plugin directory."""
    from plugin_marketplace import plugin_summary_signature
    return plugin_summary_signature(storage, plugin_id)


# ---------------------------------------------------------------------------
# Index refresh: all plugins
# ---------------------------------------------------------------------------

def refresh_all_plugin_index(
    cache: CacheManager,
    storage: Path,
    *,
    download_counts: dict[str, int] | None = None,
) -> dict[str, Any]:
    """Scan all plugins and refresh the full index.

    Returns: {indexed_count, deleted_count, duration_ms, errors}
    """
    if not _PLUGIN_FULL_REFRESH_LOCK.acquire(blocking=False):
        return {
            "indexed_count": 0,
            "deleted_count": 0,
            "duration_ms": 0,
            "errors": [],
            "status": "skipped",
            "reason": "refresh_in_progress",
            "changed_during_refresh": False,
        }

    try:
        return _refresh_all_plugin_index(
            cache,
            storage,
            download_counts=download_counts,
        )
    finally:
        _PLUGIN_FULL_REFRESH_LOCK.release()


def _refresh_all_plugin_index(
    cache: CacheManager,
    storage: Path,
    *,
    download_counts: dict[str, int] | None = None,
) -> dict[str, Any]:
    """Run one full refresh while the caller owns the scope lock."""
    started = time.monotonic()
    now = _now_iso()
    plugins_dir = storage / "Plugins"

    # The stored signature must describe the snapshot we are about to scan.
    # Computing it after the writes can permanently pair an old DB snapshot
    # with a newer filesystem signature when a publish races the refresh.
    pre_scan_signature = ""
    try:
        from plugin_marketplace import plugin_catalog_signature
        pre_scan_signature = plugin_catalog_signature(storage)
    except Exception as exc:
        print(f"[plugin_index] pre-scan signature computation failed: {exc}")

    _update_index_state(
        cache,
        "plugins",
        status="refreshing",
        signature=pre_scan_signature,
        started_at=now,
    )

    errors: list[str] = []
    indexed_count = 0
    deleted_count = 0

    # Collect current plugin IDs from disk
    disk_plugin_ids: set[str] = set()
    if plugins_dir.is_dir():
        for entry in plugins_dir.iterdir():
            if entry.is_dir():
                disk_plugin_ids.add(entry.name)

    # Collect existing plugin IDs from index
    db = cache.get_db()
    try:
        rows = db.execute(
            "SELECT plugin_id FROM plugin_index WHERE is_deleted = 0"
        ).fetchall()
        existing_ids = {row["plugin_id"] for row in rows}
    except Exception:
        existing_ids = set()
    finally:
        db.close()

    # Refresh each plugin on disk
    for plugin_id in sorted(disk_plugin_ids):
        try:
            result = refresh_plugin_index(cache, storage, plugin_id, download_counts=download_counts)
            if result:
                indexed_count += 1
        except Exception as exc:
            errors.append(f"{plugin_id}: {exc}")

    # Mark plugins that disappeared from disk
    for plugin_id in existing_ids - disk_plugin_ids:
        _mark_plugin_deleted(cache, plugin_id, storage=storage)
        deleted_count += 1

    elapsed_ms = int((time.monotonic() - started) * 1000)
    finished_at = _now_iso()

    # A bounded post-check detects an in-flight publish.  We deliberately keep
    # the pre-scan signature in index_state so the next periodic check sees a
    # mismatch and refreshes the changed snapshot; there is no retry loop here.
    post_scan_signature = ""
    try:
        from plugin_marketplace import plugin_catalog_signature
        post_scan_signature = plugin_catalog_signature(storage)
    except Exception as exc:
        print(f"[plugin_index] post-scan signature computation failed: {exc}")
    changed_during_refresh = bool(
        pre_scan_signature
        and post_scan_signature
        and pre_scan_signature != post_scan_signature
    )

    _update_index_state(
        cache,
        "plugins",
        status="ready",
        signature=pre_scan_signature,
        finished_at=finished_at,
        item_count=indexed_count,
        duration_ms=elapsed_ms,
        error="; ".join(errors[-3:]) if errors else "",
    )
    try:
        from services.app_latest_version_cache import warm_plugin_latest_versions_cache
        warm_plugin_latest_versions_cache(storage, cache)
    except Exception:
        pass

    return {
        "indexed_count": indexed_count,
        "deleted_count": deleted_count,
        "duration_ms": elapsed_ms,
        "errors": errors,
        "status": "ready",
        "changed_during_refresh": changed_during_refresh,
    }


def _update_index_state(
    cache: CacheManager,
    scope: str,
    *,
    status: str = "ready",
    signature: str = "",
    started_at: str = "",
    finished_at: str = "",
    item_count: int = 0,
    duration_ms: int = 0,
    error: str = "",
):
    db = cache.get_db()
    try:
        db.execute(
            """INSERT INTO index_state (scope, signature, status, last_started_at, last_finished_at,
                                        last_error, item_count, duration_ms)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?)
               ON CONFLICT(scope) DO UPDATE SET
                   signature = CASE WHEN excluded.signature != '' THEN excluded.signature ELSE index_state.signature END,
                   status = excluded.status,
                   last_started_at = COALESCE(excluded.last_started_at, index_state.last_started_at),
                   last_finished_at = COALESCE(excluded.last_finished_at, index_state.last_finished_at),
                   last_error = excluded.last_error,
                   item_count = excluded.item_count,
                   duration_ms = excluded.duration_ms
            """,
            (scope, signature or "", status, started_at or None, finished_at or None, error, item_count, duration_ms),
        )
        db.commit()
    except Exception as exc:
        print(f"[index_state] update failed for {scope}: {exc}")
    finally:
        db.close()


# ---------------------------------------------------------------------------
# Read from index
# ---------------------------------------------------------------------------

def get_plugin_catalog_from_index(
    cache: CacheManager,
    download_counts: dict[str, int],
) -> list[dict[str, Any]] | None:
    """Read plugin catalog from plugin_index table.

    Returns None if the index is empty (caller should fallback to disk scan).
    """
    db = cache.get_db()
    try:
        rows = db.execute(
            "SELECT * FROM plugin_index WHERE is_deleted = 0 ORDER BY plugin_id"
        ).fetchall()
        if not rows:
            return None

        items: list[dict[str, Any]] = []
        for row in rows:
            item = dict(row)
            item["id"] = item["plugin_id"]
            item["total_downloads"] = download_counts.get(item["plugin_id"], 0)
            items.append(item)
        return items
    except Exception as exc:
        print(f"[plugin_index] get_plugin_catalog_from_index failed: {exc}")
        return None
    finally:
        db.close()


def get_plugin_detail_from_index(
    cache: CacheManager,
    plugin_id: str,
    download_counts: dict[str, int],
    *,
    storage: Path | None = None,
) -> dict[str, Any] | None:
    """Read plugin detail from plugin_index + package_index tables.

    Returns a dict compatible with the old get_plugin_detail API including:
    id, version, latest_version, requires, requires_version, readme, changelog,
    current_packages, historical_packages, all_packages, has_icon, total_downloads,
    name, description, author, url, category, modified, etc.

    Missing hashes are surfaced as pending; GET never performs package I/O.
    """
    db = cache.get_db()
    try:
        row = db.execute(
            "SELECT * FROM plugin_index WHERE plugin_id = ? AND is_deleted = 0",
            (plugin_id,),
        ).fetchone()
        if not row:
            return None

        detail = dict(row)
        # Ensure all fields the old API returned are present
        detail["id"] = detail["plugin_id"]
        # "version" alias for latest_version
        detail["version"] = detail.get("latest_version", "")
        # "requires" alias for requires_version
        detail["requires"] = detail.get("requires_version", "")
        detail["total_downloads"] = download_counts.get(plugin_id, 0)
        # Convert has_icon from int to bool
        detail["has_icon"] = bool(detail.get("has_icon", 0))

        # Read packages
        pkg_rows = db.execute(
            "SELECT * FROM package_index WHERE plugin_id = ? AND is_deleted = 0 ORDER BY version DESC",
            (plugin_id,),
        ).fetchall()

        current_packages = []
        historical_packages = []
        for pkg_row in pkg_rows:
            pkg = {
                "version": pkg_row["version"],
                "filename": pkg_row["filename"],
                "size": pkg_row["size"],
                "source": pkg_row["source"],
                "relative_path": pkg_row["relative_path"],
                "modified": pkg_row["modified"],
            }
            if pkg_row["file_hash"]:
                pkg["fileHash"] = pkg_row["file_hash"]
            else:
                pkg["hashPending"] = True
            if pkg_row["source"] == "current":
                current_packages.append(pkg)
            else:
                historical_packages.append(pkg)

        detail["packages"] = current_packages
        detail["current_packages"] = current_packages
        detail["historical_packages"] = historical_packages
        detail["all_packages"] = current_packages + historical_packages
        # readme/changelog already in detail from plugin_index row
        detail["readme"] = detail.get("readme", "")
        detail["changelog"] = detail.get("changelog", "")

        return detail
    except Exception as exc:
        print(f"[plugin_index] get_plugin_detail_from_index failed for {plugin_id}: {exc}")
        return None
    finally:
        db.close()


def is_plugin_index_populated(cache: CacheManager) -> bool:
    """Check if plugin_index has any entries."""
    db = cache.get_db()
    try:
        row = db.execute(
            "SELECT COUNT(*) AS cnt FROM plugin_index WHERE is_deleted = 0"
        ).fetchone()
        return bool(row and row["cnt"] > 0)
    except Exception:
        return False
    finally:
        db.close()


def has_active_packages_missing_hash(cache: CacheManager) -> bool:
    """Return whether startup refresh must backfill an active package hash."""
    db = cache.get_db()
    try:
        row = db.execute(
            """SELECT 1 FROM package_index
               WHERE is_deleted = 0 AND (file_hash IS NULL OR file_hash = '')
               LIMIT 1"""
        ).fetchone()
        return row is not None
    except Exception:
        return False
    finally:
        db.close()


def get_plugin_index_state(cache: CacheManager) -> dict[str, Any] | None:
    """Get the current index_state for 'plugins' scope."""
    db = cache.get_db()
    try:
        row = db.execute("SELECT * FROM index_state WHERE scope = 'plugins'").fetchone()
        return dict(row) if row else None
    except Exception:
        return None
    finally:
        db.close()

"""
Artifact index service for ColorVision Marketplace.

Maintains persistent release_index, update_index, and tool_index tables
in SQLite so that API requests read from the database instead of scanning
the file system on every call.

Follows the same pattern as plugin_index.py.
"""

from __future__ import annotations

import hashlib
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from db_cache import CacheManager, now_iso


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def _dir_signature_hash(entries: list[tuple[str, int, float]]) -> str:
    """Compute a lightweight hash from a list of (name, size, mtime) tuples."""
    if not entries:
        return "empty"
    raw = "|".join(f"{n}:{s}:{m:.0f}" for n, s, m in sorted(entries))
    return hashlib.md5(raw.encode("utf-8")).hexdigest()[:16]


def _version_sort_tuple(version: str) -> tuple[int, ...]:
    try:
        return tuple(int(part) for part in str(version).split("."))
    except (TypeError, ValueError):
        return ()


def _update_sort_key(item: dict[str, Any]) -> tuple[tuple[int, ...], str]:
    return (_version_sort_tuple(str(item.get("version", ""))), str(item.get("modified", "")))


# ---------------------------------------------------------------------------
# Release index
# ---------------------------------------------------------------------------

def _scan_release_artifacts(storage: Path) -> list[dict[str, Any]]:
    """Scan storage for release artifacts (current + History/)."""
    from app_releases import build_release_artifact

    artifacts: list[dict[str, Any]] = []
    if not storage.is_dir():
        return artifacts

    for entry in storage.iterdir():
        if not entry.is_file():
            continue
        artifact = build_release_artifact(storage, entry, "current")
        if artifact:
            artifacts.append(artifact)

    history_dir = storage / "History"
    if history_dir.is_dir():
        for major_dir in history_dir.iterdir():
            if not major_dir.is_dir():
                continue
            for branch_dir in major_dir.iterdir():
                if not branch_dir.is_dir():
                    continue
                for entry in branch_dir.iterdir():
                    if not entry.is_file():
                        continue
                    artifact = build_release_artifact(storage, entry, "archive")
                    if artifact:
                        artifacts.append(artifact)

    from app_releases import release_sort_key
    artifacts.sort(key=release_sort_key, reverse=True)
    return artifacts


def _release_signature(storage: Path) -> str:
    """Lightweight signature for release artifacts.

    Uses a two-level directory walk instead of rglob to avoid deep recursion.
    History layout: History/{major.minor}/{major.minor.patch}/file
    """
    entries: list[tuple[str, int, float]] = []
    if storage.is_dir():
        for entry in storage.iterdir():
            if entry.is_file():
                try:
                    st = entry.stat()
                    entries.append((entry.name, st.st_size, st.st_mtime))
                except OSError:
                    pass
    history_dir = storage / "History"
    if history_dir.is_dir():
        try:
            history_mtime = history_dir.stat().st_mtime
        except OSError:
            history_mtime = 0
        entries.append(("_History_dir", 0, history_mtime))

        for major_dir in history_dir.iterdir():
            if not major_dir.is_dir():
                continue
            try:
                entries.append((major_dir.name, 0, major_dir.stat().st_mtime))
            except OSError:
                continue
            for branch_dir in major_dir.iterdir():
                if not branch_dir.is_dir():
                    continue
                try:
                    entries.append((branch_dir.name, 0, branch_dir.stat().st_mtime))
                except OSError:
                    continue
                for file_entry in branch_dir.iterdir():
                    if file_entry.is_file():
                        try:
                            st = file_entry.stat()
                            entries.append((file_entry.name, st.st_size, st.st_mtime))
                        except OSError:
                            pass
    return _dir_signature_hash(entries)


def refresh_release_index(cache: CacheManager, storage: Path) -> dict[str, Any]:
    """Refresh release_index table from disk. Returns summary dict."""
    started = time.monotonic()
    now = _now_iso()
    _update_index_state(cache, "releases", status="refreshing", started_at=now)

    errors: list[str] = []
    try:
        artifacts = _scan_release_artifacts(storage)
    except Exception as exc:
        errors.append(str(exc))
        artifacts = []

    sig = _release_signature(storage)
    db = cache.get_db()
    try:
        # Mark all existing as deleted first
        db.execute("UPDATE release_index SET is_deleted = 1, indexed_at = ?", (now,))

        for item in artifacts:
            relative_path = item.get("relative_path", "")
            if not relative_path:
                continue
            db.execute(
                """INSERT INTO release_index
                   (relative_path, version, filename, size, kind, kind_label,
                    era, era_label, source, major_minor, branch,
                    modified, modified_display, display_title, file_signature,
                    indexed_at, is_deleted)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0)
                   ON CONFLICT(relative_path) DO UPDATE SET
                       version = excluded.version,
                       filename = excluded.filename,
                       size = excluded.size,
                       kind = excluded.kind,
                       kind_label = excluded.kind_label,
                       era = excluded.era,
                       era_label = excluded.era_label,
                       source = excluded.source,
                       major_minor = excluded.major_minor,
                       branch = excluded.branch,
                       modified = excluded.modified,
                       modified_display = excluded.modified_display,
                       display_title = excluded.display_title,
                       file_signature = excluded.file_signature,
                       indexed_at = excluded.indexed_at,
                       is_deleted = 0
                """,
                (
                    relative_path,
                    item.get("version", ""),
                    item.get("filename", ""),
                    item.get("size", 0),
                    item.get("kind", ""),
                    item.get("kind_label", ""),
                    item.get("era", ""),
                    item.get("era_label", ""),
                    item.get("source", ""),
                    item.get("major_minor", ""),
                    item.get("branch", ""),
                    item.get("modified", ""),
                    item.get("modified_display", ""),
                    item.get("display_title", ""),
                    sig,
                    now,
                ),
            )

        # Remove entries that were marked deleted and are still deleted
        db.execute("DELETE FROM release_index WHERE is_deleted = 1 AND indexed_at != ?", (now,))
        db.commit()
    except Exception as exc:
        db.rollback()
        errors.append(str(exc))
    finally:
        db.close()

    elapsed_ms = int((time.monotonic() - started) * 1000)
    finished_at = _now_iso()

    _update_index_state(
        cache,
        "releases",
        status="ready",
        signature=sig,
        finished_at=finished_at,
        item_count=len(artifacts),
        duration_ms=elapsed_ms,
        error="; ".join(errors[-3:]) if errors else "",
    )

    cache.invalidate_cache_prefix("app_releases:")
    cache.invalidate_cache_prefix("home_release_snapshot:")
    cache.invalidate_cache_prefix("release_timeline:")

    return {
        "indexed_count": len(artifacts),
        "duration_ms": elapsed_ms,
        "errors": errors,
    }


def get_releases_from_index(cache: CacheManager) -> list[dict[str, Any]] | None:
    """Read releases from release_index. Returns None if empty."""
    db = cache.get_db()
    try:
        rows = db.execute(
            "SELECT * FROM release_index WHERE is_deleted = 0 ORDER BY version DESC, modified DESC"
        ).fetchall()
        if not rows:
            return None
        return [dict(r) for r in rows]
    except Exception:
        return None
    finally:
        db.close()


# ---------------------------------------------------------------------------
# Update index
# ---------------------------------------------------------------------------

def _scan_update_packages(storage: Path) -> list[dict[str, Any]]:
    """Scan Update/ directory for incremental update packages."""
    from update_retention import parse_update_filename

    update_dir = storage / "Update"
    items: list[dict[str, Any]] = []
    if not update_dir.is_dir():
        return items

    for entry in update_dir.iterdir():
        if entry.name.startswith(".") or not entry.is_file():
            continue
        parsed = parse_update_filename(entry.name)
        if not parsed:
            continue
        try:
            stat = entry.stat()
        except OSError:
            continue
        items.append({
            "filename": entry.name,
            "version": parsed["version"],
            "branch": parsed["branch"],
            "fix": parsed["fix"],
            "size": stat.st_size,
            "modified": datetime.fromtimestamp(stat.st_mtime, tz=timezone.utc).isoformat(),
            "relative_path": entry.relative_to(storage).as_posix(),
        })

    items.sort(key=_update_sort_key, reverse=True)
    return items


def _update_signature(storage: Path) -> str:
    update_dir = storage / "Update"
    entries: list[tuple[str, int, float]] = []
    if update_dir.is_dir():
        for entry in update_dir.iterdir():
            if entry.is_file():
                try:
                    st = entry.stat()
                    entries.append((entry.name, st.st_size, st.st_mtime))
                except OSError:
                    pass
    return _dir_signature_hash(entries)


def refresh_update_index(cache: CacheManager, storage: Path) -> dict[str, Any]:
    """Refresh update_index table from disk."""
    started = time.monotonic()
    now = _now_iso()
    _update_index_state(cache, "updates", status="refreshing", started_at=now)

    errors: list[str] = []
    try:
        items = _scan_update_packages(storage)
    except Exception as exc:
        errors.append(str(exc))
        items = []

    sig = _update_signature(storage)
    db = cache.get_db()
    try:
        db.execute("UPDATE update_index SET is_deleted = 1, indexed_at = ?", (now,))

        for item in items:
            db.execute(
                """INSERT INTO update_index
                   (filename, version, branch, fix, size, modified, relative_path,
                    file_signature, indexed_at, is_deleted)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 0)
                   ON CONFLICT(filename) DO UPDATE SET
                       version = excluded.version,
                       branch = excluded.branch,
                       fix = excluded.fix,
                       size = excluded.size,
                       modified = excluded.modified,
                       relative_path = excluded.relative_path,
                       file_signature = excluded.file_signature,
                       indexed_at = excluded.indexed_at,
                       is_deleted = 0
                """,
                (
                    item["filename"],
                    item["version"],
                    item["branch"],
                    item["fix"],
                    item["size"],
                    item["modified"],
                    item["relative_path"],
                    sig,
                    now,
                ),
            )

        db.execute("DELETE FROM update_index WHERE is_deleted = 1 AND indexed_at != ?", (now,))
        db.commit()
    except Exception as exc:
        db.rollback()
        errors.append(str(exc))
    finally:
        db.close()

    elapsed_ms = int((time.monotonic() - started) * 1000)
    finished_at = _now_iso()

    _update_index_state(
        cache,
        "updates",
        status="ready",
        signature=sig,
        finished_at=finished_at,
        item_count=len(items),
        duration_ms=elapsed_ms,
        error="; ".join(errors[-3:]) if errors else "",
    )

    return {
        "indexed_count": len(items),
        "duration_ms": elapsed_ms,
        "errors": errors,
    }


def get_updates_from_index(cache: CacheManager) -> list[dict[str, Any]] | None:
    """Read updates from update_index. Returns None if empty."""
    db = cache.get_db()
    try:
        rows = db.execute(
            "SELECT * FROM update_index WHERE is_deleted = 0"
        ).fetchall()
        if not rows:
            return None
        items = [dict(r) for r in rows]
        items.sort(key=_update_sort_key, reverse=True)
        return items
    except Exception:
        return None
    finally:
        db.close()


# ---------------------------------------------------------------------------
# Tool index
# ---------------------------------------------------------------------------

def _scan_tool_entries(storage: Path) -> list[dict[str, Any]]:
    """Scan Tool/ directory for files and subdirectories."""
    tool_dir = storage / "Tool"
    items: list[dict[str, Any]] = []
    if not tool_dir.is_dir():
        return items

    for entry in sorted(tool_dir.iterdir(), key=lambda e: (e.is_file(), e.name.lower())):
        if entry.name.startswith("."):
            continue
        try:
            stat = entry.stat()
        except OSError:
            continue

        relative_path = entry.relative_to(storage).as_posix()
        modified_iso = datetime.fromtimestamp(stat.st_mtime, tz=timezone.utc).isoformat()
        modified_display = datetime.fromtimestamp(stat.st_mtime, tz=timezone.utc).strftime("%Y-%m-%d %H:%M")

        item: dict[str, Any] = {
            "name": entry.name,
            "relative_path": relative_path,
            "filename": entry.name,
            "modified": modified_iso,
            "modified_display": modified_display,
            "is_dir": entry.is_dir(),
        }

        if entry.is_dir():
            try:
                item["file_count"] = sum(1 for c in entry.iterdir() if c.is_file())
            except OSError:
                item["file_count"] = 0
            item["size"] = 0
        else:
            item["size"] = stat.st_size
            item["file_count"] = 0

        items.append(item)

    return items


def _tool_signature(storage: Path) -> str:
    tool_dir = storage / "Tool"
    entries: list[tuple[str, int, float]] = []
    if tool_dir.is_dir():
        for entry in tool_dir.iterdir():
            try:
                st = entry.stat()
                entries.append((entry.name, st.st_size if entry.is_file() else 0, st.st_mtime))
            except OSError:
                pass
    return _dir_signature_hash(entries)


def refresh_tool_index(cache: CacheManager, storage: Path) -> dict[str, Any]:
    """Refresh tool_index table from disk."""
    started = time.monotonic()
    now = _now_iso()
    _update_index_state(cache, "tools", status="refreshing", started_at=now)

    errors: list[str] = []
    try:
        items = _scan_tool_entries(storage)
    except Exception as exc:
        errors.append(str(exc))
        items = []

    sig = _tool_signature(storage)
    db = cache.get_db()
    try:
        db.execute("UPDATE tool_index SET is_deleted = 1, indexed_at = ?", (now,))

        for item in items:
            db.execute(
                """INSERT INTO tool_index
                   (relative_path, name, filename, size, modified, modified_display,
                    is_dir, file_count, file_signature, indexed_at, is_deleted)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0)
                   ON CONFLICT(relative_path) DO UPDATE SET
                       name = excluded.name,
                       filename = excluded.filename,
                       size = excluded.size,
                       modified = excluded.modified,
                       modified_display = excluded.modified_display,
                       is_dir = excluded.is_dir,
                       file_count = excluded.file_count,
                       file_signature = excluded.file_signature,
                       indexed_at = excluded.indexed_at,
                       is_deleted = 0
                """,
                (
                    item["relative_path"],
                    item["name"],
                    item["filename"],
                    item["size"],
                    item["modified"],
                    item["modified_display"],
                    1 if item["is_dir"] else 0,
                    item["file_count"],
                    sig,
                    now,
                ),
            )

        db.execute("DELETE FROM tool_index WHERE is_deleted = 1 AND indexed_at != ?", (now,))
        db.commit()
    except Exception as exc:
        db.rollback()
        errors.append(str(exc))
    finally:
        db.close()

    elapsed_ms = int((time.monotonic() - started) * 1000)
    finished_at = _now_iso()

    _update_index_state(
        cache,
        "tools",
        status="ready",
        signature=sig,
        finished_at=finished_at,
        item_count=len(items),
        duration_ms=elapsed_ms,
        error="; ".join(errors[-3:]) if errors else "",
    )

    cache.invalidate_cache_prefix("home_tool_preview:")

    return {
        "indexed_count": len(items),
        "duration_ms": elapsed_ms,
        "errors": errors,
    }


def get_tools_from_index(cache: CacheManager) -> list[dict[str, Any]] | None:
    """Read tools from tool_index. Returns None if empty."""
    db = cache.get_db()
    try:
        rows = db.execute(
            "SELECT * FROM tool_index WHERE is_deleted = 0 ORDER BY is_dir DESC, name"
        ).fetchall()
        if not rows:
            return None
        items = []
        for r in rows:
            item = dict(r)
            item["is_dir"] = bool(item.get("is_dir", 0))
            items.append(item)
        return items
    except Exception:
        return None
    finally:
        db.close()


# ---------------------------------------------------------------------------
# Combined refresh and status
# ---------------------------------------------------------------------------

def refresh_all_indexes(cache: CacheManager, storage: Path) -> dict[str, Any]:
    """Refresh all artifact indexes (releases, updates, tools)."""
    results = {}
    results["releases"] = refresh_release_index(cache, storage)
    results["updates"] = refresh_update_index(cache, storage)
    results["tools"] = refresh_tool_index(cache, storage)

    total_ms = sum(r["duration_ms"] for r in results.values())
    total_errors = sum(len(r["errors"]) for r in results.values())

    return {
        "results": results,
        "total_duration_ms": total_ms,
        "total_errors": total_errors,
    }


def get_all_index_states_summary(cache: CacheManager) -> dict[str, Any]:
    """Get a summary of all index states for dashboard display."""
    db = cache.get_db()
    try:
        states = {}
        for scope in ("plugins", "releases", "updates", "tools", "docs"):
            row = db.execute("SELECT * FROM index_state WHERE scope = ?", (scope,)).fetchone()
            states[scope] = dict(row) if row else {
                "scope": scope,
                "status": "not_initialized",
                "signature": "",
                "last_started_at": None,
                "last_finished_at": None,
                "last_error": "",
                "item_count": 0,
                "duration_ms": 0,
            }

        # Item counts from actual tables
        counts = {}
        counts["plugins"] = db.execute("SELECT COUNT(*) AS cnt FROM plugin_index WHERE is_deleted = 0").fetchone()["cnt"]
        counts["packages"] = db.execute("SELECT COUNT(*) AS cnt FROM package_index WHERE is_deleted = 0").fetchone()["cnt"]
        counts["releases"] = db.execute("SELECT COUNT(*) AS cnt FROM release_index WHERE is_deleted = 0").fetchone()["cnt"]
        counts["updates"] = db.execute("SELECT COUNT(*) AS cnt FROM update_index WHERE is_deleted = 0").fetchone()["cnt"]
        counts["tools"] = db.execute("SELECT COUNT(*) AS cnt FROM tool_index WHERE is_deleted = 0").fetchone()["cnt"]
        docs_index = cache.get_cache_entry("docs_index:v1")
        counts["docs"] = int(((docs_index or {}).get("value") or {}).get("summary", {}).get("total") or 0)

        return {"states": states, "counts": counts}
    except Exception as exc:
        return {"states": {}, "counts": {}, "error": str(exc)}
    finally:
        db.close()


# ---------------------------------------------------------------------------
# Index state helpers (shared with plugin_index)
# ---------------------------------------------------------------------------

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


def is_index_populated(cache: CacheManager, scope: str) -> bool:
    """Check if a given index table has any entries."""
    table_map = {
        "plugins": "plugin_index",
        "releases": "release_index",
        "updates": "update_index",
        "tools": "tool_index",
    }
    table = table_map.get(scope)
    if not table:
        return False
    db = cache.get_db()
    try:
        row = db.execute(f"SELECT COUNT(*) AS cnt FROM {table}").fetchone()
        return bool(row and row["cnt"] > 0)
    except Exception:
        return False
    finally:
        db.close()


def get_index_state(cache: CacheManager, scope: str) -> dict[str, Any] | None:
    """Get the index_state for a given scope."""
    db = cache.get_db()
    try:
        row = db.execute("SELECT * FROM index_state WHERE scope = ?", (scope,)).fetchone()
        return dict(row) if row else None
    except Exception:
        return None
    finally:
        db.close()


# ---------------------------------------------------------------------------
# Startup check
# ---------------------------------------------------------------------------

def startup_check_all_indexes(cache: CacheManager, storage: Path) -> str:
    """If any index is empty, refresh it. Returns summary string."""
    results = []
    for scope, refresh_fn in [
        ("releases", refresh_release_index),
        ("updates", refresh_update_index),
        ("tools", refresh_tool_index),
    ]:
        if not is_index_populated(cache, scope):
            result = refresh_fn(cache, storage)
            results.append(f"{scope}: {result['indexed_count']} indexed")

    if not results:
        return "All artifact indexes already populated"
    return "; ".join(results)

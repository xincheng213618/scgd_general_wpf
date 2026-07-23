"""
Artifact index service for ColorVision Marketplace.

Maintains persistent release_index, update_index, and tool_index tables
in SQLite so that API requests read from the database instead of scanning
the file system on every call.

Follows the same pattern as plugin_index.py.
"""

from __future__ import annotations

import hashlib
import os
import threading
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from db_cache import CacheManager, now_iso


_REFRESH_LOCKS = {
    "releases": threading.Lock(),
    "updates": threading.Lock(),
    "tools": threading.Lock(),
}


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def _dir_signature_hash(entries: list[tuple[str, int, int]]) -> str:
    """Compute a lightweight hash from (name, size, mtime_ns) tuples."""
    if not entries:
        return "empty"
    raw = "|".join(f"{n}:{s}:{m}" for n, s, m in sorted(entries))
    return hashlib.md5(raw.encode("utf-8")).hexdigest()[:16]


def _version_sort_tuple(version: str) -> tuple[int, ...]:
    try:
        return tuple(int(part) for part in str(version).split("."))
    except (TypeError, ValueError):
        return ()


def _update_sort_key(item: dict[str, Any]) -> tuple[tuple[int, ...], str]:
    return (_version_sort_tuple(str(item.get("version", ""))), str(item.get("modified", "")))


def _active_index_count(cache: CacheManager, table: str) -> int:
    db = cache.get_db()
    try:
        row = db.execute(f"SELECT COUNT(*) AS cnt FROM {table} WHERE is_deleted = 0").fetchone()
        return int(row["cnt"] if row else 0)
    except Exception:
        return 0
    finally:
        db.close()


def _refresh_failure_result(
    cache: CacheManager,
    *,
    scope: str,
    table: str,
    started: float,
    errors: list[str],
) -> dict[str, Any]:
    elapsed_ms = int((time.monotonic() - started) * 1000)
    _update_index_state(
        cache,
        scope,
        status="error",
        finished_at=_now_iso(),
        item_count=_active_index_count(cache, table),
        duration_ms=elapsed_ms,
        error="; ".join(errors[-3:]),
    )
    return {"indexed_count": 0, "duration_ms": elapsed_ms, "errors": errors}


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
    entries: list[tuple[str, int, int]] = []
    if storage.is_dir():
        for entry in storage.iterdir():
            if entry.is_file():
                try:
                    st = entry.stat()
                    entries.append((entry.name, st.st_size, st.st_mtime_ns))
                except OSError:
                    pass
    history_dir = storage / "History"
    if history_dir.is_dir():
        try:
            history_mtime = history_dir.stat().st_mtime_ns
        except OSError:
            history_mtime = 0
        entries.append(("History", 0, history_mtime))

        try:
            with os.scandir(history_dir) as major_entries:
                for major_entry in major_entries:
                    if not major_entry.is_dir(follow_symlinks=False):
                        continue
                    try:
                        major_stat = major_entry.stat(follow_symlinks=False)
                        entries.append((
                            f"History/{major_entry.name}",
                            0,
                            major_stat.st_mtime_ns,
                        ))
                        with os.scandir(major_entry.path) as branch_entries:
                            for branch_entry in branch_entries:
                                if not branch_entry.is_dir(follow_symlinks=False):
                                    continue
                                branch_stat = branch_entry.stat(follow_symlinks=False)
                                entries.append((
                                    f"History/{major_entry.name}/{branch_entry.name}",
                                    0,
                                    branch_stat.st_mtime_ns,
                                ))
                                with os.scandir(branch_entry.path) as children:
                                    for child in children:
                                        child_stat = child.stat(follow_symlinks=False)
                                        child_kind = "d" if child.is_dir(follow_symlinks=False) else "f"
                                        entries.append((
                                            f"History/{major_entry.name}/{branch_entry.name}/{child.name}:{child_kind}",
                                            child_stat.st_size,
                                            child_stat.st_mtime_ns,
                                        ))
                    except OSError:
                        continue
        except OSError:
            pass
    return _dir_signature_hash(entries)


def refresh_release_index(cache: CacheManager, storage: Path) -> dict[str, Any]:
    """Refresh the release index once per process without blocking duplicate runs."""
    lock = _REFRESH_LOCKS["releases"]
    if not lock.acquire(blocking=False):
        return {
            "indexed_count": 0,
            "duration_ms": 0,
            "errors": [],
            "skipped": True,
            "skip_reason": "refresh already running",
        }
    try:
        return _refresh_release_index_unlocked(cache, storage)
    finally:
        lock.release()


def _refresh_release_index_unlocked(cache: CacheManager, storage: Path) -> dict[str, Any]:
    """Refresh release_index from one pre-scan filesystem snapshot signature."""
    started = time.monotonic()
    now = _now_iso()
    _update_index_state(cache, "releases", status="refreshing", started_at=now)

    errors: list[str] = []
    try:
        if not storage.is_dir():
            raise OSError(f"Storage root unavailable: {storage}")
        # Persist the pre-scan signature with the rows produced by this scan.
        # If files change during scanning, the next periodic check necessarily
        # sees a mismatch instead of accepting an old snapshot under a new sig.
        sig = _release_signature(storage)
        artifacts = _scan_release_artifacts(storage)
    except Exception as exc:
        errors.append(str(exc))
        return _refresh_failure_result(
            cache, scope="releases", table="release_index", started=started, errors=errors,
        )

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

        db.execute("DELETE FROM release_index WHERE is_deleted = 1")
        db.commit()
    except Exception as exc:
        db.rollback()
        errors.append(str(exc))
    finally:
        db.close()

    if errors:
        return _refresh_failure_result(
            cache, scope="releases", table="release_index", started=started, errors=errors,
        )

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

    post_scan_signature = _release_signature(storage)
    return {
        "indexed_count": len(artifacts),
        "duration_ms": elapsed_ms,
        "errors": errors,
        "changed_during_refresh": post_scan_signature != sig,
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


_RELEASE_SELECT_COLUMNS = """
    relative_path, version, filename, size, kind, kind_label, era, era_label,
    source, major_minor, branch, modified, modified_display, display_title
"""
_RELEASE_ORDER_SQL = (
    "release_version_key(version) DESC, modified DESC, relative_path DESC"
)


def _release_version_sql_key(value: Any) -> str:
    parts = str(value or "").split(".")
    if not parts or any(not part.isdigit() for part in parts):
        return ""
    return ".".join(f"{int(part):012d}" for part in parts)


def _prepare_release_read_db(db) -> None:
    db.create_function("release_version_key", 1, _release_version_sql_key)


def _release_index_ready(db) -> bool:
    row = db.execute(
        "SELECT status FROM index_state WHERE scope = 'releases'"
    ).fetchone()
    return bool(row and row["status"] == "ready")


def _release_row_payload(row: Any) -> dict[str, Any]:
    from app_releases import enrich_release_artifact

    item = enrich_release_artifact(dict(row))
    if not item.get("modified_date"):
        item["modified_date"] = str(item.get("modified_display", ""))[:10]
    return item


def _select_release_rows(
    db,
    where_sql: str,
    params: list[Any] | tuple[Any, ...],
    *,
    limit: int,
    offset: int = 0,
    prefer_current: bool = False,
) -> list[dict[str, Any]]:
    current_order = "CASE WHEN source = 'current' THEN 1 ELSE 0 END DESC, " if prefer_current else ""
    rows = db.execute(
        f"""SELECT {_RELEASE_SELECT_COLUMNS}
            FROM release_index
            WHERE is_deleted = 0 AND ({where_sql})
            ORDER BY {current_order}{_RELEASE_ORDER_SQL}
            LIMIT ? OFFSET ?""",
        (*params, max(0, int(limit)), max(0, int(offset))),
    ).fetchall()
    return [_release_row_payload(row) for row in rows]


def _release_counts(db) -> dict[str, int]:
    row = db.execute(
        """SELECT
            SUM(CASE WHEN source = 'current' AND UPPER(COALESCE(kind, '')) != 'APK' THEN 1 ELSE 0 END) AS current_count,
            SUM(CASE WHEN source = 'archive' AND UPPER(COALESCE(kind, '')) != 'APK' THEN 1 ELSE 0 END) AS archive_count,
            SUM(CASE WHEN source = 'current' AND UPPER(COALESCE(kind, '')) = 'APK' THEN 1 ELSE 0 END) AS current_android_count,
            SUM(CASE WHEN source = 'archive' AND UPPER(COALESCE(kind, '')) = 'APK' THEN 1 ELSE 0 END) AS archive_android_count,
            COUNT(DISTINCT CASE
                WHEN source = 'archive' AND UPPER(COALESCE(kind, '')) != 'APK'
                THEN COALESCE(major_minor, '') || CHAR(31) || COALESCE(branch, '')
            END) AS archive_timeline_count,
            COUNT(DISTINCT CASE
                WHEN UPPER(COALESCE(kind, '')) != 'APK' AND COALESCE(branch, '') != ''
                THEN branch
            END) AS release_branch_count
        FROM release_index
        WHERE is_deleted = 0"""
    ).fetchone()
    values = {key: int(row[key] or 0) for key in row.keys()}
    values["android_count"] = (
        values["current_android_count"] + values["archive_android_count"]
    )
    values["archive_more_count"] = max(values["archive_count"] - 120, 0)
    return values


def _latest_release(db, *, android: bool) -> dict[str, Any] | None:
    operator = "=" if android else "!="
    items = _select_release_rows(
        db,
        f"UPPER(COALESCE(kind, '')) {operator} 'APK'",
        (),
        limit=1,
        prefer_current=True,
    )
    return items[0] if items else None


def get_compact_home_releases_from_index(
    cache: CacheManager,
) -> dict[str, Any] | None:
    """Return the bounded release read-model consumed by the compact home API."""
    db = cache.get_db()
    try:
        _prepare_release_read_db(db)
        if not _release_index_ready(db):
            return None

        counts = _release_counts(db)
        current_releases = _select_release_rows(
            db,
            "source = 'current' AND UPPER(COALESCE(kind, '')) != 'APK'",
            (),
            limit=6,
        )
        archive_recent = _select_release_rows(
            db,
            "source = 'archive' AND UPPER(COALESCE(kind, '')) != 'APK'",
            (),
            limit=4,
        )
        return {
            "latest_release": _latest_release(db, android=False),
            "latest_android_release": _latest_release(db, android=True),
            "current_releases": current_releases,
            "archive_recent": archive_recent,
            "current_preview": current_releases,
            **counts,
        }
    except Exception as exc:
        print(f"[release_index] compact home read failed: {exc}")
        return None
    finally:
        db.close()


def _release_archive_where(
    *,
    major_minor: str,
    branch: str,
    kind: str,
    era: str,
) -> tuple[str, list[Any]]:
    clauses = ["source = 'archive'", "UPPER(COALESCE(kind, '')) != 'APK'"]
    params: list[Any] = []
    if major_minor:
        clauses.append("major_minor = ?")
        params.append(major_minor)
    if branch:
        clauses.append("branch = ?")
        params.append(branch)
    if kind:
        clauses.append("UPPER(COALESCE(kind, '')) = ?")
        params.append(kind)
    if era:
        clauses.append("LOWER(COALESCE(era, '')) = ?")
        params.append(era)
    return " AND ".join(clauses), params


def _selected_option(value: str, selected: str, label: str | None = None) -> dict[str, Any]:
    return {"value": value, "label": label or value, "selected": value == selected}


def _release_filter_options_from_index(
    db,
    *,
    major_minor: str,
    branch: str,
    kind: str,
    era: str,
) -> dict[str, list[dict[str, Any]]]:
    base = (
        "is_deleted = 0 AND source = 'archive' "
        "AND UPPER(COALESCE(kind, '')) != 'APK'"
    )

    def distinct(column: str) -> list[str]:
        rows = db.execute(
            f"""SELECT DISTINCT {column} AS value FROM release_index
                WHERE {base} AND COALESCE({column}, '') != '' ORDER BY value"""
        ).fetchall()
        return [str(row["value"]) for row in rows]

    major_values = distinct("major_minor")
    branch_values = distinct("branch")
    kind_values = [value.upper() for value in distinct("kind")]
    era_values = distinct("era")
    era_labels = {
        "archive": "压缩归档时代",
        "installer": "安装包时代",
        "other": "其他记录",
    }
    return {
        "archive_major_minor_options": [_selected_option("", major_minor, "所有主线")]
        + [_selected_option(value, major_minor) for value in major_values],
        "archive_branch_options": [_selected_option("", branch, "所有阶段")]
        + [_selected_option(value, branch) for value in branch_values],
        "archive_kind_options": [_selected_option("", kind, "所有类型")]
        + [_selected_option(value, kind) for value in kind_values],
        "archive_era_options": [_selected_option("", era, "所有时代")]
        + [_selected_option(value, era, era_labels.get(value, value)) for value in era_values],
    }


def _group_key_where(keys: list[tuple[str, str]]) -> tuple[str, list[Any]]:
    clauses: list[str] = []
    params: list[Any] = []
    for major_minor, branch in keys:
        clauses.append("(COALESCE(major_minor, '') = ? AND COALESCE(branch, '') = ?)")
        params.extend((major_minor, branch))
    return " OR ".join(clauses) or "0", params


def _release_group_aggregates(
    db,
    keys: list[tuple[str, str]],
    *,
    filtered_where: str,
    filtered_params: list[Any],
) -> tuple[dict[tuple[str, str], dict[str, Any]], dict[tuple[str, str], dict[str, Any]]]:
    key_where, key_params = _group_key_where(keys)
    full_rows = db.execute(
        f"""SELECT major_minor, branch, COUNT(*) AS group_count,
                MAX(modified) AS latest_modified, MAX(modified_display) AS latest_modified_display,
                MIN(modified) AS earliest_modified, MIN(modified_display) AS earliest_modified_display,
                SUM(CASE WHEN UPPER(COALESCE(kind, '')) IN ('ZIP', 'RAR') THEN 1 ELSE 0 END) AS archive_formats,
                SUM(CASE WHEN UPPER(COALESCE(kind, '')) = 'EXE' THEN 1 ELSE 0 END) AS installers,
                GROUP_CONCAT(DISTINCT UPPER(COALESCE(kind, ''))) AS kind_keys
            FROM release_index
            WHERE is_deleted = 0 AND source = 'archive'
              AND UPPER(COALESCE(kind, '')) != 'APK' AND ({key_where})
            GROUP BY major_minor, branch""",
        key_params,
    ).fetchall()
    visible_rows = db.execute(
        f"""SELECT major_minor, branch, COUNT(*) AS visible_count,
                GROUP_CONCAT(DISTINCT COALESCE(kind_label, kind, '')) AS kind_labels,
                GROUP_CONCAT(DISTINCT COALESCE(era_label, era, '')) AS era_labels
            FROM release_index
            WHERE is_deleted = 0 AND ({filtered_where}) AND ({key_where})
            GROUP BY major_minor, branch""",
        (*filtered_params, *key_params),
    ).fetchall()

    def keyed(rows) -> dict[tuple[str, str], dict[str, Any]]:
        return {
            (str(row["major_minor"] or ""), str(row["branch"] or "")): dict(row)
            for row in rows
        }

    return keyed(full_rows), keyed(visible_rows)


def _time_range_display(earliest: str, latest: str) -> str:
    if not earliest:
        return latest
    if not latest or earliest == latest:
        return earliest
    return f"{earliest} → {latest}"


def get_compact_releases_from_index(
    cache: CacheManager,
    *,
    major_minor: str = "",
    branch: str = "",
    kind: str = "",
    era: str = "",
    page: int = 1,
    page_size: int = 100,
    android_page: int = 1,
    android_page_size: int = 100,
) -> dict[str, Any] | None:
    """Return an independently paged compact releases read-model from SQLite."""
    major_minor = major_minor.strip()
    branch = branch.strip()
    kind = kind.strip().upper()
    era = era.strip().lower()
    page = max(1, int(page))
    page_size = max(1, int(page_size))
    android_page = max(1, int(android_page))
    android_page_size = max(1, int(android_page_size))
    has_filters = any((major_minor, branch, kind, era))

    db = cache.get_db()
    try:
        _prepare_release_read_db(db)
        if not _release_index_ready(db):
            return None

        counts = _release_counts(db)
        filtered_where, filtered_params = _release_archive_where(
            major_minor=major_minor,
            branch=branch,
            kind=kind,
            era=era,
        )
        visible_row = db.execute(
            f"""SELECT COUNT(*) AS item_count,
                    COUNT(DISTINCT COALESCE(major_minor, '') || CHAR(31) || COALESCE(branch, '')) AS group_count
                FROM release_index WHERE is_deleted = 0 AND ({filtered_where})""",
            filtered_params,
        ).fetchone()
        visible_item_count = int(visible_row["item_count"] or 0)
        visible_group_count = int(visible_row["group_count"] or 0)
        total_pages = (
            (visible_item_count + page_size - 1) // page_size if visible_item_count else 0
        )
        effective_page = min(page, max(total_pages, 1))
        page_items = _select_release_rows(
            db,
            filtered_where,
            filtered_params,
            limit=page_size,
            offset=(effective_page - 1) * page_size,
        )

        grouped_page_items: dict[tuple[str, str], list[dict[str, Any]]] = {}
        for item in page_items:
            key = (str(item.get("major_minor", "")), str(item.get("branch", "")))
            grouped_page_items.setdefault(key, []).append(item)
        keys = list(grouped_page_items)
        full_aggregates, visible_aggregates = _release_group_aggregates(
            db,
            keys,
            filtered_where=filtered_where,
            filtered_params=filtered_params,
        ) if keys else ({}, {})

        visible_groups: list[dict[str, Any]] = []
        for index, key in enumerate(keys):
            items = grouped_page_items[key]
            full = full_aggregates.get(key, {})
            visible = visible_aggregates.get(key, {})
            kind_labels = sorted(filter(None, str(visible.get("kind_labels") or "").split(",")))
            era_labels = sorted(filter(None, str(visible.get("era_labels") or "").split(",")))
            kind_keys = sorted(filter(None, str(full.get("kind_keys") or "").split(",")))
            earliest_display = str(full.get("earliest_modified_display") or "")
            latest_display = str(full.get("latest_modified_display") or "")
            visible_groups.append({
                "major_minor": key[0],
                "branch": key[1],
                "count": int(full.get("group_count") or 0),
                "latest_modified": str(full.get("latest_modified") or ""),
                "latest_modified_display": latest_display,
                "earliest_modified": str(full.get("earliest_modified") or ""),
                "earliest_modified_display": earliest_display,
                "time_range_display": _time_range_display(earliest_display, latest_display),
                "kind_summary": " · ".join(kind_labels),
                "kind_keys": kind_keys,
                "contains_archive_only_formats": bool(full.get("archive_formats")),
                "contains_installer_artifacts": bool(full.get("installers")),
                "latest_relative_path": str(items[0].get("relative_path", "")),
                "visible_items": items,
                "visible_count": int(visible.get("visible_count") or 0),
                "page_item_count": len(items),
                "visible_kind_summary": " · ".join(kind_labels),
                "visible_era_summary": " · ".join(era_labels),
                "is_expanded": has_filters or index == 0,
            })

        android_total_item_count = counts["archive_android_count"]
        android_total_pages = (
            (android_total_item_count + android_page_size - 1) // android_page_size
            if android_total_item_count else 0
        )
        effective_android_page = min(android_page, max(android_total_pages, 1))
        archived_android_releases = _select_release_rows(
            db,
            "source = 'archive' AND UPPER(COALESCE(kind, '')) = 'APK'",
            (),
            limit=android_page_size,
            offset=(effective_android_page - 1) * android_page_size,
        )
        current_releases = _select_release_rows(
            db,
            "source = 'current' AND UPPER(COALESCE(kind, '')) != 'APK'",
            (),
            limit=100,
        )
        current_android_releases = _select_release_rows(
            db,
            "source = 'current' AND UPPER(COALESCE(kind, '')) = 'APK'",
            (),
            limit=100,
        )

        return {
            "app_info": {
                "latest_release": _latest_release(db, android=False),
                "latest_android_release": _latest_release(db, android=True),
                "current_releases": current_releases,
                "current_android_releases": current_android_releases,
                "archived_android_releases": archived_android_releases,
                **counts,
            },
            "archive_visible_groups": visible_groups,
            "archive_visible_group_count": visible_group_count,
            "archive_visible_item_count": visible_item_count,
            "archive_page": effective_page,
            "archive_page_size": page_size,
            "archive_total_pages": total_pages,
            "archive_has_previous": effective_page > 1,
            "archive_has_next": effective_page < total_pages,
            "archive_page_item_count": len(page_items),
            "archive_page_group_count": len(visible_groups),
            "android_page": effective_android_page,
            "android_page_size": android_page_size,
            "android_total_pages": android_total_pages,
            "android_has_previous": effective_android_page > 1,
            "android_has_next": effective_android_page < android_total_pages,
            "android_page_item_count": len(archived_android_releases),
            "android_total_item_count": android_total_item_count,
            "release_filters": {
                "major_minor": major_minor,
                "branch": branch,
                "kind": kind,
                "era": era,
                "has_filters": has_filters,
                "reset_href": "/releases",
            },
            **_release_filter_options_from_index(
                db,
                major_minor=major_minor,
                branch=branch,
                kind=kind,
                era=era,
            ),
        }
    except Exception as exc:
        print(f"[release_index] compact releases read failed: {exc}")
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
    entries: list[tuple[str, int, int]] = []
    if update_dir.is_dir():
        for entry in update_dir.iterdir():
            if entry.is_file():
                try:
                    st = entry.stat()
                    entries.append((entry.name, st.st_size, st.st_mtime_ns))
                except OSError:
                    pass
    return _dir_signature_hash(entries)


def refresh_update_index(cache: CacheManager, storage: Path) -> dict[str, Any]:
    """Refresh the update index once per process without blocking duplicates."""
    lock = _REFRESH_LOCKS["updates"]
    if not lock.acquire(blocking=False):
        return {
            "indexed_count": 0,
            "duration_ms": 0,
            "errors": [],
            "skipped": True,
            "skip_reason": "refresh already running",
        }
    try:
        return _refresh_update_index_unlocked(cache, storage)
    finally:
        lock.release()


def _refresh_update_index_unlocked(cache: CacheManager, storage: Path) -> dict[str, Any]:
    """Refresh update_index from one pre-scan filesystem signature."""
    started = time.monotonic()
    now = _now_iso()
    _update_index_state(cache, "updates", status="refreshing", started_at=now)

    errors: list[str] = []
    try:
        if not storage.is_dir():
            raise OSError(f"Storage root unavailable: {storage}")
        sig = _update_signature(storage)
        items = _scan_update_packages(storage)
    except Exception as exc:
        errors.append(str(exc))
        return _refresh_failure_result(
            cache, scope="updates", table="update_index", started=started, errors=errors,
        )

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

        db.execute("DELETE FROM update_index WHERE is_deleted = 1")
        db.commit()
    except Exception as exc:
        db.rollback()
        errors.append(str(exc))
    finally:
        db.close()

    if errors:
        return _refresh_failure_result(
            cache, scope="updates", table="update_index", started=started, errors=errors,
        )

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

    post_scan_signature = _update_signature(storage)
    return {
        "indexed_count": len(items),
        "duration_ms": elapsed_ms,
        "errors": errors,
        "changed_during_refresh": post_scan_signature != sig,
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
    entries: list[tuple[str, int, int]] = []
    if tool_dir.is_dir():
        for entry in tool_dir.iterdir():
            try:
                st = entry.stat()
                entries.append((entry.name, st.st_size if entry.is_file() else 0, st.st_mtime_ns))
            except OSError:
                pass
    return _dir_signature_hash(entries)


def refresh_tool_index(cache: CacheManager, storage: Path) -> dict[str, Any]:
    """Refresh the tool index once per process without blocking duplicates."""
    lock = _REFRESH_LOCKS["tools"]
    if not lock.acquire(blocking=False):
        return {
            "indexed_count": 0,
            "duration_ms": 0,
            "errors": [],
            "skipped": True,
            "skip_reason": "refresh already running",
        }
    try:
        return _refresh_tool_index_unlocked(cache, storage)
    finally:
        lock.release()


def _refresh_tool_index_unlocked(cache: CacheManager, storage: Path) -> dict[str, Any]:
    """Refresh tool_index from one pre-scan filesystem signature."""
    started = time.monotonic()
    now = _now_iso()
    _update_index_state(cache, "tools", status="refreshing", started_at=now)

    errors: list[str] = []
    try:
        if not storage.is_dir():
            raise OSError(f"Storage root unavailable: {storage}")
        sig = _tool_signature(storage)
        items = _scan_tool_entries(storage)
    except Exception as exc:
        errors.append(str(exc))
        return _refresh_failure_result(
            cache, scope="tools", table="tool_index", started=started, errors=errors,
        )

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

        db.execute("DELETE FROM tool_index WHERE is_deleted = 1")
        db.commit()
    except Exception as exc:
        db.rollback()
        errors.append(str(exc))
    finally:
        db.close()

    if errors:
        return _refresh_failure_result(
            cache, scope="tools", table="tool_index", started=started, errors=errors,
        )

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

    post_scan_signature = _tool_signature(storage)
    return {
        "indexed_count": len(items),
        "duration_ms": elapsed_ms,
        "errors": errors,
        "changed_during_refresh": post_scan_signature != sig,
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

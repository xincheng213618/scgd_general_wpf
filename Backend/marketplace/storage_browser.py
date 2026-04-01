from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable


def storage_relative(storage: Path, path: Path) -> str:
    try:
        return path.relative_to(storage).as_posix()
    except ValueError:
        return path.name


def _format_modified_timestamps(timestamp: float) -> tuple[str, str]:
    dt = datetime.fromtimestamp(timestamp, tz=timezone.utc)
    return dt.isoformat(), dt.strftime("%Y-%m-%d %H:%M")


def _build_listing_item(entry: Path, relative_path: str) -> dict[str, Any] | None:
    try:
        stat = entry.stat()
    except OSError:
        return None

    modified_iso, modified_display = _format_modified_timestamps(stat.st_mtime)
    item: dict[str, Any] = {
        "name": entry.name,
        "is_dir": entry.is_dir(),
        "path": relative_path,
        "relative_path": relative_path,
        "modified": modified_display,
        "modified_iso": modified_iso,
    }
    if not item["is_dir"]:
        item["size"] = stat.st_size
    return item


def _fast_directory_file_count(entry: Path) -> int:
    try:
        return sum(1 for child in entry.iterdir() if child.is_file())
    except OSError:
        return 0


def build_entry_record(storage: Path, entry: Path, relative_path: str) -> dict[str, Any]:
    item = _build_listing_item(entry, relative_path) or {
        "name": entry.name,
        "is_dir": entry.is_dir(),
        "path": relative_path,
        "relative_path": relative_path,
        "modified": "",
        "modified_iso": "",
    }
    item["modified_display"] = item["modified"]
    if item["is_dir"]:
        item["file_count"] = sum(1 for child in entry.rglob("*") if child.is_file())
    return item


def list_directory_contents(target: Path, subpath: str, *, limit: int | None = None) -> list[dict[str, Any]]:
    items: list[dict[str, Any]] = []
    for entry in sorted(target.iterdir(), key=lambda e: (e.is_file(), e.name.lower())):
        if entry.name.startswith("."):
            continue
        relative_path = f"{subpath}/{entry.name}" if subpath else entry.name
        info = _build_listing_item(entry, relative_path)
        if info:
            items.append(info)
            if limit is not None and len(items) >= limit:
                break
    return items


def build_breadcrumbs(subpath: str, *, root_label: str = "Home") -> list[tuple[str, str]]:
    parts = [p for p in subpath.split("/") if p]
    breadcrumbs = [(root_label, "/browse")]
    for index, part in enumerate(parts):
        breadcrumbs.append((part, "/browse/" + "/".join(parts[: index + 1])))
    return breadcrumbs


def scan_storage_overview(
    storage: Path,
    *,
    get_cache_entry: Callable[..., dict[str, Any] | None],
    set_cache_entry: Callable[..., None],
    overview_cache_key: str,
    overview_cache_ttl_seconds: int,
    directory_count_cache_ttl_seconds: int,
) -> list[dict[str, Any]]:
    cached = get_cache_entry(overview_cache_key)
    if cached:
        return cached["value"]

    if not storage.is_dir():
        return []

    items: list[dict[str, Any]] = []
    for entry in sorted(storage.iterdir()):
        if entry.name.startswith("."):
            continue
        if entry.is_dir():
            cache_key = f"dir_file_count:{storage_relative(storage, entry)}"
            count_cache = get_cache_entry(cache_key)
            estimated = False
            if count_cache:
                file_count = count_cache["value"].get("file_count", 0)
            else:
                file_count = _fast_directory_file_count(entry)
                estimated = True
            items.append(
                {
                    "name": entry.name,
                    "type": "dir",
                    "file_count": file_count,
                    "file_count_estimated": estimated,
                    "modified": datetime.fromtimestamp(
                        entry.stat().st_mtime, tz=timezone.utc
                    ).isoformat(),
                }
            )
        elif entry.is_file():
            items.append(
                {
                    "name": entry.name,
                    "type": "file",
                    "size": entry.stat().st_size,
                    "modified": datetime.fromtimestamp(
                        entry.stat().st_mtime, tz=timezone.utc
                    ).isoformat(),
                }
            )

    set_cache_entry(
        overview_cache_key,
        items,
        ttl_seconds=overview_cache_ttl_seconds,
        signature="storage",
    )
    return items


def build_storage_summary(overview: list[dict[str, Any]]) -> dict[str, int]:
    directory_count = sum(1 for item in overview if item["type"] == "dir")
    top_level_file_count = sum(1 for item in overview if item["type"] == "file")
    nested_file_count = sum(item.get("file_count", 0) for item in overview if item["type"] == "dir")
    top_level_size = sum(int(item.get("size", 0) or 0) for item in overview if item["type"] == "file")
    return {
        "item_count": len(overview),
        "directory_count": directory_count,
        "top_level_file_count": top_level_file_count,
        "total_file_count": top_level_file_count + nested_file_count,
        "top_level_size": top_level_size,
        "estimated_directory_count": sum(1 for item in overview if item.get("file_count_estimated")),
    }


def get_storage_overview_context(
    storage: Path,
    *,
    get_cache_entry: Callable[..., dict[str, Any] | None],
    set_cache_entry: Callable[..., None],
    overview_cache_key: str,
    overview_cache_ttl_seconds: int,
    directory_count_cache_ttl_seconds: int,
) -> tuple[list[dict[str, Any]], dict[str, int], dict[str, Any]]:
    cached = get_cache_entry(overview_cache_key)
    if cached:
        overview = cached["value"]
        return overview, build_storage_summary(overview), {
            "cache_hit": True,
            "updated_at": cached["updated_at"],
            "updated_at_display": str(cached["updated_at"]).replace("T", " ")[:19],
            "ttl_seconds": overview_cache_ttl_seconds,
        }

    overview = scan_storage_overview(
        storage,
        get_cache_entry=get_cache_entry,
        set_cache_entry=set_cache_entry,
        overview_cache_key=overview_cache_key,
        overview_cache_ttl_seconds=overview_cache_ttl_seconds,
        directory_count_cache_ttl_seconds=directory_count_cache_ttl_seconds,
    )
    now = datetime.now(timezone.utc)
    return overview, build_storage_summary(overview), {
        "cache_hit": False,
        "updated_at": now.isoformat(),
        "updated_at_display": now.strftime("%Y-%m-%d %H:%M:%S"),
        "ttl_seconds": overview_cache_ttl_seconds,
    }


def summarize_directory_items(items: list[dict[str, Any]]) -> dict[str, int]:
    file_count = sum(1 for item in items if not item["is_dir"])
    directory_count = sum(1 for item in items if item["is_dir"])
    total_size = sum(int(item.get("size", 0) or 0) for item in items if not item["is_dir"])
    return {
        "item_count": len(items),
        "file_count": file_count,
        "directory_count": directory_count,
        "total_size": total_size,
    }


def build_storage_page_context(storage: Path, relative_path: str) -> dict[str, Any]:
    target = storage / Path(*[part for part in relative_path.split("/") if part])
    items = list_directory_contents(target, relative_path) if target.exists() and target.is_dir() else []
    return {
        "target": target,
        "items": items,
        "summary": summarize_directory_items(items),
        "subpath": relative_path,
        "breadcrumbs": build_breadcrumbs(relative_path),
        "exists": target.exists(),
        "parent_subpath": "/".join(relative_path.split("/")[:-1]) if relative_path else "",
    }


def build_storage_preview_context(storage: Path, relative_path: str, *, limit: int = 8) -> dict[str, Any]:
    target = storage / Path(*[part for part in relative_path.split("/") if part])
    items = (
        list_directory_contents(target, relative_path, limit=limit)
        if target.exists() and target.is_dir()
        else []
    )
    return {
        "target": target,
        "items": items,
        "summary": summarize_directory_items(items),
        "subpath": relative_path,
        "breadcrumbs": build_breadcrumbs(relative_path),
        "exists": target.exists(),
        "parent_subpath": "/".join(relative_path.split("/")[:-1]) if relative_path else "",
        "is_preview": True,
        "preview_limit": limit,
    }



from __future__ import annotations

from pathlib import Path
from typing import Any, Callable

from storage_browser import build_storage_page_context
from update_retention import build_update_summary, scan_update_packages


RETENTION_NOTE = "保留最新增量包，以及每个小版本的 .1 增量包。"


def build_index_page_context(
    storage: Path,
    *,
    get_app_info: Callable[[], dict[str, Any]],
    get_storage_overview_context: Callable[[], tuple[list[dict[str, Any]], dict[str, Any], dict[str, Any]]],
) -> dict[str, Any]:
    app_info = get_app_info()
    overview, overview_summary, overview_meta = get_storage_overview_context()
    update_packages, update_other_files = scan_update_packages(storage)
    tools_context = build_storage_page_context(storage, "Tool")
    return {
        "app_info": app_info,
        "overview": overview,
        "overview_summary": overview_summary,
        "overview_meta": overview_meta,
        "update_packages": update_packages[:8],
        "update_summary": build_update_summary(update_packages, update_other_files),
        "tool_items": tools_context["items"][:8],
        "tool_summary": tools_context["summary"],
    }


def build_updates_page_context(storage: Path) -> dict[str, Any]:
    canonical_packages, other_files = scan_update_packages(storage)
    other_update_items = [
        {
            "name": item["filename"],
            "is_dir": False,
            "path": item["relative_path"],
            "relative_path": item["relative_path"],
            "modified": item["modified"][:19],
            "size": item["size"],
        }
        for item in other_files
    ]
    return {
        "update_packages": canonical_packages,
        "other_update_files": other_files,
        "other_update_items": other_update_items,
        "update_summary": build_update_summary(canonical_packages, other_files),
        "retention_note": RETENTION_NOTE,
    }


def build_tools_page_context(storage: Path) -> dict[str, Any]:
    context = build_storage_page_context(storage, "Tool")
    return {
        "items": context["items"],
        "summary": context["summary"],
        "subpath": "Tool",
        "breadcrumbs": context["breadcrumbs"],
        "exists": context["exists"],
        "parent_subpath": context["parent_subpath"],
    }


def build_browse_page_context(storage: Path, relative_path: str) -> dict[str, Any]:
    context = build_storage_page_context(storage, relative_path)
    return {
        "items": context["items"],
        "subpath": relative_path,
        "breadcrumbs": context["breadcrumbs"],
        "parent_subpath": context["parent_subpath"],
    }


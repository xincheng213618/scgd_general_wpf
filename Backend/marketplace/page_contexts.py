from __future__ import annotations

from pathlib import Path
from typing import Any, Callable

from storage_browser import build_storage_page_context
from update_retention import build_update_summary, scan_update_packages, scan_update_preview_fast


RETENTION_NOTE = "保留最新增量包，以及每个小版本的 .1 增量包。"

FILESYSTEM_SPOTLIGHT = {
    "History": {
        "label": "History 归档",
        "description": "查看完整历史制品、早期 ZIP / RAR 和分支阶段演进。",
        "href": "/browse/History",
        "icon": "bi-clock-history",
    },
    "Plugins": {
        "label": "Plugins",
        "description": "插件当前包、历史归档和清单文件都从这里核对。",
        "href": "/browse/Plugins",
        "icon": "bi-puzzle",
    },
    "Update": {
        "label": "Update",
        "description": "增量包和保留策略对应的真实文件目录。",
        "href": "/browse/Update",
        "icon": "bi-arrow-repeat",
    },
    "Tool": {
        "label": "Tool / 软件",
        "description": "内部工具、驱动和外部软件安装包入口。",
        "href": "/browse/Tool",
        "icon": "bi-tools",
    },
    "Feedback": {
        "label": "Feedback",
        "description": "用户反馈、日志附件和排障材料归档目录。",
        "href": "/browse/Feedback",
        "icon": "bi-inboxes",
    },
}


def _build_filesystem_spotlight(overview: list[dict[str, Any]]) -> list[dict[str, Any]]:
    by_name = {str(item.get("name", "")): item for item in overview if item.get("type") == "dir"}
    items: list[dict[str, Any]] = []
    for name in ("History", "Plugins", "Update", "Tool", "Feedback"):
        info = by_name.get(name)
        meta = FILESYSTEM_SPOTLIGHT[name]
        items.append(
            {
                "name": name,
                "label": meta["label"],
                "description": meta["description"],
                "href": meta["href"],
                "icon": meta["icon"],
                "exists": info is not None,
                "file_count": int(info.get("file_count", 0) or 0) if info else 0,
                "modified": str(info.get("modified", ""))[:19] if info else "未创建",
            }
        )
    return items


def _selected_option(value: str, selected: str, label: str | None = None) -> dict[str, Any]:
    return {
        "value": value,
        "label": label or value,
        "selected": value == selected,
    }


def _archive_kind_options(app_info: dict[str, Any], selected: str) -> list[dict[str, Any]]:
    kinds = sorted(
        {
            str(item.get("kind", "")).strip().upper()
            for group in app_info.get("archive_timeline_groups", [])
            for item in group.get("items", [])
            if str(item.get("kind", "")).strip()
        }
    )
    options = [_selected_option("", selected, "所有类型")]
    options.extend(_selected_option(kind, selected) for kind in kinds)
    return options


def _archive_era_options(app_info: dict[str, Any], selected: str) -> list[dict[str, Any]]:
    eras = sorted(
        {
            str(item.get("era", "")).strip()
            for group in app_info.get("archive_timeline_groups", [])
            for item in group.get("items", [])
            if str(item.get("era", "")).strip()
        }
    )
    labels = {
        "archive": "压缩归档时代",
        "installer": "安装包时代",
        "other": "其他记录",
    }
    options = [_selected_option("", selected, "所有时代")]
    options.extend(_selected_option(era, selected, labels.get(era, era)) for era in eras)
    return options


def _release_filter_options(app_info: dict[str, Any], *, major_minor: str, branch: str, kind: str, era: str) -> dict[str, Any]:
    groups = list(app_info.get("archive_timeline_groups", []))
    major_minor_values = sorted({str(group.get("major_minor", "")).strip() for group in groups if str(group.get("major_minor", "")).strip()})
    branch_values = sorted({str(group.get("branch", "")).strip() for group in groups if str(group.get("branch", "")).strip()})
    return {
        "archive_major_minor_options": [_selected_option("", major_minor, "所有主线")] + [
            _selected_option(value, major_minor) for value in major_minor_values
        ],
        "archive_branch_options": [_selected_option("", branch, "所有阶段")] + [
            _selected_option(value, branch) for value in branch_values
        ],
        "archive_kind_options": _archive_kind_options(app_info, kind),
        "archive_era_options": _archive_era_options(app_info, era),
    }


def _group_matches_filters(group: dict[str, Any], *, major_minor: str, branch: str, kind: str, era: str) -> list[dict[str, Any]]:
    if major_minor and str(group.get("major_minor", "")) != major_minor:
        return []
    if branch and str(group.get("branch", "")) != branch:
        return []

    items = list(group.get("items", []))
    if kind:
        items = [item for item in items if str(item.get("kind", "")).upper() == kind]
    if era:
        items = [item for item in items if str(item.get("era", "")) == era]
    return items


def build_releases_page_context(
    app_info: dict[str, Any],
    *,
    major_minor: str = "",
    branch: str = "",
    kind: str = "",
    era: str = "",
) -> dict[str, Any]:
    major_minor = major_minor.strip()
    branch = branch.strip()
    kind = kind.strip().upper()
    era = era.strip().lower()
    has_filters = any((major_minor, branch, kind, era))

    visible_groups: list[dict[str, Any]] = []
    visible_item_count = 0
    for index, group in enumerate(app_info.get("archive_timeline_groups", [])):
        visible_items = _group_matches_filters(
            group,
            major_minor=major_minor,
            branch=branch,
            kind=kind,
            era=era,
        )
        if not visible_items:
            continue
        group_copy = dict(group)
        group_copy["visible_items"] = visible_items
        group_copy["visible_count"] = len(visible_items)
        group_copy["visible_kind_summary"] = " · ".join(
            sorted({str(item.get("kind_label", item.get("kind", ""))) for item in visible_items if str(item.get("kind_label", item.get("kind", ""))).strip()})
        )
        group_copy["visible_era_summary"] = " · ".join(
            sorted({str(item.get("era_label", item.get("era", ""))) for item in visible_items if str(item.get("era_label", item.get("era", ""))).strip()})
        )
        group_copy["is_expanded"] = has_filters or index == 0
        visible_groups.append(group_copy)
        visible_item_count += len(visible_items)

    options = _release_filter_options(app_info, major_minor=major_minor, branch=branch, kind=kind, era=era)
    return {
        "app_info": app_info,
        "archive_visible_groups": visible_groups,
        "archive_visible_group_count": len(visible_groups),
        "archive_visible_item_count": visible_item_count,
        "release_filters": {
            "major_minor": major_minor,
            "branch": branch,
            "kind": kind,
            "era": era,
            "has_filters": has_filters,
            "reset_href": "/releases",
        },
        **options,
    }


def _build_recent_change_dashboard(
    app_info: dict[str, Any],
    filesystem_spotlight: list[dict[str, Any]],
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    changes: list[dict[str, Any]] = []

    for release in list(app_info.get("current_releases", []))[:3]:
        changes.append(
            {
                "title": str(release.get("display_title", release.get("version", "版本记录"))),
                "subtitle": f"当前制品 · {release.get('kind_label', release.get('kind', '文件记录'))}",
                "timestamp": str(release.get("modified_display", "")),
                "sort_key": str(release.get("modified", "")),
                "href": f"/download/{release.get('relative_path', '')}",
                "action_label": "下载",
                "category": "当前版本",
            }
        )

    for release in list(app_info.get("archive_recent", []))[:4]:
        changes.append(
            {
                "title": str(release.get("display_title", release.get("version", "历史记录"))),
                "subtitle": f"历史制品 · {release.get('kind_label', release.get('kind', '文件记录'))}",
                "timestamp": str(release.get("modified_display", "")),
                "sort_key": str(release.get("modified", "")),
                "href": f"/download/{release.get('relative_path', '')}",
                "action_label": "下载",
                "category": "历史制品",
            }
        )

    for item in filesystem_spotlight:
        if not item.get("exists"):
            continue
        changes.append(
            {
                "title": str(item.get("label", item.get("name", "目录"))),
                "subtitle": f"目录 · {item.get('file_count', 0)} 个文件",
                "timestamp": str(item.get("modified", "")),
                "sort_key": str(item.get("modified", "")),
                "href": str(item.get("href", "/browse")),
                "action_label": "打开目录",
                "category": "目录",
            }
        )

    changes.sort(key=lambda item: item.get("sort_key", ""), reverse=True)
    visible = changes[:8]
    return visible, {
        "change_count": len(visible),
        "release_count": sum(1 for item in visible if item.get("category") != "目录"),
        "directory_count": sum(1 for item in visible if item.get("category") == "目录"),
        "latest_timestamp": visible[0]["timestamp"] if visible else "暂无",
    }


def build_index_page_context(
    storage: Path,
    *,
    get_app_info: Callable[[], dict[str, Any]],
    get_storage_overview_context: Callable[[], tuple[list[dict[str, Any]], dict[str, Any], dict[str, Any]]],
    get_tool_preview: Callable[[], dict[str, Any]] | None = None,
) -> dict[str, Any]:
    app_info = get_app_info()
    overview, overview_summary, overview_meta = get_storage_overview_context()
    update_packages, update_summary = scan_update_preview_fast(storage)
    tools_context = get_tool_preview() if get_tool_preview is not None else build_storage_page_context(storage, "Tool")
    filesystem_spotlight = _build_filesystem_spotlight(overview)
    recent_change_dashboard, recent_change_summary = _build_recent_change_dashboard(
        app_info,
        filesystem_spotlight,
    )
    return {
        "app_info": app_info,
        "overview": overview,
        "overview_summary": overview_summary,
        "overview_meta": overview_meta,
        "filesystem_spotlight": filesystem_spotlight,
        "recent_change_dashboard": recent_change_dashboard,
        "recent_change_summary": recent_change_summary,
        "update_packages": update_packages,
        "update_summary": update_summary,
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


def build_upload_page_context(
    *,
    message: str | None,
    error: str | None,
    max_upload_size_bytes: int,
    plugin_package_keep_count: int,
) -> dict[str, Any]:
    return {
        "message": message,
        "error": error,
        "max_upload_size_bytes": max_upload_size_bytes,
        "plugin_package_keep_count": plugin_package_keep_count,
        "supports_html_upload": True,
        "supports_api_publish": True,
    }


def build_browse_page_context(storage: Path, relative_path: str) -> dict[str, Any]:
    context = build_storage_page_context(storage, relative_path)
    return {
        "items": context["items"],
        "summary": context["summary"],
        "subpath": relative_path,
        "breadcrumbs": context["breadcrumbs"],
        "parent_subpath": context["parent_subpath"],
        "exists": context["exists"],
    }


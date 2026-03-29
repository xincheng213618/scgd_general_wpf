from __future__ import annotations

from typing import Any, Callable, Iterable


PluginItem = dict[str, Any]


def normalize_catalog_sort(sort_by: str | None, *, default: str = "updated") -> str:
    value = str(sort_by or default).strip().lower()
    return {
        "updatedat": "updated",
        "modified": "updated",
        "modifiedat": "updated",
        "updated": "updated",
        "name": "name",
        "downloads": "downloads",
    }.get(value, value)


def filter_plugin_summaries(
    plugins: Iterable[PluginItem],
    *,
    keyword: str = "",
    category: str = "",
) -> list[PluginItem]:
    items = list(plugins)
    keyword = keyword.strip().lower()
    category = category.strip().lower()

    if keyword:
        items = [
            plugin
            for plugin in items
            if keyword in str(plugin.get("name", "")).lower()
            or keyword in str(plugin.get("id", "")).lower()
            or keyword in str(plugin.get("description", "")).lower()
        ]

    if category:
        items = [
            plugin
            for plugin in items
            if str(plugin.get("category", "")).lower() == category
        ]

    return items


def sort_plugin_summaries(
    plugins: Iterable[PluginItem],
    *,
    sort_by: str = "updated",
    descending: bool = True,
) -> list[PluginItem]:
    items = list(plugins)
    normalized_sort = normalize_catalog_sort(sort_by)

    if normalized_sort == "name":
        items.sort(key=lambda plugin: str(plugin.get("name", "")).lower(), reverse=descending)
    elif normalized_sort == "downloads":
        items.sort(key=lambda plugin: int(plugin.get("total_downloads", 0) or 0), reverse=descending)
    else:
        items.sort(key=lambda plugin: str(plugin.get("modified", "")), reverse=descending)

    return items


def collect_plugin_categories(plugins: Iterable[PluginItem]) -> list[str]:
    return sorted(
        {
            str(plugin.get("category", "")).strip()
            for plugin in plugins
            if str(plugin.get("category", "")).strip()
        }
    )


def paginate_plugin_summaries(
    plugins: Iterable[PluginItem],
    *,
    page: int,
    page_size: int,
) -> tuple[list[PluginItem], int, int]:
    items = list(plugins)
    total_count = len(items)
    start = max(page - 1, 0) * page_size
    paged = items[start : start + page_size]
    total_pages = (total_count + page_size - 1) // page_size if page_size else 0
    return paged, total_count, total_pages


def build_plugin_summary_payload(
    plugin: PluginItem,
    *,
    icon_url_builder: Callable[[str], str | None],
) -> PluginItem:
    plugin_id = str(plugin.get("id", ""))
    has_icon = bool(plugin.get("has_icon"))
    return {
        "pluginId": plugin_id,
        "name": plugin.get("name", ""),
        "description": plugin.get("description", ""),
        "author": plugin.get("author", ""),
        "category": plugin.get("category", ""),
        "iconUrl": icon_url_builder(plugin_id) if has_icon and plugin_id else None,
        "latestVersion": plugin.get("version", ""),
        "totalDownloads": plugin.get("total_downloads", 0),
        "updatedAt": plugin.get("modified", ""),
    }


def build_plugin_search_result(
    plugins: Iterable[PluginItem],
    *,
    page: int,
    page_size: int,
    icon_url_builder: Callable[[str], str | None],
) -> PluginItem:
    paged, total_count, total_pages = paginate_plugin_summaries(
        plugins,
        page=page,
        page_size=page_size,
    )
    return {
        "items": [
            build_plugin_summary_payload(plugin, icon_url_builder=icon_url_builder)
            for plugin in paged
        ],
        "totalCount": total_count,
        "page": page,
        "pageSize": page_size,
        "totalPages": total_pages,
    }

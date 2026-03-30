from __future__ import annotations

from typing import Any, Callable, Iterable
from urllib.parse import urlencode

from plugin_queries import (
    build_plugin_search_result,
    collect_plugin_categories,
    filter_plugin_summaries,
    normalize_catalog_sort,
    paginate_plugin_summaries,
    sort_plugin_summaries,
)


PluginItem = dict[str, Any]
ALLOWED_CATALOG_SORTS = frozenset({"updated", "name", "downloads"})
ALLOWED_CATALOG_SORT_ORDERS = frozenset({"asc", "desc"})
DEFAULT_HTML_PAGE_SIZE = 12
DEFAULT_API_PAGE_SIZE = 20


def normalize_catalog_sort_name(sort_by: str | None) -> str:
    return normalize_catalog_sort(sort_by)


def normalize_catalog_sort_order(sort_by: str, sort_order: str | None = None) -> str:
    value = str(sort_order or "").strip().lower()
    if value in ALLOWED_CATALOG_SORT_ORDERS:
        return value
    return "asc" if normalize_catalog_sort_name(sort_by) == "name" else "desc"


def filter_and_sort_plugins(
    plugins: Iterable[PluginItem],
    *,
    keyword: str = "",
    category: str = "",
    sort_by: str = "updated",
    sort_order: str | None = None,
) -> tuple[list[PluginItem], str, str]:
    normalized_sort = normalize_catalog_sort_name(sort_by)
    normalized_order = normalize_catalog_sort_order(normalized_sort, sort_order)
    filtered = filter_plugin_summaries(
        plugins,
        keyword=keyword,
        category=category,
    )
    return (
        sort_plugin_summaries(
            filtered,
            sort_by=normalized_sort,
            descending=normalized_order != "asc",
        ),
        normalized_sort,
        normalized_order,
    )


def _build_catalog_url(
    *,
    keyword: str,
    category: str,
    sort_by: str,
    page: int | None = None,
    page_size: int | None = None,
) -> str:
    params: list[tuple[str, str]] = []
    if keyword:
        params.append(("q", keyword))
    if category:
        params.append(("category", category))
    if sort_by and sort_by != "updated":
        params.append(("sort", sort_by))
    if page_size and page_size != DEFAULT_HTML_PAGE_SIZE:
        params.append(("pageSize", str(page_size)))
    if page and page > 1:
        params.append(("page", str(page)))
    query = urlencode(params)
    return f"/plugins?{query}" if query else "/plugins"


def _build_pagination_items(
    *,
    page: int,
    total_pages: int,
    keyword: str,
    category: str,
    sort_by: str,
    page_size: int,
) -> list[dict[str, Any]]:
    if total_pages <= 1:
        return []

    visible: list[int | None]
    if total_pages <= 7:
        visible = list(range(1, total_pages + 1))
    else:
        visible = [1]
        if page > 3:
            visible.append(None)
        for candidate in range(max(2, page - 1), min(total_pages, page + 1) + 1):
            visible.append(candidate)
        if page < total_pages - 2:
            visible.append(None)
        visible.append(total_pages)

    items: list[dict[str, Any]] = [
        {
            "label": "上一页",
            "page": page - 1,
            "href": _build_catalog_url(
                keyword=keyword,
                category=category,
                sort_by=sort_by,
                page=page - 1,
                page_size=page_size,
            ),
            "active": False,
            "disabled": page <= 1,
            "ellipsis": False,
        }
    ]

    for candidate in visible:
        if candidate is None:
            items.append(
                {
                    "label": "…",
                    "page": None,
                    "href": "#",
                    "active": False,
                    "disabled": True,
                    "ellipsis": True,
                }
            )
            continue

        items.append(
            {
                "label": str(candidate),
                "page": candidate,
                "href": _build_catalog_url(
                    keyword=keyword,
                    category=category,
                    sort_by=sort_by,
                    page=candidate,
                    page_size=page_size,
                ),
                "active": candidate == page,
                "disabled": False,
                "ellipsis": False,
            }
        )

    items.append(
        {
            "label": "下一页",
            "page": page + 1,
            "href": _build_catalog_url(
                keyword=keyword,
                category=category,
                sort_by=sort_by,
                page=page + 1,
                page_size=page_size,
            ),
            "active": False,
            "disabled": page >= total_pages,
            "ellipsis": False,
        }
    )
    return items


def _build_category_options(
    plugins: Iterable[PluginItem],
    *,
    selected_category: str,
) -> list[dict[str, Any]]:
    counts: dict[str, int] = {}
    for plugin in plugins:
        category = str(plugin.get("category", "")).strip()
        if not category:
            continue
        counts[category] = counts.get(category, 0) + 1

    options = [
        {
            "value": "",
            "label": "所有分类",
            "count": sum(counts.values()),
            "selected": not selected_category,
        }
    ]
    for category in sorted(counts):
        options.append(
            {
                "value": category,
                "label": category,
                "count": counts[category],
                "selected": category == selected_category,
            }
        )
    return options


def collect_catalog_categories(plugins: Iterable[PluginItem]) -> list[str]:
    return collect_plugin_categories(list(plugins))


def build_plugin_catalog_page_context(
    all_plugins: Iterable[PluginItem],
    *,
    keyword: str = "",
    category: str = "",
    sort_by: str = "updated",
    page: int = 1,
    page_size: int = DEFAULT_HTML_PAGE_SIZE,
) -> dict[str, Any]:
    keyword = keyword.strip()
    category = category.strip()
    page_size = max(int(page_size), 1)

    all_items = list(all_plugins)
    filtered_sorted, normalized_sort, normalized_order = filter_and_sort_plugins(
        all_items,
        keyword=keyword,
        category=category,
        sort_by=sort_by,
    )
    total_count = len(filtered_sorted)
    total_pages = (total_count + page_size - 1) // page_size if total_count else 0
    current_page = min(max(int(page), 1), max(total_pages, 1))
    paged_plugins, _, _ = paginate_plugin_summaries(
        filtered_sorted,
        page=current_page,
        page_size=page_size,
    )
    showing_from = (current_page - 1) * page_size + 1 if total_count else 0
    showing_to = showing_from + len(paged_plugins) - 1 if total_count else 0

    page_size_options = sorted({DEFAULT_HTML_PAGE_SIZE, 24, 48, page_size})
    return {
        "plugins": paged_plugins,
        "keyword": keyword,
        "category": category,
        "sort_by": normalized_sort,
        "sort_order": normalized_order,
        "categories": collect_catalog_categories(all_items),
        "category_options": _build_category_options(all_items, selected_category=category),
        "total_plugins": len(all_items),
        "filtered_count": total_count,
        "page": current_page,
        "page_size": page_size,
        "page_size_options": page_size_options,
        "total_pages": total_pages,
        "has_pagination": total_pages > 1,
        "showing_from": showing_from,
        "showing_to": showing_to,
        "active_filter_count": sum(1 for value in (keyword, category) if value) + (0 if normalized_sort == "updated" else 1),
        "pagination_items": _build_pagination_items(
            page=current_page,
            total_pages=total_pages,
            keyword=keyword,
            category=category,
            sort_by=normalized_sort,
            page_size=page_size,
        ),
        "reset_url": "/plugins",
    }


def build_plugin_search_api_result(
    all_plugins: Iterable[PluginItem],
    *,
    keyword: str = "",
    category: str = "",
    sort_by: str = "updated",
    sort_order: str = "desc",
    page: int = 1,
    page_size: int = DEFAULT_API_PAGE_SIZE,
    icon_url_builder: Callable[[str], str | None],
) -> dict[str, Any]:
    filtered_sorted, normalized_sort, normalized_order = filter_and_sort_plugins(
        all_plugins,
        keyword=keyword,
        category=category,
        sort_by=sort_by,
        sort_order=sort_order,
    )
    payload = build_plugin_search_result(
        filtered_sorted,
        page=page,
        page_size=page_size,
        icon_url_builder=icon_url_builder,
    )
    payload["sortBy"] = normalized_sort
    payload["sortOrder"] = normalized_order
    return payload


def build_plugin_detail_api_result(
    info: PluginItem,
    *,
    icon_url_builder: Callable[[str], str | None],
) -> dict[str, Any]:
    return {
        "pluginId": info["id"],
        "name": info["name"],
        "description": info["description"],
        "author": info["author"],
        "url": info["url"],
        "category": info["category"],
        "latestVersion": info["version"],
        "requiresVersion": info["requires"],
        "iconUrl": icon_url_builder(info["id"]) if info["has_icon"] else None,
        "readme": info["readme"],
        "changelog": info["changelog"],
        "totalDownloads": info["total_downloads"],
        "updatedAt": info["modified"],
        "currentPackageCount": len(info["current_packages"]),
        "historicalPackageCount": len(info["historical_packages"]),
        "versions": [
            {
                "version": pkg["version"],
                "requiresVersion": info["requires"],
                "changeLog": info["changelog"],
                "fileSize": pkg["size"],
                "fileHash": pkg.get("fileHash"),
                "downloadCount": 0,
                "createdAt": pkg["modified"],
                "source": pkg.get("source", "current"),
            }
            for pkg in info["current_packages"]
        ],
        "archivedVersions": [
            {
                "version": pkg["version"],
                "requiresVersion": info["requires"],
                "changeLog": info["changelog"],
                "fileSize": pkg["size"],
                "fileHash": pkg.get("fileHash"),
                "downloadCount": 0,
                "createdAt": pkg["modified"],
                "source": pkg.get("source", "archive"),
            }
            for pkg in info["historical_packages"]
        ],
    }


"""Publish completeness checks for the admin console."""

from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from db_cache import CacheManager
from services.app_latest_version_cache import get_latest_version_cached
from services.docs_site import build_docs_status


def _status(ok: bool, warning: bool = False) -> str:
    if ok:
        return "ok"
    return "warning" if warning else "error"


def _check(key: str, title: str, status: str, detail: str, action_href: str = "") -> dict[str, str]:
    return {"key": key, "title": title, "status": status, "detail": detail, "actionHref": action_href}


def _version_tuple(version: str) -> tuple[int, ...]:
    try:
        return tuple(int(part) for part in str(version).split(".") if part != "")
    except ValueError:
        return ()


def _load_releases(cache: CacheManager, storage: Path) -> list[dict[str, Any]]:
    from services.artifact_index import get_releases_from_index, _scan_release_artifacts

    indexed = get_releases_from_index(cache)
    if indexed is not None:
        return indexed
    return _scan_release_artifacts(storage)


def _load_updates(cache: CacheManager, storage: Path) -> list[dict[str, Any]]:
    from services.artifact_index import get_updates_from_index, _scan_update_packages

    indexed = get_updates_from_index(cache)
    if indexed is not None:
        return indexed
    return _scan_update_packages(storage)


def _load_plugins(cache: CacheManager, storage: Path) -> list[dict[str, Any]]:
    from services.plugin_index import get_plugin_catalog_from_index

    indexed = get_plugin_catalog_from_index(cache, {})
    if indexed is not None:
        return indexed

    plugins_dir = storage / "Plugins"
    if not plugins_dir.is_dir():
        return []

    items: list[dict[str, Any]] = []
    for entry in sorted(plugins_dir.iterdir(), key=lambda item: item.name.lower()):
        if not entry.is_dir():
            continue
        latest = _read_text(entry / "LATEST_RELEASE")
        items.append({
            "id": entry.name,
            "plugin_id": entry.name,
            "name": entry.name,
            "latest_version": latest,
            "version": latest,
            "readme": _read_text(entry / "README.md"),
            "changelog": _read_text(entry / "CHANGELOG.md"),
            "current_package_count": len([p for p in entry.iterdir() if p.is_file() and p.suffix.lower() in (".cvxp", ".zip")]),
        })
    return items


def _read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8").strip()
    except (OSError, UnicodeDecodeError):
        return ""


def _app_release_section(storage: Path, releases: list[dict[str, Any]], updates: list[dict[str, Any]]) -> dict[str, Any]:
    latest_version = get_latest_version_cached(storage)
    current_releases = [item for item in releases if item.get("source") == "current"]
    latest_release = next((item for item in current_releases if item.get("version") == latest_version), None)
    if latest_release is None and current_releases:
        current_releases.sort(key=lambda item: (_version_tuple(str(item.get("version", ""))), str(item.get("modified", ""))), reverse=True)
        latest_release = current_releases[0]

    latest_update_matches = [item for item in updates if item.get("version") == latest_version]
    changelog_path = storage / "CHANGELOG.md"
    changelog = _read_text(changelog_path)
    changelog_mentions_latest = bool(latest_version and latest_version in changelog)

    checks = [
        _check(
            "installer",
            "安装包",
            _status(bool(latest_release)),
            f"检测到 {len(current_releases)} 个根目录安装包。" if latest_release else "未检测到根目录安装包。",
            "/admin/files",
        ),
        _check(
            "incremental_update",
            "增量包",
            _status(bool(latest_update_matches), warning=bool(updates)),
            f"检测到 {len(latest_update_matches)} 个与最新版本 {latest_version or '-'} 匹配的增量包。"
            if latest_update_matches else f"未检测到与最新版本 {latest_version or '-'} 匹配的增量包。",
            "/updates",
        ),
        _check(
            "changelog",
            "CHANGELOG",
            _status(bool(changelog and changelog_mentions_latest), warning=bool(changelog)),
            "CHANGELOG.md 已包含最新版本记录。"
            if changelog_mentions_latest else "CHANGELOG.md 未包含最新版本号，发布说明可能未同步。",
            "/changelog",
        ),
    ]

    return {
        "latestVersion": latest_version,
        "currentReleaseCount": len(current_releases),
        "updatePackageCount": len(updates),
        "matchedUpdateCount": len(latest_update_matches),
        "changelogExists": bool(changelog),
        "changelogMentionsLatest": changelog_mentions_latest,
        "latestRelease": latest_release,
        "checks": checks,
    }


def _plugin_docs_section(plugins: list[dict[str, Any]]) -> dict[str, Any]:
    missing_readme = []
    missing_changelog = []
    missing_package = []
    for plugin in plugins:
        plugin_id = str(plugin.get("plugin_id") or plugin.get("id") or plugin.get("name") or "")
        display_name = str(plugin.get("name") or plugin_id)
        item = {
            "pluginId": plugin_id,
            "name": display_name,
            "latestVersion": str(plugin.get("latest_version") or plugin.get("version") or ""),
        }
        if not str(plugin.get("readme") or "").strip():
            missing_readme.append(item)
        if not str(plugin.get("changelog") or "").strip():
            missing_changelog.append(item)
        if int(plugin.get("current_package_count") or 0) <= 0:
            missing_package.append(item)

    return {
        "total": len(plugins),
        "missingReadme": missing_readme,
        "missingChangelog": missing_changelog,
        "missingPackage": missing_package,
        "checks": [
            _check(
                "plugin_readme",
                "插件 README",
                _status(len(missing_readme) == 0, warning=bool(plugins)),
                f"{len(missing_readme)} 个插件缺少 README。" if missing_readme else "插件 README 已齐全。",
                "/plugins",
            ),
            _check(
                "plugin_changelog",
                "插件 CHANGELOG",
                _status(len(missing_changelog) == 0, warning=bool(plugins)),
                f"{len(missing_changelog)} 个插件缺少 CHANGELOG。" if missing_changelog else "插件 CHANGELOG 已齐全。",
                "/plugins",
            ),
            _check(
                "plugin_package",
                "插件安装包",
                _status(len(missing_package) == 0, warning=bool(plugins)),
                f"{len(missing_package)} 个插件没有当前包。" if missing_package else "插件当前包已齐全。",
                "/admin/publish",
            ),
        ],
    }


def build_publish_integrity_report(storage: Path, cache: CacheManager) -> dict[str, Any]:
    releases = _load_releases(cache, storage)
    updates = _load_updates(cache, storage)
    plugins = _load_plugins(cache, storage)
    app = _app_release_section(storage, releases, updates)
    plugin_docs = _plugin_docs_section(plugins)
    docs = build_docs_status(cache)

    docs_check = _check(
        "docs_site",
        "文档站",
        _status(bool(docs.get("built") and int(docs.get("indexedDocumentCount") or 0) > 0), warning=bool(docs.get("sourceExists"))),
        f"文档站已构建，索引 {int(docs.get('indexedDocumentCount') or 0)} 篇。"
        if docs.get("built") else "文档站未构建或构建产物缺失。",
        docs.get("entryUrl") or "/docs",
    )

    checks = [*app["checks"], *plugin_docs["checks"], docs_check]
    error_count = sum(1 for item in checks if item["status"] == "error")
    warning_count = sum(1 for item in checks if item["status"] == "warning")
    ok_count = sum(1 for item in checks if item["status"] == "ok")
    if error_count:
        overall = "error"
    elif warning_count:
        overall = "warning"
    else:
        overall = "ok"

    score = int((ok_count / max(len(checks), 1)) * 100)
    return {
        "status": overall,
        "score": score,
        "okCount": ok_count,
        "warningCount": warning_count,
        "errorCount": error_count,
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "checks": checks,
        "app": app,
        "plugins": plugin_docs,
        "docs": {
            "built": bool(docs.get("built")),
            "indexedDocumentCount": int(docs.get("indexedDocumentCount") or 0),
            "indexUpdatedAt": docs.get("indexUpdatedAt"),
        },
    }

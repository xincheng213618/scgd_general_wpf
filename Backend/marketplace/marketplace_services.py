from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable

from flask import g, has_request_context, request

from app_changelog import build_changelog_lookup, changelog_signature, get_cached_changelog_analysis
from app_releases import (
    build_app_release_context,
    build_release_artifact,
    build_release_timeline,
    reconcile_app_release_history as reconcile_app_release_history_impl,
    release_sort_key,
    scan_app_release_artifacts as scan_app_release_artifacts_impl,
)
from download_stats import get_download_counts, hash_ip, record_download
from plugin_marketplace import (
    get_plugin_detail as get_plugin_detail_impl,
    reconcile_all_plugin_package_histories as reconcile_all_plugin_package_histories_impl,
    reconcile_plugin_package_history as reconcile_plugin_package_history_impl,
    scan_plugin_summaries as scan_plugin_summaries_impl,
)
from storage_browser import (
    build_storage_preview_context,
    build_storage_summary as build_storage_summary_impl,
    get_storage_overview_context as get_storage_overview_context_impl,
    scan_storage_overview as scan_storage_overview_impl,
)
from storage_paths import resolve_storage_file as resolve_storage_file_impl
from update_retention import repair_update_storage_layout


@dataclass(frozen=True)
class MarketplaceCacheSettings:
    overview_cache_key: str
    overview_cache_ttl_seconds: int
    app_releases_cache_key: str
    app_releases_cache_ttl_seconds: int
    directory_count_cache_ttl_seconds: int
    plugin_info_cache_ttl_seconds: int
    changelog_analysis_cache_key: str
    changelog_analysis_cache_ttl_seconds: int
    home_releases_snapshot_cache_key: str
    home_releases_snapshot_ttl_seconds: int
    home_tool_preview_cache_key: str
    home_tool_preview_ttl_seconds: int
    release_timeline_cache_key: str
    release_timeline_cache_ttl_seconds: int


class MarketplaceDataService:
    def __init__(
        self,
        *,
        storage_getter: Callable[[], Path],
        config_getter: Callable[[], dict[str, Any]],
        get_cache_entry: Callable[..., dict[str, Any] | None],
        set_cache_entry: Callable[..., None],
        refresh_related_caches: Callable[..., None],
        get_db: Callable[[], Any],
        read_text_file: Callable[[Path], str | None],
        render_markdown_cached: Callable[..., Any],
        cache_settings: MarketplaceCacheSettings,
    ):
        self._storage_getter = storage_getter
        self._config_getter = config_getter
        self._get_cache_entry = get_cache_entry
        self._set_cache_entry = set_cache_entry
        self._refresh_related_caches = refresh_related_caches
        self._get_db = get_db
        self._read_text_file = read_text_file
        self._render_markdown_cached = render_markdown_cached
        self._cache = cache_settings

    def _storage(self) -> Path:
        return self._storage_getter()

    def _config(self) -> dict[str, Any]:
        return self._config_getter()

    @staticmethod
    def _path_mtime(path: Path) -> float:
        try:
            return path.stat().st_mtime
        except OSError:
            return 0.0

    def request_cached_value(self, cache_key: str, loader: Callable[[], Any]) -> Any:
        if not has_request_context():
            return loader()
        if not hasattr(g, cache_key):
            setattr(g, cache_key, loader())
        return getattr(g, cache_key)

    def scan_app_release_artifacts(self) -> list[dict[str, Any]]:
        return scan_app_release_artifacts_impl(
            self._storage(),
            get_cache_entry=self._get_cache_entry,
            set_cache_entry=self._set_cache_entry,
            cache_key=self._cache.app_releases_cache_key,
            ttl_seconds=self._cache.app_releases_cache_ttl_seconds,
        )

    def get_app_release_context(self) -> dict[str, Any]:
        return build_app_release_context(self.scan_app_release_artifacts())

    def reconcile_app_release_history(self, keep_latest: int | None = None) -> list[dict[str, str]]:
        keep_count = int(keep_latest or self._config().get("app_release_keep_count", 5) or 5)
        return reconcile_app_release_history_impl(
            self._storage(),
            keep_latest=keep_count,
            on_changed=lambda _: self._refresh_related_caches(relative_path="History"),
        )

    def resolve_storage_file(self, relative_path: str) -> Path:
        return resolve_storage_file_impl(
            self._storage(),
            relative_path,
            repair_updates=repair_update_storage_layout,
        )

    def storage_relative(self, path: Path) -> str:
        try:
            return path.relative_to(self._storage()).as_posix()
        except ValueError:
            return path.name

    def get_download_counts(self) -> dict[str, int]:
        return get_download_counts(self._get_db)

    def get_request_download_counts(self) -> dict[str, int]:
        return self.request_cached_value("download_counts", self.get_download_counts)

    def scan_plugins(self, download_counts: dict[str, int] | None = None) -> list[dict]:
        if download_counts is None:
            download_counts = self.get_download_counts()
        return scan_plugin_summaries_impl(
            self._storage(),
            download_counts=download_counts,
            get_cache_entry=self._get_cache_entry,
            set_cache_entry=self._set_cache_entry,
            ttl_seconds=self._cache.plugin_info_cache_ttl_seconds,
        )

    def get_request_plugin_catalog(self) -> list[dict]:
        return self.request_cached_value(
            "plugin_catalog",
            lambda: self.scan_plugins(download_counts=self.get_request_download_counts()),
        )

    def get_plugin_info(
        self,
        plugin_id: str,
        download_counts: dict[str, int] | None = None,
    ) -> dict[str, Any] | None:
        if download_counts is None:
            download_counts = self.get_download_counts()
        return get_plugin_detail_impl(
            self._storage(),
            plugin_id,
            download_counts=download_counts,
            get_cache_entry=self._get_cache_entry,
            set_cache_entry=self._set_cache_entry,
            ttl_seconds=self._cache.plugin_info_cache_ttl_seconds,
        )

    def reconcile_plugin_package_history(
        self,
        plugin_id: str,
        keep_latest: int | None = None,
    ) -> list[dict[str, str]]:
        keep_count = int(keep_latest or self._config().get("plugin_package_keep_count", 3) or 3)
        return reconcile_plugin_package_history_impl(
            self._storage(),
            plugin_id,
            keep_latest=keep_count,
            on_changed=lambda changed_plugin_id: self._refresh_related_caches(
                plugin_id=changed_plugin_id,
                relative_path=f"Plugins/{changed_plugin_id}",
            ),
        )

    def reconcile_all_plugin_package_histories(self) -> dict[str, list[dict[str, str]]]:
        keep_count = int(self._config().get("plugin_package_keep_count", 3) or 3)
        return reconcile_all_plugin_package_histories_impl(
            self._storage(),
            keep_latest=keep_count,
            on_changed=lambda changed_plugin_id: self._refresh_related_caches(
                plugin_id=changed_plugin_id,
                relative_path=f"Plugins/{changed_plugin_id}",
            ),
        )

    def record_download(self, plugin_id: str, version: str):
        record_download(
            self._get_db,
            plugin_id=plugin_id,
            version=version,
            client_ip=request.remote_addr,
            client_version=request.headers.get("X-Client-Version", ""),
        )

    @staticmethod
    def hash_ip(ip: str | None) -> str:
        return hash_ip(ip)

    def _collect_home_archive_preview(self) -> list[dict[str, Any]]:
        history_dir = self._storage() / "History"
        if not history_dir.is_dir():
            return []

        preview_artifacts: list[dict[str, Any]] = []
        major_dirs = sorted(
            (path for path in history_dir.iterdir() if path.is_dir()),
            key=self._path_mtime,
            reverse=True,
        )[:6]

        collected_groups = 0
        for major_dir in major_dirs:
            branch_dirs = sorted(
                (path for path in major_dir.iterdir() if path.is_dir()),
                key=self._path_mtime,
                reverse=True,
            )[:3]
            for branch_dir in branch_dirs:
                collected_groups += 1
                files = sorted(
                    (path for path in branch_dir.iterdir() if path.is_file()),
                    key=self._path_mtime,
                    reverse=True,
                )[:3]
                for file_path in files:
                    artifact = build_release_artifact(self._storage(), file_path, "archive")
                    if artifact:
                        preview_artifacts.append(artifact)
                if collected_groups >= 4:
                    return sorted(preview_artifacts, key=release_sort_key, reverse=True)

        return sorted(preview_artifacts, key=release_sort_key, reverse=True)

    def build_home_release_snapshot(self) -> dict[str, Any]:
        full_cache = self._get_cache_entry(self._cache.app_releases_cache_key)
        if full_cache:
            context = build_app_release_context(full_cache["value"])
            context["release_preview_fast"] = False
            context["archive_count_estimated"] = False
            context["archive_preview_note"] = ""
            return context

        cached = self._get_cache_entry(self._cache.home_releases_snapshot_cache_key)
        if cached:
            return cached["value"]

        current_releases: list[dict[str, Any]] = []
        storage = self._storage()
        if storage.is_dir():
            for entry in storage.iterdir():
                if not entry.is_file():
                    continue
                artifact = build_release_artifact(storage, entry, "current")
                if artifact:
                    current_releases.append(artifact)
        current_releases.sort(key=release_sort_key, reverse=True)

        archive_preview = self._collect_home_archive_preview()
        context = build_app_release_context(current_releases + archive_preview)
        context["release_preview_fast"] = True
        context["archive_count_estimated"] = bool(archive_preview)
        context["archive_preview_note"] = "首页已启用快速历史预览；完整历史与精确统计请进入版本档案页。"
        self._set_cache_entry(
            self._cache.home_releases_snapshot_cache_key,
            context,
            ttl_seconds=self._cache.home_releases_snapshot_ttl_seconds,
            signature="home",
        )
        return context

    def build_release_app_info(self) -> dict[str, Any]:
        releases = self.scan_app_release_artifacts()
        return {
            "latest_version": self._read_text_file(self._storage() / "LATEST_RELEASE") or "",
            **build_app_release_context(releases),
        }

    def build_home_app_info(self) -> dict[str, Any]:
        changelog_path = self._storage() / "CHANGELOG.md"
        changelog = self._read_text_file(changelog_path) or ""
        preview_text = "\n".join(changelog.splitlines()[:24])
        signature = changelog_signature(changelog_path)
        return {
            "latest_version": self._read_text_file(self._storage() / "LATEST_RELEASE") or "",
            **self.build_home_release_snapshot(),
            "has_changelog": bool(changelog.strip()),
            "changelog_preview_html": self._render_markdown_cached(
                cache_key="markdown:changelog_preview:v1",
                signature=f"preview:{signature}:{len(preview_text)}",
                text=preview_text,
            ),
        }

    def build_changelog_app_info(self) -> dict[str, Any]:
        changelog_path = self._storage() / "CHANGELOG.md"
        changelog = self._read_text_file(changelog_path) or ""
        signature = changelog_signature(changelog_path)
        releases = self.scan_app_release_artifacts()
        timeline_signature = f"{len(releases)}:{signature}"
        cached_timeline = self._get_cache_entry(
            self._cache.release_timeline_cache_key,
            signature=timeline_signature,
        )
        changelog_analysis = get_cached_changelog_analysis(
            changelog_path,
            get_cache_entry=self._get_cache_entry,
            set_cache_entry=self._set_cache_entry,
            cache_key=self._cache.changelog_analysis_cache_key,
            ttl_seconds=self._cache.changelog_analysis_cache_ttl_seconds,
        )
        if cached_timeline:
            release_timeline = cached_timeline["value"]
        else:
            release_timeline = build_release_timeline(
                releases,
                changelog_lookup=build_changelog_lookup(changelog_analysis.get("entries", [])),
            )
            self._set_cache_entry(
                self._cache.release_timeline_cache_key,
                release_timeline,
                ttl_seconds=self._cache.release_timeline_cache_ttl_seconds,
                signature=timeline_signature,
            )
        return {
            "latest_version": self._read_text_file(self._storage() / "LATEST_RELEASE") or "",
            "changelog": changelog,
            "changelog_html": self._render_markdown_cached(
                cache_key="markdown:changelog_full:v1",
                signature=f"full:{signature}",
                text=changelog,
            ),
            "changelog_analysis": changelog_analysis,
            "release_timeline": release_timeline,
        }

    def get_request_home_app_info(self) -> dict[str, Any]:
        return self.request_cached_value("home_app_info", self.build_home_app_info)

    def get_request_release_app_info(self) -> dict[str, Any]:
        return self.request_cached_value("release_app_info", self.build_release_app_info)

    def get_request_changelog_app_info(self) -> dict[str, Any]:
        return self.request_cached_value("changelog_app_info", self.build_changelog_app_info)

    def build_home_tool_preview(self) -> dict[str, Any]:
        cached = self._get_cache_entry(self._cache.home_tool_preview_cache_key)
        if cached:
            return cached["value"]

        preview = build_storage_preview_context(self._storage(), "Tool", limit=8)
        self._set_cache_entry(
            self._cache.home_tool_preview_cache_key,
            preview,
            ttl_seconds=self._cache.home_tool_preview_ttl_seconds,
            signature="tool",
        )
        return preview

    def get_request_home_tool_preview(self) -> dict[str, Any]:
        return self.request_cached_value("home_tool_preview", self.build_home_tool_preview)

    def get_app_info(self) -> dict[str, Any]:
        changelog_path = self._storage() / "CHANGELOG.md"
        changelog = self._read_text_file(changelog_path) or ""
        preview_lines = changelog.splitlines()[:24]
        release_context = self.get_app_release_context()
        signature = changelog_signature(changelog_path)
        changelog_analysis = get_cached_changelog_analysis(
            changelog_path,
            get_cache_entry=self._get_cache_entry,
            set_cache_entry=self._set_cache_entry,
            cache_key=self._cache.changelog_analysis_cache_key,
            ttl_seconds=self._cache.changelog_analysis_cache_ttl_seconds,
        )
        return {
            "latest_version": self._read_text_file(self._storage() / "LATEST_RELEASE") or "",
            "changelog": changelog,
            "changelog_html": self._render_markdown_cached(
                cache_key="markdown:changelog_full:v1",
                signature=f"full:{signature}",
                text=changelog,
            ),
            "changelog_preview_html": self._render_markdown_cached(
                cache_key="markdown:changelog_preview:v1",
                signature=f"preview:{signature}:{len(preview_lines)}",
                text="\n".join(preview_lines),
            ),
            "changelog_analysis": changelog_analysis,
            **release_context,
        }

    def scan_storage_overview(self) -> list[dict[str, Any]]:
        return scan_storage_overview_impl(
            self._storage(),
            get_cache_entry=self._get_cache_entry,
            set_cache_entry=self._set_cache_entry,
            overview_cache_key=self._cache.overview_cache_key,
            overview_cache_ttl_seconds=self._cache.overview_cache_ttl_seconds,
            directory_count_cache_ttl_seconds=self._cache.directory_count_cache_ttl_seconds,
        )

    @staticmethod
    def build_storage_summary(overview: list[dict[str, Any]]) -> dict[str, Any]:
        return build_storage_summary_impl(overview)

    def get_storage_overview_context(self) -> tuple[list[dict[str, Any]], dict[str, Any], dict[str, Any]]:
        return get_storage_overview_context_impl(
            self._storage(),
            get_cache_entry=self._get_cache_entry,
            set_cache_entry=self._set_cache_entry,
            overview_cache_key=self._cache.overview_cache_key,
            overview_cache_ttl_seconds=self._cache.overview_cache_ttl_seconds,
            directory_count_cache_ttl_seconds=self._cache.directory_count_cache_ttl_seconds,
        )


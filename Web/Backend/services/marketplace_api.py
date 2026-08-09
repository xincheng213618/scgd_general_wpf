"""Typed use-case facades consumed by marketplace HTTP routes."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any, BinaryIO, Callable

from catalog_view_models import (
    ALLOWED_CATALOG_SORTS,
    ALLOWED_CATALOG_SORT_ORDERS,
    build_plugin_detail_api_result,
    build_plugin_search_api_result,
    collect_catalog_categories,
    normalize_catalog_sort_name,
)
from db_cache import CacheManager, PLUGIN_INFO_CACHE_TTL_SECONDS
from marketplace_services import MarketplaceDataService
from package_publish import (
    extract_package_version,
    finalize_plugin_publish,
    persist_plugin_metadata,
    save_package_file,
    validate_api_publish_request,
)
from plugin_marketplace import prewarm_plugin_metadata
from services.request_context import RequestContext
from storage_paths import (
    is_safe_id,
    is_safe_version,
    normalize_relative_path,
    sanitize_filename,
)
from storage_uploads import store_legacy_upload
from update_retention import prune_update_packages, repair_update_storage_layout


class MarketplaceQueryError(ValueError):
    pass


@dataclass(frozen=True)
class PublishPackageCommand:
    package: Any
    plugin_id: str
    version: str
    name: str
    description: str
    author: str
    category: str
    requires_version: str
    changelog: str
    icon: Any = None


class MarketplaceCatalogService:
    def __init__(
        self,
        data: MarketplaceDataService,
        cache: CacheManager,
        *,
        storage_getter: Callable[[], Path],
        render_markdown: Callable[..., Any],
    ):
        self._data = data
        self._cache = cache
        self._storage_getter = storage_getter
        self._render_markdown = render_markdown

    def search(
        self,
        request_context: RequestContext,
        *,
        keyword: str,
        category: str,
        author: str,
        sort_by: str,
        sort_order: str,
        page: int,
        page_size: int,
    ) -> dict[str, Any]:
        normalized_sort = normalize_catalog_sort_name(sort_by)
        if normalized_sort not in ALLOWED_CATALOG_SORTS:
            raise MarketplaceQueryError("Invalid SortBy parameter")
        if sort_order not in ALLOWED_CATALOG_SORT_ORDERS:
            raise MarketplaceQueryError("Invalid SortOrder parameter")
        return build_plugin_search_api_result(
            self._data.get_request_plugin_catalog(request_context),
            keyword=keyword,
            category=category,
            author=author,
            sort_by=normalized_sort,
            sort_order=sort_order,
            page=page,
            page_size=page_size,
            icon_url_builder=self.icon_url,
        )

    def categories(self, request_context: RequestContext) -> list[str]:
        return collect_catalog_categories(
            self._data.get_request_plugin_catalog(request_context)
        )

    def detail(
        self,
        plugin_id: str,
        request_context: RequestContext,
    ) -> dict[str, Any] | None:
        info = self._data.get_plugin_info(
            plugin_id,
            download_counts=self._data.get_request_download_counts(request_context),
        )
        if not info:
            return None
        return build_plugin_detail_api_result(
            info,
            icon_url_builder=self.icon_url,
            render_markdown=self._render_markdown,
        )

    def latest_versions(self, plugin_ids: list[str]) -> dict[str, str]:
        if not plugin_ids:
            return {}
        from services.app_latest_version_cache import get_plugin_latest_versions_cached

        return get_plugin_latest_versions_cached(
            self._storage_getter(), plugin_ids, self._cache
        )

    @staticmethod
    def icon_url(plugin_id: str) -> str:
        return f"/plugins/{plugin_id}/icon"


class MarketplaceStorageService:
    def __init__(self, storage_getter: Callable[[], Path]):
        self._storage_getter = storage_getter

    @property
    def root(self) -> Path:
        return self._storage_getter()

    @staticmethod
    def is_safe_id(value: str) -> bool:
        return is_safe_id(value)

    @staticmethod
    def is_safe_version(value: str) -> bool:
        return is_safe_version(value)

    def legacy_plugin_path(self, filepath: str) -> Path:
        return self.root / "Plugins" / filepath

    def legacy_path(self, filepath: str) -> Path:
        path = self.root / filepath
        if filepath.replace("\\", "/").startswith("Update/") and not path.exists():
            repair_update_storage_layout(self.root)
            path = self.root / filepath
        return path

    def is_within(self, path: Path, root: Path | None = None) -> bool:
        try:
            path.resolve().relative_to((root or self.root).resolve())
            return True
        except ValueError:
            return False


class MarketplacePackageService:
    def __init__(
        self,
        data: MarketplaceDataService,
        cache: CacheManager,
        storage: MarketplaceStorageService,
        *,
        max_upload_size_bytes: int,
    ):
        self._data = data
        self._cache = cache
        self._storage = storage
        self._max_upload_size_bytes = max_upload_size_bytes

    def resolve_download(self, plugin_id: str, version: str) -> Path | None:
        filename = f"{plugin_id}-{version}.cvxp"
        current = self._storage.root / "Plugins" / plugin_id / filename
        if current.exists():
            return current
        history = self._storage.root / "History" / "Plugins" / plugin_id / filename
        return history if history.exists() else None

    def record_download(
        self,
        plugin_id: str,
        version: str,
        request_context: RequestContext,
    ) -> None:
        self._data.record_download(plugin_id, version, request_context)

    def publish(
        self,
        command: PublishPackageCommand,
        request_context: RequestContext,
    ) -> dict[str, str]:
        storage = self._storage.root
        upload_request = validate_api_publish_request(
            command.package,
            command.plugin_id,
            command.version,
            sanitize_filename=sanitize_filename,
            validate_plugin_id=is_safe_id,
            validate_version=is_safe_version,
        )
        save_result = save_package_file(
            storage,
            command.package,
            upload_request,
            validate_plugin_id=is_safe_id,
            read_text_file=self._read_text_file,
            version_tuple=self._version_tuple,
            reconcile_plugin_package_history=self._data.reconcile_plugin_package_history,
        )
        persist_plugin_metadata(
            save_result.plugin_dir,
            plugin_id=upload_request.plugin_id,
            version=upload_request.version,
            name=command.name or upload_request.plugin_id,
            description=command.description,
            author=command.author,
            category=command.category,
            requires_version=command.requires_version,
            changelog_text=command.changelog,
            icon_file=command.icon,
            manifest_loader=self._load_manifest,
        )
        finalize_plugin_publish(
            storage,
            plugin_id=upload_request.plugin_id,
            version=upload_request.version,
            refresh_related_caches=self._cache.refresh_related_caches,
            prewarm_plugin_metadata=prewarm_plugin_metadata,
            get_download_counts=self._data.get_download_counts,
            get_cache_entry=self._cache.get_cache_entry,
            set_cache_entry=self._cache.set_cache_entry,
            ttl_seconds=PLUGIN_INFO_CACHE_TTL_SECONDS,
        )

        # Preserve the historical publish contract: index refresh is best-effort
        # after every artifact and metadata side effect has completed.
        try:
            from services.storage_events import _refresh_plugin_index

            _refresh_plugin_index(
                self._cache,
                storage,
                f"Plugins/{upload_request.plugin_id}",
                get_download_counts=self._data.get_download_counts,
                get_cache_entry=self._cache.get_cache_entry,
                set_cache_entry=self._cache.set_cache_entry,
                ttl_seconds=PLUGIN_INFO_CACHE_TTL_SECONDS,
                actor=request_context.actor,
            )
        except Exception:
            pass

        return {
            "pluginId": upload_request.plugin_id,
            "version": upload_request.version,
        }

    def legacy_upload(
        self,
        raw_filepath: str,
        stream: BinaryIO,
        request_context: RequestContext,
    ) -> None:
        storage = self._storage.root

        def on_upload_complete(normalized_path: str):
            from services.storage_events import on_storage_change

            on_storage_change(
                self._cache,
                storage,
                normalized_path,
                actor=request_context.actor,
            )

        store_legacy_upload(
            storage=storage,
            raw_filepath=raw_filepath,
            stream=stream,
            max_size=self._max_upload_size_bytes,
            normalize_relative_path=normalize_relative_path,
            validate_plugin_id=is_safe_id,
            extract_package_version=lambda filename, plugin_id: extract_package_version(
                filename,
                plugin_id,
                sanitize_filename=sanitize_filename,
                validate_version=is_safe_version,
            ),
            is_root_release_file=lambda path: (
                path.parent == storage
                and path.suffix.lower() in (".exe", ".zip", ".rar")
            ),
            reconcile_app_release_history=self._data.reconcile_app_release_history,
            reconcile_plugin_package_history=self._data.reconcile_plugin_package_history,
            prune_update_packages=prune_update_packages,
            refresh_related_caches=self._cache.refresh_related_caches,
            on_upload_complete=on_upload_complete,
        )

    @staticmethod
    def _read_text_file(path: Path) -> str | None:
        try:
            return path.read_text(encoding="utf-8").strip()
        except (OSError, UnicodeDecodeError):
            return None

    @staticmethod
    def _load_manifest(path: Path) -> dict[str, Any]:
        # Preserve the legacy API publish behavior: malformed existing JSON is
        # an operational failure, while a missing manifest starts empty.
        return json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}

    @staticmethod
    def _version_tuple(version: str) -> tuple[int, ...]:
        return tuple(int(part) for part in version.split(".") if part.isdigit())


@dataclass(frozen=True)
class MarketplaceApiServices:
    catalog: MarketplaceCatalogService
    packages: MarketplacePackageService
    storage: MarketplaceStorageService

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any
from typing import Callable

from db_cache import PLUGIN_INFO_CACHE_TTL_SECONDS
from package_publish import (
    PackageValidationError,
    finalize_plugin_publish,
    persist_plugin_metadata,
    save_package_file,
    validate_api_publish_request,
)
from plugin_marketplace import prewarm_plugin_metadata
from storage_uploads import UploadTooLargeError, UploadWorkflowError, store_legacy_upload
from update_retention import prune_update_packages, repair_update_storage_layout
from flask import abort, jsonify, request, send_from_directory


@dataclass(frozen=True)
class MarketplaceApiRouteContext:
    get_storage: Callable[[], Path]
    max_upload_size_bytes: int
    parse_int_arg: Callable[..., int]
    normalize_catalog_sort_name: Callable[[str], str]
    allowed_catalog_sorts: Any
    allowed_catalog_sort_orders: Any
    build_plugin_search_api_result: Callable[..., Any]
    build_plugin_detail_api_result: Callable[..., Any]
    collect_catalog_categories: Callable[..., Any]
    get_request_plugin_catalog: Callable[[], Any]
    build_plugin_icon_url: Callable[[str], str]
    get_plugin_info: Callable[..., Any]
    get_request_download_counts: Callable[[], Any]
    read_text_file: Callable[[Path], str]
    is_safe_id: Callable[[str], bool]
    is_safe_version: Callable[[str], bool]
    sanitize_filename: Callable[[str], str]
    version_tuple: Callable[..., Any]
    extract_package_version: Callable[..., Any]
    load_manifest: Callable[[Path], Any]
    refresh_related_caches: Callable[..., None]
    get_download_counts: Callable[[], Any]
    get_cache_entry: Callable[..., Any]
    set_cache_entry: Callable[..., None]
    record_download: Callable[[str, str], None]
    normalize_relative_path: Callable[[str], str]
    is_root_release_file: Callable[[str], bool]
    reconcile_app_release_history: Callable[..., Any]
    reconcile_plugin_package_history: Callable[..., Any]
    require_upload_auth: Any


def register_marketplace_api_routes(app, ctx: MarketplaceApiRouteContext) -> None:
    @app.route("/api/plugins", methods=["GET"])
    def api_search_plugins():
        """Search and list plugins. Compatible with IMarketplaceService.SearchPluginsAsync."""
        keyword = request.args.get("Keyword", request.args.get("keyword", "")).strip()
        category = request.args.get("Category", request.args.get("category", "")).strip()
        author = request.args.get("Author", request.args.get("author", "")).strip()
        sort_by = request.args.get("SortBy", request.args.get("sort", "updated"))
        sort_order = request.args.get("SortOrder", request.args.get("sortOrder", "desc")).strip().lower()
        page = ctx.parse_int_arg("Page", "page", default=1, minimum=1)
        page_size = ctx.parse_int_arg("PageSize", "pageSize", default=20, minimum=1, maximum=100)
        normalized_sort = ctx.normalize_catalog_sort_name(sort_by)
        if normalized_sort not in ctx.allowed_catalog_sorts:
            abort(400, description="Invalid SortBy parameter")
        if sort_order not in ctx.allowed_catalog_sort_orders:
            abort(400, description="Invalid SortOrder parameter")

        return jsonify(
            ctx.build_plugin_search_api_result(
                ctx.get_request_plugin_catalog(),
                keyword=keyword,
                category=category,
                author=author,
                sort_by=normalized_sort,
                sort_order=sort_order,
                page=page,
                page_size=page_size,
                icon_url_builder=ctx.build_plugin_icon_url,
            )
        )

    @app.route("/api/plugins/categories", methods=["GET"])
    def api_categories():
        """Get all plugin categories."""
        return jsonify(ctx.collect_catalog_categories(ctx.get_request_plugin_catalog()))

    @app.route("/api/plugins/batch-version-check", methods=["POST"])
    def api_batch_version_check():
        """Batch check latest versions for multiple plugins at once."""
        data = request.get_json(silent=True) or {}
        plugin_ids = data.get("PluginIds", data.get("pluginIds", []))
        if not isinstance(plugin_ids, list):
            abort(400, description="PluginIds must be an array")

        results = []
        for plugin_id in plugin_ids:
            if not isinstance(plugin_id, str):
                results.append({"pluginId": str(plugin_id), "latestVersion": None, "status": "invalid"})
                continue
            normalized_id = plugin_id.strip()
            if not normalized_id or not ctx.is_safe_id(normalized_id):
                results.append({"pluginId": plugin_id, "latestVersion": None, "status": "invalid"})
                continue
            storage = ctx.get_storage()
            latest = ctx.read_text_file(storage / "Plugins" / normalized_id / "LATEST_RELEASE")
            if latest:
                results.append({"pluginId": normalized_id, "latestVersion": latest, "status": "ok"})
            else:
                results.append({"pluginId": normalized_id, "latestVersion": None, "status": "missing"})
        return jsonify(results)

    @app.route("/api/plugins/<plugin_id>", methods=["GET"])
    def api_plugin_detail(plugin_id):
        """Get detailed plugin information."""
        if not ctx.is_safe_id(plugin_id):
            abort(400, description="Invalid plugin_id")
        info = ctx.get_plugin_info(plugin_id, download_counts=ctx.get_request_download_counts())
        if not info:
            return jsonify({"error": "Plugin not found"}), 404

        return jsonify(ctx.build_plugin_detail_api_result(info, icon_url_builder=ctx.build_plugin_icon_url))

    @app.route("/api/plugins/<plugin_id>/latest-version", methods=["GET"])
    def api_latest_version(plugin_id):
        """
        Return latest version as plain text — backward compatible with LATEST_RELEASE.
        This endpoint is used by older clients that check version via a simple GET.
        """
        if not ctx.is_safe_id(plugin_id):
            abort(400, description="Invalid plugin_id")
        storage = ctx.get_storage()
        version = ctx.read_text_file(storage / "Plugins" / plugin_id / "LATEST_RELEASE")
        if not version:
            return "Plugin not found", 404
        return version, 200, {"Content-Type": "text/plain; charset=utf-8"}

    @app.route("/api/packages/<plugin_id>/<version>", methods=["GET"])
    def api_download_package(plugin_id, version):
        """Download a specific plugin version .cvxp file."""
        if not ctx.is_safe_id(plugin_id) or not ctx.is_safe_version(version):
            return jsonify({"error": "Invalid plugin_id or version"}), 400

        storage = ctx.get_storage()
        plugin_dir = storage / "Plugins" / plugin_id
        filename = f"{plugin_id}-{version}.cvxp"
        filepath = plugin_dir / filename

        if not filepath.exists():
            history_path = storage / "History" / "Plugins" / plugin_id / filename
            if history_path.exists():
                ctx.record_download(plugin_id, version)
                return send_from_directory(str(history_path.parent), history_path.name, as_attachment=True)
            return jsonify({"error": "Package not found"}), 404

        ctx.record_download(plugin_id, version)
        return send_from_directory(str(plugin_dir), filename, as_attachment=True)

    @app.route("/api/packages/publish", methods=["POST"])
    @ctx.require_upload_auth
    def api_publish_package():
        """
        Publish a new plugin version.
        Accepts multipart form: plugin metadata + .cvxp package file.
        """
        package = request.files.get("package")
        plugin_id = request.form.get("PluginId", request.form.get("plugin_id", "")).strip()
        version = request.form.get("Version", request.form.get("version", "")).strip()

        name = request.form.get("Name", request.form.get("name", plugin_id)).strip()
        description = request.form.get("Description", request.form.get("description", "")).strip()
        author = request.form.get("Author", request.form.get("author", "")).strip()
        category = request.form.get("Category", request.form.get("category", "")).strip()
        requires_ver = request.form.get(
            "RequiresVersion", request.form.get("requires_version", "")
        ).strip()
        changelog_text = request.form.get("ChangeLog", request.form.get("changelog", "")).strip()
        icon = request.files.get("icon")

        try:
            storage = ctx.get_storage()
            upload_request = validate_api_publish_request(
                package,
                plugin_id,
                version,
                sanitize_filename=ctx.sanitize_filename,
                validate_plugin_id=ctx.is_safe_id,
                validate_version=ctx.is_safe_version,
            )
            save_result = save_package_file(
                storage,
                package,
                upload_request,
                validate_plugin_id=ctx.is_safe_id,
                read_text_file=ctx.read_text_file,
                version_tuple=ctx.version_tuple,
                reconcile_plugin_package_history=ctx.reconcile_plugin_package_history,
            )
            persist_plugin_metadata(
                save_result.plugin_dir,
                plugin_id=upload_request.plugin_id,
                version=upload_request.version,
                name=name or upload_request.plugin_id,
                description=description,
                author=author,
                category=category,
                requires_version=requires_ver,
                changelog_text=changelog_text,
                icon_file=icon,
                manifest_loader=ctx.load_manifest,
            )
            finalize_plugin_publish(
                storage,
                plugin_id=upload_request.plugin_id,
                version=upload_request.version,
                refresh_related_caches=ctx.refresh_related_caches,
                prewarm_plugin_metadata=prewarm_plugin_metadata,
                get_download_counts=ctx.get_download_counts,
                get_cache_entry=ctx.get_cache_entry,
                set_cache_entry=ctx.set_cache_entry,
                ttl_seconds=PLUGIN_INFO_CACHE_TTL_SECONDS,
            )
        except PackageValidationError as exc:
            return jsonify({"error": str(exc)}), 400

        return (
            jsonify({"pluginId": upload_request.plugin_id, "version": upload_request.version}),
            201,
        )

    @app.route("/D%3A/ColorVision/Plugins/<path:filepath>")
    @app.route("/D:/ColorVision/Plugins/<path:filepath>")
    def legacy_plugin_files(filepath):
        """
        Backward-compatible endpoint matching the old file-server URL pattern:
        http://host:9999/D%3A/ColorVision/Plugins/{PluginId}/LATEST_RELEASE
        http://host:9999/D%3A/ColorVision/Plugins/{PluginId}/{PluginId}-{ver}.cvxp
        """
        storage = ctx.get_storage()
        full_path = storage / "Plugins" / filepath
        try:
            full_path.resolve().relative_to((storage / "Plugins").resolve())
        except ValueError:
            abort(403)
        if not full_path.exists():
            abort(404)
        if full_path.is_file():
            return send_from_directory(str(full_path.parent), full_path.name)
        abort(404)

    @app.route("/D%3A/ColorVision/<path:filepath>")
    @app.route("/D:/ColorVision/<path:filepath>")
    def legacy_files(filepath):
        """
        Backward-compatible endpoint for other legacy file-server URLs:
        http://host:9999/D%3A/ColorVision/LATEST_RELEASE
        http://host:9999/D%3A/ColorVision/CHANGELOG.md
        http://host:9999/D%3A/ColorVision/Update/...
        http://host:9999/D%3A/ColorVision/Tool/...
        """
        storage = ctx.get_storage()
        full_path = storage / filepath
        if filepath.replace("\\", "/").startswith("Update/") and not full_path.exists():
            repair_update_storage_layout(storage)
            full_path = storage / filepath
        try:
            full_path.resolve().relative_to(storage.resolve())
        except ValueError:
            abort(403)
        if not full_path.exists():
            abort(404)
        if full_path.is_file():
            return send_from_directory(str(full_path.parent), full_path.name)
        abort(404)

    @app.route("/upload/<path:filepath>", methods=["PUT"])
    @ctx.require_upload_auth
    def legacy_upload(filepath):
        """
        Backward-compatible upload endpoint matching old file_manager.py pattern:
        PUT http://host:9998/upload/ColorVision/Plugins/{PluginId}/{filename}
        """
        try:
            storage = ctx.get_storage()
            store_legacy_upload(
                storage=storage,
                raw_filepath=filepath,
                stream=request.stream,
                max_size=ctx.max_upload_size_bytes,
                normalize_relative_path=ctx.normalize_relative_path,
                validate_plugin_id=ctx.is_safe_id,
                extract_package_version=lambda filename, plugin_id: ctx.extract_package_version(filename, plugin_id),
                is_root_release_file=ctx.is_root_release_file,
                reconcile_app_release_history=ctx.reconcile_app_release_history,
                reconcile_plugin_package_history=ctx.reconcile_plugin_package_history,
                prune_update_packages=prune_update_packages,
                refresh_related_caches=ctx.refresh_related_caches,
            )
        except UploadTooLargeError as exc:
            return exc.message, exc.status_code
        except UploadWorkflowError as exc:
            abort(exc.status_code, description=exc.message)

        return "File uploaded successfully", 201

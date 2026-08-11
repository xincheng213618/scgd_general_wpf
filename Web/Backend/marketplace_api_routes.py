from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Callable

from flask import abort, jsonify, request, send_from_directory

from package_publish import PackageValidationError
from services.marketplace_api import (
    MarketplaceApiServices,
    MarketplaceQueryError,
    PublishPackageCommand,
)
from services.request_context import RequestContext
from storage_uploads import UploadTooLargeError, UploadWorkflowError


@dataclass(frozen=True)
class MarketplaceApiRouteContext:
    """Small HTTP adapter bundle; use-case dependencies live in services."""

    services: MarketplaceApiServices
    require_upload_auth: Any
    request_context_factory: Callable[[], RequestContext]


def _parse_int_arg(*names: str, default: int, minimum=None, maximum=None) -> int:
    raw = None
    for name in names:
        if name in request.args:
            raw = request.args.get(name)
            break
    if raw is None or str(raw).strip() == "":
        value = default
    else:
        try:
            value = int(str(raw).strip())
        except (TypeError, ValueError):
            abort(400, description=f"Invalid integer parameter: {names[0]}")
    if minimum is not None and value < minimum:
        abort(400, description=f"{names[0]} must be >= {minimum}")
    if maximum is not None and value > maximum:
        abort(400, description=f"{names[0]} must be <= {maximum}")
    return value


def register_marketplace_api_routes(app, ctx: MarketplaceApiRouteContext) -> None:
    def _plain_version_response(version: str):
        response = app.response_class(version, 200, {"Content-Type": "text/plain; charset=utf-8"})
        response.headers["Cache-Control"] = "public, max-age=30"
        return response

    @app.route("/api/plugins", methods=["GET"])
    def api_search_plugins():
        """Search and list plugins. Compatible with IMarketplaceService.SearchPluginsAsync."""
        keyword = request.args.get("Keyword", request.args.get("keyword", "")).strip()
        category = request.args.get("Category", request.args.get("category", "")).strip()
        author = request.args.get("Author", request.args.get("author", "")).strip()
        sort_by = request.args.get("SortBy", request.args.get("sort", "updated"))
        sort_order = request.args.get("SortOrder", request.args.get("sortOrder", "desc")).strip().lower()
        page = _parse_int_arg("Page", "page", default=1, minimum=1)
        page_size = _parse_int_arg("PageSize", "pageSize", default=20, minimum=1, maximum=100)
        try:
            result = ctx.services.catalog.search(
                ctx.request_context_factory(),
                keyword=keyword,
                category=category,
                author=author,
                sort_by=sort_by,
                sort_order=sort_order,
                page=page,
                page_size=page_size,
            )
        except MarketplaceQueryError as exc:
            abort(400, description=str(exc))
        return jsonify(result)

    @app.route("/api/plugins/categories", methods=["GET"])
    def api_categories():
        """Get all plugin categories."""
        return jsonify(
            ctx.services.catalog.categories(ctx.request_context_factory())
        )

    @app.route("/api/plugins/batch-version-check", methods=["POST"])
    def api_batch_version_check():
        """Batch check latest versions for multiple plugins at once."""
        data = request.get_json(silent=True) or {}
        plugin_ids = data.get("PluginIds", data.get("pluginIds", []))
        if not isinstance(plugin_ids, list):
            abort(400, description="PluginIds must be an array")

        normalized_safe_ids: list[str] = []
        normalized_by_input: list[tuple[str, str | None]] = []
        seen_ids: set[str] = set()
        for plugin_id in plugin_ids:
            if not isinstance(plugin_id, str):
                normalized_by_input.append((str(plugin_id), None))
                continue
            normalized_id = plugin_id.strip()
            if not normalized_id or not ctx.services.storage.is_safe_id(normalized_id):
                normalized_by_input.append((plugin_id, None))
                continue
            normalized_by_input.append((normalized_id, normalized_id))
            if normalized_id not in seen_ids:
                seen_ids.add(normalized_id)
                normalized_safe_ids.append(normalized_id)

        indexed_versions = ctx.services.catalog.latest_versions(normalized_safe_ids)
        results = []
        for original_id, normalized_id in normalized_by_input:
            if normalized_id is None:
                results.append({"pluginId": original_id, "latestVersion": None, "status": "invalid"})
                continue
            latest = indexed_versions.get(normalized_id)
            if latest:
                results.append({"pluginId": normalized_id, "latestVersion": latest, "status": "ok"})
            else:
                results.append({"pluginId": normalized_id, "latestVersion": None, "status": "missing"})
        response = jsonify(results)
        response.headers["Cache-Control"] = "public, max-age=30"
        return response

    @app.route("/api/plugins/<plugin_id>", methods=["GET"])
    def api_plugin_detail(plugin_id):
        """Get detailed plugin information."""
        if not ctx.services.storage.is_safe_id(plugin_id):
            abort(400, description="Invalid plugin_id")
        info = ctx.services.catalog.detail(
            plugin_id,
            ctx.request_context_factory(),
            view=request.args.get("view", "full").strip().lower(),
        )
        if not info:
            return jsonify({"error": "Plugin not found"}), 404
        return jsonify(info)

    @app.route("/api/plugins/<plugin_id>/latest-version", methods=["GET"])
    def api_latest_version(plugin_id):
        """Return latest version as plain text for legacy clients."""
        if not ctx.services.storage.is_safe_id(plugin_id):
            abort(400, description="Invalid plugin_id")
        indexed = ctx.services.catalog.latest_versions([plugin_id])
        if plugin_id in indexed:
            return _plain_version_response(indexed[plugin_id])
        return "Plugin not found", 404

    @app.route("/api/packages/<plugin_id>/<version>", methods=["GET"])
    def api_download_package(plugin_id, version):
        """Download a specific plugin version .cvxp file."""
        if (
            not ctx.services.storage.is_safe_id(plugin_id)
            or not ctx.services.storage.is_safe_version(version)
        ):
            return jsonify({"error": "Invalid plugin_id or version"}), 400

        package_path = ctx.services.packages.resolve_download(plugin_id, version)
        if package_path is None:
            return jsonify({"error": "Package not found"}), 404
        ctx.services.packages.record_download(
            plugin_id,
            version,
            ctx.request_context_factory(),
        )
        return send_from_directory(
            str(package_path.parent), package_path.name, as_attachment=True
        )

    @app.route("/api/packages/publish", methods=["POST"])
    @ctx.require_upload_auth
    def api_publish_package():
        """Publish a new plugin version from multipart form data."""
        plugin_id = request.form.get("PluginId", request.form.get("plugin_id", "")).strip()
        version = request.form.get("Version", request.form.get("version", "")).strip()
        command = PublishPackageCommand(
            package=request.files.get("package"),
            plugin_id=plugin_id,
            version=version,
            name=request.form.get("Name", request.form.get("name", plugin_id)).strip(),
            description=request.form.get("Description", request.form.get("description", "")).strip(),
            author=request.form.get("Author", request.form.get("author", "")).strip(),
            category=request.form.get("Category", request.form.get("category", "")).strip(),
            requires_version=request.form.get(
                "RequiresVersion", request.form.get("requires_version", "")
            ).strip(),
            changelog=request.form.get("ChangeLog", request.form.get("changelog", "")).strip(),
            icon=request.files.get("icon"),
        )
        try:
            result = ctx.services.packages.publish(
                command,
                ctx.request_context_factory(),
            )
        except PackageValidationError as exc:
            return jsonify({"error": str(exc)}), 400
        return jsonify(result), 201

    @app.route("/D%3A/ColorVision/Plugins/<path:filepath>")
    @app.route("/D:/ColorVision/Plugins/<path:filepath>")
    def legacy_plugin_files(filepath):
        """Serve the historical encoded-drive plugin URL contract."""
        storage = ctx.services.storage.root
        full_path = ctx.services.storage.legacy_plugin_path(filepath)
        if not ctx.services.storage.is_within(full_path, storage / "Plugins"):
            abort(403)
        if not full_path.exists():
            abort(404)
        if full_path.is_file():
            return send_from_directory(str(full_path.parent), full_path.name)
        abort(404)

    @app.route("/D%3A/ColorVision/<path:filepath>")
    @app.route("/D:/ColorVision/<path:filepath>")
    def legacy_files(filepath):
        """Serve other historical encoded-drive file URLs."""
        storage = ctx.services.storage.root
        full_path = ctx.services.storage.legacy_path(filepath)
        if not ctx.services.storage.is_within(full_path, storage):
            abort(403)
        if not full_path.exists():
            abort(404)
        if full_path.is_file():
            return send_from_directory(str(full_path.parent), full_path.name)
        abort(404)

    @app.route("/upload/<path:filepath>", methods=["PUT"])
    @ctx.require_upload_auth
    def legacy_upload(filepath):
        """Store uploads sent by the repository publishing client."""
        try:
            ctx.services.packages.legacy_upload(
                filepath,
                request.stream,
                ctx.request_context_factory(),
            )
        except UploadTooLargeError as exc:
            return exc.message, exc.status_code
        except UploadWorkflowError as exc:
            abort(exc.status_code, description=exc.message)
        return "File uploaded successfully", 201

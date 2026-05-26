"""
ColorVision Plugin Marketplace — Python/Flask Backend

A lightweight backend that serves both:
  - A web management UI (browse / search / download / upload plugins)
  - REST API endpoints for the WPF desktop client

Works directly with the existing H:\\ColorVision file structure:
  H:\\ColorVision\\
  ├── LATEST_RELEASE
  ├── CHANGELOG.md
  ├── Plugins/{PluginId}/LATEST_RELEASE
  ├── Plugins/{PluginId}/{PluginId}-{version}.cvxp
  ├── History/
  ├── Update/
  └── Tool/

Run:
  pip install -r requirements.txt
  python app.py                        # uses config.json
  python app.py --storage /path/to/dir # override storage path
"""

import argparse
import hmac
import json
import re
import sqlite3
from datetime import datetime, timezone
from functools import wraps
from pathlib import Path
from types import SimpleNamespace
from typing import Any

from app_releases import is_root_release_file as is_root_release_file_impl
from catalog_view_models import (
    ALLOWED_CATALOG_SORTS,
    ALLOWED_CATALOG_SORT_ORDERS,
    DEFAULT_HTML_PAGE_SIZE,
    build_plugin_catalog_page_context,
    build_plugin_detail_api_result,
    build_plugin_search_api_result,
    collect_catalog_categories,
    normalize_catalog_sort_name,
)
from config_loader import (
    DEFAULT_CONFIG,
    DEFAULT_SECRET_KEY,
    DEFAULT_UPLOAD_AUTH,
    MAX_FEEDBACK_FIELD_LENGTH,
    MAX_FEEDBACK_FILES,
    MAX_UPLOAD_SIZE_BYTES,
    get_upload_auth as get_upload_auth_impl,
    load_config,
    validate_runtime_config as validate_runtime_config_impl,
)
from db_cache import (
    APP_RELEASES_CACHE_KEY,
    APP_RELEASES_CACHE_TTL_SECONDS,
    CHANGELOG_ANALYSIS_CACHE_KEY,
    CHANGELOG_ANALYSIS_CACHE_TTL_SECONDS,
    DIRECTORY_COUNT_CACHE_TTL_SECONDS,
    HOME_RELEASES_SNAPSHOT_CACHE_KEY,
    HOME_RELEASES_SNAPSHOT_TTL_SECONDS,
    HOME_TOOL_PREVIEW_CACHE_KEY,
    HOME_TOOL_PREVIEW_TTL_SECONDS,
    MARKDOWN_RENDER_CACHE_TTL_SECONDS,
    OVERVIEW_CACHE_KEY,
    OVERVIEW_CACHE_TTL_SECONDS,
    PLUGIN_INFO_CACHE_TTL_SECONDS,
    RELEASE_TIMELINE_CACHE_KEY,
    RELEASE_TIMELINE_CACHE_TTL_SECONDS,
    CacheManager,
)
from download_stats import build_stats_payload
from cvwindowsservice_publish import (
    CVWSError,
    CVWS_PACKAGE_RE as _CVWS_PACKAGE_RE,
    CVWSUploadResult,
    build_cvws_page_context,
    choose_target_filename,
    infer_version_from_filename,
    is_official_filename,
    save_cvws_package,
    update_cvws_latest_release,
    validate_version as validate_cvws_version,
)
from feedback_service import FeedbackValidationError, save_feedback as save_feedback_impl
from markupsafe import Markup
from marketplace_services import MarketplaceCacheSettings, MarketplaceDataService
from marketplace_api_routes import register_marketplace_api_routes
from package_publish import (
    PackageValidationError,
    extract_package_version,
    finalize_plugin_publish,
    load_manifest,
    persist_plugin_metadata,
    save_package_file,
    validate_api_publish_request,
    validate_html_upload_request,
)
from page_contexts import (
    build_browse_page_context,
    build_index_page_context,
    build_releases_page_context,
    build_tools_page_context,
    build_upload_page_context,
    build_updates_page_context,
)
from plugin_marketplace import load_plugin_icon_payload, prewarm_plugin_metadata
from runtime_health import (
    build_health_payload as build_health_payload_impl,
    build_ready_payload as build_ready_payload_impl,
)
from storage_paths import (
    is_safe_id as is_safe_id_impl,
    is_safe_version as is_safe_version_impl,
    normalize_relative_path as normalize_relative_path_impl,
    sanitize_filename as sanitize_filename_impl,
    storage_target as storage_target_impl,
)
from storage_uploads import UploadTooLargeError, UploadWorkflowError, store_legacy_upload
from update_retention import (
    prune_update_packages,
    repair_update_storage_layout,
)

try:
    import markdown
except ImportError:  # pragma: no cover - optional runtime fallback
    markdown = None
from flask import (
    Flask,
    abort,
    jsonify,
    redirect,
    render_template,
    request,
    send_from_directory,
    session,
    url_for,
)
from werkzeug.exceptions import HTTPException

# ---------------------------------------------------------------------------
# Configuration (loaded from config_loader)
# ---------------------------------------------------------------------------

BASE_DIR = Path(__file__).resolve().parent

CONFIG = load_config()
STORAGE = Path(CONFIG["storage_path"])

app = Flask(
    __name__,
    static_folder=str(BASE_DIR / "static"),
    template_folder=str(BASE_DIR / "templates"),
)
app.secret_key = CONFIG["secret_key"]
app.config["MAX_CONTENT_LENGTH"] = MAX_UPLOAD_SIZE_BYTES

# ---------------------------------------------------------------------------
# Database and cache (delegated to db_cache.CacheManager)
# ---------------------------------------------------------------------------
DB_PATH = BASE_DIR / "marketplace.db"
_cache = CacheManager(DB_PATH)
_cache.init_db()

# Thin wrappers that preserve the existing call signatures used throughout app.py.
# These read DB_PATH at call time so tests can mutate it at runtime.
def get_db() -> sqlite3.Connection:
    db = sqlite3.connect(str(DB_PATH))
    db.row_factory = sqlite3.Row
    return db

def init_db():
    _cache._db_path = DB_PATH
    _cache.init_db()

def _set_cache_entry(key: str, value, *, ttl_seconds: int, signature: str = ""):
    _cache.set_cache_entry(key, value, ttl_seconds=ttl_seconds, signature=signature)

def _get_cache_entry(key: str, *, signature: str | None = None) -> dict | None:
    return _cache.get_cache_entry(key, signature=signature)

def _invalidate_cache_prefix(prefix: str):
    _cache.invalidate_cache_prefix(prefix)

def _refresh_related_caches(*, plugin_id: str | None = None, relative_path: str = ""):
    _cache.refresh_related_caches(plugin_id=plugin_id, relative_path=relative_path)

def _is_safe_id(value: str) -> bool:
    """Validate that a plugin ID contains only safe characters."""
    return is_safe_id_impl(value)


def _is_safe_version(value: str) -> bool:
    """Validate that a version string contains only digits and dots."""
    return is_safe_version_impl(value)


def _sanitize_filename(filename: str) -> str:
    """Remove path separators and other dangerous characters from a filename."""
    return sanitize_filename_impl(filename)


def _normalize_relative_path(relative_path: str) -> str:
    return normalize_relative_path_impl(relative_path)


def _get_upload_auth() -> tuple[str, str]:
    return get_upload_auth_impl(CONFIG)


def _is_api_request() -> bool:
    return request.path.startswith("/api/")


def _json_error(message: str, status_code: int, **details):
    payload = {"error": message, "status": status_code}
    if details:
        payload["details"] = details
    response = jsonify(payload)
    response.status_code = status_code
    return response


def _unauthorized_response():
    if _is_api_request():
        response = _json_error("Authentication required", 401)
    else:
        response = app.response_class("Authentication required", status=401)
    response.headers["WWW-Authenticate"] = 'Basic realm="ColorVision Marketplace"'
    return response


def require_upload_auth(view_func):
    @wraps(view_func)
    def wrapper(*args, **kwargs):
        expected_username, expected_password = _get_upload_auth()
        auth = request.authorization
        if (
            not auth
            or (auth.type or "").lower() != "basic"
            or not hmac.compare_digest(auth.username or "", expected_username)
            or not hmac.compare_digest(auth.password or "", expected_password)
        ):
            return _unauthorized_response()
        return view_func(*args, **kwargs)

    return wrapper


def _check_web_session_auth() -> bool:
    """Return True if the current Flask session is authenticated."""
    return bool(session.get("authenticated"))


def require_web_auth(view_func):
    """Decorator that requires web session auth, redirecting to login page on failure."""
    @wraps(view_func)
    def wrapper(*args, **kwargs):
        if not _check_web_session_auth():
            return redirect(url_for("login_page", next=request.url))
        return view_func(*args, **kwargs)

    return wrapper


def _validate_runtime_config(config: dict[str, Any]) -> list[str]:
    return validate_runtime_config_impl(
        config,
        default_secret_key=DEFAULT_SECRET_KEY,
        default_upload_auth=DEFAULT_UPLOAD_AUTH,
    )


def _probe_database() -> tuple[bool, str | None]:
    from runtime_health import probe_database

    return probe_database(get_db)


def _directory_check(path: Path, *, ensure: bool = False) -> dict[str, Any]:
    from runtime_health import directory_check

    return directory_check(path, ensure=ensure)


def _build_health_payload() -> dict[str, Any]:
    return build_health_payload_impl(
        storage=STORAGE,
        db_path=DB_PATH,
        config=CONFIG,
    )


def _build_ready_payload() -> dict[str, Any]:
    return build_ready_payload_impl(
        storage=STORAGE,
        db_path=DB_PATH,
        config=CONFIG,
        get_db=get_db,
        get_upload_auth=_get_upload_auth,
    )


def _parse_int_arg(
    *names: str,
    default: int,
    minimum: int | None = None,
    maximum: int | None = None,
) -> int:
    raw_value = None
    for name in names:
        if name in request.args:
            raw_value = request.args.get(name)
            break

    if raw_value is None or str(raw_value).strip() == "":
        value = default
    else:
        try:
            value = int(str(raw_value).strip())
        except (TypeError, ValueError):
            abort(400, description=f"Invalid integer parameter: {names[0]}")

    if minimum is not None and value < minimum:
        abort(400, description=f"{names[0]} must be >= {minimum}")
    if maximum is not None and value > maximum:
        abort(400, description=f"{names[0]} must be <= {maximum}")
    return value


def _extract_package_version(filename: str, plugin_id: str) -> str | None:
    return extract_package_version(
        filename,
        plugin_id,
        sanitize_filename=_sanitize_filename,
        validate_version=_is_safe_version,
    )


def _load_manifest(manifest_path: Path) -> dict[str, Any]:
    return load_manifest(manifest_path)


def _read_limited_form_value(field_name: str, *, default: str = "") -> str:
    from feedback_service import read_limited_form_value

    try:
        return read_limited_form_value(
            request.form,
            field_name,
            default=default,
            max_length=MAX_FEEDBACK_FIELD_LENGTH,
        )
    except FeedbackValidationError as exc:
        abort(400, description=exc.message)


# ---------------------------------------------------------------------------
# Helpers — read data from the file system
# ---------------------------------------------------------------------------


def read_text_file(path: Path) -> str | None:
    """Read a text file, return None if it doesn't exist."""
    try:
        return path.read_text(encoding="utf-8").strip()
    except (OSError, UnicodeDecodeError):
        return None


def render_markdown(text: str | None) -> Markup:
    if not text:
        return Markup("")
    if markdown is None:
        return Markup(
            f'<pre style="white-space: pre-wrap; margin: 0;">{Markup.escape(text)}</pre>'
        )
    html = markdown.markdown(
        text,
        extensions=["extra", "sane_lists", "nl2br"],
        output_format="html5",
    )
    return Markup(html)


def _render_markdown_cached(*, cache_key: str, signature: str, text: str | None) -> Markup:
    cached = _get_cache_entry(cache_key, signature=signature)
    if cached:
        return Markup(str(cached["value"]))
    rendered = render_markdown(text)
    _set_cache_entry(
        cache_key,
        str(rendered),
        ttl_seconds=MARKDOWN_RENDER_CACHE_TTL_SECONDS,
        signature=signature,
    )
    return rendered


SERVICES = MarketplaceDataService(
    storage_getter=lambda: STORAGE,
    config_getter=lambda: CONFIG,
    get_cache_entry=_get_cache_entry,
    set_cache_entry=_set_cache_entry,
    refresh_related_caches=_refresh_related_caches,
    get_db=get_db,
    read_text_file=read_text_file,
    render_markdown_cached=_render_markdown_cached,
    cache_settings=MarketplaceCacheSettings(
        overview_cache_key=OVERVIEW_CACHE_KEY,
        overview_cache_ttl_seconds=OVERVIEW_CACHE_TTL_SECONDS,
        app_releases_cache_key=APP_RELEASES_CACHE_KEY,
        app_releases_cache_ttl_seconds=APP_RELEASES_CACHE_TTL_SECONDS,
        directory_count_cache_ttl_seconds=DIRECTORY_COUNT_CACHE_TTL_SECONDS,
        plugin_info_cache_ttl_seconds=PLUGIN_INFO_CACHE_TTL_SECONDS,
        changelog_analysis_cache_key=CHANGELOG_ANALYSIS_CACHE_KEY,
        changelog_analysis_cache_ttl_seconds=CHANGELOG_ANALYSIS_CACHE_TTL_SECONDS,
        home_releases_snapshot_cache_key=HOME_RELEASES_SNAPSHOT_CACHE_KEY,
        home_releases_snapshot_ttl_seconds=HOME_RELEASES_SNAPSHOT_TTL_SECONDS,
        home_tool_preview_cache_key=HOME_TOOL_PREVIEW_CACHE_KEY,
        home_tool_preview_ttl_seconds=HOME_TOOL_PREVIEW_TTL_SECONDS,
        release_timeline_cache_key=RELEASE_TIMELINE_CACHE_KEY,
        release_timeline_cache_ttl_seconds=RELEASE_TIMELINE_CACHE_TTL_SECONDS,
    ),
)


def _storage_target(relative_path: str) -> Path:
    return storage_target_impl(STORAGE, relative_path)


def _is_root_release_file(path: Path) -> bool:
    return is_root_release_file_impl(STORAGE, path)


def scan_app_release_artifacts() -> list[dict[str, Any]]:
    return SERVICES.scan_app_release_artifacts()


def get_app_release_context() -> dict[str, Any]:
    return SERVICES.get_app_release_context()


def reconcile_app_release_history(keep_latest: int | None = None) -> list[dict[str, str]]:
    return SERVICES.reconcile_app_release_history(keep_latest)


def resolve_storage_file(relative_path: str) -> Path:
    return SERVICES.resolve_storage_file(relative_path)


def _storage_relative(path: Path) -> str:
    return SERVICES.storage_relative(path)


def _get_download_counts() -> dict[str, int]:
    return SERVICES.get_download_counts()


def _request_cached_value(cache_key: str, loader):
    return SERVICES.request_cached_value(cache_key, loader)


def _get_request_download_counts() -> dict[str, int]:
    return SERVICES.get_request_download_counts()


def _get_request_plugin_catalog() -> list[dict]:
    return SERVICES.get_request_plugin_catalog()


def _get_request_app_info() -> dict:
    return _request_cached_value("app_info", get_app_info)


def _build_release_app_info() -> dict[str, Any]:
    return SERVICES.build_release_app_info()


def _build_home_release_snapshot() -> dict[str, Any]:
    return SERVICES.build_home_release_snapshot()


def _build_home_app_info() -> dict[str, Any]:
    return SERVICES.build_home_app_info()


def _build_changelog_app_info() -> dict[str, Any]:
    return SERVICES.build_changelog_app_info()


def _get_request_home_app_info() -> dict:
    return SERVICES.get_request_home_app_info()


def _get_request_release_app_info() -> dict:
    return SERVICES.get_request_release_app_info()


def _get_request_changelog_app_info() -> dict:
    return SERVICES.get_request_changelog_app_info()


def _build_home_tool_preview() -> dict[str, Any]:
    return SERVICES.build_home_tool_preview()


def _get_request_home_tool_preview() -> dict:
    return SERVICES.get_request_home_tool_preview()


def scan_plugins(download_counts: dict[str, int] | None = None) -> list[dict]:
    """Scan the Plugins directory and return metadata for each plugin."""
    return SERVICES.scan_plugins(download_counts=download_counts)


def get_plugin_info(
    plugin_id: str, download_counts: dict[str, int] | None = None
) -> dict | None:
    return SERVICES.get_plugin_info(plugin_id, download_counts=download_counts)


def reconcile_plugin_package_history(
    plugin_id: str, keep_latest: int | None = None
) -> list[dict[str, str]]:
    return SERVICES.reconcile_plugin_package_history(plugin_id, keep_latest=keep_latest)


def reconcile_all_plugin_package_histories() -> dict[str, list[dict[str, str]]]:
    return SERVICES.reconcile_all_plugin_package_histories()


def _record_download(plugin_id: str, version: str):
    SERVICES.record_download(plugin_id, version)


def _hash_ip(ip: str | None) -> str:
    return SERVICES.hash_ip(ip)


def _build_plugin_icon_url(plugin_id: str) -> str:
    return url_for("plugin_icon", plugin_id=plugin_id, _external=True)


def get_app_info() -> dict:
    """Read application-level info (LATEST_RELEASE, CHANGELOG)."""
    return SERVICES.get_app_info()


def human_size(size_bytes: int) -> str:
    """Format bytes as a human-readable string."""
    for unit in ("B", "KB", "MB", "GB"):
        if abs(size_bytes) < 1024:
            return f"{size_bytes:.1f} {unit}"
        size_bytes /= 1024
    return f"{size_bytes:.1f} TB"


# Make human_size available in templates
app.jinja_env.globals["human_size"] = human_size
app.jinja_env.globals["render_markdown"] = render_markdown


@app.errorhandler(HTTPException)
def handle_http_exception(exc: HTTPException):
    if _is_api_request():
        return _json_error(exc.description or exc.name, exc.code or 500)
    return exc

# ---------------------------------------------------------------------------
# Scan top-level storage directories for the overview page
# ---------------------------------------------------------------------------


def scan_storage_overview() -> list[dict[str, Any]]:
    """Return a summary of top-level directories in storage."""
    return SERVICES.scan_storage_overview()


def build_storage_summary(overview: list[dict[str, Any]]) -> dict:
    return SERVICES.build_storage_summary(overview)


def get_storage_overview_context() -> tuple[list[dict[str, Any]], dict, dict]:
    return SERVICES.get_storage_overview_context()


# ===================================================================
# WEB UI ROUTES (HTML pages)
# ===================================================================


@app.route("/")
def index():
    """Home page — show storage overview."""
    return render_template(
        "index.html",
        **build_index_page_context(
            STORAGE,
            get_app_info=_get_request_home_app_info,
            get_storage_overview_context=get_storage_overview_context,
            get_tool_preview=_get_request_home_tool_preview,
        ),
    )


@app.route("/releases")
def releases_page():
    """Application release archive page."""
    return render_template(
        "releases.html",
        **build_releases_page_context(
            _get_request_release_app_info(),
            major_minor=request.args.get("major_minor", ""),
            branch=request.args.get("branch", ""),
            kind=request.args.get("kind", ""),
            era=request.args.get("era", ""),
        ),
    )


@app.route("/changelog")
def changelog_page():
    """Application changelog page."""
    app_info = _get_request_changelog_app_info()
    return render_template("changelog.html", app_info=app_info)


@app.route("/updates")
def updates_page():
    """Incremental update package page."""
    return render_template("updates.html", **build_updates_page_context(STORAGE))


@app.route("/tools")
def tools_page():
    """Tool/software distribution page."""
    return render_template("tools.html", **build_tools_page_context(STORAGE))


@app.route("/download/<path:relative_path>")
def download_storage_file(relative_path):
    """Download any file inside storage as an attachment."""
    target = resolve_storage_file(relative_path)
    return send_from_directory(str(target.parent), target.name, as_attachment=True)


@app.route("/plugins")
def plugins_page():
    """Plugin marketplace page — browse and search plugins."""
    page = _parse_int_arg("page", default=1, minimum=1)
    page_size = _parse_int_arg("pageSize", default=DEFAULT_HTML_PAGE_SIZE, minimum=1, maximum=60)
    return render_template(
        "plugins.html",
        **build_plugin_catalog_page_context(
            _get_request_plugin_catalog(),
            keyword=request.args.get("q", ""),
            category=request.args.get("category", ""),
            author=request.args.get("author", ""),
            sort_by=request.args.get("sort", "updated"),
            page=page,
            page_size=page_size,
        ),
    )


@app.route("/plugins/<plugin_id>")
def plugin_detail_page(plugin_id):
    """Plugin detail page."""
    if not _is_safe_id(plugin_id):
        abort(404)
    info = get_plugin_info(plugin_id, download_counts=_get_request_download_counts())
    if not info:
        abort(404)
    return render_template("plugin_detail.html", plugin=info)


@app.route("/plugins/<plugin_id>/icon")
def plugin_icon(plugin_id):
    """Serve plugin icon image."""
    if not _is_safe_id(plugin_id):
        abort(404)
    payload, content_type = load_plugin_icon_payload(STORAGE, plugin_id)
    if payload is not None and content_type:
        return app.response_class(payload, mimetype=content_type)
    abort(404)


@app.route("/upload", methods=["GET", "POST"])
@require_web_auth
def upload_page():
    """Upload page — upload a .cvxp plugin package."""
    if request.method == "GET":
        return render_template(
            "upload.html",
            **build_upload_page_context(
                message=None,
                error=None,
                max_upload_size_bytes=MAX_UPLOAD_SIZE_BYTES,
                plugin_package_keep_count=int(CONFIG.get("plugin_package_keep_count", 3) or 3),
            ),
        )

    # Handle file upload
    file = request.files.get("package")
    plugin_id = request.form.get("plugin_id", "")

    try:
        upload_request = validate_html_upload_request(
            file,
            plugin_id,
            sanitize_filename=_sanitize_filename,
            validate_plugin_id=_is_safe_id,
            validate_version=_is_safe_version,
        )
        save_result = save_package_file(
            STORAGE,
            file,
            upload_request,
            validate_plugin_id=_is_safe_id,
            read_text_file=read_text_file,
            version_tuple=_version_tuple,
            reconcile_plugin_package_history=reconcile_plugin_package_history,
        )
        finalize_plugin_publish(
            STORAGE,
            plugin_id=upload_request.plugin_id,
            version=upload_request.version,
            refresh_related_caches=_refresh_related_caches,
            prewarm_plugin_metadata=prewarm_plugin_metadata,
            get_download_counts=_get_download_counts,
            get_cache_entry=_get_cache_entry,
            set_cache_entry=_set_cache_entry,
            ttl_seconds=PLUGIN_INFO_CACHE_TTL_SECONDS,
        )
    except PackageValidationError as exc:
        return render_template(
            "upload.html",
            **build_upload_page_context(
                message=None,
                error=str(exc),
                max_upload_size_bytes=MAX_UPLOAD_SIZE_BYTES,
                plugin_package_keep_count=int(CONFIG.get("plugin_package_keep_count", 3) or 3),
            ),
        )

    return render_template(
        "upload.html",
        **build_upload_page_context(
            message=(
                f"上传成功: {upload_request.safe_filename} → Plugins/{upload_request.plugin_id}/"
                + (
                    f"，并自动归档 {len(save_result.moved_packages)} 个旧版本"
                    if save_result.moved_packages
                    else ""
                )
            ),
            error=None,
            max_upload_size_bytes=MAX_UPLOAD_SIZE_BYTES,
            plugin_package_keep_count=int(CONFIG.get("plugin_package_keep_count", 3) or 3),
        ),
    )


def _version_tuple(v: str) -> tuple:
    return tuple(int(x) for x in v.split(".") if x.isdigit())


@app.route("/browse")
@app.route("/browse/<path:subpath>")
def browse_page(subpath=""):
    """Generic file browser for the storage directory."""
    normalized = _normalize_relative_path(subpath)
    target = _storage_target(normalized)
    if not target.exists():
        abort(404)
    # Security: prevent path traversal
    try:
        target.resolve().relative_to(STORAGE.resolve())
    except ValueError:
        abort(403)

    if target.is_file():
        return send_from_directory(str(target.parent), target.name)

    return render_template("browse.html", **build_browse_page_context(STORAGE, normalized))


# ===================================================================
# REST API ENDPOINTS (for WPF desktop client)
# ===================================================================


@app.route("/api/app/latest-version", methods=["GET"])
def api_app_latest_version():
    """Return the current LATEST_RELEASE version string for build scripts."""
    version = SERVICES._read_text_file(STORAGE / "LATEST_RELEASE") or ""
    return jsonify({"version": version.strip()})


def _find_app_release_installer(version: str) -> dict[str, Any] | None:
    candidates = [
        item for item in scan_app_release_artifacts()
        if str(item.get("version", "")).strip() == version and str(item.get("kind", "")).upper() == "EXE"
    ]
    if not candidates:
        return None

    return max(
        candidates,
        key=lambda item: (item.get("source") == "current", str(item.get("modified", ""))),
    )


@app.route("/api/app/changelog", methods=["GET"])
def api_app_changelog():
    """Return the application CHANGELOG.md content as plain text."""
    changelog = SERVICES._read_text_file(STORAGE / "CHANGELOG.md")
    if not changelog:
        return jsonify({"error": "CHANGELOG.md not found"}), 404

    return app.response_class(changelog, content_type="text/plain; charset=utf-8")


@app.route("/api/app/releases/<version>/download", methods=["GET"])
def api_app_release_download(version):
    """Download the installer (.exe) for a specific ColorVision version."""
    if not _is_safe_version(version):
        return jsonify({"error": "Invalid version format"}), 400

    artifact = _find_app_release_installer(version)
    if artifact is None:
        return jsonify({"error": f"Installer for version {version} not found"}), 404

    target = resolve_storage_file(str(artifact.get("relative_path", "")))
    return send_from_directory(str(target.parent), target.name, as_attachment=True)


@app.route("/api/app/updates/<version>/download", methods=["GET"])
def api_app_incremental_download(version):
    """Download a specific incremental .cvx package for the application."""
    if not _is_safe_version(version):
        return jsonify({"error": "Invalid version format"}), 400

    repair_update_storage_layout(STORAGE)
    filename = f"ColorVision-Update-[{version}].cvx"
    target = STORAGE / "Update" / filename
    if not target.is_file():
        return jsonify({"error": f"Incremental package for version {version} not found"}), 404

    return send_from_directory(str(target.parent), target.name, as_attachment=True)


# ===================================================================
# CVWindowsService Tool API
# ===================================================================

_CVWS_DIR = "Tool/CVWindowsService"
CVWS_RELEASES_CACHE_KEY = "cvws_releases:v1"
CVWS_RELEASES_CACHE_TTL_SECONDS = 180


def _scan_cvwindowsservice_packages() -> list[dict[str, Any]]:
    """Scan Tool/CVWindowsService for release zip packages."""
    tool_dir = STORAGE / "Tool" / "CVWindowsService"
    if not tool_dir.is_dir():
        return []

    packages: list[dict[str, Any]] = []
    for entry in tool_dir.iterdir():
        if not entry.is_file():
            continue
        m = _CVWS_PACKAGE_RE.match(entry.name)
        if not m:
            continue
        version = m.group("version")
        suffix = m.group("suffix") or ""
        try:
            stat = entry.stat()
            size = stat.st_size
            modified_ts = stat.st_mtime
            dt = datetime.fromtimestamp(modified_ts, tz=timezone.utc)
            modified_iso = dt.isoformat()
            modified_display = dt.strftime("%Y-%m-%d %H:%M")
        except OSError:
            size = 0
            modified_iso = ""
            modified_display = ""

        packages.append({
            "fileName": entry.name,
            "version": version,
            "suffix": suffix,
            "size": size,
            "sizeText": human_size(size),
            "modified": modified_iso,
            "modifiedDisplay": modified_display,
            "downloadUrl": f"/download/{_CVWS_DIR}/{entry.name}",
        })

    # Sort by version descending
    packages.sort(key=lambda p: tuple(int(x) for x in p["version"].split(".")), reverse=True)
    return packages


def _cvws_cache_signature(tool_dir: Path, latest_version: str) -> str:
    """Build a light signature for CVWindowsService cache invalidation."""
    try:
        dir_mtime = int(tool_dir.stat().st_mtime)
    except OSError:
        dir_mtime = 0
    return f"{latest_version.strip()}|{dir_mtime}"


def _get_cvwindowsservice_releases_payload() -> dict[str, Any]:
    tool_dir = STORAGE / "Tool" / "CVWindowsService"
    latest = read_text_file(tool_dir / "LATEST_RELEASE") or ""
    signature = _cvws_cache_signature(tool_dir, latest)

    cached = _get_cache_entry(CVWS_RELEASES_CACHE_KEY, signature=signature)
    if cached:
        value = cached.get("value")
        if isinstance(value, dict):
            return value

    packages = _scan_cvwindowsservice_packages()
    payload = {
        "latestVersion": latest.strip(),
        "packages": packages,
        "count": len(packages),
    }
    _set_cache_entry(
        CVWS_RELEASES_CACHE_KEY,
        payload,
        ttl_seconds=CVWS_RELEASES_CACHE_TTL_SECONDS,
        signature=signature,
    )
    return payload


@app.route("/api/tool/cvwindowsservice/latest-version", methods=["GET"])
def api_cvwindowsservice_latest_version():
    """Return the latest CVWindowsService version from Tool/CVWindowsService/LATEST_RELEASE."""
    payload = _get_cvwindowsservice_releases_payload()
    version = str(payload.get("latestVersion", "")).strip()
    if not version:
        return jsonify({"error": "LATEST_RELEASE not found"}), 404
    return jsonify({"version": version})


@app.route("/api/tool/cvwindowsservice/releases", methods=["GET"])
def api_cvwindowsservice_releases():
    """List all CVWindowsService release packages with version and download info."""
    return jsonify(_get_cvwindowsservice_releases_payload())


@app.route("/api/tool/cvwindowsservice/download/<version>", methods=["GET"])
def api_cvwindowsservice_download(version):
    """Download a specific CVWindowsService version zip by version string."""
    if not _is_safe_version(version):
        return jsonify({"error": "Invalid version format"}), 400

    tool_dir = STORAGE / "Tool" / "CVWindowsService"
    if not tool_dir.is_dir():
        return jsonify({"error": "CVWindowsService directory not found"}), 404

    # Find deterministic best match: same version with largest numeric suffix.
    matches: list[tuple[int, Path]] = []
    for entry in tool_dir.iterdir():
        if not entry.is_file():
            continue
        m = _CVWS_PACKAGE_RE.match(entry.name)
        if m and m.group("version") == version:
            suffix_raw = m.group("suffix") or "0"
            try:
                suffix = int(suffix_raw)
            except ValueError:
                suffix = 0
            matches.append((suffix, entry))

    best_match = max(matches, key=lambda item: item[0])[1] if matches else None

    if best_match is None:
        return jsonify({"error": f"Package for version {version} not found"}), 404

    return send_from_directory(str(best_match.parent), best_match.name, as_attachment=True)


# ===================================================================
# Web Session Login / Logout
# ===================================================================

def _safe_next_url(raw: str | None) -> str | None:
    """Validate that a next URL is a safe internal path. Returns None if unsafe."""
    if not raw:
        return None
    # Only allow relative paths starting with / and not //
    if raw.startswith("/") and not raw.startswith("//"):
        return raw
    return None


@app.route("/login", methods=["GET", "POST"])
def login_page():
    """Web login page using Flask session (not Basic Auth)."""
    if _check_web_session_auth():
        next_url = _safe_next_url(request.args.get("next")) or url_for("upload_page")
        return redirect(next_url)

    error = None
    if request.method == "POST":
        username = request.form.get("username", "").strip()
        password = request.form.get("password", "")
        expected_username, expected_password = _get_upload_auth()
        if (
            hmac.compare_digest(username, expected_username)
            and hmac.compare_digest(password, expected_password)
        ):
            session["authenticated"] = True
            session["username"] = username
            next_url = (
                _safe_next_url(request.form.get("next"))
                or _safe_next_url(request.args.get("next"))
                or url_for("upload_page")
            )
            return redirect(next_url)
        error = "用户名或密码错误"

    next_url = _safe_next_url(request.args.get("next")) or ""
    return render_template("login.html", error=error, next_url=next_url)


@app.route("/logout", methods=["GET"])
def logout_page():
    """Clear the web session and redirect to home."""
    session.clear()
    return redirect(url_for("index"))


# ===================================================================
# CVWindowsService Web Upload (session-authenticated)
# ===================================================================

@app.route("/upload/cvwindowsservice", methods=["GET", "POST"])
@require_web_auth
def cvwindowsservice_upload_page():
    """Upload page for CVWindowsService packages (web session auth)."""
    if request.method == "GET":
        return render_template(
            "cvwindowsservice_upload.html",
            **build_cvws_page_context(
                STORAGE,
                scan_packages=_scan_cvwindowsservice_packages,
                read_text_file=read_text_file,
                human_size=human_size,
            ),
        )

    # POST: handle upload
    file = request.files.get("package")
    version = request.form.get("version", "").strip()
    set_latest = request.form.get("set_latest") == "on"

    # Validate file
    if not file or not getattr(file, "filename", ""):
        return render_template(
            "cvwindowsservice_upload.html",
            **build_cvws_page_context(
                STORAGE,
                scan_packages=_scan_cvwindowsservice_packages,
                read_text_file=read_text_file,
                human_size=human_size,
                error="请选择要上传的文件",
            ),
        )

    if not file.filename.lower().endswith(".zip"):
        return render_template(
            "cvwindowsservice_upload.html",
            **build_cvws_page_context(
                STORAGE,
                scan_packages=_scan_cvwindowsservice_packages,
                read_text_file=read_text_file,
                human_size=human_size,
                error="只允许上传 .zip 文件",
            ),
        )

    # Try to infer version from official filename pattern only
    if not version:
        version = infer_version_from_filename(file.filename) or ""

    if not version:
        if is_official_filename(file.filename):
            # Should not happen (official names always have version), but defensive
            error = "无法从文件名解析版本号，请手动输入版本号"
        else:
            error = "文件名不符合 CVWindowsService[版本]-后缀.zip 规则，请手动输入版本号或重命名文件"
        return render_template(
            "cvwindowsservice_upload.html",
            **build_cvws_page_context(
                STORAGE,
                scan_packages=_scan_cvwindowsservice_packages,
                read_text_file=read_text_file,
                human_size=human_size,
                error=error,
            ),
        )

    if not validate_cvws_version(version):
        return render_template(
            "cvwindowsservice_upload.html",
            **build_cvws_page_context(
                STORAGE,
                scan_packages=_scan_cvwindowsservice_packages,
                read_text_file=read_text_file,
                human_size=human_size,
                error=f"版本号格式不正确: {version}，必须为 x.y.z.w 数字格式",
            ),
        )

    # Save — preserve original filename if it matches official pattern
    target_dir = STORAGE / "Tool" / "CVWindowsService"
    try:
        result = save_cvws_package(
            file, target_dir, version,
            original_filename=file.filename if is_official_filename(file.filename) else None,
        )
    except OSError as exc:
        return render_template(
            "cvwindowsservice_upload.html",
            **build_cvws_page_context(
                STORAGE,
                scan_packages=_scan_cvwindowsservice_packages,
                read_text_file=read_text_file,
                human_size=human_size,
                error=f"保存文件失败: {exc}",
            ),
        )

    # Update LATEST_RELEASE only when explicitly requested
    if set_latest:
        update_cvws_latest_release(target_dir, version)

    # Refresh caches
    _cache.invalidate_cache_prefix("cvws_releases:")
    _cache.invalidate_cache_prefix("home_tool_preview:")
    _cache.invalidate_cache_prefix("storage_overview:")
    _cache.invalidate_cache_prefix(f"dir_file_count:Tool/CVWindowsService")

    # Build success message
    latest_now = (read_text_file(target_dir / "LATEST_RELEASE") or "").strip()
    message = (
        f"上传成功: {result.saved_filename} (版本 {result.version})"
        + (f"，已更新 LATEST_RELEASE → {latest_now}" if set_latest else "")
    )

    return render_template(
        "cvwindowsservice_upload.html",
        **build_cvws_page_context(
            STORAGE,
            scan_packages=_scan_cvwindowsservice_packages,
            read_text_file=read_text_file,
            human_size=human_size,
            message=message,
            result=result,
        ),
    )


@app.route("/api/health", methods=["GET"])
def api_health():
    """Lightweight liveness check for deployment and script preflight."""
    return jsonify(_build_health_payload())


@app.route("/api/ready", methods=["GET"])
def api_ready():
    """Upload-critical readiness check for deployment and script preflight."""
    payload = _build_ready_payload()
    return jsonify(payload), (200 if payload["ready"] else 503)


register_marketplace_api_routes(
    app,
    SimpleNamespace(
        get_storage=lambda: STORAGE,
        max_upload_size_bytes=MAX_UPLOAD_SIZE_BYTES,
        parse_int_arg=_parse_int_arg,
        normalize_catalog_sort_name=normalize_catalog_sort_name,
        allowed_catalog_sorts=ALLOWED_CATALOG_SORTS,
        allowed_catalog_sort_orders=ALLOWED_CATALOG_SORT_ORDERS,
        build_plugin_search_api_result=build_plugin_search_api_result,
        build_plugin_detail_api_result=build_plugin_detail_api_result,
        collect_catalog_categories=collect_catalog_categories,
        get_request_plugin_catalog=_get_request_plugin_catalog,
        build_plugin_icon_url=_build_plugin_icon_url,
        get_plugin_info=get_plugin_info,
        get_request_download_counts=_get_request_download_counts,
        read_text_file=read_text_file,
        is_safe_id=_is_safe_id,
        is_safe_version=_is_safe_version,
        sanitize_filename=_sanitize_filename,
        version_tuple=_version_tuple,
        extract_package_version=_extract_package_version,
        load_manifest=_load_manifest,
        refresh_related_caches=_refresh_related_caches,
        get_download_counts=_get_download_counts,
        get_cache_entry=_get_cache_entry,
        set_cache_entry=_set_cache_entry,
        record_download=_record_download,
        normalize_relative_path=_normalize_relative_path,
        is_root_release_file=_is_root_release_file,
        reconcile_app_release_history=reconcile_app_release_history,
        reconcile_plugin_package_history=reconcile_plugin_package_history,
        require_upload_auth=require_upload_auth,
    ),
)


# ===================================================================
# Stats API
# ===================================================================


@app.route("/api/stats", methods=["GET"])
def api_stats():
    """Download statistics overview."""
    return jsonify(build_stats_payload(get_db))


# ===================================================================
# FEEDBACK / LOG UPLOAD API
# ===================================================================


@app.route("/api/feedback", methods=["POST"])
def api_feedback():
    """
    Receive user feedback with optional log files, screenshots, and attachments.
    The feedback is stored in storage/Feedback/{timestamp}_{hash}/.
    """
    try:
        result = save_feedback_impl(
            STORAGE,
            form=request.form,
            files=request.files,
            remote_addr=request.remote_addr,
            max_feedback_files=MAX_FEEDBACK_FILES,
            max_feedback_field_length=MAX_FEEDBACK_FIELD_LENGTH,
            sanitize_filename=_sanitize_filename,
            hash_ip=_hash_ip,
        )
    except FeedbackValidationError as exc:
        return jsonify({"error": exc.message}), 400

    return jsonify({"feedbackId": result.feedback_id, "message": "Feedback received"}), 201


# ===================================================================
# Entry point
# ===================================================================

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="ColorVision Plugin Marketplace")
    parser.add_argument("--storage", help="Override storage path")
    parser.add_argument("--port", type=int, help="Override port")
    parser.add_argument("--debug", action="store_true", help="Enable debug mode")
    parser.add_argument(
        "--reconcile-history",
        action="store_true",
        help="Move old root ColorVision release packages into History and exit",
    )
    parser.add_argument(
        "--reconcile-plugin-history",
        action="store_true",
        help="Move old plugin .cvxp packages into History/Plugins and exit",
    )
    parser.add_argument(
        "--prune-updates",
        action="store_true",
        help="Prune incremental update packages, keeping only the latest and each branch's .1 package",
    )
    args = parser.parse_args()

    if args.storage:
        STORAGE = Path(args.storage)
    if args.port:
        CONFIG["port"] = args.port
    if args.debug:
        CONFIG["debug"] = True

    issues = _validate_runtime_config(CONFIG)
    if issues:
        if CONFIG.get("debug"):
            print("WARNING: Insecure configuration detected (debug mode allows startup):")
            for issue in issues:
                print(f"  - {issue}")
        else:
            print("Refusing to start with insecure production configuration:")
            for issue in issues:
                print(f"  - {issue}")
            raise SystemExit(2)

    if args.reconcile_history:
        moved = reconcile_app_release_history()
        print(f"Reconciled {len(moved)} file(s) into History")
        for item in moved[:20]:
            print(f"  {item['from']} -> {item['to']}")
        raise SystemExit(0)

    if args.reconcile_plugin_history:
        result = reconcile_all_plugin_package_histories()
        moved_count = sum(len(items) for items in result.values())
        print(f"Reconciled {moved_count} plugin package(s) across {len(result)} plugin(s)")
        for plugin_id, items in list(result.items())[:20]:
            print(f"[{plugin_id}] {len(items)} moved")
            for item in items[:5]:
                print(f"  {item['from']} -> {item['to']}")
        raise SystemExit(0)

    if args.prune_updates:
        result = prune_update_packages(STORAGE)
        print(f"Retained {len(result['retained'])} update package(s)")
        print(f"Deleted {len(result['deleted'])} update package(s)")
        for item in result["deleted"][:20]:
            print(f"  removed {item}")
        raise SystemExit(0)

    print(f"Storage path: {STORAGE}")
    print(f"Listening on: http://{CONFIG['host']}:{CONFIG['port']}")
    print(f"Plugins dir:  {STORAGE / 'Plugins'}")

    # Note: debug=True should only be used during development (--debug flag).
    # In production, use a WSGI server like gunicorn instead of app.run().
    if CONFIG["debug"]:
        print("WARNING: Running in debug mode. Do not use in production.")
        app.run(host=CONFIG["host"], port=CONFIG["port"], debug=True)
    else:
        app.run(host=CONFIG["host"], port=CONFIG["port"])

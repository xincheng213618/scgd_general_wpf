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
import sqlite3
from datetime import datetime, timezone
from functools import wraps
from pathlib import Path
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
from download_stats import build_stats_payload
from feedback_service import FeedbackValidationError, save_feedback as save_feedback_impl
from markupsafe import Markup
from marketplace_services import MarketplaceCacheSettings, MarketplaceDataService
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
from plugin_marketplace import prewarm_plugin_metadata
from runtime_health import (
    build_health_payload as build_health_payload_impl,
    build_ready_payload as build_ready_payload_impl,
    validate_runtime_config as validate_runtime_config_impl,
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
    render_template,
    request,
    send_from_directory,
    url_for,
)
from werkzeug.exceptions import HTTPException

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

BASE_DIR = Path(__file__).resolve().parent
DEFAULT_CONFIG = {
    "storage_path": str(BASE_DIR / "storage"),
    "host": "0.0.0.0",
    "port": 9998,
    "debug": False,
    "secret_key": "change-this-in-production",
    "app_release_keep_count": 5,
    "plugin_package_keep_count": 3,
    "upload_auth": {"username": "admin", "password": "admin"},
}
DEFAULT_SECRET_KEY = DEFAULT_CONFIG["secret_key"]
DEFAULT_UPLOAD_AUTH = dict(DEFAULT_CONFIG["upload_auth"])
MAX_UPLOAD_SIZE_BYTES = 500 * 1024 * 1024
MAX_FEEDBACK_FILES = 10
MAX_FEEDBACK_FIELD_LENGTH = 4000


def load_config():
    config = dict(DEFAULT_CONFIG)
    config["upload_auth"] = dict(DEFAULT_UPLOAD_AUTH)
    config_file = BASE_DIR / "config.json"
    if config_file.exists():
        with open(config_file, encoding="utf-8") as f:
            loaded = json.load(f)
        if isinstance(loaded.get("upload_auth"), dict):
            config["upload_auth"].update(loaded["upload_auth"])
        for key, value in loaded.items():
            if key == "upload_auth":
                continue
            config[key] = value
    return config


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
# Database helpers (SQLite for download statistics and cache)
# ---------------------------------------------------------------------------
DB_PATH = BASE_DIR / "marketplace.db"
OVERVIEW_CACHE_KEY = "storage_overview:v2"
APP_RELEASES_CACHE_KEY = "app_releases:v1"
OVERVIEW_CACHE_TTL_SECONDS = 300
APP_RELEASES_CACHE_TTL_SECONDS = 300
DIRECTORY_COUNT_CACHE_TTL_SECONDS = 300
PLUGIN_INFO_CACHE_TTL_SECONDS = 300
CHANGELOG_ANALYSIS_CACHE_KEY = "app_changelog:v2"
CHANGELOG_ANALYSIS_CACHE_TTL_SECONDS = 3600
MARKDOWN_RENDER_CACHE_TTL_SECONDS = 3600
HOME_RELEASES_SNAPSHOT_CACHE_KEY = "home_release_snapshot:v1"
HOME_RELEASES_SNAPSHOT_TTL_SECONDS = 300
HOME_TOOL_PREVIEW_CACHE_KEY = "home_tool_preview:v1"
HOME_TOOL_PREVIEW_CACHE_TTL_SECONDS = 300
RELEASE_TIMELINE_CACHE_KEY = "release_timeline:v1"
RELEASE_TIMELINE_CACHE_TTL_SECONDS = 3600


def get_db():
    db = sqlite3.connect(str(DB_PATH))
    db.row_factory = sqlite3.Row
    return db


def init_db():
    db = get_db()
    db.executescript(
        """
        CREATE TABLE IF NOT EXISTS download_log (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            plugin_id   TEXT    NOT NULL,
            version     TEXT    NOT NULL,
            client_ip   TEXT,
            client_ver  TEXT,
            downloaded_at TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE INDEX IF NOT EXISTS idx_dl_plugin ON download_log(plugin_id);
        CREATE INDEX IF NOT EXISTS idx_dl_time   ON download_log(downloaded_at);
        CREATE TABLE IF NOT EXISTS cache_entry (
            key         TEXT PRIMARY KEY,
            value       TEXT NOT NULL,
            signature   TEXT NOT NULL DEFAULT '',
            expires_at  INTEGER NOT NULL,
            updated_at  TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE INDEX IF NOT EXISTS idx_cache_expiry ON cache_entry(expires_at);
    """
    )
    db.commit()
    db.close()


init_db()


def _now_ts() -> int:
    return int(datetime.now(timezone.utc).timestamp())


def _set_cache_entry(key: str, value, *, ttl_seconds: int, signature: str = ""):
    try:
        payload = json.dumps(value, ensure_ascii=False, separators=(",", ":"))
        db = get_db()
        db.execute(
            """
            INSERT INTO cache_entry (key, value, signature, expires_at, updated_at)
            VALUES (?, ?, ?, ?, datetime('now'))
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                signature = excluded.signature,
                expires_at = excluded.expires_at,
                updated_at = datetime('now')
            """,
            (key, payload, signature, _now_ts() + ttl_seconds),
        )
        db.commit()
        db.close()
    except Exception:
        pass


def _get_cache_entry(key: str, *, signature: str | None = None) -> dict | None:
    try:
        db = get_db()
        row = db.execute(
            "SELECT value, signature, expires_at, updated_at FROM cache_entry WHERE key = ?",
            (key,),
        ).fetchone()
        db.close()
    except Exception:
        return None

    if not row or row["expires_at"] <= _now_ts():
        return None
    if signature is not None and row["signature"] != signature:
        return None

    try:
        value = json.loads(row["value"])
    except (TypeError, json.JSONDecodeError):
        return None

    return {
        "value": value,
        "updated_at": row["updated_at"],
        "expires_at": row["expires_at"],
        "signature": row["signature"],
    }


def _invalidate_cache_prefix(prefix: str):
    try:
        db = get_db()
        db.execute("DELETE FROM cache_entry WHERE key LIKE ?", (f"{prefix}%",))
        db.commit()
        db.close()
    except Exception:
        pass


def _refresh_related_caches(*, plugin_id: str | None = None, relative_path: str = ""):
    _invalidate_cache_prefix("storage_overview:")
    _invalidate_cache_prefix("app_releases:")
    _invalidate_cache_prefix("home_release_snapshot:")
    _invalidate_cache_prefix("home_tool_preview:")
    _invalidate_cache_prefix("release_timeline:")

    top_level = Path(relative_path).parts[0] if relative_path else ""
    if top_level:
        _invalidate_cache_prefix(f"dir_file_count:{top_level}")

    if plugin_id:
        _invalidate_cache_prefix("plugin_summary:")
        _invalidate_cache_prefix("plugin_detail:")
        _invalidate_cache_prefix(f"dir_file_count:Plugins/{plugin_id}")

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
    auth_config = CONFIG.get("upload_auth") or {}
    if not isinstance(auth_config, dict):
        auth_config = {}
    username = str(auth_config.get("username", "")).strip()
    password = str(auth_config.get("password", ""))
    return username, password


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
        home_tool_preview_ttl_seconds=HOME_TOOL_PREVIEW_CACHE_TTL_SECONDS,
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
    plugin_dir = STORAGE / "Plugins" / plugin_id
    icon_path = plugin_dir / "PackageIcon.png"
    if icon_path.exists():
        return send_from_directory(str(plugin_dir), "PackageIcon.png")
    abort(404)


@app.route("/upload", methods=["GET", "POST"])
@require_upload_auth
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


@app.route("/api/health", methods=["GET"])
def api_health():
    """Lightweight liveness check for deployment and script preflight."""
    return jsonify(_build_health_payload())


@app.route("/api/ready", methods=["GET"])
def api_ready():
    """Upload-critical readiness check for deployment and script preflight."""
    payload = _build_ready_payload()
    return jsonify(payload), (200 if payload["ready"] else 503)


@app.route("/api/plugins", methods=["GET"])
def api_search_plugins():
    """Search and list plugins. Compatible with IMarketplaceService.SearchPluginsAsync."""
    keyword = request.args.get("Keyword", request.args.get("keyword", "")).strip()
    category = request.args.get("Category", request.args.get("category", "")).strip()
    sort_by = request.args.get("SortBy", request.args.get("sort", "updated"))
    sort_order = request.args.get("SortOrder", request.args.get("sortOrder", "desc")).strip().lower()
    page = _parse_int_arg("Page", "page", default=1, minimum=1)
    page_size = _parse_int_arg("PageSize", "pageSize", default=20, minimum=1, maximum=100)
    normalized_sort = normalize_catalog_sort_name(sort_by)
    if normalized_sort not in ALLOWED_CATALOG_SORTS:
        abort(400, description="Invalid SortBy parameter")
    if sort_order not in ALLOWED_CATALOG_SORT_ORDERS:
        abort(400, description="Invalid SortOrder parameter")

    return jsonify(
        build_plugin_search_api_result(
            _get_request_plugin_catalog(),
            keyword=keyword,
            category=category,
            sort_by=normalized_sort,
            sort_order=sort_order,
            page=page,
            page_size=page_size,
            icon_url_builder=_build_plugin_icon_url,
        )
    )

@app.route("/api/plugins/categories", methods=["GET"])
def api_categories():
    """Get all plugin categories."""
    return jsonify(collect_catalog_categories(_get_request_plugin_catalog()))


@app.route("/api/plugins/batch-version-check", methods=["POST"])
def api_batch_version_check():
    """Batch check latest versions for multiple plugins at once."""
    data = request.get_json(silent=True) or {}
    plugin_ids = data.get("PluginIds", data.get("pluginIds", []))
    if not isinstance(plugin_ids, list):
        abort(400, description="PluginIds must be an array")

    results = []
    for pid in plugin_ids:
        if not isinstance(pid, str) or not _is_safe_id(pid):
            continue
        latest = read_text_file(STORAGE / "Plugins" / pid / "LATEST_RELEASE")
        if latest:
            results.append({"pluginId": pid, "latestVersion": latest})
    return jsonify(results)


@app.route("/api/plugins/<plugin_id>", methods=["GET"])
def api_plugin_detail(plugin_id):
    """Get detailed plugin information."""
    if not _is_safe_id(plugin_id):
        abort(400, description="Invalid plugin_id")
    info = get_plugin_info(plugin_id, download_counts=_get_request_download_counts())
    if not info:
        return jsonify({"error": "Plugin not found"}), 404

    return jsonify(build_plugin_detail_api_result(info, icon_url_builder=_build_plugin_icon_url))


@app.route("/api/plugins/<plugin_id>/latest-version", methods=["GET"])
def api_latest_version(plugin_id):
    """
    Return latest version as plain text — backward compatible with LATEST_RELEASE.
    This endpoint is used by older clients that check version via a simple GET.
    """
    if not _is_safe_id(plugin_id):
        abort(400, description="Invalid plugin_id")
    version = read_text_file(STORAGE / "Plugins" / plugin_id / "LATEST_RELEASE")
    if not version:
        return "Plugin not found", 404
    return version, 200, {"Content-Type": "text/plain; charset=utf-8"}


# ===================================================================
# PACKAGE DOWNLOAD & UPLOAD API
# ===================================================================


@app.route("/api/packages/<plugin_id>/<version>", methods=["GET"])
def api_download_package(plugin_id, version):
    """Download a specific plugin version .cvxp file."""
    if not _is_safe_id(plugin_id) or not _is_safe_version(version):
        return jsonify({"error": "Invalid plugin_id or version"}), 400

    plugin_dir = STORAGE / "Plugins" / plugin_id
    filename = f"{plugin_id}-{version}.cvxp"
    filepath = plugin_dir / filename

    if not filepath.exists():
        history_path = STORAGE / "History" / "Plugins" / plugin_id / filename
        if history_path.exists():
            _record_download(plugin_id, version)
            return send_from_directory(str(history_path.parent), history_path.name, as_attachment=True)
        return jsonify({"error": "Package not found"}), 404

    _record_download(plugin_id, version)
    return send_from_directory(str(plugin_dir), filename, as_attachment=True)


@app.route("/api/packages/publish", methods=["POST"])
@require_upload_auth
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
        upload_request = validate_api_publish_request(
            package,
            plugin_id,
            version,
            sanitize_filename=_sanitize_filename,
            validate_plugin_id=_is_safe_id,
            validate_version=_is_safe_version,
        )
        save_result = save_package_file(
            STORAGE,
            package,
            upload_request,
            validate_plugin_id=_is_safe_id,
            read_text_file=read_text_file,
            version_tuple=_version_tuple,
            reconcile_plugin_package_history=reconcile_plugin_package_history,
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
            manifest_loader=load_manifest,
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
        return jsonify({"error": str(exc)}), 400

    return (
        jsonify({"pluginId": upload_request.plugin_id, "version": upload_request.version}),
        201,
    )


# ===================================================================
# BACKWARD COMPATIBILITY — serve files the old way
# ===================================================================


@app.route("/D%3A/ColorVision/Plugins/<path:filepath>")
@app.route("/D:/ColorVision/Plugins/<path:filepath>")
def legacy_plugin_files(filepath):
    """
    Backward-compatible endpoint matching the old file-server URL pattern:
    http://host:9999/D%3A/ColorVision/Plugins/{PluginId}/LATEST_RELEASE
    http://host:9999/D%3A/ColorVision/Plugins/{PluginId}/{PluginId}-{ver}.cvxp
    """
    full_path = STORAGE / "Plugins" / filepath
    try:
        full_path.resolve().relative_to((STORAGE / "Plugins").resolve())
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
    full_path = STORAGE / filepath
    if filepath.replace("\\", "/").startswith("Update/") and not full_path.exists():
        repair_update_storage_layout(STORAGE)
        full_path = STORAGE / filepath
    try:
        full_path.resolve().relative_to(STORAGE.resolve())
    except ValueError:
        abort(403)
    if not full_path.exists():
        abort(404)
    if full_path.is_file():
        return send_from_directory(str(full_path.parent), full_path.name)
    abort(404)


# ===================================================================
# UPLOAD API — PUT-based (backward compatible with build scripts)
# ===================================================================


@app.route("/upload/<path:filepath>", methods=["PUT"])
@require_upload_auth
def legacy_upload(filepath):
    """
    Backward-compatible upload endpoint matching old file_manager.py pattern:
    PUT http://host:9998/upload/ColorVision/Plugins/{PluginId}/{filename}
    """
    try:
        store_legacy_upload(
            storage=STORAGE,
            raw_filepath=filepath,
            stream=request.stream,
            max_size=MAX_UPLOAD_SIZE_BYTES,
            normalize_relative_path=_normalize_relative_path,
            validate_plugin_id=_is_safe_id,
            extract_package_version=lambda filename, plugin_id: extract_package_version(
                filename,
                plugin_id,
                sanitize_filename=_sanitize_filename,
                validate_version=_is_safe_version,
            ),
            is_root_release_file=_is_root_release_file,
            reconcile_app_release_history=reconcile_app_release_history,
            reconcile_plugin_package_history=reconcile_plugin_package_history,
            prune_update_packages=prune_update_packages,
            refresh_related_caches=_refresh_related_caches,
        )
    except UploadTooLargeError as exc:
        return exc.message, exc.status_code
    except UploadWorkflowError as exc:
        abort(exc.status_code, description=exc.message)

    return "File uploaded successfully", 201


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

    if not CONFIG.get("debug"):
        issues = _validate_runtime_config(CONFIG)
        if issues:
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

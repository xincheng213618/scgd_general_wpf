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
import hashlib
import json
import os
import re
import sqlite3
import time
from datetime import datetime, timezone
from pathlib import Path

from flask import (
    Flask,
    abort,
    jsonify,
    redirect,
    render_template,
    request,
    send_from_directory,
    url_for,
)

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

BASE_DIR = Path(__file__).resolve().parent
DEFAULT_CONFIG = {
    "storage_path": str(BASE_DIR / "storage"),
    "host": "0.0.0.0",
    "port": 9999,
    "debug": False,
    "secret_key": "change-this-in-production",
    "upload_auth": {"username": "admin", "password": "admin"},
}


def load_config():
    config = dict(DEFAULT_CONFIG)
    config_file = BASE_DIR / "config.json"
    if config_file.exists():
        with open(config_file, encoding="utf-8") as f:
            config.update(json.load(f))
    return config


CONFIG = load_config()
STORAGE = Path(CONFIG["storage_path"])

app = Flask(__name__)
app.secret_key = CONFIG["secret_key"]
app.config["MAX_CONTENT_LENGTH"] = 500 * 1024 * 1024  # 500 MB upload limit

# ---------------------------------------------------------------------------
# Database helpers (SQLite for download statistics)
# ---------------------------------------------------------------------------
DB_PATH = BASE_DIR / "marketplace.db"


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
    """
    )
    db.commit()
    db.close()


init_db()

# ---------------------------------------------------------------------------
# Security helpers
# ---------------------------------------------------------------------------

_SAFE_ID_RE = re.compile(r"^[A-Za-z0-9_\-]+$")
_SAFE_VERSION_RE = re.compile(r"^[0-9]+(\.[0-9]+)*$")


def _is_safe_id(value: str) -> bool:
    """Validate that a plugin ID contains only safe characters."""
    return bool(value) and _SAFE_ID_RE.match(value) is not None


def _is_safe_version(value: str) -> bool:
    """Validate that a version string contains only digits and dots."""
    return bool(value) and _SAFE_VERSION_RE.match(value) is not None


def _sanitize_filename(filename: str) -> str:
    """Remove path separators and other dangerous characters from a filename."""
    # Use only the basename, strip any directory components
    name = Path(filename).name
    # Remove any remaining path separator characters
    name = re.sub(r'[/\\:*?"<>|]', "_", name)
    return name


# ---------------------------------------------------------------------------
# Helpers — read data from the file system
# ---------------------------------------------------------------------------


def read_text_file(path: Path) -> str | None:
    """Read a text file, return None if it doesn't exist."""
    try:
        return path.read_text(encoding="utf-8").strip()
    except (OSError, UnicodeDecodeError):
        return None


def scan_plugins() -> list[dict]:
    """Scan the Plugins directory and return metadata for each plugin."""
    plugins_dir = STORAGE / "Plugins"
    if not plugins_dir.is_dir():
        return []

    plugins = []
    for entry in sorted(plugins_dir.iterdir()):
        if not entry.is_dir():
            continue
        info = get_plugin_info(entry.name)
        if info:
            plugins.append(info)
    return plugins


def get_plugin_info(plugin_id: str) -> dict | None:
    """Read metadata for a single plugin from its directory."""
    plugin_dir = STORAGE / "Plugins" / plugin_id
    if not plugin_dir.is_dir():
        return None

    latest_version = read_text_file(plugin_dir / "LATEST_RELEASE")

    # Read manifest.json if present
    manifest = {}
    manifest_path = plugin_dir / "manifest.json"
    if manifest_path.exists():
        try:
            with open(manifest_path, encoding="utf-8") as f:
                manifest = json.load(f)
        except (json.JSONDecodeError, OSError):
            pass

    # Collect available package files
    packages = []
    for f in plugin_dir.iterdir():
        if f.suffix == ".cvxp":
            # Extract version from filename: PluginName-1.2.3.4.cvxp
            m = re.match(rf"^{re.escape(plugin_id)}-(.+)\.cvxp$", f.name)
            ver = m.group(1) if m else f.stem
            packages.append(
                {
                    "version": ver,
                    "filename": f.name,
                    "size": f.stat().st_size,
                    "modified": datetime.fromtimestamp(
                        f.stat().st_mtime, tz=timezone.utc
                    ).isoformat(),
                }
            )
    packages.sort(key=lambda p: p["modified"], reverse=True)

    # Read optional files
    readme = read_text_file(plugin_dir / "README.md")
    changelog = read_text_file(plugin_dir / "CHANGELOG.md")

    has_icon = (plugin_dir / "PackageIcon.png").exists()

    # Download count from DB
    total_downloads = _get_download_count(plugin_id)

    return {
        "id": manifest.get("id", plugin_id),
        "name": manifest.get("name", plugin_id),
        "description": manifest.get("description", ""),
        "author": manifest.get("author", ""),
        "url": manifest.get("url", ""),
        "version": latest_version or (packages[0]["version"] if packages else ""),
        "requires": manifest.get("requires", ""),
        "category": manifest.get("category", ""),
        "has_icon": has_icon,
        "readme": readme or "",
        "changelog": changelog or "",
        "packages": packages,
        "total_downloads": total_downloads,
        "modified": (
            packages[0]["modified"]
            if packages
            else datetime.fromtimestamp(
                plugin_dir.stat().st_mtime, tz=timezone.utc
            ).isoformat()
        ),
    }


def _get_download_count(plugin_id: str) -> int:
    try:
        db = get_db()
        row = db.execute(
            "SELECT COUNT(*) AS cnt FROM download_log WHERE plugin_id = ?",
            (plugin_id,),
        ).fetchone()
        db.close()
        return row["cnt"] if row else 0
    except Exception:
        return 0


def _record_download(plugin_id: str, version: str):
    try:
        db = get_db()
        db.execute(
            "INSERT INTO download_log (plugin_id, version, client_ip, client_ver) VALUES (?, ?, ?, ?)",
            (
                plugin_id,
                version,
                _hash_ip(request.remote_addr),
                request.headers.get("X-Client-Version", ""),
            ),
        )
        db.commit()
        db.close()
    except Exception:
        pass


def _hash_ip(ip: str | None) -> str:
    if not ip:
        return ""
    return hashlib.sha256(ip.encode()).hexdigest()[:16]


def get_app_info() -> dict:
    """Read application-level info (LATEST_RELEASE, CHANGELOG)."""
    return {
        "latest_version": read_text_file(STORAGE / "LATEST_RELEASE") or "",
        "changelog": read_text_file(STORAGE / "CHANGELOG.md") or "",
    }


def human_size(size_bytes: int) -> str:
    """Format bytes as a human-readable string."""
    for unit in ("B", "KB", "MB", "GB"):
        if abs(size_bytes) < 1024:
            return f"{size_bytes:.1f} {unit}"
        size_bytes /= 1024
    return f"{size_bytes:.1f} TB"


# Make human_size available in templates
app.jinja_env.globals["human_size"] = human_size

# ---------------------------------------------------------------------------
# Scan top-level storage directories for the overview page
# ---------------------------------------------------------------------------


def scan_storage_overview() -> list[dict]:
    """Return a summary of top-level directories in storage."""
    if not STORAGE.is_dir():
        return []
    items = []
    for entry in sorted(STORAGE.iterdir()):
        if entry.name.startswith("."):
            continue
        if entry.is_dir():
            file_count = sum(1 for _ in entry.rglob("*") if _.is_file())
            items.append(
                {
                    "name": entry.name,
                    "type": "dir",
                    "file_count": file_count,
                    "modified": datetime.fromtimestamp(
                        entry.stat().st_mtime, tz=timezone.utc
                    ).isoformat(),
                }
            )
        elif entry.is_file():
            items.append(
                {
                    "name": entry.name,
                    "type": "file",
                    "size": entry.stat().st_size,
                    "modified": datetime.fromtimestamp(
                        entry.stat().st_mtime, tz=timezone.utc
                    ).isoformat(),
                }
            )
    return items


# ===================================================================
# WEB UI ROUTES (HTML pages)
# ===================================================================


@app.route("/")
def index():
    """Home page — show storage overview."""
    app_info = get_app_info()
    overview = scan_storage_overview()
    return render_template("index.html", app_info=app_info, overview=overview)


@app.route("/plugins")
def plugins_page():
    """Plugin marketplace page — browse and search plugins."""
    keyword = request.args.get("q", "").strip()
    category = request.args.get("category", "").strip()
    sort_by = request.args.get("sort", "modified")

    plugins = scan_plugins()

    # Filter
    if keyword:
        kw = keyword.lower()
        plugins = [
            p
            for p in plugins
            if kw in p["name"].lower()
            or kw in p["id"].lower()
            or kw in p["description"].lower()
        ]
    if category:
        plugins = [p for p in plugins if p["category"].lower() == category.lower()]

    # Sort
    if sort_by == "name":
        plugins.sort(key=lambda p: p["name"].lower())
    elif sort_by == "downloads":
        plugins.sort(key=lambda p: p["total_downloads"], reverse=True)
    else:
        plugins.sort(key=lambda p: p["modified"], reverse=True)

    # Collect categories
    all_categories = sorted(
        {p["category"] for p in scan_plugins() if p["category"]}
    )

    return render_template(
        "plugins.html",
        plugins=plugins,
        keyword=keyword,
        category=category,
        sort_by=sort_by,
        categories=all_categories,
    )


@app.route("/plugins/<plugin_id>")
def plugin_detail_page(plugin_id):
    """Plugin detail page."""
    info = get_plugin_info(plugin_id)
    if not info:
        abort(404)
    return render_template("plugin_detail.html", plugin=info)


@app.route("/plugins/<plugin_id>/icon")
def plugin_icon(plugin_id):
    """Serve plugin icon image."""
    plugin_dir = STORAGE / "Plugins" / plugin_id
    icon_path = plugin_dir / "PackageIcon.png"
    if icon_path.exists():
        return send_from_directory(str(plugin_dir), "PackageIcon.png")
    abort(404)


@app.route("/upload", methods=["GET", "POST"])
def upload_page():
    """Upload page — upload a .cvxp plugin package."""
    if request.method == "GET":
        return render_template("upload.html", message=None, error=None)

    # Handle file upload
    file = request.files.get("package")
    plugin_id = request.form.get("plugin_id", "").strip()

    if not file or not file.filename:
        return render_template("upload.html", message=None, error="请选择要上传的文件")

    if not plugin_id:
        # Try to infer from filename
        m = re.match(r"^(.+?)-[\d.]+\.cvxp$", file.filename)
        if m:
            plugin_id = m.group(1)
        else:
            return render_template(
                "upload.html", message=None, error="请填写插件 ID 或使用标准文件名格式"
            )

    if not _is_safe_id(plugin_id):
        return render_template(
            "upload.html", message=None, error="插件 ID 只能包含字母、数字、下划线和连字符"
        )

    safe_filename = _sanitize_filename(file.filename)
    if not safe_filename:
        return render_template("upload.html", message=None, error="无效的文件名")

    # Ensure plugin directory exists
    plugin_dir = STORAGE / "Plugins" / plugin_id
    plugin_dir.mkdir(parents=True, exist_ok=True)

    # Save file
    save_path = plugin_dir / safe_filename
    file.save(str(save_path))

    # Update LATEST_RELEASE if we can extract version
    m = re.match(rf"^{re.escape(plugin_id)}-(.+)\.cvxp$", safe_filename)
    if m:
        version = m.group(1)
        latest_path = plugin_dir / "LATEST_RELEASE"
        existing = read_text_file(latest_path) or "0.0.0.0"
        try:
            if _version_tuple(version) >= _version_tuple(existing):
                latest_path.write_text(version, encoding="utf-8")
        except ValueError:
            latest_path.write_text(version, encoding="utf-8")

    return render_template(
        "upload.html",
        message=f"上传成功: {safe_filename} → Plugins/{plugin_id}/",
        error=None,
    )


def _version_tuple(v: str) -> tuple:
    return tuple(int(x) for x in v.split(".") if x.isdigit())


@app.route("/browse")
@app.route("/browse/<path:subpath>")
def browse_page(subpath=""):
    """Generic file browser for the storage directory."""
    target = STORAGE / subpath
    if not target.exists():
        abort(404)
    # Security: prevent path traversal
    try:
        target.resolve().relative_to(STORAGE.resolve())
    except ValueError:
        abort(403)

    if target.is_file():
        return send_from_directory(str(target.parent), target.name)

    # List directory contents
    items = []
    for entry in sorted(target.iterdir(), key=lambda e: (e.is_file(), e.name.lower())):
        if entry.name.startswith("."):
            continue
        info = {
            "name": entry.name,
            "is_dir": entry.is_dir(),
            "path": f"{subpath}/{entry.name}" if subpath else entry.name,
        }
        if entry.is_file():
            info["size"] = entry.stat().st_size
        info["modified"] = datetime.fromtimestamp(
            entry.stat().st_mtime, tz=timezone.utc
        ).strftime("%Y-%m-%d %H:%M")
        items.append(info)

    # Build breadcrumbs
    parts = [p for p in subpath.split("/") if p]
    breadcrumbs = [("Home", "/browse")]
    for i, part in enumerate(parts):
        breadcrumbs.append((part, "/browse/" + "/".join(parts[: i + 1])))

    return render_template(
        "browse.html", items=items, subpath=subpath, breadcrumbs=breadcrumbs
    )


# ===================================================================
# REST API ENDPOINTS (for WPF desktop client)
# ===================================================================


@app.route("/api/plugins", methods=["GET"])
def api_search_plugins():
    """Search and list plugins. Compatible with IMarketplaceService.SearchPluginsAsync."""
    keyword = request.args.get("Keyword", request.args.get("keyword", "")).strip()
    category = request.args.get("Category", request.args.get("category", "")).strip()
    sort_by = request.args.get("SortBy", request.args.get("sort", "updated")).strip()
    sort_order = request.args.get("SortOrder", "desc").strip()
    page = int(request.args.get("Page", request.args.get("page", 1)))
    page_size = int(request.args.get("PageSize", request.args.get("pageSize", 20)))
    page_size = min(max(page_size, 1), 100)

    plugins = scan_plugins()

    # Filter
    if keyword:
        kw = keyword.lower()
        plugins = [
            p
            for p in plugins
            if kw in p["name"].lower()
            or kw in p["id"].lower()
            or kw in p["description"].lower()
        ]
    if category:
        plugins = [p for p in plugins if p["category"].lower() == category.lower()]

    total_count = len(plugins)

    # Sort
    reverse = sort_order.lower() != "asc"
    if sort_by in ("name",):
        plugins.sort(key=lambda p: p["name"].lower(), reverse=reverse)
    elif sort_by in ("downloads",):
        plugins.sort(key=lambda p: p["total_downloads"], reverse=reverse)
    else:
        plugins.sort(key=lambda p: p["modified"], reverse=reverse)

    # Paginate
    start = (page - 1) * page_size
    items = plugins[start : start + page_size]

    return jsonify(
        {
            "items": [
                {
                    "pluginId": p["id"],
                    "name": p["name"],
                    "description": p["description"],
                    "author": p["author"],
                    "category": p["category"],
                    "iconUrl": (
                        url_for("plugin_icon", plugin_id=p["id"], _external=True)
                        if p["has_icon"]
                        else None
                    ),
                    "latestVersion": p["version"],
                    "totalDownloads": p["total_downloads"],
                    "updatedAt": p["modified"],
                }
                for p in items
            ],
            "totalCount": total_count,
            "page": page,
            "pageSize": page_size,
            "totalPages": (total_count + page_size - 1) // page_size if page_size else 0,
        }
    )


@app.route("/api/plugins/categories", methods=["GET"])
def api_categories():
    """Get all plugin categories."""
    plugins = scan_plugins()
    categories = sorted({p["category"] for p in plugins if p["category"]})
    return jsonify(categories)


@app.route("/api/plugins/batch-version-check", methods=["POST"])
def api_batch_version_check():
    """Batch check latest versions for multiple plugins at once."""
    data = request.get_json(silent=True) or {}
    plugin_ids = data.get("PluginIds", data.get("pluginIds", []))

    results = []
    for pid in plugin_ids:
        latest = read_text_file(STORAGE / "Plugins" / pid / "LATEST_RELEASE")
        if latest:
            results.append({"pluginId": pid, "latestVersion": latest})
    return jsonify(results)


@app.route("/api/plugins/<plugin_id>", methods=["GET"])
def api_plugin_detail(plugin_id):
    """Get detailed plugin information."""
    info = get_plugin_info(plugin_id)
    if not info:
        return jsonify({"error": "Plugin not found"}), 404

    return jsonify(
        {
            "pluginId": info["id"],
            "name": info["name"],
            "description": info["description"],
            "author": info["author"],
            "url": info["url"],
            "category": info["category"],
            "iconUrl": (
                url_for("plugin_icon", plugin_id=info["id"], _external=True)
                if info["has_icon"]
                else None
            ),
            "readme": info["readme"],
            "totalDownloads": info["total_downloads"],
            "updatedAt": info["modified"],
            "versions": [
                {
                    "version": pkg["version"],
                    "requiresVersion": info["requires"],
                    "fileSize": pkg["size"],
                    "downloadCount": 0,
                    "createdAt": pkg["modified"],
                }
                for pkg in info["packages"]
            ],
        }
    )


@app.route("/api/plugins/<plugin_id>/latest-version", methods=["GET"])
def api_latest_version(plugin_id):
    """
    Return latest version as plain text — backward compatible with LATEST_RELEASE.
    This endpoint is used by older clients that check version via a simple GET.
    """
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
        return jsonify({"error": "Package not found"}), 404

    _record_download(plugin_id, version)
    return send_from_directory(str(plugin_dir), filename, as_attachment=True)


@app.route("/api/packages/publish", methods=["POST"])
def api_publish_package():
    """
    Publish a new plugin version.
    Accepts multipart form: plugin metadata + .cvxp package file.
    """
    package = request.files.get("package")
    if not package or not package.filename:
        return jsonify({"error": "Package file is required"}), 400

    plugin_id = request.form.get("PluginId", request.form.get("plugin_id", "")).strip()
    version = request.form.get("Version", request.form.get("version", "")).strip()

    if not plugin_id:
        return jsonify({"error": "PluginId is required"}), 400
    if not version:
        return jsonify({"error": "Version is required"}), 400
    if not _is_safe_id(plugin_id):
        return jsonify({"error": "PluginId contains invalid characters"}), 400
    if not _is_safe_version(version):
        return jsonify({"error": "Version must be digits separated by dots"}), 400

    # Ensure directory
    plugin_dir = STORAGE / "Plugins" / plugin_id
    plugin_dir.mkdir(parents=True, exist_ok=True)

    # Save the package file
    save_filename = f"{plugin_id}-{version}.cvxp"
    save_path = plugin_dir / save_filename
    package.save(str(save_path))

    # Update LATEST_RELEASE
    latest_path = plugin_dir / "LATEST_RELEASE"
    existing = read_text_file(latest_path) or "0.0.0.0"
    try:
        if _version_tuple(version) >= _version_tuple(existing):
            latest_path.write_text(version, encoding="utf-8")
    except ValueError:
        latest_path.write_text(version, encoding="utf-8")

    # Save optional metadata
    name = request.form.get("Name", request.form.get("name", plugin_id))
    description = request.form.get("Description", request.form.get("description", ""))
    author = request.form.get("Author", request.form.get("author", ""))
    category = request.form.get("Category", request.form.get("category", ""))
    requires_ver = request.form.get(
        "RequiresVersion", request.form.get("requires_version", "")
    )
    changelog_text = request.form.get("ChangeLog", request.form.get("changelog", ""))

    # Update manifest.json
    manifest_path = plugin_dir / "manifest.json"
    manifest = {}
    if manifest_path.exists():
        try:
            with open(manifest_path, encoding="utf-8") as f:
                manifest = json.load(f)
        except (json.JSONDecodeError, OSError):
            pass

    manifest["id"] = plugin_id
    manifest["name"] = name
    if description:
        manifest["description"] = description
    if author:
        manifest["author"] = author
    if category:
        manifest["category"] = category
    if requires_ver:
        manifest["requires"] = requires_ver
    manifest["version"] = version

    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)

    # Save changelog if provided
    if changelog_text:
        (plugin_dir / "CHANGELOG.md").write_text(changelog_text, encoding="utf-8")

    # Save icon if provided
    icon = request.files.get("icon")
    if icon and icon.filename:
        icon.save(str(plugin_dir / "PackageIcon.png"))

    return (
        jsonify({"pluginId": plugin_id, "version": version}),
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
def legacy_upload(filepath):
    """
    Backward-compatible upload endpoint matching old file_manager.py pattern:
    PUT http://host:9998/upload/ColorVision/Plugins/{PluginId}/{filename}
    """
    target = STORAGE / filepath.replace("ColorVision/", "", 1)
    try:
        target.resolve().relative_to(STORAGE.resolve())
    except ValueError:
        abort(403)

    # Sanitize: ensure the final component has no path traversal
    if ".." in str(target):
        abort(403)

    target.parent.mkdir(parents=True, exist_ok=True)
    max_size = 500 * 1024 * 1024  # 500 MB limit
    total = 0
    with open(target, "wb") as f:
        while True:
            chunk = request.stream.read(8192)
            if not chunk:
                break
            total += len(chunk)
            if total > max_size:
                f.close()
                target.unlink(missing_ok=True)
                return "File too large", 413
            f.write(chunk)
    return "File uploaded successfully", 201


# ===================================================================
# Stats API
# ===================================================================


@app.route("/api/stats", methods=["GET"])
def api_stats():
    """Download statistics overview."""
    db = get_db()

    total = db.execute("SELECT COUNT(*) AS cnt FROM download_log").fetchone()["cnt"]

    per_plugin = db.execute(
        """
        SELECT plugin_id, COUNT(*) AS cnt
        FROM download_log
        GROUP BY plugin_id
        ORDER BY cnt DESC
        LIMIT 20
    """
    ).fetchall()

    recent = db.execute(
        """
        SELECT plugin_id, version, downloaded_at
        FROM download_log
        ORDER BY downloaded_at DESC
        LIMIT 20
    """
    ).fetchall()

    db.close()

    return jsonify(
        {
            "totalDownloads": total,
            "perPlugin": [
                {"pluginId": r["plugin_id"], "count": r["cnt"]} for r in per_plugin
            ],
            "recent": [
                {
                    "pluginId": r["plugin_id"],
                    "version": r["version"],
                    "downloadedAt": r["downloaded_at"],
                }
                for r in recent
            ],
        }
    )


# ===================================================================
# Entry point
# ===================================================================

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="ColorVision Plugin Marketplace")
    parser.add_argument("--storage", help="Override storage path")
    parser.add_argument("--port", type=int, help="Override port")
    parser.add_argument("--debug", action="store_true", help="Enable debug mode")
    args = parser.parse_args()

    if args.storage:
        STORAGE = Path(args.storage)
    if args.port:
        CONFIG["port"] = args.port
    if args.debug:
        CONFIG["debug"] = True

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

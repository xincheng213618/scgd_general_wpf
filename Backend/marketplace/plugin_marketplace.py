from __future__ import annotations

import hashlib
import json
import re
import shutil
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

_SAFE_ID_RE = re.compile(r"^[A-Za-z0-9_\-]+$")


def is_safe_plugin_id(value: str) -> bool:
    return bool(value) and _SAFE_ID_RE.match(value) is not None


def read_text_file(path: Path) -> str | None:
    try:
        return path.read_text(encoding="utf-8").strip()
    except (OSError, UnicodeDecodeError):
        return None


def version_tuple(version_string: str) -> tuple[int, ...]:
    return tuple(int(part) for part in version_string.split(".") if part.isdigit())


def plugin_history_dir(storage: Path, plugin_id: str) -> Path:
    return storage / "History" / "Plugins" / plugin_id


def _compute_file_hash(file_path: Path) -> str | None:
    file_hash = hashlib.sha256()
    try:
        with open(file_path, "rb") as fh:
            for chunk in iter(lambda: fh.read(8192), b""):
                file_hash.update(chunk)
    except OSError:
        return None
    return file_hash.hexdigest()


def plugin_package_from_file(
    storage: Path,
    file_path: Path,
    plugin_id: str,
    source: str,
    *,
    include_hash: bool = False,
) -> dict[str, Any] | None:
    if file_path.suffix.lower() != ".cvxp":
        return None

    match = re.match(rf"^{re.escape(plugin_id)}-(.+)\.cvxp$", file_path.name)
    version = match.group(1) if match else file_path.stem
    try:
        stat = file_path.stat()
    except OSError:
        return None

    try:
        relative_path = file_path.relative_to(storage).as_posix()
    except ValueError:
        relative_path = file_path.name

    package: dict[str, Any] = {
        "version": version,
        "filename": file_path.name,
        "size": stat.st_size,
        "source": source,
        "relative_path": relative_path,
        "modified": datetime.fromtimestamp(stat.st_mtime, tz=timezone.utc).isoformat(),
    }
    if include_hash:
        file_hash = _compute_file_hash(file_path)
        if file_hash:
            package["fileHash"] = file_hash
    return package


def scan_plugin_package_sets(
    storage: Path,
    plugin_id: str,
    *,
    include_hash: bool = False,
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    plugin_dir = storage / "Plugins" / plugin_id
    history_dir = plugin_history_dir(storage, plugin_id)
    current_packages: list[dict[str, Any]] = []
    historical_packages: list[dict[str, Any]] = []
    latest_release = read_text_file(plugin_dir / "LATEST_RELEASE") if plugin_dir.is_dir() else None

    if plugin_dir.is_dir():
        for file_path in plugin_dir.iterdir():
            package = plugin_package_from_file(
                storage,
                file_path,
                plugin_id,
                "current",
                include_hash=include_hash,
            )
            if package:
                if latest_release and package["version"] != latest_release:
                    package["source"] = "archive"
                    historical_packages.append(package)
                else:
                    current_packages.append(package)

    if history_dir.is_dir():
        for file_path in history_dir.iterdir():
            package = plugin_package_from_file(
                storage,
                file_path,
                plugin_id,
                "archive",
                include_hash=include_hash,
            )
            if package:
                historical_packages.append(package)

    current_packages.sort(key=lambda item: item["modified"], reverse=True)
    historical_packages.sort(key=lambda item: item["modified"], reverse=True)
    return current_packages, historical_packages


def plugin_signature(storage: Path, plugin_id: str) -> str:
    plugin_dir = storage / "Plugins" / plugin_id
    history_dir = plugin_history_dir(storage, plugin_id)
    parts: list[str] = []
    try:
        dir_stat = plugin_dir.stat()
        parts.append(f"dir:{dir_stat.st_mtime_ns}")
    except OSError:
        return "missing"

    try:
        entries = sorted(plugin_dir.iterdir(), key=lambda item: item.name.lower())
    except OSError:
        return "unreadable"

    for entry in entries:
        if entry.name.startswith("."):
            continue
        try:
            stat = entry.stat()
        except OSError:
            continue
        parts.append(f"{entry.name}:{'d' if entry.is_dir() else 'f'}:{stat.st_mtime_ns}:{stat.st_size}")

    if history_dir.is_dir():
        try:
            history_entries = sorted(history_dir.iterdir(), key=lambda item: item.name.lower())
        except OSError:
            history_entries = []
        for entry in history_entries:
            if entry.name.startswith("."):
                continue
            try:
                stat = entry.stat()
            except OSError:
                continue
            parts.append(f"history:{entry.name}:{stat.st_mtime_ns}:{stat.st_size}")

    return "|".join(parts)


def plugin_summary_signature(storage: Path, plugin_id: str) -> str:
    plugin_dir = storage / "Plugins" / plugin_id
    history_dir = plugin_history_dir(storage, plugin_id)
    parts: list[str] = []
    for name in ("LATEST_RELEASE", "manifest.json", "PackageIcon.png"):
        candidate = plugin_dir / name
        try:
            stat = candidate.stat()
            parts.append(f"{name}:{stat.st_mtime_ns}:{stat.st_size}")
        except OSError:
            parts.append(f"{name}:missing")

    try:
        current_entries = sorted(plugin_dir.glob("*.cvxp"), key=lambda item: item.name.lower())
    except OSError:
        current_entries = []
    for entry in current_entries:
        try:
            stat = entry.stat()
        except OSError:
            continue
        parts.append(f"current:{entry.name}:{stat.st_mtime_ns}:{stat.st_size}")

    try:
        history_stat = history_dir.stat()
        history_count = sum(1 for item in history_dir.glob("*.cvxp") if item.is_file())
        parts.append(f"history:{history_stat.st_mtime_ns}:{history_count}")
    except OSError:
        parts.append("history:missing")

    return "|".join(parts)


def plugin_catalog_signature(storage: Path) -> str:
    plugins_dir = storage / "Plugins"
    if not plugins_dir.is_dir():
        return "missing"

    parts: list[str] = []
    for entry in sorted(plugins_dir.iterdir(), key=lambda item: item.name.lower()):
        if not entry.is_dir():
            continue
        parts.append(f"{entry.name}:{plugin_summary_signature(storage, entry.name)}")
    return "|".join(parts)


def _latest_current_package(storage: Path, plugin_id: str) -> dict[str, Any] | None:
    plugin_dir = storage / "Plugins" / plugin_id
    latest: dict[str, Any] | None = None
    for file_path in plugin_dir.glob("*.cvxp"):
        package = plugin_package_from_file(storage, file_path, plugin_id, "current", include_hash=False)
        if not package:
            continue
        if latest is None or (package["modified"], package["version"]) > (latest["modified"], latest["version"]):
            latest = package
    return latest


def _count_plugin_packages(directory: Path) -> int:
    if not directory.is_dir():
        return 0
    return sum(1 for file_path in directory.glob("*.cvxp") if file_path.is_file())


def _load_manifest(manifest_path: Path) -> dict[str, Any]:
    if not manifest_path.exists():
        return {}
    try:
        with open(manifest_path, encoding="utf-8") as f:
            return json.load(f)
    except (json.JSONDecodeError, OSError):
        return {}


def _normalize_manifest_keys(manifest: dict[str, Any]) -> dict[str, Any]:
    normalized: dict[str, Any] = {}
    for key, value in manifest.items():
        if not isinstance(key, str):
            continue
        lowered = key.lower()
        if lowered == "pluginid":
            lowered = "id"
        if lowered == "requiresversion":
            lowered = "requires"
        normalized[lowered] = value
    return normalized


def _archive_cache_key(relative_path: str) -> str:
    return f"plugin_archive_meta:v1:{relative_path}"


def _read_zip_text_file(archive: zipfile.ZipFile, member_name: str) -> str | None:
    try:
        raw = archive.read(member_name)
    except KeyError:
        return None
    for encoding in ("utf-8-sig", "utf-8", "gb18030"):
        try:
            return raw.decode(encoding).strip()
        except UnicodeDecodeError:
            continue
    return raw.decode("utf-8", errors="ignore").strip()


def _archive_member_map(archive: zipfile.ZipFile) -> dict[str, str]:
    member_map: dict[str, str] = {}
    for name in archive.namelist():
        if name.endswith("/"):
            continue
        basename = Path(name).name.lower()
        member_map.setdefault(basename, name)
    return member_map


def _select_preferred_package_path(storage: Path, plugin_id: str, latest_version: str) -> Path | None:
    plugin_dir = storage / "Plugins" / plugin_id
    history_dir = plugin_history_dir(storage, plugin_id)
    preferred_name = f"{plugin_id}-{latest_version}.cvxp" if latest_version else ""

    if preferred_name and (plugin_dir / preferred_name).is_file():
        return plugin_dir / preferred_name

    current_candidates = [path for path in plugin_dir.glob("*.cvxp") if path.is_file()]
    current_candidates.sort(key=lambda item: item.stat().st_mtime, reverse=True)
    if current_candidates:
        return current_candidates[0]

    history_candidates = [path for path in history_dir.glob("*.cvxp") if path.is_file()]
    history_candidates.sort(key=lambda item: item.stat().st_mtime, reverse=True)
    return history_candidates[0] if history_candidates else None


def _read_archive_metadata(
    storage: Path,
    package_path: Path,
    *,
    get_cache_entry: Callable[..., dict[str, Any] | None],
    set_cache_entry: Callable[..., None],
) -> dict[str, Any]:
    try:
        stat = package_path.stat()
        relative_path = package_path.relative_to(storage).as_posix()
    except (OSError, ValueError):
        return {}

    cache_key = _archive_cache_key(relative_path)
    signature = f"{stat.st_mtime_ns}:{stat.st_size}"
    cached = get_cache_entry(cache_key, signature=signature)
    if cached:
        return dict(cached["value"])

    try:
        with zipfile.ZipFile(package_path) as archive:
            members = _archive_member_map(archive)
            manifest_name = members.get("manifest.json")
            readme_name = members.get("readme.md")
            changelog_name = members.get("changelog.md")
            icon_name = members.get("packageicon.png")

            manifest_payload = {}
            if manifest_name:
                manifest_text = _read_zip_text_file(archive, manifest_name)
                if manifest_text:
                    try:
                        manifest_payload = _normalize_manifest_keys(json.loads(manifest_text))
                    except json.JSONDecodeError:
                        manifest_payload = {}

            metadata = {
                "manifest": manifest_payload,
                "readme": _read_zip_text_file(archive, readme_name) if readme_name else "",
                "changelog": _read_zip_text_file(archive, changelog_name) if changelog_name else "",
                "has_icon": bool(icon_name),
                "sourceArchive": relative_path,
            }
    except (OSError, zipfile.BadZipFile):
        return {}

    set_cache_entry(cache_key, metadata, ttl_seconds=86400, signature=signature)
    return metadata


def _get_archive_metadata_for_plugin(
    storage: Path,
    plugin_id: str,
    latest_version: str,
    *,
    get_cache_entry: Callable[..., dict[str, Any] | None],
    set_cache_entry: Callable[..., None],
) -> dict[str, Any]:
    package_path = _select_preferred_package_path(storage, plugin_id, latest_version)
    if not package_path:
        return {}
    return _read_archive_metadata(
        storage,
        package_path,
        get_cache_entry=get_cache_entry,
        set_cache_entry=set_cache_entry,
    )


def get_plugin_summary(
    storage: Path,
    plugin_id: str,
    *,
    download_counts: dict[str, int],
    get_cache_entry: Callable[..., dict[str, Any] | None],
    set_cache_entry: Callable[..., None],
    ttl_seconds: int,
) -> dict[str, Any] | None:
    if not is_safe_plugin_id(plugin_id):
        return None

    plugin_dir = storage / "Plugins" / plugin_id
    if not plugin_dir.is_dir():
        return None

    signature = plugin_summary_signature(storage, plugin_id)
    cache_key = f"plugin_summary:v1:{plugin_id}"
    cached = get_cache_entry(cache_key, signature=signature)
    if cached:
        summary = dict(cached["value"])
        summary["total_downloads"] = download_counts.get(plugin_id, 0)
        return summary

    manifest = _load_manifest(plugin_dir / "manifest.json")
    latest_version = read_text_file(plugin_dir / "LATEST_RELEASE") or ""
    archive_metadata = {}
    if not manifest:
        archive_metadata = _get_archive_metadata_for_plugin(
            storage,
            plugin_id,
            latest_version,
            get_cache_entry=get_cache_entry,
            set_cache_entry=set_cache_entry,
        )
        manifest = dict(archive_metadata.get("manifest") or {})
    latest_package = _latest_current_package(storage, plugin_id)
    current_package_count = _count_plugin_packages(plugin_dir)
    historical_package_count = _count_plugin_packages(plugin_history_dir(storage, plugin_id))
    modified = (
        latest_package["modified"]
        if latest_package
        else datetime.fromtimestamp(plugin_dir.stat().st_mtime, tz=timezone.utc).isoformat()
    )

    summary = {
        "id": manifest.get("id", plugin_id),
        "name": manifest.get("name", plugin_id),
        "description": manifest.get("description", ""),
        "author": manifest.get("author", ""),
        "url": manifest.get("url", ""),
        "version": latest_version or (latest_package["version"] if latest_package else ""),
        "requires": manifest.get("requires", ""),
        "category": manifest.get("category", ""),
        "has_icon": (plugin_dir / "PackageIcon.png").exists() or bool(archive_metadata.get("has_icon")),
        "total_downloads": download_counts.get(plugin_id, 0),
        "modified": modified,
        "current_package_count": current_package_count,
        "historical_package_count": historical_package_count,
    }
    set_cache_entry(cache_key, summary, ttl_seconds=ttl_seconds, signature=signature)
    return summary


def scan_plugin_summaries(
    storage: Path,
    *,
    download_counts: dict[str, int],
    get_cache_entry: Callable[..., dict[str, Any] | None],
    set_cache_entry: Callable[..., None],
    ttl_seconds: int,
) -> list[dict[str, Any]]:
    signature = plugin_catalog_signature(storage)
    cache_key = "plugin_catalog:v1"
    cached = get_cache_entry(cache_key, signature=signature)
    if cached:
        items = list(cached["value"])
        for item in items:
            item["total_downloads"] = download_counts.get(item["id"], 0)
        return items

    plugins_dir = storage / "Plugins"
    if not plugins_dir.is_dir():
        return []

    items: list[dict[str, Any]] = []
    for entry in sorted(plugins_dir.iterdir(), key=lambda item: item.name.lower()):
        if not entry.is_dir():
            continue
        summary = get_plugin_summary(
            storage,
            entry.name,
            download_counts=download_counts,
            get_cache_entry=get_cache_entry,
            set_cache_entry=set_cache_entry,
            ttl_seconds=ttl_seconds,
        )
        if summary:
            items.append(summary)
    set_cache_entry(cache_key, items, ttl_seconds=ttl_seconds, signature=signature)
    return items


def get_plugin_detail(
    storage: Path,
    plugin_id: str,
    *,
    download_counts: dict[str, int],
    get_cache_entry: Callable[..., dict[str, Any] | None],
    set_cache_entry: Callable[..., None],
    ttl_seconds: int,
) -> dict[str, Any] | None:
    if not is_safe_plugin_id(plugin_id):
        return None

    plugin_dir = storage / "Plugins" / plugin_id
    if not plugin_dir.is_dir():
        return None

    signature = plugin_signature(storage, plugin_id)
    cache_key = f"plugin_detail:v1:{plugin_id}"
    cached = get_cache_entry(cache_key, signature=signature)
    if cached:
        detail = dict(cached["value"])
        detail["total_downloads"] = download_counts.get(plugin_id, 0)
        return detail

    summary = get_plugin_summary(
        storage,
        plugin_id,
        download_counts=download_counts,
        get_cache_entry=get_cache_entry,
        set_cache_entry=set_cache_entry,
        ttl_seconds=ttl_seconds,
    )
    if not summary:
        return None

    current_packages, historical_packages = scan_plugin_package_sets(
        storage,
        plugin_id,
        include_hash=True,
    )
    archive_metadata = _get_archive_metadata_for_plugin(
        storage,
        plugin_id,
        str(summary.get("version") or ""),
        get_cache_entry=get_cache_entry,
        set_cache_entry=set_cache_entry,
    )
    detail = {
        **summary,
        "readme": read_text_file(plugin_dir / "README.md") or str(archive_metadata.get("readme") or ""),
        "changelog": read_text_file(plugin_dir / "CHANGELOG.md") or str(archive_metadata.get("changelog") or ""),
        "packages": list(current_packages),
        "current_packages": current_packages,
        "historical_packages": historical_packages,
        "all_packages": current_packages + historical_packages,
    }
    set_cache_entry(cache_key, detail, ttl_seconds=ttl_seconds, signature=signature)
    return detail


def prewarm_plugin_metadata(
    storage: Path,
    plugin_id: str,
    version: str,
    *,
    download_counts: dict[str, int],
    get_cache_entry: Callable[..., dict[str, Any] | None],
    set_cache_entry: Callable[..., None],
    ttl_seconds: int,
) -> None:
    get_plugin_summary(
        storage,
        plugin_id,
        download_counts=download_counts,
        get_cache_entry=get_cache_entry,
        set_cache_entry=set_cache_entry,
        ttl_seconds=ttl_seconds,
    )
    get_plugin_detail(
        storage,
        plugin_id,
        download_counts=download_counts,
        get_cache_entry=get_cache_entry,
        set_cache_entry=set_cache_entry,
        ttl_seconds=ttl_seconds,
    )


def reconcile_plugin_package_history(
    storage: Path,
    plugin_id: str,
    *,
    keep_latest: int,
    on_changed: Callable[[str], None] | None = None,
) -> list[dict[str, str]]:
    plugin_dir = storage / "Plugins" / plugin_id
    if not plugin_dir.is_dir():
        return []

    keep_latest = max(int(keep_latest), 1)
    candidates: list[tuple[tuple[int, ...], str, Path]] = []
    for entry in plugin_dir.iterdir():
        package = plugin_package_from_file(storage, entry, plugin_id, "current", include_hash=False)
        if not package:
            continue
        candidates.append((version_tuple(package["version"]), package["modified"], entry))

    candidates.sort(key=lambda item: (item[0], item[1]), reverse=True)
    moved: list[dict[str, str]] = []
    history_dir = plugin_history_dir(storage, plugin_id)
    history_dir.mkdir(parents=True, exist_ok=True)

    for _, _, source_path in candidates[keep_latest:]:
        target_path = history_dir / source_path.name
        if target_path.exists():
            try:
                if target_path.stat().st_size == source_path.stat().st_size:
                    source_path.unlink(missing_ok=True)
                    moved.append(
                        {
                            "from": source_path.name,
                            "to": target_path.relative_to(storage).as_posix(),
                        }
                    )
                    continue
            except OSError:
                pass
            stamp = datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S")
            target_path = history_dir / f"{source_path.stem}-{stamp}{source_path.suffix}"

        shutil.move(str(source_path), str(target_path))
        moved.append(
            {
                "from": source_path.name,
                "to": target_path.relative_to(storage).as_posix(),
            }
        )

    if moved and on_changed:
        on_changed(plugin_id)
    return moved


def reconcile_all_plugin_package_histories(
    storage: Path,
    *,
    keep_latest: int,
    on_changed: Callable[[str], None] | None = None,
) -> dict[str, list[dict[str, str]]]:
    plugins_dir = storage / "Plugins"
    if not plugins_dir.is_dir():
        return {}

    results: dict[str, list[dict[str, str]]] = {}
    for entry in sorted(plugins_dir.iterdir(), key=lambda item: item.name.lower()):
        if not entry.is_dir():
            continue
        moved = reconcile_plugin_package_history(
            storage,
            entry.name,
            keep_latest=keep_latest,
            on_changed=on_changed,
        )
        if moved:
            results[entry.name] = moved
    return results





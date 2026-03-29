from __future__ import annotations

import hashlib
import json
import re
import shutil
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


def _load_manifest(manifest_path: Path) -> dict[str, Any]:
    if not manifest_path.exists():
        return {}
    try:
        with open(manifest_path, encoding="utf-8") as f:
            return json.load(f)
    except (json.JSONDecodeError, OSError):
        return {}


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

    signature = plugin_signature(storage, plugin_id)
    cache_key = f"plugin_summary:v1:{plugin_id}"
    cached = get_cache_entry(cache_key, signature=signature)
    if cached:
        summary = dict(cached["value"])
        summary["total_downloads"] = download_counts.get(plugin_id, 0)
        return summary

    manifest = _load_manifest(plugin_dir / "manifest.json")
    latest_version = read_text_file(plugin_dir / "LATEST_RELEASE") or ""
    current_packages, historical_packages = scan_plugin_package_sets(
        storage,
        plugin_id,
        include_hash=False,
    )
    modified = (
        current_packages[0]["modified"]
        if current_packages
        else datetime.fromtimestamp(plugin_dir.stat().st_mtime, tz=timezone.utc).isoformat()
    )

    summary = {
        "id": manifest.get("id", plugin_id),
        "name": manifest.get("name", plugin_id),
        "description": manifest.get("description", ""),
        "author": manifest.get("author", ""),
        "url": manifest.get("url", ""),
        "version": latest_version or (current_packages[0]["version"] if current_packages else ""),
        "requires": manifest.get("requires", ""),
        "category": manifest.get("category", ""),
        "has_icon": (plugin_dir / "PackageIcon.png").exists(),
        "total_downloads": download_counts.get(plugin_id, 0),
        "modified": modified,
        "current_package_count": len(current_packages),
        "historical_package_count": len(historical_packages),
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
    detail = {
        **summary,
        "readme": read_text_file(plugin_dir / "README.md") or "",
        "changelog": read_text_file(plugin_dir / "CHANGELOG.md") or "",
        "packages": list(current_packages),
        "current_packages": current_packages,
        "historical_packages": historical_packages,
        "all_packages": current_packages + historical_packages,
    }
    set_cache_entry(cache_key, detail, ttl_seconds=ttl_seconds, signature=signature)
    return detail


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


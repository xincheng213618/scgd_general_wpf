from __future__ import annotations

import hashlib
import os
import threading
from pathlib import Path
from typing import Any

_lock = threading.RLock()
_latest_cache: dict[str, str] = {}
_plugin_cache: dict[str, dict[str, str]] = {}


def _key(storage: Path) -> str:
    return os.path.normcase(os.path.abspath(os.fspath(storage)))


def _read_latest_version(storage: Path) -> str:
    try:
        return (storage / "LATEST_RELEASE").read_text(encoding="utf-8").strip()
    except (OSError, UnicodeDecodeError):
        return ""


def warm_latest_version_cache(storage: Path) -> str:
    version = _read_latest_version(storage)
    with _lock:
        _latest_cache[_key(storage)] = version
    return version


def set_latest_version_cache(storage: Path, version: str) -> str:
    clean = (version or "").strip()
    with _lock:
        _latest_cache[_key(storage)] = clean
    return clean


def refresh_latest_version_cache(storage: Path) -> str:
    return warm_latest_version_cache(storage)


def get_latest_version_cached(storage: Path) -> str:
    cache_key = _key(storage)
    with _lock:
        if cache_key in _latest_cache:
            return _latest_cache[cache_key]
    return warm_latest_version_cache(storage)


def latest_version_etag(storage: Path, version: str) -> str:
    raw = f"{_key(storage)}:{version}".encode("utf-8")
    return hashlib.sha1(raw).hexdigest()


def _load_plugin_versions_from_index(cache_manager: Any) -> dict[str, str]:
    versions: dict[str, str] = {}
    if cache_manager is None:
        return versions

    db = cache_manager.get_db()
    try:
        rows = db.execute(
            """SELECT plugin_id, latest_version
               FROM plugin_index
               WHERE is_deleted = 0
                 AND latest_version IS NOT NULL
                 AND latest_version != ''"""
        ).fetchall()
        for row in rows:
            plugin_id = (row["plugin_id"] or "").strip()
            latest = (row["latest_version"] or "").strip()
            if plugin_id and latest:
                versions[plugin_id] = latest
    except Exception:
        return {}
    finally:
        db.close()
    return versions


def _load_plugin_versions_from_disk(storage: Path) -> dict[str, str]:
    versions: dict[str, str] = {}
    plugins_dir = storage / "Plugins"
    if not plugins_dir.is_dir():
        return versions

    for entry in plugins_dir.iterdir():
        if not entry.is_dir():
            continue
        latest = _read_latest_version(entry)
        if latest:
            versions[entry.name] = latest
    return versions


def warm_plugin_latest_versions_cache(storage: Path, cache_manager: Any = None) -> dict[str, str]:
    versions = _load_plugin_versions_from_index(cache_manager)
    if not versions:
        versions = _load_plugin_versions_from_disk(storage)
    with _lock:
        _plugin_cache[_key(storage)] = dict(versions)
    return versions


def set_plugin_latest_version_cache(storage: Path, plugin_id: str, version: str | None) -> None:
    clean_id = (plugin_id or "").strip()
    if not clean_id:
        return
    clean_version = (version or "").strip()
    with _lock:
        versions = _plugin_cache.setdefault(_key(storage), {})
        if clean_version:
            versions[clean_id] = clean_version
        else:
            versions.pop(clean_id, None)


def get_plugin_latest_versions_cached(
    storage: Path,
    plugin_ids: list[str],
    cache_manager: Any = None,
) -> dict[str, str]:
    cache_key = _key(storage)
    with _lock:
        versions = _plugin_cache.get(cache_key)
        if versions is not None:
            return {plugin_id: versions[plugin_id] for plugin_id in plugin_ids if plugin_id in versions}

    versions = warm_plugin_latest_versions_cache(storage, cache_manager)
    return {plugin_id: versions[plugin_id] for plugin_id in plugin_ids if plugin_id in versions}

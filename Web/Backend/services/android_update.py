"""Build the fixed-source Android application update contract."""

from __future__ import annotations

import hashlib
from pathlib import Path
from typing import Any, Callable, Iterable

from app_releases import version_tuple


ANDROID_UPDATE_CACHE_KEY = "android_update_sha256:v1"
ANDROID_UPDATE_CACHE_TTL_SECONDS = 30 * 24 * 60 * 60

GetCacheEntry = Callable[..., dict[str, Any] | None]
SetCacheEntry = Callable[..., None]


def select_latest_android_release(releases: Iterable[dict[str, Any]]) -> dict[str, Any] | None:
    candidates = [
        release
        for release in releases
        if str(release.get("kind", "")).upper() == "APK"
        and str(release.get("source", "")).lower() == "current"
    ]
    if not candidates:
        return None
    return max(
        candidates,
        key=lambda release: (
            version_tuple(str(release.get("version", ""))),
            str(release.get("modified", "")),
        ),
    )


def resolve_android_release_file(storage: Path, release: dict[str, Any]) -> Path:
    relative_path = str(release.get("relative_path", "")).strip().replace("\\", "/")
    target = (storage / relative_path).resolve()
    try:
        target.relative_to(storage.resolve())
    except ValueError as exc:
        raise FileNotFoundError("Android release path escapes storage") from exc
    version = str(release.get("version", "")).strip()
    expected_name = f"ColorVision-Android-{version}.apk"
    if target.parent != storage.resolve() or target.name != expected_name or not target.is_file():
        raise FileNotFoundError("Android release APK not found")
    return target


def build_android_update_manifest(
    storage: Path,
    releases: Iterable[dict[str, Any]],
    *,
    get_cache_entry: GetCacheEntry,
    set_cache_entry: SetCacheEntry,
) -> dict[str, Any]:
    release = select_latest_android_release(releases)
    if release is None:
        return {"schemaVersion": 1, "available": False, "release": None}

    try:
        target = resolve_android_release_file(storage, release)
    except FileNotFoundError:
        return {"schemaVersion": 1, "available": False, "release": None}
    stat = target.stat()
    relative_path = target.relative_to(storage.resolve()).as_posix()
    signature = f"{relative_path}:{stat.st_size}:{stat.st_mtime_ns}"
    cached = get_cache_entry(ANDROID_UPDATE_CACHE_KEY, signature=signature)
    sha256 = str(cached["value"]) if cached else _sha256_file(target)
    if not cached:
        set_cache_entry(
            ANDROID_UPDATE_CACHE_KEY,
            sha256,
            ttl_seconds=ANDROID_UPDATE_CACHE_TTL_SECONDS,
            signature=signature,
        )

    version = str(release.get("version", "")).strip()
    return {
        "schemaVersion": 1,
        "available": True,
        "release": {
            "version": version,
            "filename": target.name,
            "size": stat.st_size,
            "sha256": sha256,
            "downloadUrl": f"/api/android/update/{version}/download",
        },
    }


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()

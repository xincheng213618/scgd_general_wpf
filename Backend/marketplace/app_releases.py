from __future__ import annotations

import re
import shutil
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable


_APP_RELEASE_CANONICAL_RE = re.compile(
    r"^ColorVision-(\d+(?:\.\d+)+)\.(exe|zip|rar)$", re.IGNORECASE
)
_APP_RELEASE_VERSION_RE = re.compile(r"(\d+\.\d+\.\d+\.\d+)")

GetCacheEntry = Callable[..., dict[str, Any] | None]
SetCacheEntry = Callable[..., None]
OnChanged = Callable[[str], None] | None

_KIND_LABELS = {
    "EXE": "安装包",
    "ZIP": "ZIP 归档",
    "RAR": "RAR 归档",
    "FILE": "文件记录",
}
_ERA_LABELS = {
    "archive": "压缩归档时代",
    "installer": "安装包时代",
    "other": "其他记录",
}


def _format_modified(timestamp: float) -> tuple[str, str, str]:
    dt = datetime.fromtimestamp(timestamp, tz=timezone.utc)
    return dt.isoformat(), dt.strftime("%Y-%m-%d %H:%M"), dt.strftime("%Y-%m-%d")


def _build_kind_summary(items: list[dict[str, Any]]) -> str:
    counts: dict[str, int] = {}
    for item in items:
        label = str(item.get("kind_label", item.get("kind", "文件记录")))
        counts[label] = counts.get(label, 0) + 1
    return " · ".join(
        f"{label} × {count}"
        for label, count in sorted(counts.items(), key=lambda pair: pair[0])
    )


def _build_time_range(items: list[dict[str, Any]]) -> str:
    if not items:
        return ""
    latest = str(items[0].get("modified_display", ""))
    earliest = str(items[-1].get("modified_display", ""))
    if latest == earliest:
        return latest
    return f"{earliest} → {latest}"


def classify_artifact_era(kind: str) -> tuple[str, str]:
    normalized = str(kind or "").upper()
    if normalized in {"ZIP", "RAR"}:
        return "archive", _ERA_LABELS["archive"]
    if normalized == "EXE":
        return "installer", _ERA_LABELS["installer"]
    return "other", _ERA_LABELS["other"]


def build_archive_timeline_groups(archived_releases: list[dict[str, Any]]) -> list[dict[str, Any]]:
    groups: list[dict[str, Any]] = []
    indexed: dict[tuple[str, str], dict[str, Any]] = {}

    for item in archived_releases:
        key = (str(item.get("major_minor", "")), str(item.get("branch", "")))
        group = indexed.get(key)
        if group is None:
            group = {
                "major_minor": key[0],
                "branch": key[1],
                "items": [],
            }
            indexed[key] = group
            groups.append(group)
        group["items"].append(item)

    for group in groups:
        items = group["items"]
        era_counts: dict[str, int] = {}
        kind_keys: set[str] = set()
        group["count"] = len(items)
        group["latest_modified"] = items[0].get("modified", "") if items else ""
        group["latest_modified_display"] = items[0].get("modified_display", "") if items else ""
        group["earliest_modified"] = items[-1].get("modified", "") if items else ""
        group["earliest_modified_display"] = items[-1].get("modified_display", "") if items else ""
        group["time_range_display"] = _build_time_range(items)
        group["kind_summary"] = _build_kind_summary(items)
        group["latest_relative_path"] = items[0].get("relative_path", "") if items else ""
        for item in items:
            era = str(item.get("era", "other"))
            era_counts[era] = era_counts.get(era, 0) + 1
            kind_keys.add(str(item.get("kind", "")).upper())
        group["era_counts"] = era_counts
        group["era_summary"] = " · ".join(
            f"{_ERA_LABELS.get(era, era)} × {count}"
            for era, count in sorted(era_counts.items(), key=lambda pair: pair[0])
        )
        group["kind_keys"] = sorted(kind_keys)
        group["contains_archive_only_formats"] = any(
            str(item.get("kind", "")) in {"ZIP", "RAR"}
            or "ZIP" in str(item.get("kind_label", "")).upper()
            or "RAR" in str(item.get("kind_label", "")).upper()
            for item in items
        )
        group["contains_installer_artifacts"] = any(
            str(item.get("kind", "")).upper() == "EXE"
            or "安装包" in str(item.get("kind_label", ""))
            for item in items
        )

    return groups


def version_tuple(value: str) -> tuple[int, ...]:
    return tuple(int(part) for part in value.split(".") if part.isdigit())


def extract_release_version(name: str) -> str | None:
    canonical = _APP_RELEASE_CANONICAL_RE.match(name)
    if canonical:
        return canonical.group(1)
    loose = _APP_RELEASE_VERSION_RE.search(name)
    return loose.group(1) if loose else None


def is_root_release_file(storage: Path, path: Path) -> bool:
    return (
        path.parent == storage
        and path.is_file()
        and _APP_RELEASE_CANONICAL_RE.match(path.name) is not None
    )


def release_bucket(version: str) -> tuple[str, str]:
    parts = version.split(".")
    if len(parts) >= 3:
        return ".".join(parts[:2]), ".".join(parts[:3])
    if len(parts) >= 2:
        joined = ".".join(parts[:2])
        return joined, joined
    return parts[0], parts[0]


def app_release_history_dir(storage: Path, version: str) -> Path:
    major_minor, branch = release_bucket(version)
    return storage / "History" / major_minor / branch


def build_release_artifact(
    storage: Path,
    file_path: Path,
    source: str,
) -> dict[str, Any] | None:
    version = extract_release_version(file_path.name)
    if not version:
        return None

    try:
        stat = file_path.stat()
        relative_path = file_path.relative_to(storage).as_posix()
    except (OSError, ValueError):
        return None

    major_minor, branch = release_bucket(version)
    modified_iso, modified_display, modified_date = _format_modified(stat.st_mtime)
    kind = file_path.suffix.lstrip(".").upper() or "FILE"
    era, era_label = classify_artifact_era(kind)
    return {
        "filename": file_path.name,
        "version": version,
        "size": stat.st_size,
        "kind": kind,
        "kind_label": _KIND_LABELS.get(kind, kind),
        "era": era,
        "era_label": era_label,
        "source": source,
        "major_minor": major_minor,
        "branch": branch,
        "relative_path": relative_path,
        "modified": modified_iso,
        "modified_display": modified_display,
        "modified_date": modified_date,
        "display_title": f"ColorVision {version}",
    }


def release_sort_key(item: dict[str, Any]) -> tuple[Any, ...]:
    return (
        version_tuple(str(item.get("version", "0.0.0.0"))),
        item.get("source") == "current",
        item.get("modified", ""),
    )


def scan_app_release_artifacts(
    storage: Path,
    *,
    get_cache_entry: GetCacheEntry,
    set_cache_entry: SetCacheEntry,
    cache_key: str,
    ttl_seconds: int,
) -> list[dict[str, Any]]:
    cached = get_cache_entry(cache_key)
    if cached:
        return cached["value"]

    artifacts: list[dict[str, Any]] = []
    if not storage.is_dir():
        return artifacts

    for entry in storage.iterdir():
        if not entry.is_file():
            continue
        artifact = build_release_artifact(storage, entry, "current")
        if artifact:
            artifacts.append(artifact)

    history_dir = storage / "History"
    if history_dir.is_dir():
        for entry in history_dir.rglob("*"):
            if not entry.is_file():
                continue
            artifact = build_release_artifact(storage, entry, "archive")
            if artifact:
                artifacts.append(artifact)

    artifacts.sort(key=release_sort_key, reverse=True)
    set_cache_entry(
        cache_key,
        artifacts,
        ttl_seconds=ttl_seconds,
        signature="releases",
    )
    return artifacts


def build_app_release_context(releases: list[dict[str, Any]]) -> dict[str, Any]:
    current_releases = [item for item in releases if item["source"] == "current"]
    archived_releases = [item for item in releases if item["source"] == "archive"]
    latest_release = current_releases[0] if current_releases else (releases[0] if releases else None)
    recent_branches: list[str] = []
    for item in releases:
        branch = str(item.get("branch", ""))
        if branch and branch not in recent_branches:
            recent_branches.append(branch)

    archive_timeline_groups = build_archive_timeline_groups(archived_releases)

    return {
        "latest_release": latest_release,
        "current_releases": current_releases,
        "archived_releases": archived_releases,
        "current_preview": current_releases[:6],
        "archive_preview": archived_releases[:10],
        "archive_recent": archived_releases[:120],
        "archive_timeline_groups": archive_timeline_groups,
        "archive_timeline_preview": archive_timeline_groups[:4],
        "archive_timeline_count": len(archive_timeline_groups),
        "current_count": len(current_releases),
        "archive_count": len(archived_releases),
        "release_branch_count": len(recent_branches),
        "archive_more_count": max(len(archived_releases) - 120, 0),
    }


def reconcile_app_release_history(
    storage: Path,
    *,
    keep_latest: int,
    on_changed: OnChanged = None,
) -> list[dict[str, str]]:
    keep_latest = max(int(keep_latest), 1)
    if not storage.is_dir():
        return []

    candidates: list[tuple[tuple[int, ...], Path, str]] = []
    for entry in storage.iterdir():
        if not is_root_release_file(storage, entry):
            continue
        version = extract_release_version(entry.name)
        if not version:
            continue
        candidates.append((version_tuple(version), entry, version))

    candidates.sort(key=lambda item: item[0], reverse=True)

    moved: list[dict[str, str]] = []
    for _, source_path, version in candidates[keep_latest:]:
        target_dir = app_release_history_dir(storage, version)
        target_dir.mkdir(parents=True, exist_ok=True)
        target_path = target_dir / source_path.name

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
            target_path = target_dir / f"{source_path.stem}-{stamp}{source_path.suffix}"

        shutil.move(str(source_path), str(target_path))
        moved.append(
            {
                "from": source_path.name,
                "to": target_path.relative_to(storage).as_posix(),
            }
        )

    if moved and on_changed:
        on_changed("History")
    return moved


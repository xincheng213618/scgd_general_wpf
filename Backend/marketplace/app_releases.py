from __future__ import annotations

import re
import shutil
import time
import logging
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

from app_changelog import base_release_version, resolve_changelog_for_release_group


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

_logger = logging.getLogger(__name__)


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


def _parse_iso_datetime(value: str) -> datetime:
    try:
        return datetime.fromisoformat(value)
    except ValueError:
        return datetime.fromtimestamp(0, tz=timezone.utc)


def _timeline_radius(fix_count: int) -> int:
    return max(7, min(22, 7 + max(fix_count - 1, 0) * 2))


def _is_windows_file_lock_error(exc: BaseException) -> bool:
    if not isinstance(exc, PermissionError):
        return False
    return getattr(exc, "winerror", None) == 32


def _move_file_with_retry(source_path: Path, target_path: Path) -> None:
    backoff_seconds = (0.2, 0.5, 1.0)
    for index, delay in enumerate(backoff_seconds):
        try:
            shutil.move(str(source_path), str(target_path))
            return
        except PermissionError as exc:
            if not _is_windows_file_lock_error(exc) or index == len(backoff_seconds) - 1:
                raise
            time.sleep(delay)


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


def build_release_timeline(
    releases: list[dict[str, Any]],
    *,
    changelog_lookup: dict[str, dict[str, Any]] | None = None,
) -> dict[str, Any]:
    current_releases = [item for item in releases if item.get("source") == "current"]
    archived_releases = [item for item in releases if item.get("source") == "archive"]

    grouped: dict[str, dict[str, Any]] = {}
    for item in archived_releases:
        branch = base_release_version(str(item.get("version", ""))) or str(item.get("branch", ""))
        group = grouped.get(branch)
        if group is None:
            group = {
                "branch": branch,
                "major_minor": str(item.get("major_minor", "")),
                "items": [],
                "versions": [],
                "fix_values": set(),
            }
            grouped[branch] = group
        group["items"].append(item)
        version = str(item.get("version", ""))
        if version and version not in group["versions"]:
            group["versions"].append(version)
        parts = [part for part in version.split(".") if part]
        if len(parts) >= 4 and parts[-1].isdigit():
            group["fix_values"].add(int(parts[-1]))

    nodes: list[dict[str, Any]] = []
    for branch, group in grouped.items():
        items = sorted(group["items"], key=lambda item: item.get("modified", ""), reverse=True)
        latest = items[0]
        earliest = items[-1]
        fix_count = max(len(group["fix_values"]) or len(group["versions"]), 1)
        mapped = resolve_changelog_for_release_group(
            changelog_lookup or {},
            versions=sorted(group["versions"], key=version_tuple, reverse=True),
            branch=branch,
        )
        sort_date = str(mapped.get("date_display", "")) if mapped else str(latest.get("modified_date", ""))
        annotation_label = str(mapped.get("annotation_label", "历史节点")) if mapped else "历史节点"
        annotation_reason = str(mapped.get("annotation_reason", "未找到直接对应的 CHANGELOG 记录。")) if mapped else "未找到直接对应的 CHANGELOG 记录。"
        is_milestone = bool((mapped or {}).get("is_milestone")) or fix_count >= 4
        nodes.append(
            {
                "branch": branch,
                "major_minor": group["major_minor"],
                "file_count": len(items),
                "fix_count": fix_count,
                "radius": _timeline_radius(fix_count),
                "versions": sorted(group["versions"], key=version_tuple, reverse=True),
                "latest_version": sorted(group["versions"], key=version_tuple, reverse=True)[0],
                "latest_modified": latest.get("modified", ""),
                "latest_modified_display": latest.get("modified_display", ""),
                "earliest_modified_display": earliest.get("modified_display", ""),
                "relative_path": latest.get("relative_path", ""),
                "is_milestone": is_milestone,
                "annotation_label": annotation_label,
                "annotation_reason": annotation_reason,
                "changelog_entry": mapped,
                "sort_date": sort_date,
                "kind_summary": _build_kind_summary(items),
            }
        )

    nodes.sort(
        key=lambda item: (
            item.get("sort_date", ""),
            version_tuple(str(item.get("latest_version", "0.0.0.0"))),
        ),
        reverse=False,
    )

    chart_width = max(900, 180 + len(nodes) * 120)
    baseline_y = 220
    top_y = 56
    usable_height = baseline_y - top_y
    max_fix_count = max((node["fix_count"] for node in nodes), default=1)
    label_stride = max(len(nodes) // 10, 1)
    for index, node in enumerate(nodes):
        x = 90 + index * 120
        y = baseline_y - ((usable_height * max(node["fix_count"], 1)) / max_fix_count)
        node["x"] = round(x, 2)
        node["y"] = round(y, 2)
        node["show_label"] = index % label_stride == 0 or index == len(nodes) - 1
        node["tooltip"] = {
            "branch": node["branch"],
            "fileCount": node["file_count"],
            "fixCount": node["fix_count"],
            "latestVersion": node["latest_version"],
            "kindSummary": node["kind_summary"],
            "annotationLabel": node["annotation_label"],
            "annotationReason": node["annotation_reason"],
            "changelogDate": mapped.get("date_display", "") if (mapped := node.get("changelog_entry")) else "",
            "changelogHighlights": list((mapped or {}).get("highlights", []))[:3],
        }

    highlighted_nodes = [node for node in nodes if node.get("is_milestone")]
    highlighted_nodes.sort(
        key=lambda item: (
            1 if item.get("changelog_entry") and item["changelog_entry"].get("is_milestone") else 0,
            item.get("fix_count", 0),
            version_tuple(str(item.get("latest_version", "0.0.0.0"))),
        ),
        reverse=True,
    )
    return {
        "history_file_count": len(archived_releases),
        "current_file_count": len(current_releases),
        "node_count": len(nodes),
        "max_fix_count": max_fix_count,
        "nodes": nodes,
        "highlighted_nodes": highlighted_nodes[:8],
        "svg": {
            "width": chart_width,
            "height": 280,
            "baseline_y": baseline_y,
            "top_y": top_y,
            "path": " ".join(
                ("M" if index == 0 else "L") + f" {node['x']} {node['y']}"
                for index, node in enumerate(nodes)
            ),
        },
        "label_stride": label_stride,
        "story": [
            f"历史文件共 {len(archived_releases)} 个，聚合为 {len(nodes)} 个版本节点（忽略 fix）。",
            f"每个节点的大小表示 fix 数量，当前最大 fix 数为 {max_fix_count}。",
        ] if nodes else [],
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

        try:
            _move_file_with_retry(source_path, target_path)
            moved.append(
                {
                    "from": source_path.name,
                    "to": target_path.relative_to(storage).as_posix(),
                }
            )
        except PermissionError as exc:
            if not _is_windows_file_lock_error(exc):
                raise
            _logger.warning(
                "Skip moving locked release file: %s -> %s",
                source_path,
                target_path,
            )
            continue

    if moved and on_changed:
        on_changed("History")
    return moved


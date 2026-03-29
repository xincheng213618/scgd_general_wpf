from __future__ import annotations

import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

_UPDATE_PACKAGE_RE = re.compile(r"^ColorVision-Update-\[(\d+\.\d+\.\d+\.\d+)]\.cvx$", re.IGNORECASE)


def parse_version_tuple(version: str) -> tuple[int, ...]:
    return tuple(int(part) for part in version.split("."))


def parse_update_package(path: Path, storage: Path | None = None) -> dict[str, Any] | None:
    match = _UPDATE_PACKAGE_RE.match(path.name)
    if not match or not path.is_file():
        return None

    version = match.group(1)
    version_parts = parse_version_tuple(version)
    try:
        stat = path.stat()
    except OSError:
        return None

    relative_path = path.name
    if storage is not None:
        try:
            relative_path = path.relative_to(storage).as_posix()
        except ValueError:
            relative_path = path.name

    return {
        "filename": path.name,
        "version": version,
        "version_tuple": version_parts,
        "branch": ".".join(str(part) for part in version_parts[:3]),
        "fix": version_parts[3],
        "size": stat.st_size,
        "modified": datetime.fromtimestamp(stat.st_mtime, tz=timezone.utc).isoformat(),
        "relative_path": relative_path,
        "path": path,
    }


def scan_update_packages(storage: Path) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    update_dir = storage / "Update"
    canonical: list[dict[str, Any]] = []
    others: list[dict[str, Any]] = []
    if not update_dir.is_dir():
        return canonical, others

    for entry in sorted(update_dir.iterdir(), key=lambda item: item.name.lower()):
        if entry.name.startswith("."):
            continue
        package = parse_update_package(entry, storage)
        if package:
            canonical.append(package)
        elif entry.is_file():
            stat = entry.stat()
            others.append(
                {
                    "filename": entry.name,
                    "relative_path": entry.relative_to(storage).as_posix(),
                    "size": stat.st_size,
                    "modified": datetime.fromtimestamp(stat.st_mtime, tz=timezone.utc).isoformat(),
                }
            )

    canonical.sort(key=lambda item: (item["version_tuple"], item["modified"]), reverse=True)
    others.sort(key=lambda item: item["modified"], reverse=True)
    return canonical, others


def determine_retained_update_filenames(packages: list[dict[str, Any]]) -> set[str]:
    if not packages:
        return set()

    retained = {packages[0]["filename"]}
    retained.update(package["filename"] for package in packages if package["fix"] == 1)
    return retained


def prune_update_packages(storage: Path) -> dict[str, Any]:
    canonical_packages, other_files = scan_update_packages(storage)
    retained_filenames = determine_retained_update_filenames(canonical_packages)
    deleted: list[str] = []

    for package in canonical_packages:
        if package["filename"] in retained_filenames:
            continue
        try:
            package["path"].unlink(missing_ok=True)
            deleted.append(package["filename"])
        except OSError:
            continue

    retained_packages = [
        package for package in canonical_packages if package["filename"] in retained_filenames
    ]
    retained_packages.sort(key=lambda item: (item["version_tuple"], item["modified"]), reverse=True)

    return {
        "retained": retained_packages,
        "deleted": deleted,
        "other_files": other_files,
    }


def build_update_summary(canonical_packages: list[dict[str, Any]], other_files: list[dict[str, Any]]) -> dict[str, Any]:
    retained_filenames = determine_retained_update_filenames(canonical_packages)
    retained_count = sum(1 for package in canonical_packages if package["filename"] in retained_filenames)
    return {
        "canonical_count": len(canonical_packages),
        "retained_count": retained_count,
        "other_file_count": len(other_files),
        "latest_version": canonical_packages[0]["version"] if canonical_packages else "",
    }



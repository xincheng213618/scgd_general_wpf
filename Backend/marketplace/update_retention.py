from __future__ import annotations

import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

_UPDATE_PACKAGE_RE = re.compile(r"^ColorVision-Update-\[(\d+\.\d+\.\d+\.\d+)]\.cvx$", re.IGNORECASE)


def parse_version_tuple(version: str) -> tuple[int, ...]:
    return tuple(int(part) for part in version.split("."))


def parse_update_filename(name: str) -> dict[str, Any] | None:
    match = _UPDATE_PACKAGE_RE.match(name)
    if not match:
        return None
    version = match.group(1)
    version_parts = parse_version_tuple(version)
    return {
        "filename": name,
        "version": version,
        "version_tuple": version_parts,
        "branch": ".".join(str(part) for part in version_parts[:3]),
        "fix": version_parts[3],
    }


def parse_update_package(path: Path, storage: Path | None = None) -> dict[str, Any] | None:
    parsed = parse_update_filename(path.name)
    if not parsed or not path.is_file():
        return None

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
        **parsed,
        "size": stat.st_size,
        "modified": datetime.fromtimestamp(stat.st_mtime, tz=timezone.utc).isoformat(),
        "relative_path": relative_path,
        "path": path,
    }


def scan_update_preview_fast(storage: Path, *, limit: int = 8) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    repair_update_storage_layout(storage)
    update_dir = storage / "Update"
    canonical_meta: list[dict[str, Any]] = []
    if not update_dir.is_dir():
        return [], build_update_summary([], [])

    for entry in update_dir.iterdir():
        if entry.name.startswith(".") or not entry.is_file():
            continue
        parsed = parse_update_filename(entry.name)
        if not parsed:
            continue
        canonical_meta.append({**parsed, "path": entry})

    canonical_meta.sort(key=lambda item: item["version_tuple"], reverse=True)
    retained_filenames = determine_retained_update_filenames(canonical_meta)
    preview_items: list[dict[str, Any]] = []
    for item in canonical_meta[:limit]:
        try:
            stat = item["path"].stat()
        except OSError:
            continue
        preview_items.append(
            {
                "filename": item["filename"],
                "version": item["version"],
                "version_tuple": item["version_tuple"],
                "branch": item["branch"],
                "fix": item["fix"],
                "size": stat.st_size,
                "modified": datetime.fromtimestamp(stat.st_mtime, tz=timezone.utc).isoformat(),
                "relative_path": item["path"].relative_to(storage).as_posix(),
            }
        )

    summary = {
        "canonical_count": len(canonical_meta),
        "retained_count": sum(1 for item in canonical_meta if item["filename"] in retained_filenames),
        "other_file_count": 0,
        "latest_version": canonical_meta[0]["version"] if canonical_meta else "",
    }
    return preview_items, summary


def repair_update_storage_layout(storage: Path) -> list[dict[str, str]]:
    legacy_update_dir = storage / "ColorVision" / "Update"
    canonical_update_dir = storage / "Update"
    moved: list[dict[str, str]] = []
    if not legacy_update_dir.is_dir():
        return moved

    canonical_update_dir.mkdir(parents=True, exist_ok=True)
    for entry in sorted(legacy_update_dir.iterdir(), key=lambda item: item.name.lower()):
        if not entry.is_file():
            continue

        target = canonical_update_dir / entry.name
        if target.exists():
            try:
                if target.stat().st_size == entry.stat().st_size:
                    entry.unlink(missing_ok=True)
                    moved.append(
                        {
                            "from": entry.relative_to(storage).as_posix(),
                            "to": target.relative_to(storage).as_posix(),
                        }
                    )
                    continue
            except OSError:
                continue

            stamp = datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S")
            target = canonical_update_dir / f"{entry.stem}-{stamp}{entry.suffix}"

        entry.replace(target)
        moved.append(
            {
                "from": entry.relative_to(storage).as_posix(),
                "to": target.relative_to(storage).as_posix(),
            }
        )

    try:
        if legacy_update_dir.exists() and not any(legacy_update_dir.iterdir()):
            legacy_update_dir.rmdir()
    except OSError:
        pass

    legacy_root = storage / "ColorVision"
    try:
        if legacy_root.exists() and not any(legacy_root.iterdir()):
            legacy_root.rmdir()
    except OSError:
        pass

    return moved


def scan_update_packages(storage: Path) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    repair_update_storage_layout(storage)
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



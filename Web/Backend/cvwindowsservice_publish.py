"""
CVWindowsService publish helpers.

Handles validation, filename generation, file saving, and LATEST_RELEASE
management for CVWindowsService tool packages.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable

# Official CVWindowsService filename pattern.
# Matches: CVWindowsService[4.0.6.522]-0522.zip, CVWindowsService[1.8.0.1107].zip
# Also matches .rar for historical scanning, but uploads only accept .zip.
CVWS_PACKAGE_RE = re.compile(
    r"^CVWindowsService\[(?P<version>\d+\.\d+\.\d+\.\d+)\](?:-(?P<suffix>\d+))?\.(?P<ext>zip|rar)$",
    re.IGNORECASE,
)

# Strict pattern for upload validation (zip only).
_CVWS_UPLOAD_RE = re.compile(
    r"^CVWindowsService\[(?P<version>\d+\.\d+\.\d+\.\d+)\](?:-(?P<suffix>\d+))?\.zip$",
    re.IGNORECASE,
)

_VERSION_RE = re.compile(r"^\d+\.\d+\.\d+\.\d+$")


class CVWSError(Exception):
    """Raised for CVWindowsService upload validation errors."""


@dataclass
class CVWSUploadResult:
    saved_filename: str
    saved_path: Path
    version: str
    latest_version: str


def validate_version(version: str) -> bool:
    """Check that version matches x.y.z.w numeric format."""
    return bool(_VERSION_RE.match(version)) and version == version.strip()


def is_official_filename(filename: str) -> bool:
    """Check if filename matches the official CVWindowsService naming convention."""
    return bool(_CVWS_UPLOAD_RE.match(filename))


def infer_version_from_filename(filename: str) -> str | None:
    """Extract version from an official CVWindowsService filename only.

    Only matches: CVWindowsService[x.y.z.w].zip or CVWindowsService[x.y.z.w]-suffix.zip
    Does NOT guess from arbitrary filenames containing version-like strings.
    """
    m = _CVWS_UPLOAD_RE.match(filename)
    if m:
        return m.group("version")
    return None


def choose_target_filename(
    version: str,
    target_dir: Path,
    *,
    original_filename: str | None = None,
) -> str:
    """Generate a target filename, adding a numeric suffix if conflicts exist.

    If original_filename matches the official pattern, preserves it (e.g. CVWindowsService[4.0.6.522]-0522.zip).
    Otherwise generates canonical CVWindowsService[version].zip.
    Never overwrites existing files; appends -1, -2, etc. on conflict.
    """
    # If the original filename is official, try to use it as-is
    if original_filename and is_official_filename(original_filename):
        candidate = original_filename
        if not (target_dir / candidate).exists():
            return candidate
        # Original name conflicts — fall through to generate suffixed name

    base = f"CVWindowsService[{version}].zip"

    # Check for any existing files with this version
    has_conflict = False
    max_suffix = 0
    for entry in target_dir.iterdir():
        if not entry.is_file():
            continue
        m = CVWS_PACKAGE_RE.match(entry.name)
        if m and m.group("version") == version:
            has_conflict = True
            suffix_str = m.group("suffix")
            if suffix_str:
                max_suffix = max(max_suffix, int(suffix_str))
            else:
                max_suffix = max(max_suffix, 0)

    if not has_conflict:
        return base

    return f"CVWindowsService[{version}]-{max_suffix + 1}.zip"


def save_cvws_package(
    file_storage: Any,
    target_dir: Path,
    version: str,
    *,
    original_filename: str | None = None,
) -> CVWSUploadResult:
    """Save an uploaded file to the CVWindowsService directory.

    Does NOT automatically update LATEST_RELEASE — caller decides.
    """
    target_dir.mkdir(parents=True, exist_ok=True)
    filename = choose_target_filename(version, target_dir, original_filename=original_filename)
    saved_path = target_dir / filename
    file_storage.save(str(saved_path))

    # Read current latest
    latest_path = target_dir / "LATEST_RELEASE"
    latest_version = ""
    try:
        if latest_path.exists():
            latest_version = latest_path.read_text(encoding="utf-8").strip()
    except OSError:
        pass

    return CVWSUploadResult(
        saved_filename=filename,
        saved_path=saved_path,
        version=version,
        latest_version=latest_version,
    )


def update_cvws_latest_release(
    target_dir: Path,
    version: str,
) -> None:
    """Write version to Tool/CVWindowsService/LATEST_RELEASE."""
    latest_path = target_dir / "LATEST_RELEASE"
    latest_path.write_text(version, encoding="utf-8")


def build_cvws_page_context(
    storage: Path,
    *,
    scan_packages: Callable[[], list[dict[str, Any]]],
    read_text_file: Callable[[Path], str | None],
    human_size: Callable[[int], str],
    message: str | None = None,
    error: str | None = None,
    result: CVWSUploadResult | None = None,
) -> dict[str, Any]:
    """Build template context for the CVWindowsService upload page."""
    tool_dir = storage / "Tool" / "CVWindowsService"
    latest_version = ""
    if tool_dir.is_dir():
        latest_version = (read_text_file(tool_dir / "LATEST_RELEASE") or "").strip()

    packages = scan_packages()

    return {
        "latest_version": latest_version,
        "packages": packages,
        "package_count": len(packages),
        "tool_dir_display": "Tool/CVWindowsService",
        "message": message,
        "error": error,
        "result": result,
        "human_size": human_size,
    }

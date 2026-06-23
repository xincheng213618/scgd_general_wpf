from __future__ import annotations

import re
from pathlib import Path, PurePosixPath
from typing import Callable

from flask import abort


_SAFE_ID_RE = re.compile(r"^[A-Za-z0-9_\-]+$")
_SAFE_VERSION_RE = re.compile(r"^[0-9]+(\.[0-9]+)*$")


def is_safe_id(value: str) -> bool:
    return bool(value) and _SAFE_ID_RE.match(value) is not None


def is_safe_version(value: str) -> bool:
    return bool(value) and _SAFE_VERSION_RE.match(value) is not None


def sanitize_filename(filename: str) -> str:
    name = Path(filename).name
    return re.sub(r'[/\\:*?"<>|]', "_", name)


def normalize_relative_path(relative_path: str) -> str:
    normalized_input = (relative_path or "").replace("\\", "/").strip()
    if not normalized_input:
        return ""

    pure_path = PurePosixPath(normalized_input)
    if pure_path.is_absolute() or ":" in normalized_input:
        abort(403)

    parts: list[str] = []
    for part in pure_path.parts:
        if part in ("", "."):
            continue
        if part == "..":
            abort(403)
        parts.append(part)
    return "/".join(parts)


def storage_target(storage: Path, relative_path: str) -> Path:
    normalized = normalize_relative_path(relative_path)
    return storage / Path(*PurePosixPath(normalized).parts)


def resolve_storage_file(
    storage: Path,
    relative_path: str,
    *,
    repair_updates: Callable[[Path], object] | None = None,
) -> Path:
    normalized = normalize_relative_path(relative_path)
    target = storage / Path(*PurePosixPath(normalized).parts)
    if normalized.startswith("Update/") and not target.exists() and repair_updates is not None:
        repair_updates(storage)
        target = storage / Path(*PurePosixPath(normalized).parts)

    try:
        target.resolve().relative_to(storage.resolve())
    except ValueError:
        abort(403)

    if not target.exists() or not target.is_file():
        abort(404)
    return target


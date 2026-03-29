from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Callable, IO


@dataclass(frozen=True)
class LegacyUploadResult:
    normalized_path: str
    target: Path
    plugin_id: str | None
    bytes_written: int


class UploadWorkflowError(Exception):
    def __init__(self, message: str, status_code: int):
        super().__init__(message)
        self.message = message
        self.status_code = status_code


class UploadTooLargeError(UploadWorkflowError):
    pass


def _normalize_upload_filepath(raw_filepath: str) -> str:
    normalized = (raw_filepath or "").replace("\\", "/").strip()
    if normalized.lower().startswith("colorvision/"):
        return normalized[len("ColorVision/") :]
    return normalized


def _resolve_upload_target(storage: Path, normalized_path: str) -> Path:
    return storage / Path(*PurePosixPath(normalized_path).parts)


def store_legacy_upload(
    *,
    storage: Path,
    raw_filepath: str,
    stream: IO[bytes],
    max_size: int,
    normalize_relative_path: Callable[[str], str],
    validate_plugin_id: Callable[[str], bool],
    extract_package_version: Callable[[str, str], str | None],
    is_root_release_file: Callable[[Path], bool],
    reconcile_app_release_history: Callable[[], list[dict[str, str]]],
    reconcile_plugin_package_history: Callable[[str], list[dict[str, str]]],
    prune_update_packages: Callable[[Path], object],
    refresh_related_caches: Callable[..., None],
) -> LegacyUploadResult:
    normalized = normalize_relative_path(_normalize_upload_filepath(raw_filepath))
    if not normalized:
        raise UploadWorkflowError("Upload path is required", 400)

    target = _resolve_upload_target(storage, normalized)
    try:
        target.resolve().relative_to(storage.resolve())
    except ValueError as exc:
        raise UploadWorkflowError("Forbidden upload path", 403) from exc

    if ".." in str(target):
        raise UploadWorkflowError("Forbidden upload path", 403)

    parts = PurePosixPath(normalized).parts
    plugin_id = parts[1] if len(parts) >= 2 and parts[0] == "Plugins" else None
    is_plugin_package = bool(plugin_id and target.suffix.lower() == ".cvxp")
    if plugin_id and not validate_plugin_id(plugin_id):
        raise UploadWorkflowError("Invalid plugin_id in upload path", 400)
    if is_plugin_package:
        version = extract_package_version(target.name, plugin_id)
        if not version:
            raise UploadWorkflowError("Invalid plugin package filename", 400)

    target.parent.mkdir(parents=True, exist_ok=True)
    total = 0
    with open(target, "wb") as file_handle:
        while True:
            chunk = stream.read(8192)
            if not chunk:
                break
            total += len(chunk)
            if total > max_size:
                file_handle.close()
                target.unlink(missing_ok=True)
                raise UploadTooLargeError("File too large", 413)
            file_handle.write(chunk)

    if is_root_release_file(target):
        reconcile_app_release_history()
    if is_plugin_package:
        reconcile_plugin_package_history(plugin_id)
    if parts and parts[0] == "Update":
        prune_update_packages(storage)
    refresh_related_caches(
        plugin_id=plugin_id,
        relative_path=normalized,
    )

    return LegacyUploadResult(
        normalized_path=normalized,
        target=target,
        plugin_id=plugin_id,
        bytes_written=total,
    )



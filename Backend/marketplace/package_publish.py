from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable


@dataclass(frozen=True)
class PackageUploadRequest:
    plugin_id: str
    version: str
    safe_filename: str


@dataclass(frozen=True)
class PackageSaveResult:
    plugin_dir: Path
    save_path: Path
    moved_packages: list[dict[str, str]]


class PackageValidationError(ValueError):
    pass


def infer_plugin_id_from_filename(filename: str) -> str | None:
    match = re.match(r"^(.+?)-[\d.]+\.cvxp$", filename)
    return match.group(1) if match else None


def extract_package_version(
    filename: str,
    plugin_id: str,
    *,
    sanitize_filename: Callable[[str], str],
    validate_version: Callable[[str], bool],
) -> str | None:
    safe_filename = sanitize_filename(filename)
    if safe_filename != filename or not safe_filename.lower().endswith(".cvxp"):
        return None

    match = re.match(
        rf"^{re.escape(plugin_id)}-([0-9]+(?:\.[0-9]+)*)\.cvxp$",
        safe_filename,
        re.IGNORECASE,
    )
    if not match:
        return None
    version = match.group(1)
    return version if validate_version(version) else None


def ensure_plugin_dir(
    storage: Path,
    plugin_id: str,
    *,
    validate_plugin_id: Callable[[str], bool],
) -> Path:
    if not validate_plugin_id(plugin_id):
        raise PackageValidationError("PluginId contains invalid characters")
    plugin_dir = storage / "Plugins" / plugin_id
    plugin_dir.mkdir(parents=True, exist_ok=True)
    return plugin_dir


def load_manifest(manifest_path: Path) -> dict[str, Any]:
    if not manifest_path.exists():
        return {}
    try:
        with open(manifest_path, encoding="utf-8") as file_handle:
            return json.load(file_handle)
    except (json.JSONDecodeError, OSError):
        return {}


def update_latest_release_file(
    plugin_dir: Path,
    version: str,
    *,
    read_text_file: Callable[[Path], str | None],
    version_tuple: Callable[[str], tuple],
) -> None:
    latest_path = plugin_dir / "LATEST_RELEASE"
    existing = read_text_file(latest_path) or "0.0.0.0"
    try:
        if version_tuple(version) >= version_tuple(existing):
            latest_path.write_text(version, encoding="utf-8")
    except ValueError:
        latest_path.write_text(version, encoding="utf-8")


def validate_html_upload_request(
    package_file: Any,
    plugin_id: str,
    *,
    sanitize_filename: Callable[[str], str],
    validate_plugin_id: Callable[[str], bool],
    validate_version: Callable[[str], bool],
) -> PackageUploadRequest:
    if not package_file or not getattr(package_file, "filename", ""):
        raise PackageValidationError("请选择要上传的文件")

    resolved_plugin_id = plugin_id.strip()
    if not resolved_plugin_id:
        inferred_plugin_id = infer_plugin_id_from_filename(package_file.filename)
        if not inferred_plugin_id:
            raise PackageValidationError("请填写插件 ID 或使用标准文件名格式")
        resolved_plugin_id = inferred_plugin_id

    if not validate_plugin_id(resolved_plugin_id):
        raise PackageValidationError("插件 ID 只能包含字母、数字、下划线和连字符")

    safe_filename = sanitize_filename(package_file.filename)
    version = extract_package_version(
        safe_filename,
        resolved_plugin_id,
        sanitize_filename=sanitize_filename,
        validate_version=validate_version,
    )
    if not safe_filename or not version:
        raise PackageValidationError("文件名必须为 {PluginId}-{version}.cvxp，且版本只能包含数字和点")

    return PackageUploadRequest(
        plugin_id=resolved_plugin_id,
        version=version,
        safe_filename=safe_filename,
    )


def validate_api_publish_request(
    package_file: Any,
    plugin_id: str,
    version: str,
    *,
    sanitize_filename: Callable[[str], str],
    validate_plugin_id: Callable[[str], bool],
    validate_version: Callable[[str], bool],
) -> PackageUploadRequest:
    if not package_file or not getattr(package_file, "filename", ""):
        raise PackageValidationError("Package file is required")
    if not plugin_id:
        raise PackageValidationError("PluginId is required")
    if not version:
        raise PackageValidationError("Version is required")
    if not validate_plugin_id(plugin_id):
        raise PackageValidationError("PluginId contains invalid characters")
    if not validate_version(version):
        raise PackageValidationError("Version must be digits separated by dots")
    if not sanitize_filename(package_file.filename).lower().endswith(".cvxp"):
        raise PackageValidationError("Package file must have a .cvxp extension")

    return PackageUploadRequest(
        plugin_id=plugin_id,
        version=version,
        safe_filename=f"{plugin_id}-{version}.cvxp",
    )


def save_package_file(
    storage: Path,
    package_file: Any,
    upload_request: PackageUploadRequest,
    *,
    validate_plugin_id: Callable[[str], bool],
    read_text_file: Callable[[Path], str | None],
    version_tuple: Callable[[str], tuple],
    reconcile_plugin_package_history: Callable[[str], list[dict[str, str]]],
) -> PackageSaveResult:
    plugin_dir = ensure_plugin_dir(
        storage,
        upload_request.plugin_id,
        validate_plugin_id=validate_plugin_id,
    )
    save_path = plugin_dir / upload_request.safe_filename
    package_file.save(str(save_path))
    update_latest_release_file(
        plugin_dir,
        upload_request.version,
        read_text_file=read_text_file,
        version_tuple=version_tuple,
    )
    moved_packages = reconcile_plugin_package_history(upload_request.plugin_id)
    return PackageSaveResult(
        plugin_dir=plugin_dir,
        save_path=save_path,
        moved_packages=moved_packages,
    )


def persist_plugin_metadata(
    plugin_dir: Path,
    *,
    plugin_id: str,
    version: str,
    name: str,
    description: str = "",
    author: str = "",
    category: str = "",
    requires_version: str = "",
    changelog_text: str = "",
    icon_file: Any = None,
    manifest_loader: Callable[[Path], dict[str, Any]] = load_manifest,
) -> None:
    manifest_path = plugin_dir / "manifest.json"
    manifest = manifest_loader(manifest_path)

    manifest["id"] = plugin_id
    manifest["name"] = name or plugin_id
    if description:
        manifest["description"] = description
    if author:
        manifest["author"] = author
    if category:
        manifest["category"] = category
    if requires_version:
        manifest["requires"] = requires_version
    manifest["version"] = version

    with open(manifest_path, "w", encoding="utf-8") as file_handle:
        json.dump(manifest, file_handle, indent=2, ensure_ascii=False)

    if changelog_text:
        (plugin_dir / "CHANGELOG.md").write_text(changelog_text, encoding="utf-8")

    if icon_file and getattr(icon_file, "filename", ""):
        icon_file.save(str(plugin_dir / "PackageIcon.png"))


def finalize_plugin_publish(
    storage: Path,
    *,
    plugin_id: str,
    version: str,
    refresh_related_caches: Callable[..., None],
    prewarm_plugin_metadata: Callable[..., None],
    get_download_counts: Callable[[], dict[str, int]],
    get_cache_entry: Callable[..., dict[str, Any] | None],
    set_cache_entry: Callable[..., None],
    ttl_seconds: int,
) -> None:
    refresh_related_caches(plugin_id=plugin_id, relative_path=f"Plugins/{plugin_id}")
    prewarm_plugin_metadata(
        storage,
        plugin_id,
        version,
        download_counts=get_download_counts(),
        get_cache_entry=get_cache_entry,
        set_cache_entry=set_cache_entry,
        ttl_seconds=ttl_seconds,
    )



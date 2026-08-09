"""Signed standalone Spectrum release storage and validation.

The on-disk contract deliberately stays independent from ColorVision's main
application updater.  Every release is immutable and self-contained under
``storage/Spectrum/releases/<version>``; ``LATEST_RELEASE`` is only advanced
after the release directory has been committed.
"""

from __future__ import annotations

import base64
import hashlib
import json
import os
import re
import stat
import tempfile
import threading
import uuid
import zipfile
from dataclasses import dataclass
from datetime import datetime, timedelta
from functools import lru_cache
from pathlib import Path
from typing import Any, BinaryIO

from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding, rsa


SPECTRUM_PUBLIC_KEY_SPKI_BASE64 = (
    "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA4gY405ZempwK2pWckyGjsSyoQKoE/"
    "HYkWzl83sylcObMxPRP4tBugwOxYUjiO05Cw9Bhj00/sTKXLpcUVpVper9s6l7LopF6IB1u"
    "bbrcEvKjSqvomyaaP7Wtc7eEI3H5qWKtK+GB9Y0wAQ3VtHp6yuK7x06MGRQrW6cRg+yqRd"
    "06NWHjNjCMZq0EmoGLKydTlRO66dJkddKCxnemyfS/w8ikni0xexeVp0nOSHDBYL/tkUz5E"
    "s3q75GOgcLbge5K1xE234BHn3lmL8Fewu7WsVHQAvxP5+pENPxFVAMUuIYvQj0r+NXcu3f"
    "3oiKrkBbGTHUV/Y/lgdVdv36/4NTLPQIDAQAB"
)

VERSION_RE = re.compile(r"^\d{1,5}(?:\.\d{1,5}){3}$")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
MAX_MANIFEST_BYTES = 256 * 1024
MAX_SIGNATURE_BYTES = 16 * 1024
MAX_ZIP_ENTRIES = 100_000
MAX_UNCOMPRESSED_BYTES = 4 * 1024 * 1024 * 1024
_EXPECTED_MANIFEST_KEYS = {
    "schemaVersion",
    "productId",
    "version",
    "publishedAtUtc",
    "releaseNotes",
    "package",
}
_EXPECTED_PACKAGE_KEYS = {"fileName", "size", "sha256"}
_WINDOWS_RESERVED_NAMES = {
    "CON", "PRN", "AUX", "NUL",
    *(f"COM{index}" for index in range(1, 10)),
    *(f"LPT{index}" for index in range(1, 10)),
}
_PUBLISH_LOCK = threading.Lock()


class SpectrumReleaseError(ValueError):
    """A client-supplied release failed validation."""


class SpectrumReleaseConflict(SpectrumReleaseError):
    """The requested immutable version conflicts with existing state."""


class SpectrumReleaseNotFound(SpectrumReleaseError):
    """A requested signed release does not exist."""


@dataclass(frozen=True)
class StoredSpectrumRelease:
    manifest: dict[str, Any]
    manifest_bytes: bytes
    signature_bytes: bytes
    package_path: Path


@dataclass(frozen=True)
class SpectrumPublishResult:
    created: bool
    release: StoredSpectrumRelease


def is_spectrum_version(value: str) -> bool:
    if not isinstance(value, str) or VERSION_RE.fullmatch(value) is None:
        return False
    return all(int(component) <= 65535 for component in value.split("."))


def version_key(value: str) -> tuple[int, int, int, int]:
    if not is_spectrum_version(value):
        raise SpectrumReleaseError("Version must contain four numeric components")
    return tuple(int(component) for component in value.split("."))  # type: ignore[return-value]


def _reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise SpectrumReleaseError(f"Manifest contains duplicate JSON key: {key}")
        result[key] = value
    return result


def _reject_json_constant(value: str):
    raise SpectrumReleaseError(f"Manifest contains invalid JSON constant: {value}")


def canonical_manifest_bytes(manifest: dict[str, Any]) -> bytes:
    return json.dumps(
        manifest,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")


def parse_and_validate_manifest(
    manifest_bytes: bytes,
    *,
    expected_version: str | None = None,
    expected_release_notes: str | None = None,
) -> dict[str, Any]:
    if not manifest_bytes or len(manifest_bytes) > MAX_MANIFEST_BYTES:
        raise SpectrumReleaseError("Manifest is empty or too large")
    try:
        manifest_text = manifest_bytes.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise SpectrumReleaseError("Manifest must be valid UTF-8") from exc
    try:
        manifest = json.loads(
            manifest_text,
            object_pairs_hook=_reject_duplicate_keys,
            parse_constant=_reject_json_constant,
        )
    except SpectrumReleaseError:
        raise
    except (TypeError, ValueError, json.JSONDecodeError) as exc:
        raise SpectrumReleaseError("Manifest must be valid JSON") from exc

    if not isinstance(manifest, dict):
        raise SpectrumReleaseError("Manifest root must be a JSON object")
    if set(manifest) != _EXPECTED_MANIFEST_KEYS:
        raise SpectrumReleaseError("Manifest fields do not match schema version 1")
    if type(manifest["schemaVersion"]) is not int or manifest["schemaVersion"] != 1:
        raise SpectrumReleaseError("Manifest schemaVersion must be 1")
    if manifest["productId"] != "Spectrum":
        raise SpectrumReleaseError("Manifest productId must be Spectrum")

    version = manifest["version"]
    if not is_spectrum_version(version):
        raise SpectrumReleaseError("Manifest version must contain four numeric components")
    if expected_version is not None and version != expected_version:
        raise SpectrumReleaseError("Version form field does not match the signed manifest")

    published_at = manifest["publishedAtUtc"]
    if not isinstance(published_at, str) or not published_at:
        raise SpectrumReleaseError("Manifest publishedAtUtc must be a UTC timestamp")
    try:
        parsed_timestamp = datetime.fromisoformat(published_at.replace("Z", "+00:00"))
    except ValueError as exc:
        raise SpectrumReleaseError("Manifest publishedAtUtc must be a UTC timestamp") from exc
    if parsed_timestamp.tzinfo is None or parsed_timestamp.utcoffset() != timedelta(0):
        raise SpectrumReleaseError("Manifest publishedAtUtc must use UTC")

    release_notes = manifest["releaseNotes"]
    if not isinstance(release_notes, str):
        raise SpectrumReleaseError("Manifest releaseNotes must be a string")
    if expected_release_notes is not None and release_notes != expected_release_notes:
        raise SpectrumReleaseError("ReleaseNotes form field does not match the signed manifest")

    package = manifest["package"]
    if not isinstance(package, dict) or set(package) != _EXPECTED_PACKAGE_KEYS:
        raise SpectrumReleaseError("Manifest package fields do not match schema version 1")
    file_name = package["fileName"]
    allowed_names = {f"Spectrum{version}.zip", f"Spectrum-{version}.zip"}
    if not isinstance(file_name, str) or file_name not in allowed_names:
        raise SpectrumReleaseError("Manifest package.fileName must match the Spectrum version")
    if type(package["size"]) is not int or package["size"] <= 0:
        raise SpectrumReleaseError("Manifest package.size must be a positive integer")
    if not isinstance(package["sha256"], str) or SHA256_RE.fullmatch(package["sha256"]) is None:
        raise SpectrumReleaseError("Manifest package.sha256 must be 64 lowercase hexadecimal characters")

    if canonical_manifest_bytes(manifest) != manifest_bytes:
        raise SpectrumReleaseError("Manifest JSON is not in canonical form")
    return manifest


@lru_cache(maxsize=1)
def _load_public_key() -> rsa.RSAPublicKey:
    try:
        encoded = base64.b64decode(SPECTRUM_PUBLIC_KEY_SPKI_BASE64, validate=True)
        public_key = serialization.load_der_public_key(encoded)
    except (TypeError, ValueError) as exc:
        raise RuntimeError("The configured Spectrum public key is invalid") from exc
    if not isinstance(public_key, rsa.RSAPublicKey):
        raise RuntimeError("The configured Spectrum public key is not RSA")
    return public_key


def verify_manifest_signature(manifest_bytes: bytes, signature_bytes: bytes) -> None:
    if not signature_bytes or len(signature_bytes) > MAX_SIGNATURE_BYTES:
        raise SpectrumReleaseError("Signature is empty or too large")
    try:
        _load_public_key().verify(
            signature_bytes,
            manifest_bytes,
            padding.PKCS1v15(),
            hashes.SHA256(),
        )
    except InvalidSignature as exc:
        raise SpectrumReleaseError("Manifest signature is invalid") from exc


def _safe_uploaded_name(value: str) -> str:
    return value.replace("\\", "/").rsplit("/", 1)[-1]


def _validate_zip_member(name: str) -> str:
    if not name or "\x00" in name:
        raise SpectrumReleaseError("ZIP contains an invalid member name")
    normalized = name.replace("\\", "/")
    if normalized.startswith("/") or re.match(r"^[A-Za-z]:", normalized):
        raise SpectrumReleaseError(f"ZIP contains an absolute path: {name}")

    is_directory = normalized.endswith("/")
    if is_directory:
        normalized = normalized[:-1]
    parts = normalized.split("/")
    if not normalized or any(part in ("", ".", "..") for part in parts):
        raise SpectrumReleaseError(f"ZIP contains an unsafe path: {name}")
    for part in parts:
        if part.endswith((" ", ".")) or ":" in part:
            raise SpectrumReleaseError(f"ZIP contains a Windows-unsafe path: {name}")
        reserved_stem = part.split(".", 1)[0].upper()
        if reserved_stem in _WINDOWS_RESERVED_NAMES:
            raise SpectrumReleaseError(f"ZIP contains a reserved Windows path: {name}")
    return "/".join(parts) + ("/" if is_directory else "")


def validate_spectrum_zip(package_path: Path) -> None:
    required_root_files = {
        "Spectrum.exe",
        "Spectrum.dll",
        "Spectrum.deps.json",
        "Spectrum.runtimeconfig.json",
    }
    try:
        with zipfile.ZipFile(package_path, "r") as archive:
            members = archive.infolist()
            if not members or len(members) > MAX_ZIP_ENTRIES:
                raise SpectrumReleaseError("ZIP is empty or contains too many entries")

            normalized_names: set[str] = set()
            root_files: set[str] = set()
            total_uncompressed = 0
            for member in members:
                normalized = _validate_zip_member(member.filename)
                collision_key = normalized.rstrip("/").casefold()
                if collision_key in normalized_names:
                    raise SpectrumReleaseError(f"ZIP contains a duplicate path: {member.filename}")
                normalized_names.add(collision_key)

                mode = member.external_attr >> 16
                if mode and stat.S_ISLNK(mode):
                    raise SpectrumReleaseError(f"ZIP contains a symbolic link: {member.filename}")
                if member.flag_bits & 0x1:
                    raise SpectrumReleaseError(f"ZIP contains an encrypted entry: {member.filename}")
                if member.file_size < 0:
                    raise SpectrumReleaseError("ZIP contains an invalid entry size")
                total_uncompressed += member.file_size
                if total_uncompressed > MAX_UNCOMPRESSED_BYTES:
                    raise SpectrumReleaseError("ZIP expands beyond the allowed size")
                if not member.is_dir() and normalized == "Spectrum.exe":
                    root_files.add(normalized)
                elif not member.is_dir() and normalized in required_root_files:
                    root_files.add(normalized)

            if "Spectrum.exe" not in root_files:
                raise SpectrumReleaseError("ZIP must contain Spectrum.exe at its root")
            missing = sorted(required_root_files - root_files)
            if missing:
                raise SpectrumReleaseError(
                    f"ZIP is missing required root files: {', '.join(missing)}"
                )
            bad_member = archive.testzip()
            if bad_member is not None:
                raise SpectrumReleaseError(f"ZIP CRC check failed: {bad_member}")
    except SpectrumReleaseError:
        raise
    except (OSError, RuntimeError, NotImplementedError, zipfile.BadZipFile) as exc:
        raise SpectrumReleaseError("Package is not a valid, readable ZIP archive") from exc


def _read_file_limited(path: Path, limit: int, label: str) -> bytes:
    try:
        size = path.stat().st_size
    except OSError as exc:
        raise SpectrumReleaseNotFound(f"{label} not found") from exc
    if size <= 0 or size > limit:
        raise SpectrumReleaseError(f"Stored {label} is empty or too large")
    try:
        return path.read_bytes()
    except OSError as exc:
        raise SpectrumReleaseNotFound(f"{label} not found") from exc


def _release_root(storage: Path, version: str) -> Path:
    return storage / "Spectrum" / "releases" / version


def load_spectrum_release(storage: Path, version: str) -> StoredSpectrumRelease:
    if not is_spectrum_version(version):
        raise SpectrumReleaseError("Invalid Spectrum version")
    release_root = _release_root(storage, version)
    if not release_root.is_dir():
        raise SpectrumReleaseNotFound(f"Spectrum release {version} not found")

    manifest_bytes = _read_file_limited(release_root / "manifest.json", MAX_MANIFEST_BYTES, "manifest")
    signature_bytes = _read_file_limited(release_root / "manifest.sig", MAX_SIGNATURE_BYTES, "signature")
    manifest = parse_and_validate_manifest(manifest_bytes, expected_version=version)
    verify_manifest_signature(manifest_bytes, signature_bytes)

    package_path = release_root / manifest["package"]["fileName"]
    try:
        package_size = package_path.stat().st_size
    except OSError as exc:
        raise SpectrumReleaseNotFound(f"Spectrum package for {version} not found") from exc
    if package_size != manifest["package"]["size"]:
        raise SpectrumReleaseError(f"Stored Spectrum package size does not match manifest for {version}")
    return StoredSpectrumRelease(manifest, manifest_bytes, signature_bytes, package_path)


def read_latest_version(storage: Path) -> str | None:
    pointer = storage / "Spectrum" / "LATEST_RELEASE"
    if not pointer.exists():
        return None
    try:
        value = pointer.read_text(encoding="utf-8").strip()
    except (OSError, UnicodeDecodeError) as exc:
        raise SpectrumReleaseError("Spectrum latest release pointer is unreadable") from exc
    if not is_spectrum_version(value):
        raise SpectrumReleaseError("Spectrum latest release pointer is invalid")
    return value


def load_latest_spectrum_release(storage: Path) -> StoredSpectrumRelease:
    version = read_latest_version(storage)
    if version is None:
        raise SpectrumReleaseNotFound("No signed Spectrum release has been published")
    return load_spectrum_release(storage, version)


def spectrum_latest_payload(release: StoredSpectrumRelease) -> dict[str, str]:
    return {
        "manifestBase64": base64.b64encode(release.manifest_bytes).decode("ascii"),
        "signatureBase64": base64.b64encode(release.signature_bytes).decode("ascii"),
    }


def spectrum_release_payload(release: StoredSpectrumRelease) -> dict[str, Any]:
    manifest = release.manifest
    package = manifest["package"]
    version = manifest["version"]
    return {
        "version": version,
        "publishedAtUtc": manifest["publishedAtUtc"],
        "releaseNotes": manifest["releaseNotes"],
        "fileName": package["fileName"],
        "size": package["size"],
        "sha256": package["sha256"],
        "downloadUrl": f"/api/tool/spectrum/download/{version}",
    }


def list_spectrum_releases(storage: Path) -> list[dict[str, Any]]:
    releases_root = storage / "Spectrum" / "releases"
    if not releases_root.is_dir():
        return []
    releases: list[dict[str, Any]] = []
    for entry in releases_root.iterdir():
        if not entry.is_dir() or not is_spectrum_version(entry.name):
            continue
        try:
            releases.append(spectrum_release_payload(load_spectrum_release(storage, entry.name)))
        except (OSError, SpectrumReleaseError):
            continue
    releases.sort(key=lambda item: version_key(item["version"]), reverse=True)
    return releases


def build_spectrum_tool_card(storage: Path) -> dict[str, Any]:
    latest = None
    try:
        latest = spectrum_release_payload(load_latest_spectrum_release(storage))
    except (OSError, SpectrumReleaseError):
        pass
    return {
        "productId": "Spectrum",
        "name": "Spectrum 光谱分析软件",
        "description": "独立光谱采集与分析软件，无需安装 ColorVision 主程序。",
        "latest": latest,
        "browseUrl": "/browse/Spectrum",
    }


def _hash_file(path: Path) -> tuple[int, str]:
    digest = hashlib.sha256()
    size = 0
    with path.open("rb") as stream:
        while True:
            chunk = stream.read(1024 * 1024)
            if not chunk:
                break
            size += len(chunk)
            digest.update(chunk)
    return size, digest.hexdigest()


def _stage_uploaded_package(product_root: Path, package_stream: BinaryIO) -> tuple[Path, int, str]:
    descriptor, temp_name = tempfile.mkstemp(prefix=".spectrum-upload-", suffix=".zip", dir=product_root)
    temp_path = Path(temp_name)
    digest = hashlib.sha256()
    size = 0
    try:
        with os.fdopen(descriptor, "wb") as destination:
            while True:
                chunk = package_stream.read(1024 * 1024)
                if not chunk:
                    break
                size += len(chunk)
                digest.update(chunk)
                destination.write(chunk)
            destination.flush()
            os.fsync(destination.fileno())
        return temp_path, size, digest.hexdigest()
    except Exception:
        try:
            os.close(descriptor)
        except OSError:
            pass
        temp_path.unlink(missing_ok=True)
        raise


def _write_bytes_durable(path: Path, data: bytes) -> None:
    with path.open("xb") as stream:
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())


def _write_latest_pointer(product_root: Path, version: str) -> None:
    pointer = product_root / "LATEST_RELEASE"
    temp_pointer = product_root / f".LATEST_RELEASE-{uuid.uuid4().hex}.tmp"
    try:
        _write_bytes_durable(temp_pointer, version.encode("utf-8"))
        os.replace(temp_pointer, pointer)
    finally:
        temp_pointer.unlink(missing_ok=True)


def _cleanup_staging_directory(staging: Path | None) -> None:
    if staging is None or not staging.exists():
        return
    for child in staging.iterdir():
        if child.is_file():
            child.unlink(missing_ok=True)
    staging.rmdir()


def publish_spectrum_release(
    storage: Path,
    *,
    version: str,
    release_notes: str,
    manifest_bytes: bytes,
    signature_bytes: bytes,
    package_stream: BinaryIO,
    package_filename: str,
) -> SpectrumPublishResult:
    manifest = parse_and_validate_manifest(
        manifest_bytes,
        expected_version=version,
        expected_release_notes=release_notes,
    )
    verify_manifest_signature(manifest_bytes, signature_bytes)
    package = manifest["package"]
    if _safe_uploaded_name(package_filename) != package["fileName"]:
        raise SpectrumReleaseError("Uploaded package filename does not match the signed manifest")

    product_root = storage / "Spectrum"
    releases_root = product_root / "releases"
    releases_root.mkdir(parents=True, exist_ok=True)
    staged_package: Path | None = None
    staging_directory: Path | None = None
    try:
        staged_package, staged_size, staged_sha256 = _stage_uploaded_package(product_root, package_stream)
        if staged_size != package["size"]:
            raise SpectrumReleaseError("Uploaded package size does not match the signed manifest")
        if staged_sha256 != package["sha256"]:
            raise SpectrumReleaseError("Uploaded package SHA-256 does not match the signed manifest")
        validate_spectrum_zip(staged_package)

        with _PUBLISH_LOCK:
            current = read_latest_version(storage)
            if current is not None and version_key(version) < version_key(current):
                raise SpectrumReleaseConflict(
                    f"Spectrum version {version} is older than current latest version {current}"
                )

            target_root = releases_root / version
            if target_root.exists():
                try:
                    existing = load_spectrum_release(storage, version)
                    existing_size, existing_sha256 = _hash_file(existing.package_path)
                except (OSError, SpectrumReleaseError) as exc:
                    raise SpectrumReleaseConflict(
                        f"Spectrum version {version} already exists but is incomplete or invalid"
                    ) from exc
                if (
                    existing.manifest["package"] == package
                    and existing_size == staged_size
                    and existing_sha256 == staged_sha256
                ):
                    existing_identity = dict(existing.manifest)
                    incoming_identity = dict(manifest)
                    existing_identity.pop("publishedAtUtc", None)
                    incoming_identity.pop("publishedAtUtc", None)
                    if existing_identity != incoming_identity:
                        raise SpectrumReleaseConflict(
                            f"Spectrum version {version} already exists with different signed metadata"
                        )
                    if current is None or version_key(version) > version_key(current):
                        _write_latest_pointer(product_root, version)
                    return SpectrumPublishResult(created=False, release=existing)
                raise SpectrumReleaseConflict(
                    f"Spectrum version {version} already exists with different package bytes"
                )

            staging_directory = Path(tempfile.mkdtemp(prefix=".staging-", dir=releases_root))
            target_package = staging_directory / package["fileName"]
            os.replace(staged_package, target_package)
            staged_package = None
            _write_bytes_durable(staging_directory / "manifest.json", manifest_bytes)
            _write_bytes_durable(staging_directory / "manifest.sig", signature_bytes)
            os.replace(staging_directory, target_root)
            staging_directory = None

            # Commit the latest pointer only after the immutable release exists.
            _write_latest_pointer(product_root, version)
            stored = load_spectrum_release(storage, version)
            return SpectrumPublishResult(created=True, release=stored)
    finally:
        if staged_package is not None:
            staged_package.unlink(missing_ok=True)
        _cleanup_staging_directory(staging_directory)

from __future__ import annotations

import os
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import IO, Any
from urllib.parse import quote
from uuid import uuid4

DEFAULT_TRANSFER_UPLOAD_DIR = "Transfer"
TRANSFER_FILE_SCOPE = "file:transfer"
TRANSFER_CHUNK_SIZE = 1024 * 1024


class TransferFileError(Exception):
    def __init__(self, message: str, status_code: int):
        super().__init__(message)
        self.message = message
        self.status_code = status_code


@dataclass(frozen=True)
class TransferFileRecord:
    name: str
    size: int
    modified: str
    modified_display: str
    download_url: str


@dataclass(frozen=True)
class TransferUploadResult:
    name: str
    target: Path
    bytes_written: int
    replaced: bool


def transfer_root(storage: Path, config: dict[str, Any]) -> Path:
    raw = str(config.get("transfer_upload_dir") or DEFAULT_TRANSFER_UPLOAD_DIR).strip()
    if not raw:
        raw = DEFAULT_TRANSFER_UPLOAD_DIR
    root = Path(raw)
    if not root.is_absolute():
        if any(part == ".." for part in root.parts):
            raise TransferFileError("Invalid transfer_upload_dir", 500)
        root = storage / root
    return root


def path_is_within(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except ValueError:
        return False


def is_transfer_storage_path(storage: Path, config: dict[str, Any], target: Path) -> bool:
    root = transfer_root(storage, config)
    return path_is_within(target, root)


def validate_transfer_filename(filename: str) -> str:
    name = (filename or "").strip()
    if not name or name in (".", ".."):
        raise TransferFileError("File name is required", 400)
    if "/" in name or "\\" in name or ":" in name:
        raise TransferFileError("Only files directly inside the transfer folder are allowed", 403)
    if any(ord(ch) < 32 for ch in name):
        raise TransferFileError("Invalid file name", 400)
    if name.endswith(".uploading"):
        raise TransferFileError("Invalid file name", 400)
    if Path(name).name != name:
        raise TransferFileError("Invalid file name", 400)
    return name


def resolve_transfer_file(root: Path, filename: str) -> Path:
    name = validate_transfer_filename(filename)
    target = root / name
    if not path_is_within(target, root):
        raise TransferFileError("Forbidden transfer path", 403)
    return target


def _format_timestamp(timestamp: float) -> tuple[str, str]:
    dt = datetime.fromtimestamp(timestamp, tz=timezone.utc)
    return dt.isoformat(), dt.strftime("%Y-%m-%d %H:%M")


def list_transfer_files(root: Path) -> list[TransferFileRecord]:
    if not root.exists():
        return []
    if not root.is_dir():
        raise TransferFileError("Transfer path is not a directory", 500)

    records: list[TransferFileRecord] = []
    for entry in sorted(root.iterdir(), key=lambda item: item.name.lower()):
        if not entry.is_file() or entry.name.startswith(".") or entry.name.endswith(".uploading"):
            continue
        try:
            stat = entry.stat()
        except OSError:
            continue
        modified, modified_display = _format_timestamp(stat.st_mtime)
        records.append(
            TransferFileRecord(
                name=entry.name,
                size=stat.st_size,
                modified=modified,
                modified_display=modified_display,
                download_url=f"/api/transfer/files/{quote(entry.name)}",
            )
        )
    return records


def stream_transfer_upload(
    root: Path,
    filename: str,
    stream: IO[bytes],
    *,
    chunk_size: int = TRANSFER_CHUNK_SIZE,
) -> TransferUploadResult:
    target = resolve_transfer_file(root, filename)
    root.mkdir(parents=True, exist_ok=True)
    replaced = target.exists()
    temp_target = root / f".{target.name}.{uuid4().hex}.uploading"
    bytes_written = 0

    try:
        with open(temp_target, "wb") as output:
            while True:
                chunk = stream.read(chunk_size)
                if not chunk:
                    break
                output.write(chunk)
                bytes_written += len(chunk)
        os.replace(temp_target, target)
    except OSError as exc:
        temp_target.unlink(missing_ok=True)
        raise TransferFileError(f"Upload failed: {exc}", 500) from exc
    except Exception:
        temp_target.unlink(missing_ok=True)
        raise

    return TransferUploadResult(
        name=target.name,
        target=target,
        bytes_written=bytes_written,
        replaced=replaced,
    )


def delete_transfer_file(root: Path, filename: str) -> Path:
    target = resolve_transfer_file(root, filename)
    if not target.exists() or not target.is_file():
        raise TransferFileError("File not found", 404)
    try:
        target.unlink()
    except OSError as exc:
        raise TransferFileError(f"Delete failed: {exc}", 500) from exc
    return target

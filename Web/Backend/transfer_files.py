from __future__ import annotations

import os
import json
import re
import threading
import time
from dataclasses import dataclass, replace
from datetime import datetime, timezone
from pathlib import Path
from typing import IO, Any
from urllib.parse import quote
from uuid import uuid4

DEFAULT_TRANSFER_UPLOAD_DIR = "Transfer"
ANONYMOUS_TRANSFER_OWNER_TYPE = "anonymous_transfer"
DEFAULT_ANONYMOUS_TRANSFER_MAX_BYTES = 2 * 1024 * 1024 * 1024
TRANSFER_FILE_SCOPE = "file:transfer"
TRANSFER_CHUNK_SIZE = 1024 * 1024
TRANSFER_RESUME_CHUNK_SIZE = 8 * 1024 * 1024
TRANSFER_RESUME_MAX_CHUNK_SIZE = 16 * 1024 * 1024
TRANSFER_UPLOAD_SESSION_DIR = ".transfer_uploads"
TRANSFER_SHARE_DIR = ".transfer_shares"
ANONYMOUS_TRANSFER_FILE_TTL_SECONDS = 24 * 60 * 60
TRANSFER_UPLOAD_SESSION_TTL_SECONDS = 7 * 24 * 60 * 60
TRANSFER_UPLOAD_COMPLETED_TTL_SECONDS = 24 * 60 * 60
TRANSFER_UPLOAD_FINGERPRINT_PATTERN = re.compile(r"^[0-9a-f]{64}$")
TRANSFER_UPLOAD_ID_PATTERN = re.compile(r"^[0-9a-f]{32}$")
TRANSFER_SHARE_TOKEN_PATTERN = re.compile(r"^[0-9a-f]{32}$")

_session_locks_guard = threading.Lock()
_session_locks: dict[str, threading.Lock] = {}
_session_create_lock = threading.Lock()
_share_lock = threading.Lock()


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
    share_url: str
    expires_at: str | None
    temporary: bool


@dataclass(frozen=True)
class TransferUploadResult:
    name: str
    target: Path
    bytes_written: int
    replaced: bool


@dataclass(frozen=True)
class TransferUploadSession:
    upload_id: str
    filename: str
    total_size: int
    offset: int
    fingerprint: str
    owner_type: str
    owner_id: str
    created_at: float
    updated_at: float
    complete: bool = False
    replaced: bool = False
    share_token: str = ""
    expires_at: float = 0.0

    @property
    def download_url(self) -> str:
        return f"/api/transfer/files/{quote(self.filename)}"

    @property
    def share_url(self) -> str:
        return f"/transfer/share/{self.share_token}" if self.share_token else ""


@dataclass(frozen=True)
class TransferShare:
    token: str
    filename: str
    created_at: float
    expires_at: float = 0.0

    @property
    def share_url(self) -> str:
        return f"/transfer/share/{self.token}"

    @property
    def download_url(self) -> str:
        return f"/api/transfer/shares/{self.token}/download"


@dataclass(frozen=True)
class TransferShareRecord:
    token: str
    name: str
    size: int
    modified: str
    modified_display: str
    created_at: str
    expires_at: str | None
    temporary: bool
    share_url: str
    download_url: str


@dataclass(frozen=True)
class TransferUploadAppendResult:
    session: TransferUploadSession
    newly_completed: bool


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


def is_anonymous_transfer_upload_enabled(config: dict[str, Any]) -> bool:
    return config.get("anonymous_transfer_upload_enabled") is True


def get_anonymous_transfer_max_bytes(config: dict[str, Any]) -> int:
    raw = config.get("anonymous_transfer_max_bytes", DEFAULT_ANONYMOUS_TRANSFER_MAX_BYTES)
    if isinstance(raw, bool):
        return DEFAULT_ANONYMOUS_TRANSFER_MAX_BYTES
    try:
        value = int(raw)
    except (TypeError, ValueError):
        return DEFAULT_ANONYMOUS_TRANSFER_MAX_BYTES
    return value if value > 0 else DEFAULT_ANONYMOUS_TRANSFER_MAX_BYTES


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


def _share_root(root: Path) -> Path:
    share_root = root / TRANSFER_SHARE_DIR
    if not path_is_within(share_root, root):
        raise TransferFileError("Invalid transfer share path", 500)
    return share_root


def _validate_share_token(token: str) -> str:
    value = (token or "").strip().lower()
    if not TRANSFER_SHARE_TOKEN_PATTERN.fullmatch(value):
        raise TransferFileError("Share link not found", 404)
    return value


def _share_path(root: Path, token: str) -> Path:
    return _share_root(root) / f"{_validate_share_token(token)}.json"


def _share_to_payload(share: TransferShare) -> dict[str, Any]:
    return {
        "token": share.token,
        "filename": share.filename,
        "created_at": share.created_at,
        "expires_at": share.expires_at,
    }


def _share_from_payload(payload: dict[str, Any]) -> TransferShare:
    token = _validate_share_token(str(payload.get("token", "")))
    filename = validate_transfer_filename(str(payload.get("filename", "")))
    try:
        created_at = float(payload.get("created_at", 0))
        expires_at = float(payload.get("expires_at", 0) or 0)
    except (TypeError, ValueError) as exc:
        raise TransferFileError("Invalid transfer share metadata", 500) from exc
    if created_at <= 0 or expires_at < 0:
        raise TransferFileError("Invalid transfer share metadata", 500)
    return TransferShare(token=token, filename=filename, created_at=created_at, expires_at=expires_at)


def _write_transfer_share(metadata_path: Path, share: TransferShare) -> None:
    metadata_path.parent.mkdir(parents=True, exist_ok=True)
    temp_path = metadata_path.with_name(f".{metadata_path.name}.{uuid4().hex}.tmp")
    try:
        with open(temp_path, "w", encoding="utf-8", newline="\n") as output:
            json.dump(_share_to_payload(share), output, ensure_ascii=False, separators=(",", ":"))
            output.flush()
            os.fsync(output.fileno())
        os.replace(temp_path, metadata_path)
    except OSError as exc:
        temp_path.unlink(missing_ok=True)
        raise TransferFileError(f"Share link update failed: {exc}", 500) from exc


def _load_transfer_share(metadata_path: Path) -> TransferShare:
    try:
        with open(metadata_path, "r", encoding="utf-8") as source:
            payload = json.load(source)
    except (OSError, json.JSONDecodeError) as exc:
        raise TransferFileError("Share link not found", 404) from exc
    if not isinstance(payload, dict):
        raise TransferFileError("Share link not found", 404)
    return _share_from_payload(payload)


def _find_transfer_share(root: Path, filename: str) -> TransferShare | None:
    share_root = _share_root(root)
    if not share_root.is_dir():
        return None
    name_key = filename.casefold()
    for metadata_path in share_root.glob("*.json"):
        try:
            share = _load_transfer_share(metadata_path)
        except TransferFileError:
            continue
        if share.filename.casefold() == name_key:
            return share
    return None


def get_or_create_transfer_share(
    root: Path,
    filename: str,
    *,
    expires_at: float | None = None,
) -> TransferShare:
    name = validate_transfer_filename(filename)
    target = resolve_transfer_file(root, name)
    if not target.is_file():
        raise TransferFileError("File not found", 404)
    effective_expiry = None if expires_at is None else max(0.0, float(expires_at or 0.0))

    with _share_lock:
        existing = _find_transfer_share(root, name)
        if existing is not None:
            if existing.expires_at > 0 and effective_expiry == 0:
                existing = replace(existing, expires_at=0.0)
                _write_transfer_share(_share_path(root, existing.token), existing)
            return existing

        token = uuid4().hex
        share = TransferShare(
            token=token,
            filename=name,
            created_at=time.time(),
            expires_at=effective_expiry or 0.0,
        )
        _write_transfer_share(_share_path(root, token), share)
        return share


def _share_record(root: Path, share: TransferShare) -> TransferShareRecord:
    target = resolve_transfer_file(root, share.filename)
    if not target.is_file():
        raise TransferFileError("Shared file not found", 404)
    try:
        stat = target.stat()
    except OSError as exc:
        raise TransferFileError(f"Shared file unavailable: {exc}", 500) from exc
    modified, modified_display = _format_timestamp(stat.st_mtime)
    created_at, _ = _format_timestamp(share.created_at)
    expires_at = _format_timestamp(share.expires_at)[0] if share.expires_at > 0 else None
    return TransferShareRecord(
        token=share.token,
        name=share.filename,
        size=stat.st_size,
        modified=modified,
        modified_display=modified_display,
        created_at=created_at,
        expires_at=expires_at,
        temporary=share.expires_at > 0,
        share_url=share.share_url,
        download_url=share.download_url,
    )


def get_transfer_share(root: Path, token: str, *, now: float | None = None) -> TransferShareRecord:
    metadata_path = _share_path(root, token)
    share = _load_transfer_share(metadata_path)
    current_time = time.time() if now is None else now
    if share.expires_at > 0 and current_time >= share.expires_at:
        cleanup_expired_transfer_files(root, now=current_time)
        raise TransferFileError("临时文件已过期", 410)
    return _share_record(root, share)


def _remove_transfer_shares(root: Path, filename: str) -> None:
    share_root = _share_root(root)
    if not share_root.is_dir():
        return
    name_key = filename.casefold()
    with _share_lock:
        for metadata_path in share_root.glob("*.json"):
            try:
                share = _load_transfer_share(metadata_path)
            except TransferFileError:
                continue
            if share.filename.casefold() == name_key:
                metadata_path.unlink(missing_ok=True)


def cleanup_expired_transfer_files(root: Path, *, now: float | None = None) -> int:
    share_root = _share_root(root)
    if not share_root.is_dir():
        return 0
    current_time = time.time() if now is None else now
    deleted = 0
    with _share_lock:
        for metadata_path in share_root.glob("*.json"):
            try:
                share = _load_transfer_share(metadata_path)
            except TransferFileError:
                continue
            if share.expires_at <= 0 or current_time < share.expires_at:
                continue
            target = resolve_transfer_file(root, share.filename)
            try:
                if target.is_file():
                    target.unlink()
                    deleted += 1
                metadata_path.unlink(missing_ok=True)
            except OSError:
                continue
    return deleted


def list_transfer_files(root: Path) -> list[TransferFileRecord]:
    cleanup_expired_transfer_files(root)
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
        share = get_or_create_transfer_share(root, entry.name)
        expires_at = _format_timestamp(share.expires_at)[0] if share.expires_at > 0 else None
        records.append(
            TransferFileRecord(
                name=entry.name,
                size=stat.st_size,
                modified=modified,
                modified_display=modified_display,
                download_url=f"/api/transfer/files/{quote(entry.name)}",
                share_url=share.share_url,
                expires_at=expires_at,
                temporary=share.expires_at > 0,
            )
        )
    return records


def _validate_upload_id(upload_id: str) -> str:
    value = (upload_id or "").strip().lower()
    if not TRANSFER_UPLOAD_ID_PATTERN.fullmatch(value):
        raise TransferFileError("Upload session not found", 404)
    return value


def _validate_upload_fingerprint(fingerprint: str) -> str:
    value = (fingerprint or "").strip().lower()
    if not TRANSFER_UPLOAD_FINGERPRINT_PATTERN.fullmatch(value):
        raise TransferFileError("Invalid upload fingerprint", 400)
    return value


def _validate_upload_size(total_size: int) -> int:
    if isinstance(total_size, bool):
        raise TransferFileError("Invalid upload size", 400)
    try:
        value = int(total_size)
    except (TypeError, ValueError) as exc:
        raise TransferFileError("Invalid upload size", 400) from exc
    if value < 0 or value > 0x7FFF_FFFF_FFFF_FFFF:
        raise TransferFileError("Invalid upload size", 400)
    return value


def _upload_session_root(root: Path) -> Path:
    session_root = root / TRANSFER_UPLOAD_SESSION_DIR
    if not path_is_within(session_root, root):
        raise TransferFileError("Invalid upload session path", 500)
    return session_root


def _upload_session_paths(root: Path, upload_id: str) -> tuple[Path, Path]:
    validated_id = _validate_upload_id(upload_id)
    session_root = _upload_session_root(root)
    return session_root / f"{validated_id}.json", session_root / f"{validated_id}.part"


def _session_to_payload(session: TransferUploadSession) -> dict[str, Any]:
    return {
        "upload_id": session.upload_id,
        "filename": session.filename,
        "total_size": session.total_size,
        "offset": session.offset,
        "fingerprint": session.fingerprint,
        "owner_type": session.owner_type,
        "owner_id": session.owner_id,
        "created_at": session.created_at,
        "updated_at": session.updated_at,
        "complete": session.complete,
        "replaced": session.replaced,
        "share_token": session.share_token,
        "expires_at": session.expires_at,
    }


def _session_from_payload(payload: dict[str, Any]) -> TransferUploadSession:
    upload_id = _validate_upload_id(str(payload.get("upload_id", "")))
    filename = validate_transfer_filename(str(payload.get("filename", "")))
    total_size = _validate_upload_size(payload.get("total_size", -1))
    offset = _validate_upload_size(payload.get("offset", -1))
    if offset > total_size:
        raise TransferFileError("Invalid upload session offset", 500)
    fingerprint = _validate_upload_fingerprint(str(payload.get("fingerprint", "")))
    try:
        created_at = float(payload.get("created_at", 0))
        updated_at = float(payload.get("updated_at", 0))
        expires_at = float(payload.get("expires_at", 0) or 0)
    except (TypeError, ValueError) as exc:
        raise TransferFileError("Invalid upload session timestamp", 500) from exc
    if created_at <= 0 or updated_at <= 0 or expires_at < 0:
        raise TransferFileError("Invalid upload session timestamp", 500)
    raw_share_token = str(payload.get("share_token", "") or "")
    share_token = _validate_share_token(raw_share_token) if raw_share_token else ""
    return TransferUploadSession(
        upload_id=upload_id,
        filename=filename,
        total_size=total_size,
        offset=offset,
        fingerprint=fingerprint,
        owner_type=str(payload.get("owner_type", "") or "system"),
        owner_id=str(payload.get("owner_id", "") or "system"),
        created_at=created_at,
        updated_at=updated_at,
        complete=bool(payload.get("complete", False)),
        replaced=bool(payload.get("replaced", False)),
        share_token=share_token,
        expires_at=expires_at,
    )


def _write_upload_session(metadata_path: Path, session: TransferUploadSession) -> None:
    metadata_path.parent.mkdir(parents=True, exist_ok=True)
    temp_path = metadata_path.with_name(f".{metadata_path.name}.{uuid4().hex}.tmp")
    try:
        with open(temp_path, "w", encoding="utf-8", newline="\n") as output:
            json.dump(_session_to_payload(session), output, ensure_ascii=False, separators=(",", ":"))
            output.flush()
            os.fsync(output.fileno())
        os.replace(temp_path, metadata_path)
    except OSError as exc:
        temp_path.unlink(missing_ok=True)
        raise TransferFileError(f"Upload session update failed: {exc}", 500) from exc


def _load_upload_session(metadata_path: Path) -> TransferUploadSession:
    try:
        with open(metadata_path, "r", encoding="utf-8") as source:
            payload = json.load(source)
    except (OSError, json.JSONDecodeError) as exc:
        raise TransferFileError("Upload session not found", 404) from exc
    if not isinstance(payload, dict):
        raise TransferFileError("Upload session not found", 404)
    return _session_from_payload(payload)


def _get_session_lock(upload_id: str) -> threading.Lock:
    with _session_locks_guard:
        return _session_locks.setdefault(upload_id, threading.Lock())


def _owner_matches(session: TransferUploadSession, owner_type: str, owner_id: str) -> bool:
    return session.owner_type == (owner_type or "system") and session.owner_id == (owner_id or "system")


def _reconcile_upload_session(
    root: Path,
    metadata_path: Path,
    session: TransferUploadSession,
) -> TransferUploadSession:
    if session.complete:
        target = resolve_transfer_file(root, session.filename)
        if target.is_file() and not session.share_token:
            expiry = session.expires_at
            if session.owner_type == ANONYMOUS_TRANSFER_OWNER_TYPE and expiry <= 0:
                expiry = session.updated_at + ANONYMOUS_TRANSFER_FILE_TTL_SECONDS
            share = get_or_create_transfer_share(root, session.filename, expires_at=expiry)
            session = replace(session, share_token=share.token, expires_at=share.expires_at)
            _write_upload_session(metadata_path, session)
        return session
    _, part_path = _upload_session_paths(root, session.upload_id)
    if part_path.is_file():
        try:
            committed_size = min(part_path.stat().st_size, session.offset, session.total_size)
        except OSError:
            return session
        if committed_size != session.offset:
            session = replace(session, offset=committed_size, updated_at=time.time())
            _write_upload_session(metadata_path, session)
        return session

    target = resolve_transfer_file(root, session.filename)
    if session.offset == session.total_size and target.is_file():
        try:
            if target.stat().st_size == session.total_size:
                completed_at = time.time()
                expiry = completed_at + ANONYMOUS_TRANSFER_FILE_TTL_SECONDS \
                    if session.owner_type == ANONYMOUS_TRANSFER_OWNER_TYPE else 0.0
                share = get_or_create_transfer_share(root, session.filename, expires_at=expiry)
                session = replace(
                    session,
                    complete=True,
                    updated_at=completed_at,
                    share_token=share.token,
                    expires_at=share.expires_at,
                )
                _write_upload_session(metadata_path, session)
                return session
        except OSError:
            pass

    if session.offset != 0:
        session = replace(session, offset=0, updated_at=time.time())
        _write_upload_session(metadata_path, session)
    return session


def _cleanup_upload_sessions(root: Path, *, now: float | None = None) -> None:
    session_root = _upload_session_root(root)
    if not session_root.is_dir():
        return
    current_time = now if now is not None else time.time()
    for metadata_path in session_root.glob("*.json"):
        try:
            session = _load_upload_session(metadata_path)
        except TransferFileError:
            continue
        ttl = TRANSFER_UPLOAD_COMPLETED_TTL_SECONDS if session.complete else TRANSFER_UPLOAD_SESSION_TTL_SECONDS
        if current_time - session.updated_at <= ttl:
            continue
        _, part_path = _upload_session_paths(root, session.upload_id)
        try:
            metadata_path.unlink(missing_ok=True)
            part_path.unlink(missing_ok=True)
        except OSError:
            continue


def get_transfer_upload_session(
    root: Path,
    upload_id: str,
    *,
    owner_type: str,
    owner_id: str,
) -> TransferUploadSession:
    metadata_path, _ = _upload_session_paths(root, upload_id)
    session = _load_upload_session(metadata_path)
    if not _owner_matches(session, owner_type, owner_id):
        raise TransferFileError("Upload session not found", 404)
    session = _reconcile_upload_session(root, metadata_path, session)
    if session.expires_at > 0 and time.time() >= session.expires_at:
        cleanup_expired_transfer_files(root)
        raise TransferFileError("临时文件已过期", 410)
    return session


def create_or_resume_transfer_upload(
    root: Path,
    filename: str,
    total_size: int,
    fingerprint: str,
    *,
    owner_type: str,
    owner_id: str,
) -> TransferUploadSession:
    name = validate_transfer_filename(filename)
    size = _validate_upload_size(total_size)
    validated_fingerprint = _validate_upload_fingerprint(fingerprint)
    effective_owner_type = owner_type or "system"
    effective_owner_id = owner_id or "system"
    root.mkdir(parents=True, exist_ok=True)
    cleanup_expired_transfer_files(root)
    target = resolve_transfer_file(root, name)
    anonymous_upload = effective_owner_type == ANONYMOUS_TRANSFER_OWNER_TYPE
    if anonymous_upload and target.exists():
        raise TransferFileError("同名文件已存在，请重命名后重试", 409)
    session_root = _upload_session_root(root)
    session_root.mkdir(parents=True, exist_ok=True)

    with _session_create_lock:
        _cleanup_upload_sessions(root)
        for metadata_path in session_root.glob("*.json"):
            try:
                existing = _load_upload_session(metadata_path)
            except TransferFileError:
                continue
            existing = _reconcile_upload_session(root, metadata_path, existing)
            if (
                not existing.complete
                and existing.filename == name
                and existing.total_size == size
                and existing.fingerprint == validated_fingerprint
                and _owner_matches(existing, effective_owner_type, effective_owner_id)
            ):
                return existing

        upload_id = uuid4().hex
        metadata_path, part_path = _upload_session_paths(root, upload_id)
        now = time.time()
        session = TransferUploadSession(
            upload_id=upload_id,
            filename=name,
            total_size=size,
            offset=0,
            fingerprint=validated_fingerprint,
            owner_type=effective_owner_type,
            owner_id=effective_owner_id,
            created_at=now,
            updated_at=now,
        )
        try:
            with open(part_path, "xb"):
                pass
            _write_upload_session(metadata_path, session)
        except Exception:
            part_path.unlink(missing_ok=True)
            metadata_path.unlink(missing_ok=True)
            raise

        if size == 0:
            replaced = target.exists()
            if anonymous_upload and replaced:
                raise TransferFileError("同名文件已存在，请重命名后重试", 409)
            try:
                os.replace(part_path, target)
            except OSError as exc:
                raise TransferFileError(f"Upload failed: {exc}", 500) from exc
            completed_at = time.time()
            expiry = completed_at + ANONYMOUS_TRANSFER_FILE_TTL_SECONDS if anonymous_upload else 0.0
            share = get_or_create_transfer_share(root, name, expires_at=expiry)
            session = replace(
                session,
                complete=True,
                replaced=replaced,
                updated_at=completed_at,
                share_token=share.token,
                expires_at=share.expires_at,
            )
            _write_upload_session(metadata_path, session)
        return session


def append_transfer_upload(
    root: Path,
    upload_id: str,
    offset: int,
    stream: IO[bytes],
    *,
    owner_type: str,
    owner_id: str,
    max_chunk_size: int = TRANSFER_RESUME_MAX_CHUNK_SIZE,
) -> TransferUploadAppendResult:
    validated_id = _validate_upload_id(upload_id)
    requested_offset = _validate_upload_size(offset)
    lock = _get_session_lock(validated_id)

    with lock:
        session = get_transfer_upload_session(
            root,
            validated_id,
            owner_type=owner_type,
            owner_id=owner_id,
        )
        if session.complete:
            return TransferUploadAppendResult(session=session, newly_completed=False)
        if requested_offset != session.offset:
            raise TransferFileError(f"Upload offset mismatch; expected {session.offset}", 409)

        metadata_path, part_path = _upload_session_paths(root, validated_id)
        if not part_path.exists():
            if session.offset != 0:
                raise TransferFileError("Upload session data is missing", 409)
            part_path.touch()

        bytes_written = 0
        try:
            with open(part_path, "r+b") as output:
                output.truncate(session.offset)
                output.seek(session.offset)
                while True:
                    chunk = stream.read(TRANSFER_CHUNK_SIZE)
                    if not chunk:
                        break
                    next_chunk_size = bytes_written + len(chunk)
                    next_offset = session.offset + next_chunk_size
                    if next_chunk_size > max_chunk_size:
                        raise TransferFileError("Upload chunk is too large", 413)
                    if next_offset > session.total_size:
                        raise TransferFileError("Upload exceeds declared file size", 409)
                    output.write(chunk)
                    bytes_written = next_chunk_size
                if bytes_written == 0 and session.offset < session.total_size:
                    raise TransferFileError("Upload chunk is empty", 400)
                output.flush()
                os.fsync(output.fileno())
        except TransferFileError:
            raise
        except OSError as exc:
            raise TransferFileError(f"Upload failed: {exc}", 500) from exc

        new_offset = session.offset + bytes_written
        updated = replace(session, offset=new_offset, updated_at=time.time())
        if new_offset < session.total_size:
            _write_upload_session(metadata_path, updated)
            return TransferUploadAppendResult(session=updated, newly_completed=False)

        target = resolve_transfer_file(root, session.filename)
        replaced = target.exists()
        if session.owner_type == ANONYMOUS_TRANSFER_OWNER_TYPE and replaced:
            _write_upload_session(metadata_path, updated)
            raise TransferFileError("同名文件已存在，请重命名后重试", 409)
        _write_upload_session(metadata_path, updated)
        try:
            os.replace(part_path, target)
        except OSError as exc:
            raise TransferFileError(f"Upload failed: {exc}", 500) from exc
        completed_at = time.time()
        expiry = completed_at + ANONYMOUS_TRANSFER_FILE_TTL_SECONDS \
            if session.owner_type == ANONYMOUS_TRANSFER_OWNER_TYPE else 0.0
        share = get_or_create_transfer_share(root, session.filename, expires_at=expiry)
        completed = replace(
            updated,
            complete=True,
            replaced=replaced,
            updated_at=completed_at,
            share_token=share.token,
            expires_at=share.expires_at,
        )
        _write_upload_session(metadata_path, completed)
        return TransferUploadAppendResult(session=completed, newly_completed=True)


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
        _remove_transfer_shares(root, target.name)
    except OSError as exc:
        raise TransferFileError(f"Delete failed: {exc}", 500) from exc
    return target

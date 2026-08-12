"""Administrator-facing feedback inventory backed by the Feedback directory."""

from __future__ import annotations

import json
import os
import threading
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Mapping


FEEDBACK_STATUSES = ("new", "in_progress", "resolved")
_METADATA_NAME = "feedback.json"
_STATE_NAME = ".admin.json"
_MAX_JSON_BYTES = 1024 * 1024
_MAX_QUERY_LENGTH = 200
_write_lock = threading.Lock()


def _utc_iso(timestamp: float) -> str:
    return datetime.fromtimestamp(timestamp, timezone.utc).isoformat()


def _bounded_text(value: Any, maximum: int = 4000) -> str:
    return value[:maximum] if isinstance(value, str) else ""


def _read_json_object(path: Path) -> tuple[dict[str, Any] | None, bool]:
    if not path.is_file() or path.is_symlink():
        return None, False
    try:
        if path.stat().st_size > _MAX_JSON_BYTES:
            return None, False
        with path.open(encoding="utf-8") as stream:
            value = json.load(stream)
        return (value, True) if isinstance(value, dict) else (None, False)
    except (OSError, UnicodeError, json.JSONDecodeError):
        return None, False


def _feedback_root(storage: Path) -> Path:
    return Path(storage) / "Feedback"


def _safe_feedback_directory(storage: Path, feedback_id: str) -> Path:
    if (
        not isinstance(feedback_id, str)
        or not feedback_id
        or len(feedback_id) > 128
        or feedback_id in {".", ".."}
        or any(character not in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.-" for character in feedback_id)
    ):
        raise FileNotFoundError("Feedback not found")

    root = _feedback_root(storage)
    if root.is_symlink() or not root.is_dir():
        raise FileNotFoundError("Feedback not found")
    directory = root / feedback_id
    try:
        if directory.is_symlink() or not directory.is_dir():
            raise FileNotFoundError("Feedback not found")
        if directory.resolve(strict=True).parent != root.resolve(strict=True):
            raise FileNotFoundError("Feedback not found")
    except OSError as exc:
        raise FileNotFoundError("Feedback not found") from exc
    return directory


def _attachments(directory: Path) -> list[dict[str, Any]]:
    items: list[dict[str, Any]] = []
    try:
        children = list(directory.iterdir())
    except OSError:
        return items
    for child in children:
        if child.name in {_METADATA_NAME, _STATE_NAME} or child.is_symlink():
            continue
        try:
            if not child.is_file() or child.resolve(strict=True).parent != directory.resolve(strict=True):
                continue
            stat = child.stat()
        except OSError:
            continue
        items.append({
            "name": child.name,
            "size_bytes": stat.st_size,
            "modified_at": _utc_iso(stat.st_mtime),
        })
    return sorted(items, key=lambda item: item["name"].casefold())


def _record_from_directory(directory: Path, *, include_details: bool) -> dict[str, Any]:
    metadata_path = directory / _METADATA_NAME
    state_path = directory / _STATE_NAME
    metadata, metadata_valid = _read_json_object(metadata_path)
    state, state_valid = _read_json_object(state_path)
    metadata = metadata or {}
    state = state or {}
    status = state.get("status")
    if status not in FEEDBACK_STATUSES:
        status = "new"

    try:
        fallback_created_at = _utc_iso(directory.stat().st_mtime)
    except OSError:
        fallback_created_at = datetime.now(timezone.utc).isoformat()
    created_at = _bounded_text(metadata.get("createdAt"), 100) or fallback_created_at
    message = _bounded_text(metadata.get("message"))
    attachments = _attachments(directory)
    record: dict[str, Any] = {
        "feedback_id": directory.name,
        "status": status,
        "created_at": created_at,
        "updated_at": _bounded_text(state.get("updatedAt"), 100) or None,
        "user_name": _bounded_text(metadata.get("userName")),
        "app_version": _bounded_text(metadata.get("appVersion")),
        "message_preview": message[:160],
        "attachment_count": len(attachments),
        "attachment_bytes": sum(item["size_bytes"] for item in attachments),
        "metadata_valid": metadata_valid,
        "state_valid": state_valid if state_path.exists() else True,
    }
    if include_details:
        record.update({
            "message": message,
            "machine_info": _bounded_text(metadata.get("machineInfo")),
            "client_ip": _bounded_text(metadata.get("clientIp"), 200),
            "attachments": attachments,
        })
    return record


def _all_feedback(storage: Path, *, include_details: bool = False) -> list[dict[str, Any]]:
    root = _feedback_root(storage)
    if root.is_symlink() or not root.is_dir():
        return []
    records: list[dict[str, Any]] = []
    try:
        directories = list(root.iterdir())
    except OSError:
        return records
    for directory in directories:
        if directory.is_symlink() or not directory.is_dir():
            continue
        records.append(_record_from_directory(directory, include_details=include_details))
    records.sort(key=lambda item: (item["created_at"], item["feedback_id"]), reverse=True)
    return records


def query_feedback(
    storage: Path,
    *,
    status: str | None = None,
    query: str | None = None,
    limit: int = 20,
    offset: int = 0,
) -> dict[str, Any]:
    if status and status not in FEEDBACK_STATUSES:
        raise ValueError("status must be new, in_progress, or resolved")
    normalized_query = (query or "").strip()
    if len(normalized_query) > _MAX_QUERY_LENGTH:
        raise ValueError(f"query must not exceed {_MAX_QUERY_LENGTH} characters")
    if limit < 1 or limit > 100:
        raise ValueError("limit must be between 1 and 100")
    if offset < 0:
        raise ValueError("offset must be non-negative")

    records = _all_feedback(storage)
    summary = {
        "records": len(records),
        "status_counts": {
            candidate: sum(1 for item in records if item["status"] == candidate)
            for candidate in FEEDBACK_STATUSES
        },
        "attachment_count": sum(item["attachment_count"] for item in records),
        "attachment_bytes": sum(item["attachment_bytes"] for item in records),
        "invalid_metadata": sum(1 for item in records if not item["metadata_valid"]),
        "invalid_state": sum(1 for item in records if not item["state_valid"]),
    }

    filtered = records
    if status:
        filtered = [item for item in filtered if item["status"] == status]
    if normalized_query:
        needle = normalized_query.casefold()
        filtered = [
            item for item in filtered
            if needle in "\n".join((
                item["feedback_id"],
                item["user_name"],
                item["app_version"],
                item["message_preview"],
            )).casefold()
        ]
    return {
        "items": filtered[offset:offset + limit],
        "total": len(filtered),
        "limit": limit,
        "offset": offset,
        "summary": summary,
    }


def get_feedback_detail(storage: Path, feedback_id: str) -> dict[str, Any]:
    directory = _safe_feedback_directory(storage, feedback_id)
    return _record_from_directory(directory, include_details=True)


def resolve_feedback_attachment(storage: Path, feedback_id: str, filename: str) -> Path:
    directory = _safe_feedback_directory(storage, feedback_id)
    if (
        not isinstance(filename, str)
        or not filename
        or Path(filename).name != filename
        or filename in {_METADATA_NAME, _STATE_NAME}
    ):
        raise FileNotFoundError("Attachment not found")
    target = directory / filename
    try:
        if target.is_symlink() or not target.is_file():
            raise FileNotFoundError("Attachment not found")
        if target.resolve(strict=True).parent != directory.resolve(strict=True):
            raise FileNotFoundError("Attachment not found")
    except OSError as exc:
        raise FileNotFoundError("Attachment not found") from exc
    return target


def validate_feedback_status_payload(payload: Any) -> str:
    if not isinstance(payload, dict) or set(payload) != {"status"}:
        raise ValueError("request body must contain only status")
    status = payload["status"]
    if status not in FEEDBACK_STATUSES:
        raise ValueError("status must be new, in_progress, or resolved")
    return status


def update_feedback_status(storage: Path, feedback_id: str, status: str) -> dict[str, Any]:
    if status not in FEEDBACK_STATUSES:
        raise ValueError("status must be new, in_progress, or resolved")
    directory = _safe_feedback_directory(storage, feedback_id)
    before = _record_from_directory(directory, include_details=False)["status"]
    if before == status:
        return {"changed": False, "before": before, **get_feedback_detail(storage, feedback_id)}

    state_path = directory / _STATE_NAME
    temporary_path = directory / f".{_STATE_NAME}.{uuid.uuid4().hex}.tmp"
    state = {
        "status": status,
        "updatedAt": datetime.now(timezone.utc).isoformat(),
    }
    encoded = (json.dumps(state, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    with _write_lock:
        try:
            with temporary_path.open("xb") as stream:
                stream.write(encoded)
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temporary_path, state_path)
        finally:
            if temporary_path.exists():
                temporary_path.unlink()
    return {"changed": True, "before": before, **get_feedback_detail(storage, feedback_id)}

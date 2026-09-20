"""Administrator-facing feedback inventory backed by the Feedback directory."""

from __future__ import annotations

import json
import hashlib
import html
import os
import re
import threading
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Mapping
from urllib.parse import quote


FEEDBACK_STATUSES = ("new", "in_progress", "resolved")
FEEDBACK_FILTERS = ("open", *FEEDBACK_STATUSES)
_METADATA_NAME = "feedback.json"
_STATE_NAME = ".admin.json"
_MAX_JSON_BYTES = 1024 * 1024
_MAX_QUERY_LENGTH = 200
_write_lock = threading.Lock()


def _is_internal_name(filename: str) -> bool:
    normalized = filename.casefold()
    return (
        normalized in {_METADATA_NAME, _STATE_NAME}
        or normalized.startswith(".feedback.json.")
        or normalized.startswith(".admin.")
    )


def _utc_iso(timestamp: float) -> str:
    return datetime.fromtimestamp(timestamp, timezone.utc).isoformat()


def _parse_timestamp(value: str) -> datetime | None:
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except (AttributeError, TypeError, ValueError):
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def _bounded_text(value: Any, maximum: int = 4000) -> str:
    return value[:maximum] if isinstance(value, str) else ""


def _received_at(metadata: Mapping[str, Any], directory_name: str) -> str:
    # File timestamps change during copying, synchronization and status updates.
    for field in ("serverReceivedAt", "createdAt"):
        timestamp = _parse_timestamp(metadata.get(field))
        if timestamp is not None:
            return timestamp.isoformat()
    match = re.match(r"^(\d{8}_\d{6})(_BJT_)?", directory_name)
    if match:
        try:
            zone = timezone(timedelta(hours=8)) if match[2] else timezone.utc
            timestamp = datetime.strptime(match[1], "%Y%m%d_%H%M%S").replace(tzinfo=zone)
            return timestamp.astimezone(timezone.utc).isoformat()
        except ValueError:
            pass
    return ""


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


def _valid_feedback_id(value: Any) -> bool:
    return isinstance(value, str) and value not in {".", ".."} and re.fullmatch(r"[A-Za-z0-9_.-]{1,128}", value) is not None


def _is_link(path: Path) -> bool:
    # Include Windows junctions, not just symbolic links.
    return path.is_symlink() or bool(getattr(path.lstat(), "st_file_attributes", 0) & 0x400)


def _feedback_directories(storage: Path) -> list[Path]:
    """Read both historical flat records and machine/record directories, at most two levels."""
    root = _feedback_root(storage)
    try:
        if _is_link(root) or not root.is_dir():
            return []
        result = []
        for parent in root.iterdir():
            if _is_link(parent) or not parent.is_dir():
                continue
            children = list(parent.iterdir())
            groups = [child for child in children if not _is_link(child) and child.is_dir()]
            if (parent / _METADATA_NAME).exists() or re.match(r"^\d{8}_\d{6}_", parent.name) or not groups:
                result.append(parent)
            else:
                result.extend(groups)
        return result
    except OSError:
        return []


def _feedback_identifier(metadata: Mapping[str, Any], directory_name: str) -> str:
    identifier = metadata.get("feedbackId")
    return identifier if _valid_feedback_id(identifier) else directory_name


def _safe_feedback_directory(storage: Path, feedback_id: str) -> Path:
    if not _valid_feedback_id(feedback_id):
        raise FileNotFoundError("Feedback not found")
    matches = []
    for directory in _feedback_directories(storage):
        metadata, _ = _read_json_object(directory / _METADATA_NAME)
        if _feedback_identifier(metadata or {}, directory.name) == feedback_id:
            matches.append(directory)
    if len(matches) != 1:
        raise FileNotFoundError("Feedback not found or identifier is ambiguous")
    return matches[0]


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _attachments(directory: Path, *, include_hashes: bool = False) -> list[dict[str, Any]]:
    items: list[dict[str, Any]] = []
    try:
        children = list(directory.iterdir())
    except OSError:
        return items
    for child in children:
        if _is_internal_name(child.name) or child.is_symlink():
            continue
        try:
            if not child.is_file() or child.resolve(strict=True).parent != directory.resolve(strict=True):
                continue
            stat = child.stat()
        except OSError:
            continue
        item = {
            "name": child.name,
            "size_bytes": stat.st_size,
            "modified_at": _utc_iso(stat.st_mtime),
        }
        if include_hashes:
            try:
                item["sha256"] = _sha256(child)
            except OSError:
                continue
        items.append(item)
    return sorted(items, key=lambda item: item["name"].casefold())


def _record_from_directory(directory: Path, *, include_details: bool, include_hashes: bool = True) -> dict[str, Any]:
    metadata_path = directory / _METADATA_NAME
    state_path = directory / _STATE_NAME
    metadata, metadata_valid = _read_json_object(metadata_path)
    state, state_valid = _read_json_object(state_path)
    metadata = metadata or {}
    state = state or {}
    status = state.get("status")
    if status not in FEEDBACK_STATUSES:
        status = "new"

    created_at = _received_at(metadata, directory.name)
    message = _bounded_text(metadata.get("message"))
    attachments = _attachments(directory, include_hashes=include_details and include_hashes)
    owner_user_id = metadata.get("ownerUserId")
    if not isinstance(owner_user_id, int) or isinstance(owner_user_id, bool) or owner_user_id <= 0:
        owner_user_id = None
    machine_name = _bounded_text(metadata.get("machineName"), 255)
    if not machine_name:
        legacy_machine_info = _bounded_text(metadata.get("machineInfo"))
        machine_name = legacy_machine_info.split(" / ", 1)[0].strip() if " / " in legacy_machine_info else ""
    record: dict[str, Any] = {
        "feedback_id": _feedback_identifier(metadata, directory.name),
        "status": status,
        "created_at": created_at,
        "updated_at": _bounded_text(state.get("updatedAt"), 100) or None,
        "user_name": _bounded_text(metadata.get("userName")),
        "owner_user_id": owner_user_id,
        "owner_username": _bounded_text(metadata.get("ownerUsername"), 128),
        "ownership": "account" if owner_user_id is not None else "legacy_unbound",
        "machine_name": machine_name,
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
            "client_submitted_at": _bounded_text(metadata.get("clientSubmittedAt"), 100) or None,
            "diagnostics_collected_at": _bounded_text(metadata.get("diagnosticsCollectedAt"), 100) or None,
            "attachments": attachments,
        })
    return record


def _all_feedback(storage: Path, *, include_details: bool = False) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    for directory in _feedback_directories(storage):
        records.append(_record_from_directory(directory, include_details=include_details))
    records.sort(key=lambda item: (
        _parse_timestamp(item["created_at"]) or datetime.min.replace(tzinfo=timezone.utc),
        item["feedback_id"],
    ), reverse=True)
    return records


def write_feedback_index(storage: Path) -> Path:
    """Write a local share index; original feedback IDs and attachments stay intact."""
    root = _feedback_root(storage)
    if root.is_symlink() or not root.is_dir():
        raise FileNotFoundError("Feedback directory not found")
    with _write_lock:
        rows = []
        directories = {}
        for directory in _feedback_directories(storage):
            metadata, _ = _read_json_object(directory / _METADATA_NAME)
            directories[_feedback_identifier(metadata or {}, directory.name)] = directory
        for record in _all_feedback(storage):
            timestamp = _parse_timestamp(record["created_at"])
            received = timestamp.astimezone(timezone(timedelta(hours=8))).strftime("%Y-%m-%d %H:%M:%S") if timestamp else "未知"
            identifier = record["feedback_id"]
            directory = directories[identifier]
            relative = quote(directory.relative_to(root).as_posix())
            attachments = _attachments(directory, include_hashes=False)
            links = "<br>".join(
                f'<a href="{relative}/{quote(item["name"])}">{html.escape(item["name"])}</a>'
                for item in attachments
            )
            rows.append("<tr>" + "".join(f"<td>{html.escape(value)}</td>" for value in (
                received, record["machine_name"] or "未知机器", record["app_version"], identifier,
            )) + f"<td>{links}</td></tr>")
        document = ('<!doctype html><html lang="zh-CN"><meta charset="utf-8">'
                    '<meta name="viewport" content="width=device-width,initial-scale=1">'
                    '<title>ColorVision 反馈索引</title><style>'
                    'body{font:15px system-ui;margin:28px;color:#202b3b;background:#f6f8fb}'
                    'table{border-collapse:collapse;background:white;width:100%}'
                    'th,td{text-align:left;padding:12px;border-bottom:1px solid #dde3eb;vertical-align:top}'
                    'th{background:#e9eef5;position:sticky;top:0}a{color:#165ab6}'
                    '</style><h1>ColorVision 反馈索引</h1>'
                    '<p>按接收时间从新到旧排列（北京时间），不使用文件或目录修改时间。'
                    '可用 Ctrl+F 查找机器名；附件保持原目录。版本为客户端提交值。</p>'
                    '<table><thead><tr><th>接收时间（北京时间）</th><th>机器</th><th>提交版本</th>'
                    '<th>反馈编号</th><th>附件</th></tr></thead><tbody>'
                    + "".join(rows) + '</tbody></table></html>')
        target = root / "index.html"
        temporary = root / f".index.{uuid.uuid4().hex}.tmp"
        try:
            temporary.write_text(document, encoding="utf-8")
            os.replace(temporary, target)
        finally:
            temporary.unlink(missing_ok=True)
    return target


def query_feedback(
    storage: Path,
    *,
    status: str | None = None,
    query: str | None = None,
    limit: int = 20,
    offset: int = 0,
    owner_user_id: int | None = None,
    machine: str | None = None,
    app_version: str | None = None,
    created_from: str | None = None,
    created_to: str | None = None,
) -> dict[str, Any]:
    if status and status not in FEEDBACK_FILTERS:
        raise ValueError("status must be open, new, in_progress, or resolved")
    normalized_query = (query or "").strip()
    if len(normalized_query) > _MAX_QUERY_LENGTH:
        raise ValueError(f"query must not exceed {_MAX_QUERY_LENGTH} characters")
    if limit < 1 or limit > 100:
        raise ValueError("limit must be between 1 and 100")
    if offset < 0:
        raise ValueError("offset must be non-negative")

    normalized_machine = (machine or "").strip()
    normalized_version = (app_version or "").strip()
    if len(normalized_machine) > _MAX_QUERY_LENGTH:
        raise ValueError(f"machine must not exceed {_MAX_QUERY_LENGTH} characters")
    if len(normalized_version) > _MAX_QUERY_LENGTH:
        raise ValueError(f"app_version must not exceed {_MAX_QUERY_LENGTH} characters")
    from_timestamp = _parse_timestamp(created_from) if created_from else None
    to_timestamp = _parse_timestamp(created_to) if created_to else None
    if created_from and from_timestamp is None:
        raise ValueError("created_from must be an ISO 8601 timestamp")
    if created_to and to_timestamp is None:
        raise ValueError("created_to must be an ISO 8601 timestamp")
    if from_timestamp and to_timestamp and from_timestamp >= to_timestamp:
        raise ValueError("created_from must be earlier than created_to")

    records = _all_feedback(storage)
    if owner_user_id is not None:
        records = [item for item in records if item["owner_user_id"] == owner_user_id]
    if normalized_machine:
        needle = normalized_machine.casefold()
        records = [item for item in records if needle in item["machine_name"].casefold()]
    if normalized_version:
        needle = normalized_version.casefold()
        records = [item for item in records if needle in item["app_version"].casefold()]
    if from_timestamp or to_timestamp:
        filtered_by_time = []
        for item in records:
            timestamp = _parse_timestamp(item["created_at"])
            if timestamp is None:
                continue
            if from_timestamp and timestamp < from_timestamp:
                continue
            if to_timestamp and timestamp >= to_timestamp:
                continue
            filtered_by_time.append(item)
        records = filtered_by_time
    open_timestamps = [
        timestamp
        for item in records
        if item["status"] != "resolved"
        if (timestamp := _parse_timestamp(item["created_at"])) is not None
    ]
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
        "oldest_open_at": min(open_timestamps).isoformat() if open_timestamps else None,
    }

    filtered = records
    if status == "open":
        filtered = [item for item in filtered if item["status"] != "resolved"]
    elif status:
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


def get_feedback_detail(
    storage: Path,
    feedback_id: str,
    *,
    owner_user_id: int | None = None,
    include_hashes: bool = True,
) -> dict[str, Any]:
    directory = _safe_feedback_directory(storage, feedback_id)
    if owner_user_id is not None:
        record = _record_from_directory(directory, include_details=False)
        if record["owner_user_id"] != owner_user_id:
            raise FileNotFoundError("Feedback not found")
    return _record_from_directory(directory, include_details=True, include_hashes=include_hashes)


def resolve_feedback_attachment(
    storage: Path,
    feedback_id: str,
    filename: str,
    *,
    owner_user_id: int | None = None,
) -> Path:
    directory = _safe_feedback_directory(storage, feedback_id)
    if owner_user_id is not None:
        record = _record_from_directory(directory, include_details=False)
        if record["owner_user_id"] != owner_user_id:
            raise FileNotFoundError("Attachment not found")
    if (
        not isinstance(filename, str)
        or not filename
        or Path(filename).name != filename
        or _is_internal_name(filename)
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
    result = _update_directory_status(directory, feedback_id, status)
    # Status changes must not read potentially hundreds of MiB of diagnostic attachments.
    return {**_record_from_directory(directory, include_details=True, include_hashes=False), **result}


def _update_directory_status(directory: Path, feedback_id: str, status: str) -> dict[str, Any]:
    state_path = directory / _STATE_NAME
    temporary_path = directory / f"{_STATE_NAME}.{uuid.uuid4().hex}.tmp"
    with _write_lock:
        previous, valid = _read_json_object(state_path)
        if state_path.exists() and not valid:
            raise OSError("Unable to read feedback management state")
        previous = previous or {}
        before = previous.get("status")
        if before not in FEEDBACK_STATUSES:
            before = "new"
        if before == status:
            return {"feedback_id": feedback_id, "changed": False, "before": before,
                    "status": status, "updated_at": _bounded_text(previous.get("updatedAt"), 100) or None}
        state = {**previous, "status": status, "updatedAt": datetime.now(timezone.utc).isoformat()}
        encoded = (json.dumps(state, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
        try:
            with temporary_path.open("xb") as stream:
                stream.write(encoded)
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temporary_path, state_path)
        finally:
            if temporary_path.exists():
                temporary_path.unlink()
    return {"feedback_id": feedback_id, "changed": True, "before": before,
            "status": status, "updated_at": state["updatedAt"]}


class FeedbackHandlingConflict(Exception):
    """The operator edited an older handling revision."""


_HANDLING_LIMITS = {"conclusion": 4000, "fixed_version": 100, "verification": 4000}


def validate_feedback_handling_payload(payload: Any) -> dict[str, Any]:
    if not isinstance(payload, dict) or set(payload) != {*_HANDLING_LIMITS, "revision"}:
        raise ValueError("request body must contain conclusion, fixed_version, verification and revision")
    if type(payload["revision"]) is not int or payload["revision"] < 0:
        raise ValueError("revision must be a non-negative integer")
    result = {"revision": payload["revision"]}
    for field, maximum in _HANDLING_LIMITS.items():
        value = payload[field]
        if not isinstance(value, str) or len(value) > maximum:
            raise ValueError(f"{field} must be text of at most {maximum} characters")
        result[field] = value.strip()
    return result


def _handling_state(directory: Path) -> tuple[dict[str, Any], dict[str, Any]]:
    path = directory / _STATE_NAME
    state, valid = _read_json_object(path)
    if path.exists() and not valid:
        raise OSError("Unable to read feedback management state")
    state = state or {}
    handling = state.get("handling")
    if handling is None:
        handling = {**dict.fromkeys(_HANDLING_LIMITS, ""), "revision": 0,
                    "updated_at": None, "actor_type": "", "actor_id": "", "history": []}
    if not isinstance(handling, dict) or type(handling.get("revision")) is not int or not isinstance(handling.get("history"), list):
        raise OSError("Unable to read feedback handling record")
    return state, handling


def get_feedback_handling(storage: Path, feedback_id: str) -> dict[str, Any]:
    _, handling = _handling_state(_safe_feedback_directory(storage, feedback_id))
    return {"feedback_id": feedback_id, **handling}


def update_feedback_handling(
    storage: Path, feedback_id: str, payload: Any, *, actor_type: str, actor_id: str,
) -> dict[str, Any]:
    values = validate_feedback_handling_payload(payload)
    directory = _safe_feedback_directory(storage, feedback_id)
    with _write_lock:
        state, previous = _handling_state(directory)
        if previous["revision"] != values["revision"]:
            raise FeedbackHandlingConflict()
        if all(previous.get(field, "") == values[field] for field in _HANDLING_LIMITS):
            return {"feedback_id": feedback_id, **previous, "changed": False}
        entry = {**values, "revision": previous["revision"] + 1,
                 "updated_at": datetime.now(timezone.utc).isoformat(),
                 "actor_type": actor_type[:100], "actor_id": actor_id[:200]}
        # Bound the sidecar size; the UI explicitly labels the retained history window.
        handling = {**entry, "history": [entry, *previous["history"]][:20]}
        state["handling"] = handling
        temporary = directory / f"{_STATE_NAME}.{uuid.uuid4().hex}.tmp"
        try:
            with temporary.open("xb") as stream:
                stream.write((json.dumps(state, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temporary, directory / _STATE_NAME)
        finally:
            if temporary.exists():
                temporary.unlink()
    return {"feedback_id": feedback_id, **handling, "changed": True}


def validate_feedback_bulk_status_payload(payload: Any) -> tuple[list[str], str]:
    if not isinstance(payload, dict) or set(payload) != {"feedback_ids", "status"}:
        raise ValueError("request body must contain only feedback_ids and status")
    status = validate_feedback_status_payload({"status": payload["status"]})
    identifiers = payload["feedback_ids"]
    if not isinstance(identifiers, list) or not 1 <= len(identifiers) <= 500:
        raise ValueError("feedback_ids must contain between 1 and 500 identifiers")
    if any(not _valid_feedback_id(identifier) for identifier in identifiers):
        raise ValueError("feedback_ids contains an invalid identifier")
    if len(set(identifiers)) != len(identifiers):
        raise ValueError("feedback_ids must not contain duplicates")
    return identifiers, status


def update_feedback_statuses(storage: Path, feedback_ids: list[str], status: str) -> dict[str, Any]:
    """Update an explicit selection; new arrivals are never included implicitly."""
    validate_feedback_bulk_status_payload({"feedback_ids": feedback_ids, "status": status})
    directories: dict[str, list[Path]] = {}
    for directory in _feedback_directories(storage):
        metadata, _ = _read_json_object(directory / _METADATA_NAME)
        identifier = _feedback_identifier(metadata or {}, directory.name)
        directories.setdefault(identifier, []).append(directory)
    results = []
    for identifier in feedback_ids:
        matches = directories.get(identifier, [])
        if len(matches) != 1:
            results.append({"feedback_id": identifier, "error": "Feedback not found or identifier is ambiguous"})
            continue
        try:
            results.append(_update_directory_status(matches[0], identifier, status))
        except OSError:
            results.append({"feedback_id": identifier, "error": "Unable to persist feedback status"})
    return {
        "results": results,
        "changed": sum(bool(item.get("changed")) for item in results),
        "unchanged": sum(item.get("changed") is False for item in results),
        "failed": sum("error" in item for item in results),
    }

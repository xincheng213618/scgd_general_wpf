from __future__ import annotations

import hashlib
import json
import logging
import os
import re
import uuid
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Callable


@dataclass(slots=True)
class FeedbackValidationError(ValueError):
    message: str


@dataclass(slots=True)
class FeedbackSaveResult:
    feedback_id: str
    feedback_dir: Path
    metadata: dict[str, Any]


UploadedFile = Any

_BEIJING_TIMEZONE = timezone(timedelta(hours=8), name="BJT")
_RESERVED_ATTACHMENT_NAMES = frozenset({"feedback.json", ".admin.json"})
_MACHINE_SLUG_PATTERN = re.compile(r"[^A-Za-z0-9_-]+")


def _safe_machine_slug(machine_name: str) -> str:
    normalized = _MACHINE_SLUG_PATTERN.sub("-", machine_name.strip()).strip("-_")
    return (normalized[:32] or "UNKNOWN").upper()


def _is_reserved_attachment_name(filename: str) -> bool:
    normalized = filename.casefold()
    return (
        normalized in _RESERVED_ATTACHMENT_NAMES
        or normalized.startswith(".admin.")
        or normalized.startswith("feedback.json.")
        or normalized.startswith(".feedback.json.")
    )


def _optional_iso_timestamp(value: str, field_name: str) -> str:
    if not value:
        return ""
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise FeedbackValidationError(f"{field_name} must be an ISO 8601 timestamp") from exc
    if parsed.tzinfo is None:
        raise FeedbackValidationError(f"{field_name} must include a timezone")
    return parsed.astimezone(timezone.utc).isoformat()


def build_feedback_id(now: datetime, *, message: str, user_name: str, machine_name: str) -> str:
    beijing_now = now.astimezone(_BEIJING_TIMEZONE)
    timestamp = beijing_now.strftime("%Y%m%d_%H%M%S")
    feedback_seed = f"{message}|{user_name}|{machine_name}|{now.isoformat()}|{uuid.uuid4().hex}"
    unique_suffix = hashlib.sha256(feedback_seed.encode()).hexdigest()[:12]
    return f"{timestamp}_BJT_{unique_suffix}"


def unique_output_path(directory: Path, filename: str) -> Path:
    candidate = directory / filename
    if not candidate.exists():
        return candidate

    stem = Path(filename).stem
    suffix = Path(filename).suffix
    index = 1
    while True:
        candidate = directory / f"{stem}-{index}{suffix}"
        if not candidate.exists():
            return candidate
        index += 1


def read_limited_form_value(
    form: Any,
    field_name: str,
    *,
    default: str = "",
    max_length: int,
) -> str:
    value = str(form.get(field_name, default) or default)
    if len(value) > max_length:
        raise FeedbackValidationError(f"{field_name} exceeds the maximum length")
    return value.strip()


def collect_uploaded_files(files: Any) -> list[UploadedFile]:
    return [
        file_item
        for key in files
        for file_item in files.getlist(key)
        if file_item and getattr(file_item, "filename", "")
    ]


def save_feedback(
    storage: Path,
    *,
    form: Any,
    files: Any,
    remote_addr: str | None,
    max_feedback_files: int,
    max_feedback_field_length: int,
    sanitize_filename: Callable[[str], str],
    hash_ip: Callable[[str | None], str],
    owner_user_id: int | None = None,
    owner_username: str = "",
) -> FeedbackSaveResult:
    message = read_limited_form_value(
        form,
        "message",
        max_length=max_feedback_field_length,
    )
    user_name = read_limited_form_value(
        form,
        "userName",
        max_length=max_feedback_field_length,
    )
    app_version = read_limited_form_value(
        form,
        "appVersion",
        max_length=max_feedback_field_length,
    )
    machine_info = read_limited_form_value(
        form,
        "machineInfo",
        max_length=max_feedback_field_length,
    )
    machine_name = read_limited_form_value(
        form,
        "machineName",
        max_length=255,
    )
    if not machine_name and " / " in machine_info:
        machine_name = machine_info.split(" / ", 1)[0].strip()[:255]
    client_submitted_at = _optional_iso_timestamp(
        read_limited_form_value(
            form,
            "clientSubmittedAt",
            max_length=100,
        ),
        "clientSubmittedAt",
    )
    diagnostics_collected_at = _optional_iso_timestamp(
        read_limited_form_value(
            form,
            "diagnosticsCollectedAt",
            max_length=100,
        ),
        "diagnosticsCollectedAt",
    )

    uploaded_files = collect_uploaded_files(files)
    if len(uploaded_files) > max_feedback_files:
        raise FeedbackValidationError(f"A maximum of {max_feedback_files} files is allowed")
    if not message and not uploaded_files:
        raise FeedbackValidationError("Message or at least one file is required")

    now = datetime.now(timezone.utc)
    feedback_id = build_feedback_id(
        now,
        message=message,
        user_name=user_name,
        machine_name=machine_name,
    )
    machine_dir = storage / "Feedback" / _safe_machine_slug(machine_name)
    from services.feedback_admin import _is_link
    for ancestor in (storage / "Feedback", machine_dir):
        if ancestor.exists() and _is_link(ancestor):
            raise FeedbackValidationError("Feedback storage cannot use a linked directory")
    feedback_dir = machine_dir / feedback_id
    feedback_dir.mkdir(parents=True, exist_ok=True)

    metadata: dict[str, Any] = {
        "feedbackId": feedback_id,
        "message": message,
        "userName": user_name,
        "appVersion": app_version,
        "machineInfo": machine_info,
        "machineName": machine_name,
        "clientIp": hash_ip(remote_addr),
        "createdAt": now.isoformat(),
        "serverReceivedAt": now.isoformat(),
        "serverReceivedTimeZone": "UTC",
        "clientSubmittedAt": client_submitted_at or None,
        "diagnosticsCollectedAt": diagnostics_collected_at or None,
        "ownerUserId": owner_user_id if isinstance(owner_user_id, int) and owner_user_id > 0 else None,
        "ownerUsername": owner_username if owner_user_id else "",
        "files": [],
    }

    for uploaded in uploaded_files:
        safe_name = sanitize_filename(uploaded.filename)
        if not safe_name or _is_reserved_attachment_name(safe_name):
            raise FeedbackValidationError("Attachment filename is reserved")
        output_path = unique_output_path(feedback_dir, safe_name)
        uploaded.save(str(output_path))
        metadata["files"].append(output_path.name)

    if not message and not metadata["files"]:
        raise FeedbackValidationError("Message or at least one valid file is required")

    metadata_path = feedback_dir / "feedback.json"
    temporary_path = feedback_dir / f".feedback.json.{uuid.uuid4().hex}.tmp"
    encoded = (json.dumps(metadata, indent=2, ensure_ascii=False) + "\n").encode("utf-8")
    try:
        with temporary_path.open("xb") as metadata_file:
            metadata_file.write(encoded)
            metadata_file.flush()
            os.fsync(metadata_file.fileno())
        os.replace(temporary_path, metadata_path)
    finally:
        if temporary_path.exists():
            temporary_path.unlink()

    try:
        from services.feedback_admin import write_feedback_index
        write_feedback_index(storage)
    except OSError:
        logging.getLogger(__name__).warning("Feedback saved, but share index refresh failed", exc_info=True)

    return FeedbackSaveResult(
        feedback_id=feedback_id,
        feedback_dir=feedback_dir,
        metadata=metadata,
    )


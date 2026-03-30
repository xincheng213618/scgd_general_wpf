from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from datetime import datetime, timezone
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

    uploaded_files = collect_uploaded_files(files)
    if len(uploaded_files) > max_feedback_files:
        raise FeedbackValidationError(f"A maximum of {max_feedback_files} files is allowed")
    if not message and not uploaded_files:
        raise FeedbackValidationError("Message or at least one file is required")

    now = datetime.now(timezone.utc)
    timestamp = now.strftime("%Y%m%d_%H%M%S")
    feedback_seed = f"{message}|{user_name}|{now.isoformat()}"
    feedback_id = f"{timestamp}_{hashlib.sha256(feedback_seed.encode()).hexdigest()[:12]}"
    feedback_dir = storage / "Feedback" / feedback_id
    feedback_dir.mkdir(parents=True, exist_ok=True)

    metadata: dict[str, Any] = {
        "feedbackId": feedback_id,
        "message": message,
        "userName": user_name,
        "appVersion": app_version,
        "machineInfo": machine_info,
        "clientIp": hash_ip(remote_addr),
        "createdAt": now.isoformat(),
        "files": [],
    }

    for uploaded in uploaded_files:
        safe_name = sanitize_filename(uploaded.filename)
        if not safe_name:
            continue
        output_path = unique_output_path(feedback_dir, safe_name)
        uploaded.save(str(output_path))
        metadata["files"].append(output_path.name)

    with open(feedback_dir / "feedback.json", "w", encoding="utf-8") as metadata_file:
        json.dump(metadata, metadata_file, indent=2, ensure_ascii=False)

    return FeedbackSaveResult(
        feedback_id=feedback_id,
        feedback_dir=feedback_dir,
        metadata=metadata,
    )


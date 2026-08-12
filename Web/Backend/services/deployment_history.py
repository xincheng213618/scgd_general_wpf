from __future__ import annotations

import json
from collections import Counter
from pathlib import Path
from typing import Any


DEFAULT_RETENTION_LIMIT = 500


def _text(value: Any) -> str | None:
    if value is None:
        return None
    result = str(value).strip()
    return result or None


def _integer(value: Any) -> int | None:
    if isinstance(value, bool):
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _filename(value: Any) -> str | None:
    text = _text(value)
    if not text:
        return None
    return text.replace("\\", "/").rstrip("/").rsplit("/", 1)[-1] or None


def _retention_summary(value: Any) -> dict[str, Any] | None:
    if not isinstance(value, dict):
        return None
    allowed = (
        "status",
        "keep_records",
        "before_count",
        "after_count",
        "removed_count",
        "removed_successful",
        "removed_failed",
        "removed_bytes",
        "preserved_unclassified",
        "preserved_invalid",
    )
    result = {key: value[key] for key in allowed if key in value}
    return result or None


def _failure_reason(error: Any) -> str | None:
    text = (_text(error) or "").lower()
    if not text:
        return None
    categories = (
        (("git", "commit", "repository", "branch", "bundle"), "source_control"),
        (("npm", "node", "vite", "tsc", "frontend"), "frontend_build"),
        (("test", "unittest", "pytest"), "tests"),
        (("health", "ready", "port", "service", "process", "runtime log"), "service_health"),
        (("backup", "sqlite", "database"), "backup"),
    )
    for terms, category in categories:
        if any(term in text for term in terms):
            return category
    return "deployment"


def _public_entry(record: dict[str, Any], sequence: int) -> dict[str, Any]:
    status = _text(record.get("status")) or "unknown"
    commit = (
        _text(record.get("deployed_commit"))
        or _text(record.get("commit"))
        or _text(record.get("target_commit"))
    )
    recovery = []
    if isinstance(record.get("recovery"), list):
        for item in record["recovery"]:
            value = _text(item)
            if value:
                recovery.append(value.split(":", 1)[0])

    return {
        "sequence": sequence,
        "timestamp": _text(record.get("timestamp")),
        "status": status,
        "source": _text(record.get("source")),
        "commit": commit,
        "previous_commit": _text(record.get("previous_commit")),
        "backup_name": _filename(record.get("backup_path")),
        "frontend_build": _text(record.get("frontend_build")),
        "backend_targeted_tests": _text(record.get("backend_targeted_tests")),
        "health": _text(record.get("health")),
        "ready": record.get("ready") if isinstance(record.get("ready"), bool) else None,
        "runtime_log_verified": (
            record.get("runtime_log_verified")
            if isinstance(record.get("runtime_log_verified"), bool)
            else None
        ),
        "old_pid": _integer(record.get("old_pid")),
        "new_pid": _integer(record.get("new_pid")),
        "failure_reason": _failure_reason(record.get("error")) if status == "failed" else None,
        "recovery": recovery,
        "history_retention": _retention_summary(record.get("history_retention")),
        "backup_retention": _retention_summary(record.get("backup_retention")),
        "git_bundle_retention": _retention_summary(record.get("git_bundle_retention")),
    }


def query_deployment_history(
    storage_path: Path,
    *,
    status: str | None = None,
    source: str | None = None,
    commit: str | None = None,
    limit: int = 20,
    offset: int = 0,
) -> dict[str, Any]:
    if not 1 <= limit <= 100:
        raise ValueError("limit must be between 1 and 100")
    if offset < 0:
        raise ValueError("offset must be zero or greater")

    history_path = Path(storage_path) / "web-deploy-history.jsonl"
    records: list[tuple[int, dict[str, Any]]] = []
    malformed_records = 0
    if history_path.is_file():
        with history_path.open("rb") as stream:
            for sequence, line_bytes in enumerate(stream, start=1):
                try:
                    line = line_bytes.decode("utf-8-sig" if sequence == 1 else "utf-8")
                    if not line.strip():
                        malformed_records += 1
                        continue
                    record = json.loads(line)
                except (json.JSONDecodeError, UnicodeDecodeError):
                    malformed_records += 1
                    continue
                if not isinstance(record, dict):
                    malformed_records += 1
                    continue
                records.append((sequence, record))

    status_counts = Counter((_text(record.get("status")) or "unknown") for _, record in records)
    source_counts = Counter((_text(record.get("source")) or "legacy") for _, record in records)
    retention_limit = DEFAULT_RETENTION_LIMIT
    if records:
        latest_retention = records[-1][1].get("history_retention")
        if isinstance(latest_retention, dict):
            configured_limit = _integer(latest_retention.get("keep_records"))
            if configured_limit and configured_limit > 0:
                retention_limit = configured_limit

    normalized_status = (_text(status) or "").lower()
    normalized_source = (_text(source) or "").lower()
    normalized_commit = (_text(commit) or "").lower()
    filtered: list[tuple[int, dict[str, Any]]] = []
    for sequence, record in reversed(records):
        record_status = (_text(record.get("status")) or "unknown").lower()
        record_source = (_text(record.get("source")) or "legacy").lower()
        record_commit = (
            _text(record.get("deployed_commit"))
            or _text(record.get("commit"))
            or _text(record.get("target_commit"))
            or ""
        ).lower()
        if normalized_status and record_status != normalized_status:
            continue
        if normalized_source and record_source != normalized_source:
            continue
        if normalized_commit and normalized_commit not in record_commit:
            continue
        filtered.append((sequence, record))

    page = filtered[offset:offset + limit]
    return {
        "entries": [_public_entry(record, sequence) for sequence, record in page],
        "total": len(filtered),
        "limit": limit,
        "offset": offset,
        "summary": {
            "records": len(records),
            "malformed_records": malformed_records,
            "retention_limit": retention_limit,
            "statuses": dict(sorted(status_counts.items())),
            "sources": dict(sorted(source_counts.items())),
        },
    }

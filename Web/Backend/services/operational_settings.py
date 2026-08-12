"""Safe, live operational settings persisted to the Web config file.

Only the explicitly allowlisted numeric retention settings are exposed. The
service deliberately reads and rewrites the existing JSON document so secrets,
paths, and future configuration keys remain untouched.
"""

from __future__ import annotations

import json
import os
import threading
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping


@dataclass(frozen=True)
class OperationalSettingSpec:
    default: int
    minimum: int
    maximum: int


OPERATIONAL_RETENTION_SETTINGS: dict[str, OperationalSettingSpec] = {
    "app_release_keep_count": OperationalSettingSpec(5, 1, 100),
    "plugin_package_keep_count": OperationalSettingSpec(3, 1, 100),
    "access_analytics_retention_days": OperationalSettingSpec(90, 1, 3650),
    "job_run_retention_days": OperationalSettingSpec(30, 1, 3650),
    "audit_log_retention_days": OperationalSettingSpec(365, 1, 3650),
    "admin_db_backup_keep_count": OperationalSettingSpec(10, 2, 1000),
}

_write_lock = threading.Lock()


def operational_retention_limits() -> dict[str, dict[str, int]]:
    return {
        name: {"minimum": spec.minimum, "maximum": spec.maximum}
        for name, spec in OPERATIONAL_RETENTION_SETTINGS.items()
    }


def get_operational_retention_settings(config: Mapping[str, Any]) -> dict[str, int]:
    """Return effective values, using defaults for absent or invalid config."""
    values: dict[str, int] = {}
    for name, spec in OPERATIONAL_RETENTION_SETTINGS.items():
        raw = config.get(name, spec.default)
        try:
            if isinstance(raw, bool):
                raise ValueError
            value = int(raw)
        except (TypeError, ValueError):
            value = spec.default
        if not spec.minimum <= value <= spec.maximum:
            value = spec.default
        values[name] = value
    return values


def validate_operational_retention_payload(payload: Any) -> dict[str, int]:
    if not isinstance(payload, dict) or set(payload) != {"values"}:
        raise ValueError("request body must contain only the values object")
    raw_values = payload["values"]
    if not isinstance(raw_values, dict):
        raise ValueError("values must be an object")

    expected = set(OPERATIONAL_RETENTION_SETTINGS)
    received = set(raw_values)
    missing = sorted(expected - received)
    unknown = sorted(received - expected)
    if missing:
        raise ValueError(f"missing settings: {', '.join(missing)}")
    if unknown:
        raise ValueError(f"unknown settings: {', '.join(unknown)}")

    values: dict[str, int] = {}
    for name, spec in OPERATIONAL_RETENTION_SETTINGS.items():
        value = raw_values[name]
        if isinstance(value, bool) or not isinstance(value, int):
            raise ValueError(f"{name} must be an integer")
        if not spec.minimum <= value <= spec.maximum:
            raise ValueError(
                f"{name} must be between {spec.minimum} and {spec.maximum}"
            )
        values[name] = value
    return values


def persist_operational_retention_settings(
    config_path: Path,
    active_config: dict[str, Any],
    values: Mapping[str, int],
) -> dict[str, Any]:
    """Atomically persist allowed values, then update the live config mapping."""
    path = Path(config_path)
    temporary_path: Path | None = None
    with _write_lock:
        if path.exists():
            with path.open(encoding="utf-8") as stream:
                persisted = json.load(stream)
            if not isinstance(persisted, dict):
                raise ValueError("configuration file must contain a JSON object")
        else:
            persisted = {}

        before = get_operational_retention_settings(active_config)
        updated = dict(persisted)
        updated.update(values)
        encoded = (json.dumps(updated, ensure_ascii=False, indent=2) + "\n").encode("utf-8")

        temporary_path = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
        try:
            with temporary_path.open("xb") as stream:
                stream.write(encoded)
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temporary_path, path)
        finally:
            if temporary_path.exists():
                temporary_path.unlink()

        active_config.update(values)
        changed = [name for name in OPERATIONAL_RETENTION_SETTINGS if before[name] != values[name]]
        return {
            "values": dict(values),
            "changed": changed,
            "before": before,
        }

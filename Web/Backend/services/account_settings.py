"""Safe account-access policy exposed to administrators and public auth."""

from __future__ import annotations

from pathlib import Path
from typing import Any, Mapping

from services.config_persistence import persist_config_values


PUBLIC_REGISTRATION_SETTING = "public_registration_enabled"
DEFAULT_PUBLIC_REGISTRATION_ENABLED = False


def is_public_registration_enabled(config: Mapping[str, Any]) -> bool:
    """Fail closed when the setting is absent or not a JSON boolean."""
    value = config.get(PUBLIC_REGISTRATION_SETTING, DEFAULT_PUBLIC_REGISTRATION_ENABLED)
    return value if isinstance(value, bool) else DEFAULT_PUBLIC_REGISTRATION_ENABLED


def get_account_settings(config: Mapping[str, Any]) -> dict[str, bool]:
    return {PUBLIC_REGISTRATION_SETTING: is_public_registration_enabled(config)}


def validate_account_settings_payload(payload: Any) -> dict[str, bool]:
    if not isinstance(payload, dict) or set(payload) != {PUBLIC_REGISTRATION_SETTING}:
        raise ValueError(f"request body must contain only {PUBLIC_REGISTRATION_SETTING}")
    value = payload[PUBLIC_REGISTRATION_SETTING]
    if not isinstance(value, bool):
        raise ValueError(f"{PUBLIC_REGISTRATION_SETTING} must be a boolean")
    return {PUBLIC_REGISTRATION_SETTING: value}


def persist_account_settings(
    config_path: Path,
    active_config: dict[str, Any],
    values: Mapping[str, bool],
) -> dict[str, Any]:
    before = get_account_settings(active_config)
    normalized = get_account_settings(values)
    persist_config_values(config_path, active_config, normalized)
    changed = [
        name for name, value in normalized.items()
        if before[name] != value
    ]
    return {"values": normalized, "before": before, "changed": changed}

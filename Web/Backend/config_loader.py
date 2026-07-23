"""
Configuration loading for ColorVision Marketplace.

Loads config.json over DEFAULT_CONFIG and provides validation helpers.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

BASE_DIR = Path(__file__).resolve().parent

DEFAULT_CONFIG: dict[str, Any] = {
    "storage_path": str(BASE_DIR / "storage"),
    "host": "0.0.0.0",
    "port": 9998,
    "debug": False,
    "secret_key": "change-this-in-production",
    "app_release_keep_count": 5,
    "plugin_package_keep_count": 3,
    "transfer_upload_dir": "Transfer",
    "access_analytics_enabled": True,
    "access_analytics_queue_size": 4096,
    "access_analytics_batch_size": 128,
    "access_analytics_flush_interval_seconds": 0.5,
    "access_analytics_retention_days": 90,
    "upload_auth": {"username": "admin", "password": "admin"},
}

DEFAULT_SECRET_KEY: str = DEFAULT_CONFIG["secret_key"]
DEFAULT_UPLOAD_AUTH: dict[str, str] = dict(DEFAULT_CONFIG["upload_auth"])

MAX_UPLOAD_SIZE_BYTES = 500 * 1024 * 1024  # 500 MB
MAX_FEEDBACK_FILES = 10
MAX_FEEDBACK_FIELD_LENGTH = 4000


def load_config() -> dict[str, Any]:
    """Load configuration by merging config.json over DEFAULT_CONFIG."""
    config = dict(DEFAULT_CONFIG)
    config["upload_auth"] = dict(DEFAULT_UPLOAD_AUTH)
    config_file = BASE_DIR / "config.json"
    if config_file.exists():
        with open(config_file, encoding="utf-8") as f:
            loaded = json.load(f)
        if isinstance(loaded.get("upload_auth"), dict):
            config["upload_auth"].update(loaded["upload_auth"])
        for key, value in loaded.items():
            if key == "upload_auth":
                continue
            config[key] = value
    return config


def get_upload_auth(config: dict[str, Any]) -> tuple[str, str]:
    """Extract upload credentials from config."""
    auth_config = config.get("upload_auth") or {}
    if not isinstance(auth_config, dict):
        auth_config = {}
    username = str(auth_config.get("username", "")).strip()
    password = str(auth_config.get("password", ""))
    return username, password


def validate_runtime_config(
    config: dict[str, Any],
    *,
    default_secret_key: str = DEFAULT_SECRET_KEY,
    default_upload_auth: dict[str, str] | None = None,
) -> list[str]:
    """Check for insecure default configuration values. Returns list of issues."""
    if default_upload_auth is None:
        default_upload_auth = DEFAULT_UPLOAD_AUTH
    from runtime_health import validate_runtime_config as _validate
    return _validate(
        config,
        default_secret_key=default_secret_key,
        default_upload_auth=default_upload_auth,
    )

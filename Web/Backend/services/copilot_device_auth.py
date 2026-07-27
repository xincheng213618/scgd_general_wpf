"""Validate signed ColorVision desktop identity headers for Copilot sync."""

from __future__ import annotations

import hashlib
import hmac
import re
import time
from dataclasses import dataclass
from typing import Any, Mapping


_VERSION_PATTERN = re.compile(r"^\d+(?:\.\d+){1,3}$")
_DEVICE_ID_PATTERN = re.compile(r"^[0-9A-Fa-f]{64}$")
_NONCE_PATTERN = re.compile(r"^[0-9a-f]{32}$")
_SIGNATURE_PATTERN = re.compile(r"^[0-9a-f]{64}$")
_MAX_CLOCK_SKEW_SECONDS = 300


@dataclass(frozen=True)
class CopilotDeviceIdentity:
    product: str
    app_version: str
    device_id: str
    os_version: str
    architecture: str


@dataclass(frozen=True)
class CopilotDeviceAuthResult:
    identity: CopilotDeviceIdentity | None
    error: str = ""
    status: int = 401

    @property
    def authorized(self) -> bool:
        return self.identity is not None


def verify_copilot_device(
    config: Mapping[str, Any],
    headers: Mapping[str, str],
    *,
    now: int | None = None,
) -> CopilotDeviceAuthResult:
    version_keys = _configured_version_keys(config)
    if not version_keys:
        return CopilotDeviceAuthResult(
            None,
            "ColorVision device sync is not configured",
            503,
        )

    product = _header(headers, "X-ColorVision-Product")
    app_version = _header(headers, "X-ColorVision-Version")
    device_id = _header(headers, "X-ColorVision-Device-Id")
    os_version = _header(headers, "X-ColorVision-OS-Version")
    architecture = _header(headers, "X-ColorVision-Architecture")
    timestamp_text = _header(headers, "X-ColorVision-Timestamp")
    nonce = _header(headers, "X-ColorVision-Nonce")
    signature = _header(headers, "X-ColorVision-Signature").lower()

    if product != "ColorVision":
        return CopilotDeviceAuthResult(None, "ColorVision device proof required")
    if not _VERSION_PATTERN.fullmatch(app_version):
        return CopilotDeviceAuthResult(None, "Invalid ColorVision application version")
    if not _DEVICE_ID_PATTERN.fullmatch(device_id):
        return CopilotDeviceAuthResult(None, "Invalid ColorVision device id")
    if not os_version or len(os_version) > 64:
        return CopilotDeviceAuthResult(None, "Invalid ColorVision OS version")
    if architecture not in {"X64", "Arm64"}:
        return CopilotDeviceAuthResult(None, "Unsupported ColorVision architecture")
    if not _NONCE_PATTERN.fullmatch(nonce):
        return CopilotDeviceAuthResult(None, "Invalid ColorVision request nonce")
    if not _SIGNATURE_PATTERN.fullmatch(signature):
        return CopilotDeviceAuthResult(None, "Invalid ColorVision device signature")

    try:
        timestamp = int(timestamp_text)
    except (TypeError, ValueError):
        return CopilotDeviceAuthResult(None, "Invalid ColorVision request timestamp")
    current = int(time.time()) if now is None else int(now)
    if abs(current - timestamp) > _MAX_CLOCK_SKEW_SECONDS:
        return CopilotDeviceAuthResult(None, "Expired ColorVision device proof")

    canonical = "\n".join((
        product,
        app_version,
        device_id,
        os_version,
        architecture,
        timestamp_text,
        nonce,
    )).encode("utf-8")
    if not any(
        hmac.compare_digest(
            hmac.new(key.encode("utf-8"), canonical, hashlib.sha256).hexdigest(),
            signature,
        )
        for key in version_keys
    ):
        return CopilotDeviceAuthResult(None, "Invalid ColorVision device signature")

    return CopilotDeviceAuthResult(CopilotDeviceIdentity(
        product=product,
        app_version=app_version,
        device_id=device_id.upper(),
        os_version=os_version,
        architecture=architecture,
    ))


def _configured_version_keys(config: Mapping[str, Any]) -> tuple[str, ...]:
    sync_config = config.get("copilot_sync") or {}
    if not isinstance(sync_config, Mapping):
        return ()
    configured = sync_config.get("version_keys") or []
    if isinstance(configured, str):
        configured = [configured]
    if not isinstance(configured, (list, tuple)):
        return ()
    keys = tuple(
        str(item).strip()
        for item in configured[:16]
        if str(item).strip()
    )
    return keys


def _header(headers: Mapping[str, str], name: str) -> str:
    value = str(headers.get(name, "") or "").strip()
    if len(value) > 256 or "\r" in value or "\n" in value:
        return ""
    return value

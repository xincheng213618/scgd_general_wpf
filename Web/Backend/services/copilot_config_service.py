"""Persistence and validation for centrally managed Copilot model profiles."""

from __future__ import annotations

import base64
import hashlib
import json
import os
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from ipaddress import ip_address
from typing import Any, Callable
from urllib.parse import urlparse

from cryptography.hazmat.primitives.ciphers.aead import AESGCM

from db_cache import CacheManager


VENDOR_TYPES = {
    "Custom",
    "DeepSeek",
    "OpenAI",
    "Claude",
    "Grok",
    "Gemini",
    "GLM",
    "MiniMax",
    "Xiaomi",
    "SenseNova",
}
PROVIDER_TYPES = {"OpenAICompatible", "AnthropicCompatible"}
REASONING_MODES = {"Default", "Disabled", "Enabled", "High", "Max"}
_ENCRYPTION_PREFIX = "aesgcm:v1:"
_ENCRYPTION_CONTEXT = b"ColorVision.Copilot.Profile.ApiKey.v1"


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def _normalize_enum(value: Any, allowed: set[str], field_name: str) -> str:
    text = str(value or "").strip()
    match = next((item for item in allowed if item.casefold() == text.casefold()), None)
    if match is None:
        raise ValueError(f"{field_name} is invalid")
    return match


def _normalize_text(value: Any, field_name: str, *, max_length: int) -> str:
    text = str(value or "").strip()
    if not text:
        raise ValueError(f"{field_name} is required")
    if len(text) > max_length:
        raise ValueError(f"{field_name} is too long")
    return text


def _is_loopback_host(hostname: str) -> bool:
    if hostname.casefold() == "localhost":
        return True
    try:
        return ip_address(hostname).is_loopback
    except ValueError:
        return False


def _normalize_base_url(value: Any, allow_insecure_http: bool) -> str:
    base_url = _normalize_text(value, "baseUrl", max_length=2048).rstrip("/")
    parsed = urlparse(base_url)
    if parsed.scheme not in {"http", "https"} or not parsed.hostname:
        raise ValueError("baseUrl must be an absolute HTTP or HTTPS URL")
    if parsed.username or parsed.password or parsed.query or parsed.fragment:
        raise ValueError("baseUrl cannot contain credentials, a query, or a fragment")
    if parsed.scheme == "http" and not _is_loopback_host(parsed.hostname) and not allow_insecure_http:
        raise ValueError("Remote HTTP model endpoints require allowInsecureHttp")
    return base_url


@dataclass(frozen=True)
class CopilotProfileInput:
    name: str
    vendor_type: str
    provider_type: str
    base_url: str
    model: str
    allow_insecure_http: bool
    reasoning_mode: str
    is_enabled: bool
    is_default: bool
    sort_order: int
    api_key: str | None

    @classmethod
    def from_payload(cls, payload: dict[str, Any], *, require_api_key: bool) -> "CopilotProfileInput":
        allow_insecure_http = bool(payload.get("allowInsecureHttp", False))
        raw_api_key = payload.get("apiKey")
        api_key = None if raw_api_key is None or str(raw_api_key).strip() == "" else str(raw_api_key).strip()
        if require_api_key and not api_key:
            raise ValueError("apiKey is required")
        if api_key is not None and len(api_key) > 8192:
            raise ValueError("apiKey is too long")
        try:
            sort_order = int(payload.get("sortOrder", 0))
        except (TypeError, ValueError) as exc:
            raise ValueError("sortOrder must be an integer") from exc
        if sort_order < -100000 or sort_order > 100000:
            raise ValueError("sortOrder must be between -100000 and 100000")

        return cls(
            name=_normalize_text(payload.get("name"), "name", max_length=200),
            vendor_type=_normalize_enum(payload.get("vendorType"), VENDOR_TYPES, "vendorType"),
            provider_type=_normalize_enum(payload.get("providerType"), PROVIDER_TYPES, "providerType"),
            base_url=_normalize_base_url(payload.get("baseUrl"), allow_insecure_http),
            model=_normalize_text(payload.get("model"), "model", max_length=300),
            allow_insecure_http=allow_insecure_http,
            reasoning_mode=_normalize_enum(payload.get("reasoningMode", "Default"), REASONING_MODES, "reasoningMode"),
            is_enabled=bool(payload.get("enabled", True)),
            is_default=bool(payload.get("isDefault", False)),
            sort_order=sort_order,
            api_key=api_key,
        )


class CopilotConfigService:
    def __init__(self, cache: CacheManager, secret_key_getter: Callable[[], str]):
        self._cache = cache
        self._secret_key_getter = secret_key_getter

    def list_admin_profiles(self) -> list[dict[str, Any]]:
        db = self._cache.get_db()
        try:
            rows = db.execute(
                "SELECT * FROM copilot_profiles ORDER BY sort_order, name COLLATE NOCASE, id"
            ).fetchall()
            return [self._serialize_admin(row) for row in rows]
        finally:
            db.close()

    def list_client_profiles(self) -> dict[str, Any]:
        db = self._cache.get_db()
        try:
            rows = db.execute(
                """SELECT * FROM copilot_profiles
                   WHERE is_enabled = 1
                   ORDER BY is_default DESC, sort_order, name COLLATE NOCASE, id"""
            ).fetchall()
            profiles = [self._serialize_client(row) for row in rows]
        finally:
            db.close()

        revision_source = json.dumps(
            [(item["id"], item["updatedAt"]) for item in profiles],
            ensure_ascii=True,
            separators=(",", ":"),
        )
        revision = hashlib.sha256(revision_source.encode("utf-8")).hexdigest()[:24]
        default_profile = next((item["id"] for item in profiles if item["isDefault"]), None)
        return {
            "schemaVersion": 1,
            "revision": revision,
            "generatedAt": _now_iso(),
            "defaultProfileId": default_profile,
            "profiles": profiles,
        }

    def create_profile(self, payload: dict[str, Any]) -> dict[str, Any]:
        profile = CopilotProfileInput.from_payload(payload, require_api_key=True)
        profile_id = uuid.uuid4().hex
        now = _now_iso()
        encrypted_api_key = self._encrypt(profile.api_key or "")
        db = self._cache.get_db()
        try:
            db.execute("BEGIN IMMEDIATE")
            if profile.is_default:
                db.execute("UPDATE copilot_profiles SET is_default = 0")
            db.execute(
                """INSERT INTO copilot_profiles
                   (id, name, vendor_type, provider_type, base_url, model,
                    api_key_encrypted, allow_insecure_http, reasoning_mode,
                    is_enabled, is_default, sort_order, created_at, updated_at)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                (
                    profile_id,
                    profile.name,
                    profile.vendor_type,
                    profile.provider_type,
                    profile.base_url,
                    profile.model,
                    encrypted_api_key,
                    int(profile.allow_insecure_http),
                    profile.reasoning_mode,
                    int(profile.is_enabled),
                    int(profile.is_default),
                    profile.sort_order,
                    now,
                    now,
                ),
            )
            row = db.execute("SELECT * FROM copilot_profiles WHERE id = ?", (profile_id,)).fetchone()
            db.commit()
            return self._serialize_admin(row)
        except Exception:
            db.rollback()
            raise
        finally:
            db.close()

    def update_profile(self, profile_id: str, payload: dict[str, Any]) -> dict[str, Any] | None:
        profile_id = self._normalize_id(profile_id)
        profile = CopilotProfileInput.from_payload(payload, require_api_key=False)
        db = self._cache.get_db()
        try:
            existing = db.execute(
                "SELECT * FROM copilot_profiles WHERE id = ?", (profile_id,)
            ).fetchone()
            if existing is None:
                return None
            encrypted_api_key = (
                self._encrypt(profile.api_key)
                if profile.api_key is not None
                else existing["api_key_encrypted"]
            )
            if profile.is_enabled and not encrypted_api_key:
                raise ValueError("An enabled profile requires apiKey")

            db.execute("BEGIN IMMEDIATE")
            if profile.is_default:
                db.execute("UPDATE copilot_profiles SET is_default = 0 WHERE id <> ?", (profile_id,))
            db.execute(
                """UPDATE copilot_profiles SET
                   name = ?, vendor_type = ?, provider_type = ?, base_url = ?,
                   model = ?, api_key_encrypted = ?, allow_insecure_http = ?,
                   reasoning_mode = ?, is_enabled = ?, is_default = ?,
                   sort_order = ?, updated_at = ?
                   WHERE id = ?""",
                (
                    profile.name,
                    profile.vendor_type,
                    profile.provider_type,
                    profile.base_url,
                    profile.model,
                    encrypted_api_key,
                    int(profile.allow_insecure_http),
                    profile.reasoning_mode,
                    int(profile.is_enabled),
                    int(profile.is_default),
                    profile.sort_order,
                    _now_iso(),
                    profile_id,
                ),
            )
            row = db.execute("SELECT * FROM copilot_profiles WHERE id = ?", (profile_id,)).fetchone()
            db.commit()
            return self._serialize_admin(row)
        except Exception:
            db.rollback()
            raise
        finally:
            db.close()

    def delete_profile(self, profile_id: str) -> bool:
        profile_id = self._normalize_id(profile_id)
        db = self._cache.get_db()
        try:
            cursor = db.execute("DELETE FROM copilot_profiles WHERE id = ?", (profile_id,))
            db.commit()
            return cursor.rowcount > 0
        finally:
            db.close()

    @staticmethod
    def _normalize_id(profile_id: str) -> str:
        text = str(profile_id or "").strip().lower()
        if len(text) != 32 or any(character not in "0123456789abcdef" for character in text):
            raise ValueError("profile id is invalid")
        return text

    def _encryption_key(self) -> bytes:
        secret_key = str(self._secret_key_getter() or "").strip()
        if not secret_key:
            raise RuntimeError("Web secret_key is required to protect Copilot API keys")
        return hashlib.sha256(_ENCRYPTION_CONTEXT + secret_key.encode("utf-8")).digest()

    def _encrypt(self, value: str) -> str:
        nonce = os.urandom(12)
        ciphertext = AESGCM(self._encryption_key()).encrypt(
            nonce,
            value.encode("utf-8"),
            _ENCRYPTION_CONTEXT,
        )
        payload = base64.urlsafe_b64encode(nonce + ciphertext).decode("ascii")
        return _ENCRYPTION_PREFIX + payload

    def _decrypt(self, value: str) -> str:
        if not value.startswith(_ENCRYPTION_PREFIX):
            raise RuntimeError("Copilot API key has an unsupported encryption format")
        raw = base64.urlsafe_b64decode(value[len(_ENCRYPTION_PREFIX):].encode("ascii"))
        if len(raw) < 29:
            raise RuntimeError("Copilot API key ciphertext is invalid")
        plaintext = AESGCM(self._encryption_key()).decrypt(
            raw[:12],
            raw[12:],
            _ENCRYPTION_CONTEXT,
        )
        return plaintext.decode("utf-8")

    @staticmethod
    def _serialize_admin(row: Any) -> dict[str, Any]:
        encrypted = str(row["api_key_encrypted"] or "")
        return {
            "id": row["id"],
            "name": row["name"],
            "vendorType": row["vendor_type"],
            "providerType": row["provider_type"],
            "baseUrl": row["base_url"],
            "model": row["model"],
            "hasApiKey": bool(encrypted),
            "allowInsecureHttp": bool(row["allow_insecure_http"]),
            "reasoningMode": row["reasoning_mode"],
            "enabled": bool(row["is_enabled"]),
            "isDefault": bool(row["is_default"]),
            "sortOrder": int(row["sort_order"]),
            "createdAt": row["created_at"],
            "updatedAt": row["updated_at"],
        }

    def _serialize_client(self, row: Any) -> dict[str, Any]:
        profile = self._serialize_admin(row)
        profile.pop("hasApiKey", None)
        profile["apiKey"] = self._decrypt(str(row["api_key_encrypted"]))
        return profile

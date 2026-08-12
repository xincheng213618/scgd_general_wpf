"""
API Key lifecycle service for ColorVision Marketplace.

Key format: cvmp_<prefix>_<secret>
  - prefix: 8 chars, stored in DB for lookup
  - secret: 32 chars, only shown once; DB stores hash

Supports scopes, expiration, revocation, rotation, and last_used_at tracking.
"""

from __future__ import annotations

import hashlib
import hmac
import secrets
from datetime import datetime, timedelta, timezone
from typing import Any

from db_cache import CacheManager

try:
    from werkzeug.security import check_password_hash, generate_password_hash
except ImportError:  # pragma: no cover
    generate_password_hash = None
    check_password_hash = None

KEY_PREFIX = "cvmp"
LAST_USED_WRITE_INTERVAL = timedelta(minutes=1)

API_KEY_SCOPE_DEFINITIONS: tuple[dict[str, str], ...] = (
    {"value": "admin:*", "label": "完整管理权限", "description": "访问全部管理接口；只应用于受控管理员自动化。", "category": "管理", "access": "admin"},
    {"value": "cache:read", "label": "缓存与索引读取", "description": "读取缓存、索引和文档构建状态。", "category": "系统运维", "access": "read"},
    {"value": "cache:refresh", "label": "缓存与索引维护", "description": "刷新索引并清理过期缓存。", "category": "系统运维", "access": "write"},
    {"value": "jobs:read", "label": "任务查看", "description": "读取任务状态和运行历史。", "category": "系统运维", "access": "read"},
    {"value": "jobs:write", "label": "任务执行", "description": "运行、启用或禁用后台任务。", "category": "系统运维", "access": "write"},
    {"value": "stats:read", "label": "统计查看", "description": "读取概览、访问统计和性能摘要。", "category": "系统运维", "access": "read"},
    {"value": "plugin:read", "label": "插件目录读取", "description": "读取插件目录与公开元数据。", "category": "发布", "access": "read"},
    {"value": "plugin:publish", "label": "插件发布", "description": "上传并发布插件安装包。", "category": "发布", "access": "write"},
    {"value": "release:publish", "label": "主程序发布", "description": "上传并发布主程序版本。", "category": "发布", "access": "write"},
    {"value": "file:transfer", "label": "文件中转", "description": "上传、下载、列出和删除中转文件。", "category": "文件", "access": "write"},
    {"value": "ops:relay", "label": "桌面 Relay", "description": "桌面端心跳、任务拉取、回执和受限支持事件。", "category": "桌面运维", "access": "service"},
    {"value": "ops:operator", "label": "运维调度", "description": "查看主机并创建目录约束的桌面运维任务。", "category": "桌面运维", "access": "write"},
    {"value": "copilot:config:read", "label": "Copilot 配置同步", "description": "读取启用的 Copilot 配置。", "category": "Copilot", "access": "read"},
)
ALLOWED_SCOPES = frozenset(item["value"] for item in API_KEY_SCOPE_DEFINITIONS)
DEFAULT_API_KEY_SCOPES = ("stats:read",)


def list_api_key_scope_definitions() -> list[dict[str, str]]:
    return [dict(item) for item in API_KEY_SCOPE_DEFINITIONS]


def validate_api_key_scopes(scopes_str: str) -> tuple[list[str], list[str]]:
    requested = {scope.strip() for scope in scopes_str.split(",") if scope.strip()}
    invalid = sorted(requested - ALLOWED_SCOPES)
    valid = sorted(requested & ALLOWED_SCOPES)
    return valid, invalid


def api_key_actor_id(key_info: Any) -> str:
    try:
        prefix = str(key_info["key_prefix"] or "").strip()
    except (KeyError, TypeError, IndexError):
        prefix = ""
    return f"key:{prefix or 'unknown'}"


def _utc_now() -> datetime:
    return datetime.now(timezone.utc)


def _now_iso() -> str:
    return _utc_now().isoformat()


def normalize_api_key_expiry(expires_at: str | None) -> str | None:
    """Validate an optional ISO-8601 expiry and normalize it to UTC."""
    if expires_at is None:
        return None
    value = str(expires_at).strip()
    if not value:
        return None
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except (TypeError, ValueError) as exc:
        raise ValueError("expires_at must be a valid ISO 8601 timestamp") from exc
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc).isoformat()


def api_key_status(row: Any, *, now: datetime | None = None) -> str:
    """Return the credential's effective status, including expiry validity."""
    if not bool(row["is_active"]) or row["revoked_at"]:
        return "revoked"
    expires_at = row["expires_at"]
    if not expires_at:
        return "active"
    try:
        expiry = datetime.fromisoformat(
            normalize_api_key_expiry(str(expires_at)) or ""
        )
    except (TypeError, ValueError):
        return "invalid_expiry"
    current = now or _utc_now()
    if current.tzinfo is None:
        current = current.replace(tzinfo=timezone.utc)
    return "expired" if current.astimezone(timezone.utc) >= expiry else "active"


def _public_key_payload(row: Any, *, now: datetime | None = None) -> dict[str, Any]:
    key = dict(row)
    key.pop("key_hash", None)
    key["status"] = api_key_status(row, now=now)
    return key


def _last_used_write_due(last_used_at: str | None, now: datetime) -> bool:
    if not last_used_at:
        return True
    try:
        previous = datetime.fromisoformat(last_used_at)
        if previous.tzinfo is None:
            previous = previous.replace(tzinfo=timezone.utc)
    except (TypeError, ValueError):
        return True
    elapsed = now - previous
    return elapsed < timedelta(0) or elapsed >= LAST_USED_WRITE_INTERVAL


def _refresh_last_used_at(db, row, now: datetime) -> str | None:
    previous = row["last_used_at"]
    if not _last_used_write_due(previous, now):
        return previous

    updated = now.isoformat()
    cursor = db.execute(
        """UPDATE api_keys SET last_used_at = ?
           WHERE id = ? AND COALESCE(last_used_at, '') = ?""",
        (updated, row["id"], str(previous or "")),
    )
    if cursor.rowcount:
        db.commit()
        return updated
    return previous


def _generate_key() -> tuple[str, str, str]:
    """Generate an API key. Returns (full_key, prefix, secret)."""
    prefix = secrets.token_hex(4)  # 8 chars
    secret = secrets.token_hex(16)  # 32 chars
    full_key = f"{KEY_PREFIX}_{prefix}_{secret}"
    return full_key, prefix, secret


def _hash_secret(secret: str) -> str:
    """Hash the secret part of the key."""
    if generate_password_hash is not None:
        return generate_password_hash(secret)
    # Fallback: SHA-256 (not ideal but functional)
    return hashlib.sha256(secret.encode("utf-8")).hexdigest()


def _verify_secret(secret: str, key_hash: str) -> bool:
    """Verify a secret against its hash."""
    if check_password_hash is not None:
        try:
            return check_password_hash(key_hash, secret)
        except Exception:
            pass
    # Fallback: SHA-256 comparison
    return hmac.compare_digest(hashlib.sha256(secret.encode("utf-8")).hexdigest(), key_hash)


def create_api_key(
    cache: CacheManager,
    *,
    name: str,
    description: str = "",
    scopes: str = "",
    created_by: str = "",
    expires_at: str | None = None,
) -> dict[str, Any]:
    """Create a new API key. Returns dict with full_key (shown only once)."""
    normalized_expiry = normalize_api_key_expiry(expires_at)
    current = _utc_now()
    if (
        normalized_expiry is not None
        and datetime.fromisoformat(normalized_expiry) <= current
    ):
        raise ValueError("expires_at must be in the future")
    full_key, prefix, secret = _generate_key()
    key_hash = _hash_secret(secret)
    now = current.isoformat()

    db = cache.get_db()
    try:
        cursor = db.execute(
            """INSERT INTO api_keys (name, description, key_prefix, key_hash, scopes,
                                     created_by, created_at, expires_at, is_active)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1)""",
            (name, description, prefix, key_hash, scopes, created_by, now, normalized_expiry),
        )
        key_id = cursor.lastrowid
        db.commit()
    finally:
        db.close()

    return {
        "id": key_id,
        "name": name,
        "description": description,
        "key": full_key,  # Only returned once!
        "key_prefix": prefix,
        "scopes": scopes,
        "created_by": created_by,
        "created_at": now,
        "expires_at": normalized_expiry,
        "status": "active",
    }


def verify_api_key(
    cache: CacheManager,
    key: str,
    *,
    required_scopes: list[str] | None = None,
) -> dict[str, Any] | None:
    """Verify an API key string. Returns key record or None if invalid."""
    # Parse key format: cvmp_<prefix>_<secret>
    parts = key.split("_", 2)
    if len(parts) != 3 or parts[0] != KEY_PREFIX:
        return None

    prefix = parts[1]
    secret = parts[2]

    db = cache.get_db()
    try:
        row = db.execute(
            "SELECT * FROM api_keys WHERE key_prefix = ? AND is_active = 1",
            (prefix,),
        ).fetchone()
        if not row:
            return None

        now = _utc_now()
        if api_key_status(row, now=now) != "active":
            return None

        # Verify secret only after cheap revocation and expiry checks.
        if not _verify_secret(secret, row["key_hash"]):
            return None

        # Check scopes
        if required_scopes:
            key_scopes = set(s.strip() for s in (row["scopes"] or "").split(",") if s.strip())
            if "admin:*" not in key_scopes:
                for scope in required_scopes:
                    if scope not in key_scopes:
                        return None

        key_info = _public_key_payload(row, now=now)
        try:
            key_info["last_used_at"] = _refresh_last_used_at(db, row, now)
        except Exception as exc:
            # Usage metadata is advisory. A transient write lock must not turn
            # an already verified credential into an authentication failure.
            print(f"[api_key] last-used refresh skipped for prefix '{prefix}': {exc}")
        return key_info
    except Exception as exc:
        print(f"[api_key] verify failed for prefix '{prefix}': {exc}")
        return None
    finally:
        db.close()


def revoke_api_key(
    cache: CacheManager,
    key_id: int,
) -> bool:
    """Revoke an API key by ID."""
    now = _now_iso()
    db = cache.get_db()
    try:
        cursor = db.execute(
            "UPDATE api_keys SET is_active = 0, revoked_at = ? WHERE id = ? AND is_active = 1",
            (now, key_id),
        )
        db.commit()
        return cursor.rowcount > 0
    finally:
        db.close()


def rotate_api_key(
    cache: CacheManager,
    key_id: int,
    *,
    created_by: str = "",
) -> dict[str, Any] | None:
    """Rotate an API key: revoke old, create new with same name/scopes."""
    db = cache.get_db()
    try:
        row = db.execute("SELECT * FROM api_keys WHERE id = ?", (key_id,)).fetchone()
        if not row:
            return None
    finally:
        db.close()

    # Revoke old key
    revoke_api_key(cache, key_id)

    # Create new key with same settings
    return create_api_key(
        cache,
        name=row["name"],
        description=row["description"] or "",
        scopes=row["scopes"] or "",
        created_by=created_by or row["created_by"] or "",
        expires_at=row["expires_at"],
    )


def list_api_keys(cache: CacheManager) -> list[dict[str, Any]]:
    """List all API keys (without key_hash)."""
    db = cache.get_db()
    try:
        rows = db.execute("SELECT * FROM api_keys ORDER BY id DESC").fetchall()
        now = _utc_now()
        return [_public_key_payload(row, now=now) for row in rows]
    except Exception:
        return []
    finally:
        db.close()


def get_api_key_usage(
    cache: CacheManager,
    key_id: int,
    *,
    recent_limit: int = 20,
) -> dict[str, Any] | None:
    """Get public key metadata plus its recent audited operations."""
    db = cache.get_db()
    try:
        row = db.execute("SELECT * FROM api_keys WHERE id = ?", (key_id,)).fetchone()
        if not row:
            return None
        public = _public_key_payload(row)
        actor_ids = (api_key_actor_id(row), str(key_id))
        total_row = db.execute(
            """SELECT COUNT(*) AS total FROM audit_log
               WHERE actor_type = 'api_key' AND actor_id IN (?, ?)""",
            actor_ids,
        ).fetchone()
        rows = db.execute(
            """SELECT action, target_type, target_id, detail, created_at
               FROM audit_log
               WHERE actor_type = 'api_key' AND actor_id IN (?, ?)
               ORDER BY id DESC LIMIT ?""",
            (*actor_ids, max(1, min(int(recent_limit), 50))),
        ).fetchall()
        public["audit_activity"] = {
            "total": int(total_row["total"]) if total_row else 0,
            "items": [dict(item) for item in rows],
        }
        return public
    except Exception:
        return None
    finally:
        db.close()

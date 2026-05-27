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
from datetime import datetime, timezone
from typing import Any

from db_cache import CacheManager

try:
    from werkzeug.security import check_password_hash, generate_password_hash
except ImportError:  # pragma: no cover
    generate_password_hash = None
    check_password_hash = None

KEY_PREFIX = "cvmp"


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


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
    scopes: str = "",
    created_by: str = "",
    expires_at: str | None = None,
) -> dict[str, Any]:
    """Create a new API key. Returns dict with full_key (shown only once)."""
    full_key, prefix, secret = _generate_key()
    key_hash = _hash_secret(secret)
    now = _now_iso()

    db = cache.get_db()
    try:
        cursor = db.execute(
            """INSERT INTO api_keys (name, key_prefix, key_hash, scopes, created_by,
                                     created_at, expires_at, is_active)
               VALUES (?, ?, ?, ?, ?, ?, ?, 1)""",
            (name, prefix, key_hash, scopes, created_by, now, expires_at),
        )
        key_id = cursor.lastrowid
        db.commit()
    finally:
        db.close()

    return {
        "id": key_id,
        "name": name,
        "key": full_key,  # Only returned once!
        "key_prefix": prefix,
        "scopes": scopes,
        "created_by": created_by,
        "created_at": now,
        "expires_at": expires_at,
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

        # Verify secret
        if not _verify_secret(secret, row["key_hash"]):
            return None

        # Check expiration
        if row["expires_at"]:
            try:
                exp = datetime.fromisoformat(row["expires_at"])
                if exp.tzinfo is None:
                    exp = exp.replace(tzinfo=timezone.utc)
                if datetime.now(timezone.utc) > exp:
                    return None
            except (ValueError, TypeError):
                pass

        # Check scopes
        if required_scopes:
            key_scopes = set(s.strip() for s in (row["scopes"] or "").split(",") if s.strip())
            if "admin:*" not in key_scopes:
                for scope in required_scopes:
                    if scope not in key_scopes:
                        return None

        # Update last_used_at
        now = _now_iso()
        db.execute(
            "UPDATE api_keys SET last_used_at = ? WHERE id = ?",
            (now, row["id"]),
        )
        db.commit()

        return dict(row)
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
        scopes=row["scopes"] or "",
        created_by=created_by or row["created_by"] or "",
        expires_at=row["expires_at"],
    )


def list_api_keys(cache: CacheManager) -> list[dict[str, Any]]:
    """List all API keys (without key_hash)."""
    db = cache.get_db()
    try:
        rows = db.execute("SELECT * FROM api_keys ORDER BY id DESC").fetchall()
        keys = []
        for row in rows:
            key = dict(row)
            key.pop("key_hash", None)
            keys.append(key)
        return keys
    except Exception:
        return []
    finally:
        db.close()


def get_api_key_usage(cache: CacheManager, key_id: int) -> dict[str, Any] | None:
    """Get usage info for a specific API key."""
    db = cache.get_db()
    try:
        row = db.execute("SELECT * FROM api_keys WHERE id = ?", (key_id,)).fetchone()
        if not row:
            return None
        key = dict(row)
        key.pop("key_hash", None)
        return key
    except Exception:
        return None
    finally:
        db.close()

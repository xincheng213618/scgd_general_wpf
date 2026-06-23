"""
Authentication service for ColorVision Marketplace.

Manages user accounts with password hashing via werkzeug.security.
Falls back to config.json upload_auth when users table is empty.
"""

from __future__ import annotations

import re
from datetime import datetime, timezone
from typing import Any

from db_cache import CacheManager

try:
    from werkzeug.security import check_password_hash, generate_password_hash
except ImportError:  # pragma: no cover
    generate_password_hash = None
    check_password_hash = None


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


USERNAME_PATTERN = re.compile(r"^[A-Za-z0-9_.-]{3,32}$")
MIN_PASSWORD_LENGTH = 6


def normalize_username(username: str) -> str:
    return username.strip()


def validate_registration(username: str, password: str) -> str | None:
    username = normalize_username(username)
    if not username:
        return "请输入用户名"
    if not USERNAME_PATTERN.match(username):
        return "用户名只能使用 3-32 位字母、数字、下划线、点或连字符"
    if len(password) < MIN_PASSWORD_LENGTH:
        return f"密码至少需要 {MIN_PASSWORD_LENGTH} 位"
    return None


def ensure_admin_user(
    cache: CacheManager,
    config: dict[str, Any],
):
    """If users table is empty, create an admin user from config upload_auth."""
    db = cache.get_db()
    try:
        row = db.execute("SELECT COUNT(*) AS cnt FROM users").fetchone()
        if row and row["cnt"] > 0:
            return  # users already exist

        auth_config = config.get("upload_auth") or {}
        username = str(auth_config.get("username", "")).strip()
        password = str(auth_config.get("password", ""))

        if not username or not password:
            return

        if generate_password_hash is None:
            print("[auth] werkzeug not available, skipping admin user creation")
            return

        pw_hash = generate_password_hash(password)
        now = _now_iso()
        db.execute(
            """INSERT OR IGNORE INTO users (username, password_hash, role, is_active, created_at, updated_at)
               VALUES (?, ?, 'admin', 1, ?, ?)""",
            (username, pw_hash, now, now),
        )
        db.commit()
        print(f"[auth] Created admin user '{username}' from config")
    except Exception as exc:
        print(f"[auth] ensure_admin_user failed: {exc}")
    finally:
        db.close()


def create_user(
    cache: CacheManager,
    username: str,
    password: str,
    *,
    role: str = "user",
) -> tuple[dict[str, Any] | None, str | None]:
    """Create a normal user account. Returns (user, error_message)."""
    if generate_password_hash is None:
        return None, "密码服务不可用"

    username = normalize_username(username)
    validation_error = validate_registration(username, password)
    if validation_error:
        return None, validation_error

    normalized_role = role if role in {"admin", "user"} else "user"
    pw_hash = generate_password_hash(password)
    now = _now_iso()
    db = cache.get_db()
    try:
        existing = db.execute(
            "SELECT id FROM users WHERE lower(username) = lower(?)",
            (username,),
        ).fetchone()
        if existing:
            return None, "用户名已存在"

        cursor = db.execute(
            """INSERT INTO users (username, password_hash, role, is_active, created_at, updated_at)
               VALUES (?, ?, ?, 1, ?, ?)""",
            (username, pw_hash, normalized_role, now, now),
        )
        db.commit()
        row = db.execute("SELECT * FROM users WHERE id = ?", (cursor.lastrowid,)).fetchone()
        user = dict(row)
        user.pop("password_hash", None)
        return user, None
    except Exception:
        return None, "注册失败"
    finally:
        db.close()


def verify_user_credentials(
    cache: CacheManager,
    username: str,
    password: str,
) -> dict[str, Any] | None:
    """Verify username/password against users table. Returns user dict or None."""
    if check_password_hash is None:
        return None

    username = normalize_username(username)
    db = cache.get_db()
    try:
        row = db.execute(
            "SELECT * FROM users WHERE username = ? AND is_active = 1",
            (username,),
        ).fetchone()
        if not row:
            return None

        if not check_password_hash(row["password_hash"], password):
            return None

        # Update last_login_at
        now = _now_iso()
        db.execute(
            "UPDATE users SET last_login_at = ? WHERE id = ?",
            (now, row["id"]),
        )
        db.commit()

        return dict(row)
    except Exception:
        return None
    finally:
        db.close()


def list_users(cache: CacheManager) -> list[dict[str, Any]]:
    """List all users (without password_hash)."""
    db = cache.get_db()
    try:
        rows = db.execute("SELECT * FROM users ORDER BY id").fetchall()
        users = []
        for row in rows:
            user = dict(row)
            user.pop("password_hash", None)
            users.append(user)
        return users
    except Exception:
        return []
    finally:
        db.close()

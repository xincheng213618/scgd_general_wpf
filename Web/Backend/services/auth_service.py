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
VALID_USER_ROLES = frozenset({"admin", "user"})


def _user_payload(row) -> dict[str, Any]:
    user = dict(row)
    user.pop("password_hash", None)
    user["is_active"] = bool(user.get("is_active"))
    return user


def normalize_username(username: str) -> str:
    return username.strip()


def validate_registration(username: str, password: str) -> str | None:
    username = normalize_username(username)
    if not username:
        return "请输入用户名"
    if not USERNAME_PATTERN.match(username):
        return "用户名只能使用 3-32 位字母、数字、下划线、点或连字符"
    return validate_password(password)


def validate_password(password: str) -> str | None:
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

    if role not in VALID_USER_ROLES:
        return None, "无效账号角色"
    normalized_role = role
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

        return _user_payload(row)
    except Exception:
        return None
    finally:
        db.close()


def list_users(cache: CacheManager) -> list[dict[str, Any]]:
    """List all users (without password_hash)."""
    db = cache.get_db()
    try:
        rows = db.execute("SELECT * FROM users ORDER BY id").fetchall()
        return [_user_payload(row) for row in rows]
    except Exception:
        return []
    finally:
        db.close()


def get_user_by_id(cache: CacheManager, user_id: int) -> dict[str, Any] | None:
    """Return one safe user record for session validation and administration."""
    db = cache.get_db()
    try:
        row = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        return _user_payload(row) if row else None
    except Exception:
        return None
    finally:
        db.close()


def get_user_by_username(cache: CacheManager, username: str) -> dict[str, Any] | None:
    """Return a database account regardless of active state, without its hash."""
    db = cache.get_db()
    try:
        row = db.execute(
            "SELECT * FROM users WHERE lower(username) = lower(?)",
            (normalize_username(username),),
        ).fetchone()
        return _user_payload(row) if row else None
    except Exception:
        return None
    finally:
        db.close()


def set_user_active(
    cache: CacheManager,
    user_id: int,
    *,
    active: bool,
) -> tuple[dict[str, Any] | None, str | None]:
    """Enable or disable an account while preserving one active administrator."""
    db = cache.get_db()
    try:
        # Serialize the last-admin check with the status update so two requests
        # cannot concurrently disable the final two administrators.
        db.execute("BEGIN IMMEDIATE")
        row = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        if not row:
            return None, "user_not_found"

        if bool(row["is_active"]) == active:
            return _user_payload(row), None

        if not active and row["role"] == "admin":
            active_admins = db.execute(
                "SELECT COUNT(*) AS count FROM users WHERE role = 'admin' AND is_active = 1"
            ).fetchone()
            if not active_admins or int(active_admins["count"]) <= 1:
                return None, "last_active_admin"

        now = _now_iso()
        db.execute(
            """UPDATE users
               SET is_active = ?, auth_version = COALESCE(auth_version, 0) + 1,
                   updated_at = ?
               WHERE id = ?""",
            (1 if active else 0, now, user_id),
        )
        db.commit()
        updated = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        return _user_payload(updated), None
    except Exception:
        return None, "user_update_failed"
    finally:
        db.close()


def set_user_role(
    cache: CacheManager,
    user_id: int,
    *,
    role: str,
) -> tuple[dict[str, Any] | None, str | None]:
    """Change an account role and invalidate every existing session."""
    if role not in VALID_USER_ROLES:
        return None, "invalid_role"

    db = cache.get_db()
    try:
        db.execute("BEGIN IMMEDIATE")
        row = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        if not row:
            return None, "user_not_found"
        if row["role"] == role:
            return _user_payload(row), None

        if row["role"] == "admin" and role != "admin" and bool(row["is_active"]):
            active_admins = db.execute(
                "SELECT COUNT(*) AS count FROM users WHERE role = 'admin' AND is_active = 1"
            ).fetchone()
            if not active_admins or int(active_admins["count"]) <= 1:
                return None, "last_active_admin"

        now = _now_iso()
        db.execute(
            """UPDATE users
               SET role = ?, auth_version = COALESCE(auth_version, 0) + 1,
                   updated_at = ?
               WHERE id = ?""",
            (role, now, user_id),
        )
        db.commit()
        updated = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        return _user_payload(updated), None
    except Exception:
        return None, "user_update_failed"
    finally:
        db.close()


def reset_user_password(
    cache: CacheManager,
    user_id: int,
    *,
    password: str,
) -> tuple[dict[str, Any] | None, str | None]:
    """Replace a password hash and invalidate every existing session."""
    if generate_password_hash is None:
        return None, "password_service_unavailable"
    validation_error = validate_password(password)
    if validation_error:
        return None, validation_error

    password_hash = generate_password_hash(password)
    db = cache.get_db()
    try:
        db.execute("BEGIN IMMEDIATE")
        row = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        if not row:
            return None, "user_not_found"

        now = _now_iso()
        db.execute(
            """UPDATE users
               SET password_hash = ?, auth_version = COALESCE(auth_version, 0) + 1,
                   updated_at = ?
               WHERE id = ?""",
            (password_hash, now, user_id),
        )
        db.commit()
        updated = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        return _user_payload(updated), None
    except Exception:
        return None, "password_reset_failed"
    finally:
        db.close()

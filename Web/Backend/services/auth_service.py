"""
Authentication service for ColorVision Marketplace.

Manages database-backed user accounts with password hashing via werkzeug.security.
The reserved configuration administrator is authenticated outside this service.
"""

from __future__ import annotations

import re
import sqlite3
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
EMAIL_PATTERN = re.compile(r"^[^\s@]+@[^\s@]+\.[^\s@]+$")
MIN_PASSWORD_LENGTH = 15
MAX_PASSWORD_LENGTH = 128
MAX_DISPLAY_NAME_LENGTH = 64
MAX_EMAIL_LENGTH = 254
VALID_USER_ROLES = frozenset({"admin", "user"})
VALID_ACCOUNT_ORIGINS = frozenset({
    "self_registered",
    "administrator_created",
    "legacy",
})


def _user_payload(row) -> dict[str, Any]:
    user = dict(row)
    user.pop("password_hash", None)
    user["is_active"] = bool(user.get("is_active"))
    user["must_change_password"] = bool(user.get("must_change_password"))
    user["password_recovery_pending"] = bool(user.get("password_recovery_pending"))
    user["active_session_count"] = int(user.get("active_session_count") or 0)
    user["password_recovery_request_count"] = int(
        user.get("password_recovery_request_count") or 0
    )
    user.setdefault("display_name", "")
    user.setdefault("email", "")
    user.setdefault("account_origin", "legacy")
    user.setdefault("password_recovery_requested_at", None)
    user.setdefault("password_changed_at", None)
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
        return f"密码至少需要 {MIN_PASSWORD_LENGTH} 个字符"
    if len(password) > MAX_PASSWORD_LENGTH:
        return f"密码不能超过 {MAX_PASSWORD_LENGTH} 个字符"
    return None


def normalize_profile(display_name: str, email: str) -> tuple[str, str]:
    return display_name.strip(), email.strip().lower()


def validate_profile(display_name: str, email: str) -> str | None:
    if len(display_name) > MAX_DISPLAY_NAME_LENGTH:
        return f"昵称不能超过 {MAX_DISPLAY_NAME_LENGTH} 个字符"
    if len(email) > MAX_EMAIL_LENGTH:
        return f"邮箱不能超过 {MAX_EMAIL_LENGTH} 个字符"
    if email and not EMAIL_PATTERN.fullmatch(email):
        return "请输入有效的邮箱地址"
    return None


def create_user(
    cache: CacheManager,
    username: str,
    password: str,
    *,
    role: str = "user",
    display_name: str = "",
    email: str = "",
    must_change_password: bool = False,
    account_origin: str = "legacy",
) -> tuple[dict[str, Any] | None, str | None]:
    """Create a normal user account. Returns (user, error_message)."""
    if generate_password_hash is None:
        return None, "密码服务不可用"

    username = normalize_username(username)
    validation_error = validate_registration(username, password)
    if validation_error:
        return None, validation_error

    display_name, email = normalize_profile(display_name, email)
    profile_error = validate_profile(display_name, email)
    if profile_error:
        return None, profile_error

    if role not in VALID_USER_ROLES:
        return None, "无效账号角色"
    if account_origin not in VALID_ACCOUNT_ORIGINS:
        return None, "无效账号来源"
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

        if email:
            existing_email = db.execute(
                "SELECT id FROM users WHERE lower(email) = lower(?)",
                (email,),
            ).fetchone()
            if existing_email:
                return None, "邮箱已被其他账号使用"

        cursor = db.execute(
            """INSERT INTO users
                   (username, password_hash, role, is_active, must_change_password,
                    account_origin, display_name, email, created_at, updated_at,
                    password_changed_at)
               VALUES (?, ?, ?, 1, ?, ?, ?, ?, ?, ?, ?)""",
            (
                username,
                pw_hash,
                normalized_role,
                1 if must_change_password else 0,
                account_origin,
                display_name,
                email,
                now,
                now,
                now,
            ),
        )
        db.commit()
        row = db.execute("SELECT * FROM users WHERE id = ?", (cursor.lastrowid,)).fetchone()
        return _user_payload(row), None
    except sqlite3.IntegrityError:
        return None, "用户名或邮箱已存在"
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
            "SELECT * FROM users WHERE lower(username) = lower(?) AND is_active = 1",
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
    from services.password_recovery_service import expire_password_recovery_requests

    expire_password_recovery_requests(cache)
    db = cache.get_db()
    try:
        rows = db.execute(
            """SELECT users.*,
                      (SELECT COUNT(*) FROM user_sessions
                       WHERE user_sessions.user_id = users.id
                         AND user_sessions.revoked_at IS NULL
                         AND user_sessions.auth_version = COALESCE(users.auth_version, 0)
                      ) AS active_session_count,
                      CASE WHEN recovery.id IS NULL THEN 0 ELSE 1 END
                          AS password_recovery_pending,
                      COALESCE(recovery.request_count, 0)
                          AS password_recovery_request_count,
                      recovery.last_requested_at AS password_recovery_requested_at
               FROM users
               LEFT JOIN password_recovery_requests recovery
                 ON recovery.user_id = users.id AND recovery.status = 'pending'
               ORDER BY users.id"""
        ).fetchall()
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


def delete_inactive_user(
    cache: CacheManager,
    user_id: int,
) -> tuple[dict[str, Any] | None, str | None]:
    """Permanently remove one disabled account and its private security state."""
    db = cache.get_db()
    try:
        db.execute("BEGIN IMMEDIATE")
        row = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        if not row:
            db.rollback()
            return None, "user_not_found"
        if bool(row["is_active"]):
            db.rollback()
            return None, "account_must_be_disabled"

        sessions_deleted = int(db.execute(
            "SELECT COUNT(*) FROM user_sessions WHERE user_id = ?",
            (user_id,),
        ).fetchone()[0])
        recovery_requests_deleted = int(db.execute(
            "SELECT COUNT(*) FROM password_recovery_requests WHERE user_id = ?",
            (user_id,),
        ).fetchone()[0])
        username = str(row["username"] or "")
        username_key = username.strip().casefold()[:128] or "<empty>"
        cleared_sources = max(0, int(db.execute(
            "DELETE FROM login_attempts WHERE username_key = ?",
            (username_key,),
        ).rowcount or 0))
        deleted = db.execute("DELETE FROM users WHERE id = ?", (user_id,)).rowcount
        if deleted != 1:
            raise RuntimeError("Account disappeared during deletion")
        db.commit()
        return {
            "id": int(row["id"]),
            "username": username,
            "role": str(row["role"] or "user"),
            "account_origin": str(row["account_origin"] or "legacy"),
            "sessions_deleted": sessions_deleted,
            "password_recovery_requests_deleted": recovery_requests_deleted,
            "login_failure_sources_cleared": cleared_sources,
        }, None
    except Exception:
        db.rollback()
        return None, "user_delete_failed"
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
    require_change: bool = True,
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
                   must_change_password = ?, updated_at = ?, password_changed_at = ?
               WHERE id = ?""",
            (password_hash, 1 if require_change else 0, now, now, user_id),
        )
        db.commit()
        updated = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        return _user_payload(updated), None
    except Exception:
        return None, "password_reset_failed"
    finally:
        db.close()


def require_user_password_change(
    cache: CacheManager,
    user_id: int,
) -> tuple[dict[str, Any] | None, str | None]:
    """Require the account to replace its current password on next login."""
    db = cache.get_db()
    try:
        db.execute("BEGIN IMMEDIATE")
        row = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        if not row:
            return None, "user_not_found"

        now = _now_iso()
        db.execute(
            """UPDATE users
               SET must_change_password = 1,
                   auth_version = COALESCE(auth_version, 0) + 1,
                   updated_at = ?
               WHERE id = ?""",
            (now, user_id),
        )
        db.commit()
        updated = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        return _user_payload(updated), None
    except Exception:
        return None, "password_change_requirement_failed"
    finally:
        db.close()


def query_users(
    cache: CacheManager,
    *,
    query: str = "",
    role: str = "",
    account_origin: str = "",
    active: bool | None = None,
    password_change_required: bool | None = None,
    password_recovery_pending: bool | None = None,
    sort_by: str = "",
    sort_order: str = "desc",
    limit: int = 20,
    offset: int = 0,
) -> dict[str, Any]:
    """Return a filtered user page plus unfiltered account totals."""
    from services.password_recovery_service import expire_password_recovery_requests

    expire_password_recovery_requests(cache)
    query = query.strip()
    where: list[str] = []
    parameters: list[Any] = []
    if query:
        where.append(
            """(instr(lower(users.username), lower(?)) > 0
                 OR instr(lower(users.display_name), lower(?)) > 0
                 OR instr(lower(users.email), lower(?)) > 0)"""
        )
        parameters.extend([query, query, query])
    if role:
        where.append("users.role = ?")
        parameters.append(role)
    if account_origin:
        where.append("users.account_origin = ?")
        parameters.append(account_origin)
    if active is not None:
        where.append("users.is_active = ?")
        parameters.append(1 if active else 0)
    if password_change_required is not None:
        where.append("users.must_change_password = ?")
        parameters.append(1 if password_change_required else 0)
    if password_recovery_pending is not None:
        where.append("recovery.id IS NOT NULL" if password_recovery_pending else "recovery.id IS NULL")

    where_sql = f" WHERE {' AND '.join(where)}" if where else ""
    from_sql = (
        " FROM users LEFT JOIN password_recovery_requests recovery"
        " ON recovery.user_id = users.id AND recovery.status = 'pending'"
    )
    sort_columns = {
        "username": "lower(users.username)",
        "display_name": "lower(users.display_name)",
        "email": "lower(users.email)",
        "role": "users.role",
        "account_origin": "users.account_origin",
        "is_active": "users.is_active",
        "active_session_count": "active_session_count",
        "created_at": "users.created_at",
        "last_login_at": "users.last_login_at",
        "password_recovery_requested_at": "recovery.last_requested_at",
    }
    sort_column = sort_columns.get(sort_by)
    direction = "ASC" if sort_order == "asc" else "DESC"
    order_sql = (
        f"{sort_column} IS NULL, {sort_column} {direction}, users.id DESC"
        if sort_column
        else "users.id DESC"
    )
    db = cache.get_db()
    try:
        total_row = db.execute(
            f"SELECT COUNT(users.id) AS count{from_sql}{where_sql}",
            parameters,
        ).fetchone()
        rows = db.execute(
            f"""SELECT users.*,
                       (SELECT COUNT(*) FROM user_sessions
                        WHERE user_sessions.user_id = users.id
                          AND user_sessions.revoked_at IS NULL
                          AND user_sessions.auth_version = COALESCE(users.auth_version, 0)
                       ) AS active_session_count,
                       CASE WHEN recovery.id IS NULL THEN 0 ELSE 1 END
                           AS password_recovery_pending,
                       COALESCE(recovery.request_count, 0)
                           AS password_recovery_request_count,
                       recovery.last_requested_at AS password_recovery_requested_at
                {from_sql}{where_sql}
                ORDER BY {order_sql} LIMIT ? OFFSET ?""",
            [*parameters, limit, offset],
        ).fetchall()
        summary = db.execute(
            """SELECT
                   COUNT(*) AS total,
                   SUM(CASE WHEN is_active = 1 THEN 1 ELSE 0 END) AS active,
                   SUM(CASE WHEN is_active = 0 THEN 1 ELSE 0 END) AS inactive,
                   SUM(CASE WHEN role = 'admin' THEN 1 ELSE 0 END) AS admins,
                   SUM(CASE WHEN role = 'user' THEN 1 ELSE 0 END) AS users,
                   SUM(CASE WHEN account_origin = 'self_registered' THEN 1 ELSE 0 END)
                       AS self_registered,
                   SUM(CASE WHEN account_origin = 'administrator_created' THEN 1 ELSE 0 END)
                       AS administrator_created,
                   SUM(CASE WHEN account_origin = 'legacy' THEN 1 ELSE 0 END)
                       AS legacy,
                   SUM(CASE WHEN must_change_password = 1 THEN 1 ELSE 0 END)
                       AS pending_password_changes,
                   SUM(CASE WHEN EXISTS (
                       SELECT 1 FROM password_recovery_requests recovery
                       WHERE recovery.user_id = users.id AND recovery.status = 'pending'
                   ) THEN 1 ELSE 0 END) AS pending_password_recovery
               FROM users"""
        ).fetchone()
        return {
            "items": [_user_payload(row) for row in rows],
            "total": int(total_row["count"] if total_row else 0),
            "limit": limit,
            "offset": offset,
            "summary": {
                "total": int(summary["total"] or 0),
                "active": int(summary["active"] or 0),
                "inactive": int(summary["inactive"] or 0),
                "admins": int(summary["admins"] or 0),
                "users": int(summary["users"] or 0),
                "self_registered": int(summary["self_registered"] or 0),
                "administrator_created": int(summary["administrator_created"] or 0),
                "legacy": int(summary["legacy"] or 0),
                "pending_password_changes": int(summary["pending_password_changes"] or 0),
                "pending_password_recovery": int(summary["pending_password_recovery"] or 0),
            },
        }
    finally:
        db.close()


def update_user_profile(
    cache: CacheManager,
    user_id: int,
    *,
    display_name: str,
    email: str,
) -> tuple[dict[str, Any] | None, str | None]:
    """Update public account metadata without changing authentication state."""
    display_name, email = normalize_profile(display_name, email)
    validation_error = validate_profile(display_name, email)
    if validation_error:
        return None, validation_error

    db = cache.get_db()
    try:
        row = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        if not row:
            return None, "user_not_found"

        if email:
            existing_email = db.execute(
                "SELECT id FROM users WHERE lower(email) = lower(?) AND id != ?",
                (email, user_id),
            ).fetchone()
            if existing_email:
                return None, "邮箱已被其他账号使用"

        now = _now_iso()
        db.execute(
            "UPDATE users SET display_name = ?, email = ?, updated_at = ? WHERE id = ?",
            (display_name, email, now, user_id),
        )
        db.commit()
        updated = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        return _user_payload(updated), None
    except sqlite3.IntegrityError:
        return None, "邮箱已被其他账号使用"
    except Exception:
        return None, "profile_update_failed"
    finally:
        db.close()


def change_user_password(
    cache: CacheManager,
    user_id: int,
    *,
    current_password: str,
    new_password: str,
) -> tuple[dict[str, Any] | None, str | None]:
    """Change the signed-in user's password after verifying the old password."""
    if check_password_hash is None or generate_password_hash is None:
        return None, "password_service_unavailable"
    validation_error = validate_password(new_password)
    if validation_error:
        return None, validation_error
    if current_password == new_password:
        return None, "新密码不能与当前密码相同"

    db = cache.get_db()
    try:
        db.execute("BEGIN IMMEDIATE")
        row = db.execute(
            "SELECT * FROM users WHERE id = ? AND is_active = 1",
            (user_id,),
        ).fetchone()
        if not row:
            return None, "user_not_found"
        if not check_password_hash(row["password_hash"], current_password):
            return None, "当前密码不正确"

        now = _now_iso()
        db.execute(
            """UPDATE users
               SET password_hash = ?, auth_version = COALESCE(auth_version, 0) + 1,
                   must_change_password = 0, updated_at = ?, password_changed_at = ?
               WHERE id = ?""",
            (generate_password_hash(new_password), now, now, user_id),
        )
        db.commit()
        updated = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        return _user_payload(updated), None
    except Exception:
        return None, "password_change_failed"
    finally:
        db.close()

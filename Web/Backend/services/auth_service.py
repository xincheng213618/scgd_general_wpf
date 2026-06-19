"""
Authentication service for ColorVision Marketplace.

Manages user accounts with password hashing via werkzeug.security.
Falls back to config.json upload_auth when users table is empty.
"""

from __future__ import annotations

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


def verify_user_credentials(
    cache: CacheManager,
    username: str,
    password: str,
) -> dict[str, Any] | None:
    """Verify username/password against users table. Returns user dict or None."""
    if check_password_hash is None:
        return None

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

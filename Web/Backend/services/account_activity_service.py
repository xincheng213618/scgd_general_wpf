"""Privacy-scoped account activity queries for the signed-in user."""

from __future__ import annotations

from typing import Any

from db_cache import CacheManager


ACCOUNT_ACTIVITY_ACTIONS = (
    "login_success",
    "login_failed",
    "login_throttled",
    "login_throttle_unlock",
    "user_register",
    "user_create",
    "user_profile_update",
    "user_password_change",
    "user_password_reset",
    "user_password_change_required",
    "user_password_recovery_request",
    "user_role_update",
    "user_enable",
    "user_disable",
    "user_session_revoke",
    "user_sessions_revoke_others",
    "user_sessions_force_revoke",
    "user_logout",
    "auth_forbidden",
)

SECURITY_ACTIVITY_ACTIONS = frozenset({
    "login_failed",
    "login_throttled",
    "login_throttle_unlock",
    "user_password_change",
    "user_password_reset",
    "user_password_change_required",
    "user_password_recovery_request",
    "user_role_update",
    "user_disable",
    "user_session_revoke",
    "user_sessions_revoke_others",
    "user_sessions_force_revoke",
    "auth_forbidden",
})


def _activity_filter(username: str, user_id: int | None) -> tuple[str, list[Any]]:
    placeholders = ",".join("?" for _ in ACCOUNT_ACTIVITY_ACTIONS)
    identity_conditions = [
        "(actor_type = 'user' AND lower(actor_id) = lower(?))",
        "(action IN ('login_failed', 'login_throttled') "
        "AND actor_type = 'anonymous' AND lower(actor_id) = lower(?))",
        "(target_type = 'user' AND lower(target_id) = lower(?))",
    ]
    parameters: list[Any] = [*ACCOUNT_ACTIVITY_ACTIONS, username, username, username]
    if user_id is not None:
        identity_conditions.append("(target_type = 'user' AND target_id = ?)")
        parameters.append(str(user_id))
    return (
        f"action IN ({placeholders}) AND ({' OR '.join(identity_conditions)})",
        parameters,
    )


def _entry_payload(row, username: str) -> dict[str, Any]:
    actor_type = str(row["actor_type"] or "")
    actor_id = str(row["actor_id"] or "")
    action = str(row["action"] or "")
    if actor_type == "anonymous":
        source = "anonymous"
    elif actor_type == "user" and actor_id.casefold() == username.casefold():
        source = "self"
    else:
        source = "administrator"
    return {
        "id": int(row["id"]),
        "action": action,
        "source": source,
        "ip": str(row["ip"] or ""),
        "user_agent": str(row["user_agent"] or ""),
        "detail": str(row["detail"] or ""),
        "created_at": row["created_at"],
        "security": action in SECURITY_ACTIVITY_ACTIONS,
    }


def get_account_activity_page(
    cache: CacheManager,
    *,
    username: str,
    user_id: int | None,
    limit: int,
    offset: int,
) -> dict[str, Any]:
    """Return only audit entries belonging to the current account identity."""
    where, parameters = _activity_filter(username, user_id)
    security_placeholders = ",".join("?" for _ in SECURITY_ACTIVITY_ACTIONS)
    db = cache.get_db()
    try:
        total_row = db.execute(
            f"SELECT COUNT(*) AS total FROM audit_log WHERE {where}",
            parameters,
        ).fetchone()
        rows = db.execute(
            f"""SELECT * FROM audit_log
                WHERE {where}
                ORDER BY id DESC LIMIT ? OFFSET ?""",
            [*parameters, limit, offset],
        ).fetchall()
        summary = db.execute(
                f"""SELECT
                    SUM(CASE WHEN action = 'login_failed' THEN 1 ELSE 0 END) AS failed_logins,
                    SUM(CASE WHEN action = 'login_throttled' THEN 1 ELSE 0 END) AS throttled_logins,
                    SUM(CASE WHEN action IN ({security_placeholders}) THEN 1 ELSE 0 END) AS security_events
                FROM audit_log WHERE {where}""",
            [*SECURITY_ACTIVITY_ACTIONS, *parameters],
        ).fetchone()
        return {
            "entries": [_entry_payload(row, username) for row in rows],
            "total": int(total_row["total"] if total_row else 0),
            "limit": limit,
            "offset": offset,
            "summary": {
                "failed_logins": int(summary["failed_logins"] or 0),
                "throttled_logins": int(summary["throttled_logins"] or 0),
                "security_events": int(summary["security_events"] or 0),
            },
        }
    finally:
        db.close()

"""Database-backed role permissions for Web accounts."""

from __future__ import annotations

from datetime import datetime, timezone
from hashlib import sha256
from typing import Any, Iterable


ROLE_DEFINITIONS: tuple[dict[str, Any], ...] = (
    {
        "code": "admin",
        "name": "管理员",
        "description": "现有管理员角色，始终保留全部权限。",
        "is_system": True,
    },
    {
        "code": "user",
        "name": "注册用户",
        "description": "公开注册与管理员创建的普通账号；当前默认拥有全部功能权限。",
        "is_system": True,
    },
)

PERMISSION_DEFINITIONS: tuple[dict[str, Any], ...] = (
    {"code": "admin:access", "name": "管理后台", "description": "进入 Web 管理后台。", "category": "基础", "sort_order": 10},
    {"code": "file:transfer", "name": "文件中转", "description": "上传、下载、列出和删除中转文件。", "category": "文件", "sort_order": 20},
    {"code": "files:manage", "name": "文件管理", "description": "浏览和管理非公开存储文件。", "category": "文件", "sort_order": 30},
    {"code": "plugin:publish", "name": "插件发布", "description": "上传并发布插件安装包。", "category": "发布", "sort_order": 40},
    {"code": "release:publish", "name": "主程序发布", "description": "上传并发布主程序与服务包。", "category": "发布", "sort_order": 50},
    {"code": "cache:read", "name": "缓存与索引查看", "description": "查看缓存、索引和文档构建状态。", "category": "系统运维", "sort_order": 60},
    {"code": "cache:refresh", "name": "缓存与索引维护", "description": "刷新索引并清理过期缓存。", "category": "系统运维", "sort_order": 70},
    {"code": "jobs:read", "name": "任务查看", "description": "查看后台任务与运行历史。", "category": "系统运维", "sort_order": 80},
    {"code": "jobs:write", "name": "任务执行", "description": "运行、启用或禁用后台任务。", "category": "系统运维", "sort_order": 90},
    {"code": "deployments:read", "name": "部署历史", "description": "查看部署记录和数据库备份。", "category": "系统运维", "sort_order": 100},
    {"code": "backups:manage", "name": "数据库备份", "description": "查看并创建后台数据库备份。", "category": "系统运维", "sort_order": 110},
    {"code": "operations:manage", "name": "终端运维", "description": "查看终端并执行受控运维操作。", "category": "系统运维", "sort_order": 120},
    {"code": "feedback:manage", "name": "反馈管理", "description": "查看反馈、附件并更新处理状态。", "category": "运营", "sort_order": 130},
    {"code": "stats:read", "name": "统计查看", "description": "查看概览、访问统计与性能数据。", "category": "运营", "sort_order": 140},
    {"code": "audit:read", "name": "审计日志", "description": "查看后台操作审计记录。", "category": "安全", "sort_order": 150},
    {"code": "users:manage", "name": "用户管理", "description": "创建、启停用户并调整账号角色。", "category": "安全", "sort_order": 160},
    {"code": "permissions:manage", "name": "权限管理", "description": "查看和调整角色权限。", "category": "安全", "sort_order": 170},
    {"code": "api_keys:manage", "name": "API Key 管理", "description": "创建、轮换和撤销 API Key。", "category": "安全", "sort_order": 180},
    {"code": "copilot:manage", "name": "Copilot 配置", "description": "管理 Web 端 Copilot 配置。", "category": "配置", "sort_order": 190},
    {"code": "settings:manage", "name": "系统设置", "description": "调整注册、保留策略等系统设置。", "category": "配置", "sort_order": 200},
)

ALL_PERMISSION_CODES = frozenset(item["code"] for item in PERMISSION_DEFINITIONS)


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def _permission_revision(role: str, permission_codes: Iterable[str]) -> str:
    value = "\0".join([role, *sorted(set(permission_codes))])
    return sha256(value.encode("utf-8")).hexdigest()


def describe_permissions(permission_codes: Iterable[str]) -> list[dict[str, Any]]:
    """Return display metadata only for the caller's effective permissions."""
    granted = set(permission_codes)
    return [dict(item) for item in PERMISSION_DEFINITIONS if item["code"] in granted]


def seed_permission_catalog(db) -> None:
    """Create/update the fixed catalog and grant new permissions to both roles.

    Existing grants for the user role are not recreated, so later permission
    adjustments remain durable. The administrator role is intentionally kept
    fully privileged to preserve the existing administrator contract.
    """
    now = _now_iso()
    for role in ROLE_DEFINITIONS:
        db.execute(
            """INSERT INTO roles (code, name, description, is_system, created_at, updated_at)
               VALUES (?, ?, ?, ?, ?, ?)
               ON CONFLICT(code) DO UPDATE SET
                   name = excluded.name,
                   description = excluded.description,
                   is_system = excluded.is_system,
                   updated_at = excluded.updated_at""",
            (
                role["code"],
                role["name"],
                role["description"],
                1 if role["is_system"] else 0,
                now,
                now,
            ),
        )

    for permission in PERMISSION_DEFINITIONS:
        existed = db.execute(
            "SELECT 1 FROM permissions WHERE code = ?",
            (permission["code"],),
        ).fetchone() is not None
        db.execute(
            """INSERT INTO permissions (code, name, description, category, sort_order, created_at, updated_at)
               VALUES (?, ?, ?, ?, ?, ?, ?)
               ON CONFLICT(code) DO UPDATE SET
                   name = excluded.name,
                   description = excluded.description,
                   category = excluded.category,
                   sort_order = excluded.sort_order,
                   updated_at = excluded.updated_at""",
            (
                permission["code"],
                permission["name"],
                permission["description"],
                permission["category"],
                permission["sort_order"],
                now,
                now,
            ),
        )
        if not existed:
            for role_code in ("admin", "user"):
                db.execute(
                    """INSERT OR IGNORE INTO role_permissions
                       (role_code, permission_code, granted_at)
                       VALUES (?, ?, ?)""",
                    (role_code, permission["code"], now),
                )

    for permission_code in ALL_PERMISSION_CODES:
        db.execute(
            """INSERT OR IGNORE INTO role_permissions
               (role_code, permission_code, granted_at)
               VALUES ('admin', ?, ?)""",
            (permission_code, now),
        )


def ensure_permission_catalog(cache) -> None:
    db = cache.get_db()
    try:
        with db:
            seed_permission_catalog(db)
    finally:
        db.close()


def get_role_permission_codes(cache, role: str) -> frozenset[str]:
    db = cache.get_db()
    try:
        rows = db.execute(
            """SELECT permission_code FROM role_permissions
               WHERE role_code = ? ORDER BY permission_code""",
            (role,),
        ).fetchall()
        return frozenset(str(row["permission_code"]) for row in rows)
    finally:
        db.close()


def role_has_permissions(cache, role: str, required: Iterable[str] | None) -> bool:
    required_codes = frozenset(required or ())
    if not required_codes:
        return True
    granted = get_role_permission_codes(cache, role)
    return "admin:*" in granted or required_codes <= granted


def role_can_access_admin(cache, role: str) -> bool:
    granted = get_role_permission_codes(cache, role)
    return "admin:*" in granted or "admin:access" in granted


def list_permission_matrix(cache) -> dict[str, Any]:
    ensure_permission_catalog(cache)
    db = cache.get_db()
    try:
        permissions = [dict(row) for row in db.execute(
            """SELECT code, name, description, category, sort_order
               FROM permissions ORDER BY sort_order, code"""
        ).fetchall()]
        member_counts = {
            str(row["role"]): {
                "member_count": int(row["member_count"] or 0),
                "active_member_count": int(row["active_member_count"] or 0),
            }
            for row in db.execute(
                """SELECT role,
                          COUNT(*) AS member_count,
                          SUM(CASE WHEN is_active = 1 THEN 1 ELSE 0 END) AS active_member_count
                   FROM users GROUP BY role"""
            ).fetchall()
        }
        roles = []
        for row in db.execute(
            """SELECT code, name, description, is_system
               FROM roles ORDER BY CASE code WHEN 'admin' THEN 0 ELSE 1 END, code"""
        ).fetchall():
            role = dict(row)
            role["is_system"] = bool(role["is_system"])
            role.update(member_counts.get(role["code"], {
                "member_count": 0,
                "active_member_count": 0,
            }))
            role["permissions"] = sorted(
                item["permission_code"]
                for item in db.execute(
                    """SELECT permission_code FROM role_permissions
                       WHERE role_code = ? ORDER BY permission_code""",
                    (role["code"],),
                ).fetchall()
            )
            role["revision"] = _permission_revision(role["code"], role["permissions"])
            role["editable"] = role["code"] != "admin"
            roles.append(role)
        return {"permissions": permissions, "roles": roles}
    finally:
        db.close()


def replace_role_permissions(
    cache,
    role: str,
    permission_codes: Iterable[str],
    *,
    expected_revision: str | None = None,
) -> tuple[dict[str, Any] | None, str | None]:
    requested = {str(code).strip() for code in permission_codes if str(code).strip()}
    invalid = requested - ALL_PERMISSION_CODES
    if invalid:
        return None, f"invalid_permissions:{','.join(sorted(invalid))}"
    if role == "admin":
        return None, "administrator_permissions_are_fixed"
    if role != "user":
        return None, "role_not_found"

    ensure_permission_catalog(cache)
    db = cache.get_db()
    try:
        now = _now_iso()
        db.execute("BEGIN IMMEDIATE")
        current_codes = [
            str(row["permission_code"])
            for row in db.execute(
                """SELECT permission_code FROM role_permissions
                   WHERE role_code = ? ORDER BY permission_code""",
                (role,),
            ).fetchall()
        ]
        if (
            expected_revision is not None
            and expected_revision != _permission_revision(role, current_codes)
        ):
            db.rollback()
            return None, "permission_revision_conflict"
        current_set = set(current_codes)
        added = sorted(requested - current_set)
        removed = sorted(current_set - requested)
        db.execute("DELETE FROM role_permissions WHERE role_code = ?", (role,))
        db.executemany(
            """INSERT INTO role_permissions (role_code, permission_code, granted_at)
               VALUES (?, ?, ?)""",
            [(role, code, now) for code in sorted(requested)],
        )
        db.commit()
        matrix = list_permission_matrix(cache)
        updated_role = next(
            item for item in matrix["roles"] if item["code"] == role
        )
        matrix["change"] = {
            "role": role,
            "added": added,
            "removed": removed,
            "affected_active_members": int(updated_role["active_member_count"] or 0),
            "revision": str(updated_role["revision"]),
        }
        return matrix, None
    except Exception:
        db.rollback()
        return None, "permission_update_failed"
    finally:
        db.close()

from __future__ import annotations

import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable


GetDb = Callable[[], Any]
GetUploadAuth = Callable[[], tuple[str, str]]


def validate_runtime_config(
    config: dict[str, Any],
    *,
    default_secret_key: str,
    default_upload_auth: dict[str, Any],
) -> list[str]:
    issues: list[str] = []
    if str(config.get("secret_key", "")).strip() == default_secret_key:
        issues.append("secret_key must be changed from the default value")

    auth_config = config.get("upload_auth") or {}
    if not isinstance(auth_config, dict):
        auth_config = {}
    username = str(auth_config.get("username", "")).strip()
    password = str(auth_config.get("password", ""))
    if (
        username == str(default_upload_auth.get("username", "")).strip()
        and password == str(default_upload_auth.get("password", ""))
    ):
        issues.append("upload_auth.username/password must be changed from the default values")
    if not username or not password:
        issues.append("upload_auth.username and upload_auth.password must be configured")
    return issues


def probe_database(get_db: GetDb) -> tuple[bool, str | None]:
    try:
        db = get_db()
        db.execute("SELECT 1").fetchone()
        db.close()
        return True, None
    except Exception as exc:  # pragma: no cover - defensive wrapper
        return False, str(exc)


def directory_check(path: Path, *, ensure: bool = False) -> dict[str, Any]:
    error = ""
    if ensure:
        try:
            path.mkdir(parents=True, exist_ok=True)
        except OSError as exc:
            error = str(exc)

    exists = path.exists()
    is_dir = path.is_dir()
    probe_path = path if exists else path.parent
    writable = probe_path.exists() and os.access(probe_path, os.W_OK)
    ok = exists and is_dir and writable and not error
    return {
        "path": str(path),
        "exists": exists,
        "isDir": is_dir,
        "writable": writable,
        "ok": ok,
        "error": error,
    }


def build_health_payload(
    *,
    storage: Path,
    db_path: Path,
    config: dict[str, Any],
) -> dict[str, Any]:
    return {
        "status": "ok",
        "service": "ColorVision Marketplace",
        "time": datetime.now(timezone.utc).isoformat(),
        "storagePath": str(storage),
        "dbPath": str(db_path),
        "debug": bool(config.get("debug")),
    }


def build_ready_payload(
    *,
    storage: Path,
    db_path: Path,
    config: dict[str, Any],
    get_db: GetDb,
    get_upload_auth: GetUploadAuth,
) -> dict[str, Any]:
    storage_check = directory_check(storage, ensure=True)
    plugins_check = directory_check(storage / "Plugins", ensure=True)
    db_ok, db_error = probe_database(get_db)
    username, password = get_upload_auth()
    auth_ok = bool(username and password)

    issues: list[str] = []
    if not storage_check["ok"]:
        issues.append("storage path is not ready for uploads")
    if not plugins_check["ok"]:
        issues.append("Plugins directory is not ready for uploads")
    if not db_ok:
        issues.append("database is not ready")
    if not auth_ok:
        issues.append("upload authentication is not configured")

    ready = not issues
    return {
        "status": "ready" if ready else "degraded",
        "ready": ready,
        "time": datetime.now(timezone.utc).isoformat(),
        "checks": {
            "storage": storage_check,
            "plugins": plugins_check,
            "database": {
                "path": str(db_path),
                "ok": db_ok,
                "error": db_error or "",
            },
            "uploadAuth": {
                "ok": auth_ok,
                "usernameConfigured": bool(username),
                "passwordConfigured": bool(password),
            },
        },
        "issues": issues,
    }


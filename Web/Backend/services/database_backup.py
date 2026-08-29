"""Create privacy-cleaned, retention-managed marketplace database snapshots."""

from __future__ import annotations

import threading
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

from db_cache import CacheManager


_BACKUP_LOCK = threading.Lock()


def create_database_backup(
    cache: CacheManager,
    config: dict[str, Any],
    *,
    now: datetime | None = None,
) -> dict[str, Any]:
    """Create one safe snapshot and apply the live privacy/rotation contract."""
    with _BACKUP_LOCK:
        backup_path = _next_backup_path(cache.db_path.parent, now=now)
        if not cache.backup_db(backup_path):
            raise RuntimeError("Backup failed")

        try:
            from services.access_analytics import (
                prune_access_analytics_database,
                reporting_utc_offset_minutes,
            )
            from services.admin_data_retention import run_admin_data_retention

            access_cleanup = prune_access_analytics_database(
                backup_path,
                retention_days=int(config.get("access_analytics_retention_days", 90) or 90),
                utc_offset_minutes=reporting_utc_offset_minutes(config),
            )
            admin_retention = run_admin_data_retention(
                cache.get_db,
                backup_path.parent,
                config,
                protected_paths=(backup_path,),
            )
            cleanup_error_paths = {
                str(Path(path).resolve()).casefold()
                for path in (
                    *admin_retention["backupAudit"]["errorPaths"],
                    *admin_retention["backupSecurity"]["errorPaths"],
                )
            }
            if str(backup_path.resolve()).casefold() in cleanup_error_paths:
                raise RuntimeError("The new snapshot failed privacy cleanup")
        except Exception as exc:
            backup_path.unlink(missing_ok=True)
            raise RuntimeError(f"Backup retention cleanup failed: {exc}") from exc

        backup_retention = dict(admin_retention["backupFiles"])
        backup_retention["status"] = admin_retention["status"]
        backup_retention["errors"] = admin_retention["errors"]
        backup_retention["auditDeleted"] = admin_retention["audit"]["deleted"]
        backup_retention["snapshotAuditDeleted"] = admin_retention["backupAudit"]["deleted"]
        backup_retention["securityRowsDeleted"] = admin_retention["backupSecurity"]["deleted"]
        backup_retention["securityAccountsInvalidated"] = (
            admin_retention["backupSecurity"]["accountsInvalidated"]
        )
        backup_retention["securitySnapshotsScrubbed"] = admin_retention["backupSecurity"]["backups"]

        return {
            "status": "ok",
            "backup_name": backup_path.name,
            "backup_path": str(backup_path),
            "backup_size_bytes": backup_path.stat().st_size,
            "access_analytics_deleted": access_cleanup["deleted"],
            "security_rows_deleted": admin_retention["backupSecurity"]["deleted"],
            "security_accounts_invalidated": (
                admin_retention["backupSecurity"]["accountsInvalidated"]
            ),
            "backup_retention": backup_retention,
        }


def _next_backup_path(directory: Path, *, now: datetime | None = None) -> Path:
    """Choose a timestamp name without overwriting a same-second snapshot."""
    current = now or datetime.now(timezone.utc)
    if current.tzinfo is None:
        current = current.replace(tzinfo=timezone.utc)
    current = current.astimezone(timezone.utc)

    for offset in range(60):
        timestamp = (current + timedelta(seconds=offset)).strftime("%Y%m%d_%H%M%S")
        candidate = Path(directory) / f"marketplace_backup_{timestamp}.db"
        if not candidate.exists():
            return candidate
    raise RuntimeError("No database backup timestamp is available in the next minute")

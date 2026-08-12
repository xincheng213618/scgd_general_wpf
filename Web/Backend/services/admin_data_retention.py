"""Retention for administrator audit rows and recognized database snapshots."""

from __future__ import annotations

import re
import sqlite3
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Callable, Iterable

from services.access_analytics import parse_bounded_int


MANUAL_BACKUP_NAME_PATTERN = re.compile(r"marketplace_backup_\d{8}_\d{6}\.db")
DEFAULT_AUDIT_LOG_RETENTION_DAYS = 365
DEFAULT_ADMIN_DB_BACKUP_KEEP_COUNT = 10


def list_manual_db_backups(directory: Path) -> list[dict[str, Any]]:
    """List recognized snapshots without exposing filesystem paths."""
    root = Path(directory).resolve()
    if not root.is_dir():
        return []

    backups: list[dict[str, Any]] = []
    for path in sorted(root.glob("marketplace_backup_*.db"), reverse=True):
        if not _is_recognized_backup(path, root):
            continue
        try:
            created_at = datetime.strptime(
                path.name,
                "marketplace_backup_%Y%m%d_%H%M%S.db",
            ).replace(tzinfo=timezone.utc).isoformat()
            backups.append({
                "name": path.name,
                "created_at": created_at,
                "size_bytes": path.stat().st_size,
            })
        except (OSError, ValueError):
            continue
    return backups


def parse_admin_retention_config(config: dict[str, Any]) -> tuple[int, int]:
    """Return validated audit-day and manual-backup limits."""
    audit_days = parse_bounded_int(
        config.get("audit_log_retention_days"),
        name="audit_log_retention_days",
        default=DEFAULT_AUDIT_LOG_RETENTION_DAYS,
        minimum=1,
        maximum=3650,
    )
    backup_count = parse_bounded_int(
        config.get("admin_db_backup_keep_count"),
        name="admin_db_backup_keep_count",
        default=DEFAULT_ADMIN_DB_BACKUP_KEEP_COUNT,
        minimum=2,
        maximum=1000,
    )
    return audit_days, backup_count


def prune_audit_log(
    db_factory: Callable[[], Any],
    *,
    retention_days: int,
    now: datetime | None = None,
) -> dict[str, Any]:
    """Delete live audit rows older than the configured UTC cutoff."""
    cutoff = _audit_cutoff(retention_days, now)
    db = db_factory()
    try:
        deleted = _delete_audit_rows_before(db, cutoff.isoformat())
    finally:
        db.close()
    return _audit_result(retention_days, cutoff, deleted)


def prune_audit_log_database(
    db_path: Path,
    *,
    retention_days: int,
    now: datetime | None = None,
) -> dict[str, Any]:
    """Apply audit retention to one SQLite snapshot and verify its integrity."""
    cutoff = _audit_cutoff(retention_days, now)
    db = sqlite3.connect(str(db_path), timeout=15)
    try:
        deleted = _delete_audit_rows_before(db, cutoff.isoformat())
        check = db.execute("PRAGMA quick_check").fetchone()
        if not check or str(check[0]).lower() != "ok":
            raise sqlite3.DatabaseError(
                f"backup integrity check failed: {check[0] if check else 'no result'}"
            )
    finally:
        db.close()
    result = _audit_result(retention_days, cutoff, deleted)
    result["path"] = str(db_path)
    return result


def prune_audit_log_backups(
    directory: Path,
    *,
    retention_days: int,
    now: datetime | None = None,
) -> dict[str, Any]:
    """Scrub expired audit rows from recognized database snapshots."""
    root = Path(directory).resolve()
    results: list[dict[str, Any]] = []
    errors: list[str] = []
    error_paths: list[str] = []
    if not root.is_dir():
        return {"backups": 0, "deleted": 0, "results": [], "errors": [], "errorPaths": []}

    for path in sorted(root.glob("marketplace_backup_*.db")):
        if not _is_recognized_backup(path, root):
            continue
        try:
            results.append(prune_audit_log_database(
                path,
                retention_days=retention_days,
                now=now,
            ))
        except Exception as exc:
            errors.append(f"{path.name}: {exc}")
            error_paths.append(str(path))
    return {
        "backups": len(results),
        "deleted": sum(int(item["deleted"]) for item in results),
        "results": results,
        "errors": errors,
        "errorPaths": error_paths,
    }


def prune_manual_db_backups(
    directory: Path,
    *,
    keep_count: int,
    protected_paths: Iterable[Path] = (),
) -> dict[str, Any]:
    """Keep the newest recognized manual backups and protect everything else."""
    if not 2 <= int(keep_count) <= 1000:
        raise ValueError("keep_count must be between 2 and 1000")

    root = Path(directory).resolve()
    if not root.is_dir():
        return {
            "status": "success",
            "keepCount": int(keep_count),
            "beforeCount": 0,
            "afterCount": 0,
            "removedCount": 0,
            "removedBytes": 0,
            "preservedUnclassified": 0,
            "errors": [],
        }

    protected = {_resolved_key(Path(path)) for path in protected_paths}
    recognized: list[Path] = []
    preserved_unclassified = 0
    for path in sorted(root.glob("marketplace_backup_*.db")):
        if _is_recognized_backup(path, root):
            recognized.append(path)
        else:
            preserved_unclassified += 1

    ordered = sorted(recognized, key=lambda item: item.name, reverse=True)
    kept = {_resolved_key(path) for path in ordered[: int(keep_count)]}
    kept.update(protected)
    removed_count = 0
    removed_bytes = 0
    errors: list[str] = []

    for path in ordered:
        path_key = _resolved_key(path)
        if path_key in kept:
            continue
        if not _is_recognized_backup(path, root):
            errors.append(f"{path.name}: backup path changed during retention")
            continue
        try:
            size = path.stat().st_size
            path.unlink()
            removed_count += 1
            removed_bytes += size
        except OSError as exc:
            errors.append(f"{path.name}: {exc}")

    return {
        "status": "success" if not errors else "error",
        "keepCount": int(keep_count),
        "beforeCount": len(recognized),
        "afterCount": len(recognized) - removed_count,
        "removedCount": removed_count,
        "removedBytes": removed_bytes,
        "preservedUnclassified": preserved_unclassified,
        "errors": errors,
    }


def run_admin_data_retention(
    db_factory: Callable[[], Any],
    db_directory: Path,
    config: dict[str, Any],
    *,
    now: datetime | None = None,
    protected_paths: Iterable[Path] = (),
) -> dict[str, Any]:
    """Apply the complete live-and-snapshot administrator retention contract."""
    audit_days, backup_count = parse_admin_retention_config(config)
    live_audit = prune_audit_log(db_factory, retention_days=audit_days, now=now)
    backup_audit = prune_audit_log_backups(
        db_directory,
        retention_days=audit_days,
        now=now,
    )
    backup_files = prune_manual_db_backups(
        db_directory,
        keep_count=backup_count,
        protected_paths=(
            *(Path(path) for path in backup_audit["errorPaths"]),
            *protected_paths,
        ),
    )
    errors = [*backup_audit["errors"], *backup_files["errors"]]
    return {
        "status": "success" if not errors else "error",
        "audit": live_audit,
        "backupAudit": backup_audit,
        "backupFiles": backup_files,
        "errors": errors,
    }


def _audit_cutoff(retention_days: int, now: datetime | None) -> datetime:
    if not 1 <= int(retention_days) <= 3650:
        raise ValueError("retention_days must be between 1 and 3650")
    current = now or datetime.now(timezone.utc)
    if current.tzinfo is None:
        current = current.replace(tzinfo=timezone.utc)
    return current.astimezone(timezone.utc) - timedelta(days=int(retention_days))


def _delete_audit_rows_before(db: Any, cutoff: str) -> int:
    table = db.execute(
        "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'audit_log'"
    ).fetchone()
    if table is None:
        return 0
    cursor = db.execute("DELETE FROM audit_log WHERE created_at < ?", (cutoff,))
    deleted = max(0, int(cursor.rowcount or 0))
    db.commit()
    return deleted


def _audit_result(retention_days: int, cutoff: datetime, deleted: int) -> dict[str, Any]:
    return {
        "retentionDays": int(retention_days),
        "cutoff": cutoff.isoformat(),
        "deleted": int(deleted),
    }


def _is_recognized_backup(path: Path, root: Path) -> bool:
    try:
        return (
            MANUAL_BACKUP_NAME_PATTERN.fullmatch(path.name) is not None
            and path.is_file()
            and not path.is_symlink()
            and path.resolve().parent == root
        )
    except OSError:
        return False


def _resolved_key(path: Path) -> str:
    return str(path.resolve()).casefold()

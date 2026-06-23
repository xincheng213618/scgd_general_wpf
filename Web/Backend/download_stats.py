from __future__ import annotations

import hashlib
from typing import Any, Callable


def hash_ip(ip: str | None) -> str:
    if not ip:
        return ""
    return hashlib.sha256(ip.encode()).hexdigest()[:16]


def get_download_counts(db_factory: Callable[[], Any]) -> dict[str, int]:
    try:
        db = db_factory()
        rows = db.execute(
            "SELECT plugin_id, COUNT(*) AS cnt FROM download_log GROUP BY plugin_id"
        ).fetchall()
        db.close()
        return {row["plugin_id"]: row["cnt"] for row in rows}
    except Exception:
        return {}


def get_download_count(db_factory: Callable[[], Any], plugin_id: str) -> int:
    try:
        db = db_factory()
        row = db.execute(
            "SELECT COUNT(*) AS cnt FROM download_log WHERE plugin_id = ?",
            (plugin_id,),
        ).fetchone()
        db.close()
        return row["cnt"] if row else 0
    except Exception:
        return 0


def record_download(
    db_factory: Callable[[], Any],
    *,
    plugin_id: str,
    version: str,
    client_ip: str | None,
    client_version: str = "",
) -> None:
    try:
        db = db_factory()
        db.execute(
            "INSERT INTO download_log (plugin_id, version, client_ip, client_ver) VALUES (?, ?, ?, ?)",
            (
                plugin_id,
                version,
                hash_ip(client_ip),
                client_version,
            ),
        )
        db.commit()
        db.close()
    except Exception:
        pass


def build_stats_payload(
    db_factory: Callable[[], Any],
    *,
    per_plugin_limit: int = 20,
    recent_limit: int = 20,
) -> dict[str, Any]:
    db = db_factory()
    try:
        total = db.execute("SELECT COUNT(*) AS cnt FROM download_log").fetchone()["cnt"]

        per_plugin = db.execute(
            """
            SELECT plugin_id, COUNT(*) AS cnt
            FROM download_log
            GROUP BY plugin_id
            ORDER BY cnt DESC
            LIMIT ?
            """,
            (per_plugin_limit,),
        ).fetchall()

        recent = db.execute(
            """
            SELECT plugin_id, version, downloaded_at
            FROM download_log
            ORDER BY downloaded_at DESC
            LIMIT ?
            """,
            (recent_limit,),
        ).fetchall()
    finally:
        db.close()

    return {
        "totalDownloads": total,
        "perPlugin": [
            {"pluginId": row["plugin_id"], "count": row["cnt"]}
            for row in per_plugin
        ],
        "recent": [
            {
                "pluginId": row["plugin_id"],
                "version": row["version"],
                "downloadedAt": row["downloaded_at"],
            }
            for row in recent
        ],
    }

"""Read-only operator queries for activation licenses and devices."""

from __future__ import annotations

import time
from contextlib import closing


def list_activation_licenses(get_db, *, query: str, status: str, limit: int, offset: int):
    now = int(time.time())
    filters = []
    values = []
    if query:
        filters.append("""(
            instr(l.id, lower(?)) > 0 OR EXISTS (
                SELECT 1 FROM activation_devices d
                WHERE d.license_id = l.id AND instr(lower(d.device_name), lower(?)) > 0
            )
        )""")
        values.extend((query, query))
    if status:
        filters.append("l.expires_at > ?" if status == "active" else "l.expires_at <= ?")
        values.append(now)
    where = " WHERE " + " AND ".join(filters) if filters else ""

    with closing(get_db()) as db:
        db.execute("BEGIN")
        total = db.execute(f"SELECT COUNT(*) FROM activation_licenses l{where}", values).fetchone()[0]
        rows = db.execute(
            f"""SELECT l.id, l.device_limit, l.issued_at, l.expires_at,
                       CASE WHEN l.expires_at > ? THEN 'active' ELSE 'expired' END AS status,
                       (SELECT COUNT(*) FROM activation_devices d
                        WHERE d.license_id = l.id AND d.released_at IS NULL
                          AND d.expires_at > ? AND l.expires_at > ?) AS active_devices
                FROM activation_licenses l{where}
                ORDER BY l.issued_at DESC, l.id DESC LIMIT ? OFFSET ?""",
            (now, now, now, *values, limit, offset),
        ).fetchall()
    return {"licenses": [dict(row) for row in rows], "total": total}


def list_activation_devices(get_db, license_id: str, *, limit: int, offset: int):
    now = int(time.time())
    with closing(get_db()) as db:
        db.execute("BEGIN")
        license_row = db.execute("SELECT expires_at FROM activation_licenses WHERE id = ?", (license_id,)).fetchone()
        if license_row is None:
            return None
        total = db.execute("SELECT COUNT(*) FROM activation_devices WHERE license_id = ?", (license_id,)).fetchone()[0]
        rows = db.execute(
            """SELECT device_id, device_name, mode, activated_at, last_seen_at, expires_at, released_at,
                      CASE WHEN released_at IS NOT NULL THEN 'released'
                           WHEN expires_at > ? AND ? > ? THEN 'active' ELSE 'expired' END AS status
               FROM activation_devices WHERE license_id = ?
               ORDER BY (status = 'active') DESC, last_seen_at DESC, device_id DESC LIMIT ? OFFSET ?""",
            (now, license_row["expires_at"], now, license_id, limit, offset),
        ).fetchall()
    return {"devices": [dict(row) for row in rows], "total": total}

"""SQLite adapter for Operations support-session state."""

from __future__ import annotations

import sqlite3
from collections.abc import Callable

from ports.operations_support import SupportEventResult


class SqliteOperationsSupportStore:
    def __init__(self, connection_factory: Callable[[], sqlite3.Connection]):
        self._connection_factory = connection_factory

    @staticmethod
    def _latest_state(db: sqlite3.Connection, host_id: str, session_id: str) -> str | None:
        row = db.execute(
            """SELECT event_type FROM operations_support_events
               WHERE host_id=? AND session_id=? AND event_type!='message'
               ORDER BY created_at DESC, rowid DESC LIMIT 1""",
            (host_id, session_id),
        ).fetchone()
        return str(row["event_type"]) if row else None

    def latest_state(self, host_id: str, session_id: str) -> str | None:
        db = self._connection_factory()
        try:
            return self._latest_state(db, host_id, session_id)
        finally:
            db.close()

    def record_event(
        self,
        *,
        event_id: str,
        host_id: str,
        session_id: str,
        event_type: str,
        payload_json: str,
        created_at: str,
    ) -> SupportEventResult:
        db = self._connection_factory()
        try:
            db.execute("BEGIN IMMEDIATE")
            host = db.execute(
                "SELECT host_id FROM operations_hosts WHERE host_id=?",
                (host_id,),
            ).fetchone()
            if not host:
                db.rollback()
                return "host_not_found"

            current_type = self._latest_state(db, host_id, session_id)
            if event_type == "session.requested":
                if current_type:
                    db.rollback()
                    return "deduplicated"
            elif event_type == "session.active":
                if current_type == "session.active":
                    db.rollback()
                    return "deduplicated"
                if current_type != "session.requested":
                    db.rollback()
                    return "support_session_not_requested"
            elif event_type == "message":
                if current_type != "session.active":
                    db.rollback()
                    return "support_session_not_active"
            elif event_type in {"session.closed", "session.failed"}:
                if current_type in {"session.closed", "session.failed"}:
                    db.rollback()
                    return "deduplicated"
                if current_type not in {"session.requested", "session.active"}:
                    db.rollback()
                    return "support_session_not_found"

            db.execute(
                "INSERT INTO operations_support_events VALUES (?, ?, ?, ?, ?, ?)",
                (event_id, host_id, session_id, event_type, payload_json, created_at),
            )
            db.commit()
            return "created"
        except Exception:
            db.rollback()
            raise
        finally:
            db.close()

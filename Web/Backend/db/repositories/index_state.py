"""SQLite adapter for the index-state persistence port."""

from __future__ import annotations

import sqlite3
from collections.abc import Callable
from typing import Any


class SqliteIndexStateRepository:
    """Own all SQL and transaction behavior for ``index_state``."""

    def __init__(self, connection_factory: Callable[[], sqlite3.Connection]):
        self._connection_factory = connection_factory

    def update(
        self,
        scope: str,
        *,
        status: str = "ready",
        signature: str = "",
        started_at: str = "",
        finished_at: str = "",
        item_count: int = 0,
        duration_ms: int = 0,
        error: str = "",
    ) -> None:
        db = self._connection_factory()
        try:
            db.execute(
                """INSERT INTO index_state (scope, signature, status, last_started_at, last_finished_at,
                                            last_error, item_count, duration_ms)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                   ON CONFLICT(scope) DO UPDATE SET
                       signature = CASE WHEN excluded.signature != '' THEN excluded.signature ELSE index_state.signature END,
                       status = excluded.status,
                       last_started_at = COALESCE(excluded.last_started_at, index_state.last_started_at),
                       last_finished_at = COALESCE(excluded.last_finished_at, index_state.last_finished_at),
                       last_error = excluded.last_error,
                       item_count = excluded.item_count,
                       duration_ms = excluded.duration_ms
                """,
                (
                    scope,
                    signature or "",
                    status,
                    started_at or None,
                    finished_at or None,
                    error,
                    item_count,
                    duration_ms,
                ),
            )
            db.commit()
        except Exception as exc:
            db.rollback()
            print(f"[index_state] update failed for {scope}: {exc}")
        finally:
            db.close()

    def get(self, scope: str) -> dict[str, Any] | None:
        db = self._connection_factory()
        try:
            row = db.execute(
                "SELECT * FROM index_state WHERE scope = ?",
                (scope,),
            ).fetchone()
            return dict(row) if row else None
        except Exception as exc:
            print(f"[index_state] read failed for {scope}: {exc}")
            return None
        finally:
            db.close()

    def get_many(self, scopes: tuple[str, ...]) -> dict[str, dict[str, Any]]:
        states: dict[str, dict[str, Any]] = {}
        db = self._connection_factory()
        try:
            for scope in scopes:
                row = db.execute(
                    "SELECT * FROM index_state WHERE scope = ?",
                    (scope,),
                ).fetchone()
                states[scope] = dict(row) if row else self._not_initialized(scope)
            return states
        finally:
            db.close()

    def get_all(self) -> dict[str, dict[str, Any]]:
        db = self._connection_factory()
        try:
            rows = db.execute("SELECT * FROM index_state").fetchall()
            return {row["scope"]: dict(row) for row in rows}
        except Exception as exc:
            print(f"[index_state] list failed: {exc}")
            return {}
        finally:
            db.close()

    @staticmethod
    def _not_initialized(scope: str) -> dict[str, Any]:
        return {
            "scope": scope,
            "status": "not_initialized",
            "signature": "",
            "last_started_at": None,
            "last_finished_at": None,
            "last_error": "",
            "item_count": 0,
            "duration_ms": 0,
        }

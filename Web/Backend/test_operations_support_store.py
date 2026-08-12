from __future__ import annotations

import tempfile
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from db.repositories.operations_support import SqliteOperationsSupportStore
from db_cache import CacheManager


class OperationsSupportStoreTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.cache = CacheManager(Path(self.temp_dir.name) / "marketplace.db")
        self.cache.init_db()
        self.store = SqliteOperationsSupportStore(self.cache.get_db)
        db = self.cache.get_db()
        try:
            db.execute(
                """INSERT INTO operations_hosts
                   (host_id, last_seen_at, created_at, updated_at)
                   VALUES ('host-1', '2026-08-12T00:00:00+00:00',
                           '2026-08-12T00:00:00+00:00', '2026-08-12T00:00:00+00:00')"""
            )
            db.commit()
        finally:
            db.close()

    def tearDown(self):
        self.temp_dir.cleanup()

    def record(self, event_id: str, event_type: str, session_id: str = "session-1") -> str:
        return self.store.record_event(
            event_id=event_id,
            host_id="host-1",
            session_id=session_id,
            event_type=event_type,
            payload_json="{}",
            created_at="2026-08-12T00:00:00+00:00",
        )

    def test_state_machine_preserves_latest_non_message_state(self):
        self.assertEqual(self.record("active-early", "session.active"), "support_session_not_requested")
        self.assertEqual(self.record("requested", "session.requested"), "created")
        self.assertEqual(self.record("active", "session.active"), "created")
        self.assertEqual(self.record("message", "message"), "created")
        self.assertEqual(self.store.latest_state("host-1", "session-1"), "session.active")
        self.assertEqual(self.record("closed", "session.closed"), "created")
        self.assertEqual(self.record("closed-again", "session.closed"), "deduplicated")

    def test_concurrent_session_requests_are_atomically_deduplicated(self):
        with ThreadPoolExecutor(max_workers=2) as executor:
            results = list(executor.map(
                lambda event_id: self.record(event_id, "session.requested", "session-race"),
                ("request-a", "request-b"),
            ))

        self.assertEqual(sorted(results), ["created", "deduplicated"])
        db = self.cache.get_db()
        try:
            count = db.execute(
                "SELECT COUNT(*) FROM operations_support_events WHERE session_id='session-race'"
            ).fetchone()[0]
        finally:
            db.close()
        self.assertEqual(count, 1)

    def test_unknown_host_is_rejected_without_writing(self):
        result = self.store.record_event(
            event_id="missing",
            host_id="missing-host",
            session_id="session-1",
            event_type="session.requested",
            payload_json="{}",
            created_at="2026-08-12T00:00:00+00:00",
        )
        self.assertEqual(result, "host_not_found")


if __name__ == "__main__":
    unittest.main()

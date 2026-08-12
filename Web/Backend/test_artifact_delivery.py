"""Contracts for unified artifact HTTP delivery and completion events."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from flask import Flask, request

from routes.artifact_delivery import deliver_artifact
from services.artifact_delivery import ArtifactDeliveryService, ArtifactDownloadEvent


class ArtifactDeliveryTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.addCleanup(self._temp.cleanup)
        self.path = Path(self._temp.name) / "artifact.bin"
        self.payload = bytes(range(256)) * 80
        self.path.write_bytes(self.payload)
        self.events: list[ArtifactDownloadEvent] = []
        self.service = ArtifactDeliveryService()
        self.app = Flask(__name__)

        @self.app.route("/artifact")
        def artifact():
            return deliver_artifact(
                self.service,
                self.path,
                request_method=request.method,
                event=ArtifactDownloadEvent(
                    artifact_type="test",
                    artifact_id="artifact",
                    version="1.0",
                ),
                on_completed=self.events.append,
            )

        self.client = self.app.test_client()

    def test_full_get_records_after_complete_iteration(self):
        response = self.client.get("/artifact", buffered=True)
        self.addCleanup(response.close)

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.get_data(), self.payload)
        self.assertEqual(response.headers["Accept-Ranges"], "bytes")
        self.assertEqual(response.headers["X-Content-Type-Options"], "nosniff")
        self.assertEqual(len(self.events), 1)
        self.assertEqual(self.events[0].artifact_id, "artifact")

    def test_head_and_conditional_get_do_not_record(self):
        head = self.client.head("/artifact")
        self.addCleanup(head.close)
        self.assertEqual(head.status_code, 200)
        self.assertEqual(head.get_data(), b"")
        self.assertEqual(head.headers["Content-Length"], str(len(self.payload)))
        self.assertEqual(self.events, [])

        first = self.client.get("/artifact", buffered=True)
        self.addCleanup(first.close)
        etag = first.headers["ETag"]
        self.events.clear()
        conditional = self.client.get(
            "/artifact",
            headers={"If-None-Match": etag},
            buffered=True,
        )
        self.addCleanup(conditional.close)
        self.assertEqual(conditional.status_code, 304)
        self.assertEqual(self.events, [])

    def test_partial_range_does_not_record_but_full_range_does(self):
        partial = self.client.get(
            "/artifact",
            headers={"Range": "bytes=1-2"},
            buffered=True,
        )
        self.addCleanup(partial.close)
        self.assertEqual(partial.status_code, 206)
        self.assertEqual(partial.get_data(), self.payload[1:3])
        self.assertEqual(
            partial.headers["Content-Range"],
            f"bytes 1-2/{len(self.payload)}",
        )
        self.assertEqual(self.events, [])

        complete = self.client.get(
            "/artifact",
            headers={"Range": "bytes=0-"},
            buffered=True,
        )
        self.addCleanup(complete.close)
        self.assertEqual(complete.status_code, 206)
        self.assertEqual(complete.get_data(), self.payload)
        self.assertEqual(len(self.events), 1)

    def test_interrupted_iteration_does_not_record(self):
        response = self.client.get("/artifact", buffered=False)
        iterator = iter(response.response)
        first_chunk = next(iterator)
        self.assertGreater(len(first_chunk), 0)
        self.assertLess(len(first_chunk), len(self.payload))

        response.close()

        self.assertEqual(self.events, [])


if __name__ == "__main__":
    unittest.main()

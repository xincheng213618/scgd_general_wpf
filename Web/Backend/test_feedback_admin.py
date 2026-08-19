import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from services.feedback_admin import (
    get_feedback_detail,
    query_feedback,
    resolve_feedback_attachment,
    update_feedback_status,
    validate_feedback_status_payload,
)


class FeedbackAdminTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.storage = Path(self.temp_dir.name)
        self.feedback_root = self.storage / "Feedback"
        self.feedback_root.mkdir()

    def tearDown(self):
        self.temp_dir.cleanup()

    def _create_feedback(
        self,
        feedback_id: str = "20260812_120000_demo",
        created_at: str = "2026-08-12T12:00:00+00:00",
    ) -> Path:
        directory = self.feedback_root / feedback_id
        directory.mkdir()
        (directory / "feedback.json").write_text(json.dumps({
            "feedbackId": feedback_id,
            "message": "startup problem",
            "userName": "operator",
            "appVersion": "1.2.3.4",
            "machineInfo": "Windows test host",
            "clientIp": "hashed-client",
            "createdAt": created_at,
            "files": ["report.zip"],
        }), encoding="utf-8")
        (directory / "report.zip").write_bytes(b"diagnostic")
        return directory

    def test_query_summarizes_and_filters_without_exposing_details(self):
        self._create_feedback()
        legacy = self.feedback_root / "legacy-folder"
        legacy.mkdir()
        (legacy / "legacy.db").write_bytes(b"db")

        result = query_feedback(self.storage, query="operator", limit=20, offset=0)

        self.assertEqual(result["total"], 1)
        self.assertEqual(result["items"][0]["status"], "new")
        self.assertEqual(result["items"][0]["attachment_count"], 1)
        self.assertNotIn("message", result["items"][0])
        self.assertNotIn("machine_info", result["items"][0])
        self.assertEqual(result["summary"]["records"], 2)
        self.assertEqual(result["summary"]["invalid_metadata"], 1)
        self.assertEqual(result["summary"]["attachment_count"], 2)
        self.assertEqual(result["summary"]["oldest_open_at"], "2026-08-12T12:00:00+00:00")

    def test_open_filter_keeps_new_and_in_progress_feedback(self):
        first = self._create_feedback("20260810_120000_new", "2026-08-10T12:00:00+00:00")
        second = self._create_feedback("20260811_120000_progress", "2026-08-11T12:00:00+00:00")
        resolved = self._create_feedback("20260812_120000_resolved", "2026-08-12T12:00:00+00:00")
        update_feedback_status(self.storage, second.name, "in_progress")
        update_feedback_status(self.storage, resolved.name, "resolved")

        result = query_feedback(self.storage, status="open", limit=20, offset=0)

        self.assertEqual(result["total"], 2)
        self.assertEqual(
            {item["feedback_id"]: item["status"] for item in result["items"]},
            {first.name: "new", second.name: "in_progress"},
        )
        self.assertEqual(result["summary"]["status_counts"]["resolved"], 1)
        self.assertEqual(result["summary"]["oldest_open_at"], "2026-08-10T12:00:00+00:00")

    def test_detail_and_attachment_reject_traversal_and_internal_files(self):
        directory = self._create_feedback()
        detail = get_feedback_detail(self.storage, directory.name)
        self.assertEqual(detail["message"], "startup problem")
        self.assertEqual(detail["attachments"][0]["name"], "report.zip")
        self.assertEqual(
            resolve_feedback_attachment(self.storage, directory.name, "report.zip"),
            directory / "report.zip",
        )

        for feedback_id, filename in (
            ("..", "report.zip"),
            (directory.name, "../feedback.json"),
            (directory.name, "feedback.json"),
            (directory.name, ".admin.json"),
        ):
            with self.subTest(feedback_id=feedback_id, filename=filename), self.assertRaises(FileNotFoundError):
                resolve_feedback_attachment(self.storage, feedback_id, filename)

    def test_status_payload_is_exact_and_status_persists_atomically(self):
        directory = self._create_feedback()
        self.assertEqual(validate_feedback_status_payload({"status": "resolved"}), "resolved")
        for payload in (None, {}, {"status": "closed"}, {"status": "new", "note": "no"}):
            with self.subTest(payload=payload), self.assertRaises(ValueError):
                validate_feedback_status_payload(payload)

        result = update_feedback_status(self.storage, directory.name, "in_progress")
        self.assertTrue(result["changed"])
        self.assertEqual(result["before"], "new")
        self.assertEqual(result["status"], "in_progress")
        persisted = json.loads((directory / ".admin.json").read_text(encoding="utf-8"))
        self.assertEqual(persisted["status"], "in_progress")

    def test_failed_replace_preserves_previous_status(self):
        directory = self._create_feedback()
        with mock.patch(
            "services.feedback_admin.os.replace",
            side_effect=OSError("replace failed"),
        ), self.assertRaises(OSError):
            update_feedback_status(self.storage, directory.name, "resolved")

        self.assertFalse((directory / ".admin.json").exists())
        self.assertEqual(list(directory.glob(".*.tmp")), [])
        self.assertEqual(get_feedback_detail(self.storage, directory.name)["status"], "new")


if __name__ == "__main__":
    unittest.main()

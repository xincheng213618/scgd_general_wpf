from __future__ import annotations

import json
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path
from unittest.mock import patch

from feedback_service import FeedbackValidationError, build_feedback_id, save_feedback


class _Files(dict):
    def getlist(self, key):
        return self[key]


class _Upload:
    def __init__(self, filename: str, payload: bytes):
        self.filename = filename
        self.payload = payload

    def save(self, path: str):
        Path(path).write_bytes(self.payload)


class FeedbackServiceTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.storage = Path(self._temp.name)

    def tearDown(self):
        self._temp.cleanup()

    @staticmethod
    def _sanitize(value: str) -> str:
        return Path(value).name.replace("?", "_")

    def _save(self, *, form=None, uploads=None, **kwargs):
        return save_feedback(
            self.storage,
            form=form or {"message": "diagnostics"},
            files=_Files({"files": uploads or []}),
            remote_addr="127.0.0.1",
            max_feedback_files=10,
            max_feedback_field_length=4000,
            sanitize_filename=self._sanitize,
            hash_ip=lambda value: "hashed-ip" if value else "",
            **kwargs,
        )

    def test_feedback_id_uses_explicit_beijing_time_and_a_unique_machine_independent_identifier(self):
        now = datetime(2026, 9, 15, 16, 30, tzinfo=timezone.utc)
        feedback_id = build_feedback_id(
            now,
            message="test",
            user_name="operator",
            machine_name="Win KAGV/测试?" + "x" * 80,
        )

        self.assertRegex(feedback_id, r"^20260916_003000_BJT_[0-9a-f]{12}$")
        self.assertLessEqual(len(feedback_id), 80)
        self.assertNotEqual(feedback_id, build_feedback_id(
            now,
            message="test",
            user_name="operator",
            machine_name="Win KAGV/测试?" + "x" * 80,
        ))

    def test_verified_owner_and_structured_machine_metadata_are_persisted(self):
        result = self._save(
            form={
                "message": "screen is blank",
                "userName": "client supplied name",
                "machineName": "ARVR-STATION-07",
                "machineInfo": "ARVR-STATION-07 / Windows 11",
                "clientSubmittedAt": "2026-09-16T14:40:38+08:00",
            },
            owner_user_id=42,
            owner_username="verified-account",
        )

        metadata = json.loads((result.feedback_dir / "feedback.json").read_text("utf-8"))
        self.assertEqual(metadata["ownerUserId"], 42)
        self.assertEqual(metadata["ownerUsername"], "verified-account")
        self.assertEqual(metadata["machineName"], "ARVR-STATION-07")
        self.assertEqual(metadata["clientSubmittedAt"], "2026-09-16T06:40:38+00:00")
        self.assertTrue(metadata["serverReceivedAt"].endswith("+00:00"))
        self.assertEqual(result.feedback_dir.parent, self.storage / "Feedback" / "ARVR-STATION-07")

    def test_client_cannot_smuggle_owner_fields_or_internal_metadata_files(self):
        result = self._save(
            form={"message": "test", "ownerUserId": "999", "ownerUsername": "forged"},
        )
        metadata = json.loads((result.feedback_dir / "feedback.json").read_text("utf-8"))
        self.assertIsNone(metadata["ownerUserId"])
        self.assertEqual(metadata["ownerUsername"], "")

        for filename in (
            "feedback.json",
            "FEEDBACK.JSON",
            ".feedback.json.attack.tmp",
            ".admin.json",
            ".admin.attack.tmp",
        ):
            with self.subTest(filename=filename), self.assertRaises(FeedbackValidationError):
                self._save(uploads=[_Upload(filename, b"forged")])

    def test_legacy_client_machine_info_names_new_folder_and_refreshes_share_index(self):
        result = self._save(form={"message": "legacy", "machineInfo": "PC-LEGACY / Windows 10"})
        self.assertEqual(result.feedback_dir.parent.name, "PC-LEGACY")
        self.assertEqual(result.metadata["machineName"], "PC-LEGACY")
        index = self.storage / "Feedback" / "index.html"
        self.assertIn("PC-LEGACY", index.read_text("utf-8"))

    def test_share_index_failure_does_not_turn_a_saved_feedback_into_a_failed_upload(self):
        with patch("services.feedback_admin.write_feedback_index", side_effect=OSError("index blocked")):
            with self.assertLogs("feedback_service", level="WARNING"):
                result = self._save()
        self.assertTrue((result.feedback_dir / "feedback.json").is_file())

    def test_failed_metadata_replace_does_not_look_complete(self):
        with patch("feedback_service.os.replace", side_effect=OSError("disk failure")):
            with self.assertRaises(OSError):
                self._save(uploads=[_Upload("diagnostics.zip", b"zip")])

        directories = list((self.storage / "Feedback" / "UNKNOWN").iterdir())
        self.assertEqual(len(directories), 1)
        self.assertFalse((directories[0] / "feedback.json").exists())
        self.assertEqual(list(directories[0].glob(".feedback.json.*.tmp")), [])


if __name__ == "__main__":
    unittest.main()

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
    update_feedback_statuses,
    validate_feedback_bulk_status_payload,
    validate_feedback_status_payload,
    write_feedback_index,
    get_feedback_handling,
    update_feedback_handling,
    validate_feedback_handling_payload,
    FeedbackHandlingConflict,
)


class FeedbackAdminTests(unittest.TestCase):
    def test_unreadable_management_state_is_not_overwritten_by_status_or_handling(self):
        directory = self._create_feedback()
        state_path = directory / ".admin.json"
        original = b'{"status":"in_progress","handling":'
        state_path.write_bytes(original)
        with self.assertRaises(OSError):
            update_feedback_status(self.storage, directory.name, "resolved")
        result = update_feedback_statuses(self.storage, [directory.name], "resolved")
        self.assertEqual(result["failed"], 1)
        with self.assertRaises(OSError):
            update_feedback_handling(self.storage, directory.name, {
                "conclusion": "new", "fixed_version": "", "verification": "", "revision": 0,
            }, actor_type="user", actor_id="reviewer")
        self.assertEqual(state_path.read_bytes(), original)

    def test_handling_preserves_originals_and_survives_status_changes(self):
        directory = self._create_feedback()
        originals = {name: (directory / name).read_bytes() for name in ("feedback.json", "report.zip")}
        payload = {"conclusion": "原因及处理", "fixed_version": "1.2.3.5", "verification": "现场待验证", "revision": 0}
        with mock.patch("services.feedback_admin._sha256", side_effect=AssertionError("must not hash")):
            first = update_feedback_handling(self.storage, directory.name, payload, actor_type="user", actor_id="maintainer")
            self.assertEqual(first["revision"], 1)
            self.assertEqual(first["actor_id"], "maintainer")
            for status in ("resolved", "in_progress"):
                update_feedback_statuses(self.storage, [directory.name], status)
                self.assertEqual(get_feedback_handling(self.storage, directory.name)["conclusion"], payload["conclusion"])
            update_feedback_status(self.storage, directory.name, "resolved")
            self.assertEqual(get_feedback_handling(self.storage, directory.name)["history"], first["history"])
            self.assertNotIn("handling", get_feedback_detail(self.storage, directory.name, include_hashes=False))
        for name, content in originals.items():
            self.assertEqual((directory / name).read_bytes(), content)
        unchanged = update_feedback_handling(self.storage, directory.name, {**payload, "revision": 1}, actor_type="user", actor_id="other")
        self.assertFalse(unchanged["changed"])
        self.assertEqual(unchanged["actor_id"], "maintainer")
        with self.assertRaises(FeedbackHandlingConflict):
            update_feedback_handling(self.storage, directory.name, payload, actor_type="user", actor_id="other")

    def test_handling_atomic_failure_and_bounded_history(self):
        directory = self._create_feedback()
        payload = {"conclusion": "first", "fixed_version": "", "verification": "", "revision": 0}
        update_feedback_handling(self.storage, directory.name, payload, actor_type="user", actor_id="one")
        before = (directory / ".admin.json").read_bytes()
        with mock.patch("services.feedback_admin.os.replace", side_effect=OSError("disk failure")):
            with self.assertRaises(OSError):
                update_feedback_handling(self.storage, directory.name, {**payload, "conclusion": "second", "revision": 1}, actor_type="user", actor_id="two")
        self.assertEqual((directory / ".admin.json").read_bytes(), before)
        self.assertEqual(list(directory.glob("*.tmp")), [])
        for revision in range(1, 24):
            result = update_feedback_handling(self.storage, directory.name,
                                             {**payload, "conclusion": str(revision), "revision": revision}, actor_type="user", actor_id="one")
        self.assertEqual(len(result["history"]), 20)
        self.assertEqual(result["history"][0]["revision"], 24)
        self.assertEqual(result["history"][-1]["revision"], 5)

    def test_handling_payload_rejects_forged_authorship_and_invalid_fields(self):
        payload = {"conclusion": "", "fixed_version": "", "verification": "", "revision": 0}
        for invalid in ({**payload, "actor_id": "admin"}, {**payload, "revision": True},
                        {**payload, "revision": -1}, {**payload, "conclusion": "x" * 4001},
                        {**payload, "verification": None}, {**payload, "fixed_version": "x" * 101}):
            with self.subTest(payload=invalid), self.assertRaises(ValueError):
                validate_feedback_handling_payload(invalid)

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

    def test_receive_time_order_ignores_copied_mtime_and_normalizes_offsets(self):
        older = self._create_feedback("20260919_010000_old", "2026-09-19T16:00:00+08:00")
        newer = self._create_feedback("20260919_090000_new", "2026-09-19T09:00:00+00:00")
        unknown = self.feedback_root / "unknown-time"
        unknown.mkdir()
        import os
        os.utime(older, (2000000000, 2000000000))
        os.utime(newer, (1000000000, 1000000000))
        result = query_feedback(self.storage)
        self.assertEqual([item["feedback_id"] for item in result["items"]], [newer.name, older.name, unknown.name])
        self.assertEqual(result["items"][-1]["created_at"], "")

    def test_invalid_metadata_time_falls_back_to_legacy_id_not_mtime(self):
        legacy = self._create_feedback("20260919_083417_8465929f5134", "invalid")
        result = get_feedback_detail(self.storage, legacy.name)
        self.assertEqual(result["created_at"], "2026-09-19T08:34:17+00:00")
        modern = self._create_feedback("20260919_163500_BJT_PC_a12345678901", "")
        self.assertEqual(get_feedback_detail(self.storage, modern.name)["created_at"], "2026-09-19T08:35:00+00:00")

    def test_share_index_shows_legacy_machine_and_beijing_time_without_rewriting_feedback(self):
        directory = self._create_feedback("20260919_083417_8465929f5134", "2026-09-19T08:34:17Z")
        metadata_path = directory / "feedback.json"
        metadata = json.loads(metadata_path.read_text("utf-8"))
        metadata["machineInfo"] = '<script>alert(1)</script> / Windows'
        metadata_path.write_text(json.dumps(metadata), encoding="utf-8")
        before = metadata_path.read_bytes()
        index = write_feedback_index(self.storage)
        document = index.read_text("utf-8")
        self.assertIn("2026-09-19 16:34:17", document)
        self.assertIn("&lt;script&gt;", document)
        self.assertNotIn("<script>", document)
        self.assertIn(f'{directory.name}/report.zip', document)
        self.assertEqual(metadata_path.read_bytes(), before)
        self.assertEqual(query_feedback(self.storage)["total"], 1)

    def test_machine_grouping_preserves_record_id_downloads_and_status_after_directory_rename(self):
        old = self._create_feedback("20260919_083417_8465929f5134", "2026-09-19T08:34:17Z")
        original_id = old.name
        machine = self.feedback_root / "PC-STATION"
        machine.mkdir()
        moved = old.rename(machine / "20260919_163417_BJT_8465929f5134")
        self.assertEqual(query_feedback(self.storage)["total"], 1)
        self.assertEqual(query_feedback(self.storage)["items"][0]["feedback_id"], original_id)
        self.assertEqual(resolve_feedback_attachment(self.storage, original_id, "report.zip"), moved / "report.zip")
        self.assertTrue(update_feedback_status(self.storage, original_id, "in_progress")["changed"])
        self.assertEqual(get_feedback_detail(self.storage, original_id)["status"], "in_progress")
        document = write_feedback_index(self.storage).read_text("utf-8")
        self.assertIn("PC-STATION/20260919_163417_BJT_8465929f5134/report.zip", document)
        self.assertEqual(json.loads((moved / "feedback.json").read_text("utf-8"))["feedbackId"], original_id)

    def test_duplicate_stable_ids_are_rejected_instead_of_downloading_from_another_machine(self):
        first = self._create_feedback()
        import shutil
        machine = self.feedback_root / "PC-OTHER"
        machine.mkdir()
        shutil.copytree(first, machine / "another-directory")
        with self.assertRaises(FileNotFoundError):
            resolve_feedback_attachment(self.storage, first.name, "report.zip")

    def test_detail_and_attachment_reject_traversal_and_internal_files(self):
        directory = self._create_feedback()
        (directory / ".feedback.json.abcd.tmp").write_bytes(b"metadata temp")
        (directory / ".admin.json.abcd.tmp").write_bytes(b"state temp")
        detail = get_feedback_detail(self.storage, directory.name)
        self.assertEqual(detail["message"], "startup problem")
        self.assertEqual(detail["attachments"][0]["name"], "report.zip")
        self.assertEqual(detail["attachments"][0]["sha256"], "5a695eea5b00a31f8aef7dbb89c8f798fab371246ac1549afe84b16420707b99")
        self.assertEqual(
            resolve_feedback_attachment(self.storage, directory.name, "report.zip"),
            directory / "report.zip",
        )

        for feedback_id, filename in (
            ("..", "report.zip"),
            (directory.name, "../feedback.json"),
            (directory.name, "feedback.json"),
            (directory.name, ".admin.json"),
            (directory.name, ".feedback.json.abcd.tmp"),
            (directory.name, ".admin.json.abcd.tmp"),
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

    def test_lightweight_detail_and_status_update_never_hash_attachments(self):
        directory = self._create_feedback()
        with mock.patch("services.feedback_admin._sha256", side_effect=AssertionError("must not hash")):
            detail = get_feedback_detail(self.storage, directory.name, include_hashes=False)
            updated = update_feedback_status(self.storage, directory.name, "resolved")
        self.assertEqual(detail["attachments"][0]["name"], "report.zip")
        self.assertNotIn("sha256", detail["attachments"][0])
        self.assertEqual(updated["status"], "resolved")

    def test_bulk_status_updates_only_explicit_ids_is_idempotent_and_preserves_originals(self):
        first = self._create_feedback("first")
        second = self._create_feedback("second")
        new_arrival = self._create_feedback("new-arrival")
        original = {path: path.read_bytes() for directory in (first, second, new_arrival)
                    for path in directory.iterdir()}
        with mock.patch("services.feedback_admin._sha256", side_effect=AssertionError("must not hash")):
            result = update_feedback_statuses(self.storage, [first.name, second.name], "resolved")
            self.assertEqual((result["changed"], result["failed"]), (2, 0))
            second_state = (second / ".admin.json").read_bytes()
            again = update_feedback_statuses(self.storage, [first.name, second.name], "resolved")
            self.assertEqual((again["changed"], again["unchanged"]), (0, 2))
            self.assertEqual((second / ".admin.json").read_bytes(), second_state)
        self.assertFalse((new_arrival / ".admin.json").exists())
        for path, content in original.items():
            self.assertEqual(path.read_bytes(), content)

    def test_bulk_status_reports_partial_failure_without_hiding_successes(self):
        first = self._create_feedback("first")
        second = self._create_feedback("second")
        import os
        original_replace = os.replace

        def fail_second(source, target):
            if Path(target).parent == second:
                raise OSError("locked")
            original_replace(source, target)

        with mock.patch("services.feedback_admin.os.replace", side_effect=fail_second):
            result = update_feedback_statuses(self.storage, [first.name, second.name, "missing"], "resolved")
        self.assertEqual((result["changed"], result["failed"]), (1, 2))
        self.assertEqual(result["results"][0]["status"], "resolved")
        self.assertIn("error", result["results"][1])
        self.assertFalse((second / ".admin.json").exists())
        self.assertEqual(list(second.glob(".*.tmp")), [])

    def test_bulk_payload_rejects_invalid_ids_duplicates_and_oversized_selection(self):
        for identifiers in ([], "one", ["../escape"], ["one", "one"], [None], [str(i) for i in range(501)]):
            with self.subTest(identifiers=identifiers), self.assertRaises(ValueError):
                validate_feedback_bulk_status_payload({"feedback_ids": identifiers, "status": "resolved"})
        with self.assertRaises(ValueError):
            validate_feedback_bulk_status_payload({"feedback_ids": ["one"], "status": "closed"})

    def test_owner_scope_filters_before_summary_search_and_pagination(self):
        own = self._create_feedback("20260916_BJT_PC1_own", "2026-09-16T06:00:00+00:00")
        other = self._create_feedback("20260916_BJT_PC2_other", "2026-09-16T07:00:00+00:00")
        own_metadata = json.loads((own / "feedback.json").read_text("utf-8"))
        own_metadata.update({"ownerUserId": 11, "ownerUsername": "alice", "machineName": "PC-1"})
        (own / "feedback.json").write_text(json.dumps(own_metadata), encoding="utf-8")
        other_metadata = json.loads((other / "feedback.json").read_text("utf-8"))
        other_metadata.update({"ownerUserId": 22, "ownerUsername": "bob", "machineName": "PC-2"})
        (other / "feedback.json").write_text(json.dumps(other_metadata), encoding="utf-8")

        result = query_feedback(self.storage, owner_user_id=11, query="bob", limit=1, offset=0)

        self.assertEqual(result["total"], 0)
        self.assertEqual(result["summary"]["records"], 1)
        self.assertEqual(result["summary"]["attachment_count"], 1)
        with self.assertRaises(FileNotFoundError):
            get_feedback_detail(self.storage, other.name, owner_user_id=11)
        with self.assertRaises(FileNotFoundError):
            resolve_feedback_attachment(self.storage, other.name, "report.zip", owner_user_id=11)

    def test_legacy_unbound_machine_and_time_filters_remain_admin_visible(self):
        legacy = self._create_feedback("legacy-feedback", "2026-09-15T16:30:00+00:00")
        metadata = json.loads((legacy / "feedback.json").read_text("utf-8"))
        metadata["machineInfo"] = "ARVR-LEGACY / Windows 10"
        metadata["appVersion"] = "1.4.14.71"
        (legacy / "feedback.json").write_text(json.dumps(metadata), encoding="utf-8")

        result = query_feedback(
            self.storage,
            machine="legacy",
            app_version="14.71",
            created_from="2026-09-15T16:00:00+00:00",
            created_to="2026-09-15T17:00:00+00:00",
            limit=20,
            offset=0,
        )

        self.assertEqual(result["total"], 1)
        self.assertEqual(result["items"][0]["machine_name"], "ARVR-LEGACY")
        self.assertEqual(result["items"][0]["ownership"], "legacy_unbound")
        self.assertEqual(query_feedback(self.storage, owner_user_id=11, limit=20, offset=0)["total"], 0)


if __name__ == "__main__":
    unittest.main()

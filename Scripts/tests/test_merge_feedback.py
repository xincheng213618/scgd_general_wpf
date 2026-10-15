import contextlib
import gzip
import io
import json
from pathlib import Path
import sqlite3
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import merge_feedback as merger


class MergeFeedbackTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name) / "MACHINE"
        self.root.mkdir()
        self.output = self.root / "_aggregate"

    def tearDown(self):
        self.temporary.cleanup()

    def feedback(self, name, day, rows=None, *, new_schema=False, machine="MACHINE", unsafe=False, project=False):
        folder = self.root / name
        folder.mkdir()
        metadata = {"feedbackId": name, "createdAt": f"2026-09-{day:02}T12:00:00+08:00"}
        (folder / "feedback.json").write_text(json.dumps(metadata), encoding="utf-8")
        path = folder / "diagnostics.zip"
        database = folder / "fixture.db"
        project_database = folder / "project.db"
        if rows is not None:
            with contextlib.closing(sqlite3.connect(database)) as db, db:
                db.execute("CREATE TABLE FlowNodeRecord(id INTEGER PRIMARY KEY,batch_id INTEGER,serial_number TEXT,node_id TEXT,start_time TEXT,end_time TEXT,elapsed_ms INTEGER)")
                db.execute("CREATE INDEX idx_node_batch ON FlowNodeRecord(batch_id,start_time)")
                db.executemany("INSERT INTO FlowNodeRecord VALUES(?,?,?,?,?,?,?)", [(row[0], row[0], row[1], "node", f"2026-09-10 10:{row[0]:02}:00", row[2], row[3]) for row in rows])
                payload_column = "send_payload_gzip BLOB" if new_schema else "send_payload TEXT"
                db.execute(f"CREATE TABLE FlowNodeMessage(id INTEGER PRIMARY KEY,batch_id INTEGER,serial_number TEXT,node_id TEXT,node_record_id INTEGER,msg_id TEXT,send_time TEXT,{payload_column})")
                for row in rows:
                    content = row[1] + " payload"
                    db.execute("INSERT INTO FlowNodeMessage VALUES(?,?,?,?,?,?,?,?)", (row[0], row[0], row[1], "node", row[0], f"msg{row[0]}", f"2026-09-10 10:{row[0]:02}:00", gzip.compress(content.encode(), mtime=0) if new_schema else content))
                if new_schema:
                    db.execute("ALTER TABLE FlowNodeRecord ADD COLUMN optional_new TEXT")
                    db.execute("UPDATE FlowNodeRecord SET optional_new='available'")
            if project:
                with contextlib.closing(sqlite3.connect(project_database)) as db, db:
                    db.execute("CREATE TABLE ARVRReuslt(Id INTEGER PRIMARY KEY,SN TEXT,BatchId INTEGER,TestType INTEGER,Model TEXT,CreateTime TEXT,RunTime INTEGER)")
                    db.execute("CREATE TABLE ObjectiveTestResultRecord(Id INTEGER PRIMARY KEY,SN TEXT,ResultId INTEGER,BatchId INTEGER,CreateTime TEXT,UpdateTime TEXT,TotalResult INTEGER)")
                    for row in rows:
                        start = f"2026-09-10 10:{row[0]:02}:00"
                        db.execute("INSERT INTO ARVRReuslt VALUES(?,?,?,?,?,?,?)", (row[0], row[1], row[0], 1, "PG", start, row[3]))
                        db.execute("INSERT INTO ObjectiveTestResultRecord VALUES(?,?,?,?,?,?,?)", (row[0], row[1], row[0], row[0], start, start, 1))
        with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as z:
            z.writestr("SystemInfo.txt", f"MachineName: {machine}\nVersion: 1.4.15.3\n")
            z.writestr("AppLogs/same-name.txt", "original " + name)
            z.writestr("Config/ColorVisionConfig.json", json.dumps({"source": name}))
            if rows is not None:
                z.write(database, "Database/FlowNodeRecords.db")
                z.writestr("Database/FlowNodeRecords.db.export.json", json.dumps({"Status": "ok", "From": "2026-09-10", "To": "2026-09-20", "Tables": {"FlowNodeRecord": len(rows)}}))
                if project:
                    z.write(project_database, "Database/ProjectARVRPro.db")
            if unsafe:
                z.writestr("../escape.txt", "bad")
        database.unlink(missing_ok=True)
        project_database.unlink(missing_ok=True)
        return path

    def apply(self, selected=None):
        with contextlib.redirect_stdout(io.StringIO()):
            return merger.apply(self.root, merger.discover(self.root, selected))

    def database(self, manifest):
        return self.output / manifest["databaseDirectory"] / "FlowNodeRecords.db"

    def test_preview_writes_nothing_and_preserves_sources(self):
        path = self.feedback("first", 12, [(1, "SN", None, 0)])
        before = merger.sha256(path)
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, merger.main([str(self.root)]))
        self.assertFalse(self.output.exists())
        self.assertEqual(before, merger.sha256(path))

    def test_overlap_schema_payloads_associations_and_source_evidence(self):
        first = self.feedback("first", 12, [(1, "SN1", None, 0), (2, "SN2", "done", 200)])
        second = self.feedback("second", 13, [(1, "SN1", "done", 100), (3, "SN3", "done", 300)], new_schema=True)
        hashes = [merger.sha256(first), merger.sha256(second)]
        manifest = self.apply()
        self.assertEqual(3, manifest["tables"]["FlowNodeRecords.db"]["FlowNodeRecord"])
        with contextlib.closing(sqlite3.connect(self.database(manifest))) as db:
            self.assertEqual(("done", 100, "available"), db.execute("SELECT end_time,elapsed_ms,optional_new FROM FlowNodeRecord WHERE id=1").fetchone())
            self.assertIsNone(db.execute("SELECT optional_new FROM FlowNodeRecord WHERE id=2").fetchone()[0])
            self.assertEqual(3, db.execute("SELECT COUNT(*) FROM FlowNodeMessage m JOIN FlowNodeRecord n ON m.node_record_id=n.id AND m.batch_id=n.batch_id AND m.serial_number=n.serial_number").fetchone()[0])
            blob = db.execute("SELECT send_payload_gzip FROM FlowNodeMessage WHERE id=1").fetchone()[0]
            self.assertEqual("SN1 payload", gzip.decompress(blob).decode())
            self.assertEqual(2, db.execute("SELECT COUNT(*) FROM _aggregate_row_source WHERE table_name='flownoderecord' AND row_id=1").fetchone()[0])
            self.assertIn("idx_node_batch", [r[1] for r in db.execute("PRAGMA index_list(FlowNodeRecord)")])
        counts = manifest["sources"][1]["mergeCounts"]["FlowNodeRecords.db"]["FlowNodeRecord"]
        self.assertEqual({"read": 2, "inserted": 1, "overlap": 1, "updated": 1}, counts)
        for source in manifest["sources"]:
            evidence = self.output / source["evidenceDirectory"]
            self.assertEqual("original " + source["feedbackId"], (evidence / "AppLogs/same-name.txt").read_text())
        self.assertEqual(hashes, [merger.sha256(first), merger.sha256(second)])

    def test_incremental_update_is_idempotent_and_previous_generation_is_immutable(self):
        self.feedback("first", 12, [(1, "SN1", "done", 100)])
        first = self.apply()
        previous_hash = merger.sha256(self.database(first))
        manifest_hash = merger.sha256(self.output / "aggregate.json")
        self.apply()
        self.assertEqual(manifest_hash, merger.sha256(self.output / "aggregate.json"))
        self.feedback("second", 13, [(2, "SN2", "done", 200)])
        second = self.apply()
        self.assertEqual(2, second["tables"]["FlowNodeRecords.db"]["FlowNodeRecord"])
        self.assertEqual(previous_hash, merger.sha256(self.database(first)))
        self.assertNotEqual(first["databaseDirectory"], second["databaseDirectory"])

    def test_backfilled_older_snapshot_does_not_overwrite_newer_state(self):
        self.feedback("newer", 15, [(1, "SN", "done", 100)])
        self.apply()
        self.feedback("older", 12, [(1, "SN", None, 0), (2, "old-only", "done", 50)])
        manifest = self.apply()
        with contextlib.closing(sqlite3.connect(self.database(manifest))) as db:
            self.assertEqual(("done", 100), db.execute("SELECT end_time,elapsed_ms FROM FlowNodeRecord WHERE id=1").fetchone())
            self.assertEqual("newer", db.execute("SELECT feedback_id FROM _aggregate_row_head WHERE table_name='flownoderecord' AND row_id=1").fetchone()[0])
            self.assertEqual(2, db.execute("SELECT COUNT(*) FROM FlowNodeRecord").fetchone()[0])

    def test_identity_conflict_does_not_publish_partial_generation(self):
        self.feedback("first", 12, [(1, "SN1", "done", 100)])
        manifest = self.apply()
        previous = merger.sha256(self.output / "aggregate.json")
        database_hash = merger.sha256(self.database(manifest))
        self.feedback("conflict", 13, [(1, "DIFFERENT", "done", 100)])
        with self.assertRaisesRegex(merger.MergeError, "身份冲突"):
            self.apply()
        self.assertEqual(previous, merger.sha256(self.output / "aggregate.json"))
        self.assertEqual(database_hash, merger.sha256(self.database(manifest)))
        self.assertFalse((self.output / ".merge.lock").exists())

    def test_original_altered_after_import_is_rejected(self):
        path = self.feedback("first", 12, [(1, "SN", "done", 100)])
        self.apply()
        previous = merger.sha256(self.output / "aggregate.json")
        with zipfile.ZipFile(path, "a") as z:
            z.writestr("new.txt", "altered")
        with self.assertRaisesRegex(merger.MergeError, "原反馈内容变化"):
            self.apply()
        self.assertEqual(previous, merger.sha256(self.output / "aggregate.json"))

    def test_log_only_feedback_remains_available_and_can_later_receive_databases(self):
        self.feedback("logs", 12)
        first = self.apply()
        self.assertEqual({}, first["tables"])
        self.feedback("data", 13, [(1, "SN", "done", 100)])
        second = self.apply()
        self.assertEqual(2, len(second["sources"]))
        self.assertEqual(1, second["tables"]["FlowNodeRecords.db"]["FlowNodeRecord"])

    def test_foreign_machine_and_unsafe_zip_are_rejected_before_writes(self):
        with self.subTest("foreign machine"):
            self.feedback("foreign", 12, machine="OTHER")
            with self.assertRaisesRegex(merger.MergeError, "机器身份"):
                merger.discover(self.root)
        with self.subTest("zip traversal"):
            self.feedback("unsafe", 13, unsafe=True)
            with self.assertRaisesRegex(merger.MergeError, "ZIP 路径"):
                merger.discover(self.root, ["unsafe"])
        self.assertFalse(self.output.exists())

    def test_original_feedback_id_is_stable_when_directory_is_renamed(self):
        path = self.feedback("first", 12)
        path.parent.rename(self.root / "renamed-folder")
        self.assertEqual("first", merger.discover(self.root)[0]["feedbackId"])

    def test_existing_merge_lock_is_preserved(self):
        self.feedback("first", 12)
        self.output.mkdir()
        lock = self.output / ".merge.lock"
        lock.write_text("other process")
        with self.assertRaisesRegex(merger.MergeError, "已有合并任务"):
            self.apply()
        self.assertEqual("other process", lock.read_text())

    def test_repeated_sn_is_not_collapsed_and_result_references_keep_original_ids(self):
        self.feedback("first", 12, [(1, "sameSN", "done", 100), (2, "sameSN", "done", 200)], project=True)
        self.feedback("second", 13, [(2, "sameSN", "done", 200), (3, "sameSN", "done", 300)], project=True)
        manifest = self.apply()
        path = self.output / manifest["databaseDirectory"] / "ProjectARVRPro.db"
        with contextlib.closing(sqlite3.connect(path)) as db:
            self.assertEqual(3, db.execute("SELECT COUNT(*) FROM ObjectiveTestResultRecord").fetchone()[0])
            self.assertEqual(3, db.execute("SELECT COUNT(*) FROM ObjectiveTestResultRecord o JOIN ARVRReuslt r ON o.ResultId=r.id AND o.BatchId=r.BatchId AND o.SN=r.SN").fetchone()[0])
            self.assertEqual(200, db.execute("SELECT AVG(RunTime) FROM ARVRReuslt").fetchone()[0])

    def test_precise_legacy_run_identity_allows_completion_update(self):
        destination = Path(self.temporary.name) / "merged.db"
        for day, elapsed, completed in ((12, 0, "2026-09-10 10:00:00"), (13, 100, "2026-09-10 10:00:01")):
            source = Path(self.temporary.name) / f"run-{day}.db"
            with contextlib.closing(sqlite3.connect(source)) as db, db:
                db.execute("CREATE TABLE FlowRunRecord(id INTEGER PRIMARY KEY,template_id INTEGER,serial_number TEXT,completed_time TEXT,elapsed_ms INTEGER)")
                db.execute("INSERT INTO FlowRunRecord VALUES(1,10,'SN_20260910T100000.1234567',?,?)", (completed, elapsed))
            merger.merge_database(source, destination, {"feedbackId": f"run-{day}", "receivedAt": f"2026-09-{day:02}T00:00:00+00:00"})
        with contextlib.closing(sqlite3.connect(destination)) as db:
            self.assertEqual(100, db.execute("SELECT elapsed_ms FROM FlowRunRecord").fetchone()[0])

    def test_equal_receive_times_have_deterministic_source_order(self):
        self.feedback("z-later", 12, [(1, "SN", "done", 100)])
        self.apply()
        self.feedback("a-earlier", 12, [(1, "SN", None, 0)])
        manifest = self.apply()
        with contextlib.closing(sqlite3.connect(self.database(manifest))) as db:
            self.assertEqual(100, db.execute("SELECT elapsed_ms FROM FlowNodeRecord").fetchone()[0])

    def test_matching_sn_and_id_without_a_time_anchor_are_not_enough_to_deduplicate(self):
        destination = Path(self.temporary.name) / "ambiguous.db"
        for day in (12, 13):
            source = Path(self.temporary.name) / f"ambiguous-{day}.db"
            with contextlib.closing(sqlite3.connect(source)) as db, db:
                db.execute("CREATE TABLE ObjectiveTestResultRecord(id INTEGER PRIMARY KEY,SN TEXT,CreateTime TEXT)")
                db.execute("INSERT INTO ObjectiveTestResultRecord VALUES(1,'sameSN',NULL)")
            feedback = {"feedbackId": f"ambiguous-{day}", "receivedAt": f"2026-09-{day:02}T00:00:00+00:00"}
            if day == 12:
                merger.merge_database(source, destination, feedback)
            else:
                with self.assertRaisesRegex(merger.MergeError, "身份冲突"):
                    merger.merge_database(source, destination, feedback)


if __name__ == "__main__":
    unittest.main()

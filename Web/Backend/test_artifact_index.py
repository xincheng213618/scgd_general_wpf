"""Tests for artifact index (release/update/tool), scheduler no-op, and security."""

import base64
import copy
import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import app as marketplace_app
from db_cache import CacheManager


class ArtifactIndexTests(unittest.TestCase):
    """Tests for release_index, update_index, tool_index services."""

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.storage = self.root / "storage"
        (self.storage / "Plugins").mkdir(parents=True, exist_ok=True)

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)
        self.original_testing = marketplace_app.app.config.get("TESTING", False)
        self.original_secret_key = marketplace_app.app.secret_key

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {"username": "tester", "password": "secret"}
        marketplace_app.CONFIG["secret_key"] = "test-secret-key"
        marketplace_app.CONFIG["debug"] = False
        marketplace_app.app.secret_key = marketplace_app.CONFIG["secret_key"]
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.app.config["MAX_CONTENT_LENGTH"] = marketplace_app.MAX_UPLOAD_SIZE_BYTES
        marketplace_app.init_db()

        self.cache = CacheManager(self.root / "marketplace.db")
        self.cache.init_db()

        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.secret_key = self.original_secret_key
        marketplace_app.app.config["TESTING"] = self.original_testing
        self.temp_dir.cleanup()

    def _auth_headers(self, username="tester", password="secret"):
        token = base64.b64encode(f"{username}:{password}".encode("utf-8")).decode("ascii")
        return {"Authorization": f"Basic {token}"}

    def _create_release(self, version, suffix=".exe", payload=b"release", in_history=False):
        if in_history:
            parts = version.split(".")
            d = self.storage / "History" / ".".join(parts[:2]) / ".".join(parts[:3])
            d.mkdir(parents=True, exist_ok=True)
        else:
            d = self.storage
        path = d / f"ColorVision-{version}{suffix}"
        path.write_bytes(payload)
        return path

    def _create_update(self, version, payload=b"update"):
        d = self.storage / "Update"
        d.mkdir(parents=True, exist_ok=True)
        path = d / f"ColorVision-Update-[{version}].cvx"
        path.write_bytes(payload)
        return path

    def _create_tool(self, name, is_dir=False, payload=b"tool"):
        d = self.storage / "Tool"
        d.mkdir(parents=True, exist_ok=True)
        if is_dir:
            td = d / name
            td.mkdir(parents=True, exist_ok=True)
            (td / "file.txt").write_bytes(payload)
            return td
        else:
            path = d / name
            path.write_bytes(payload)
            return path

    # -------------------------------------------------------------------
    # Release index tests
    # -------------------------------------------------------------------

    def test_refresh_release_index_populates_table(self):
        self._create_release("1.2.0.1")
        self._create_release("1.0.0.1", in_history=True)

        from services.artifact_index import refresh_release_index
        result = refresh_release_index(self.cache, self.storage)

        self.assertEqual(result["indexed_count"], 2)
        self.assertEqual(len(result["errors"]), 0)

        db = self.cache.get_db()
        rows = db.execute("SELECT * FROM release_index WHERE is_deleted = 0").fetchall()
        db.close()
        self.assertEqual(len(rows), 2)

    def test_get_releases_from_index_returns_none_when_empty(self):
        from services.artifact_index import get_releases_from_index
        result = get_releases_from_index(self.cache)
        self.assertIsNone(result)

    def test_get_releases_from_index_returns_data_after_refresh(self):
        self._create_release("2.0.0.1")

        from services.artifact_index import refresh_release_index, get_releases_from_index
        refresh_release_index(self.cache, self.storage)

        result = get_releases_from_index(self.cache)
        self.assertIsNotNone(result)
        self.assertEqual(len(result), 1)
        self.assertEqual(result[0]["version"], "2.0.0.1")

    def test_release_index_marks_deleted_on_refresh(self):
        self._create_release("1.0.0.1")
        from services.artifact_index import refresh_release_index
        refresh_release_index(self.cache, self.storage)

        # Remove the file
        import shutil
        shutil.rmtree(self.storage / "History" / "1.0" / "1.0.0", ignore_errors=True)
        (self.storage / "ColorVision-1.0.0.1.exe").unlink(missing_ok=True)

        # Actually we need to just delete the current release
        (self.storage / "ColorVision-1.0.0.1.exe").unlink(missing_ok=True)

        result = refresh_release_index(self.cache, self.storage)
        self.assertEqual(result["indexed_count"], 0)
        db = self.cache.get_db()
        row = db.execute("SELECT * FROM release_index WHERE version = '1.0.0.1'").fetchone()
        db.close()
        self.assertIsNone(row)

    def test_update_and_tool_tombstones_are_physically_deleted(self):
        update_path = self._create_update("1.0.0.1")
        tool_path = self._create_tool("OldTool.exe")
        from services.artifact_index import refresh_tool_index, refresh_update_index
        refresh_update_index(self.cache, self.storage)
        refresh_tool_index(self.cache, self.storage)

        update_path.unlink()
        tool_path.unlink()
        refresh_update_index(self.cache, self.storage)
        refresh_tool_index(self.cache, self.storage)

        db = self.cache.get_db()
        update_row = db.execute("SELECT * FROM update_index WHERE filename = 'ColorVision-Update-[1.0.0.1].cvx'").fetchone()
        tool_row = db.execute("SELECT * FROM tool_index WHERE name = 'OldTool.exe'").fetchone()
        db.close()
        self.assertIsNone(update_row)
        self.assertIsNone(tool_row)

    def test_scan_failures_preserve_old_indexes_and_set_error_state(self):
        self._create_release("1.0.0.1")
        self._create_update("1.0.0.1")
        self._create_tool("Tool.exe")
        from services.artifact_index import (
            get_index_state,
            refresh_release_index,
            refresh_tool_index,
            refresh_update_index,
        )
        refresh_release_index(self.cache, self.storage)
        refresh_update_index(self.cache, self.storage)
        refresh_tool_index(self.cache, self.storage)

        cases = (
            ("releases", "release_index", "_scan_release_artifacts", refresh_release_index),
            ("updates", "update_index", "_scan_update_packages", refresh_update_index),
            ("tools", "tool_index", "_scan_tool_entries", refresh_tool_index),
        )
        for scope, table, scanner, refresh in cases:
            with self.subTest(scope=scope), patch(
                f"services.artifact_index.{scanner}", side_effect=OSError(f"{scope} unavailable")
            ):
                result = refresh(self.cache, self.storage)
                self.assertTrue(result["errors"])
                db = self.cache.get_db()
                active_count = db.execute(
                    f"SELECT COUNT(*) AS cnt FROM {table} WHERE is_deleted = 0"
                ).fetchone()["cnt"]
                db.close()
                self.assertEqual(active_count, 1)
                state = get_index_state(self.cache, scope)
                self.assertEqual(state["status"], "error")
                self.assertIn("unavailable", state["last_error"])

    def test_unavailable_storage_root_does_not_clear_release_index(self):
        self._create_release("1.0.0.1")
        from services.artifact_index import get_index_state, refresh_release_index
        refresh_release_index(self.cache, self.storage)
        missing_storage = self.root / "offline-storage"

        result = refresh_release_index(self.cache, missing_storage)

        self.assertTrue(result["errors"])
        db = self.cache.get_db()
        count = db.execute(
            "SELECT COUNT(*) AS cnt FROM release_index WHERE is_deleted = 0"
        ).fetchone()["cnt"]
        db.close()
        self.assertEqual(count, 1)
        self.assertEqual(get_index_state(self.cache, "releases")["status"], "error")

    def test_release_signature_uses_directory_entries_without_stating_history_files(self):
        history_file = self._create_release("1.0.0.1", in_history=True)
        from services.artifact_index import _release_signature
        original_stat = Path.stat

        def guarded_stat(path, *args, **kwargs):
            if path == history_file:
                raise AssertionError("history files must not be stat'ed")
            return original_stat(path, *args, **kwargs)

        with patch.object(Path, "stat", new=guarded_stat):
            before = _release_signature(self.storage)

        second = history_file.with_name("ColorVision-1.0.0.2.exe")
        second.write_bytes(b"release-2")
        with patch.object(Path, "stat", new=guarded_stat):
            after_add = _release_signature(self.storage)
        second.unlink()
        with patch.object(Path, "stat", new=guarded_stat):
            after_delete = _release_signature(self.storage)

        self.assertNotEqual(before, after_add)
        self.assertNotEqual(after_add, after_delete)

    def test_release_signature_preserves_subsecond_root_mtime_precision(self):
        release = self._create_release("1.0.0.1")
        from services.artifact_index import _release_signature
        base_ns = 1_700_000_000_000_000_000
        os.utime(release, ns=(base_ns, base_ns))
        first = _release_signature(self.storage)
        os.utime(release, ns=(base_ns + 100_000_000, base_ns + 100_000_000))
        second = _release_signature(self.storage)

        self.assertNotEqual(first, second)

    def test_release_signature_detects_same_name_history_file_overwrite(self):
        release = self._create_release("1.0.0.1", in_history=True)
        from services.artifact_index import _release_signature

        branch = release.parent
        branch_stat = branch.stat()
        first = _release_signature(self.storage)

        release.write_bytes(b"replacement-release-with-a-different-size")
        os.utime(
            branch,
            ns=(branch_stat.st_atime_ns, branch_stat.st_mtime_ns),
        )
        second = _release_signature(self.storage)

        self.assertNotEqual(first, second)

    def test_release_index_state_updated(self):
        self._create_release("1.0.0.1")
        from services.artifact_index import refresh_release_index, get_index_state
        refresh_release_index(self.cache, self.storage)

        state = get_index_state(self.cache, "releases")
        self.assertIsNotNone(state)
        self.assertEqual(state["status"], "ready")
        self.assertGreater(state["item_count"], 0)

    def test_compact_release_read_model_keeps_group_totals_across_pages(self):
        for fix in range(150):
            suffix = ".zip" if fix % 2 == 0 else ".exe"
            self._create_release(f"2.0.0.{fix}", suffix=suffix, in_history=True)

        from services.artifact_index import (
            get_compact_releases_from_index,
            refresh_release_index,
        )
        refresh_release_index(self.cache, self.storage)

        first = get_compact_releases_from_index(self.cache, page=1, page_size=100)
        second = get_compact_releases_from_index(self.cache, page=2, page_size=100)

        self.assertIsNotNone(first)
        self.assertIsNotNone(second)
        for payload, expected_page_count in ((first, 100), (second, 50)):
            group = payload["archive_visible_groups"][0]
            self.assertEqual(group["visible_count"], 150)
            self.assertEqual(group["page_item_count"], expected_page_count)
            self.assertIn("ZIP 归档", group["visible_kind_summary"])
            self.assertIn("安装包", group["visible_kind_summary"])
            self.assertIn("压缩归档时代", group["visible_era_summary"])
            self.assertIn("安装包时代", group["visible_era_summary"])

    def test_compact_releases_bounds_5000_archived_android_rows(self):
        rows = []
        for index in range(5000):
            version = f"3.0.{index // 1000}.{index % 1000}"
            filename = f"ColorVision-Android-{version}.apk"
            rows.append((
                f"History/3.0/3.0.{index // 1000}/{filename}",
                version,
                filename,
                1024 + index,
                "APK",
                "Android 安装包",
                "android",
                "Android 应用",
                "archive",
                "3.0",
                f"3.0.{index // 1000}",
                f"2026-01-{(index % 28) + 1:02d}T00:00:00+00:00",
                f"2026-01-{(index % 28) + 1:02d} 00:00",
                f"ColorVision Android {version}",
            ))
        db = self.cache.get_db()
        db.executemany(
            """INSERT INTO release_index
               (relative_path, version, filename, size, kind, kind_label, era,
                era_label, source, major_minor, branch, modified,
                modified_display, display_title, is_deleted)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0)""",
            rows,
        )
        db.execute(
            """INSERT INTO index_state (scope, signature, status, item_count)
               VALUES ('releases', 'bulk-test', 'ready', 5000)
               ON CONFLICT(scope) DO UPDATE SET status = 'ready', item_count = 5000"""
        )
        db.commit()
        db.close()

        response = self.client.get(
            "/api/site/releases?view=compact&android_page=2&android_page_size=20"
        )

        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertNotIn("android_releases", payload["app_info"])
        self.assertEqual(payload["app_info"]["android_count"], 5000)
        self.assertEqual(payload["app_info"]["archive_android_count"], 5000)
        self.assertEqual(len(payload["app_info"]["archived_android_releases"]), 20)
        self.assertEqual(payload["android_page"], 2)
        self.assertEqual(payload["android_page_size"], 20)
        self.assertEqual(payload["android_total_pages"], 250)
        self.assertEqual(payload["android_total_item_count"], 5000)
        self.assertTrue(payload["android_has_previous"])
        self.assertTrue(payload["android_has_next"])
        self.assertLess(len(json.dumps(payload, ensure_ascii=False).encode("utf-8")), 64 * 1024)

    def test_compact_routes_do_not_call_full_legacy_release_builders(self):
        self._create_release("2.0.0.1")
        self._create_release("1.0.0.1", in_history=True)
        (self.storage / "CHANGELOG.md").write_text("## 2.0.0.1\n- bounded", encoding="utf-8")
        from services.artifact_index import refresh_release_index
        refresh_release_index(self.cache, self.storage)

        with patch.object(
            marketplace_app.SERVICES,
            "get_request_release_app_info",
            side_effect=AssertionError("legacy releases builder must not run"),
        ):
            releases = self.client.get("/api/site/releases?view=compact&page_size=20")
        with patch.object(
            marketplace_app.SERVICES,
            "get_request_home_app_info",
            side_effect=AssertionError("legacy home builder must not run"),
        ):
            home = self.client.get("/api/site/home?view=compact")
        with patch.object(
            marketplace_app.SERVICES,
            "get_request_changelog_app_info",
            side_effect=AssertionError("legacy changelog builder must not run"),
        ):
            changelog = self.client.get("/api/site/changelog?view=compact")

        self.assertEqual(releases.status_code, 200)
        self.assertEqual(home.status_code, 200)
        self.assertEqual(changelog.status_code, 200)
        self.assertEqual(set(changelog.get_json()["app_info"]), {"latest_version", "changelog_html"})

    def test_artifact_refresh_persists_pre_scan_signature_for_all_scopes(self):
        self._create_release("1.0.0.1")
        self._create_update("1.0.0.1")
        self._create_tool("Tool-1.exe")
        import services.artifact_index as artifact_index
        from services.scheduler import _run_artifact_index_check

        cases = (
            (
                "releases", "release_index", artifact_index._release_signature,
                artifact_index._scan_release_artifacts, artifact_index.refresh_release_index,
                "_scan_release_artifacts", lambda: self._create_release("2.0.0.1"),
            ),
            (
                "updates", "update_index", artifact_index._update_signature,
                artifact_index._scan_update_packages, artifact_index.refresh_update_index,
                "_scan_update_packages", lambda: self._create_update("2.0.0.1"),
            ),
            (
                "tools", "tool_index", artifact_index._tool_signature,
                artifact_index._scan_tool_entries, artifact_index.refresh_tool_index,
                "_scan_tool_entries", lambda: self._create_tool("Tool-2.exe"),
            ),
        )
        for scope, table, signature_fn, scanner, refresh, scanner_name, mutate in cases:
            with self.subTest(scope=scope):
                pre_scan_signature = signature_fn(self.storage)

                def scan_then_mutate(storage, _scanner=scanner, _mutate=mutate):
                    snapshot = _scanner(storage)
                    _mutate()
                    return snapshot

                with patch(
                    f"services.artifact_index.{scanner_name}",
                    side_effect=scan_then_mutate,
                ):
                    result = refresh(self.cache, self.storage)

                self.assertTrue(result["changed_during_refresh"])
                state = artifact_index.get_index_state(self.cache, scope)
                self.assertEqual(state["signature"], pre_scan_signature)
                self.assertNotEqual(state["signature"], signature_fn(self.storage))
                db = self.cache.get_db()
                before_count = db.execute(
                    f"SELECT COUNT(*) AS cnt FROM {table} WHERE is_deleted = 0"
                ).fetchone()["cnt"]
                db.close()
                self.assertEqual(before_count, 1)

                summary = _run_artifact_index_check(self.cache, self.storage, scope)
                self.assertIn("Refreshed", summary)
                db = self.cache.get_db()
                after_count = db.execute(
                    f"SELECT COUNT(*) AS cnt FROM {table} WHERE is_deleted = 0"
                ).fetchone()["cnt"]
                db.close()
                self.assertEqual(after_count, 2)

    def test_artifact_refresh_single_flight_is_nonblocking_for_all_scopes(self):
        import services.artifact_index as artifact_index

        cases = (
            ("releases", artifact_index.refresh_release_index),
            ("updates", artifact_index.refresh_update_index),
            ("tools", artifact_index.refresh_tool_index),
        )
        for scope, refresh in cases:
            with self.subTest(scope=scope):
                lock = artifact_index._REFRESH_LOCKS[scope]
                self.assertTrue(lock.acquire(blocking=False))
                try:
                    result = refresh(self.cache, self.storage)
                finally:
                    lock.release()
                self.assertTrue(result["skipped"])
                self.assertEqual(result["duration_ms"], 0)

    # -------------------------------------------------------------------
    # Update index tests
    # -------------------------------------------------------------------

    def test_refresh_update_index_populates_table(self):
        self._create_update("1.0.0.1")
        self._create_update("1.0.0.3")

        from services.artifact_index import refresh_update_index
        result = refresh_update_index(self.cache, self.storage)

        self.assertEqual(result["indexed_count"], 2)
        self.assertEqual(len(result["errors"]), 0)

    def test_get_updates_from_index_returns_none_when_empty(self):
        from services.artifact_index import get_updates_from_index
        result = get_updates_from_index(self.cache)
        self.assertIsNone(result)

    def test_get_updates_from_index_returns_data_after_refresh(self):
        self._create_update("2.0.0.1")

        from services.artifact_index import refresh_update_index, get_updates_from_index
        refresh_update_index(self.cache, self.storage)

        result = get_updates_from_index(self.cache)
        self.assertIsNotNone(result)
        self.assertEqual(len(result), 1)
        self.assertEqual(result[0]["version"], "2.0.0.1")

    def test_get_updates_from_index_sorts_versions_numerically(self):
        self._create_update("1.4.9.1")
        self._create_update("1.4.10.7")
        self._create_update("1.4.10.1")

        from services.artifact_index import refresh_update_index, get_updates_from_index
        refresh_update_index(self.cache, self.storage)

        result = get_updates_from_index(self.cache)

        self.assertIsNotNone(result)
        self.assertEqual([item["version"] for item in result[:3]], ["1.4.10.7", "1.4.10.1", "1.4.9.1"])

    def test_update_index_state_updated(self):
        self._create_update("1.0.0.1")
        from services.artifact_index import refresh_update_index, get_index_state
        refresh_update_index(self.cache, self.storage)

        state = get_index_state(self.cache, "updates")
        self.assertIsNotNone(state)
        self.assertEqual(state["status"], "ready")

    # -------------------------------------------------------------------
    # Tool index tests
    # -------------------------------------------------------------------

    def test_refresh_tool_index_populates_table(self):
        self._create_tool("Installer.exe")
        self._create_tool("SubDir", is_dir=True)

        from services.artifact_index import refresh_tool_index
        result = refresh_tool_index(self.cache, self.storage)

        self.assertEqual(result["indexed_count"], 2)
        self.assertEqual(len(result["errors"]), 0)

    def test_get_tools_from_index_returns_none_when_empty(self):
        from services.artifact_index import get_tools_from_index
        result = get_tools_from_index(self.cache)
        self.assertIsNone(result)

    def test_get_tools_from_index_returns_data_after_refresh(self):
        self._create_tool("SomeTool.zip")

        from services.artifact_index import refresh_tool_index, get_tools_from_index
        refresh_tool_index(self.cache, self.storage)

        result = get_tools_from_index(self.cache)
        self.assertIsNotNone(result)
        self.assertEqual(len(result), 1)
        self.assertEqual(result[0]["name"], "SomeTool.zip")
        self.assertFalse(result[0]["is_dir"])

    def test_tool_index_handles_directories(self):
        self._create_tool("MyTool", is_dir=True)

        from services.artifact_index import refresh_tool_index, get_tools_from_index
        refresh_tool_index(self.cache, self.storage)

        result = get_tools_from_index(self.cache)
        self.assertIsNotNone(result)
        dir_item = next(i for i in result if i["name"] == "MyTool")
        self.assertTrue(dir_item["is_dir"])
        self.assertGreater(dir_item["file_count"], 0)

    def test_tool_index_state_updated(self):
        self._create_tool("Tool.exe")
        from services.artifact_index import refresh_tool_index, get_index_state
        refresh_tool_index(self.cache, self.storage)

        state = get_index_state(self.cache, "tools")
        self.assertIsNotNone(state)
        self.assertEqual(state["status"], "ready")

    # -------------------------------------------------------------------
    # Combined refresh
    # -------------------------------------------------------------------

    def test_refresh_all_indexes(self):
        self._create_release("1.0.0.1")
        self._create_update("1.0.0.1")
        self._create_tool("Tool.exe")

        from services.artifact_index import refresh_all_indexes
        result = refresh_all_indexes(self.cache, self.storage)

        self.assertIn("releases", result["results"])
        self.assertIn("updates", result["results"])
        self.assertIn("tools", result["results"])
        self.assertEqual(result["total_errors"], 0)

    def test_get_all_index_states_summary(self):
        self._create_release("1.0.0.1")
        self._create_update("1.0.0.1")
        self._create_tool("Tool.exe")

        from services.artifact_index import refresh_all_indexes, get_all_index_states_summary
        refresh_all_indexes(self.cache, self.storage)

        summary = get_all_index_states_summary(self.cache)
        self.assertIn("states", summary)
        self.assertIn("counts", summary)
        self.assertEqual(summary["counts"]["releases"], 1)
        self.assertEqual(summary["counts"]["updates"], 1)
        self.assertEqual(summary["counts"]["tools"], 1)

    # -------------------------------------------------------------------
    # Scheduler no-op tests
    # -------------------------------------------------------------------

    def test_release_index_check_no_change_skips_refresh(self):
        self._create_release("1.0.0.1")

        from services.artifact_index import refresh_release_index
        from services.scheduler import _run_artifact_index_check
        refresh_release_index(self.cache, self.storage)

        result = _run_artifact_index_check(self.cache, self.storage, "releases")
        self.assertIn("No changes detected", result)

    def test_update_index_check_no_change_skips_refresh(self):
        self._create_update("1.0.0.1")

        from services.artifact_index import refresh_update_index
        from services.scheduler import _run_artifact_index_check
        refresh_update_index(self.cache, self.storage)

        result = _run_artifact_index_check(self.cache, self.storage, "updates")
        self.assertIn("No changes detected", result)

    def test_tool_index_check_no_change_skips_refresh(self):
        self._create_tool("Tool.exe")

        from services.artifact_index import refresh_tool_index
        from services.scheduler import _run_artifact_index_check
        refresh_tool_index(self.cache, self.storage)

        result = _run_artifact_index_check(self.cache, self.storage, "tools")
        self.assertIn("No changes detected", result)

    def test_release_index_check_detects_changes(self):
        self._create_release("1.0.0.1")

        from services.artifact_index import refresh_release_index
        from services.scheduler import _run_artifact_index_check
        refresh_release_index(self.cache, self.storage)

        # Add a new release
        self._create_release("2.0.0.1")

        result = _run_artifact_index_check(self.cache, self.storage, "releases")
        self.assertIn("Refreshed", result)

    # -------------------------------------------------------------------
    # Pages read from index (not disk)
    # -------------------------------------------------------------------

    def test_updates_site_api_reads_from_index(self):
        self._create_update("1.0.0.1")

        from services.artifact_index import refresh_update_index
        refresh_update_index(self.cache, self.storage)

        # Patch disk scanning to prove page reads from index
        from unittest.mock import patch
        with patch("update_retention.scan_update_packages", side_effect=AssertionError("should not scan disk")):
            response = self.client.get("/api/site/updates")

        self.assertEqual(response.status_code, 200)
        versions = {item["version"] for item in response.get_json()["update_packages"]}
        self.assertIn("1.0.0.1", versions)

    def test_updates_site_api_preserves_numeric_index_order(self):
        self._create_update("1.4.9.1")
        self._create_update("1.4.10.7")

        from services.artifact_index import refresh_update_index
        refresh_update_index(self.cache, self.storage)

        response = self.client.get("/api/site/updates")

        self.assertEqual(response.status_code, 200)
        versions = [item["version"] for item in response.get_json()["update_packages"]]
        self.assertEqual(versions[:2], ["1.4.10.7", "1.4.9.1"])

    def test_tools_site_api_reads_from_index(self):
        self._create_tool("MyTool.exe")

        from services.artifact_index import refresh_tool_index
        refresh_tool_index(self.cache, self.storage)

        from unittest.mock import patch
        with patch("storage_browser.build_storage_page_context", side_effect=AssertionError("should not scan disk")):
            response = self.client.get("/api/site/tools")

        self.assertEqual(response.status_code, 200)
        self.assertTrue(any(item["name"] == "MyTool.exe" for item in response.get_json()["items"]))

    # -------------------------------------------------------------------
    # Admin refresh API tests
    # -------------------------------------------------------------------

    def test_admin_refresh_releases(self):
        self._create_release("1.0.0.1")

        response = self.client.post(
            "/api/admin/index/releases/refresh",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertIn("indexed_count", payload)
        self.assertEqual(payload["indexed_count"], 1)

    def test_admin_refresh_updates(self):
        self._create_update("1.0.0.1")

        response = self.client.post(
            "/api/admin/index/updates/refresh",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(payload["indexed_count"], 1)

    def test_admin_refresh_tools(self):
        self._create_tool("Tool.exe")

        response = self.client.post(
            "/api/admin/index/tools/refresh",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(payload["indexed_count"], 1)

    def test_admin_refresh_all_indexes(self):
        self._create_release("1.0.0.1")
        self._create_update("1.0.0.1")
        self._create_tool("Tool.exe")

        response = self.client.post(
            "/api/admin/index/refresh-all",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertIn("releases", payload)
        self.assertIn("updates", payload)
        self.assertIn("tools", payload)

    def test_admin_index_status(self):
        self._create_release("1.0.0.1")
        from services.artifact_index import refresh_release_index
        refresh_release_index(self.cache, self.storage)

        response = self.client.get(
            "/api/admin/index/status",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertIn("states", payload)
        self.assertIn("counts", payload)

    def test_admin_refresh_requires_auth(self):
        response = self.client.post("/api/admin/index/releases/refresh")
        self.assertEqual(response.status_code, 401)

    def test_admin_refresh_updates_requires_auth(self):
        response = self.client.post("/api/admin/index/updates/refresh")
        self.assertEqual(response.status_code, 401)

    def test_admin_refresh_tools_requires_auth(self):
        response = self.client.post("/api/admin/index/tools/refresh")
        self.assertEqual(response.status_code, 401)


class SecurityTests(unittest.TestCase):
    """Tests for 401 vs 403, scope whitelist, and audit actor recording."""

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.storage = self.root / "storage"
        (self.storage / "Plugins").mkdir(parents=True, exist_ok=True)

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)
        self.original_testing = marketplace_app.app.config.get("TESTING", False)
        self.original_secret_key = marketplace_app.app.secret_key

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {"username": "tester", "password": "secret"}
        marketplace_app.CONFIG["secret_key"] = "test-secret-key"
        marketplace_app.CONFIG["debug"] = False
        marketplace_app.app.secret_key = marketplace_app.CONFIG["secret_key"]
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.app.config["MAX_CONTENT_LENGTH"] = marketplace_app.MAX_UPLOAD_SIZE_BYTES
        marketplace_app.init_db()

        self.cache = CacheManager(self.root / "marketplace.db")
        self.cache.init_db()

        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.secret_key = self.original_secret_key
        marketplace_app.app.config["TESTING"] = self.original_testing
        self.temp_dir.cleanup()

    def _auth_headers(self, username="tester", password="secret"):
        token = base64.b64encode(f"{username}:{password}".encode("utf-8")).decode("ascii")
        return {"Authorization": f"Basic {token}"}

    def _create_admin_key(self, scopes="admin:*"):
        resp = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Test Key", "scopes": scopes},
        )
        return resp.get_json()

    # -------------------------------------------------------------------
    # 401 vs 403 tests
    # -------------------------------------------------------------------

    def test_no_auth_returns_401(self):
        response = self.client.get("/api/admin/cache/status")
        self.assertEqual(response.status_code, 401)
        payload = response.get_json()
        self.assertEqual(payload["error"], "Authentication required")

    def test_invalid_bearer_token_returns_401(self):
        response = self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": "Bearer cvmp_invalid_invalid"},
        )
        self.assertEqual(response.status_code, 401)

    def test_insufficient_scope_returns_403(self):
        key_data = self._create_admin_key("stats:read")
        response = self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": f"Bearer {key_data['key']}"},
        )
        self.assertEqual(response.status_code, 403)
        payload = response.get_json()
        self.assertIn("Insufficient scope", payload["error"])

    def test_sufficient_scope_returns_200(self):
        key_data = self._create_admin_key("cache:read")
        response = self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": f"Bearer {key_data['key']}"},
        )
        self.assertEqual(response.status_code, 200)

    def test_admin_star_grants_all_access(self):
        key_data = self._create_admin_key("admin:*")
        headers = {"Authorization": f"Bearer {key_data['key']}"}

        # Should work for cache:read
        response = self.client.get("/api/admin/cache/status", headers=headers)
        self.assertEqual(response.status_code, 200)

        # Should work for cache:refresh
        response = self.client.post("/api/admin/cache/cleanup", headers=headers)
        self.assertEqual(response.status_code, 200)

        # Should work for jobs:read
        response = self.client.get("/api/admin/jobs", headers=headers)
        self.assertEqual(response.status_code, 200)

    def test_cache_read_cannot_write(self):
        key_data = self._create_admin_key("cache:read")
        headers = {"Authorization": f"Bearer {key_data['key']}"}

        # Can read
        response = self.client.get("/api/admin/cache/status", headers=headers)
        self.assertEqual(response.status_code, 200)

        # Cannot write
        response = self.client.post("/api/admin/cache/cleanup", headers=headers)
        self.assertEqual(response.status_code, 403)

    def test_jobs_read_cannot_write(self):
        from services.scheduler import ensure_default_jobs
        ensure_default_jobs(self.cache)

        key_data = self._create_admin_key("jobs:read")
        headers = {"Authorization": f"Bearer {key_data['key']}"}

        # Can read
        response = self.client.get("/api/admin/jobs", headers=headers)
        self.assertEqual(response.status_code, 200)

        # Cannot run
        response = self.client.post("/api/admin/jobs/cache_cleanup/run", headers=headers)
        self.assertEqual(response.status_code, 403)

    # -------------------------------------------------------------------
    # API Key scope whitelist
    # -------------------------------------------------------------------

    def test_invalid_scope_rejected_at_creation(self):
        response = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Bad Key", "scopes": "invalid:scope"},
        )
        self.assertEqual(response.status_code, 400)
        payload = response.get_json()
        self.assertIn("Invalid scopes", payload["error"])
        self.assertIn("allowed_scopes", payload)

    def test_valid_scopes_accepted(self):
        for scope in ["admin:*", "cache:read", "cache:refresh", "jobs:read", "jobs:write", "stats:read", "plugin:publish"]:
            response = self.client.post(
                "/api/admin/api-keys",
                headers=self._auth_headers(),
                json={"name": f"Key {scope}", "scopes": scope},
            )
            self.assertEqual(response.status_code, 201, f"Scope '{scope}' should be accepted")

    def test_multiple_scopes_validated(self):
        response = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Multi Key", "scopes": "cache:read,jobs:read"},
        )
        self.assertEqual(response.status_code, 201)

    def test_multiple_scopes_with_one_invalid_rejected(self):
        response = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Mixed Key", "scopes": "cache:read,invalid:scope"},
        )
        self.assertEqual(response.status_code, 400)

    # -------------------------------------------------------------------
    # Audit actor recording
    # -------------------------------------------------------------------

    def test_bearer_key_records_prefix_in_audit(self):
        key_data = self._create_admin_key("cache:refresh")
        key_prefix = key_data["key_prefix"]

        # Perform an action with Bearer key
        self.client.post(
            "/api/admin/cache/cleanup",
            headers={"Authorization": f"Bearer {key_data['key']}"},
        )

        # Check audit log
        entries = self.cache.get_audit_log(action="cache_cleanup")
        self.assertGreaterEqual(len(entries), 1)
        entry = entries[0]
        self.assertEqual(entry["actor_type"], "api_key")
        self.assertIn(key_prefix, entry["actor_id"])

    def test_basic_auth_records_username_in_audit(self):
        self.client.post(
            "/api/admin/cache/cleanup",
            headers=self._auth_headers(),
        )

        entries = self.cache.get_audit_log(action="cache_cleanup")
        self.assertGreaterEqual(len(entries), 1)
        entry = entries[0]
        self.assertEqual(entry["actor_type"], "user")
        self.assertEqual(entry["actor_id"], "tester")

    def test_insufficient_scope_records_audit(self):
        key_data = self._create_admin_key("stats:read")

        self.client.get(
            "/api/admin/cache/status",
            headers={"Authorization": f"Bearer {key_data['key']}"},
        )

        entries = self.cache.get_audit_log(action="auth_forbidden")
        self.assertGreaterEqual(len(entries), 1)
        self.assertEqual(entries[0]["actor_type"], "api_key")

    def test_no_auth_records_audit(self):
        self.client.get("/api/admin/cache/status")

        entries = self.cache.get_audit_log(action="auth_unauthorized")
        self.assertGreaterEqual(len(entries), 1)
        self.assertEqual(entries[0]["actor_type"], "anonymous")

    # -------------------------------------------------------------------
    # API Key description and expiry
    # -------------------------------------------------------------------

    def test_api_key_creation_with_description(self):
        response = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "My Key", "description": "For CI/CD", "scopes": "cache:read"},
        )
        self.assertEqual(response.status_code, 201)
        payload = response.get_json()
        self.assertEqual(payload.get("description"), "For CI/CD")

    def test_api_key_creation_default_expiry(self):
        response = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Expiry Key"},
        )
        self.assertEqual(response.status_code, 201)
        payload = response.get_json()
        self.assertIsNotNone(payload.get("expires_at"))

    # -------------------------------------------------------------------
    # DB backup
    # -------------------------------------------------------------------

    def test_db_backup_creates_file(self):
        response = self.client.post(
            "/api/admin/backup/db",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(payload["status"], "ok")
        self.assertIn("backup_path", payload)
        self.assertGreater(payload["backup_size_bytes"], 0)

        # Verify backup file exists
        backup_path = Path(payload["backup_path"])
        self.assertTrue(backup_path.exists())

    # -------------------------------------------------------------------
    # Basic Auth validation for admin endpoints
    # -------------------------------------------------------------------

    def test_bad_basic_auth_returns_401(self):
        """Bad Basic Auth credentials must be rejected."""
        bad_headers = {
            "Authorization": "Basic " + base64.b64encode(b"bad:bad").decode("ascii"),
        }
        response = self.client.get("/api/admin/cache/status", headers=bad_headers)
        self.assertEqual(response.status_code, 401)

    def test_correct_basic_auth_returns_200(self):
        """Correct Basic Auth credentials must be accepted."""
        response = self.client.get(
            "/api/admin/cache/status",
            headers=self._auth_headers(),
        )
        self.assertEqual(response.status_code, 200)

    def test_basic_auth_wrong_password_returns_401(self):
        """Correct username but wrong password must be rejected."""
        bad_headers = {
            "Authorization": "Basic " + base64.b64encode(b"tester:wrong").decode("ascii"),
        }
        response = self.client.get("/api/admin/cache/status", headers=bad_headers)
        self.assertEqual(response.status_code, 401)

    def test_basic_auth_wrong_username_returns_401(self):
        """Wrong username must be rejected."""
        bad_headers = {
            "Authorization": "Basic " + base64.b64encode(b"wrong:secret").decode("ascii"),
        }
        response = self.client.get("/api/admin/cache/status", headers=bad_headers)
        self.assertEqual(response.status_code, 401)

    # -------------------------------------------------------------------
    # Scope whitelist includes plugin:read and release:publish
    # -------------------------------------------------------------------

    def test_plugin_read_scope_accepted(self):
        response = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Read Key", "scopes": "plugin:read"},
        )
        self.assertEqual(response.status_code, 201)

    def test_release_publish_scope_accepted(self):
        response = self.client.post(
            "/api/admin/api-keys",
            headers=self._auth_headers(),
            json={"name": "Release Key", "scopes": "release:publish"},
        )
        self.assertEqual(response.status_code, 201)


class HomepageIndexTests(unittest.TestCase):
    """Tests for home page index integration."""

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.storage = self.root / "storage"
        (self.storage / "Plugins").mkdir(parents=True, exist_ok=True)

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)
        self.original_testing = marketplace_app.app.config.get("TESTING", False)
        self.original_secret_key = marketplace_app.app.secret_key

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {"username": "tester", "password": "secret"}
        marketplace_app.CONFIG["secret_key"] = "test-secret-key"
        marketplace_app.CONFIG["debug"] = False
        marketplace_app.app.secret_key = marketplace_app.CONFIG["secret_key"]
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.app.config["MAX_CONTENT_LENGTH"] = marketplace_app.MAX_UPLOAD_SIZE_BYTES
        marketplace_app.init_db()

        self.cache = CacheManager(self.root / "marketplace.db")
        self.cache.init_db()

        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.secret_key = self.original_secret_key
        marketplace_app.app.config["TESTING"] = self.original_testing
        self.temp_dir.cleanup()

    def test_home_site_api_shows_update_packages_from_index(self):
        update_dir = self.storage / "Update"
        update_dir.mkdir(parents=True, exist_ok=True)
        (update_dir / "ColorVision-Update-[1.0.0.1].cvx").write_bytes(b"update")

        from services.artifact_index import refresh_update_index
        refresh_update_index(self.cache, self.storage)

        response = self.client.get("/api/site/home")
        self.assertEqual(response.status_code, 200)
        versions = {item["version"] for item in response.get_json()["update_packages"]}
        self.assertIn("1.0.0.1", versions)

    def test_home_site_api_falls_back_to_disk_when_index_empty(self):
        update_dir = self.storage / "Update"
        update_dir.mkdir(parents=True, exist_ok=True)
        (update_dir / "ColorVision-Update-[2.0.0.1].cvx").write_bytes(b"update")

        # Index is empty, should fallback to disk scan
        response = self.client.get("/api/site/home")
        self.assertEqual(response.status_code, 200)
        versions = {item["version"] for item in response.get_json()["update_packages"]}
        self.assertIn("2.0.0.1", versions)


class UploadIndexRefreshTests(unittest.TestCase):
    """Tests that legacy PUT uploads trigger artifact index refresh."""

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.storage = self.root / "storage"
        (self.storage / "Plugins").mkdir(parents=True, exist_ok=True)

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)
        self.original_testing = marketplace_app.app.config.get("TESTING", False)
        self.original_secret_key = marketplace_app.app.secret_key

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {"username": "tester", "password": "secret"}
        marketplace_app.CONFIG["secret_key"] = "test-secret-key"
        marketplace_app.CONFIG["debug"] = False
        marketplace_app.app.secret_key = marketplace_app.CONFIG["secret_key"]
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.app.config["MAX_CONTENT_LENGTH"] = marketplace_app.MAX_UPLOAD_SIZE_BYTES
        marketplace_app.init_db()

        self.cache = CacheManager(self.root / "marketplace.db")
        self.cache.init_db()

        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.secret_key = self.original_secret_key
        marketplace_app.app.config["TESTING"] = self.original_testing
        self.temp_dir.cleanup()

    def _auth_headers(self, username="tester", password="secret"):
        token = base64.b64encode(f"{username}:{password}".encode("utf-8")).decode("ascii")
        return {"Authorization": f"Basic {token}"}

    def test_put_release_file_refreshes_release_index(self):
        response = self.client.put(
            "/upload/ColorVision/ColorVision-1.2.0.1.exe",
            data=b"installer-payload",
            headers=self._auth_headers(),
            content_type="application/octet-stream",
        )
        self.assertEqual(response.status_code, 201)

        from services.artifact_index import get_releases_from_index
        releases = get_releases_from_index(self.cache)
        self.assertIsNotNone(releases)
        self.assertTrue(any(r["version"] == "1.2.0.1" for r in releases))

    def test_put_update_file_refreshes_update_index(self):
        response = self.client.put(
            "/upload/ColorVision/Update/ColorVision-Update-[1.0.0.1].cvx",
            data=b"update-payload",
            headers=self._auth_headers(),
            content_type="application/octet-stream",
        )
        self.assertEqual(response.status_code, 201)

        from services.artifact_index import get_updates_from_index
        updates = get_updates_from_index(self.cache)
        self.assertIsNotNone(updates)
        self.assertTrue(any(u["version"] == "1.0.0.1" for u in updates))

    def test_put_tool_file_refreshes_tool_index(self):
        response = self.client.put(
            "/upload/ColorVision/Tool/SomeTool.zip",
            data=b"tool-payload",
            headers=self._auth_headers(),
            content_type="application/octet-stream",
        )
        self.assertEqual(response.status_code, 201)

        from services.artifact_index import get_tools_from_index
        tools = get_tools_from_index(self.cache)
        self.assertIsNotNone(tools)
        self.assertTrue(any(t["name"] == "SomeTool.zip" for t in tools))

    def test_put_plugin_package_does_not_crash(self):
        """Plugin uploads should still work (plugin_index handled separately)."""
        response = self.client.put(
            "/upload/ColorVision/Plugins/TestPlugin/TestPlugin-1.0.0.cvxp",
            data=b"plugin-payload",
            headers=self._auth_headers(),
            content_type="application/octet-stream",
        )
        self.assertEqual(response.status_code, 201)

    def test_put_release_zip_refreshes_release_index(self):
        response = self.client.put(
            "/upload/ColorVision/ColorVision-1.0.0.1.zip",
            data=b"zip-payload",
            headers=self._auth_headers(),
            content_type="application/octet-stream",
        )
        self.assertEqual(response.status_code, 201)

        from services.artifact_index import get_releases_from_index
        releases = get_releases_from_index(self.cache)
        self.assertIsNotNone(releases)
        self.assertTrue(any(r["version"] == "1.0.0.1" for r in releases))

    def test_put_android_apk_refreshes_release_index(self):
        response = self.client.put(
            "/upload/ColorVision/ColorVision-Android-1.0.apk",
            data=b"apk-payload",
            headers=self._auth_headers(),
            content_type="application/octet-stream",
        )
        self.assertEqual(response.status_code, 201)

        from services.artifact_index import get_releases_from_index
        releases = get_releases_from_index(self.cache)
        self.assertIsNotNone(releases)
        self.assertTrue(any(r["filename"] == "ColorVision-Android-1.0.apk" for r in releases))


class BrowsePaginationTests(unittest.TestCase):
    """Tests for /api/site/browse pagination behavior."""

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.storage = self.root / "storage"
        self.storage.mkdir(parents=True, exist_ok=True)

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)
        self.original_testing = marketplace_app.app.config.get("TESTING", False)
        self.original_secret_key = marketplace_app.app.secret_key

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {"username": "tester", "password": "secret"}
        marketplace_app.CONFIG["secret_key"] = "test-secret-key"
        marketplace_app.CONFIG["debug"] = False
        marketplace_app.app.secret_key = marketplace_app.CONFIG["secret_key"]
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.app.config["MAX_CONTENT_LENGTH"] = marketplace_app.MAX_UPLOAD_SIZE_BYTES
        marketplace_app.init_db()

        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.secret_key = self.original_secret_key
        marketplace_app.app.config["TESTING"] = self.original_testing
        self.temp_dir.cleanup()

    def test_browse_default_limit(self):
        """Default limit should cap results for large directories."""
        for i in range(300):
            (self.storage / f"file_{i:04d}.txt").write_text(str(i))

        response = self.client.get("/api/site/browse")
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        self.assertEqual(len(payload["items"]), 200)
        self.assertTrue(any(item["name"] == "file_0000.txt" for item in payload["items"]))

    def test_browse_custom_limit(self):
        """Custom limit parameter should be respected."""
        for i in range(50):
            (self.storage / f"file_{i:04d}.txt").write_text(str(i))

        response = self.client.get("/api/site/browse?limit=10")
        self.assertEqual(response.status_code, 200)
        payload = response.get_json()
        names = [item["name"] for item in payload["items"]]
        self.assertEqual(len(names), 10)
        self.assertIn("file_0000.txt", names)
        self.assertIn("file_0009.txt", names)

    def test_browse_offset(self):
        """Offset parameter should skip items."""
        for i in range(20):
            (self.storage / f"file_{i:04d}.txt").write_text(str(i))

        response = self.client.get("/api/site/browse?limit=5&offset=10")
        self.assertEqual(response.status_code, 200)
        names = [item["name"] for item in response.get_json()["items"]]
        self.assertEqual(names[0], "file_0010.txt")


class SchemaVersionTests(unittest.TestCase):
    """Tests for schema migration mechanism."""

    def test_schema_version_table_created(self):
        from db.schema_version import ensure_schema_version, CURRENT_SCHEMA_VERSION
        import sqlite3

        temp_dir = tempfile.TemporaryDirectory()
        db_path = Path(temp_dir.name) / "test.db"
        db = sqlite3.connect(str(db_path))
        db.row_factory = sqlite3.Row

        version = ensure_schema_version(db)
        self.assertEqual(version, CURRENT_SCHEMA_VERSION)

        # Verify table exists
        row = db.execute("SELECT value FROM schema_version WHERE key = 'version'").fetchone()
        self.assertIsNotNone(row)
        self.assertEqual(row["value"], CURRENT_SCHEMA_VERSION)

        db.close()
        temp_dir.cleanup()

    def test_schema_migration_idempotent(self):
        """Running migration twice should not fail."""
        from db.schema_version import ensure_schema_version
        import sqlite3

        temp_dir = tempfile.TemporaryDirectory()
        db_path = Path(temp_dir.name) / "test.db"
        db = sqlite3.connect(str(db_path))
        db.row_factory = sqlite3.Row

        # Create the base tables first
        db.execute("CREATE TABLE IF NOT EXISTS job_runs (id INTEGER PRIMARY KEY)")
        db.commit()

        # Run migration twice
        ensure_schema_version(db)
        ensure_schema_version(db)

        # Verify columns exist
        db.execute("SELECT scanned_count FROM job_runs")
        db.execute("SELECT changed_count FROM job_runs")

        db.close()
        temp_dir.cleanup()

    def test_schema_migration_v3_adds_plugin_detail_columns(self):
        """Existing v2 databases should gain plugin detail columns used by plugin_index."""
        from db.schema_version import ensure_schema_version, CURRENT_SCHEMA_VERSION
        import sqlite3

        temp_dir = tempfile.TemporaryDirectory()
        db_path = Path(temp_dir.name) / "test.db"
        db = sqlite3.connect(str(db_path))
        db.row_factory = sqlite3.Row

        db.execute(
            """
            CREATE TABLE plugin_index (
                plugin_id TEXT PRIMARY KEY,
                name TEXT,
                latest_version TEXT,
                is_deleted INTEGER DEFAULT 0
            )
            """
        )
        db.execute(
            """
            CREATE TABLE schema_version (
                key TEXT PRIMARY KEY,
                value INTEGER NOT NULL
            )
            """
        )
        db.execute("INSERT INTO schema_version (key, value) VALUES ('version', 2)")
        db.commit()

        version = ensure_schema_version(db)
        self.assertEqual(version, CURRENT_SCHEMA_VERSION)

        columns = {
            row["name"]
            for row in db.execute("PRAGMA table_info(plugin_index)").fetchall()
        }
        self.assertIn("readme", columns)
        self.assertIn("changelog", columns)
        self.assertIn("source_manifest_path", columns)
        self.assertIn("source_archive_path", columns)

        row = db.execute("SELECT value FROM schema_version WHERE key = 'version'").fetchone()
        self.assertEqual(row["value"], CURRENT_SCHEMA_VERSION)

        db.close()
        temp_dir.cleanup()

    def test_schema_migration_reraises_non_duplicate_errors(self):
        """Non-duplicate-column, non-missing-table errors should propagate."""
        from db.schema_version import _add_column_if_missing
        import sqlite3

        temp_dir = tempfile.TemporaryDirectory()
        db_path = Path(temp_dir.name) / "test.db"
        db = sqlite3.connect(str(db_path))

        # Create a table first, then try to add a column with invalid syntax
        db.execute("CREATE TABLE test_table (id INTEGER)")
        db.commit()

        # This should raise an OperationalError (not about duplicate column or missing table)
        with self.assertRaises(sqlite3.OperationalError):
            _add_column_if_missing(db, "test_table", "INVALID SYNTAX!!!")

        db.close()
        temp_dir.cleanup()


class AuthIntegrationTests(unittest.TestCase):
    """Tests for auth middleware integration."""

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.storage = self.root / "storage"
        (self.storage / "Plugins").mkdir(parents=True, exist_ok=True)

        self.original_storage = marketplace_app.STORAGE
        self.original_db_path = marketplace_app.DB_PATH
        self.original_config = copy.deepcopy(marketplace_app.CONFIG)
        self.original_testing = marketplace_app.app.config.get("TESTING", False)
        self.original_secret_key = marketplace_app.app.secret_key

        marketplace_app.STORAGE = self.storage
        marketplace_app.DB_PATH = self.root / "marketplace.db"
        marketplace_app.CONFIG = copy.deepcopy(marketplace_app.CONFIG)
        marketplace_app.CONFIG["storage_path"] = str(self.storage)
        marketplace_app.CONFIG["upload_auth"] = {"username": "tester", "password": "secret"}
        marketplace_app.CONFIG["secret_key"] = "test-secret-key"
        marketplace_app.CONFIG["debug"] = False
        marketplace_app.app.secret_key = marketplace_app.CONFIG["secret_key"]
        marketplace_app.app.config["TESTING"] = True
        marketplace_app.app.config["MAX_CONTENT_LENGTH"] = marketplace_app.MAX_UPLOAD_SIZE_BYTES
        marketplace_app.init_db()

        self.client = marketplace_app.app.test_client()

    def tearDown(self):
        marketplace_app.STORAGE = self.original_storage
        marketplace_app.DB_PATH = self.original_db_path
        marketplace_app.CONFIG = self.original_config
        marketplace_app.app.secret_key = self.original_secret_key
        marketplace_app.app.config["TESTING"] = self.original_testing
        self.temp_dir.cleanup()

    def _auth_headers(self, username="tester", password="secret"):
        token = base64.b64encode(f"{username}:{password}".encode("utf-8")).decode("ascii")
        return {"Authorization": f"Basic {token}"}

    def test_login_page_renders(self):
        response = self.client.get("/login")
        self.assertEqual(response.status_code, 200)
        self.assertIn('<div id="root">', response.get_data(as_text=True))

    def test_login_success_redirects(self):
        response = self.client.post("/login", data={
            "username": "tester",
            "password": "secret",
        }, follow_redirects=False)
        self.assertIn(response.status_code, [302, 303])

    def test_login_failure_returns_json_error(self):
        response = self.client.post("/api/auth/login", json={
            "username": "tester",
            "password": "wrong",
        })
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.get_json()["error"], "用户名或密码错误")

    def test_login_failure_writes_audit(self):
        self.client.post("/login", data={
            "username": "hacker",
            "password": "wrong",
        })
        entries = marketplace_app._cache.get_audit_log(action="login_failed")
        self.assertGreaterEqual(len(entries), 1)
        self.assertEqual(entries[0]["actor_id"], "hacker")

    def test_logout_clears_session(self):
        # Login first
        self.client.post("/login", data={
            "username": "tester",
            "password": "secret",
        })
        # Logout
        response = self.client.get("/logout", follow_redirects=False)
        self.assertIn(response.status_code, [302, 303])

    def test_admin_publish_requires_login(self):
        response = self.client.get("/admin/publish", follow_redirects=False)
        self.assertIn(response.status_code, [302, 303])
        self.assertIn("login", response.headers.get("Location", ""))


if __name__ == "__main__":
    unittest.main()

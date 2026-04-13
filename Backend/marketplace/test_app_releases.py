import os
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import app_releases


class AppReleasesTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.storage = Path(self.temp_dir.name) / "storage"
        self.storage.mkdir(parents=True, exist_ok=True)
        self.cache: dict[str, dict] = {}

    def tearDown(self):
        self.temp_dir.cleanup()

    def _get_cache_entry(self, key: str, **_kwargs):
        return self.cache.get(key)

    def _set_cache_entry(self, key: str, value, **_kwargs):
        self.cache[key] = {"value": value}

    def _create_release(
        self,
        version: str,
        *,
        in_history: bool = False,
        size: int = 3,
        suffix: str = ".zip",
        mtime: float | None = None,
    ) -> Path:
        if in_history:
            target_dir = app_releases.app_release_history_dir(self.storage, version)
            target_dir.mkdir(parents=True, exist_ok=True)
        else:
            target_dir = self.storage
        path = target_dir / f"ColorVision-{version}{suffix}"
        path.write_bytes(b"x" * size)
        if mtime is not None:
            os.utime(path, (mtime, mtime))
        return path

    def test_scan_app_release_artifacts_collects_current_and_history(self):
        self._create_release("1.0.0.1")
        self._create_release("0.9.0.1", in_history=True)

        releases = app_releases.scan_app_release_artifacts(
            self.storage,
            get_cache_entry=self._get_cache_entry,
            set_cache_entry=self._set_cache_entry,
            cache_key="releases",
            ttl_seconds=300,
        )

        self.assertEqual(len(releases), 2)
        self.assertEqual(releases[0]["version"], "1.0.0.1")
        self.assertEqual(releases[0]["source"], "current")
        self.assertEqual(releases[1]["source"], "archive")
        self.assertEqual(releases[1]["relative_path"], "History/0.9/0.9.0/ColorVision-0.9.0.1.zip")
        self.assertEqual(releases[0]["kind_label"], "ZIP 归档")
        self.assertEqual(releases[0]["era"], "archive")
        self.assertEqual(releases[0]["era_label"], "压缩归档时代")
        self.assertRegex(releases[0]["modified_display"], r"\d{4}-\d{2}-\d{2} \d{2}:\d{2}")

    def test_scan_app_release_artifacts_reuses_cache(self):
        cached_items = [{"version": "9.9.9.9", "source": "current"}]
        self.cache["releases"] = {"value": cached_items}
        self._create_release("1.0.0.1")

        releases = app_releases.scan_app_release_artifacts(
            self.storage,
            get_cache_entry=self._get_cache_entry,
            set_cache_entry=self._set_cache_entry,
            cache_key="releases",
            ttl_seconds=300,
        )

        self.assertIs(releases, cached_items)

    def test_build_app_release_context_summarizes_counts(self):
        releases = [
            {
                "version": "1.2.0.1",
                "source": "current",
                "branch": "1.2.0",
                "major_minor": "1.2",
                "kind_label": "安装包",
                "era": "installer",
                "era_label": "安装包时代",
                "modified_display": "2026-03-31 12:00",
            },
            {
                "version": "1.1.0.1",
                "source": "archive",
                "branch": "1.1.0",
                "major_minor": "1.1",
                "kind_label": "ZIP 归档",
                "era": "archive",
                "era_label": "压缩归档时代",
                "modified_display": "2026-03-20 10:00",
            },
            {
                "version": "1.1.0.0",
                "source": "archive",
                "branch": "1.1.0",
                "major_minor": "1.1",
                "kind_label": "安装包",
                "era": "installer",
                "era_label": "安装包时代",
                "modified_display": "2026-03-18 09:00",
            },
        ]

        context = app_releases.build_app_release_context(releases)

        self.assertEqual(context["latest_release"]["version"], "1.2.0.1")
        self.assertEqual(context["current_count"], 1)
        self.assertEqual(context["archive_count"], 2)
        self.assertEqual(context["release_branch_count"], 2)
        self.assertEqual(len(context["archive_preview"]), 2)
        self.assertEqual(context["archive_timeline_count"], 1)
        self.assertEqual(context["archive_timeline_groups"][0]["kind_summary"], "ZIP 归档 × 1 · 安装包 × 1")
        self.assertTrue(context["archive_timeline_groups"][0]["contains_archive_only_formats"])
        self.assertTrue(context["archive_timeline_groups"][0]["contains_installer_artifacts"])
        self.assertIn("压缩归档时代", context["archive_timeline_groups"][0]["era_summary"])

    def test_build_archive_timeline_groups_tracks_time_range_and_formats(self):
        releases = [
            {
                "version": "1.0.0.2",
                "source": "archive",
                "major_minor": "1.0",
                "branch": "1.0.0",
                "kind": "EXE",
                "kind_label": "安装包",
                "modified": "2026-03-30T12:00:00+00:00",
                "modified_display": "2026-03-30 12:00",
            },
            {
                "version": "1.0.0.1",
                "source": "archive",
                "major_minor": "1.0",
                "branch": "1.0.0",
                "kind": "ZIP",
                "kind_label": "ZIP 归档",
                "modified": "2026-03-01T08:00:00+00:00",
                "modified_display": "2026-03-01 08:00",
            },
        ]

        groups = app_releases.build_archive_timeline_groups(releases)

        self.assertEqual(len(groups), 1)
        self.assertEqual(groups[0]["time_range_display"], "2026-03-01 08:00 → 2026-03-30 12:00")
        self.assertTrue(groups[0]["contains_archive_only_formats"])
        self.assertTrue(groups[0]["contains_installer_artifacts"])
        self.assertIn("ZIP 归档", groups[0]["kind_summary"])

    def test_classify_artifact_era_distinguishes_archive_and_installer_formats(self):
        self.assertEqual(app_releases.classify_artifact_era("zip"), ("archive", "压缩归档时代"))
        self.assertEqual(app_releases.classify_artifact_era("exe"), ("installer", "安装包时代"))
        self.assertEqual(app_releases.classify_artifact_era("bin"), ("other", "其他记录"))

    def test_reconcile_app_release_history_moves_older_root_releases(self):
        self._create_release("1.0.0.1")
        old_release = self._create_release("0.9.0.1")
        changed = []

        moved = app_releases.reconcile_app_release_history(
            self.storage,
            keep_latest=1,
            on_changed=changed.append,
        )

        self.assertEqual(len(moved), 1)
        self.assertFalse(old_release.exists())
        self.assertEqual(moved[0]["to"], "History/0.9/0.9.0/ColorVision-0.9.0.1.zip")
        self.assertTrue((self.storage / moved[0]["to"]).exists())
        self.assertEqual(changed, ["History"])

    def test_reconcile_app_release_history_removes_duplicate_when_target_matches(self):
        self._create_release("1.0.0.1", size=7)
        duplicate = self._create_release("0.9.0.1", size=5)
        target = app_releases.app_release_history_dir(self.storage, "0.9.0.1")
        target.mkdir(parents=True, exist_ok=True)
        (target / duplicate.name).write_bytes(b"x" * 5)

        moved = app_releases.reconcile_app_release_history(self.storage, keep_latest=1)

        self.assertEqual(len(moved), 1)
        self.assertFalse(duplicate.exists())
        self.assertEqual(moved[0]["to"], "History/0.9/0.9.0/ColorVision-0.9.0.1.zip")

    def test_reconcile_app_release_history_skips_locked_file_in_windows(self):
        self._create_release("1.0.0.2")
        old_release = self._create_release("1.0.0.1")

        locked_error = PermissionError(13, "file in use")
        setattr(locked_error, "winerror", 32)

        with (
            mock.patch("app_releases.shutil.move", side_effect=[locked_error, locked_error, locked_error]) as move_mock,
            mock.patch("app_releases.time.sleep", return_value=None),
        ):
            moved = app_releases.reconcile_app_release_history(self.storage, keep_latest=1)

        self.assertEqual(moved, [])
        self.assertTrue(old_release.exists())
        self.assertEqual(move_mock.call_count, 3)


if __name__ == "__main__":
    unittest.main()



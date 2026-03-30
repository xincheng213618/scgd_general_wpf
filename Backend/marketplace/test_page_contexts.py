import tempfile
import unittest
from pathlib import Path

from page_contexts import build_index_page_context, build_releases_page_context


class PageContextsTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.storage = Path(self.temp_dir.name) / "storage"
        self.storage.mkdir(parents=True, exist_ok=True)

    def tearDown(self):
        self.temp_dir.cleanup()

    def _sample_app_info(self):
        return {
            "latest_version": "1.2.0.1",
            "current_count": 1,
            "archive_count": 3,
            "release_branch_count": 2,
            "archive_timeline_count": 2,
            "current_releases": [
                {
                    "display_title": "ColorVision 1.2.0.1",
                    "relative_path": "ColorVision-1.2.0.1.exe",
                    "modified": "2026-03-31T10:00:00+00:00",
                    "modified_display": "2026-03-31 10:00",
                    "kind": "EXE",
                    "kind_label": "安装包",
                    "era": "installer",
                    "era_label": "安装包时代",
                    "size": 10,
                }
            ],
            "archive_recent": [
                {
                    "display_title": "ColorVision 1.1.0.1",
                    "relative_path": "History/1.1/1.1.0/ColorVision-1.1.0.1.zip",
                    "modified": "2026-03-20T10:00:00+00:00",
                    "modified_display": "2026-03-20 10:00",
                    "kind": "ZIP",
                    "kind_label": "ZIP 归档",
                    "era": "archive",
                    "era_label": "压缩归档时代",
                    "size": 9,
                }
            ],
            "archive_timeline_groups": [
                {
                    "major_minor": "1.1",
                    "branch": "1.1.0",
                    "items": [
                        {
                            "display_title": "ColorVision 1.1.0.1",
                            "relative_path": "History/1.1/1.1.0/ColorVision-1.1.0.1.zip",
                            "modified": "2026-03-20T10:00:00+00:00",
                            "modified_display": "2026-03-20 10:00",
                            "kind": "ZIP",
                            "kind_label": "ZIP 归档",
                            "era": "archive",
                            "era_label": "压缩归档时代",
                            "size": 9,
                        },
                        {
                            "display_title": "ColorVision 1.1.0.0",
                            "relative_path": "History/1.1/1.1.0/ColorVision-1.1.0.0.rar",
                            "modified": "2026-03-18T10:00:00+00:00",
                            "modified_display": "2026-03-18 10:00",
                            "kind": "RAR",
                            "kind_label": "RAR 归档",
                            "era": "archive",
                            "era_label": "压缩归档时代",
                            "size": 8,
                        },
                    ],
                    "count": 2,
                    "latest_modified": "2026-03-20T10:00:00+00:00",
                    "latest_modified_display": "2026-03-20 10:00",
                    "earliest_modified": "2026-03-18T10:00:00+00:00",
                    "earliest_modified_display": "2026-03-18 10:00",
                    "time_range_display": "2026-03-18 10:00 → 2026-03-20 10:00",
                    "kind_summary": "RAR 归档 × 1 · ZIP 归档 × 1",
                    "contains_archive_only_formats": True,
                },
                {
                    "major_minor": "1.0",
                    "branch": "1.0.0",
                    "items": [
                        {
                            "display_title": "ColorVision 1.0.0.1",
                            "relative_path": "History/1.0/1.0.0/ColorVision-1.0.0.1.exe",
                            "modified": "2026-03-10T10:00:00+00:00",
                            "modified_display": "2026-03-10 10:00",
                            "kind": "EXE",
                            "kind_label": "安装包",
                            "era": "installer",
                            "era_label": "安装包时代",
                            "size": 7,
                        },
                    ],
                    "count": 1,
                    "latest_modified": "2026-03-10T10:00:00+00:00",
                    "latest_modified_display": "2026-03-10 10:00",
                    "earliest_modified": "2026-03-10T10:00:00+00:00",
                    "earliest_modified_display": "2026-03-10 10:00",
                    "time_range_display": "2026-03-10 10:00",
                    "kind_summary": "安装包 × 1",
                    "contains_archive_only_formats": False,
                },
            ],
            "archive_timeline_preview": [],
        }

    def test_build_releases_page_context_exposes_filter_options_and_filtered_groups(self):
        context = build_releases_page_context(
            self._sample_app_info(),
            major_minor="1.1",
            branch="1.1.0",
            kind="ZIP",
            era="archive",
        )

        self.assertEqual(context["release_filters"]["major_minor"], "1.1")
        self.assertEqual(context["release_filters"]["branch"], "1.1.0")
        self.assertEqual(context["release_filters"]["kind"], "ZIP")
        self.assertEqual(context["release_filters"]["era"], "archive")
        self.assertEqual(len(context["archive_visible_groups"]), 1)
        self.assertEqual(context["archive_visible_groups"][0]["visible_count"], 1)
        self.assertEqual(context["archive_visible_groups"][0]["visible_era_summary"], "压缩归档时代")
        self.assertTrue(context["archive_visible_groups"][0]["is_expanded"])
        self.assertEqual(context["archive_visible_item_count"], 1)
        self.assertTrue(any(option["value"] == "ZIP" for option in context["archive_kind_options"]))
        self.assertTrue(any(option["value"] == "archive" for option in context["archive_era_options"]))

    def test_build_releases_page_context_reports_empty_filtered_state(self):
        context = build_releases_page_context(self._sample_app_info(), kind="FILE")

        self.assertEqual(context["archive_visible_item_count"], 0)
        self.assertEqual(context["archive_visible_group_count"], 0)
        self.assertTrue(context["release_filters"]["has_filters"])

    def test_build_releases_page_context_defaults_to_first_group_expanded_without_filters(self):
        context = build_releases_page_context(self._sample_app_info())

        self.assertTrue(context["archive_visible_groups"][0]["is_expanded"])
        self.assertFalse(context["archive_visible_groups"][1]["is_expanded"])

    def test_build_index_page_context_includes_recent_change_dashboard(self):
        overview = [
            {"name": "History", "type": "dir", "file_count": 3, "modified": "2026-03-31T09:00:00+00:00"},
            {"name": "Plugins", "type": "dir", "file_count": 5, "modified": "2026-03-30T09:00:00+00:00"},
        ]

        context = build_index_page_context(
            storage=self.storage,
            get_app_info=self._sample_app_info,
            get_storage_overview_context=lambda: (
                overview,
                {"directory_count": 2, "total_file_count": 8, "top_level_size": 0},
                {"cache_hit": True, "updated_at_display": "2026-03-31 10:00", "ttl_seconds": 300},
            ),
        )

        self.assertTrue(context["recent_change_dashboard"])
        self.assertEqual(context["recent_change_dashboard"][0]["title"], "ColorVision 1.2.0.1")
        self.assertTrue(any(item["category"] == "目录" for item in context["recent_change_dashboard"]))
        self.assertGreaterEqual(context["recent_change_summary"]["change_count"], 3)


if __name__ == "__main__":
    unittest.main()



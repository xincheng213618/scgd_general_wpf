import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from page_contexts import (
    build_browse_page_context,
    build_compact_index_page_context,
    build_compact_releases_page_context,
    build_index_page_context,
    build_releases_page_context,
)


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

    def test_build_compact_releases_page_context_paginates_globally_without_item_duplication(self):
        app_info = self._sample_app_info()
        app_info["android_releases"] = [{"version": "1.2.0.1", "platform": "android"}]
        app_info["current_android_releases"] = list(app_info["android_releases"])
        app_info["archived_android_releases"] = [{"version": "1.1.0.1", "platform": "android"}]
        context = build_compact_releases_page_context(app_info, page=2, page_size=2)

        self.assertEqual(context["archive_visible_item_count"], 3)
        self.assertEqual(context["archive_visible_group_count"], 2)
        self.assertEqual(context["archive_page"], 2)
        self.assertEqual(context["archive_total_pages"], 2)
        self.assertEqual(context["archive_page_item_count"], 1)
        self.assertEqual(context["archive_page_group_count"], 1)
        self.assertTrue(context["archive_has_previous"])
        self.assertFalse(context["archive_has_next"])
        group = context["archive_visible_groups"][0]
        self.assertEqual(group["branch"], "1.0.0")
        self.assertNotIn("items", group)
        self.assertEqual(len(group["visible_items"]), 1)
        self.assertEqual(group["page_item_count"], 1)
        self.assertNotIn("archive_timeline_groups", context["app_info"])
        self.assertNotIn("android_releases", context["app_info"])
        self.assertEqual(len(context["app_info"]["archived_android_releases"]), 1)
        self.assertEqual(context["android_page"], 1)
        self.assertEqual(context["android_total_item_count"], 1)

    def test_compact_group_totals_remain_global_when_one_group_spans_pages(self):
        app_info = self._sample_app_info()
        group_items = []
        for fix in range(150):
            is_archive = fix % 2 == 0
            group_items.append({
                "version": f"2.0.0.{fix}",
                "kind": "ZIP" if is_archive else "EXE",
                "kind_label": "ZIP 归档" if is_archive else "安装包",
                "era": "archive" if is_archive else "installer",
                "era_label": "压缩归档时代" if is_archive else "安装包时代",
                "relative_path": f"History/2.0/2.0.0/{fix}",
            })
        app_info["archive_timeline_groups"] = [{
            "major_minor": "2.0",
            "branch": "2.0.0",
            "items": group_items,
            "count": 150,
        }]
        app_info["archived_android_releases"] = []

        first = build_compact_releases_page_context(app_info, page=1, page_size=100)
        second = build_compact_releases_page_context(app_info, page=2, page_size=100)

        for context, expected_page_count in ((first, 100), (second, 50)):
            group = context["archive_visible_groups"][0]
            self.assertEqual(group["visible_count"], 150)
            self.assertEqual(group["page_item_count"], expected_page_count)
            self.assertEqual(group["visible_kind_summary"], "ZIP 归档 · 安装包")
            self.assertEqual(group["visible_era_summary"], "压缩归档时代 · 安装包时代")

    def test_build_compact_index_page_context_has_only_home_consumed_fields(self):
        context = build_compact_index_page_context({
            "app_info": self._sample_app_info(),
            "update_summary": {"canonical_count": 2},
            "tool_summary": {"file_count": 3},
            "recent_change_dashboard": [{"title": "change"}],
            "docs": {"total": 1},
            "overview": [{"name": "must not leak"}],
        })

        self.assertEqual(
            set(context),
            {"app_info", "update_summary", "tool_summary", "recent_change_dashboard", "docs"},
        )
        self.assertNotIn("archive_recent", context["app_info"])
        self.assertNotIn("archive_timeline_groups", context["app_info"])
        self.assertEqual(context["app_info"]["current_count"], 1)

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

    def test_build_browse_page_context_only_builds_current_page_items(self):
        for index in range(5):
            (self.storage / f"item-{index}.txt").write_text(str(index), encoding="utf-8")

        import storage_browser
        original = storage_browser._build_listing_item
        built_items: list[str] = []

        def wrapped(entry: Path, relative_path: str):
            built_items.append(entry.name)
            return original(entry, relative_path)

        with patch("storage_browser._build_listing_item", side_effect=wrapped):
            context = build_browse_page_context(self.storage, "", limit=2, offset=1)

        self.assertEqual(context["total_count"], 5)
        self.assertEqual([item["name"] for item in context["items"]], ["item-1.txt", "item-2.txt"])
        self.assertEqual(built_items, ["item-1.txt", "item-2.txt"])


if __name__ == "__main__":
    unittest.main()



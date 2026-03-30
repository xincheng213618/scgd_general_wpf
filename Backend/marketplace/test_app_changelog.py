import tempfile
import unittest
from pathlib import Path

import app_changelog


class AppChangelogTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.path = Path(self.temp_dir.name) / "CHANGELOG.md"
        self.cache: dict[str, dict] = {}

    def tearDown(self):
        self.temp_dir.cleanup()

    def _get_cache_entry(self, key: str, **kwargs):
        cached = self.cache.get(key)
        if not cached:
            return None
        signature = kwargs.get("signature")
        if signature is not None and cached.get("signature") != signature:
            return None
        return cached

    def _set_cache_entry(self, key: str, value, **kwargs):
        self.cache[key] = {"value": value, "signature": kwargs.get("signature", "")}

    def test_parse_changelog_entries_extracts_versions_dates_and_items(self):
        text = """# CHANGELOG\n\n## [1.2.0.1] 2026.03.24\n\n1.新增插件市场\n2.优化下载中心\n\n## [1.1.0.1] 2026.03.01\n\n1.修复更新逻辑\n"""

        entries = app_changelog.parse_changelog_entries(text)

        self.assertEqual(len(entries), 2)
        self.assertEqual(entries[0]["version"], "1.2.0.1")
        self.assertEqual(entries[0]["date_display"], "2026-03-24")
        self.assertEqual(entries[0]["change_count"], 2)
        self.assertIn("插件生态", entries[0]["tags"])

    def test_analyze_changelog_text_builds_chart_and_milestones(self):
        text = """# CHANGELOG\n\n## [1.2.0.1] 2026.03.24\n\n1.新增插件市场\n2.优化下载中心\n3.重构更新逻辑\n\n## [1.1.0.1] 2026.03.01\n\n1.修复更新逻辑\n"""

        analysis = app_changelog.analyze_changelog_text(text)

        self.assertEqual(analysis["summary"]["release_count"], 2)
        self.assertTrue(analysis["chart"]["svg"]["points"])
        self.assertIn("M ", analysis["chart"]["svg"]["path"])
        self.assertTrue(analysis["milestones"])

    def test_get_cached_changelog_analysis_invalidates_when_signature_changes(self):
        self.path.write_text("## [1.0.0.1] 2026.03.01\n\n1.初始版本\n", encoding="utf-8")
        first = app_changelog.get_cached_changelog_analysis(
            self.path,
            get_cache_entry=self._get_cache_entry,
            set_cache_entry=self._set_cache_entry,
            cache_key="changelog",
            ttl_seconds=3600,
        )

        self.path.write_text("## [9.9.9.9] 2026.03.02\n\n1.不会命中旧签名\n", encoding="utf-8")
        second = app_changelog.get_cached_changelog_analysis(
            self.path,
            get_cache_entry=self._get_cache_entry,
            set_cache_entry=self._set_cache_entry,
            cache_key="changelog",
            ttl_seconds=3600,
        )

        self.assertNotEqual(first["entries"][0]["version"], second["entries"][0]["version"])


if __name__ == "__main__":
    unittest.main()



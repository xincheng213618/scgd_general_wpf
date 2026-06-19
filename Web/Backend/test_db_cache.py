import json
import tempfile
import unittest
from pathlib import Path

from db_cache import CacheManager, now_ts


class CacheManagerTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.db_path = Path(self.tmp.name) / "test.db"
        self.cache = CacheManager(self.db_path)
        self.cache.init_db()

    def tearDown(self):
        self.tmp.cleanup()

    def test_init_db_creates_tables(self):
        db = self.cache.get_db()
        tables = {row[0] for row in db.execute(
            "SELECT name FROM sqlite_master WHERE type='table'"
        ).fetchall()}
        db.close()
        self.assertIn("download_log", tables)
        self.assertIn("cache_entry", tables)

    def test_set_and_get_cache_entry(self):
        self.cache.set_cache_entry("k1", {"a": 1}, ttl_seconds=60)
        result = self.cache.get_cache_entry("k1")
        self.assertIsNotNone(result)
        self.assertEqual(result["value"], {"a": 1})

    def test_get_expired_entry_returns_none(self):
        self.cache.set_cache_entry("k2", "val", ttl_seconds=0)
        result = self.cache.get_cache_entry("k2")
        self.assertIsNone(result)

    def test_signature_match_returns_value(self):
        self.cache.set_cache_entry("k3", "val", ttl_seconds=60, signature="sig1")
        result = self.cache.get_cache_entry("k3", signature="sig1")
        self.assertIsNotNone(result)
        self.assertEqual(result["value"], "val")

    def test_signature_mismatch_returns_none(self):
        self.cache.set_cache_entry("k4", "val", ttl_seconds=60, signature="sig1")
        result = self.cache.get_cache_entry("k4", signature="sig2")
        self.assertIsNone(result)

    def test_invalidate_cache_prefix(self):
        self.cache.set_cache_entry("prefix:a", 1, ttl_seconds=60)
        self.cache.set_cache_entry("prefix:b", 2, ttl_seconds=60)
        self.cache.set_cache_entry("other:c", 3, ttl_seconds=60)
        self.cache.invalidate_cache_prefix("prefix:")
        self.assertIsNone(self.cache.get_cache_entry("prefix:a"))
        self.assertIsNone(self.cache.get_cache_entry("prefix:b"))
        self.assertIsNotNone(self.cache.get_cache_entry("other:c"))

    def test_refresh_related_caches_clears_expected_prefixes(self):
        for key in ["storage_overview:x", "app_releases:x", "home_release_snapshot:x",
                     "home_tool_preview:x", "release_timeline:x"]:
            self.cache.set_cache_entry(key, "v", ttl_seconds=60)
        self.cache.refresh_related_caches()
        for key in ["storage_overview:x", "app_releases:x", "home_release_snapshot:x",
                     "home_tool_preview:x", "release_timeline:x"]:
            self.assertIsNone(self.cache.get_cache_entry(key))

    def test_refresh_related_caches_with_plugin_id(self):
        self.cache.set_cache_entry("plugin_summary:p1", "v", ttl_seconds=60)
        self.cache.set_cache_entry("plugin_detail:p1", "v", ttl_seconds=60)
        self.cache.set_cache_entry("dir_file_count:Plugins/p1", "v", ttl_seconds=60)
        self.cache.refresh_related_caches(plugin_id="p1")
        self.assertIsNone(self.cache.get_cache_entry("plugin_summary:p1"))
        self.assertIsNone(self.cache.get_cache_entry("plugin_detail:p1"))

    def test_refresh_related_caches_clears_plugin_catalog_hash_and_archive_metadata(self):
        self.cache.set_cache_entry("plugin_catalog:v1", "catalog", ttl_seconds=60)
        self.cache.set_cache_entry("plugin_package_hash:v1:Plugins/p1/p1-1.0.0.cvxp", "hash", ttl_seconds=60)
        self.cache.set_cache_entry("plugin_archive_meta:v1:Plugins/p1/p1-1.0.0.cvxp", {"has_icon": True}, ttl_seconds=60)

        self.cache.refresh_related_caches(plugin_id="p1", relative_path="Plugins/p1")

        self.assertIsNone(self.cache.get_cache_entry("plugin_catalog:v1"))
        self.assertIsNone(self.cache.get_cache_entry("plugin_package_hash:v1:Plugins/p1/p1-1.0.0.cvxp"))
        self.assertIsNone(self.cache.get_cache_entry("plugin_archive_meta:v1:Plugins/p1/p1-1.0.0.cvxp"))

    def test_now_ts_returns_int(self):
        ts = now_ts()
        self.assertIsInstance(ts, int)
        self.assertGreater(ts, 1_700_000_000)


if __name__ == "__main__":
    unittest.main()

import os
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from services.app_latest_version_cache import _key


class AppLatestVersionCacheTests(unittest.TestCase):
    def test_key_is_lexically_normalized_without_resolving_filesystem(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            storage = Path(temp_dir) / "missing" / ".." / "storage"
            with patch.object(Path, "resolve", side_effect=AssertionError("must not resolve")):
                actual = _key(storage)

        self.assertEqual(actual, os.path.normcase(os.path.abspath(os.fspath(storage))))

    def test_key_collapses_lexical_dot_segments(self):
        base = Path("nonexistent-root")
        self.assertEqual(_key(base / "child" / ".." / "storage"), _key(base / "storage"))


if __name__ == "__main__":
    unittest.main()

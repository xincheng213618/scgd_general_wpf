import tempfile
import unittest
from pathlib import Path

from Scripts.build_basic_templates import ASSETS, RUNTIME_PATH, build, validate_runtime
from Scripts.build_update import get_all_files


class BasicTemplateDeliveryTests(unittest.TestCase):
    def test_shipped_database_rebuilds_from_source_and_is_in_update_archive(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / RUNTIME_PATH
            build(ASSETS / "BasicTemplates.json", target)
            self.assertEqual((ASSETS / "BasicTemplates.db").read_bytes(), target.read_bytes())
            validate_runtime(root, {RUNTIME_PATH})
            self.assertIn(RUNTIME_PATH, {Path(p).relative_to(root).as_posix() for p in get_all_files(str(root))})

    def test_missing_misplaced_and_corrupt_catalog_block_installer(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaisesRegex(ValueError, "Missing"):
                validate_runtime(root, {RUNTIME_PATH})
            target = root / RUNTIME_PATH
            build(ASSETS / "BasicTemplates.json", target)
            with self.assertRaisesRegex(ValueError, "does not deliver"):
                validate_runtime(root, {"BasicTemplates.db"})
            target.write_bytes(b"invalid sqlite")
            with self.assertRaisesRegex(ValueError, "Invalid"):
                validate_runtime(root, {RUNTIME_PATH})


if __name__ == "__main__":
    unittest.main()

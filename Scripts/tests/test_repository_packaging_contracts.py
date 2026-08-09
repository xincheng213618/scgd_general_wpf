import hashlib
import json
import re
import unittest
from pathlib import Path, PurePosixPath


REPO_ROOT = Path(__file__).resolve().parents[2]
SHARED_FILES_PATHS = (
    Path("Scripts/shared_files.json"),
    Path("SDK/ColorVision.PluginKit/scripts/shared_files.json"),
)
MANIFEST_ROOTS = ("Plugins", "Projects")
PACKAGE_ID_PATTERN = re.compile(r"^[A-Za-z][A-Za-z0-9._-]{0,63}$")


class SharedFilesContractTests(unittest.TestCase):
    def test_repository_and_sdk_shared_files_are_byte_identical(self) -> None:
        repository_path, sdk_path = (REPO_ROOT / path for path in SHARED_FILES_PATHS)
        repository_bytes = repository_path.read_bytes()
        sdk_bytes = sdk_path.read_bytes()

        if repository_bytes != sdk_bytes:
            repository_hash = hashlib.sha256(repository_bytes).hexdigest()
            sdk_hash = hashlib.sha256(sdk_bytes).hexdigest()
            self.fail(
                f"{SHARED_FILES_PATHS[0].as_posix()} sha256={repository_hash}\n"
                f"{SHARED_FILES_PATHS[1].as_posix()} sha256={sdk_hash}"
            )


class RepositoryManifestContractTests(unittest.TestCase):
    def test_existing_top_level_plugin_and_project_manifests_use_safe_paths(self) -> None:
        for root_name in MANIFEST_ROOTS:
            manifest_paths = sorted(
                path for path in (REPO_ROOT / root_name).glob("*/manifest.json") if path.is_file()
            )
            self.assertTrue(manifest_paths, f"No top-level manifests found under {root_name}")

            for manifest_path in manifest_paths:
                relative_path = manifest_path.relative_to(REPO_ROOT).as_posix()
                with self.subTest(manifest=relative_path):
                    manifest = json.loads(manifest_path.read_bytes().decode("utf-8-sig"))
                    self.assertIsInstance(manifest, dict)
                    fields = {str(key).lower(): value for key, value in manifest.items()}

                    plugin_id = fields.get("id", "")
                    self.assertIsInstance(plugin_id, str)
                    plugin_id = plugin_id.strip()
                    self.assertLessEqual(len(plugin_id), 64)
                    self.assertRegex(plugin_id, PACKAGE_ID_PATTERN)

                    dll_path = fields.get("dllpath", "")
                    self.assertIsInstance(dll_path, str)
                    dll_path = dll_path.strip()
                    self.assertLessEqual(len(dll_path), 260)
                    if dll_path:
                        normalized_dll_path = PurePosixPath(dll_path.replace("\\", "/"))
                        self.assertFalse(normalized_dll_path.is_absolute())
                        self.assertNotIn("..", normalized_dll_path.parts)
                        self.assertNotIn(":", dll_path)
                        self.assertEqual(".dll", normalized_dll_path.suffix.lower())


if __name__ == "__main__":
    unittest.main()

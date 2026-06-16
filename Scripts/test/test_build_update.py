import unittest
import os
import tempfile
from unittest.mock import patch

import build_update


class BuildUpdateTests(unittest.TestCase):
    def test_get_file_version_falls_back_to_windows_resource_reader(self):
        with (
            patch("build_update.get_file_version_from_pefile", return_value=None),
            patch("build_update.get_file_version_from_windows_resource", return_value="1.2.3.4"),
        ):
            self.assertEqual(build_update.get_file_version("ColorVision.exe"), "1.2.3.4")

    def test_main_fails_when_version_is_missing(self):
        with patch("build_update.get_file_version", return_value=None), patch("builtins.print") as mocked_print:
            exit_code = build_update.main()

        self.assertEqual(exit_code, 1)
        self.assertTrue(mocked_print.called)
        self.assertIn("无法从", mocked_print.call_args[0][0])

    def test_get_all_files_can_exclude_shell_extension_files(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            files = [
                "ColorVision.exe",
                "ColorVision.ShellExtension.dll",
                "ColorVision.ShellExtension.runtimeconfig.json",
                os.path.join("runtimes", "win-x64", "native.dll"),
            ]

            for relative_path in files:
                file_path = os.path.join(temp_dir, relative_path)
                os.makedirs(os.path.dirname(file_path), exist_ok=True)
                with open(file_path, "w", encoding="utf-8") as file:
                    file.write(relative_path)

            included = {
                os.path.relpath(file_path, temp_dir).replace("\\", "/")
                for file_path in build_update.get_all_files(temp_dir)
            }
            incremental = {
                os.path.relpath(file_path, temp_dir).replace("\\", "/")
                for file_path in build_update.get_all_files(temp_dir, include_shell_extension=False)
            }

        self.assertIn("ColorVision.ShellExtension.dll", included)
        self.assertIn("ColorVision.ShellExtension.runtimeconfig.json", included)
        self.assertNotIn("ColorVision.ShellExtension.dll", incremental)
        self.assertNotIn("ColorVision.ShellExtension.runtimeconfig.json", incremental)
        self.assertIn("ColorVision.exe", incremental)
        self.assertIn("runtimes/win-x64/native.dll", incremental)


if __name__ == "__main__":
    unittest.main()


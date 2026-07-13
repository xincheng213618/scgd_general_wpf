import os
import tempfile
import unittest
import zipfile
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

    def test_main_fails_when_required_runtime_payload_is_missing(self):
        with (
            patch("build_update.get_file_version", return_value="1.2.3.4"),
            patch("build_update.validate_release_runtime_payload", return_value=False),
            patch("build_update.create_directory_if_not_exists") as mocked_create_directory,
        ):
            exit_code = build_update.main()

        self.assertEqual(exit_code, 1)
        mocked_create_directory.assert_not_called()

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

    def test_get_all_files_skips_root_service_host_but_keeps_service_host_folder(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            files = [
                "ColorVision.exe",
                "ColorVisionServiceHost.exe",
                "ColorVisionServiceHost.deps.json",
                os.path.join("ServiceHost", "ColorVisionServiceHost.exe"),
                os.path.join("ServiceHost", "Tasks", "RegisterFileAssociations.ps1"),
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

        self.assertIn("ColorVision.exe", included)
        self.assertNotIn("ColorVisionServiceHost.exe", included)
        self.assertNotIn("ColorVisionServiceHost.deps.json", included)
        self.assertIn("ServiceHost/ColorVisionServiceHost.exe", included)
        self.assertIn("ServiceHost/Tasks/RegisterFileAssociations.ps1", included)

    def test_get_all_files_skips_publish_directory(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            files = [
                "ColorVision.exe",
                os.path.join("publish", "ColorVision.exe"),
                os.path.join("publish", "runtimes", "linux-x64", "native.dll"),
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

        self.assertIn("ColorVision.exe", included)
        self.assertNotIn("publish/ColorVision.exe", included)
        self.assertNotIn("publish/runtimes/linux-x64/native.dll", included)

    def test_incremental_zip_always_contains_required_runtime_files(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            old_zip = os.path.join(temp_dir, "old.zip")
            new_version_dir = os.path.join(temp_dir, "new")
            incremental_zip = os.path.join(temp_dir, "update.cvx")
            os.makedirs(new_version_dir)
            required_content = b"required-runtime"
            unchanged_content = b"unchanged"
            with zipfile.ZipFile(old_zip, "w", zipfile.ZIP_DEFLATED) as archive:
                for required_file in build_update.REQUIRED_MAIN_RUNTIME_FILES:
                    archive.writestr(required_file, required_content)
                archive.writestr("Unchanged.dll", unchanged_content)
            for required_file in build_update.REQUIRED_MAIN_RUNTIME_FILES:
                with open(os.path.join(new_version_dir, required_file), "wb") as file:
                    file.write(required_content)
            with open(os.path.join(new_version_dir, "Unchanged.dll"), "wb") as file:
                file.write(unchanged_content)

            build_update.make_incremental_zip(old_zip, new_version_dir, incremental_zip)

            with zipfile.ZipFile(incremental_zip, "r") as archive:
                self.assertEqual(archive.namelist(), list(build_update.REQUIRED_MAIN_RUNTIME_FILES))
                for required_file in build_update.REQUIRED_MAIN_RUNTIME_FILES:
                    self.assertEqual(archive.read(required_file), required_content)

    def test_main_fails_when_incremental_upload_fails(self):
        with (
            patch("build_update.get_file_version", return_value="1.2.3.4"),
            patch("build_update.validate_release_runtime_payload", return_value=True),
            patch("build_update.create_directory_if_not_exists"),
            patch("build_update.find_latest_zip", return_value="old.zip"),
            patch("build_update.make_incremental_zip"),
            patch("build_update.upload_file", return_value=False),
            patch("build_update.create_full_zip") as mocked_full_zip,
        ):
            exit_code = build_update.main()

        self.assertEqual(exit_code, 1)
        mocked_full_zip.assert_not_called()


if __name__ == "__main__":
    unittest.main()


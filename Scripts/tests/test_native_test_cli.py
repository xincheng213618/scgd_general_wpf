"""Exercise CLI rejection before native suites run; opt in to an explicit build."""

import os
import subprocess
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]


class NativeTestCommandLineTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        configured = os.environ.get("COLORVISION_NATIVE_TEST_EXE")
        if not configured:
            raise unittest.SkipTest("Set COLORVISION_NATIVE_TEST_EXE to the freshly built x64 test executable")
        cls.executable = Path(configured).resolve(strict=True)
        cls.environment = os.environ.copy()
        directories = [cls.executable.parent, REPO_ROOT / "x64/Release",
                       REPO_ROOT / "packages/opencv/x64/vc18/bin"]
        cls.environment["PATH"] = os.pathsep.join(str(path) for path in directories) + os.pathsep + cls.environment.get("PATH", "")

    def invoke(self, *arguments):
        return subprocess.run([str(self.executable), *arguments], cwd=REPO_ROOT,
                              env=self.environment, capture_output=True, encoding="utf-8",
                              errors="replace", timeout=10, check=False)

    def assert_usage_error(self, *arguments):
        completed = self.invoke(*arguments)
        output = completed.stdout + completed.stderr
        self.assertEqual(2, completed.returncode, output)
        self.assertIn("Usage:", output)
        self.assertNotIn("M_FindLuminousArea smoke test", output)

    def test_unknown_modes_do_not_fall_back_to_default_or_cuda_suites(self):
        for mode in ("--rsi-unknown-mode", "--cuda-fusion-unknown-mode"):
            with self.subTest(mode=mode):
                self.assert_usage_error(mode)

    def test_wrong_operand_counts_are_rejected_before_file_or_device_access(self):
        invalid = [
            ("--luminous-v2-only", "extra"),
            ("--luminous-v2-cvraw",),
            ("--calibration-legacy-color", "missing.cvraw", "missing.color"),
            ("--find-cross-cvraw", "missing.cvraw", "0"),
            ("--cuda-fusion-benchmark", "missing.dll", "1", "1", "1"),
            ("--cuda-fusion-compare", "reference.dll", "candidate.dll", "1", "1", "1", "3", "extra"),
            ("--help", "extra"),
            ("missing.png", "extra"),
        ]
        for arguments in invalid:
            with self.subTest(arguments=arguments):
                self.assert_usage_error(*arguments)

    def test_help_lists_focused_modes_without_running_them(self):
        completed = self.invoke("--help")
        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("--luminous-v2-only", completed.stdout)
        self.assertIn("--find-cross-cvraw", completed.stdout)
        self.assertIn("--cuda-fusion-benchmark", completed.stdout)
        self.assertNotIn("M_FindLuminousArea smoke test", completed.stdout)


if __name__ == "__main__":
    unittest.main()

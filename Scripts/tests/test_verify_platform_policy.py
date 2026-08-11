import tempfile
import unittest
from pathlib import Path

from Scripts.verify_platform_policy import (
    PlatformPolicyError,
    validate_arm64_build_guard,
    validate_host_solution,
    validate_native_project,
    validate_platform_policy,
    validate_x64_only_props,
)


REPO_ROOT = Path(__file__).resolve().parents[2]


class PlatformPolicyTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary_directory = tempfile.TemporaryDirectory(prefix="platform-policy-tests-")
        self.root = Path(self._temporary_directory.name)

    def tearDown(self) -> None:
        self._temporary_directory.cleanup()

    def test_current_repository_declares_x64_only_host_support(self) -> None:
        checked = validate_platform_policy(REPO_ROOT)

        self.assertIn((REPO_ROOT / "build.sln").resolve(), checked)
        self.assertIn((REPO_ROOT / "Native/opencv_cuda/opencv_cuda.vcxproj").resolve(), checked)

    def test_rejects_global_arm64_declaration(self) -> None:
        props = self.root / "Directory.Build.props"
        props.write_text("<Project><PropertyGroup><Platforms>x64;ARM64</Platforms></PropertyGroup></Project>", encoding="utf-8")

        with self.assertRaisesRegex(PlatformPolicyError, "must declare exactly"):
            validate_x64_only_props(props)

    def test_rejects_arm64_solution_configuration(self) -> None:
        solution = self.root / "build.sln"
        solution.write_text("Release|ARM64 = Release|ARM64", encoding="utf-8")

        with self.assertRaisesRegex(PlatformPolicyError, "unsupported ARM64"):
            validate_host_solution(solution)

    def test_rejects_native_project_without_release_x64(self) -> None:
        project = self.root / "native.vcxproj"
        project.write_text(
            '<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">'
            '<ItemGroup><ProjectConfiguration Include="Release|ARM64" /></ItemGroup></Project>',
            encoding="utf-8",
        )

        with self.assertRaisesRegex(PlatformPolicyError, r"Release\|x64"):
            validate_native_project(project)

    def test_rejects_platform_guard_without_fail_fast_error(self) -> None:
        targets = self.root / "Directory.Build.targets"
        targets.write_text(
            '<Project><Target Name="ValidateColorVisionHostPlatform" '
            'Condition="\'$(Platform)\' == \'ARM64\' and \'$(AllowStandaloneArm64Build)\' != \'true\'" />'
            '</Project>',
            encoding="utf-8",
        )

        with self.assertRaisesRegex(PlatformPolicyError, "does not stop"):
            validate_arm64_build_guard(targets)


if __name__ == "__main__":
    unittest.main()

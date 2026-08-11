import subprocess
import struct
import tempfile
import unittest
import zipfile
from pathlib import Path

from Scripts.verify_platform_policy import (
    COMIMAGE_FLAGS_32BITREQUIRED,
    PlatformPolicyError,
    _read_anycpu_dotnet_pe,
    validate_arm64_build_guard,
    validate_fileio_anycpu_build_guard,
    validate_fileio_anycpu_project,
    validate_fileio_package,
    validate_fileio_package_directory,
    validate_host_solution,
    validate_native_project,
    validate_platform_policy_import,
    validate_platform_policy,
    validate_x64_only_props,
)


REPO_ROOT = Path(__file__).resolve().parents[2]
POLICY_INITIAL_TARGETS = (
    "ValidateColorVisionHostPlatform;ValidateColorVisionFileIOAnyCpuPackage"
)
HOST_ARM64_CONDITION = (
    "$([System.String]::Copy('$(Platform);$(PlatformTarget);$(RuntimeIdentifier);"
    "$(RuntimeIdentifiers)').ToLowerInvariant().Contains('arm64'))"
)
FILEIO_ANYCPU_CONDITION = (
    "'$(MSBuildProjectName)' == 'ColorVision.FileIO' and "
    "('$(PlatformTarget)' != 'AnyCPU' or '$(RuntimeIdentifier)' != '' or "
    "'$(RuntimeIdentifiers)' != '')"
)


class PlatformPolicyTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary_directory = tempfile.TemporaryDirectory(prefix="platform-policy-tests-")
        self.root = Path(self._temporary_directory.name)

    def tearDown(self) -> None:
        self._temporary_directory.cleanup()

    def test_current_repository_declares_x64_release_support(self) -> None:
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

    def test_rejects_platform_guard_condition_contract_mutations(self) -> None:
        mutations = (
            ("platform", "$(Platform)", "$(Configuration)"),
            ("platform-target", "$(PlatformTarget)", "$(Configuration)"),
            ("runtime-identifier", "$(RuntimeIdentifier)", "$(Configuration)"),
            ("runtime-identifiers", "$(RuntimeIdentifiers)", "$(Configuration)"),
        )
        for name, old, new in mutations:
            with self.subTest(name=name):
                targets = self.root / f"{name}.targets"
                targets.write_text(
                    f'<Project InitialTargets="{POLICY_INITIAL_TARGETS}">'
                    '<Target Name="ValidateColorVisionHostPlatform" '
                    'BeforeTargets="PrepareForBuild;Pack" '
                    f'Condition="{HOST_ARM64_CONDITION.replace(old, new, 1)}">'
                    '<Error Text="stop" /></Target></Project>',
                    encoding="utf-8",
                )
                with self.assertRaisesRegex(PlatformPolicyError, "fail closed"):
                    validate_arm64_build_guard(targets)

    def test_rejects_platform_guard_without_fail_fast_error(self) -> None:
        targets = self.root / "Directory.Build.targets"
        targets.write_text(
            f'<Project InitialTargets="{POLICY_INITIAL_TARGETS}">'
            '<Target Name="ValidateColorVisionHostPlatform" '
            'BeforeTargets="PrepareForBuild;Pack" '
            f'Condition="{HOST_ARM64_CONDITION}" /></Project>',
            encoding="utf-8",
        )

        with self.assertRaisesRegex(PlatformPolicyError, "does not stop"):
            validate_arm64_build_guard(targets)

    def test_rejects_platform_policy_initial_target_mutations(self) -> None:
        mutations = (
            ("missing", None),
            ("missing-host", "ValidateColorVisionFileIOAnyCpuPackage"),
            (
                "reordered",
                "ValidateColorVisionFileIOAnyCpuPackage;ValidateColorVisionHostPlatform",
            ),
            ("extra", f"{POLICY_INITIAL_TARGETS};UnexpectedTarget"),
        )
        for name, initial_targets in mutations:
            with self.subTest(name=name):
                targets = self.root / f"initial-{name}.targets"
                initial_attribute = (
                    f' InitialTargets="{initial_targets}"' if initial_targets is not None else ""
                )
                targets.write_text(
                    f"<Project{initial_attribute}>"
                    '<Target Name="ValidateColorVisionHostPlatform" '
                    'BeforeTargets="PrepareForBuild;Pack" '
                    f'Condition="{HOST_ARM64_CONDITION}">'
                    '<Error Text="stop" /></Target></Project>',
                    encoding="utf-8",
                )
                with self.assertRaisesRegex(PlatformPolicyError, "initial targets"):
                    validate_arm64_build_guard(targets)

    def test_rejects_host_guard_before_target_mutations(self) -> None:
        for before_targets in ("", "PrepareForBuild", "Pack", "PrepareForBuild;Pack;Publish"):
            with self.subTest(before_targets=before_targets):
                targets = self.root / f"before-{len(before_targets)}.targets"
                targets.write_text(
                    f'<Project InitialTargets="{POLICY_INITIAL_TARGETS}">'
                    '<Target Name="ValidateColorVisionHostPlatform" '
                    f'BeforeTargets="{before_targets}" Condition="{HOST_ARM64_CONDITION}">'
                    '<Error Text="stop" /></Target></Project>',
                    encoding="utf-8",
                )
                with self.assertRaisesRegex(PlatformPolicyError, "host guard must run"):
                    validate_arm64_build_guard(targets)

    def test_rejects_missing_platform_policy_imports_in_each_msbuild_subtree(self) -> None:
        importers = (
            ("Directory.Build.targets", "$(MSBuildThisFileDirectory)ColorVision.PlatformPolicy.targets"),
            ("Plugins/Directory.Build.targets", "$(MSBuildThisFileDirectory)..\\ColorVision.PlatformPolicy.targets"),
            ("Projects/Directory.Build.targets", "$(MSBuildThisFileDirectory)..\\ColorVision.PlatformPolicy.targets"),
        )
        for relative_path, expected_project in importers:
            with self.subTest(relative_path=relative_path):
                path = self.root / relative_path
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("<Project />", encoding="utf-8")
                with self.assertRaisesRegex(PlatformPolicyError, "must import"):
                    validate_platform_policy_import(path, expected_project)

    def test_rejects_conditional_platform_policy_imports(self) -> None:
        importers = (
            ("Directory.Build.targets", "$(MSBuildThisFileDirectory)ColorVision.PlatformPolicy.targets"),
            ("Plugins/Directory.Build.targets", "$(MSBuildThisFileDirectory)..\\ColorVision.PlatformPolicy.targets"),
            ("Projects/Directory.Build.targets", "$(MSBuildThisFileDirectory)..\\ColorVision.PlatformPolicy.targets"),
        )
        for relative_path, expected_project in importers:
            with self.subTest(relative_path=relative_path):
                path = self.root / relative_path
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(
                    f'<Project><Import Project="{expected_project}" Condition="\'false\'" /></Project>',
                    encoding="utf-8",
                )
                with self.assertRaisesRegex(PlatformPolicyError, "unconditionally"):
                    validate_platform_policy_import(path, expected_project)

    def test_rejects_fileio_build_guard_condition_mutations(self) -> None:
        mutations = (
            ("project-name", "'$(MSBuildProjectName)' == 'ColorVision.FileIO' and ", ""),
            ("platform-target", "'$(PlatformTarget)' != 'AnyCPU' or ", ""),
            ("runtime-identifier", "'$(RuntimeIdentifier)' != '' or ", ""),
            ("runtime-identifiers", " or '$(RuntimeIdentifiers)' != ''", ""),
        )
        for name, old, new in mutations:
            with self.subTest(name=name):
                targets = self.root / f"fileio-{name}.targets"
                targets.write_text(
                    f'<Project InitialTargets="{POLICY_INITIAL_TARGETS}">'
                    '<Target Name="ValidateColorVisionFileIOAnyCpuPackage" '
                    'BeforeTargets="PrepareForBuild;Pack" '
                    f'Condition="{FILEIO_ANYCPU_CONDITION.replace(old, new, 1)}">'
                    '<Error Text="stop" /></Target></Project>',
                    encoding="utf-8",
                )
                with self.assertRaisesRegex(PlatformPolicyError, "fail closed"):
                    validate_fileio_anycpu_build_guard(targets)

    def test_rejects_architecture_specific_fileio_package_project_mutations(self) -> None:
        baseline = (
            "<Project><PropertyGroup>"
            "<TargetFrameworks>net10.0;net8.0;net6.0;net461</TargetFrameworks>"
            "<VersionPrefix>1.5.1.1</VersionPrefix>"
            "<GeneratePackageOnBuild>True</GeneratePackageOnBuild>"
            "<PackageId>ColorVision.FileIO</PackageId>"
            "<Platforms>AnyCPU</Platforms>"
            "<PlatformTarget>AnyCPU</PlatformTarget>"
            "</PropertyGroup></Project>"
        )
        mutations = (
            ("arm64-platform", "<Platforms>AnyCPU</Platforms>", "<Platforms>AnyCPU;ARM64</Platforms>"),
            ("missing-target", "<PlatformTarget>AnyCPU</PlatformTarget>", ""),
            ("legacy-exception", "</PropertyGroup>", "<AllowStandaloneArm64Build>true</AllowStandaloneArm64Build></PropertyGroup>"),
        )
        for name, old, new in mutations:
            with self.subTest(name=name):
                project = self.root / f"{name}.csproj"
                project.write_text(baseline.replace(old, new, 1), encoding="utf-8")
                with self.assertRaises(PlatformPolicyError):
                    validate_fileio_anycpu_project(project)


class EvaluatedPlatformPolicyTests(unittest.TestCase):
    def test_official_host_plugin_and_project_arm64_builds_fail_before_prepare_for_build(self) -> None:
        cases = (
            ("host-platform", "ColorVision/ColorVision.csproj", "ARM64", None, None),
            ("plugin-platform", "Plugins/SystemMonitor/SystemMonitor.csproj", "ARM64", None, None),
            ("project-platform", "Projects/ProjectLUX/ProjectLUX.csproj", "arm64", None, None),
            ("host-rid", "ColorVision/ColorVision.csproj", "x64", "win-arm64", None),
            ("plugin-rid", "Plugins/SystemMonitor/SystemMonitor.csproj", "x64", "win-arm64", None),
            ("project-rid", "Projects/ProjectLUX/ProjectLUX.csproj", "x64", "win-arm64", None),
            ("linux-arm64-rid", "ColorVision/ColorVision.csproj", "x64", "linux-arm64", None),
            (
                "win10-arm64-rid",
                "Plugins/SystemMonitor/SystemMonitor.csproj",
                "x64",
                "win10-arm64",
                None,
            ),
            (
                "spaced-runtime-identifiers",
                "Projects/ProjectLUX/ProjectLUX.csproj",
                "x64",
                None,
                " win-x64 ; win-arm64 ",
            ),
        )
        for name, project, platform, runtime_identifier, runtime_identifiers in cases:
            with self.subTest(name=name):
                result = self._prepare_for_build(
                    project,
                    platform,
                    "net10.0-windows",
                    runtime_identifier=runtime_identifier,
                    runtime_identifiers=runtime_identifiers,
                )
                output = result.stdout + result.stderr
                self.assertNotEqual(0, result.returncode, output)
                self.assertIn("support x64 only", output)
                self.assertNotIn("MSB4057", output)

    def test_official_plugin_and_project_x64_prepare_for_build_succeeds(self) -> None:
        cases = (
            ("Plugins/SystemMonitor/SystemMonitor.csproj", "net10.0-windows"),
            ("Projects/ProjectLUX/ProjectLUX.csproj", "net10.0-windows"),
        )
        for project, framework in cases:
            with self.subTest(project=project):
                result = self._prepare_for_build(project, "x64", framework)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_fileio_anycpu_prepare_for_build_succeeds(self) -> None:
        result = self._prepare_for_build(
            "Engine/ColorVision.FileIO/ColorVision.FileIO.csproj",
            "AnyCPU",
            "net10.0",
        )

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_fileio_architecture_overrides_fail_without_creating_a_package(self) -> None:
        with tempfile.TemporaryDirectory(prefix="fileio-invalid-package-tests-") as output_directory:
            cases = (
                ("arm64-platform", "ARM64", None, None, None, "support x64 only"),
                ("arm64-platform-target", "AnyCPU", "ARM64", None, None, "support x64 only"),
                ("x64-platform-target", "AnyCPU", "x64", None, None, "must remain an AnyCPU"),
                ("x86-platform-target", "AnyCPU", "x86", None, None, "must remain an AnyCPU"),
                ("arm64-rid", "AnyCPU", None, "win-arm64", None, "support x64 only"),
                ("x64-rid", "AnyCPU", None, "win-x64", None, "must remain an AnyCPU"),
                ("x86-rid", "AnyCPU", None, "win-x86", None, "must remain an AnyCPU"),
                (
                    "runtime-identifiers",
                    "AnyCPU",
                    None,
                    None,
                    "win-x64;win-x86",
                    "must remain an AnyCPU",
                ),
            )
            for name, platform, platform_target, runtime_identifier, runtime_identifiers, message in cases:
                with self.subTest(name=name):
                    case_output = Path(output_directory) / name
                    result = self._pack_fileio(
                        platform,
                        case_output,
                        platform_target=platform_target,
                        runtime_identifier=runtime_identifier,
                        runtime_identifiers=runtime_identifiers,
                    )
                    output = result.stdout + result.stderr
                    self.assertNotEqual(0, result.returncode, output)
                    self.assertIn(message, output)
                    self.assertEqual([], list(self._package_directory(case_output).iterdir()))

    def test_fileio_arm64_pack_no_build_fails_without_creating_a_package(self) -> None:
        with tempfile.TemporaryDirectory(prefix="fileio-arm64-no-build-pack-") as output_directory:
            output_root = Path(output_directory)
            package_output = self._package_directory(output_root)
            package_output.mkdir()
            result = subprocess.run(
                [
                    "dotnet",
                    "pack",
                    str(REPO_ROOT / "Engine/ColorVision.FileIO/ColorVision.FileIO.csproj"),
                    "--no-build",
                    "--no-restore",
                    "-nologo",
                    "-c",
                    "Release",
                    "-p:Platform=ARM64",
                    f"-p:BaseOutputPath={output_root / 'bin'}\\",
                    f"-p:PackageOutputPath={package_output}",
                    "-verbosity:minimal",
                ],
                cwd=REPO_ROOT,
                capture_output=True,
                text=True,
                errors="replace",
                check=False,
            )
            output = result.stdout + result.stderr
            self.assertNotEqual(0, result.returncode, output)
            self.assertIn("support x64 only", output)
            self.assertEqual([], list(package_output.iterdir()))

    def test_fileio_package_has_stable_coordinates_and_anycpu_pe_assets(self) -> None:
        with tempfile.TemporaryDirectory(prefix="fileio-anycpu-package-tests-") as output_directory:
            output_root = Path(output_directory)
            result = self._pack_fileio("AnyCPU", output_root)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)

            version = validate_fileio_anycpu_project(REPO_ROOT / "Engine/ColorVision.FileIO/ColorVision.FileIO.csproj")
            package_path = validate_fileio_package_directory(
                self._package_directory(output_root), version
            )
            members = validate_fileio_package(package_path, version)

            architecture_directory = output_root / "architecture-mutation"
            architecture_directory.mkdir()
            architecture_package = architecture_directory / package_path.name
            self._mutate_package(
                package_path,
                architecture_package,
                "lib/net10.0/ColorVision.FileIO.dll",
                (REPO_ROOT / "x64/Release/opencv_cuda.dll").read_bytes(),
            )
            with self.assertRaisesRegex(PlatformPolicyError, "PE identity"):
                validate_fileio_package(architecture_package, version)

            with zipfile.ZipFile(package_path) as archive:
                anycpu_member = "lib/net10.0/ColorVision.FileIO.dll"
                anycpu_bytes = archive.read(anycpu_member)
                nuspec_name = next(name for name in archive.namelist() if name.endswith(".nuspec"))
                nuspec_bytes = archive.read(nuspec_name)

            _, _, cor_flags, cor_flags_offset = _read_anycpu_dotnet_pe(
                anycpu_bytes, anycpu_member
            )
            self.assertGreaterEqual(cor_flags_offset, 0)
            x86_bytes = bytearray(anycpu_bytes)
            struct.pack_into(
                "<I",
                x86_bytes,
                cor_flags_offset,
                cor_flags | COMIMAGE_FLAGS_32BITREQUIRED,
            )
            x86_directory = output_root / "x86-corflags-mutation"
            x86_directory.mkdir()
            x86_package = x86_directory / package_path.name
            self._mutate_package(
                package_path,
                x86_package,
                anycpu_member,
                bytes(x86_bytes),
            )
            with self.assertRaisesRegex(PlatformPolicyError, "CLR flags"):
                validate_fileio_package(x86_package, version)

            extra_pe_directory = output_root / "extra-pe-mutation"
            extra_pe_directory.mkdir()
            extra_pe_package = extra_pe_directory / package_path.name
            self._mutate_package(
                package_path,
                extra_pe_package,
                "tools/native.dll",
                (REPO_ROOT / "x64/Release/opencv_cuda.dll").read_bytes(),
            )
            with self.assertRaisesRegex(PlatformPolicyError, "PE assets drifted"):
                validate_fileio_package(extra_pe_package, version)

            hidden_pe_directory = output_root / "hidden-pyd-mutation"
            hidden_pe_directory.mkdir()
            hidden_pe_package = hidden_pe_directory / package_path.name
            self._mutate_package(
                package_path,
                hidden_pe_package,
                "tools/.native.pyd",
                (REPO_ROOT / "x64/Release/opencv_cuda.dll").read_bytes(),
            )
            with self.assertRaisesRegex(PlatformPolicyError, "PE assets drifted"):
                validate_fileio_package(hidden_pe_package, version)

            duplicate_members = (
                ("casefold", "LIB/net10.0/colorvision.fileio.dll"),
                ("backslash", "lib\\net10.0\\ColorVision.FileIO.dll"),
            )
            for name, duplicate_member in duplicate_members:
                with self.subTest(duplicate=name):
                    duplicate_directory = output_root / f"duplicate-{name}"
                    duplicate_directory.mkdir()
                    duplicate_package = duplicate_directory / package_path.name
                    self._mutate_package(
                        package_path,
                        duplicate_package,
                        duplicate_member,
                        anycpu_bytes,
                    )
                    with zipfile.ZipFile(duplicate_package) as duplicate_archive:
                        self.assertIn(
                            duplicate_member,
                            [info.orig_filename for info in duplicate_archive.infolist()],
                        )
                    with self.assertRaises(PlatformPolicyError):
                        validate_fileio_package(duplicate_package, version)

            coordinate_directory = output_root / "coordinate-mutation"
            coordinate_directory.mkdir()
            coordinate_package = coordinate_directory / package_path.name
            old_coordinate = b"<id>ColorVision.FileIO</id>"
            self.assertEqual(1, nuspec_bytes.count(old_coordinate))
            self._mutate_package(
                package_path,
                coordinate_package,
                nuspec_name,
                nuspec_bytes.replace(old_coordinate, b"<id>ColorVision.FileIO.Arm64</id>"),
            )
            with self.assertRaisesRegex(PlatformPolicyError, "package coordinates drifted"):
                validate_fileio_package(coordinate_package, version)

            other_package = self._package_directory(output_root) / "Other.Package.1.0.0.nupkg"
            other_package.write_bytes(package_path.read_bytes())
            with self.assertRaises(PlatformPolicyError):
                validate_fileio_package_directory(self._package_directory(output_root), version)

        self.assertEqual(
            (
                "lib/net10.0/ColorVision.FileIO.dll",
                "lib/net8.0/ColorVision.FileIO.dll",
                "lib/net6.0/ColorVision.FileIO.dll",
                "lib/net461/ColorVision.FileIO.dll",
            ),
            members,
        )

    @staticmethod
    def _prepare_for_build(
        project: str,
        platform: str,
        framework: str,
        *,
        platform_target: str | None = None,
        runtime_identifier: str | None = None,
        runtime_identifiers: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        command = [
            "dotnet",
            "msbuild",
            str(REPO_ROOT / project),
            "-nologo",
            "-t:PrepareForBuild",
            "-p:Configuration=Release",
            f"-p:Platform={platform}",
            f"-p:TargetFramework={framework}",
            "-verbosity:minimal",
        ]
        for name, value in (
            ("PlatformTarget", platform_target),
            ("RuntimeIdentifier", runtime_identifier),
            ("RuntimeIdentifiers", runtime_identifiers),
        ):
            if value is not None:
                command.append(EvaluatedPlatformPolicyTests._property_argument(name, value))
        return subprocess.run(
            command,
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            errors="replace",
            check=False,
        )

    @staticmethod
    def _pack_fileio(
        platform: str,
        output_directory: Path,
        platform_target: str | None = None,
        runtime_identifier: str | None = None,
        runtime_identifiers: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        output_directory.mkdir(parents=True, exist_ok=True)
        base_output = output_directory / "bin"
        package_output = EvaluatedPlatformPolicyTests._package_directory(output_directory)
        package_output.mkdir()
        command = [
            "dotnet",
            "build",
            str(REPO_ROOT / "Engine/ColorVision.FileIO/ColorVision.FileIO.csproj"),
            "--no-restore",
            "--no-incremental",
            "-nologo",
            "-c",
            "Release",
            f"-p:Platform={platform}",
            f"-p:BaseOutputPath={base_output}\\",
            f"-p:PackageOutputPath={package_output}",
            "-verbosity:minimal",
        ]
        for name, value in (
            ("PlatformTarget", platform_target),
            ("RuntimeIdentifier", runtime_identifier),
            ("RuntimeIdentifiers", runtime_identifiers),
        ):
            if value is not None:
                command.append(EvaluatedPlatformPolicyTests._property_argument(name, value))
        return subprocess.run(
            command,
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            errors="replace",
            check=False,
        )

    @staticmethod
    def _package_directory(output_directory: Path) -> Path:
        return output_directory / "package"

    @staticmethod
    def _property_argument(name: str, value: str) -> str:
        return f"-p:{name}={value.replace(';', '%3B')}"

    @staticmethod
    def _mutate_package(source: Path, destination: Path, member: str, replacement: bytes) -> None:
        replaced = False
        with zipfile.ZipFile(source) as input_archive, zipfile.ZipFile(
            destination, "w", zipfile.ZIP_DEFLATED
        ) as output_archive:
            for info in input_archive.infolist():
                if info.filename == member:
                    content = replacement
                    replaced = True
                else:
                    content = input_archive.read(info.filename)
                output_archive.writestr(info, content)
            if not replaced:
                added_info = zipfile.ZipInfo(member)
                if "\\" in member:
                    added_info.filename = member
                    added_info.orig_filename = member
                output_archive.writestr(added_info, replacement)


if __name__ == "__main__":
    unittest.main()

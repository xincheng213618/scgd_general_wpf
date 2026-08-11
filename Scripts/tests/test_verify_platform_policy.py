import json
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
    validate_fileio_anycpu_build_guard,
    validate_fileio_anycpu_project,
    validate_fileio_is_only_anycpu_role_declaration,
    validate_fileio_package,
    validate_fileio_package_directory,
    validate_fileio_project_reference_mapping,
    validate_host_solution,
    validate_native_project,
    validate_platform_policy_import,
    validate_platform_policy,
    validate_platform_role_guard,
    validate_x64_build_guard,
    validate_x64_only_props,
    validate_x64_project_platforms,
)


REPO_ROOT = Path(__file__).resolve().parents[2]
POLICY_INITIAL_TARGETS = (
    "ValidateColorVisionPlatformRole;ValidateColorVisionHostPlatform"
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
        self.assertIn((REPO_ROOT / "src/ColorVisionSetup/Directory.Build.props").resolve(), checked)
        self.assertIn((REPO_ROOT / "UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj").resolve(), checked)

    def test_rejects_global_arm64_declaration(self) -> None:
        props = self.root / "Directory.Build.props"
        props.write_text("<Project><PropertyGroup><Platforms>x64;ARM64</Platforms></PropertyGroup></Project>", encoding="utf-8")

        with self.assertRaisesRegex(PlatformPolicyError, "must declare exactly"):
            validate_x64_only_props(props)

    def test_rejects_x64_props_without_fixed_target_or_role(self) -> None:
        baseline = (
            '<Project><PropertyGroup><Platforms>x64</Platforms>'
            '<Platform Condition="\'$(Platform)\' == \'\' or \'$(Platform)\' == \'AnyCPU\'">x64</Platform>'
            '<PlatformTarget>x64</PlatformTarget>'
            '</PropertyGroup><ItemGroup><ColorVisionPlatformRole Include="X64" /></ItemGroup></Project>'
        )
        for name, old in (
            ("target", "<PlatformTarget>x64</PlatformTarget>"),
            ("role", '<ColorVisionPlatformRole Include="X64" />'),
        ):
            with self.subTest(name=name):
                props = self.root / f"{name}.props"
                props.write_text(baseline.replace(old, ""), encoding="utf-8")
                with self.assertRaises(PlatformPolicyError):
                    validate_x64_only_props(props)

    def test_rejects_arm64_solution_configuration(self) -> None:
        solution = self.root / "build.sln"
        solution.write_text("Release|ARM64 = Release|ARM64", encoding="utf-8")

        with self.assertRaisesRegex(PlatformPolicyError, "unsupported ARM64"):
            validate_host_solution(solution)

    def test_rejects_solution_aliases_that_do_not_route_managed_projects_to_policy_platforms(self) -> None:
        baseline = (REPO_ROOT / "build.sln").read_text(encoding="utf-8-sig")
        mutations = (
            (
                "host-anycpu",
                "{AD038578-6456-48BF-9379-151DCBE9DBF5}.Release|Any CPU.ActiveCfg = Release|x64",
                "{AD038578-6456-48BF-9379-151DCBE9DBF5}.Release|Any CPU.ActiveCfg = Release|Any CPU",
            ),
            (
                "fileio-x64",
                "{8716CF0F-7104-1CCD-8D2A-EA9963ABC719}.Release|x64.ActiveCfg = Release|Any CPU",
                "{8716CF0F-7104-1CCD-8D2A-EA9963ABC719}.Release|x64.ActiveCfg = Release|x64",
            ),
        )
        for name, old, new in mutations:
            with self.subTest(name=name):
                self.assertIn(old, baseline)
                solution = self.root / f"{name}.sln"
                solution.write_text(baseline.replace(old, new, 1), encoding="utf-8")
                with self.assertRaisesRegex(PlatformPolicyError, "maps"):
                    validate_host_solution(solution)

    def test_rejects_solution_when_active_and_build_mapping_are_both_removed(self) -> None:
        baseline = (REPO_ROOT / "build.sln").read_text(encoding="utf-8-sig")
        mapping = "{AD038578-6456-48BF-9379-151DCBE9DBF5}.Release|Any CPU"
        active = f"\t\t{mapping}.ActiveCfg = Release|x64\n"
        build = f"\t\t{mapping}.Build.0 = Release|x64\n"
        self.assertIn(active, baseline)
        self.assertIn(build, baseline)
        solution = self.root / "missing-managed-mapping.sln"
        solution.write_text(baseline.replace(active, "", 1).replace(build, "", 1), encoding="utf-8")

        with self.assertRaisesRegex(PlatformPolicyError, "missing managed ActiveCfg"):
            validate_host_solution(solution)

    def test_fileio_is_the_only_allowed_anycpu_role_declaration(self) -> None:
        project = self.root / "Other.csproj"
        project.write_text(
            '<Project><ItemGroup><ColorVisionPlatformRole Include="AnyCPU" /></ItemGroup></Project>',
            encoding="utf-8",
        )

        with self.assertRaisesRegex(PlatformPolicyError, "Only ColorVision.FileIO"):
            validate_fileio_is_only_anycpu_role_declaration(self.root)

    def test_rejects_project_that_advertises_anycpu_alongside_x64(self) -> None:
        project = self.root / "advertised-platforms.csproj"
        project.write_text(
            "<Project><PropertyGroup><Platforms>AnyCPU;x64</Platforms></PropertyGroup></Project>",
            encoding="utf-8",
        )

        with self.assertRaisesRegex(PlatformPolicyError, "only the x64"):
            validate_x64_project_platforms(project)

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
        baseline = (REPO_ROOT / "ColorVision.PlatformPolicy.targets").read_text(encoding="utf-8")
        mutations = (
            (
                "role",
                '<Target Name="ValidateColorVisionHostPlatform" BeforeTargets="PrepareForBuild;Pack"\n\t\t\tCondition="\'@(ColorVisionPlatformRole)\' == \'X64\'">',
                '<Target Name="ValidateColorVisionHostPlatform" BeforeTargets="PrepareForBuild;Pack"\n\t\t\tCondition="\'@(ColorVisionPlatformRole)\' != \'\'">',
            ),
            ("platform", "'$(Platform)' != 'x64'", "'$(Platform)' == ''"),
            ("platform-target", "'$(PlatformTarget)' != 'x64'", "'$(PlatformTarget)' == ''"),
            ("runtime-identifier", "'$(RuntimeIdentifier)' != 'win-x64'", "'$(RuntimeIdentifier)' != 'win-arm64'"),
            ("runtime-identifiers", "'%(Identity)' != 'win-x64'", "'%(Identity)' != 'win-arm64'"),
            ("runtime-item", 'Include="$(RuntimeIdentifiers)"', 'Include="$(RuntimeIdentifier)"'),
        )
        for name, old, new in mutations:
            with self.subTest(name=name):
                targets = self.root / f"{name}.targets"
                self.assertIn(old, baseline)
                targets.write_text(baseline.replace(old, new, 1), encoding="utf-8")
                with self.assertRaises(PlatformPolicyError):
                    validate_x64_build_guard(targets)

    def test_rejects_platform_guard_without_fail_fast_error(self) -> None:
        baseline = (REPO_ROOT / "ColorVision.PlatformPolicy.targets").read_text(encoding="utf-8")
        removed = """\t\t<Error Condition="'$(Platform)' != 'x64'"
\t\t\t   Text="ColorVision host runtime, official plugins, and project packages support x64 only: Platform must be x64." />
"""
        self.assertIn(removed, baseline)
        targets = self.root / "Directory.Build.targets"
        targets.write_text(baseline.replace(removed, "", 1), encoding="utf-8")

        with self.assertRaisesRegex(PlatformPolicyError, "fail closed"):
            validate_x64_build_guard(targets)

    def test_rejects_platform_role_guard_mutations(self) -> None:
        baseline = (REPO_ROOT / "ColorVision.PlatformPolicy.targets").read_text(encoding="utf-8")
        for name, old, new in (
            ("missing-x64", "'@(ColorVisionPlatformRole)' != 'X64'", "'@(ColorVisionPlatformRole)' == ''"),
            ("missing-anycpu", "'@(ColorVisionPlatformRole)' != 'AnyCPU'", "'@(ColorVisionPlatformRole)' == ''"),
            (
                "anycpu-not-bound-to-fileio",
                "'$(MSBuildProjectFullPath)' == '$(_ColorVisionFileIOProjectPath)'",
                "'$(MSBuildProjectFullPath)' != ''",
            ),
            ("missing-project-kind", "'$(MSBuildProjectExtension)' == '.csproj' and ", ""),
        ):
            with self.subTest(name=name):
                targets = self.root / f"role-{name}.targets"
                targets.write_text(baseline.replace(old, new, 1), encoding="utf-8")
                with self.assertRaisesRegex(PlatformPolicyError, "role guard"):
                    validate_platform_role_guard(targets)

    def test_rejects_platform_policy_initial_target_mutations(self) -> None:
        mutations = (
            ("missing", None),
            (
                "missing-host",
                "ValidateColorVisionPlatformRole",
            ),
            (
                "reordered",
                "ValidateColorVisionHostPlatform;ValidateColorVisionPlatformRole",
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
                    '<Target Name="ValidateColorVisionHostPlatform" BeforeTargets="PrepareForBuild;Pack" />'
                    '</Project>',
                    encoding="utf-8",
                )
                with self.assertRaisesRegex(PlatformPolicyError, "initial targets"):
                    validate_x64_build_guard(targets)

    def test_rejects_host_guard_before_target_mutations(self) -> None:
        baseline = (REPO_ROOT / "ColorVision.PlatformPolicy.targets").read_text(encoding="utf-8")
        for before_targets in ("", "PrepareForBuild", "Pack", "PrepareForBuild;Pack;Publish"):
            with self.subTest(before_targets=before_targets):
                targets = self.root / f"before-{len(before_targets)}.targets"
                old = '<Target Name="ValidateColorVisionHostPlatform" BeforeTargets="PrepareForBuild;Pack"'
                new = f'<Target Name="ValidateColorVisionHostPlatform" BeforeTargets="{before_targets}"'
                self.assertIn(old, baseline)
                targets.write_text(baseline.replace(old, new, 1), encoding="utf-8")
                with self.assertRaisesRegex(PlatformPolicyError, "must run"):
                    validate_x64_build_guard(targets)

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
        baseline = (REPO_ROOT / "ColorVision.PlatformPolicy.targets").read_text(encoding="utf-8")
        mutations = (
            ("role", "'@(ColorVisionPlatformRole)' == 'AnyCPU'", "'@(ColorVisionPlatformRole)' != ''"),
            (
                "restore-entry",
                'BeforeTargets="PrepareForBuild;Pack;Restore"',
                'BeforeTargets="PrepareForBuild;Pack"',
            ),
            ("platform", "'$(Platform)' != 'AnyCPU'", "'$(Platform)' == ''"),
            ("platform-target", "'$(PlatformTarget)' != 'AnyCPU'", "'$(PlatformTarget)' == ''"),
            (
                "runtime-identifier",
                "'$(RuntimeIdentifier)' != '' or '$(RuntimeIdentifiers)' != ''",
                "'$(RuntimeIdentifier)' == '' or '$(RuntimeIdentifiers)' != ''",
            ),
            (
                "runtime-identifiers",
                "'$(RuntimeIdentifier)' != '' or '$(RuntimeIdentifiers)' != ''",
                "'$(RuntimeIdentifier)' != '' or '$(RuntimeIdentifiers)' == ''",
            ),
        )
        for name, old, new in mutations:
            with self.subTest(name=name):
                targets = self.root / f"fileio-{name}.targets"
                self.assertIn(old, baseline)
                targets.write_text(baseline.replace(old, new, 1), encoding="utf-8")
                with self.assertRaises(PlatformPolicyError):
                    validate_fileio_anycpu_build_guard(targets)

    def test_rejects_fileio_project_reference_mapping_mutations(self) -> None:
        baseline = (REPO_ROOT / "ColorVision.PlatformPolicy.targets").read_text(encoding="utf-8")
        mutations = (
            (
                "path",
                "'Engine', 'ColorVision.FileIO', 'ColorVision.FileIO.csproj'",
                "'Engine', 'ColorVision.FileIO', 'Other.csproj'",
            ),
            (
                "role",
                '<Target Name="ConfigureColorVisionFileIOProjectReference" BeforeTargets="AssignProjectConfiguration"\n\t\t\tCondition="\'@(ColorVisionPlatformRole)\' == \'X64\'">',
                '<Target Name="ConfigureColorVisionFileIOProjectReference" BeforeTargets="AssignProjectConfiguration"\n\t\t\tCondition="\'@(ColorVisionPlatformRole)\' == \'AnyCPU\'">',
            ),
            (
                "ordering",
                '<Target Name="ConfigureColorVisionFileIOProjectReference" BeforeTargets="AssignProjectConfiguration"',
                '<Target Name="ConfigureColorVisionFileIOProjectReference" BeforeTargets="ResolveProjectReferences"',
            ),
            (
                "transitive-ordering",
                'AfterTargets="IncludeTransitiveProjectReferences"',
                'AfterTargets="PrepareProjectReferences"',
            ),
            (
                "reference-match",
                "'%(ProjectReference.FullPath)' == '$(_ColorVisionFileIOProjectPath)'",
                "'%(ProjectReference.Filename)' == 'ColorVision.FileIO'",
            ),
            (
                "runtime-identifiers",
                ";RuntimeIdentifier;RuntimeIdentifiers</GlobalPropertiesToRemove>",
                ";RuntimeIdentifier</GlobalPropertiesToRemove>",
            ),
            (
                "coverage-ordering",
                'BeforeTargets="MsCoverageReferencedPathMaps"',
                'BeforeTargets="CoreCompile"',
            ),
            (
                "coverage-match",
                "'%(AnnotatedProjects.FullPath)' == '$(_ColorVisionFileIOProjectPath)'",
                "'%(AnnotatedProjects.Filename)' == 'ColorVision.FileIO'",
            ),
        )
        for name, old, new in mutations:
            with self.subTest(name=name):
                targets = self.root / f"fileio-reference-{name}.targets"
                self.assertIn(old, baseline)
                targets.write_text(baseline.replace(old, new, 1), encoding="utf-8")
                with self.assertRaises(PlatformPolicyError):
                    validate_fileio_project_reference_mapping(targets)

    def test_rejects_architecture_specific_fileio_package_project_mutations(self) -> None:
        baseline = (
            '<Project TreatAsLocalProperty="Platform;PlatformTarget;RuntimeIdentifier;RuntimeIdentifiers;'
            '_ColorVisionFileIORequestedPlatform;_ColorVisionFileIORequestedPlatformTarget;'
            '_ColorVisionFileIORequestedRuntimeIdentifier;_ColorVisionFileIORequestedRuntimeIdentifiers">'
            "<PropertyGroup>"
            "<_ColorVisionFileIORequestedPlatform>$(Platform)</_ColorVisionFileIORequestedPlatform>"
            "<_ColorVisionFileIORequestedPlatformTarget>$(PlatformTarget)</_ColorVisionFileIORequestedPlatformTarget>"
            "<_ColorVisionFileIORequestedRuntimeIdentifier>$(RuntimeIdentifier)</_ColorVisionFileIORequestedRuntimeIdentifier>"
            "<_ColorVisionFileIORequestedRuntimeIdentifiers>$(RuntimeIdentifiers)</_ColorVisionFileIORequestedRuntimeIdentifiers>"
            "<TargetFrameworks>net10.0;net8.0;net6.0;net461</TargetFrameworks>"
            "<VersionPrefix>1.5.1.1</VersionPrefix>"
            "<GeneratePackageOnBuild>True</GeneratePackageOnBuild>"
            "<PackageId>ColorVision.FileIO</PackageId>"
            "<Platforms>AnyCPU</Platforms>"
            "<Platform>AnyCPU</Platform>"
            "<PlatformTarget>AnyCPU</PlatformTarget>"
            "<RuntimeIdentifier></RuntimeIdentifier>"
            "<RuntimeIdentifiers></RuntimeIdentifiers>"
            "</PropertyGroup><ItemGroup>"
            '<ColorVisionPlatformRole Remove="@(ColorVisionPlatformRole)" />'
            '<ColorVisionPlatformRole Include="AnyCPU" />'
            "</ItemGroup></Project>"
        )
        mutations = (
            ("arm64-platform", "<Platforms>AnyCPU</Platforms>", "<Platforms>AnyCPU;ARM64</Platforms>"),
            ("missing-target", "<PlatformTarget>AnyCPU</PlatformTarget>", ""),
            ("missing-platform", "<Platform>AnyCPU</Platform>", ""),
            ("missing-role", '<ColorVisionPlatformRole Include="AnyCPU" />', ""),
            ("missing-local-property", "Platform;PlatformTarget", "Platform"),
            (
                "conditional-capture",
                "<_ColorVisionFileIORequestedPlatform>$(Platform)</_ColorVisionFileIORequestedPlatform>",
                "<_ColorVisionFileIORequestedPlatform Condition=\"'false'\">$(Platform)</_ColorVisionFileIORequestedPlatform>",
            ),
            ("legacy-exception", "</PropertyGroup>", "<AllowStandaloneArm64Build>true</AllowStandaloneArm64Build></PropertyGroup>"),
        )
        for name, old, new in mutations:
            with self.subTest(name=name):
                project = self.root / f"{name}.csproj"
                project.write_text(baseline.replace(old, new, 1), encoding="utf-8")
                with self.assertRaises(PlatformPolicyError):
                    validate_fileio_anycpu_project(project)


class EvaluatedPlatformPolicyTests(unittest.TestCase):
    OFFICIAL_PROJECTS = (
        "ColorVision/ColorVision.csproj",
        "Plugins/SystemMonitor/SystemMonitor.csproj",
        "Projects/ProjectLUX/ProjectLUX.csproj",
    )

    def test_official_projects_fail_closed_for_every_non_x64_override(self) -> None:
        overrides = (
            ("platform-x86", "x86", None, None, None),
            ("platform-anycpu", "AnyCPU", None, None, None),
            ("platform-arm64", "ARM64", None, None, None),
            ("platform-target-x86", "x64", "x86", None, None),
            ("platform-target-anycpu", "x64", "AnyCPU", None, None),
            ("rid-win-x86", "x64", None, "win-x86", None),
            ("rid-linux-x64", "x64", None, "linux-x64", None),
            ("mixed-rids", "x64", None, None, " win-x64 ; win-x86 "),
        )
        for project in self.OFFICIAL_PROJECTS:
            for name, platform, platform_target, runtime_identifier, runtime_identifiers in overrides:
                with self.subTest(project=project, name=name):
                    result = self._prepare_for_build(
                        project,
                        platform,
                        "net10.0-windows",
                        platform_target=platform_target,
                        runtime_identifier=runtime_identifier,
                        runtime_identifiers=runtime_identifiers,
                    )
                    output = result.stdout + result.stderr
                    self.assertNotEqual(0, result.returncode, output)
                    self.assertIn("support x64 only", output)
                    self.assertNotIn("MSB4057", output)

    def test_official_project_default_and_design_time_properties_are_x64(self) -> None:
        for project in self.OFFICIAL_PROJECTS:
            with self.subTest(project=project, mode="default"):
                properties = self._evaluated_properties(project)
                self.assertEqual("x64", properties["Platform"])
                self.assertEqual("x64", properties["PlatformTarget"])
                self.assertEqual("x64", properties["Platforms"])
            with self.subTest(project=project, mode="explicit-invalid-platform"):
                properties = self._evaluated_properties(project, platform="x86")
                self.assertEqual("x86", properties["Platform"])
                self.assertEqual("x64", properties["PlatformTarget"])

    def test_official_projects_x64_prepare_for_build_succeeds(self) -> None:
        for project in self.OFFICIAL_PROJECTS:
            with self.subTest(project=project):
                result = self._prepare_for_build(project, "x64", "net10.0-windows")
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_historical_setup_is_explicitly_x64_and_rejects_architecture_overrides(self) -> None:
        project = "src/ColorVisionSetup/ColorVisionSetup.csproj"
        properties = self._evaluated_properties(project)
        self.assertEqual("x64", properties["Platform"])
        self.assertEqual("x64", properties["PlatformTarget"])
        self.assertEqual("x64", properties["Platforms"])

        positive = self._prepare_historical_setup("x64")
        self.assertEqual(0, positive.returncode, positive.stdout + positive.stderr)
        for name, platform, platform_target in (
            ("platform-anycpu", "AnyCPU", None),
            ("platform-x86", "x86", None),
            ("platform-target-anycpu", "x64", "AnyCPU"),
            ("platform-target-x86", "x64", "x86"),
        ):
            with self.subTest(name=name):
                result = self._prepare_historical_setup(platform, platform_target)
                output = result.stdout + result.stderr
                self.assertNotEqual(0, result.returncode, output)
                self.assertIn("support x64 only", output)

    def test_official_project_pack_no_build_fails_before_pack_for_invalid_platforms(self) -> None:
        overrides = (
            ("platform-x86", "x86", None, None, None),
            ("platform-target-anycpu", "x64", "AnyCPU", None, None),
            ("rid-linux-x64", "x64", None, "linux-x64", None),
            ("mixed-rids", "x64", None, None, "win-x64;win-x86"),
        )
        with tempfile.TemporaryDirectory(prefix="x64-pack-no-build-tests-") as output_directory:
            for project in self.OFFICIAL_PROJECTS:
                for name, platform, platform_target, runtime_identifier, runtime_identifiers in overrides:
                    with self.subTest(project=project, name=name):
                        package_output = Path(output_directory) / project.replace("/", "-") / name
                        package_output.mkdir(parents=True)
                        result = self._pack_no_build(
                            project,
                            platform,
                            package_output,
                            platform_target=platform_target,
                            runtime_identifier=runtime_identifier,
                            runtime_identifiers=runtime_identifiers,
                        )
                        output = result.stdout + result.stderr
                        self.assertNotEqual(0, result.returncode, output)
                        self.assertIn("support x64 only", output)
                        self.assertEqual([], list(package_output.iterdir()))

    def test_fileio_anycpu_prepare_for_build_succeeds(self) -> None:
        result = self._prepare_for_build(
            "Engine/ColorVision.FileIO/ColorVision.FileIO.csproj",
            "AnyCPU",
            "net10.0",
        )

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        properties = self._evaluated_properties(
            "Engine/ColorVision.FileIO/ColorVision.FileIO.csproj"
        )
        self.assertEqual("AnyCPU", properties["Platform"])
        self.assertEqual("AnyCPU", properties["PlatformTarget"])

    def test_x64_parent_maps_only_fileio_reference_to_its_anycpu_contract(self) -> None:
        command = [
            "dotnet",
            "msbuild",
            str(REPO_ROOT / "Engine/ColorVision.Engine/ColorVision.Engine.csproj"),
            "-nologo",
            "-t:ConfigureColorVisionFileIOProjectReference",
            "-p:Configuration=Release",
            "-p:Platform=x64",
            "-getItem:ProjectReference",
        ]
        result = subprocess.run(
            command,
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            errors="replace",
            check=False,
        )
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        references = json.loads(result.stdout)["Items"]["ProjectReference"]
        fileio_path = (REPO_ROOT / "Engine/ColorVision.FileIO/ColorVision.FileIO.csproj").resolve()
        fileio_references = [
            reference
            for reference in references
            if Path(reference["FullPath"]).resolve() == fileio_path
        ]
        self.assertEqual(1, len(fileio_references))
        self.assertEqual(
            {"Platform", "PlatformTarget", "RuntimeIdentifier", "RuntimeIdentifiers"},
            {
                value
                for value in fileio_references[0]["GlobalPropertiesToRemove"].split(";")
                if value
            },
        )
        for reference in references:
            if Path(reference["FullPath"]).resolve() != fileio_path:
                removals = set(reference.get("GlobalPropertiesToRemove", "").split(";"))
                self.assertNotIn("Platform", removals)
                self.assertNotIn("PlatformTarget", removals)

    def test_spectrum_x64_restore_graph_keeps_fileio_anycpu(self) -> None:
        result = subprocess.run(
            [
                "dotnet",
                "restore",
                str(REPO_ROOT / "Test/Spectrum.Tests/Spectrum.Tests.csproj"),
                "-p:Platform=x64",
                "--verbosity",
                "minimal",
            ],
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            errors="replace",
            check=False,
        )
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_fileio_direct_x64_cannot_spoof_restore_or_capture_properties(self) -> None:
        spoofed_properties = (
            ("ColorVisionFileIOProjectReference", "true"),
            ("ExcludeRestorePackageImports", "true"),
            ("_ColorVisionFileIORequestedPlatform", "AnyCPU"),
        )
        with tempfile.TemporaryDirectory(prefix="fileio-direct-spoof-") as output_directory:
            for name, value in spoofed_properties:
                with self.subTest(name=name):
                    package_output = Path(output_directory) / name
                    package_output.mkdir()
                    result = subprocess.run(
                        [
                            "dotnet",
                            "build",
                            str(REPO_ROOT / "Engine/ColorVision.FileIO/ColorVision.FileIO.csproj"),
                            "--no-restore",
                            "-p:Platform=x64",
                            f"-p:{name}={value}",
                            f"-p:PackageOutputPath={package_output}",
                            "--verbosity",
                            "minimal",
                        ],
                        cwd=REPO_ROOT,
                        capture_output=True,
                        text=True,
                        errors="replace",
                        check=False,
                    )
                    output = result.stdout + result.stderr
                    self.assertNotEqual(0, result.returncode, output)
                    self.assertIn("must remain an AnyCPU", output)
                    self.assertEqual([], list(package_output.iterdir()))

    def test_fileio_architecture_overrides_fail_without_creating_a_package(self) -> None:
        with tempfile.TemporaryDirectory(prefix="fileio-invalid-package-tests-") as output_directory:
            cases = (
                ("arm64-platform", "ARM64", None, None, None, "must remain an AnyCPU"),
                ("x64-platform", "x64", None, None, None, "must remain an AnyCPU"),
                ("x86-platform", "x86", None, None, None, "must remain an AnyCPU"),
                ("arm64-platform-target", "AnyCPU", "ARM64", None, None, "must remain an AnyCPU"),
                ("x64-platform-target", "AnyCPU", "x64", None, None, "must remain an AnyCPU"),
                ("x86-platform-target", "AnyCPU", "x86", None, None, "must remain an AnyCPU"),
                ("arm64-rid", "AnyCPU", None, "win-arm64", None, "must remain an AnyCPU"),
                ("x64-rid", "AnyCPU", None, "win-x64", None, "must remain an AnyCPU"),
                ("x86-rid", "AnyCPU", None, "win-x86", None, "must remain an AnyCPU"),
                ("linux-x64-rid", "AnyCPU", None, "linux-x64", None, "must remain an AnyCPU"),
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

    def test_fileio_invalid_pack_no_build_fails_without_creating_a_package(self) -> None:
        overrides = (
            ("platform-x64", "x64", None, None, None),
            ("platform-target-x86", "AnyCPU", "x86", None, None),
            ("rid-win-x64", "AnyCPU", None, "win-x64", None),
            ("mixed-rids", "AnyCPU", None, None, "win-x64;win-x86"),
        )
        with tempfile.TemporaryDirectory(prefix="fileio-no-build-pack-") as output_directory:
            for name, platform, platform_target, runtime_identifier, runtime_identifiers in overrides:
                with self.subTest(name=name):
                    package_output = Path(output_directory) / name
                    package_output.mkdir()
                    result = self._pack_no_build(
                        "Engine/ColorVision.FileIO/ColorVision.FileIO.csproj",
                        platform,
                        package_output,
                        platform_target=platform_target,
                        runtime_identifier=runtime_identifier,
                        runtime_identifiers=runtime_identifiers,
                    )
                    output = result.stdout + result.stderr
                    self.assertNotEqual(0, result.returncode, output)
                    self.assertIn("must remain an AnyCPU", output)
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
    def _prepare_historical_setup(
        platform: str,
        platform_target: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        command = [
            "dotnet",
            "msbuild",
            str(REPO_ROOT / "src/ColorVisionSetup/ColorVisionSetup.csproj"),
            "-nologo",
            "-t:PrepareForBuild",
            "-p:Configuration=Release",
            f"-p:Platform={platform}",
            "-verbosity:minimal",
        ]
        if platform_target is not None:
            command.append(f"-p:PlatformTarget={platform_target}")
        return subprocess.run(
            command,
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            errors="replace",
            check=False,
        )

    @staticmethod
    def _evaluated_properties(project: str, *, platform: str | None = None) -> dict[str, str]:
        command = [
            "dotnet",
            "msbuild",
            str(REPO_ROOT / project),
            "-nologo",
            "-p:Configuration=Release",
            "-getProperty:Platform,PlatformTarget,Platforms",
        ]
        if platform is not None:
            command.append(f"-p:Platform={platform}")
        result = subprocess.run(
            command,
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            errors="replace",
            check=False,
        )
        if result.returncode != 0:
            raise AssertionError(result.stdout + result.stderr)
        return json.loads(result.stdout)["Properties"]

    @staticmethod
    def _pack_no_build(
        project: str,
        platform: str,
        package_output: Path,
        *,
        platform_target: str | None = None,
        runtime_identifier: str | None = None,
        runtime_identifiers: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        command = [
            "dotnet",
            "pack",
            str(REPO_ROOT / project),
            "--no-build",
            "--no-restore",
            "-nologo",
            "-c",
            "Release",
            f"-p:Platform={platform}",
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

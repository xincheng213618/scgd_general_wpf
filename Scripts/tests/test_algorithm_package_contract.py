import unittest
import os
import re
import shutil
import subprocess
import tempfile
import textwrap
import zipfile
from pathlib import Path
from unittest import mock
from xml.etree import ElementTree


REPO_ROOT = Path(__file__).resolve().parents[2]
ALGORITHMS_PROJECT = REPO_ROOT / "UI/ColorVision.Algorithms/ColorVision.Algorithms.csproj"
IMAGE_EDITOR_PROJECT = REPO_ROOT / "UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj"
PUBLISH_WORKFLOW = REPO_ROOT / ".github/workflows/dotnet.yml"
BUILD_SOLUTION = REPO_ROOT / "build.sln"
UI_VERSION_PROPS = REPO_ROOT / "UI/Directory.Build.props"
OPENCV_HELPER_PROJECT = REPO_ROOT / "Native/opencv_helper/opencv_helper.vcxproj"


def _elements(path: Path, local_name: str):
    root = ElementTree.parse(path).getroot()
    return [element for element in root.iter() if element.tag.rsplit("}", 1)[-1] == local_name]


def _values(path: Path, local_name: str):
    return [(element.text or "").strip() for element in _elements(path, local_name)]


def _publish_workflow_steps() -> dict[str, str]:
    return dict(re.findall(
        r"^    - name: ([^\n]+)\n(.*?)(?=^    - name: |\Z)",
        PUBLISH_WORKFLOW.read_text(encoding="utf-8"),
        re.MULTILINE | re.DOTALL,
    ))


def _scoped_algorithms_preflight_source() -> str:
    step = _publish_workflow_steps()["Verify scoped Algorithms release"]
    source = re.search(r"        @'\n(.*?)        '@ \| python -", step, re.DOTALL)
    if source is None:
        raise AssertionError("The scoped Algorithms release must validate its package before publishing.")
    return textwrap.dedent(source.group(1))


def _run(*args: str, cwd: Path = REPO_ROOT) -> str:
    environment = os.environ.copy()
    # MSBUILD_EXE_PATH is an input to native-MSBuild discovery only. Passing it
    # through to dotnet makes the SDK resolver probe the Visual Studio MSBuild
    # directory and can load an incompatible System.Text.Json (MSB4242).
    environment.pop("MSBUILD_EXE_PATH", None)
    environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    environment["DOTNET_NOLOGO"] = "1"
    completed = subprocess.run(
        args,
        cwd=cwd,
        env=environment,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if completed.returncode != 0:
        output = "\n".join(part for part in (completed.stdout, completed.stderr) if part)
        raise AssertionError(f"Command failed ({completed.returncode}): {' '.join(args)}\n{output}")
    return completed.stdout


def _vswhere_command(vswhere: Path) -> list[str]:
    return [
        str(vswhere),
        "-latest",
        "-prerelease",
        "-products",
        "*",
        "-requires",
        "Microsoft.Component.MSBuild",
        "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
        "-find",
        r"MSBuild\**\Bin\MSBuild.exe",
    ]


def _package_metadata(package: Path) -> tuple[ElementTree.Element, set[str]]:
    with zipfile.ZipFile(package) as archive:
        names = set(archive.namelist())
        nuspec_names = [name for name in names if name.casefold().endswith(".nuspec")]
        if len(nuspec_names) != 1:
            raise AssertionError(f"Expected one nuspec in {package}, found {nuspec_names}")
        return ElementTree.fromstring(archive.read(nuspec_names[0])), names


def _visual_studio_msbuild() -> Path:
    configured = os.environ.get("MSBUILD_EXE_PATH")
    if configured and Path(configured).is_file():
        return Path(configured)
    discovered = shutil.which("msbuild")
    if discovered:
        return Path(discovered)

    program_files_x86 = os.environ.get("ProgramFiles(x86)")
    if program_files_x86:
        vswhere = Path(program_files_x86) / "Microsoft Visual Studio/Installer/vswhere.exe"
        if vswhere.is_file():
            completed = subprocess.run(
                _vswhere_command(vswhere),
                check=False,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
            )
            candidates = [Path(line.strip()) for line in completed.stdout.splitlines() if line.strip()]
            if completed.returncode == 0 and candidates and candidates[0].is_file():
                return candidates[0]

    raise AssertionError(
        "The clean ImageEditor package contract requires Visual Studio MSBuild with the "
        "Microsoft.VisualStudio.Component.VC.Tools.x86.x64 workload. No compatible MSBuild was found."
    )


def _build_isolated_opencv_helper(root: Path) -> Path:
    msbuild = _visual_studio_msbuild()
    output = root / "native-output"
    intermediate = root / "native-intermediate"
    isolated_repo_root = root / "native-copy-root"
    output.mkdir()
    intermediate.mkdir()
    isolated_repo_root.mkdir()
    print(f"package-contract native prerequisite: {msbuild} -> {output}")
    _run(
        str(msbuild),
        str(OPENCV_HELPER_PROJECT),
        "/m",
        "/t:Build",
        "/p:Configuration=Release",
        "/p:Platform=x64",
        f"/p:OutDir={output}{os.sep}",
        f"/p:IntDir={intermediate}{os.sep}",
        f"/p:ColorVisionRepoRoot={isolated_repo_root}{os.sep}",
    )
    native_binary = output / "opencv_helper.dll"
    if not native_binary.is_file():
        raise AssertionError(f"Native prerequisite did not produce {native_binary}.")
    data = native_binary.read_bytes()
    if len(data) < 1024 or not data.startswith(b"MZ"):
        raise AssertionError(f"Native prerequisite is not a non-empty PE image: {native_binary} ({len(data)} bytes).")
    return native_binary


def _metadata_value(root: ElementTree.Element, local_name: str) -> str:
    element = next(
        (item for item in root.iter() if item.tag.rsplit("}", 1)[-1] == local_name),
        None,
    )
    if element is None or not (element.text or "").strip():
        raise AssertionError(f"Package metadata is missing {local_name}")
    return (element.text or "").strip()


class AlgorithmPackageContractTests(unittest.TestCase):
    def test_native_msbuild_discovery_includes_prerelease_visual_studio(self) -> None:
        command = _vswhere_command(Path("vswhere.exe"))
        self.assertIn("-prerelease", command)
        self.assertIn("Microsoft.VisualStudio.Component.VC.Tools.x86.x64", command)

    def test_msbuild_exe_path_is_not_inherited_by_dotnet_subprocesses(self) -> None:
        completed = mock.Mock(returncode=0, stdout="", stderr="")
        with mock.patch.dict(os.environ, {"MSBUILD_EXE_PATH": r"C:\VS\MSBuild.exe"}, clear=False):
            with mock.patch("Scripts.tests.test_algorithm_package_contract.subprocess.run", return_value=completed) as run:
                _run("dotnet", "--info")

        environment = run.call_args.kwargs["env"]
        self.assertNotIn("MSBUILD_EXE_PATH", environment)

    def test_build_solution_includes_algorithms_as_a_release_x64_project(self) -> None:
        solution = BUILD_SOLUTION.read_text(encoding="utf-8-sig")
        self.assertIn('"UI\\ColorVision.Algorithms\\ColorVision.Algorithms.csproj"', solution)
        project_line = next(line for line in solution.splitlines() if 'ColorVision.Algorithms.csproj' in line)
        project_id = project_line.rsplit('"', 2)[1]
        self.assertIn(f"{project_id}.Release|x64.ActiveCfg = Release|x64", solution)
        self.assertIn(f"{project_id}.Release|x64.Build.0 = Release|x64", solution)

    def test_algorithms_package_has_stable_identity_dual_targets_and_readme(self) -> None:
        self.assertEqual(["ColorVision.Algorithms"], _values(ALGORITHMS_PROJECT, "PackageId"))
        self.assertEqual(["net8.0;net10.0"], _values(ALGORITHMS_PROJECT, "TargetFrameworks"))
        self.assertEqual(["true"], [value.casefold() for value in _values(ALGORITHMS_PROJECT, "GeneratePackageOnBuild")])
        self.assertEqual(["README.md"], _values(ALGORITHMS_PROJECT, "PackageReadmeFile"))

        readme_items = [
            element
            for element in _elements(ALGORITHMS_PROJECT, "None")
            if (element.attrib.get("Include") or "").replace("\\", "/").casefold() == "readme.md"
        ]
        self.assertEqual(1, len(readme_items))
        self.assertEqual("true", readme_items[0].attrib.get("Pack", "").casefold())
        self.assertTrue((ALGORITHMS_PROJECT.parent / "README.md").is_file())

    def test_image_editor_package_declares_algorithms_project_dependency(self) -> None:
        references = [
            (element.attrib.get("Include") or "").replace("\\", "/").casefold()
            for element in _elements(IMAGE_EDITOR_PROJECT, "ProjectReference")
        ]
        self.assertIn("../colorvision.algorithms/colorvision.algorithms.csproj", references)

    def test_publish_workflow_orders_algorithms_before_image_editor(self) -> None:
        workflow = _publish_workflow_steps()["Publish to NuGet"]
        algorithms_push = "UI\\ColorVision.Algorithms\\bin\\x64\\Release\\*.nupkg"
        image_editor_push = "UI\\ColorVision.ImageEditor\\bin\\x64\\Release\\*.nupkg"
        self.assertIn(algorithms_push, workflow)
        self.assertIn(image_editor_push, workflow)
        self.assertLess(workflow.index(algorithms_push), workflow.index(image_editor_push))

    def test_publish_is_release_only_preflighted_and_never_skips_duplicates(self) -> None:
        workflow = PUBLISH_WORKFLOW.read_text(encoding="utf-8")
        self.assertIn("release:\n    types: [published]", workflow)
        self.assertNotIn("if: github.event_name == 'push'", workflow)
        self.assertIn("if: github.event_name == 'release'", workflow)
        preflight = "python Scripts/verify_nuget_package_versions.py"
        first_push = workflow.index("dotnet nuget push")
        self.assertIn(preflight, workflow)
        self.assertLess(workflow.index(preflight), first_push)
        self.assertNotIn("--skip-duplicate", workflow)

    def test_scoped_release_preserves_all_verification_and_the_default_package_batch(self) -> None:
        workflow = PUBLISH_WORKFLOW.read_text(encoding="utf-8")
        steps = _publish_workflow_steps()
        scope = "startsWith(github.event.release.tag_name, 'algorithms-v')"
        for name in ("Verify NuGet package versions are unused", "Publish to NuGet"):
            self.assertIn(f"if: github.event_name == 'release' && !{scope}", steps[name])
        for name in ("Verify scoped Algorithms release", "Publish scoped Algorithms package to NuGet"):
            self.assertIn(f"if: github.event_name == 'release' && {scope}", steps[name])
            for prerequisite in (
                "Build solution with MSBuild", "Run Python tests", "Run UI tests",
                "Run UI performance probes in an isolated process", "Run Copilot tests",
                "Run Spectrum tests", "Run Conoscope tests", "Run ProjectARVRPro tests",
                "Run ProjectKB tests", "Run ProjectLUX tests",
            ):
                self.assertNotIn("if:", steps[prerequisite])
                self.assertLess(workflow.index(f"- name: {prerequisite}"), workflow.index(f"- name: {name}"))

        batch = steps["Publish to NuGet"]
        self.assertEqual(15, batch.count("dotnet nuget push"))
        scoped = steps["Publish scoped Algorithms package to NuGet"]
        self.assertEqual(1, scoped.count("dotnet nuget push"))
        self.assertNotIn("*.nupkg", scoped)
        self.assertIn("ALGORITHMS_PACKAGE: ${{ steps.algorithms_package.outputs.package_path }}", scoped)
        self.assertIn("dotnet nuget push $env:ALGORITHMS_PACKAGE --api-key $env:NUGET_API_KEY", scoped)
        self.assertIn("RELEASE_TAG: ${{ github.event.release.tag_name }}", steps["Verify scoped Algorithms release"])
        self.assertNotIn("--api-key ${{", workflow)
        self.assertLess(workflow.index("- name: Verify scoped Algorithms release"),
                        workflow.index("- name: Publish scoped Algorithms package to NuGet"))

    def test_scoped_release_validates_the_tag_and_exact_package_identity(self) -> None:
        source = _scoped_algorithms_preflight_source()
        cases = (
            ("algorithms-v1.5.8", "ColorVision.Algorithms", "1.5.8", True),
            ("algorithms-v1.5.8.1", "ColorVision.Algorithms", "1.5.8.1", True),
            ("algorithms-v1.5.8-preview.1", "ColorVision.Algorithms", "1.5.8-preview.1", True),
            ("algorithms-v1.5.8.0", "ColorVision.Algorithms", "1.5.8", False),
            ("algorithms-v01.5.8", "ColorVision.Algorithms", "1.5.8", False),
            ("algorithms-v../../other", "ColorVision.Algorithms", "1.5.8", False),
            ("v1.5.8", "ColorVision.Algorithms", "1.5.8", False),
            ("algorithms-v1.5.8", "ColorVision.ImageEditor", "1.5.8", False),
            ("algorithms-v1.5.8", "ColorVision.Algorithms", "1.5.9", False),
        )
        for tag, package_id, version, valid in cases:
            with self.subTest(tag=tag, package_id=package_id, version=version):
                with tempfile.TemporaryDirectory(prefix="algorithms-release-scope-") as directory:
                    output = Path(directory) / "github-output"
                    identity = mock.Mock(package_id=package_id, version=version)
                    with mock.patch.dict(os.environ, {"RELEASE_TAG": tag, "GITHUB_OUTPUT": str(output)}):
                        with mock.patch("Scripts.verify_nuget_package_versions.read_package_identity", return_value=identity) as read:
                            with mock.patch("Scripts.verify_nuget_package_versions.main", return_value=0) as preflight:
                                if valid:
                                    exec(compile(source, str(PUBLISH_WORKFLOW), "exec"), {})
                                    expected = Path("UI/ColorVision.Algorithms/bin/x64/Release") / f"ColorVision.Algorithms.{version}.nupkg"
                                    read.assert_called_once_with(expected)
                                    preflight.assert_called_once_with([str(expected)])
                                    self.assertEqual(f"package_path={expected.as_posix()}\n", output.read_text(encoding="utf-8"))
                                else:
                                    with self.assertRaises(ValueError):
                                        exec(compile(source, str(PUBLISH_WORKFLOW), "exec"), {})
                                    preflight.assert_not_called()
                                    self.assertFalse(output.exists())

    def test_scoped_release_never_exports_a_publish_path_when_version_preflight_fails(self) -> None:
        source = _scoped_algorithms_preflight_source()
        for result in (1, 2):
            with self.subTest(preflight_exit_code=result):
                with tempfile.TemporaryDirectory(prefix="algorithms-release-preflight-") as directory:
                    output = Path(directory) / "github-output"
                    identity = mock.Mock(package_id="ColorVision.Algorithms", version="1.5.8")
                    with mock.patch.dict(os.environ, {"RELEASE_TAG": "algorithms-v1.5.8", "GITHUB_OUTPUT": str(output)}):
                        with mock.patch("Scripts.verify_nuget_package_versions.read_package_identity", return_value=identity):
                            with mock.patch("Scripts.verify_nuget_package_versions.main", return_value=result):
                                with self.assertRaises(SystemExit) as raised:
                                    exec(compile(source, str(PUBLISH_WORKFLOW), "exec"), {})
                                self.assertEqual(result, raised.exception.code)
                                self.assertFalse(output.exists())

    def test_solution_restore_uses_the_same_release_x64_dimensions_as_build(self) -> None:
        workflow = PUBLISH_WORKFLOW.read_text(encoding="utf-8")
        self.assertIn(
            "dotnet restore build.sln -p:Configuration=Release -p:Platform=x64",
            workflow,
        )
        self.assertIn("msbuild build.sln /p:Configuration=Release /p:Platform=x64", workflow)

    def test_each_no_restore_test_has_a_matching_release_x64_restore(self) -> None:
        workflow = PUBLISH_WORKFLOW.read_text(encoding="utf-8")
        restore_commands: dict[str, str] = {}
        test_commands: dict[str, str] = {}
        for raw_line in workflow.splitlines():
            line = raw_line.strip()
            if line.startswith("run: "):
                line = line.removeprefix("run: ")
            restore = re.fullmatch(r"dotnet restore (?P<project>\S+\.csproj)(?P<arguments>.*)", line)
            if restore:
                restore_commands[restore.group("project").replace("\\", "/")] = restore.group("arguments")
            test = re.fullmatch(r"dotnet test (?P<project>\S+\.csproj)(?P<arguments>.*)", line)
            if test and "--no-restore" in test.group("arguments"):
                test_commands[test.group("project").replace("\\", "/")] = test.group("arguments")

        self.assertEqual(7, len(test_commands), "Keep this assertion in sync when CI test projects change.")
        self.assertEqual(set(test_commands), set(restore_commands))
        for project, arguments in restore_commands.items():
            with self.subTest(project=project, command="restore"):
                self.assertIn("-p:Configuration=Release", arguments)
                self.assertIn("-p:Platform=x64", arguments)
        for project, arguments in test_commands.items():
            with self.subTest(project=project, command="test"):
                self.assertIn("-c Release", arguments)
                self.assertIn("-p:Platform=x64", arguments)
                self.assertIn("--no-restore", arguments)

    def test_ci_prepares_visual_studio_native_build_before_package_contract(self) -> None:
        workflow = PUBLISH_WORKFLOW.read_text(encoding="utf-8")
        setup = workflow.index("microsoft/setup-msbuild")
        native_build = workflow.index("msbuild build.sln /p:Configuration=Release /p:Platform=x64")
        python_tests = workflow.index('python -m unittest discover -s Scripts/tests -p "test_*.py" -v')
        publish_preflight = workflow.index("python Scripts/verify_nuget_package_versions.py")
        self.assertLess(setup, native_build)
        self.assertLess(native_build, python_tests)
        self.assertLess(python_tests, publish_preflight)

    def test_algorithms_and_image_editor_inherit_one_ui_package_version(self) -> None:
        versions = _values(UI_VERSION_PROPS, "VersionPrefix")
        self.assertEqual(1, len(versions))
        self.assertEqual([], _values(ALGORITHMS_PROJECT, "PackageVersion"))
        self.assertEqual([], _values(IMAGE_EDITOR_PROJECT, "PackageVersion"))

    def test_clean_build_packages_are_dual_target_signed_and_consumable(self) -> None:
        with tempfile.TemporaryDirectory(prefix="colorvision-algorithm-package-") as directory:
            root = Path(directory)
            algorithms_packages = root / "algorithms-packages"
            editor_packages = root / "editor-packages"
            native_binary = _build_isolated_opencv_helper(root)

            _run(
                "dotnet",
                "build",
                str(ALGORITHMS_PROJECT),
                "-c",
                "Release",
                "-p:Platform=x64",
                "--artifacts-path",
                str(root / "algorithms-artifacts"),
                f"-p:PackageOutputPath={algorithms_packages}",
                "--verbosity",
                "minimal",
            )
            _run(
                "dotnet",
                "pack",
                str(IMAGE_EDITOR_PROJECT),
                "-c",
                "Release",
                "-p:Platform=x64",
                "-p:UseProjectReference=false",
                f"-p:OpenCvHelperBinary={native_binary}",
                "--artifacts-path",
                str(root / "editor-artifacts"),
                "--output",
                str(editor_packages),
                "-p:GeneratePackageOnBuild=false",
                "--verbosity",
                "minimal",
            )

            algorithm_nupkgs = list(algorithms_packages.glob("ColorVision.Algorithms.*.nupkg"))
            algorithm_snupkgs = list(algorithms_packages.glob("ColorVision.Algorithms.*.snupkg"))
            editor_nupkgs = list(editor_packages.glob("ColorVision.ImageEditor.*.nupkg"))
            self.assertEqual(1, len(algorithm_nupkgs))
            self.assertEqual(1, len(algorithm_snupkgs))
            self.assertEqual(1, len(editor_nupkgs))

            algorithms_metadata, algorithm_entries = _package_metadata(algorithm_nupkgs[0])
            self.assertEqual("ColorVision.Algorithms", _metadata_value(algorithms_metadata, "id"))
            algorithm_version = _metadata_value(algorithms_metadata, "version")
            self.assertIn("lib/net8.0/ColorVision.Algorithms.dll", algorithm_entries)
            self.assertIn("lib/net10.0/ColorVision.Algorithms.dll", algorithm_entries)
            self.assertIn("README.md", algorithm_entries)

            editor_metadata, editor_entries = _package_metadata(editor_nupkgs[0])
            dependencies = [
                item
                for item in editor_metadata.iter()
                if item.tag.rsplit("}", 1)[-1] == "dependency"
                and item.attrib.get("id") == "ColorVision.Algorithms"
            ]
            self.assertEqual(1, len(dependencies))
            self.assertEqual(algorithm_version, dependencies[0].attrib.get("version"))
            self.assertNotIn("runtimes/win-x64/native/opencv_helper.dll", editor_entries)

            consumer = root / "consumer"
            consumer.mkdir()
            package_source = algorithms_packages.as_posix()
            (consumer / "Consumer.csproj").write_text(
                f"""<Project Sdk=\"Microsoft.NET.Sdk\">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <Nullable>enable</Nullable>
    <PlatformTarget>x64</PlatformTarget>
    <RestoreSources>{package_source}</RestoreSources>
    <RestorePackagesPath>{(root / "consumer-packages").as_posix()}</RestorePackagesPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=\"ColorVision.Algorithms\" Version=\"{algorithm_version}\" />
  </ItemGroup>
</Project>
""",
                encoding="utf-8",
            )
            (consumer / "Program.cs").write_text(
                '''using System;
using System.Collections.Generic;
using System.Text.Json;
using ColorVision.Algorithms;

static AlgorithmRunner CompileLegacyNullConstructorCall()
    => new AlgorithmRunner(null!, null!, null!);
_ = (Func<AlgorithmRunner>)CompileLegacyNullConstructorCall;

byte[]? token = typeof(AlgorithmId).Assembly.GetName().GetPublicKeyToken();
if (token is null || token.Length == 0)
{
    return 2;
}

AlgorithmParameterPreset source = AlgorithmParameterPreset.Create(
    "package-roundtrip",
    new AlgorithmId("package.invert"),
    new AlgorithmVersion(1, 2, 3),
    new PackageParameters { Amount = 12.5 },
    new Dictionary<string, string> { ["source"] = "package-test" });
string json = JsonSerializer.Serialize(source, AlgorithmJson.Options);
AlgorithmParameterPreset? restored = JsonSerializer.Deserialize<AlgorithmParameterPreset>(json, AlgorithmJson.Options);
if (restored is null || !restored.Validate().IsValid || restored.ToInvocation().Metadata["source"] != "package-test")
{
    return 3;
}

AlgorithmParameterPreset missingVersionAndMetadata = JsonSerializer.Deserialize<AlgorithmParameterPreset>("""
    {"schema":"colorvision.algorithm-parameter-preset/v1","presetId":"bad","algorithmId":"package.invert","parameterSchemaVersion":1,"parameters":{},"metadata":null}
    """, AlgorithmJson.Options)!;
if (missingVersionAndMetadata.Validate().IsValid)
{
    return 4;
}
try
{
    _ = missingVersionAndMetadata.ToInvocation();
    return 5;
}
catch (InvalidOperationException)
{
}
catch (ArgumentNullException)
{
    return 6;
}

AlgorithmParameterPreset scalarParameters = JsonSerializer.Deserialize<AlgorithmParameterPreset>("""
    {"schema":"colorvision.algorithm-parameter-preset/v1","presetId":"bad","algorithmId":"package.invert","algorithmVersion":"1.0.0","parameterSchemaVersion":1,"parameters":"not-an-object","metadata":{}}
    """, AlgorithmJson.Options)!;
if (scalarParameters.Validate().IsValid)
{
    return 7;
}

Console.WriteLine(Convert.ToHexString(token).ToLowerInvariant());
return 0;

sealed record PackageParameters : IAlgorithmParameters
{
    public int SchemaVersion => 1;
    public double Amount { get; init; }
    public AlgorithmValidationResult Validate() => AlgorithmValidationResult.Valid();
}
''',
                encoding="utf-8",
            )

            _run("dotnet", "restore", "Consumer.csproj", "--verbosity", "minimal", cwd=consumer)
            _run(
                "dotnet",
                "build",
                "Consumer.csproj",
                "-c",
                "Release",
                "--no-restore",
                "--verbosity",
                "minimal",
                cwd=consumer,
            )
            tokens = []
            for framework in ("net8.0", "net10.0"):
                token = _run(
                    "dotnet",
                    "run",
                    "--project",
                    "Consumer.csproj",
                    "-c",
                    "Release",
                    "-f",
                    framework,
                    "--no-build",
                    cwd=consumer,
                ).strip()
                self.assertEqual(16, len(token), f"Unexpected strong-name token for {framework}: {token}")
                tokens.append(token)
            self.assertEqual(tokens[0], tokens[1])


if __name__ == "__main__":
    unittest.main()

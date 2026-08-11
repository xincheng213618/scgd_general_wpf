import argparse
import re
import struct
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree


X64_ONLY_PROPS = (
    Path("Directory.Build.props"),
    Path("Plugins/Directory.Build.props"),
)
HOST_SOLUTIONS = (
    Path("build.sln"),
    Path("scgd_general_wpf.sln"),
    Path("UI/UI.sln"),
)
HOST_SOLUTION_CONFIGURATIONS = frozenset({
    "Debug|Any CPU",
    "Debug|x64",
    "Debug|x86",
    "Release|Any CPU",
    "Release|x64",
    "Release|x86",
})
PLATFORM_POLICY_TARGETS = Path("ColorVision.PlatformPolicy.targets")
PLATFORM_POLICY_IMPORTS = {
    Path("Directory.Build.targets"): "$(MSBuildThisFileDirectory)ColorVision.PlatformPolicy.targets",
    Path("Plugins/Directory.Build.targets"): "$(MSBuildThisFileDirectory)..\\ColorVision.PlatformPolicy.targets",
    Path("Projects/Directory.Build.targets"): "$(MSBuildThisFileDirectory)..\\ColorVision.PlatformPolicy.targets",
}
X64_NATIVE_PACKAGE_PROJECTS = (
    Path("UI/ColorVision.Core/ColorVision.Core.csproj"),
    Path("Engine/cvColorVision/cvColorVision.csproj"),
)
FILEIO_PROJECT = Path("Engine/ColorVision.FileIO/ColorVision.FileIO.csproj")
FILEIO_PACKAGE_ID = "ColorVision.FileIO"
FILEIO_PACKAGE_FRAMEWORKS = ("net10.0", "net8.0", "net6.0", "net461")
IMAGE_FILE_MACHINE_I386 = 0x014C
PE32_MAGIC = 0x010B
COMIMAGE_FLAGS_ILONLY = 0x00000001
COMIMAGE_FLAGS_32BITREQUIRED = 0x00000002
COMIMAGE_FLAGS_32BITPREFERRED = 0x00020000
PLATFORM_POLICY_INITIAL_TARGETS = (
    "ValidateColorVisionHostPlatform",
    "ValidateColorVisionFileIOAnyCpuPackage",
)


class PlatformPolicyError(RuntimeError):
    pass


def _read_xml(path: Path) -> ElementTree.Element:
    try:
        return ElementTree.parse(path).getroot()
    except (ElementTree.ParseError, OSError) as exc:
        raise PlatformPolicyError(f"Could not read project policy file {path}: {exc}") from exc


def _elements_by_local_name(root: ElementTree.Element, local_name: str):
    for element in root.iter():
        if element.tag.rsplit("}", 1)[-1] == local_name:
            yield element


def _normalized_condition(value: str) -> str:
    return re.sub(r"\s+", "", value).casefold()


def _validate_policy_initial_targets(root: ElementTree.Element, path: Path) -> None:
    initial_targets = tuple(
        value.strip()
        for value in (root.attrib.get("InitialTargets") or "").split(";")
        if value.strip()
    )
    if initial_targets != PLATFORM_POLICY_INITIAL_TARGETS:
        raise PlatformPolicyError(
            f"{path} must run both platform guards as initial targets; found {initial_targets}."
        )


def validate_x64_only_props(path: Path) -> None:
    root = _read_xml(path)
    values = [(element.text or "").strip() for element in _elements_by_local_name(root, "Platforms")]
    if values != ["x64"]:
        raise PlatformPolicyError(f"{path} must declare exactly <Platforms>x64</Platforms>; found {values}.")


def validate_host_solution(path: Path) -> None:
    try:
        text = path.read_text(encoding="utf-8-sig")
    except OSError as exc:
        raise PlatformPolicyError(f"Could not read host solution {path}: {exc}") from exc
    if re.search(r"\bARM64\b", text, flags=re.IGNORECASE):
        raise PlatformPolicyError(f"{path} exposes an unsupported ARM64 solution configuration.")
    section = re.search(
        r"GlobalSection\(SolutionConfigurationPlatforms\)\s*=\s*preSolution(?P<body>.*?)EndGlobalSection",
        text,
        flags=re.DOTALL,
    )
    if not section:
        raise PlatformPolicyError(f"{path} does not contain solution platform configurations.")
    configurations = frozenset(
        line.split("=", 1)[0].strip()
        for line in section.group("body").splitlines()
        if "=" in line
    )
    if configurations != HOST_SOLUTION_CONFIGURATIONS:
        raise PlatformPolicyError(
            f"{path} solution configuration aliases drifted: "
            f"expected={sorted(HOST_SOLUTION_CONFIGURATIONS)}, actual={sorted(configurations)}."
        )


def validate_native_project(path: Path) -> None:
    root = _read_xml(path)
    configurations = {
        (element.attrib.get("Include") or "").strip()
        for element in _elements_by_local_name(root, "ProjectConfiguration")
    }
    if "Release|x64" not in configurations:
        raise PlatformPolicyError(f"{path} does not provide the supported Release|x64 configuration.")
    unsupported = sorted(value for value in configurations if value.casefold().endswith("|arm64"))
    if unsupported:
        raise PlatformPolicyError(f"{path} contains unvalidated ARM64 configurations: {unsupported}.")


def validate_main_application(path: Path) -> None:
    root = _read_xml(path)
    targets = [(element.text or "").strip() for element in _elements_by_local_name(root, "PlatformTarget")]
    if targets != ["x64"]:
        raise PlatformPolicyError(f"{path} must target x64; found PlatformTarget values {targets}.")


def validate_arm64_build_guard(path: Path) -> None:
    root = _read_xml(path)
    _validate_policy_initial_targets(root, path)
    targets = [
        element
        for element in _elements_by_local_name(root, "Target")
        if element.attrib.get("Name") == "ValidateColorVisionHostPlatform"
    ]
    if len(targets) != 1:
        raise PlatformPolicyError(f"{path} must define the ColorVision host platform guard.")
    before_targets = {
        value.strip().casefold()
        for value in (targets[0].attrib.get("BeforeTargets") or "").split(";")
        if value.strip()
    }
    if before_targets != {"prepareforbuild", "pack"}:
        raise PlatformPolicyError(f"{path} host guard must run before PrepareForBuild and Pack.")
    expected_condition = _normalized_condition(
        "$([System.String]::Copy('$(Platform);$(PlatformTarget);$(RuntimeIdentifier);"
        "$(RuntimeIdentifiers)').ToLowerInvariant().Contains('arm64'))"
    )
    condition = _normalized_condition(targets[0].attrib.get("Condition") or "")
    if condition != expected_condition:
        raise PlatformPolicyError(f"{path} does not fail closed for every ARM64 architecture property.")
    if len(list(_elements_by_local_name(targets[0], "Error"))) != 1:
        raise PlatformPolicyError(f"{path} platform guard does not stop unsupported builds.")


def validate_fileio_anycpu_build_guard(path: Path) -> None:
    root = _read_xml(path)
    _validate_policy_initial_targets(root, path)
    targets = [
        element
        for element in _elements_by_local_name(root, "Target")
        if element.attrib.get("Name") == "ValidateColorVisionFileIOAnyCpuPackage"
    ]
    if len(targets) != 1:
        raise PlatformPolicyError(f"{path} must define the FileIO AnyCPU package guard.")
    before_targets = {
        value.strip().casefold()
        for value in (targets[0].attrib.get("BeforeTargets") or "").split(";")
        if value.strip()
    }
    if before_targets != {"prepareforbuild", "pack"}:
        raise PlatformPolicyError(f"{path} FileIO guard must run before PrepareForBuild and Pack.")
    expected_condition = _normalized_condition(
        "'$(MSBuildProjectName)' == 'ColorVision.FileIO' and "
        "('$(PlatformTarget)' != 'AnyCPU' or '$(RuntimeIdentifier)' != '' "
        "or '$(RuntimeIdentifiers)' != '')"
    )
    condition = _normalized_condition(targets[0].attrib.get("Condition") or "")
    if condition != expected_condition:
        raise PlatformPolicyError(f"{path} FileIO guard does not fail closed for architecture overrides.")
    if len(list(_elements_by_local_name(targets[0], "Error"))) != 1:
        raise PlatformPolicyError(f"{path} FileIO guard does not stop invalid package builds.")


def validate_platform_policy_import(path: Path, expected_project: str) -> None:
    root = _read_xml(path)
    imports = list(_elements_by_local_name(root, "Import"))
    import_projects = [
        (element.attrib.get("Project") or "").replace("/", "\\").casefold()
        for element in imports
    ]
    normalized_expected = expected_project.replace("/", "\\").casefold()
    matching = [
        element
        for element, project in zip(imports, import_projects, strict=True)
        if project == normalized_expected
    ]
    if len(matching) != 1 or (matching[0].attrib.get("Condition") or "").strip():
        raise PlatformPolicyError(
            f"{path} must import {expected_project!r} exactly once and unconditionally; "
            f"found {import_projects}."
        )


def validate_no_arm64_project_declarations(repository_root: Path) -> None:
    declared_projects: set[Path] = set()
    for path in repository_root.rglob("*.csproj"):
        if any(part.casefold() in {"bin", "obj"} for part in path.parts):
            continue
        root = _read_xml(path)
        architecture_values = {
            value.strip().casefold()
            for property_name in (
                "Platforms",
                "PlatformTarget",
                "RuntimeIdentifier",
                "RuntimeIdentifiers",
            )
            for element in _elements_by_local_name(root, property_name)
            for value in (element.text or "").split(";")
            if value.strip()
        }
        if not any("arm64" in value for value in architecture_values):
            continue
        relative_path = path.relative_to(repository_root)
        declared_projects.add(relative_path)
    if declared_projects:
        raise PlatformPolicyError(
            "Projects must not advertise unsupported ARM64 platforms: "
            f"{sorted(map(str, declared_projects))}."
        )


def validate_fileio_anycpu_project(path: Path) -> str:
    root = _read_xml(path)

    def values(name: str) -> list[str]:
        return [(element.text or "").strip() for element in _elements_by_local_name(root, name)]

    if values("Platforms") != ["AnyCPU"]:
        raise PlatformPolicyError(f"{path} must expose only the AnyCPU project platform.")
    if values("PlatformTarget") != ["AnyCPU"]:
        raise PlatformPolicyError(f"{path} must compile every package asset with PlatformTarget=AnyCPU.")
    if [value.casefold() for value in values("GeneratePackageOnBuild")] != ["true"]:
        raise PlatformPolicyError(f"{path} must keep its package-on-build behavior explicit.")
    if values("PackageId") != [FILEIO_PACKAGE_ID]:
        raise PlatformPolicyError(f"{path} must explicitly lock PackageId={FILEIO_PACKAGE_ID}.")
    versions = values("VersionPrefix")
    if len(versions) != 1 or not re.fullmatch(r"\d+(?:\.\d+){2,3}", versions[0]):
        raise PlatformPolicyError(f"{path} must declare one numeric VersionPrefix; found {versions}.")
    forbidden = [
        name
        for name in ("AllowStandaloneArm64Build", "RuntimeIdentifier", "RuntimeIdentifiers")
        if values(name)
    ]
    if forbidden:
        raise PlatformPolicyError(f"{path} contains architecture-specific package properties: {forbidden}.")
    return versions[0]


def _read_anycpu_dotnet_pe(data: bytes, context: str) -> tuple[int, int, int, int]:
    def unpack(format_string: str, offset: int, description: str):
        size = struct.calcsize(format_string)
        if offset < 0 or offset + size > len(data):
            raise PlatformPolicyError(f"Truncated PE {context} while reading {description}.")
        return struct.unpack_from(format_string, data, offset)

    if len(data) < 64 or data[:2] != b"MZ":
        raise PlatformPolicyError(f"{context} is not a PE assembly.")
    (pe_offset,) = unpack("<I", 0x3C, "DOS header")
    if pe_offset + 24 > len(data) or data[pe_offset:pe_offset + 4] != b"PE\0\0":
        raise PlatformPolicyError(f"{context} is missing the PE signature.")
    file_header = pe_offset + 4
    machine, section_count, _, _, _, optional_size, _ = unpack(
        "<HHIIIHH", file_header, "COFF header"
    )
    optional_header = file_header + 20
    (magic,) = unpack("<H", optional_header, "optional header")
    if magic == 0x10B:
        data_directories_offset = 96
    elif magic == 0x20B:
        data_directories_offset = 112
    else:
        raise PlatformPolicyError(f"{context} has unsupported PE magic 0x{magic:04X}.")
    if machine != IMAGE_FILE_MACHINE_I386 or magic != PE32_MAGIC:
        return machine, magic, 0, -1
    cli_directory_offset = data_directories_offset + 14 * 8
    if optional_size < cli_directory_offset + 8:
        raise PlatformPolicyError(f"{context} has no CLR data directory.")
    cli_rva, cli_size = unpack(
        "<II", optional_header + cli_directory_offset, "CLR data directory"
    )
    if cli_rva == 0 or cli_size < 20:
        raise PlatformPolicyError(f"{context} is not a managed CLR assembly.")

    (size_of_headers,) = unpack("<I", optional_header + 60, "SizeOfHeaders")
    section_table = optional_header + optional_size
    sections = []
    for index in range(section_count):
        offset = section_table + index * 40
        _, virtual_size, virtual_address, raw_size, raw_offset = unpack(
            "<8sIIII", offset, f"section {index}"
        )
        sections.append((virtual_address, max(virtual_size, raw_size), raw_offset, raw_size))

    def rva_to_offset(rva: int) -> int:
        if rva < size_of_headers:
            return rva
        for virtual_address, extent, raw_offset, raw_size in sections:
            if virtual_address <= rva < virtual_address + extent:
                delta = rva - virtual_address
                if delta >= raw_size:
                    break
                return raw_offset + delta
        raise PlatformPolicyError(f"{context} contains an unmapped CLR header RVA.")

    cli_offset = rva_to_offset(cli_rva)
    _, _, _, _, _, cor_flags = unpack("<IHHIII", cli_offset, "CLR header")
    return machine, magic, cor_flags, cli_offset + 16


def _looks_like_pe(data: bytes) -> bool:
    if len(data) < 64 or data[:2] != b"MZ":
        return False
    pe_offset = struct.unpack_from("<I", data, 0x3C)[0]
    return pe_offset <= len(data) - 4 and data[pe_offset:pe_offset + 4] == b"PE\0\0"


def validate_fileio_package(package_path: str | Path, expected_version: str) -> tuple[str, ...]:
    path = Path(package_path)
    expected_filename = f"{FILEIO_PACKAGE_ID}.{expected_version}.nupkg"
    if path.name.casefold() != expected_filename.casefold():
        raise PlatformPolicyError(
            f"FileIO package filename drifted: expected {expected_filename!r}, found {path.name!r}."
        )
    try:
        with zipfile.ZipFile(path) as archive:
            names = archive.namelist()
            normalized_name_list = [name.replace("\\", "/") for name in names]
            normalized_keys = [name.casefold() for name in normalized_name_list]
            if len(normalized_keys) != len(set(normalized_keys)):
                raise PlatformPolicyError(f"{path} contains duplicate or case-colliding ZIP members.")
            normalized_names = dict(zip(normalized_name_list, names, strict=True))

            nuspec_names = [
                normalized_names[name]
                for name in normalized_name_list
                if name.casefold().endswith(".nuspec")
            ]
            if len(nuspec_names) != 1:
                raise PlatformPolicyError(f"{path} must contain exactly one nuspec.")
            nuspec = ElementTree.fromstring(archive.read(nuspec_names[0]))
            package_ids = [
                (element.text or "").strip()
                for element in _elements_by_local_name(nuspec, "id")
            ]
            package_versions = [
                (element.text or "").strip()
                for element in _elements_by_local_name(nuspec, "version")
            ]
            if package_ids != [FILEIO_PACKAGE_ID] or package_versions != [expected_version]:
                raise PlatformPolicyError(
                    f"{path} package coordinates drifted: ids={package_ids}, versions={package_versions}."
                )
            if any(name.casefold().startswith("runtimes/") for name in normalized_names):
                raise PlatformPolicyError(f"{path} must not contain architecture-specific runtime assets.")

            expected_library_members = tuple(
                f"lib/{framework}/{FILEIO_PACKAGE_ID}.dll"
                for framework in FILEIO_PACKAGE_FRAMEWORKS
            )
            actual_pe_members = tuple(sorted(
                name
                for name, archive_name in normalized_names.items()
                if not name.endswith("/") and _looks_like_pe(archive.read(archive_name))
            ))
            if actual_pe_members != tuple(sorted(expected_library_members)):
                raise PlatformPolicyError(
                    f"{path} PE assets drifted: {actual_pe_members}."
                )

            verified: list[str] = []
            for member in expected_library_members:
                if member not in normalized_names:
                    raise PlatformPolicyError(f"{path} is missing {member}.")
                machine, magic, flags, _ = _read_anycpu_dotnet_pe(
                    archive.read(normalized_names[member]), f"{path}!{member}"
                )
                if machine != IMAGE_FILE_MACHINE_I386 or magic != PE32_MAGIC:
                    raise PlatformPolicyError(
                        f"{path}!{member} PE identity is machine=0x{machine:04X}, "
                        f"magic=0x{magic:04X}; AnyCPU requires I386 PE32 CLR format."
                    )
                if not flags & COMIMAGE_FLAGS_ILONLY or flags & (
                    COMIMAGE_FLAGS_32BITREQUIRED | COMIMAGE_FLAGS_32BITPREFERRED
                ):
                    raise PlatformPolicyError(
                        f"{path}!{member} CLR flags 0x{flags:08X} are architecture-bound."
                    )
                verified.append(member)
    except (OSError, zipfile.BadZipFile, ElementTree.ParseError) as exc:
        raise PlatformPolicyError(f"Could not validate FileIO package {path}: {exc}") from exc
    return tuple(verified)


def validate_fileio_package_directory(directory: str | Path, expected_version: str) -> Path:
    path = Path(directory)
    expected = path / f"{FILEIO_PACKAGE_ID}.{expected_version}.nupkg"
    packages = sorted(path.glob("*.nupkg"))
    if packages != [expected]:
        raise PlatformPolicyError(
            f"{path} must contain only the publishable package {expected.name!r}; found {packages}."
        )
    validate_fileio_package(expected, expected_version)
    return expected


def validate_native_package_project(path: Path) -> None:
    root = _read_xml(path)
    package_paths = [
        (element.text or "").replace("\\", "/").strip("/").casefold()
        for element in _elements_by_local_name(root, "PackagePath")
    ]
    if "runtimes/win-x64/native" not in package_paths:
        raise PlatformPolicyError(f"{path} does not package the supported win-x64 native runtime.")
    arm64_paths = sorted(value for value in package_paths if value.startswith("runtimes/win-arm64/"))
    if arm64_paths:
        raise PlatformPolicyError(f"{path} packages unvalidated win-arm64 native assets: {arm64_paths}.")


def validate_release_paths(repository_root: Path) -> None:
    required_tokens = {
        Path("Scripts/build.py"): '"/p:Platform=x64"',
        Path("Scripts/build_update.py"): "'bin', 'x64', 'Release'",
        Path("Scripts/package_cvxp.py"): '"-p:Platform=x64"',
        Path(".github/workflows/dotnet.yml"): "Platform=x64",
    }
    for relative_path, token in required_tokens.items():
        path = repository_root / relative_path
        try:
            text = path.read_text(encoding="utf-8-sig")
        except OSError as exc:
            raise PlatformPolicyError(f"Could not read release path {path}: {exc}") from exc
        if token not in text:
            raise PlatformPolicyError(f"{relative_path} no longer pins the supported x64 release path.")


def validate_platform_policy(repository_root: str | Path) -> tuple[Path, ...]:
    root = Path(repository_root).resolve()
    checked: list[Path] = []
    for relative_path in X64_ONLY_PROPS:
        path = root / relative_path
        validate_x64_only_props(path)
        checked.append(path)
    for relative_path in HOST_SOLUTIONS:
        path = root / relative_path
        validate_host_solution(path)
        checked.append(path)

    main_application = root / "ColorVision/ColorVision.csproj"
    validate_main_application(main_application)
    checked.append(main_application)

    build_guard = root / PLATFORM_POLICY_TARGETS
    validate_arm64_build_guard(build_guard)
    validate_fileio_anycpu_build_guard(build_guard)
    checked.append(build_guard)
    for relative_path, expected_project in PLATFORM_POLICY_IMPORTS.items():
        path = root / relative_path
        validate_platform_policy_import(path, expected_project)
        checked.append(path)
    validate_no_arm64_project_declarations(root)

    fileio_project = root / FILEIO_PROJECT
    validate_fileio_anycpu_project(fileio_project)
    checked.append(fileio_project)

    native_projects = sorted((root / "Native").rglob("*.vcxproj"))
    if not native_projects:
        raise PlatformPolicyError("No native projects were found under Native/.")
    for path in native_projects:
        validate_native_project(path)
        checked.append(path)

    for relative_path in X64_NATIVE_PACKAGE_PROJECTS:
        path = root / relative_path
        validate_native_package_project(path)
        checked.append(path)
    validate_release_paths(root)
    return tuple(checked)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Verify the x64 ColorVision release support policy.")
    parser.add_argument("--repository-root", type=Path, default=Path(__file__).resolve().parents[1])
    package_group = parser.add_mutually_exclusive_group()
    package_group.add_argument(
        "--fileio-package",
        type=Path,
        help="Also verify a generated ColorVision.FileIO nupkg coordinate and AnyCPU PE contract.",
    )
    package_group.add_argument(
        "--fileio-package-directory",
        type=Path,
        help="Verify that a release output contains exactly the expected AnyCPU FileIO nupkg.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        checked = validate_platform_policy(args.repository_root)
    except PlatformPolicyError as exc:
        print(f"Platform policy verification failed: {exc}", file=sys.stderr)
        return 1
    if args.fileio_package or args.fileio_package_directory:
        try:
            version = validate_fileio_anycpu_project(args.repository_root / FILEIO_PROJECT)
            if args.fileio_package:
                package_path = args.fileio_package
                verified_members = validate_fileio_package(package_path, version)
            else:
                package_path = validate_fileio_package_directory(
                    args.fileio_package_directory, version
                )
                verified_members = validate_fileio_package(package_path, version)
        except PlatformPolicyError as exc:
            print(f"Platform policy verification failed: {exc}", file=sys.stderr)
            return 1
        print(
            f"Verified {package_path} as {FILEIO_PACKAGE_ID} {version} AnyCPU across "
            f"{len(verified_members)} target frameworks."
        )
    print(f"Verified the x64 release policy across {len(checked)} project, solution, and policy files.")
    print("Any CPU and x86 solution entries are non-shipping developer aliases, not supported host targets.")
    print("ARM64 remains unsupported until native, package, CI, installer, and device validation are complete.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

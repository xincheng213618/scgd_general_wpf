import argparse
import re
import sys
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
X64_NATIVE_PACKAGE_PROJECTS = (
    Path("UI/ColorVision.Core/ColorVision.Core.csproj"),
    Path("Engine/cvColorVision/cvColorVision.csproj"),
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
    targets = [
        element
        for element in _elements_by_local_name(root, "Target")
        if element.attrib.get("Name") == "ValidateColorVisionHostPlatform"
    ]
    if len(targets) != 1:
        raise PlatformPolicyError(f"{path} must define the ColorVision host platform guard.")
    condition = (targets[0].attrib.get("Condition") or "").casefold()
    if "arm64" not in condition or "allowstandalonearm64build" not in condition:
        raise PlatformPolicyError(f"{path} does not fail closed for unapproved ARM64 builds.")
    if not list(_elements_by_local_name(targets[0], "Error")):
        raise PlatformPolicyError(f"{path} platform guard does not stop unsupported builds.")


def validate_standalone_arm64_exceptions(repository_root: Path) -> tuple[Path, ...]:
    allowed_projects = {Path("Engine/ColorVision.FileIO/ColorVision.FileIO.csproj")}
    declared_projects: set[Path] = set()
    for path in repository_root.rglob("*.csproj"):
        if any(part.casefold() in {"bin", "obj"} for part in path.parts):
            continue
        root = _read_xml(path)
        platforms = {
            value.strip().casefold()
            for element in _elements_by_local_name(root, "Platforms")
            for value in (element.text or "").split(";")
            if value.strip()
        }
        if "arm64" not in platforms:
            continue
        relative_path = path.relative_to(repository_root)
        declared_projects.add(relative_path)
        exceptions = [
            (element.text or "").strip().casefold()
            for element in _elements_by_local_name(root, "AllowStandaloneArm64Build")
        ]
        if exceptions != ["true"]:
            raise PlatformPolicyError(
                f"{relative_path} declares ARM64 without AllowStandaloneArm64Build=true."
            )
    if declared_projects != allowed_projects:
        raise PlatformPolicyError(
            "Standalone ARM64 project allowlist drift: "
            f"expected={sorted(map(str, allowed_projects))}, actual={sorted(map(str, declared_projects))}."
        )
    return tuple(repository_root / path for path in sorted(declared_projects))


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

    build_guard = root / "Directory.Build.targets"
    validate_arm64_build_guard(build_guard)
    checked.append(build_guard)
    checked.extend(validate_standalone_arm64_exceptions(root))

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
    parser = argparse.ArgumentParser(description="Verify the x64-only ColorVision host release policy.")
    parser.add_argument("--repository-root", type=Path, default=Path(__file__).resolve().parents[1])
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        checked = validate_platform_policy(args.repository_root)
    except PlatformPolicyError as exc:
        print(f"Platform policy verification failed: {exc}", file=sys.stderr)
        return 1
    print(f"Verified x64-only host policy across {len(checked)} project and solution files.")
    print("ARM64 remains unsupported until native, package, CI, installer, and device validation are complete.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

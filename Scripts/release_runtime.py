from collections.abc import Callable, Iterable
from pathlib import Path
from xml.etree import ElementTree


REQUIRED_MAIN_RUNTIME_FILES = (
    "Anthropic.dll",
    "HtmlAgilityPack.dll",
    "Microsoft.Agents.AI.Anthropic.dll",
    "ModelContextProtocol.Core.dll",
    "WpfMath.dll",
    "XamlMath.Shared.dll",
)


def find_missing_runtime_files(
    runtime_directory: str | Path,
    required_files: Iterable[str] = REQUIRED_MAIN_RUNTIME_FILES,
) -> list[str]:
    runtime_path = Path(runtime_directory)
    return sorted(
        file_name
        for file_name in required_files
        if not (runtime_path / file_name).is_file() or (runtime_path / file_name).stat().st_size == 0
    )


def find_missing_installer_sources(
    aip_path: str | Path,
    required_files: Iterable[str] = REQUIRED_MAIN_RUNTIME_FILES,
) -> list[str]:
    source_file_names = _read_installer_source_file_names(aip_path)
    return sorted(file_name for file_name in required_files if file_name.casefold() not in source_file_names)


def find_unlisted_installer_runtime_dlls(runtime_directory: str | Path, aip_path: str | Path) -> list[str]:
    runtime_path = Path(runtime_directory)
    source_file_names = _read_installer_source_file_names(aip_path)
    return sorted(
        file_path.name
        for file_path in runtime_path.glob("*.dll")
        if file_path.is_file() and file_path.stat().st_size > 0 and file_path.name.casefold() not in source_file_names
    )


def _read_installer_source_file_names(aip_path: str | Path) -> set[str]:
    project_path = Path(aip_path)
    if not project_path.is_file():
        return set()

    try:
        root = ElementTree.parse(project_path).getroot()
    except (ElementTree.ParseError, OSError):
        return set()

    return {
        source_path.replace("\\", "/").rsplit("/", 1)[-1].casefold()
        for element in root.iter()
        if (source_path := element.attrib.get("SourcePath"))
    }


def validate_release_runtime_payload(
    runtime_directory: str | Path,
    aip_path: str | Path | None = None,
    *,
    required_files: Iterable[str] = REQUIRED_MAIN_RUNTIME_FILES,
    report: Callable[[str], None] = print,
) -> bool:
    required = tuple(dict.fromkeys(required_files))
    missing_runtime = find_missing_runtime_files(runtime_directory, required)
    missing_installer = find_missing_installer_sources(aip_path, required) if aip_path is not None else []
    unlisted_runtime_dlls = find_unlisted_installer_runtime_dlls(runtime_directory, aip_path) if aip_path is not None else []
    missing_installer_names = {file_name.casefold() for file_name in missing_installer}
    unlisted_runtime_dlls = [
        file_name for file_name in unlisted_runtime_dlls if file_name.casefold() not in missing_installer_names
    ]

    if missing_runtime:
        report("Release runtime payload is missing: " + ", ".join(missing_runtime))
    if missing_installer:
        report("Advanced Installer payload is missing: " + ", ".join(missing_installer))
    if unlisted_runtime_dlls:
        report("Advanced Installer does not include runtime DLLs: " + ", ".join(unlisted_runtime_dlls))
    if missing_runtime or missing_installer or unlisted_runtime_dlls:
        return False

    report("Verified required release runtime payload: " + ", ".join(required))
    if aip_path is not None:
        report("Verified all root release runtime DLLs are included by Advanced Installer.")
    return True

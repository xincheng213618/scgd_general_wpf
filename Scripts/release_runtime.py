from collections.abc import Callable, Iterable
from pathlib import Path
from xml.etree import ElementTree


REQUIRED_MAIN_RUNTIME_FILES = (
    "HtmlAgilityPack.dll",
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
    project_path = Path(aip_path)
    if not project_path.is_file():
        return sorted(required_files)

    try:
        root = ElementTree.parse(project_path).getroot()
    except (ElementTree.ParseError, OSError):
        return sorted(required_files)

    source_file_names = {
        source_path.replace("\\", "/").rsplit("/", 1)[-1].casefold()
        for element in root.iter()
        if (source_path := element.attrib.get("SourcePath"))
    }
    return sorted(file_name for file_name in required_files if file_name.casefold() not in source_file_names)


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

    if missing_runtime:
        report("Release runtime payload is missing: " + ", ".join(missing_runtime))
    if missing_installer:
        report("Advanced Installer payload is missing: " + ", ".join(missing_installer))
    if missing_runtime or missing_installer:
        return False

    report("Verified required release runtime payload: " + ", ".join(required))
    return True

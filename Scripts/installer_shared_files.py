"""Conservatively identify host files installed at their original relative paths."""

import os
from pathlib import Path
from xml.etree import ElementTree


TABLE_PREFIX = "caphyon.advinst.msicomp."


def _unique_rows(root: ElementTree.Element, table: str, key: str) -> dict[str, ElementTree.Element]:
    rows: dict[str, ElementTree.Element] = {}
    duplicates: set[str] = set()
    for component in root.findall("COMPONENT"):
        if component.get("cid") != TABLE_PREFIX + table:
            continue
        for row in component.findall("ROW"):
            identifier = row.get(key)
            if not identifier:
                continue
            if identifier in rows:
                duplicates.add(identifier)
            rows[identifier] = row
    return {identifier: row for identifier, row in rows.items() if identifier not in duplicates}


def _long_name(value: str, *, allow_dot: bool = False) -> str | None:
    names = value.split("|")
    if len(names) > 2:
        return None
    for name in names:
        if allow_dot and name == ".":
            continue
        if not name or name in {".", ".."} or name.endswith((" ", ".")):
            return None
        if any(character in name for character in '\\/:*?"<>[]$%') or any(ord(character) < 32 for character in name):
            return None
    return names[-1]


def _directory_target_name(value: str) -> str | None:
    # MSI DefaultDir is target[:source], and each side may use short|long names.
    names = value.split(":")
    if len(names) > 2 or any(_long_name(name, allow_dot=True) is None for name in names):
        return None
    return _long_name(names[0], allow_dot=True)


def _relative_directory(directory_id: str | None, directories: dict[str, ElementTree.Element]) -> str | None:
    if "APPDIR" not in directories:
        return None
    visited: set[str] = set()
    parts: list[str] = []
    while directory_id != "APPDIR":
        if not directory_id or directory_id in visited:
            return None
        visited.add(directory_id)
        row = directories.get(directory_id)
        if row is None:
            return None
        name = _directory_target_name(row.get("DefaultDir", ""))
        if name is None:
            return None
        if name != ".":
            parts.append(name)
        directory_id = row.get("Directory_Parent")
    return "/".join(reversed(parts))


def collect_installer_shared_files(aip_path: str | Path, runtime_directory: str | Path) -> set[str]:
    """Return proven APPDIR-relative host files, skipping ambiguous AIP rows.

    Only literal, existing sources inside the host output qualify. Their source
    and installation-relative paths must match; renamed, relocated, conditional,
    macro-based, missing or ambiguous entries are not assumed to be shared.
    Unreadable/invalid XML and a missing host directory remain caller-visible
    errors so a publisher can fail closed instead of publishing stale metadata.
    """
    aip = Path(aip_path).resolve()
    runtime = Path(runtime_directory).resolve()
    if not runtime.is_dir():
        raise FileNotFoundError(f"Host output directory not found: {runtime}")
    root = ElementTree.parse(aip).getroot()
    directories = _unique_rows(root, "MsiDirsComponent", "Directory")
    components = _unique_rows(root, "MsiCompsComponent", "Component")
    files = _unique_rows(root, "MsiFilesComponent", "File")
    shared_files: set[str] = set()
    for row in files.values():
        component = components.get(row.get("Component_", ""))
        if component is None or component.get("Condition", "").strip():
            continue
        directory = _relative_directory(component.get("Directory_"), directories)
        filename = _long_name(row.get("FileName", ""))
        source_text = row.get("SourcePath", "")
        if directory is None or filename is None or not source_text or any(character in source_text for character in "[]$%") or any(ord(character) < 32 for character in source_text):
            continue
        source = Path(source_text.replace("\\", "/"))
        if not source.is_absolute():
            source = aip.parent / source
        # Reject external/UNC sources lexically before filesystem resolution.
        source = Path(os.path.abspath(source))
        if not source.is_relative_to(runtime):
            continue
        try:
            source = source.resolve()
            if not source.is_relative_to(runtime) or not source.is_file():
                continue
        except (OSError, RuntimeError, ValueError):
            continue
        source_relative = source.relative_to(runtime).as_posix()
        destination_relative = f"{directory}/{filename}" if directory else filename
        if source_relative.casefold() == destination_relative.casefold():
            shared_files.add(source_relative)
    return shared_files

from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from xml.etree import ElementTree


REQUIRED_SERVICE_HOST_RUNTIME_PATHS = (
    "ServiceHost/ColorVisionServiceHost.exe",
    "ServiceHost/ColorVisionServiceHost.dll",
    "ServiceHost/ColorVisionServiceHost.deps.json",
    "ServiceHost/ColorVisionServiceHost.runtimeconfig.json",
    "ServiceHost/Newtonsoft.Json.dll",
    "ServiceHost/System.ServiceProcess.ServiceController.dll",
    "ServiceHost/runtimes/win/lib/net10.0/System.ServiceProcess.ServiceController.dll",
    "ServiceHost/Tasks/RegisterFileAssociations.ps1",
    "ServiceHost/Tasks/RegisterThumbnail.ps1",
    "ServiceHost/Tasks/UnregisterThumbnail.ps1",
)


@dataclass(frozen=True)
class InstallerFileEntry:
    file_id: str
    component_id: str
    source_path: Path
    target_path: str


def validate_service_host_runtime(version_directory: str | Path) -> None:
    runtime_path = Path(version_directory)
    missing_paths = [
        relative_path
        for relative_path in REQUIRED_SERVICE_HOST_RUNTIME_PATHS
        if not runtime_path.joinpath(*PurePosixPath(relative_path).parts).is_file()
    ]
    if missing_paths:
        raise FileNotFoundError(
            "ServiceHost runtime is incomplete: " + ", ".join(missing_paths)
        )


def normalize_installer_path(path_value: str) -> str:
    return PurePosixPath(path_value.replace("\\", "/")).as_posix().strip("/")


def _long_name(msi_name: str) -> str:
    target_name = msi_name.split(":", 1)[0]
    return target_name.rsplit("|", 1)[-1]


def _index_unique_rows(rows, key_name: str, label: str) -> dict[str, ElementTree.Element]:
    result: dict[str, ElementTree.Element] = {}
    for row in rows:
        key = row.attrib[key_name]
        if key in result:
            raise ValueError(f"Advanced Installer contains duplicate {label} identifier: {key}")
        result[key] = row
    return result


def read_installer_file_entries(aip_path: str | Path) -> tuple[InstallerFileEntry, ...]:
    project_path = Path(aip_path).resolve()
    root = ElementTree.parse(project_path).getroot()
    project_root = (project_path.parent / root.attrib.get("RootPath", ".")).resolve()
    rows = [element for element in root.iter() if element.tag.rsplit("}", 1)[-1] == "ROW"]

    directory_rows = _index_unique_rows(
        [row for row in rows if "Directory" in row.attrib and "DefaultDir" in row.attrib],
        "Directory",
        "directory",
    )
    component_rows = _index_unique_rows(
        [row for row in rows if "Component" in row.attrib and "Directory_" in row.attrib],
        "Component",
        "component",
    )
    file_rows = _index_unique_rows(
        [row for row in rows if "File" in row.attrib and "Component_" in row.attrib and "SourcePath" in row.attrib],
        "File",
        "file",
    )

    def directory_parts(directory_id: str) -> tuple[str, ...]:
        parts: list[str] = []
        visited: set[str] = set()
        current = directory_id
        while current not in {"APPDIR", "TARGETDIR"}:
            if current in visited:
                raise ValueError(f"Advanced Installer directory cycle includes: {current}")
            visited.add(current)
            row = directory_rows.get(current)
            if row is None:
                raise ValueError(f"Advanced Installer component references unknown directory: {current}")
            name = _long_name(row.attrib["DefaultDir"])
            if name not in {"", "."}:
                parts.append(name)
            current = row.attrib.get("Directory_Parent", "")
        if current != "APPDIR":
            raise ValueError(f"Advanced Installer file is outside APPDIR: {directory_id}")
        return tuple(reversed(parts))

    entries: list[InstallerFileEntry] = []
    for file_id, file_row in file_rows.items():
        component_id = file_row.attrib["Component_"]
        component_row = component_rows.get(component_id)
        if component_row is None:
            raise ValueError(f"Advanced Installer file references unknown component: {file_id} -> {component_id}")
        target_parts = (*directory_parts(component_row.attrib["Directory_"]), _long_name(file_row.attrib["FileName"]))
        source_value = file_row.attrib["SourcePath"]
        source_path = Path(source_value)
        if not source_path.is_absolute():
            source_path = project_root / source_path
        entries.append(InstallerFileEntry(
            file_id=file_id,
            component_id=component_id,
            source_path=source_path.resolve(),
            target_path=normalize_installer_path("/".join(target_parts)),
        ))
    return tuple(entries)

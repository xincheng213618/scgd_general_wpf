import re
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
    feature_id: str
    build_name: str
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
    normalized_keys: dict[str, str] = {}
    for row in rows:
        key = row.attrib[key_name]
        normalized_key = key.casefold()
        if normalized_key in normalized_keys:
            raise ValueError(
                f"Advanced Installer contains duplicate/case-colliding {label} identifier: "
                f"{normalized_keys[normalized_key]} / {key}"
            )
        normalized_keys[normalized_key] = key
        result[key] = row
    return result


def _row_is_active_for_build(
    row: ElementTree.Element,
    build_name: str,
    known_builds: dict[str, str],
) -> bool:
    raw_builds = row.attrib.get("Builds")
    if raw_builds is None:
        return True

    build_tokens = [token.strip() for token in re.split(r"[;,]", raw_builds) if token.strip()]
    if not build_tokens:
        raise ValueError("Advanced Installer Builds filter is empty.")
    unknown_builds = [token for token in build_tokens if token.casefold() not in known_builds]
    if unknown_builds:
        raise ValueError(
            "Advanced Installer Builds filter references unknown build(s): "
            + ", ".join(unknown_builds)
        )
    return build_name.casefold() in {token.casefold() for token in build_tokens}


def read_installer_file_entries(
    aip_path: str | Path,
    *,
    build_name: str = "DefaultBuild",
) -> tuple[InstallerFileEntry, ...]:
    project_path = Path(aip_path).resolve()
    root = ElementTree.parse(project_path).getroot()
    project_root = (project_path.parent / root.attrib.get("RootPath", ".")).resolve()
    rows = [element for element in root.iter() if element.tag.rsplit("}", 1)[-1] == "ROW"]

    build_rows = _index_unique_rows(
        [row for row in rows if "BuildKey" in row.attrib and "BuildName" in row.attrib],
        "BuildKey",
        "build",
    )
    if not build_rows:
        raise ValueError("Advanced Installer project does not declare any build.")
    _index_unique_rows(build_rows.values(), "BuildName", "build name")
    known_builds = {key.casefold(): key for key in build_rows}
    active_build = known_builds.get(build_name.casefold())
    if active_build is None:
        raise ValueError(f"Advanced Installer project does not declare the required build: {build_name}")

    feature_rows = _index_unique_rows(
        [row for row in rows if "Feature" in row.attrib and "Level" in row.attrib],
        "Feature",
        "feature",
    )
    if not feature_rows:
        raise ValueError("Advanced Installer project does not declare any install feature.")

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

    feature_links = [row for row in rows if "Feature_" in row.attrib and "Component_" in row.attrib]
    seen_feature_links: dict[tuple[str, str], tuple[str, str]] = {}
    features_by_component: dict[str, list[ElementTree.Element]] = {}
    for row in feature_links:
        feature_id = row.attrib["Feature_"]
        component_id = row.attrib["Component_"]
        pair = (feature_id.casefold(), component_id.casefold())
        if pair in seen_feature_links:
            previous = seen_feature_links[pair]
            raise ValueError(
                "Advanced Installer contains duplicate/case-colliding feature membership: "
                f"{previous[0]} -> {previous[1]} / {feature_id} -> {component_id}"
            )
        if feature_id not in feature_rows:
            raise ValueError(f"Advanced Installer membership references unknown feature: {feature_id}")
        if component_id not in component_rows:
            raise ValueError(f"Advanced Installer membership references unknown component: {component_id}")
        seen_feature_links[pair] = (feature_id, component_id)
        features_by_component.setdefault(component_id, []).append(row)

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
            if not _row_is_active_for_build(row, active_build, known_builds):
                raise ValueError(
                    f"Advanced Installer active file references directory excluded from {active_build}: {current}"
                )
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
        memberships = features_by_component.get(component_id, [])
        if len(memberships) != 1:
            raise ValueError(
                "Advanced Installer file component must belong to exactly one install feature: "
                f"{component_id} (found {len(memberships)})"
            )
        membership = memberships[0]
        feature_id = membership.attrib["Feature_"]
        feature_row = feature_rows.get(feature_id)
        if feature_row is None:
            raise ValueError(
                f"Advanced Installer component references unknown feature: {component_id} -> {feature_id}"
            )
        active = all(
            _row_is_active_for_build(row, active_build, known_builds)
            for row in (file_row, component_row, membership, feature_row)
        )
        if not active:
            continue
        target_parts = (*directory_parts(component_row.attrib["Directory_"]), _long_name(file_row.attrib["FileName"]))
        source_value = file_row.attrib["SourcePath"]
        source_path = Path(source_value)
        if not source_path.is_absolute():
            source_path = project_root / source_path
        entries.append(InstallerFileEntry(
            file_id=file_id,
            component_id=component_id,
            feature_id=feature_id,
            build_name=active_build,
            source_path=source_path.resolve(),
            target_path=normalize_installer_path("/".join(target_parts)),
        ))
    return tuple(entries)

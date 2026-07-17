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


def read_installer_source_paths(aip_path: str | Path) -> set[str]:
    root = ElementTree.parse(aip_path).getroot()
    return {
        source_path.replace("\\", "/").casefold()
        for element in root.iter()
        if (source_path := element.attrib.get("SourcePath"))
    }


def installer_contains_relative_path(installer_sources: set[str], relative_path: str) -> bool:
    normalized = relative_path.replace("\\", "/").casefold()
    return any(source == normalized or source.endswith("/" + normalized) for source in installer_sources)

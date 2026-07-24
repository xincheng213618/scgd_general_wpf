import argparse
import hashlib
import os
import re
import shutil
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable
from xml.etree import ElementTree

try:
    from .backend_client import (
        DEFAULT_CONNECT_TIMEOUT,
        DEFAULT_READ_TIMEOUT,
        DEFAULT_UPLOAD_FOLDER,
        DEFAULT_UPLOAD_RETRIES,
        RemoteUploadSettings,
        fetch_latest_version as backend_fetch_latest_version,
        preflight_remote_upload as backend_preflight_remote_upload,
        resolve_upload_base_url,
        resolve_upload_credentials,
        upload_content as backend_upload_content,
        upload_file as backend_upload_file,
    )
    from .service_host_runtime import (
        REQUIRED_SERVICE_HOST_RUNTIME_PATHS,
        installer_contains_relative_path,
        read_installer_source_paths,
        validate_service_host_runtime,
    )
except ImportError:
    from backend_client import (
        DEFAULT_CONNECT_TIMEOUT,
        DEFAULT_READ_TIMEOUT,
        DEFAULT_UPLOAD_FOLDER,
        DEFAULT_UPLOAD_RETRIES,
        RemoteUploadSettings,
        fetch_latest_version as backend_fetch_latest_version,
        preflight_remote_upload as backend_preflight_remote_upload,
        resolve_upload_base_url,
        resolve_upload_credentials,
        upload_content as backend_upload_content,
        upload_file as backend_upload_file,
    )
    from service_host_runtime import (
        REQUIRED_SERVICE_HOST_RUNTIME_PATHS,
        installer_contains_relative_path,
        read_installer_source_paths,
        validate_service_host_runtime,
    )
from tqdm import tqdm

VERSION_RE = re.compile(r"(\d+\.\d+\.\d+\.\d+)")
INSTALLER_EXTENSIONS = {".exe", ".msi", ".zip", ".rar"}
DEFAULT_PROJECT_NAME = "ColorVision"

CRITICAL_RUNTIME_PROJECT_OUTPUTS = (
    ("Engine/ColorVision.Engine/bin/x64/Release/net10.0-windows/ColorVision.Engine.dll", "ColorVision.Engine.dll"),
    ("UI/ColorVision.Common/bin/x64/Release/net10.0-windows7.0/ColorVision.Common.dll", "ColorVision.Common.dll"),
    ("UI/ColorVision.UI/bin/x64/Release/net10.0-windows7.0/ColorVision.UI.dll", "ColorVision.UI.dll"),
    ("UI/ColorVision.Rbac/bin/x64/Release/net10.0-windows7.0/ColorVision.Rbac.dll", "ColorVision.Rbac.dll"),
    ("UI/ColorVision.Database/bin/x64/Release/net10.0-windows7.0/ColorVision.Database.dll", "ColorVision.Database.dll"),
    ("UI/ColorVision.Solution/bin/x64/Release/net10.0-windows7.0/ColorVision.Solution.dll", "ColorVision.Solution.dll"),
    ("UI/ColorVision.ImageEditor/bin/x64/Release/net10.0-windows7.0/ColorVision.ImageEditor.dll", "ColorVision.ImageEditor.dll"),
    ("UI/ColorVision.ImageTools/bin/x64/Release/net10.0-windows7.0/ColorVision.ImageTools.dll", "ColorVision.ImageTools.dll"),
    ("UI/ColorVision.Scheduler/bin/x64/Release/net10.0-windows7.0/ColorVision.Scheduler.dll", "ColorVision.Scheduler.dll"),
    ("UI/ColorVision.SocketProtocol/bin/x64/Release/net10.0-windows7.0/ColorVision.SocketProtocol.dll", "ColorVision.SocketProtocol.dll"),
    ("UI/ColorVision.UI.Desktop/bin/x64/Release/net10.0-windows7.0/ColorVision.UI.Desktop.dll", "ColorVision.UI.Desktop.dll"),
)


DEFAULT_READ_TIMEOUT = max(DEFAULT_READ_TIMEOUT, 1800)
DEFAULT_MSBUILD_CANDIDATES = (
    Path(r"C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe"),
    Path(r"C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe"),
    Path(r"C:\Program Files\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\amd64\MSBuild.exe"),
    Path(r"C:\Program Files\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\MSBuild.exe"),
)


@dataclass(frozen=True)
class ProjectConfig:
    name: str
    msbuild_path: Path
    solution_path: Path
    advanced_installer_path: Path
    aip_path: Path
    setup_files_dir: Path
    changelog_src: Path


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as file_handle:
        while chunk := file_handle.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def files_match(source: Path, destination: Path) -> bool:
    return (
        source.stat().st_size == destination.stat().st_size
        and sha256_file(source) == sha256_file(destination)
    )


def validate_runtime_copy_integrity(
    solution_root: str | Path,
    runtime_directory: str | Path,
    *,
    project_outputs: tuple[tuple[str, str], ...] = CRITICAL_RUNTIME_PROJECT_OUTPUTS,
    report: Callable[[str], None] = print,
) -> bool:
    solution_path = Path(solution_root)
    runtime_path = Path(runtime_directory)

    for project_relative_path, runtime_name in project_outputs:
        project_output = solution_path / project_relative_path
        runtime_output = runtime_path / runtime_name
        if not project_output.is_file() or not runtime_output.is_file():
            report(f"Runtime integrity input is missing: {project_output} -> {runtime_output}")
            return False
        if not files_match(project_output, runtime_output):
            report(f"Runtime DLL differs from its project output: {project_output} -> {runtime_output}")
            return False

    report(f"Verified {len(project_outputs)} runtime DLL copies against their project outputs.")
    return True


def ensure_runtime_copy_integrity(
    solution_root: str | Path,
    runtime_directory: str | Path,
    *,
    project_outputs: tuple[tuple[str, str], ...] = CRITICAL_RUNTIME_PROJECT_OUTPUTS,
    report: Callable[[str], None] = print,
) -> bool:
    solution_path = Path(solution_root)
    runtime_path = Path(runtime_directory)

    for project_relative_path, runtime_name in project_outputs:
        project_output = solution_path / project_relative_path
        runtime_output = runtime_path / runtime_name
        if not project_output.is_file():
            report(f"Runtime integrity project output is missing: {project_output}")
            return False
        if runtime_output.is_file() and files_match(project_output, runtime_output):
            continue

        try:
            runtime_output.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(project_output, runtime_output)
        except OSError as exc:
            report(f"Could not repair runtime DLL copy: {project_output} -> {runtime_output}: {exc}")
            return False

        if not runtime_output.is_file() or not files_match(project_output, runtime_output):
            report(f"Runtime DLL still differs after repair: {project_output} -> {runtime_output}")
            return False
        report(f"Repaired runtime DLL copy: {project_output} -> {runtime_output}")

    return validate_runtime_copy_integrity(
        solution_path,
        runtime_path,
        project_outputs=project_outputs,
        report=report,
    )


def validate_installer_runtime_dlls(
    runtime_directory: str | Path,
    aip_path: str | Path,
    *,
    report: Callable[[str], None] = print,
) -> bool:
    runtime_path = Path(runtime_directory)
    if not runtime_path.is_dir():
        report(f"Release runtime directory does not exist: {runtime_path}")
        return False

    try:
        installer_source_paths = read_installer_source_paths(aip_path)
        validate_service_host_runtime(runtime_path)
    except (ElementTree.ParseError, OSError) as exc:
        report(f"Could not validate Advanced Installer runtime: {exc}")
        return False

    installer_sources = {
        source_path.rsplit("/", 1)[-1]
        for source_path in installer_source_paths
    }
    missing_dlls = sorted(
        file_path.name
        for file_path in runtime_path.glob("*.dll")
        if file_path.is_file() and file_path.name.casefold() not in installer_sources
    )
    if missing_dlls:
        report("Advanced Installer does not include runtime DLLs: " + ", ".join(missing_dlls))
        return False

    missing_service_host_paths = [
        relative_path
        for relative_path in REQUIRED_SERVICE_HOST_RUNTIME_PATHS
        if not installer_contains_relative_path(installer_source_paths, relative_path)
    ]
    if missing_service_host_paths:
        report("Advanced Installer does not include ServiceHost runtime files: " + ", ".join(missing_service_host_paths))
        return False

    report("Verified root runtime DLLs and the complete ServiceHost runtime in Advanced Installer.")
    return True


def rebuild_project(msbuild_path: Path, solution_path: Path, advanced_installer_path: Path, aip_path: Path) -> bool:
    try:
        print(f"Running MSBuild: {msbuild_path} {solution_path}")
        subprocess.run(
            [str(msbuild_path), str(solution_path), "/p:Configuration=Release", "/p:Platform=x64"],
            check=True,
        )

        runtime_directory = solution_path.parent / "ColorVision" / "bin" / "x64" / "Release" / "net10.0-windows"
        if not ensure_runtime_copy_integrity(solution_path.parent, runtime_directory):
            return False
        if not validate_installer_runtime_dlls(runtime_directory, aip_path):
            return False

        print(f"Running Advanced Installer: {advanced_installer_path} /rebuild {aip_path}")
        subprocess.run([str(advanced_installer_path), "/rebuild", str(aip_path)], check=True)
        return True
    except FileNotFoundError as exc:
        print(f"Build tool not found: {exc}")
    except subprocess.CalledProcessError as exc:
        print(f"An error occurred while rebuilding the project: {exc}")
    return False


def get_latest_file(directory: str | Path) -> Path | None:
    directory_path = Path(directory)
    if not directory_path.is_dir():
        return None

    candidates = [
        path
        for path in directory_path.iterdir()
        if path.is_file()
        and path.suffix.lower() in INSTALLER_EXTENSIONS
        and extract_version_from_filename(path.name)
    ]
    if not candidates:
        candidates = [path for path in directory_path.iterdir() if path.is_file()]
    if not candidates:
        return None

    if all(extract_version_from_filename(item.name) for item in candidates):
        return max(
            candidates,
            key=lambda item: (
                version_tuple(extract_version_from_filename(item.name) or "0.0.0.0"),
                item.stat().st_ctime,
            ),
        )
    return max(candidates, key=lambda item: item.stat().st_ctime)


def extract_version_from_filename(filename: str | Path) -> str | None:
    version_match = VERSION_RE.search(Path(filename).name)
    return version_match.group(1) if version_match else None


def version_tuple(version_string: str) -> tuple[int, ...]:
    return tuple(map(int, version_string.split(".")))


def should_update_version(latest_version: str, current_version: str) -> bool:
    return version_tuple(latest_version) >= version_tuple(current_version)


def upload_file(
    file_path: str | Path,
    settings: RemoteUploadSettings,
    *,
    session: Any | None = None,
    progress_factory: Callable[..., Any] = tqdm,
) -> bool:
    return backend_upload_file(
        file_path,
        settings,
        session=session,
        progress_factory=progress_factory,
    )


def preflight_remote_upload(
    settings: RemoteUploadSettings,
    *,
    session: Any | None = None,
) -> bool:
    return backend_preflight_remote_upload(settings, session=session)


def publish_primary_release(
    latest_version: str,
    latest_file: str | Path,
    changelog_src: str | Path,
    remote_settings: RemoteUploadSettings,
    *,
    upload_func: Callable[[str | Path, RemoteUploadSettings], bool] = upload_file,
    upload_content_func: Callable[[str | bytes, str, RemoteUploadSettings], bool] = backend_upload_content,
) -> bool:
    latest_file = Path(latest_file)
    changelog_src = Path(changelog_src)

    if not changelog_src.is_file():
        print(f"Release changelog is missing: {changelog_src}")
        return False

    current_version = backend_fetch_latest_version(remote_settings)
    if not should_update_version(latest_version, current_version):
        print(f"The current version ({current_version}) is up to date.")
        return False

    print(f"Uploading primary release package: {latest_file.name}")
    if not upload_func(latest_file, remote_settings):
        print("Primary release package upload failed; CHANGELOG.md and LATEST_RELEASE will not be updated.")
        return False

    print(f"Uploading release changelog: {changelog_src.name}")
    if not upload_func(changelog_src, remote_settings):
        print("CHANGELOG.md upload failed; LATEST_RELEASE will not be updated.")
        return False

    print("Uploading release marker: LATEST_RELEASE")
    if not upload_content_func(latest_version, "LATEST_RELEASE", remote_settings):
        print("LATEST_RELEASE upload failed.")
        return False

    print(f"Updated the release version to {latest_version}")
    print(f"Upload {latest_file}")
    return True


def resolve_msbuild_path() -> Path:
    configured_path = os.environ.get("COLORVISION_MSBUILD_PATH")
    if configured_path:
        return Path(configured_path)

    for candidate in DEFAULT_MSBUILD_CANDIDATES:
        if candidate.exists():
            return candidate

    return DEFAULT_MSBUILD_CANDIDATES[0]


def build_projects(base_path: Path) -> dict[str, ProjectConfig]:
    user_home = Path(os.environ.get("USERPROFILE") or os.path.expanduser("~"))
    documents_dir = user_home / "Documents"
    ai_project_dir = documents_dir / "Advanced Installer" / "Projects" / "ColorVision"

    return {
        "ColorVision": ProjectConfig(
            name="ColorVision",
            msbuild_path=resolve_msbuild_path(),
            solution_path=base_path / "build.sln",
            advanced_installer_path=base_path.parent / "AdvancedInstaller v19.7.1" / "App" / "ProgramFiles" / "bin" / "x86" / "AdvancedInstaller.com",
            aip_path=ai_project_dir / "ColorVision.aip",
            setup_files_dir=ai_project_dir / "Setup Files",
            changelog_src=base_path / "CHANGELOG.md",
        )
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build and publish the ColorVision installer")
    parser.add_argument("--project", default=DEFAULT_PROJECT_NAME, help="Project name to build")
    parser.add_argument("--upload-url", default=None, help="Backend base URL for remote uploads")
    parser.add_argument("--upload-folder", default=os.environ.get("COLORVISION_UPLOAD_FOLDER", DEFAULT_UPLOAD_FOLDER), help="Remote folder path used by the backend upload endpoint")
    parser.add_argument("--upload-user", default=None, help="Backend upload username (prefer env var or shared default)")
    parser.add_argument("--upload-password", default=None, help="Backend upload password (prefer env var or shared default)")
    parser.add_argument("--upload-use-system-proxy", action="store_true", help="Use the system proxy for backend upload checks and uploads")
    parser.add_argument("--connect-timeout", type=int, default=DEFAULT_CONNECT_TIMEOUT, help="HTTP connect timeout in seconds")
    parser.add_argument("--read-timeout", type=int, default=DEFAULT_READ_TIMEOUT, help="HTTP read timeout in seconds")
    parser.add_argument("--upload-retries", type=int, default=DEFAULT_UPLOAD_RETRIES, help="Number of remote upload attempts")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    script_path = Path(__file__).resolve().parent
    base_path = script_path.parent
    projects = build_projects(base_path)

    if args.project not in projects:
        print(f"Unknown project: {args.project}")
        print(f"Available projects: {', '.join(sorted(projects))}")
        return 2

    project = projects[args.project]
    setup_files_dir = project.setup_files_dir
    if getattr(args, "upload_use_system_proxy", False):
        os.environ["COLORVISION_UPLOAD_USE_SYSTEM_PROXY"] = "1"

    upload_url = resolve_upload_base_url(args.upload_url)
    upload_username, upload_password = resolve_upload_credentials(
        args.upload_user,
        args.upload_password,
    )
    remote_settings = RemoteUploadSettings(
        base_url=upload_url,
        folder_name=args.upload_folder,
        username=upload_username,
        password=upload_password,
        enabled=True,
        connect_timeout=max(args.connect_timeout, 1),
        read_timeout=max(args.read_timeout, 1),
        max_retries=max(args.upload_retries, 1),
    )

    if not remote_settings.username or not remote_settings.password:
        print(
            "Remote upload requires Basic Auth credentials. "
            "Set COLORVISION_UPLOAD_USERNAME and COLORVISION_UPLOAD_PASSWORD."
        )
        return 2

    if not preflight_remote_upload(remote_settings):
        print("Remote upload preflight failed; aborting before build/upload.")
        return 2

    if not rebuild_project(
        project.msbuild_path,
        project.solution_path,
        project.advanced_installer_path,
        project.aip_path,
    ):
        return 1

    latest_file = get_latest_file(setup_files_dir)
    print(setup_files_dir)
    print(f"latest_file: {latest_file}")

    if not latest_file or not latest_file.exists():
        print("No installer files found in the directory.")
        return 1

    latest_version = extract_version_from_filename(latest_file)
    if not latest_version:
        print("Could not extract the version from the filename.")
        return 1

    if not publish_primary_release(
        latest_version,
        latest_file,
        project.changelog_src,
        remote_settings,
    ):
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

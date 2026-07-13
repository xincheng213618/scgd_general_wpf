import argparse
import json
import os
import subprocess
import sys
import tempfile
import time
import zipfile
from pathlib import Path, PurePosixPath
from urllib.parse import quote

import pefile


EXTRA_FILES = ["README.md", "CHANGELOG.md", "manifest.json", "PackageIcon.png"]
SCRIPT_DIR = Path(__file__).resolve().parent
RUNTIME_DIR = Path(getattr(sys, "_MEIPASS", SCRIPT_DIR)).resolve() if getattr(sys, "frozen", False) else SCRIPT_DIR
REPO_ROOT = SCRIPT_DIR.parent
DEFAULT_SHARED_FILES = RUNTIME_DIR / "shared_files.json"
DEFAULT_OUTPUT_DIR = SCRIPT_DIR
DEFAULT_CONFIGURATION = "Release"
DEFAULT_FRAMEWORK = "net10.0-windows"
DEFAULT_PLATFORM = "x64"
DEFAULT_CONFIG_FILE_NAME = "pluginkit.config.json"
DEFAULT_UPLOAD_URL = "http://xc213618.ddns.me:9998"
DEFAULT_UPLOAD_USERNAME = "xincheng"
DEFAULT_UPLOAD_PASSWORD = "xincheng"
DEFAULT_CONNECT_TIMEOUT = 10
DEFAULT_READ_TIMEOUT = 1800
DEFAULT_RETRY_COUNT = 3
DEFAULT_CHUNK_SIZE = 1024 * 1024
DISCOVERY_IGNORED_DIR_NAMES = {
    ".git",
    ".idea",
    ".vs",
    ".vscode",
    "__pycache__",
    "bin",
    "dist",
    "obj",
    "packages",
}
ALLOWED_RUNTIME_PREFIXES = (
    "runtimes/win/",
    "runtimes/win-x64/",
)


def get_requests_module():
    try:
        import requests
    except ImportError as exc:
        raise RuntimeError("The requests package is required for upload operations.") from exc
    return requests


def load_json_file(file_path: Path):
    with file_path.open("r", encoding="utf-8") as file:
        return json.load(file)


def write_json_file(file_path: Path, data) -> None:
    file_path.parent.mkdir(parents=True, exist_ok=True)
    with file_path.open("w", encoding="utf-8") as file:
        json.dump(data, file, indent=2, ensure_ascii=False)


def capture_directory_state(directory: Path) -> tuple[bool, bool]:
    if not directory.exists():
        return False, False
    return True, any(directory.iterdir())


def cleanup_empty_output_directory(directory: Path, existed_before: bool, had_entries_before: bool) -> None:
    if not directory.exists() or not directory.is_dir():
        return
    if any(directory.iterdir()):
        return
    if existed_before and had_entries_before:
        return

    directory.rmdir()
    print(f"Deleted empty output directory: {directory}")


def make_config_path_value(path_value: Path, base_dir: Path) -> str:
    try:
        relative_path = os.path.relpath(path_value, base_dir)
    except ValueError:
        return str(path_value)
    return relative_path


def resolve_path_from_base(path_value: str | None, base_dir: Path) -> Path | None:
    if not path_value:
        return None

    candidate = Path(path_value).expanduser()
    if not candidate.is_absolute():
        candidate = base_dir / candidate
    return candidate.resolve()


def resolve_project_file_path(project_path_value: str | Path, base_dir: Path) -> Path:
    candidate = Path(project_path_value).expanduser()
    if not candidate.is_absolute():
        candidate = base_dir / candidate
    candidate = candidate.resolve()

    if candidate.is_file():
        if candidate.suffix.lower() != ".csproj":
            raise ValueError(f"Project file must be a .csproj: {candidate}")
        return candidate

    if candidate.is_dir():
        project_files = discover_project_candidates(candidate)
        if len(project_files) == 1:
            return project_files[0]
        if not project_files:
            raise FileNotFoundError(f"No .csproj file found under: {candidate}")
        raise ValueError(f"Multiple .csproj files found under: {candidate}. Please pass --project-file explicitly.")

    raise FileNotFoundError(f"Project path not found: {candidate}")


def should_skip_discovery_dir(dir_name: str) -> bool:
    normalized_name = dir_name.lower()
    return normalized_name.startswith(".") or normalized_name in DISCOVERY_IGNORED_DIR_NAMES


def find_project_files_under(directory: Path) -> list[Path]:
    project_files: list[Path] = []
    for current_root, dir_names, file_names in os.walk(directory):
        dir_names[:] = sorted(
            dir_name
            for dir_name in dir_names
            if not should_skip_discovery_dir(dir_name)
        )
        for file_name in sorted(file_names):
            if file_name.lower().endswith(".csproj"):
                project_files.append((Path(current_root) / file_name).resolve())
    return project_files


def discover_project_candidates(base_dir: Path) -> list[Path]:
    top_level_projects = sorted(project_file.resolve() for project_file in base_dir.glob("*.csproj"))
    if top_level_projects:
        return top_level_projects

    nested_projects: list[Path] = []
    for child_dir in sorted(path for path in base_dir.iterdir() if path.is_dir() and not should_skip_discovery_dir(path.name)):
        nested_projects.extend(find_project_files_under(child_dir))
    return nested_projects


def try_find_single_project_file(base_dir: Path) -> Path | None:
    project_files = discover_project_candidates(base_dir)
    if len(project_files) == 1:
        return project_files[0]
    return None


def try_resolve_project_candidate(selection: str, candidate_projects: list[Path]) -> Path | None:
    if not selection or not candidate_projects or not selection.isdigit():
        return None

    selected_index = int(selection)
    if 1 <= selected_index <= len(candidate_projects):
        return candidate_projects[selected_index - 1]

    raise ValueError(f"Please choose a number between 1 and {len(candidate_projects)}.")


def prompt_yes_no(prompt_text: str, default: bool) -> bool:
    suffix = "[Y/n]" if default else "[y/N]"
    while True:
        response = input(f"{prompt_text} {suffix}: ").strip().lower()
        if not response:
            return default
        if response in {"y", "yes"}:
            return True
        if response in {"n", "no"}:
            return False
        print("Please answer y or n.")


def prompt_optional_value(prompt_text: str, default_value: str | None = None) -> str:
    if default_value:
        response = input(f"{prompt_text} [{default_value}]: ").strip()
        return response or default_value
    return input(f"{prompt_text}: ").strip()


def print_config_summary(config_data: dict) -> None:
    print("\nConfig summary:")
    print(f"  build enabled : {config_data.get('buildEnabled', False)}")
    if config_data.get("buildCommand"):
        print(f"  build command : {config_data['buildCommand']}")
    elif config_data.get("projectFile"):
        print(f"  project file  : {config_data['projectFile']}")
    print(f"  source dir    : {config_data.get('srcDir', '')}")
    print(f"  output dir    : {config_data.get('outputDir', '')}")
    print(f"  upload        : {config_data.get('uploadEnabled', True)}")
    if config_data.get("uploadEnabled", True):
        print(f"  upload url    : {config_data.get('uploadUrl', '')}")
        print(f"  keep cvxp     : {config_data.get('keepPackageAfterUpload', True)}")


def create_interactive_config(config_path: Path) -> dict | None:
    base_dir = config_path.parent.resolve()
    default_project_file = try_find_single_project_file(base_dir)
    project_candidates = discover_project_candidates(base_dir)
    project_file: Path | None = None
    build_command: str | None = None
    build_working_dir: Path | None = None

    print("No config file was found. A new pluginkit.config.json will be created in the current folder.")
    build_enabled = prompt_yes_no("Configure a build step before packaging", True)
    if build_enabled:
        default_build_source = make_config_path_value(default_project_file, base_dir) if default_project_file else None
        if default_project_file:
            print(f"Press Enter to use the detected project: {default_build_source}")
        else:
            print("No single .csproj was found in the current folder.")
            if project_candidates:
                print("Project candidates:")
                for index, candidate_project in enumerate(project_candidates, start=1):
                    print(f"  {index}. {make_config_path_value(candidate_project, base_dir)}")
        print("You can also enter another .csproj path, a folder that contains one .csproj, or use cmd:<command> for a custom build command.")

        while True:
            build_source = prompt_optional_value("Build source", default_build_source).strip()
            if build_source.lower().startswith("cmd:"):
                build_command = build_source[4:].strip()
                if not build_command:
                    print("Custom build command cannot be empty.")
                    continue
                build_working_dir = base_dir
                break
            try:
                project_file = try_resolve_project_candidate(build_source, project_candidates) or resolve_project_file_path(build_source, base_dir)
                break
            except (FileNotFoundError, ValueError) as exc:
                print(exc)

    source_root = project_file.parent if project_file else base_dir
    default_src_dir = source_root / "bin" / DEFAULT_PLATFORM / DEFAULT_CONFIGURATION / DEFAULT_FRAMEWORK
    src_dir = resolve_path_from_base(prompt_optional_value("Package source directory", str(default_src_dir)), base_dir)
    output_dir = resolve_path_from_base(prompt_optional_value("Package output directory", str(base_dir / "packages")), base_dir)
    upload_enabled = prompt_yes_no("Upload after packaging", True)
    keep_package_after_upload = True
    if upload_enabled:
        keep_package_after_upload = prompt_yes_no("Keep local .cvxp after successful upload", False)

    config_data: dict[str, object] = {
        "configuration": DEFAULT_CONFIGURATION,
        "framework": DEFAULT_FRAMEWORK,
        "platform": DEFAULT_PLATFORM,
        "srcDir": make_config_path_value(src_dir, base_dir),
        "outputDir": make_config_path_value(output_dir, base_dir),
        "buildEnabled": build_enabled,
        "uploadEnabled": upload_enabled,
        "keepPackageAfterUpload": keep_package_after_upload,
        "dotnet": os.environ.get("DOTNET_EXE", "dotnet"),
        "uploadUrl": os.environ.get("COLORVISION_UPLOAD_URL", DEFAULT_UPLOAD_URL),
        "username": os.environ.get("COLORVISION_UPLOAD_USERNAME", DEFAULT_UPLOAD_USERNAME),
        "password": os.environ.get("COLORVISION_UPLOAD_PASSWORD", DEFAULT_UPLOAD_PASSWORD),
    }

    if project_file:
        config_data["projectFile"] = make_config_path_value(project_file, base_dir)
    if build_command:
        config_data["buildCommand"] = build_command
        config_data["buildWorkingDir"] = make_config_path_value(build_working_dir, base_dir)

    print_config_summary(config_data)
    if not prompt_yes_no(f"Write config to {config_path.name}", True):
        print("Cancelled.")
        return None

    write_json_file(config_path, config_data)
    print(f"Generated config: {config_path}")
    return config_data


def load_packager_config(config_path: Path) -> dict:
    config_data = load_json_file(config_path)
    if not isinstance(config_data, dict):
        raise RuntimeError(f"Config must be a JSON object: {config_path}")
    return config_data


def build_default_config(project_file: Path, config_path: Path) -> dict:
    base_dir = config_path.parent.resolve()
    default_src_dir = project_file.parent / "bin" / DEFAULT_PLATFORM / DEFAULT_CONFIGURATION / DEFAULT_FRAMEWORK

    return {
        "projectFile": make_config_path_value(project_file, base_dir),
        "configuration": DEFAULT_CONFIGURATION,
        "framework": DEFAULT_FRAMEWORK,
        "platform": DEFAULT_PLATFORM,
        "srcDir": make_config_path_value(default_src_dir, base_dir),
        "outputDir": make_config_path_value((base_dir / "packages").resolve(), base_dir),
        "buildEnabled": True,
        "uploadEnabled": True,
        "keepPackageAfterUpload": False,
        "dotnet": os.environ.get("DOTNET_EXE", "dotnet"),
        "uploadUrl": os.environ.get("COLORVISION_UPLOAD_URL", DEFAULT_UPLOAD_URL),
        "username": os.environ.get("COLORVISION_UPLOAD_USERNAME", DEFAULT_UPLOAD_USERNAME),
        "password": os.environ.get("COLORVISION_UPLOAD_PASSWORD", DEFAULT_UPLOAD_PASSWORD),
    }


def initialize_config(project_path_value: str, config_path: Path) -> Path:
    project_file = resolve_project_file_path(project_path_value, Path.cwd())
    config_data = build_default_config(project_file, config_path)
    write_json_file(config_path, config_data)
    return project_file


def normalize_relative_path(path_value: str) -> str:
    return Path(path_value.replace("\\", "/")).as_posix()


def normalize_archive_relative_path(path_value: str) -> str:
    return PurePosixPath(path_value.replace("\\", "/")).as_posix()


def should_keep_runtime_path(path_value: str) -> bool:
    normalized = normalize_archive_relative_path(path_value).lower()
    if not normalized.startswith("runtimes/"):
        return True

    return normalized.startswith(ALLOWED_RUNTIME_PREFIXES)


def build_upload_url(base_url: str, folder_name: str, file_name: str) -> str:
    encoded_folder = "/".join(quote(part, safe="") for part in folder_name.replace("\\", "/").strip("/").split("/") if part)
    return f"{base_url.rstrip('/')}/upload/{encoded_folder}/{quote(file_name, safe='')}"


def create_http_session(requests_module):
    session = requests_module.Session()
    session.trust_env = False
    session.proxies.clear()
    return session


def preflight_remote_upload(session, base_url: str, auth: tuple[str, str]) -> bool:
    requests_module = get_requests_module()
    timeout = (DEFAULT_CONNECT_TIMEOUT, 15)

    for endpoint in ("/api/health", "/api/ready"):
        url = f"{base_url.rstrip('/')}{endpoint}"
        try:
            response = session.get(url, auth=auth, timeout=timeout)
        except requests_module.RequestException as exc:
            print(f"Upload preflight failed: {exc}")
            return False

        if response.status_code == 404:
            print(f"Preflight endpoint not available ({endpoint}); continuing with legacy upload flow.")
            return True

        if response.status_code != 200:
            print(f"Upload preflight failed: HTTP {response.status_code} {response.text.strip()}")
            return False

    print("Upload preflight passed.")
    return True


def upload_file(file_path: Path, folder_name: str, base_url: str, username: str, password: str) -> bool:
    requests_module = get_requests_module()
    session = create_http_session(requests_module)
    auth = (username, password)
    last_error = ""

    if not preflight_remote_upload(session, base_url, auth):
        return False

    if not username or not password:
        print("Upload failed: missing username or password.")
        return False

    upload_url = build_upload_url(base_url, folder_name, file_path.name)

    for attempt in range(1, DEFAULT_RETRY_COUNT + 1):
        try:
            with file_path.open("rb") as file_stream:
                def read_chunks():
                    while True:
                        chunk = file_stream.read(DEFAULT_CHUNK_SIZE)
                        if not chunk:
                            break
                        yield chunk

                response = session.put(
                    upload_url,
                    data=read_chunks(),
                    auth=auth,
                    timeout=(DEFAULT_CONNECT_TIMEOUT, DEFAULT_READ_TIMEOUT),
                    headers={"Content-Type": "application/octet-stream"},
                )

            if response.status_code == 201:
                print(f"Uploaded: {file_path.name}")
                return True

            if response.status_code == 401:
                print("Upload failed: HTTP 401 Unauthorized")
                return False

            last_error = f"HTTP {response.status_code}: {response.text.strip()}"
            print(f"Upload attempt {attempt} failed: {last_error}")
            if response.status_code < 500 and response.status_code not in {408, 429}:
                return False
        except requests_module.RequestException as exc:
            last_error = str(exc)
            print(f"Upload attempt {attempt} failed: {last_error}")

        if attempt < DEFAULT_RETRY_COUNT:
            wait_seconds = min(2 ** (attempt - 1), 5)
            print(f"Retrying upload in {wait_seconds} second(s)...")
            time.sleep(wait_seconds)

    if last_error:
        print(f"Upload failed after {DEFAULT_RETRY_COUNT} attempt(s): {last_error}")
    return False


def upload_latest_release(version: str, folder_name: str, base_url: str, username: str, password: str) -> bool:
    with tempfile.TemporaryDirectory(prefix="package_cvxp_release_") as temp_dir:
        latest_release_path = Path(temp_dir) / "LATEST_RELEASE"
        latest_release_path.write_text(version, encoding="utf-8")
        return upload_file(latest_release_path, folder_name, base_url, username, password)


def get_file_version(file_path: Path) -> str | None:
    pe = pefile.PE(str(file_path))
    version_info = None

    if hasattr(pe, "FileInfo"):
        for file_info in pe.FileInfo:
            for entry in file_info:
                if entry.Key == b"StringFileInfo":
                    for string_table in entry.StringTable:
                        if b"FileVersion" in string_table.entries:
                            version_info = string_table.entries[b"FileVersion"].decode("utf-8")
                            break

    return version_info


def infer_project_name(src_dir: Path, project_file: Path | None, plugin_name: str | None) -> str:
    if plugin_name:
        return plugin_name

    if project_file:
        return project_file.stem

    manifest_path = src_dir / "manifest.json"
    if manifest_path.is_file():
        try:
            manifest_data = load_json_file(manifest_path)
            dll_path = manifest_data.get("dllpath")
            if dll_path:
                return Path(dll_path).stem
        except (OSError, json.JSONDecodeError):
            pass

    for deps_file in sorted(src_dir.glob("*.deps.json")):
        return deps_file.stem

    dll_candidates = sorted(file_path for file_path in src_dir.glob("*.dll") if not file_path.name.endswith(".resources.dll"))
    if dll_candidates:
        return dll_candidates[0].stem

    return src_dir.name


def infer_plugin_root_from_src_dir(src_dir: Path) -> Path:
    for candidate in (src_dir, *src_dir.parents):
        if candidate.name.lower() == "bin" and candidate.parent != candidate:
            return candidate.parent.resolve()
    return src_dir


def resolve_plugin_root(src_dir: Path, project_file: Path | None, plugin_root: Path | None) -> Path:
    if plugin_root:
        return plugin_root
    if project_file:
        return project_file.parent
    return infer_plugin_root_from_src_dir(src_dir)


def resolve_src_dir(src_dir: Path | None, project_file: Path | None, configuration: str, framework: str, platform: str) -> Path:
    if src_dir:
        return src_dir.resolve()

    if not project_file:
        raise ValueError("Either --src-dir or --project-file must be provided.")

    project_root = project_file.parent
    candidates = [
        project_root / "bin" / platform / configuration / framework,
        project_root / "bin" / configuration / framework,
    ]
    for candidate in candidates:
        if candidate.is_dir():
            return candidate.resolve()
    return candidates[0].resolve()


def resolve_shared_files_path(shared_files: Path | None) -> Path:
    candidates: list[Path] = []
    if shared_files:
        candidates.append(shared_files.expanduser().resolve())
    candidates.append((SCRIPT_DIR / "shared_files.json").resolve())
    candidates.append((Path.cwd() / "shared_files.json").resolve())

    checked: list[str] = []
    for candidate in candidates:
        candidate_text = str(candidate)
        if candidate_text in checked:
            continue
        checked.append(candidate_text)
        if candidate.is_file():
            return candidate

    checked_paths = "\n".join(f"- {path}" for path in checked)
    raise FileNotFoundError(
        "shared_files.json not found. Checked:\n"
        f"{checked_paths}\n"
        "Place shared_files.json next to package_cvxp.py, in the current directory, or pass --shared-files."
    )


def build_project(project_file: Path, configuration: str, framework: str, platform: str, dotnet_command: str) -> None:
    command = [
        dotnet_command,
        "build",
        str(project_file),
        "-c",
        configuration,
        "-f",
        framework,
        f"-p:Platform={platform}",
    ]
    print(f"Build command: {' '.join(command)}")
    subprocess.run(command, check=True, cwd=project_file.parent)


def run_custom_build_command(command_text: str, working_dir: Path) -> None:
    print(f"Build command: {command_text}")
    subprocess.run(command_text, check=True, cwd=working_dir, shell=True)


def run_build_step(
    project_file: Path | None,
    build_command: str | None,
    build_working_dir: Path | None,
    configuration: str,
    framework: str,
    platform: str,
    dotnet_command: str,
) -> None:
    if build_command:
        run_custom_build_command(build_command, build_working_dir or (project_file.parent if project_file else Path.cwd()))
        return

    if not project_file:
        raise ValueError("Build is enabled but no project file or build command was provided.")

    build_project(project_file, configuration, framework, platform, dotnet_command)


def load_shared_files_manifest(file_path: Path) -> set[str]:
    manifest_data = load_json_file(file_path)
    if isinstance(manifest_data, dict):
        shared_files = manifest_data.get("shared_files", [])
    elif isinstance(manifest_data, list):
        shared_files = manifest_data
    else:
        raise RuntimeError(f"Unsupported shared_files.json format: {file_path}")
    return {normalize_relative_path(path_value) for path_value in shared_files}


def find_extra_files(plugin_root: Path) -> list[Path]:
    extra_files: list[Path] = []
    for file_name in EXTRA_FILES:
        file_path = plugin_root / file_name
        if file_path.is_file():
            extra_files.append(file_path)
    return extra_files


def package_plugin(src_dir: Path, plugin_root: Path, shared_files: set[str], output_file: Path, project_name: str) -> tuple[Path, int]:
    with tempfile.TemporaryDirectory(prefix="package_cvxp_") as temp_dir_name:
        temp_dir = Path(temp_dir_name)
        project_path = temp_dir / project_name
        project_path.mkdir(parents=True, exist_ok=True)

        stripped_files: list[str] = []
        skipped_runtime_files = 0
        for file_path in sorted(path for path in src_dir.rglob("*") if path.is_file()):
            relative_path = normalize_relative_path(str(file_path.relative_to(src_dir)))
            if file_path.suffix.lower() == ".pdb":
                continue
            if not should_keep_runtime_path(relative_path):
                skipped_runtime_files += 1
                continue
            if relative_path in shared_files:
                stripped_files.append(relative_path)
                continue

            destination = project_path / Path(relative_path)
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(file_path.read_bytes())

        write_json_file(project_path / "stripped_files.json", sorted(stripped_files))

        for extra_file in find_extra_files(plugin_root):
            destination = project_path / extra_file.name
            destination.write_bytes(extra_file.read_bytes())

        output_file.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(output_file, "w", zipfile.ZIP_DEFLATED) as zip_file:
            for file_path in sorted(path for path in temp_dir.rglob("*") if path.is_file()):
                zip_file.write(file_path, file_path.relative_to(temp_dir).as_posix())

    return output_file, len(stripped_files), skipped_runtime_files


def main() -> None:
    raw_args = sys.argv[1:]
    default_config_path = (Path.cwd() / DEFAULT_CONFIG_FILE_NAME).resolve()
    auto_mode = False
    if not raw_args:
        if default_config_path.is_file():
            raw_args = ["--config", str(default_config_path)]
            auto_mode = True
        else:
            create_interactive_config(default_config_path)
            return

    parser = argparse.ArgumentParser(description="Package a plugin into .cvxp using shared_files.json, upload it, then delete the local package unless configured otherwise.")
    parser.add_argument("--config", help="Path to a pluginkit config JSON file")
    parser.add_argument("--init-config", help="Generate a config JSON from a plugin .csproj path or a project folder")
    parser.add_argument("--src-dir", help="Compiled plugin output directory")
    parser.add_argument("--project-file", help="Plugin .csproj path used to infer plugin root and output directory")
    parser.add_argument("--plugin-root", help="Plugin project root used to copy README/CHANGELOG/manifest/icon")
    parser.add_argument("--plugin-name", help="Plugin name used for output file name and upload folder")
    parser.add_argument("--shared-files", help="Path to shared_files.json. If omitted, package_cvxp.py looks next to itself first.")
    parser.add_argument("--output-dir", help=f"Output directory for the .cvxp file (default: {DEFAULT_OUTPUT_DIR})")
    parser.add_argument("-c", "--configuration", help=f"Build configuration used with --project-file (default: {DEFAULT_CONFIGURATION})")
    parser.add_argument("-f", "--framework", help=f"Target framework used with --project-file (default: {DEFAULT_FRAMEWORK})")
    parser.add_argument("--platform", help=f"MSBuild platform used with --build (default: {DEFAULT_PLATFORM})")
    parser.add_argument("--build", action="store_true", help="Run dotnet build before packaging. Requires --project-file.")
    parser.add_argument("--build-only", action="store_true", help="Only run the build step, then exit without packaging")
    parser.add_argument("--dotnet", help="dotnet executable used with --build")
    parser.add_argument("--upload-url", help="Upload server base URL")
    parser.add_argument("--username", help="Upload username")
    parser.add_argument("--password", help="Upload password")
    parser.add_argument("--keep-package", action="store_true", help="Keep the local .cvxp file after successful upload")
    args = parser.parse_args(raw_args)

    config_path = resolve_path_from_base(args.config, Path.cwd()) if args.config else None
    if args.init_config:
        config_output = config_path or (Path.cwd() / DEFAULT_CONFIG_FILE_NAME)
        project_file = initialize_config(args.init_config, config_output)
        print(f"Generated config: {config_output}")
        print(f"Project file: {project_file}")
        return

    config_data = load_packager_config(config_path) if config_path else {}
    config_base_dir = config_path.parent if config_path else Path.cwd()

    project_file_value = args.project_file or config_data.get("projectFile") or config_data.get("projectPath")
    project_file = resolve_project_file_path(project_file_value, config_base_dir) if project_file_value else None
    build_command = config_data.get("buildCommand")
    build_working_dir = resolve_path_from_base(config_data.get("buildWorkingDir"), config_base_dir) if config_data.get("buildWorkingDir") else None
    src_dir_value = args.src_dir or config_data.get("srcDir")
    plugin_root_value = args.plugin_root or config_data.get("pluginRoot")
    shared_files_value = args.shared_files or config_data.get("sharedFiles")
    output_dir_value = args.output_dir or config_data.get("outputDir")
    project_name_value = args.plugin_name or config_data.get("pluginName")
    configuration = args.configuration or config_data.get("configuration") or DEFAULT_CONFIGURATION
    framework = args.framework or config_data.get("framework") or DEFAULT_FRAMEWORK
    platform = args.platform or config_data.get("platform") or DEFAULT_PLATFORM
    dotnet_command = args.dotnet or config_data.get("dotnet") or os.environ.get("DOTNET_EXE", "dotnet")
    upload_url = args.upload_url or config_data.get("uploadUrl") or os.environ.get("COLORVISION_UPLOAD_URL", DEFAULT_UPLOAD_URL)
    username = args.username or config_data.get("username") or os.environ.get("COLORVISION_UPLOAD_USERNAME", DEFAULT_UPLOAD_USERNAME)
    password = args.password or config_data.get("password") or os.environ.get("COLORVISION_UPLOAD_PASSWORD", DEFAULT_UPLOAD_PASSWORD)
    config_build_enabled = bool(config_data.get("buildEnabled", False))
    config_upload_enabled = bool(config_data.get("uploadEnabled", True))
    config_keep_package = bool(config_data.get("keepPackageAfterUpload", True if config_data else False))
    keep_package_after_upload = args.keep_package or config_keep_package

    should_build = args.build or args.build_only or (auto_mode and config_build_enabled)
    if should_build:
        run_build_step(
            project_file,
            build_command,
            build_working_dir,
            configuration,
            framework,
            platform,
            dotnet_command,
        )

    if args.build_only:
        return

    src_dir = resolve_src_dir(
        resolve_path_from_base(src_dir_value, config_base_dir),
        project_file,
        configuration,
        framework,
        platform,
    )
    plugin_root = resolve_plugin_root(
        src_dir,
        project_file,
        resolve_path_from_base(plugin_root_value, config_base_dir),
    )
    shared_files_path = resolve_shared_files_path(resolve_path_from_base(shared_files_value, config_base_dir))
    output_dir = resolve_path_from_base(output_dir_value, config_base_dir) or DEFAULT_OUTPUT_DIR.resolve()
    output_dir_existed_before, output_dir_had_entries_before = capture_directory_state(output_dir)

    if not src_dir.is_dir():
        raise FileNotFoundError(f"Plugin output directory not found: {src_dir}")

    project_name = infer_project_name(src_dir, project_file, project_name_value)
    dll_path = src_dir / f"{project_name}.dll"
    if not dll_path.is_file():
        raise FileNotFoundError(f"Plugin DLL not found: {dll_path}")

    version = get_file_version(dll_path)
    if not version:
        raise RuntimeError(f"Cannot read version from: {dll_path}")

    shared_files = load_shared_files_manifest(shared_files_path)
    output_file = output_dir / f"{project_name}-{version}.cvxp"
    output_file, stripped_count, skipped_runtime_count = package_plugin(src_dir, plugin_root, shared_files, output_file, project_name)

    print(f"Source directory: {src_dir}")
    print(f"Plugin root: {plugin_root}")
    print(f"Shared files manifest: {shared_files_path}")
    print(f"Shared file count: {len(shared_files)}")
    print(f"Stripped file count: {stripped_count}")
    print(f"Skipped runtime file count: {skipped_runtime_count}")
    print(f"Packaged: {output_file}")

    should_upload = not auto_mode or config_upload_enabled
    if not should_upload:
        return

    plugin_folder = f"Plugins/{project_name}"
    if not upload_file(output_file, plugin_folder, upload_url, username, password):
        raise RuntimeError("Package upload failed.")

    if not upload_latest_release(version, plugin_folder, upload_url, username, password):
        raise RuntimeError("LATEST_RELEASE upload failed.")

    if not keep_package_after_upload and output_file.exists():
        output_file.unlink()
        print(f"Deleted local package: {output_file}")
        cleanup_empty_output_directory(output_dir, output_dir_existed_before, output_dir_had_entries_before)


if __name__ == "__main__":
    main()

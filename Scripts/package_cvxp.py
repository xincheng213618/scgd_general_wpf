import argparse
import json
import os
import subprocess
import tempfile
import time
import zipfile
from pathlib import Path, PurePosixPath
from urllib.parse import quote

import pefile


EXTRA_FILES = ["README.md", "CHANGELOG.md", "manifest.json", "PackageIcon.png"]
SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent
DEFAULT_SHARED_FILES = SCRIPT_DIR / "shared_files.json"
DEFAULT_OUTPUT_DIR = SCRIPT_DIR
DEFAULT_UPLOAD_URL = "http://xc213618.ddns.me:9998"
DEFAULT_UPLOAD_USERNAME = "xincheng"
DEFAULT_UPLOAD_PASSWORD = "xincheng"
DEFAULT_CONNECT_TIMEOUT = 10
DEFAULT_READ_TIMEOUT = 1800
DEFAULT_RETRY_COUNT = 3
DEFAULT_CHUNK_SIZE = 1024 * 1024
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


def resolve_src_dir(src_dir: Path | None, project_file: Path | None, configuration: str, framework: str) -> Path:
    if src_dir:
        return src_dir.resolve()

    if not project_file:
        raise ValueError("Either --src-dir or --project-file must be provided.")

    project_root = project_file.parent
    candidates = [
        project_root / "bin" / "x64" / configuration / framework,
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


def build_project(project_file: Path, configuration: str, framework: str, dotnet_command: str) -> None:
    command = [
        dotnet_command,
        "build",
        str(project_file),
        "-c",
        configuration,
        "-f",
        framework,
        "-p:Platform=x64",
    ]
    print(f"Build command: {' '.join(command)}")
    subprocess.run(command, check=True, cwd=project_file.parent)


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
    parser = argparse.ArgumentParser(description="Package a plugin into .cvxp using shared_files.json, upload it, then delete the local package.")
    parser.add_argument("--src-dir", help="Compiled plugin output directory")
    parser.add_argument("--project-file", help="Plugin .csproj path used to infer plugin root and output directory")
    parser.add_argument("--plugin-root", help="Plugin project root used to copy README/CHANGELOG/manifest/icon")
    parser.add_argument("--plugin-name", help="Plugin name used for output file name and upload folder")
    parser.add_argument("--shared-files", help="Path to shared_files.json. If omitted, package_cvxp.py looks next to itself first.")
    parser.add_argument("--output-dir", default=str(DEFAULT_OUTPUT_DIR), help=f"Output directory for the .cvxp file (default: {DEFAULT_OUTPUT_DIR})")
    parser.add_argument("-c", "--configuration", default="Release", help="Build configuration used with --project-file")
    parser.add_argument("-f", "--framework", default="net10.0-windows", help="Target framework used with --project-file")
    parser.add_argument("--build", action="store_true", help="Run dotnet build before packaging. Requires --project-file.")
    parser.add_argument("--dotnet", default=os.environ.get("DOTNET_EXE", "dotnet"), help="dotnet executable used with --build")
    parser.add_argument("--upload-url", default=os.environ.get("COLORVISION_UPLOAD_URL", DEFAULT_UPLOAD_URL), help="Upload server base URL")
    parser.add_argument("--username", default=os.environ.get("COLORVISION_UPLOAD_USERNAME", DEFAULT_UPLOAD_USERNAME), help="Upload username")
    parser.add_argument("--password", default=os.environ.get("COLORVISION_UPLOAD_PASSWORD", DEFAULT_UPLOAD_PASSWORD), help="Upload password")
    args = parser.parse_args()

    project_file = Path(args.project_file).expanduser().resolve() if args.project_file else None
    if args.build:
        if not project_file:
            raise ValueError("--build requires --project-file.")
        build_project(project_file, args.configuration, args.framework, args.dotnet)

    src_dir = resolve_src_dir(
        Path(args.src_dir).expanduser() if args.src_dir else None,
        project_file,
        args.configuration,
        args.framework,
    )
    plugin_root = resolve_plugin_root(
        src_dir,
        project_file,
        Path(args.plugin_root).expanduser().resolve() if args.plugin_root else None,
    )
    shared_files_path = resolve_shared_files_path(Path(args.shared_files) if args.shared_files else None)
    output_dir = Path(args.output_dir).expanduser().resolve()

    if not src_dir.is_dir():
        raise FileNotFoundError(f"Plugin output directory not found: {src_dir}")

    project_name = infer_project_name(src_dir, project_file, args.plugin_name)
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

    plugin_folder = f"Plugins/{project_name}"
    try:
        if not upload_file(output_file, plugin_folder, args.upload_url, args.username, args.password):
            raise RuntimeError("Package upload failed.")

        if not upload_latest_release(version, plugin_folder, args.upload_url, args.username, args.password):
            raise RuntimeError("LATEST_RELEASE upload failed.")
    finally:
        if output_file.exists():
            output_file.unlink()
            print(f"Deleted local package: {output_file}")


if __name__ == "__main__":
    main()

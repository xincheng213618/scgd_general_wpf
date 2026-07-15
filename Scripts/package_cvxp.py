import argparse
import json
import os
import re
import subprocess
import tempfile
import time
import zipfile
from dataclasses import dataclass
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
MAX_MANIFEST_BYTES = 1024 * 1024
MAX_COPILOT_AGENT_ROLES = 16
MAX_COPILOT_AGENT_METADATA_CHARACTERS = 8_000
PLUGIN_ID_PATTERN = re.compile(r"^[a-z][a-z0-9._-]{1,63}$")
ROLE_ID_PATTERN = re.compile(r"^[a-z][a-z0-9-]{1,47}$")
TOOL_NAME_PATTERN = re.compile(r"^Delegate[A-Z][A-Za-z0-9]{1,55}$")
WORKSPACE_CAPABILITIES = {"searchfiles", "greptext", "readlocalfile", "listdirectory"}
WEB_CAPABILITIES = {"websearch", "fetchurl"}
AGENT_MODES = {"auto", "explain", "web", "code", "diagnose", "chat"}


@dataclass(frozen=True)
class CopilotAgentManifestSummary:
    manifest_present: bool
    role_count: int = 0
    metadata_characters: int = 0


def _manifest_error(manifest_path: Path, field: str, message: str) -> ValueError:
    return ValueError(f"Invalid plugin manifest '{manifest_path}' at {field}: {message}")


def _require_manifest(condition: bool, manifest_path: Path, field: str, message: str) -> None:
    if not condition:
        raise _manifest_error(manifest_path, field, message)


def _read_string(data: dict, key: str, manifest_path: Path, field: str, *, required: bool, maximum: int) -> str:
    value = data.get(key, "")
    _require_manifest(isinstance(value, str), manifest_path, field, "must be a string")
    value = value.strip()
    if required:
        _require_manifest(bool(value), manifest_path, field, "must not be empty")
    _require_manifest(len(value) <= maximum, manifest_path, field, f"must not exceed {maximum:,} characters")
    return value


def _read_string_list(data: dict, key: str, manifest_path: Path, field: str, *, required: bool) -> list[str]:
    value = data.get(key, [])
    _require_manifest(isinstance(value, list), manifest_path, field, "must be an array")
    _require_manifest(all(isinstance(item, str) and item.strip() for item in value), manifest_path, field, "must contain non-empty strings")
    if required:
        _require_manifest(bool(value), manifest_path, field, "must not be empty")
    return [item.strip() for item in value]


def _read_budget(data: dict, key: str, manifest_path: Path, field: str, default: int, minimum: int, maximum: int) -> int:
    value = data.get(key, 0)
    _require_manifest(isinstance(value, int) and not isinstance(value, bool), manifest_path, field, "must be an integer")
    value = default if value == 0 else value
    _require_manifest(minimum <= value <= maximum, manifest_path, field, f"must be 0 or between {minimum:,} and {maximum:,}")
    return value


def _normalize_token(value: str) -> str:
    return "".join(character.lower() for character in value if character.isalnum())


def _default_tool_name(role_id: str) -> str:
    return "Delegate" + "".join(segment[:1].upper() + segment[1:] for segment in re.split(r"[-_.]+", role_id) if segment)


def validate_plugin_manifest(manifest_path: Path) -> CopilotAgentManifestSummary:
    if not manifest_path.is_file():
        return CopilotAgentManifestSummary(False)

    raw_manifest = manifest_path.read_bytes()
    _require_manifest(len(raw_manifest) <= MAX_MANIFEST_BYTES, manifest_path, "$", f"file must not exceed {MAX_MANIFEST_BYTES:,} bytes")
    try:
        manifest = json.loads(raw_manifest.decode("utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise _manifest_error(manifest_path, "$", f"invalid UTF-8 JSON: {exc}") from exc

    _require_manifest(isinstance(manifest, dict), manifest_path, "$", "must be a JSON object")
    if "copilot_agents" not in manifest:
        return CopilotAgentManifestSummary(True)

    roles = manifest["copilot_agents"]
    _require_manifest(isinstance(roles, list), manifest_path, "copilot_agents", "must be an array")
    _require_manifest(len(roles) <= MAX_COPILOT_AGENT_ROLES, manifest_path, "copilot_agents", f"must contain at most {MAX_COPILOT_AGENT_ROLES} roles")
    if not roles:
        return CopilotAgentManifestSummary(True)

    plugin_id = manifest.get("id", "")
    _require_manifest(isinstance(plugin_id, str) and PLUGIN_ID_PATTERN.fullmatch(plugin_id.strip()) is not None, manifest_path, "id", "must contain 2-64 lowercase ASCII letters, digits, '.', '_' or '-'")

    role_ids: set[str] = set()
    tool_names: set[str] = set()
    metadata_characters = 0
    for index, role in enumerate(roles):
        prefix = f"copilot_agents[{index}]"
        _require_manifest(isinstance(role, dict), manifest_path, prefix, "must be an object")

        role_id = _read_string(role, "id", manifest_path, f"{prefix}.id", required=True, maximum=48).lower()
        _require_manifest(ROLE_ID_PATTERN.fullmatch(role_id) is not None, manifest_path, f"{prefix}.id", "must contain 2-48 lowercase ASCII letters, digits or '-'")
        _require_manifest(role_id not in role_ids, manifest_path, f"{prefix}.id", "duplicates another role id in this plugin")
        role_ids.add(role_id)

        tool_name = _read_string(role, "tool", manifest_path, f"{prefix}.tool", required=False, maximum=64) or _default_tool_name(role_id)
        _require_manifest(TOOL_NAME_PATTERN.fullmatch(tool_name) is not None, manifest_path, f"{prefix}.tool", "must use the form DelegateName with ASCII letters or digits")
        _require_manifest(tool_name.lower() not in tool_names, manifest_path, f"{prefix}.tool", "duplicates another role tool name in this plugin")
        tool_names.add(tool_name.lower())

        display_name = _read_string(role, "name", manifest_path, f"{prefix}.name", required=False, maximum=80) or role_id
        description = _read_string(role, "description", manifest_path, f"{prefix}.description", required=True, maximum=1_200)
        _read_string(role, "instructions", manifest_path, f"{prefix}.instructions", required=True, maximum=8_000)

        scope_value = _read_string(role, "scope", manifest_path, f"{prefix}.scope", required=True, maximum=32)
        scope = _normalize_token(scope_value)
        _require_manifest(scope in {"workspace", "workspacereadonly", "web", "publicweb"}, manifest_path, f"{prefix}.scope", "must be WorkspaceReadOnly or PublicWeb")
        is_workspace = scope in {"workspace", "workspacereadonly"}

        capabilities = {_normalize_token(value) for value in _read_string_list(role, "capabilities", manifest_path, f"{prefix}.capabilities", required=True)}
        known_capabilities = WORKSPACE_CAPABILITIES | WEB_CAPABILITIES
        unknown_capabilities = capabilities - known_capabilities
        _require_manifest(not unknown_capabilities, manifest_path, f"{prefix}.capabilities", f"contains unknown values: {', '.join(sorted(unknown_capabilities))}")
        allowed_capabilities = WORKSPACE_CAPABILITIES if is_workspace else WEB_CAPABILITIES
        _require_manifest(capabilities <= allowed_capabilities, manifest_path, f"{prefix}.capabilities", "cannot mix workspace and public-web capabilities")

        child_mode_value = _read_string(role, "child_mode", manifest_path, f"{prefix}.child_mode", required=False, maximum=16)
        child_mode = child_mode_value.lower() if child_mode_value else ("code" if is_workspace else "web")
        _require_manifest(child_mode in AGENT_MODES, manifest_path, f"{prefix}.child_mode", "contains an unknown Agent mode")
        _require_manifest(not is_workspace or child_mode not in {"chat", "web"}, manifest_path, f"{prefix}.child_mode", "a workspace role cannot use Chat or Web mode")
        _require_manifest(is_workspace or child_mode == "web", manifest_path, f"{prefix}.child_mode", "a public-web role must use Web mode")

        parent_modes = _read_string_list(role, "parent_modes", manifest_path, f"{prefix}.parent_modes", required=False)
        normalized_parent_modes = {value.lower() for value in parent_modes} if parent_modes else {"auto", "explain", "web", "code", "diagnose"}
        _require_manifest(normalized_parent_modes <= AGENT_MODES and "chat" not in normalized_parent_modes, manifest_path, f"{prefix}.parent_modes", "must contain only defined non-Chat Agent modes")

        _read_budget(role, "maximum_tool_calls", manifest_path, f"{prefix}.maximum_tool_calls", 6, 1, 12)
        _read_budget(role, "maximum_agent_passes", manifest_path, f"{prefix}.maximum_agent_passes", 2, 1, 3)
        _read_budget(role, "maximum_duration_seconds", manifest_path, f"{prefix}.maximum_duration_seconds", 90, 10, 120)
        _read_budget(role, "maximum_answer_characters", manifest_path, f"{prefix}.maximum_answer_characters", 12_000, 1_000, 20_000)
        metadata_characters += len(tool_name) + len(display_name) + len(description)

    _require_manifest(metadata_characters <= MAX_COPILOT_AGENT_METADATA_CHARACTERS, manifest_path, "copilot_agents", f"role names and descriptions must not exceed {MAX_COPILOT_AGENT_METADATA_CHARACTERS:,} characters in total")
    return CopilotAgentManifestSummary(True, len(roles), metadata_characters)


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
    parser.add_argument("--validate-only", action="store_true", help="Validate manifest.json and copilot_agents without building, packaging, or uploading.")
    parser.add_argument("--dotnet", default=os.environ.get("DOTNET_EXE", "dotnet"), help="dotnet executable used with --build")
    parser.add_argument("--upload-url", default=os.environ.get("COLORVISION_UPLOAD_URL", DEFAULT_UPLOAD_URL), help="Upload server base URL")
    parser.add_argument("--username", default=os.environ.get("COLORVISION_UPLOAD_USERNAME", DEFAULT_UPLOAD_USERNAME), help="Upload username")
    parser.add_argument("--password", default=os.environ.get("COLORVISION_UPLOAD_PASSWORD", DEFAULT_UPLOAD_PASSWORD), help="Upload password")
    args = parser.parse_args()

    project_file = Path(args.project_file).expanduser().resolve() if args.project_file else None
    explicit_src_dir = Path(args.src_dir).expanduser().resolve() if args.src_dir else None
    if not project_file and not explicit_src_dir and not args.plugin_root:
        raise ValueError("Provide --project-file, --src-dir, or --plugin-root.")

    inferred_src_dir = explicit_src_dir or (project_file.parent if project_file else Path(args.plugin_root).expanduser().resolve())
    plugin_root = resolve_plugin_root(
        inferred_src_dir,
        project_file,
        Path(args.plugin_root).expanduser().resolve() if args.plugin_root else None,
    )
    manifest_summary = validate_plugin_manifest(plugin_root / "manifest.json")
    if manifest_summary.manifest_present:
        print(f"Manifest validation passed: {plugin_root / 'manifest.json'}")
        print(f"Copilot role count: {manifest_summary.role_count}/{MAX_COPILOT_AGENT_ROLES}")
        print(f"Copilot advertised metadata: {manifest_summary.metadata_characters:,}/{MAX_COPILOT_AGENT_METADATA_CHARACTERS:,} characters")
    else:
        print(f"Manifest not present; legacy packaging compatibility remains active: {plugin_root / 'manifest.json'}")

    if args.validate_only:
        return

    if args.build:
        if not project_file:
            raise ValueError("--build requires --project-file.")
        build_project(project_file, args.configuration, args.framework, args.dotnet)

    src_dir = resolve_src_dir(explicit_src_dir, project_file, args.configuration, args.framework)
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

import argparse
import os
import re
import shutil
import subprocess
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable
from urllib.parse import quote

from tqdm import tqdm

VERSION_RE = re.compile(r"(\d+\.\d+\.\d+\.\d+)")
INSTALLER_EXTENSIONS = {".exe", ".msi", ".zip", ".rar"}
DEFAULT_PROJECT_NAME = "ColorVision"
DEFAULT_UPLOAD_URL = "http://xc213618.ddns.me:9998"
DEFAULT_UPLOAD_FOLDER = "ColorVision"
DEFAULT_CONNECT_TIMEOUT = 10
DEFAULT_READ_TIMEOUT = 1800
DEFAULT_UPLOAD_RETRIES = 3
DEFAULT_UPLOAD_CHUNK_SIZE = 1024 * 1024


@dataclass(frozen=True)
class RemoteUploadSettings:
    base_url: str
    folder_name: str
    username: str
    password: str
    enabled: bool = True
    connect_timeout: int = DEFAULT_CONNECT_TIMEOUT
    read_timeout: int = DEFAULT_READ_TIMEOUT
    max_retries: int = DEFAULT_UPLOAD_RETRIES
    chunk_size: int = DEFAULT_UPLOAD_CHUNK_SIZE


@dataclass(frozen=True)
class ProjectConfig:
    name: str
    msbuild_path: Path
    solution_path: Path
    advanced_installer_path: Path
    aip_path: Path
    setup_files_dir: Path
    latest_release_path: Path
    target_directory: Path
    changelog_src: Path
    changelog_dst: Path
    wechat_target_directory: Path
    baidu_target_directory: Path


def rebuild_project(msbuild_path: Path, solution_path: Path, advanced_installer_path: Path, aip_path: Path) -> bool:
    try:
        print(f"Running MSBuild: {msbuild_path} {solution_path}")
        subprocess.run(
            [str(msbuild_path), str(solution_path), "/p:Configuration=Release", "/p:Platform=x64"],
            check=True,
        )

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


def copy_with_progress(src: str | Path, dst: str | Path) -> Path:
    src_path = Path(src)
    dst_path = Path(dst)
    if dst_path.is_dir():
        dst_path = dst_path / src_path.name

    dst_path.parent.mkdir(parents=True, exist_ok=True)
    file_size = src_path.stat().st_size
    copied = 0
    chunk_size = DEFAULT_UPLOAD_CHUNK_SIZE

    with src_path.open("rb") as fsrc, dst_path.open("wb") as fdst:
        start_time = time.time()
        while True:
            chunk = fsrc.read(chunk_size)
            if not chunk:
                break
            fdst.write(chunk)
            copied += len(chunk)

            elapsed_time = max(time.time() - start_time, 0.001)
            progress = copied / file_size * 100 if file_size else 100.0
            speed = copied / elapsed_time

            remaining_bytes = max(file_size - copied, 0)
            remaining_time = remaining_bytes / speed if speed > 0 else 0
            remaining_time_hms = time.strftime("%H:%M:%S", time.gmtime(remaining_time))

            print(
                f"\rCopied {copied / (1024 * 1024):.2f} MB of {file_size / (1024 * 1024):.2f} MB "
                f"({progress:.2f}%) at {speed / (1024 * 1024):.2f} MB/s, "
                f"remaining time {remaining_time_hms}",
                end="",
            )

        print()

    shutil.copystat(src_path, dst_path, follow_symlinks=True)
    return dst_path


def read_version_file(path: str | Path) -> str:
    file_path = Path(path)
    try:
        return file_path.read_text(encoding="utf-8").strip() or "0.0.0.0"
    except FileNotFoundError:
        return "0.0.0.0"


def write_version_file(path: str | Path, version: str) -> None:
    file_path = Path(path)
    file_path.parent.mkdir(parents=True, exist_ok=True)
    file_path.write_text(version, encoding="utf-8")


def should_update_version(latest_version: str, current_version: str) -> bool:
    return version_tuple(latest_version) >= version_tuple(current_version)


def copy_if_exists(src: str | Path, dst: str | Path) -> bool:
    src_path = Path(src)
    dst_path = Path(dst)
    if not src_path.exists():
        print(f"Could not copy file to {dst_path}: source does not exist")
        return False
    dst_path.parent.mkdir(parents=True, exist_ok=True)
    try:
        shutil.copy2(src_path, dst_path)
        return True
    except OSError as exc:
        print(f"Could not copy file to {dst_path}: {exc}")
        return False


def build_upload_url(base_url: str, folder_name: str, file_name: str) -> str:
    encoded_parts = [quote(part, safe="") for part in folder_name.strip("/").split("/") if part]
    encoded_parts.append(quote(file_name, safe=""))
    return f"{base_url.rstrip('/')}/upload/{'/'.join(encoded_parts)}"


def _create_auth(settings: RemoteUploadSettings):
    if not settings.username or not settings.password:
        return None
    try:
        from requests.auth import HTTPBasicAuth
    except ImportError:
        return None
    return HTTPBasicAuth(settings.username, settings.password)


def upload_file(
    file_path: str | Path,
    settings: RemoteUploadSettings,
    *,
    session: Any | None = None,
    progress_factory: Callable[..., Any] = tqdm,
) -> bool:
    try:
        import requests
    except ImportError:
        print("Remote upload requires the requests package. Please install it first.")
        return False

    file_path = Path(file_path)
    file_size = file_path.stat().st_size
    upload_url = build_upload_url(settings.base_url, settings.folder_name, file_path.name)
    http_client = session or requests.Session()
    auth = _create_auth(settings)
    last_error = ""

    if settings.enabled and auth is None:
        print(
            "Remote upload is enabled but credentials are missing. "
            "Set COLORVISION_UPLOAD_USERNAME and COLORVISION_UPLOAD_PASSWORD, or use --skip-remote-upload."
        )
        return False

    for attempt in range(1, settings.max_retries + 1):
        try:
            with file_path.open("rb") as file_stream:
                with progress_factory(
                    total=file_size,
                    unit="B",
                    unit_scale=True,
                    desc=file_path.name,
                    ascii=True,
                ) as progress_bar:

                    def read_in_chunks(chunk_size: int = settings.chunk_size):
                        while True:
                            data = file_stream.read(chunk_size)
                            if not data:
                                break
                            progress_bar.update(len(data))
                            yield data

                    response = http_client.put(
                        upload_url,
                        data=read_in_chunks(),
                        auth=auth,
                        timeout=(settings.connect_timeout, settings.read_timeout),
                        headers={"Content-Type": "application/octet-stream"},
                    )

            if response.status_code == 201:
                print("File uploaded successfully")
                return True
            if response.status_code == 401:
                print(
                    "File upload failed: HTTP 401 Unauthorized. "
                    "Check the backend upload credentials in config.json and your environment variables."
                )
                return False

            last_error = f"HTTP {response.status_code}: {response.text.strip()}"
            print(f"File upload attempt {attempt} failed: {last_error}")
            if response.status_code < 500 and response.status_code not in {408, 429}:
                return False
        except requests.RequestException as exc:
            last_error = str(exc)
            print(f"File upload attempt {attempt} failed: {last_error}")

        if attempt < settings.max_retries:
            wait_seconds = min(2 ** (attempt - 1), 5)
            print(f"Retrying upload in {wait_seconds} second(s)...")
            time.sleep(wait_seconds)

    print(f"File upload failed after {settings.max_retries} attempt(s): {last_error}")
    return False


def publish_primary_release(
    latest_version: str,
    latest_release_path: str | Path,
    latest_file: str | Path,
    target_directory: str | Path,
    changelog_src: str | Path,
    changelog_dst: str | Path,
    remote_settings: RemoteUploadSettings,
    *,
    upload_func: Callable[[str | Path, RemoteUploadSettings], bool] = upload_file,
    copy_func: Callable[[str | Path, str | Path], Path] = copy_with_progress,
) -> bool:
    latest_release_path = Path(latest_release_path)
    latest_file = Path(latest_file)
    target_directory = Path(target_directory)
    changelog_src = Path(changelog_src)
    changelog_dst = Path(changelog_dst)

    current_version = read_version_file(latest_release_path)
    if not should_update_version(latest_version, current_version):
        print(f"The current version ({current_version}) is up to date.")
        return False

    publish_ok = False
    if remote_settings.enabled:
        publish_ok = upload_func(latest_file, remote_settings)
    else:
        try:
            copy_func(latest_file, target_directory)
            publish_ok = True
        except OSError as exc:
            print(f"Upload {latest_file}: {exc}")
            publish_ok = False

    if not publish_ok:
        print("Primary release publish failed; LATEST_RELEASE will not be updated.")
        return False

    copy_if_exists(changelog_src, changelog_dst)
    write_version_file(latest_release_path, latest_version)
    print(f"Updated the release version to {latest_version}")
    print(f"Upload {latest_file}")
    return True


def sync_local_release_copy(
    latest_version: str,
    latest_release_path: str | Path,
    latest_file: str | Path,
    target_directory: str | Path,
    changelog_src: str | Path,
    changelog_dst: str | Path,
) -> bool:
    latest_release_path = Path(latest_release_path)
    latest_file = Path(latest_file)
    target_directory = Path(target_directory)
    changelog_src = Path(changelog_src)
    changelog_dst = Path(changelog_dst)

    if not latest_release_path.exists():
        print(f"{target_directory}不存在，跳过更新。")
        return False

    current_version = read_version_file(latest_release_path)
    if not should_update_version(latest_version, current_version):
        print(f"The current version ({current_version}) is up to date.")
        return False

    try:
        copy_with_progress(latest_file, target_directory)
    except OSError as exc:
        print(f"Upload {latest_file}: {exc}")
        return False

    copy_if_exists(changelog_src, changelog_dst)
    write_version_file(latest_release_path, latest_version)
    print(f"Updated the release version to {latest_version}")
    print(f"Upload {latest_file}")
    return True


def compare_and_write_version(
    latest_version: str,
    latest_release_path: str | Path,
    latest_file: str | Path,
    changelog_src: str | Path,
    changelog_dst: str | Path,
    *,
    target_directory: str | Path,
    remote_settings: RemoteUploadSettings,
) -> bool:
    return publish_primary_release(
        latest_version,
        latest_release_path,
        latest_file,
        target_directory,
        changelog_src,
        changelog_dst,
        remote_settings,
    )


def compare_and_write_version_weixin(
    latest_version: str,
    latest_release_path: str | Path,
    latest_file: str | Path,
    target_directory: str | Path,
    changelog_src: str | Path,
    changelog_dst: str | Path,
) -> bool:
    return sync_local_release_copy(
        latest_version,
        latest_release_path,
        latest_file,
        target_directory,
        changelog_src,
        changelog_dst,
    )


def env_flag(name: str, default: bool) -> bool:
    value = os.environ.get(name)
    if value is None:
        return default
    return value.strip().lower() not in {"0", "false", "no", "off"}


def build_projects(base_path: Path) -> dict[str, ProjectConfig]:
    user_home = Path(os.environ.get("USERPROFILE") or os.path.expanduser("~"))
    documents_dir = user_home / "Documents"
    ai_project_dir = documents_dir / "Advanced Installer" / "Projects" / "ColorVision"

    return {
        "ColorVision": ProjectConfig(
            name="ColorVision",
            msbuild_path=Path(r"C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe"),
            solution_path=base_path / "build.sln",
            advanced_installer_path=base_path.parent / "AdvancedInstaller v19.7.1" / "App" / "ProgramFiles" / "bin" / "x86" / "AdvancedInstaller.com",
            aip_path=ai_project_dir / "ColorVision.aip",
            setup_files_dir=ai_project_dir / "Setup Files",
            latest_release_path=Path(r"H:\ColorVision\LATEST_RELEASE"),
            target_directory=Path(r"H:\ColorVision"),
            changelog_src=base_path / "CHANGELOG.md",
            changelog_dst=Path(r"H:\ColorVision\CHANGELOG.md"),
            wechat_target_directory=Path(r"C:\Users\Xin\Documents\WXWork\1688854819471931\WeDrive\视彩光电\视彩（上海）光电技术有限公司\视彩软件及工具简易教程\新版软件安装包\ColorVision"),
            baidu_target_directory=Path(r"D:\BaiduSyncdisk\ColorVision"),
        )
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build and publish the ColorVision installer")
    parser.add_argument("--project", default=DEFAULT_PROJECT_NAME, help="Project name to build")
    parser.add_argument("--skip-build", action="store_true", help="Skip MSBuild and Advanced Installer rebuild")
    parser.add_argument("--skip-remote-upload", action="store_true", help="Do not upload the installer through the backend /upload API")
    parser.add_argument("--upload-url", default=os.environ.get("COLORVISION_UPLOAD_URL", DEFAULT_UPLOAD_URL), help="Backend base URL for remote uploads")
    parser.add_argument("--upload-folder", default=os.environ.get("COLORVISION_UPLOAD_FOLDER", DEFAULT_UPLOAD_FOLDER), help="Remote folder path used by the backend upload endpoint")
    parser.add_argument("--upload-user", default=os.environ.get("COLORVISION_UPLOAD_USERNAME", ""), help="Backend upload username (prefer env var)")
    parser.add_argument("--upload-password", default=os.environ.get("COLORVISION_UPLOAD_PASSWORD", ""), help="Backend upload password (prefer env var)")
    parser.add_argument("--connect-timeout", type=int, default=DEFAULT_CONNECT_TIMEOUT, help="HTTP connect timeout in seconds")
    parser.add_argument("--read-timeout", type=int, default=DEFAULT_READ_TIMEOUT, help="HTTP read timeout in seconds")
    parser.add_argument("--upload-retries", type=int, default=DEFAULT_UPLOAD_RETRIES, help="Number of remote upload attempts")
    parser.add_argument("--latest-file", help="Explicit installer file to publish")
    parser.add_argument("--setup-files-dir", help="Override the Advanced Installer Setup Files directory")
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
    setup_files_dir = Path(args.setup_files_dir) if args.setup_files_dir else project.setup_files_dir
    remote_upload_enabled = env_flag("COLORVISION_REMOTE_UPLOAD", True) and not args.skip_remote_upload
    remote_settings = RemoteUploadSettings(
        base_url=args.upload_url,
        folder_name=args.upload_folder,
        username=args.upload_user,
        password=args.upload_password,
        enabled=remote_upload_enabled,
        connect_timeout=max(args.connect_timeout, 1),
        read_timeout=max(args.read_timeout, 1),
        max_retries=max(args.upload_retries, 1),
    )

    if remote_upload_enabled and (not remote_settings.username or not remote_settings.password):
        print(
            "Remote upload requires Basic Auth credentials. "
            "Set COLORVISION_UPLOAD_USERNAME and COLORVISION_UPLOAD_PASSWORD, "
            "or pass --skip-remote-upload to fall back to local copy."
        )
        return 2

    if not args.skip_build:
        if not rebuild_project(
            project.msbuild_path,
            project.solution_path,
            project.advanced_installer_path,
            project.aip_path,
        ):
            return 1

    latest_file = Path(args.latest_file) if args.latest_file else get_latest_file(setup_files_dir)
    print(setup_files_dir)
    print(f"latest_file: {latest_file}")

    if not latest_file or not latest_file.exists():
        print("No installer files found in the directory.")
        return 1

    latest_version = extract_version_from_filename(latest_file)
    if not latest_version:
        print("Could not extract the version from the filename.")
        return 1

    compare_and_write_version_weixin(
        latest_version,
        project.wechat_target_directory / "LATEST_RELEASE",
        latest_file,
        project.wechat_target_directory,
        project.changelog_src,
        project.wechat_target_directory / "CHANGELOG.md",
    )
    compare_and_write_version_weixin(
        latest_version,
        project.baidu_target_directory / "LATEST_RELEASE",
        latest_file,
        project.baidu_target_directory,
        project.changelog_src,
        project.baidu_target_directory / "CHANGELOG.md",
    )
    compare_and_write_version(
        latest_version,
        project.latest_release_path,
        latest_file,
        project.changelog_src,
        project.changelog_dst,
        target_directory=project.target_directory,
        remote_settings=remote_settings,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

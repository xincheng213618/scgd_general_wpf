import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import quote

from backend_client import (
    RemoteUploadSettings,
    create_http_session,
    get_requests_module,
    preflight_remote_upload,
    resolve_upload_base_url,
    resolve_upload_credentials,
    upload_file,
)


TOOL_DIRECTORY = "Tool/ProjectARVRPro.IntegrationDemo"
PROTOCOL_VERSION = "1.0"
DEFAULT_RELEASE_NOTES = (
    "提供 TCP 调用、标准结果 JSON 解析、CSV 导出、样例报文和可直接复制的 C# 契约源码。"
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build, validate, and publish the ProjectARVRPro integration demo."
    )
    parser.add_argument(
        "--validate-only",
        action="store_true",
        help="Build and validate the package without uploading anything.",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        help="Optionally retain the generated ZIP and latest.json in this directory.",
    )
    parser.add_argument("--upload-url", help="Override COLORVISION_UPLOAD_URL.")
    parser.add_argument("--username", help="Override COLORVISION_UPLOAD_USERNAME.")
    parser.add_argument("--password", help="Override COLORVISION_UPLOAD_PASSWORD.")
    parser.add_argument("--dotnet", default="dotnet", help="Path to dotnet.exe.")
    parser.add_argument("--release-notes", default=DEFAULT_RELEASE_NOTES)
    return parser.parse_args()


def read_version(project_path: Path) -> str:
    root = ET.parse(project_path).getroot()
    version = root.findtext(".//VersionPrefix")
    if not version or not version.strip():
        raise RuntimeError(f"VersionPrefix is missing from {project_path}")
    return version.strip()


def run_checked(command: list[str], *, cwd: Path) -> None:
    print(f"> {' '.join(command)}")
    subprocess.run(command, cwd=cwd, check=True)


def copy_demo_sources(project_dir: Path, package_dir: Path) -> None:
    for name in (
        "ProjectARVRPro.IntegrationDemo.csproj",
        "Program.cs",
        "MainWindow.xaml",
        "MainWindow.xaml.cs",
        "PackageIcon.png",
        "README.md",
        "CHANGELOG.md",
    ):
        source = project_dir / name
        if source.exists():
            shutil.copy2(source, package_dir / name)

    contracts_source = project_dir / "Contracts"
    contracts_target = package_dir / "Contracts"
    if contracts_target.exists():
        shutil.rmtree(contracts_target)
    shutil.copytree(contracts_source, contracts_target)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def create_zip(package_dir: Path, zip_path: Path) -> None:
    with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(package_dir.rglob("*")):
            if path.is_file():
                archive.write(path, path.relative_to(package_dir.parent))


def validate_zip(zip_path: Path, expected_root: str) -> None:
    with zipfile.ZipFile(zip_path, "r") as archive:
        bad_file = archive.testzip()
        if bad_file:
            raise RuntimeError(f"ZIP integrity check failed at {bad_file}")
        names = set(archive.namelist())

    required = {
        f"{expected_root}/ProjectARVRPro.IntegrationDemo.exe",
        f"{expected_root}/ProjectARVRPro.IntegrationDemo.csproj",
        f"{expected_root}/README.md",
        f"{expected_root}/CHANGELOG.md",
        f"{expected_root}/Samples/project-arvr-result.json",
        f"{expected_root}/Contracts/ObjectiveTestResult.cs",
    }
    missing = sorted(required - names)
    if missing:
        raise RuntimeError(f"ZIP is missing required files: {', '.join(missing)}")


def build_package(
    repo_root: Path,
    work_dir: Path,
    dotnet: str,
    release_notes: str,
) -> tuple[Path, Path, dict]:
    project_dir = repo_root / "Projects" / "ProjectARVRPro.IntegrationDemo"
    project_file = project_dir / "ProjectARVRPro.IntegrationDemo.csproj"
    plugin_file = repo_root / "Projects" / "ProjectARVRPro" / "ProjectARVRPro.csproj"
    demo_version = read_version(project_file)
    plugin_version = read_version(plugin_file)
    publish_dir = work_dir / "publish"
    package_dir = work_dir / "package" / "ProjectARVRPro.IntegrationDemo"
    smoke_dir = work_dir / "smoke-output"

    run_checked(
        [
            dotnet,
            "publish",
            str(project_file),
            "-f",
            "net48",
            "-c",
            "Release",
            "-p:Platform=x64",
            "--nologo",
            "-o",
            str(publish_dir),
        ],
        cwd=repo_root,
    )

    shutil.copytree(publish_dir, package_dir)
    copy_demo_sources(project_dir, package_dir)

    sample_path = package_dir / "Samples" / "project-arvr-result.json"
    demo_exe = package_dir / "ProjectARVRPro.IntegrationDemo.exe"
    run_checked(
        [
            str(demo_exe),
            "--parse-file",
            str(sample_path),
            "--output",
            str(smoke_dir),
        ],
        cwd=package_dir,
    )
    if not list(smoke_dir.glob("*_items.csv")):
        raise RuntimeError("Demo smoke test did not produce a CSV result.")

    package_info = {
        "schemaVersion": 1,
        "version": demo_version,
        "protocolVersion": PROTOCOL_VERSION,
        "verifiedProjectARVRProVersion": plugin_version,
        "requiresDotNetFramework": "4.8",
    }
    (package_dir / "package-info.json").write_text(
        json.dumps(package_info, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    zip_name = f"ProjectARVRPro.IntegrationDemo-{demo_version}.zip"
    zip_path = work_dir / zip_name
    create_zip(package_dir, zip_path)
    validate_zip(zip_path, package_dir.name)

    metadata = {
        **package_info,
        "fileName": zip_name,
        "downloadPath": f"/download/{TOOL_DIRECTORY}/{quote(zip_name)}",
        "sizeBytes": zip_path.stat().st_size,
        "sha256": sha256_file(zip_path),
        "publishedAtUtc": datetime.now(timezone.utc).isoformat(timespec="seconds").replace(
            "+00:00", "Z"
        ),
        "releaseNotes": release_notes.strip(),
    }
    metadata_path = work_dir / "latest.json"
    metadata_path.write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return zip_path, metadata_path, metadata


def copy_artifacts(zip_path: Path, metadata_path: Path, output_dir: Path) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(zip_path, output_dir / zip_path.name)
    shutil.copy2(metadata_path, output_dir / metadata_path.name)
    print(f"Artifacts retained at {output_dir.resolve()}")


def verify_remote_zip(session, base_url: str, metadata: dict) -> None:
    url = f"{base_url.rstrip('/')}{metadata['downloadPath']}"
    response = session.get(url, stream=True, timeout=(10, 1800))
    response.raise_for_status()
    digest = hashlib.sha256()
    size = 0
    for chunk in response.iter_content(chunk_size=1024 * 1024):
        if chunk:
            size += len(chunk)
            digest.update(chunk)
    if size != metadata["sizeBytes"]:
        raise RuntimeError(f"Remote ZIP size mismatch: expected {metadata['sizeBytes']}, got {size}")
    if digest.hexdigest().upper() != metadata["sha256"]:
        raise RuntimeError("Remote ZIP SHA-256 mismatch.")


def verify_remote_metadata(session, base_url: str, expected: dict) -> None:
    url = f"{base_url.rstrip('/')}/download/{TOOL_DIRECTORY}/latest.json"
    response = session.get(url, timeout=(10, 30))
    response.raise_for_status()
    actual = response.json()
    if actual != expected:
        raise RuntimeError("Remote latest.json does not match the generated release metadata.")


def publish(zip_path: Path, metadata_path: Path, metadata: dict, args: argparse.Namespace) -> None:
    requests = get_requests_module()
    if requests is None:
        raise RuntimeError("Publishing requires the requests package.")

    username, password = resolve_upload_credentials(args.username, args.password)
    settings = RemoteUploadSettings(
        base_url=resolve_upload_base_url(args.upload_url),
        folder_name=TOOL_DIRECTORY,
        username=username,
        password=password,
    )
    session = create_http_session(requests_module=requests)
    if not preflight_remote_upload(settings, session=session):
        raise RuntimeError("Backend upload preflight failed.")

    # Publish the immutable package and verify it before updating the latest pointer.
    if not upload_file(zip_path, settings, session=session):
        raise RuntimeError("Demo ZIP upload failed.")
    verify_remote_zip(session, settings.base_url, metadata)

    if not upload_file(metadata_path, settings, session=session):
        raise RuntimeError("latest.json upload failed.")
    verify_remote_metadata(session, settings.base_url, metadata)
    print(f"Published: {settings.base_url}{metadata['downloadPath']}")


def main() -> int:
    args = parse_args()
    repo_root = Path(__file__).resolve().parent.parent
    try:
        with tempfile.TemporaryDirectory(prefix="project-arvrpro-demo-") as temp_dir:
            work_dir = Path(temp_dir)
            zip_path, metadata_path, metadata = build_package(
                repo_root,
                work_dir,
                args.dotnet,
                args.release_notes,
            )
            print(
                f"Validated {zip_path.name}: {metadata['sizeBytes']} bytes, "
                f"SHA-256 {metadata['sha256']}"
            )
            if args.output_dir:
                copy_artifacts(zip_path, metadata_path, args.output_dir)
            if args.validate_only:
                print("Validation completed; no files were uploaded.")
            else:
                publish(zip_path, metadata_path, metadata, args)
        return 0
    except (OSError, RuntimeError, subprocess.CalledProcessError, zipfile.BadZipFile) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

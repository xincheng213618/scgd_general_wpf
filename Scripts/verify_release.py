from __future__ import annotations

import re
import shutil
import subprocess
import xml.etree.ElementTree as ET
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
from urllib.parse import quote

try:
    from .backend_client import create_http_session, resolve_upload_base_url
except ImportError:
    from backend_client import create_http_session, resolve_upload_base_url


CONNECT_TIMEOUT_SECONDS = 5
READ_TIMEOUT_SECONDS = 15


class ReleaseVerificationError(RuntimeError):
    pass


def read_version(repo_root: Path) -> str:
    root = ET.parse(repo_root / "Directory.Build.props").getroot()
    element = root.find(".//VersionPrefix")
    version = (element.text if element is not None else "").strip()
    if not re.fullmatch(r"\d+\.\d+\.\d+\.\d+", version):
        raise ReleaseVerificationError(f"Invalid VersionPrefix: {version!r}")
    return version


def get_release_paths(version: str) -> tuple[Path, Path]:
    user_home = Path.home()
    installer = (
        user_home
        / "Documents"
        / "Advanced Installer"
        / "Projects"
        / "ColorVision"
        / "Setup Files"
        / f"ColorVision-{version}.exe"
    )
    update = user_home / "Desktop" / "History" / "update" / f"ColorVision-Update-[{version}].cvx"
    return installer, update


def verify_signature(installer: Path) -> str:
    escaped_path = str(installer).replace("'", "''")
    command = (
        f"$signature = Get-AuthenticodeSignature -LiteralPath '{escaped_path}'; "
        "if ($signature.Status -ne 'Valid') { exit 1 }; "
        "Write-Output $signature.Status"
    )
    powershell = shutil.which("pwsh.exe") or shutil.which("powershell.exe")
    if not powershell:
        raise ReleaseVerificationError("PowerShell is required to verify the installer signature")
    result = subprocess.run(
        [powershell, "-NoProfile", "-NonInteractive", "-Command", command],
        capture_output=True,
        text=True,
        timeout=20,
        check=False,
    )
    status = result.stdout.strip()
    if result.returncode != 0 or status != "Valid":
        detail = result.stderr.strip() or status or f"exit code {result.returncode}"
        raise ReleaseVerificationError(f"Installer signature is not valid: {detail}")
    return status


def verify_latest_version(base_url: str, version: str) -> str:
    session = create_http_session()
    try:
        response = session.get(
            f"{base_url}/api/app/latest-version",
            timeout=(CONNECT_TIMEOUT_SECONDS, READ_TIMEOUT_SECONDS),
        )
        if response.status_code != 200:
            raise ReleaseVerificationError(f"latest-version returned HTTP {response.status_code}")
        try:
            actual = str(response.json().get("version", "")).strip()
        except (AttributeError, ValueError) as exc:
            raise ReleaseVerificationError("latest-version returned invalid JSON") from exc
        if actual != version:
            raise ReleaseVerificationError(
                f"latest-version mismatch: expected={version}, actual={actual or '<missing>'}"
            )
        return actual
    finally:
        session.close()


def verify_changelog(base_url: str, version: str) -> str:
    session = create_http_session()
    try:
        response = session.get(
            f"{base_url}/api/app/changelog",
            timeout=(CONNECT_TIMEOUT_SECONDS, READ_TIMEOUT_SECONDS),
        )
        if response.status_code != 200:
            raise ReleaseVerificationError(f"changelog returned HTTP {response.status_code}")
        first_heading = next(
            (line.strip() for line in response.text.splitlines() if line.startswith("## ")),
            "",
        )
        if not first_heading.startswith(f"## [{version}]"):
            raise ReleaseVerificationError(
                f"changelog mismatch: expected {version}, actual={first_heading or '<missing>'}"
            )
        return first_heading
    finally:
        session.close()


def verify_remote_size(base_url: str, endpoint: str, local_file: Path) -> str:
    expected_size = local_file.stat().st_size
    session = create_http_session()
    response = None
    try:
        response = session.get(
            f"{base_url}{endpoint}",
            headers={"Range": "bytes=0-0"},
            stream=True,
            timeout=(CONNECT_TIMEOUT_SECONDS, READ_TIMEOUT_SECONDS),
        )
        if response.status_code != 206:
            raise ReleaseVerificationError(
                f"{local_file.name} range request returned HTTP {response.status_code}"
            )
        content_range = response.headers.get("Content-Range", "")
        match = re.fullmatch(r"bytes 0-0/(\d+)", content_range)
        actual_size = int(match.group(1)) if match else -1
        if actual_size != expected_size:
            raise ReleaseVerificationError(
                f"{local_file.name} size mismatch: local={expected_size}, remote={actual_size}"
            )
        return f"{expected_size} bytes"
    finally:
        if response is not None:
            response.close()
        session.close()


def read_git_status(repo_root: Path) -> str:
    result = subprocess.run(
        ["git", "status", "--short", "--branch"],
        cwd=repo_root,
        capture_output=True,
        text=True,
        timeout=15,
        check=False,
    )
    if result.returncode != 0:
        raise ReleaseVerificationError(result.stderr.strip() or "git status failed")
    lines = result.stdout.splitlines()
    branch = lines[0] if lines else "## <unknown>"
    return f"{branch}; changes={max(len(lines) - 1, 0)}"


def main() -> int:
    repo_root = Path(__file__).resolve().parent.parent
    try:
        version = read_version(repo_root)
        installer, update = get_release_paths(version)
        missing = [str(path) for path in (installer, update) if not path.is_file()]
        if missing:
            raise ReleaseVerificationError("Missing release artifact(s): " + ", ".join(missing))

        base_url = resolve_upload_base_url()
        encoded_version = quote(version, safe=".")
        checks = {
            "installer signature": lambda: verify_signature(installer),
            "remote latest": lambda: verify_latest_version(base_url, version),
            "remote changelog": lambda: verify_changelog(base_url, version),
            "installer download": lambda: verify_remote_size(
                base_url,
                f"/api/app/releases/{encoded_version}/download",
                installer,
            ),
            "update download": lambda: verify_remote_size(
                base_url,
                f"/api/app/updates/{encoded_version}/download",
                update,
            ),
            "git status": lambda: read_git_status(repo_root),
        }
        results: dict[str, str] = {}
        with ThreadPoolExecutor(max_workers=len(checks)) as executor:
            futures = {executor.submit(check): name for name, check in checks.items()}
            for future in as_completed(futures):
                name = futures[future]
                results[name] = future.result()

        for name in checks:
            print(f"[verify] {name}: {results[name]}")
        print(f"[verify] Quick release verification passed: {version}")
        return 0
    except (OSError, ReleaseVerificationError, subprocess.SubprocessError) as exc:
        print(f"[verify] Release verification failed: {exc}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

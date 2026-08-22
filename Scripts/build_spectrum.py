"""
编译并发布 Spectrum 的独立 ZIP 和 ColorVision 插件包。

用法:
    py Scripts/build_spectrum.py [-c Release] [-f net10.0-windows]
    py Scripts/build_spectrum.py --upload --release-notes "变更说明"

输出:
    Release/Spectrum/Spectrum<version>.zip     独立安装包
    Release/Spectrum/Spectrum-<version>.cvxp   插件包（完整发布验证成功后删除）
"""

import argparse
import base64
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from typing import Any, Callable
from urllib.parse import quote

try:
    from .backend_client import (
        DEFAULT_CONNECT_TIMEOUT,
        DEFAULT_READ_TIMEOUT,
        RemoteUploadSettings,
        create_http_session,
        post_multipart_with_auth,
        preflight_remote_upload,
        resolve_upload_base_url,
        resolve_upload_credentials,
        upload_content as backend_upload_content,
        upload_file as backend_upload_file,
    )
    from .package_cvxp import synchronize_manifest_version
except ImportError:
    from backend_client import (
        DEFAULT_CONNECT_TIMEOUT,
        DEFAULT_READ_TIMEOUT,
        RemoteUploadSettings,
        create_http_session,
        post_multipart_with_auth,
        preflight_remote_upload,
        resolve_upload_base_url,
        resolve_upload_credentials,
        upload_content as backend_upload_content,
        upload_file as backend_upload_file,
    )
    from package_cvxp import synchronize_manifest_version


ALLOWED_RUNTIME_PREFIXES = (
    "runtimes/win/",
    "runtimes/win-x64/",
)

SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent

PROJECT_NAME = "Spectrum"
PROJECT_DIR = REPO_ROOT / "Plugins" / PROJECT_NAME
PROJECT_PATH = PROJECT_DIR / f"{PROJECT_NAME}.csproj"
BUILD_DIR = REPO_ROOT / "Release" / PROJECT_NAME

SPECTRUM_PUBLISH_PATH = "/api/tool/spectrum/publish"
SPECTRUM_LATEST_PATH = "/api/tool/spectrum/latest"
SPECTRUM_LATEST_VERSION_PATH = "/api/tool/spectrum/latest-version"
SPECTRUM_DOWNLOAD_PATH = "/api/tool/spectrum/download"
SPECTRUM_SIGNING_CERTIFICATE_THUMBPRINT = "0AFB92F7CF8B33F13C931B327B1BE5DC773F30FA"
SPECTRUM_SIGNING_CERTIFICATE_COMMON_NAME = "xincheng"
FOUR_PART_VERSION_PATTERN = re.compile(r"^[0-9]+(?:\.[0-9]+){3}$")

_CVXP_EXTRA_FILES = ["README.md", "CHANGELOG.md", "manifest.json", "PackageIcon.png"]

_FILE_EXCLUDE = {
    "toupcam.dll",
    "nncam.dll",
    "ikapc.dll",
    "oracle.manageddataaccess.dll",
    "scgdcamlayer.dll",
    "scgdprocess.dll",
    "scgddataprocess.dll",
    "scgdmilcam.dll",
    "scbase.dll",
    "cvcalibration.dll",
    "opencv_videoio_ffmpeg4110_64.dll",
    "opencv_videoio_ffmpeg4130_64.dll",
    "opencv_videoio_ffmpeg4140_64.dll",
}

_POWERSHELL_SIGN_SCRIPT = r"""
$ErrorActionPreference = 'Stop'
$inputPath = [Environment]::GetEnvironmentVariable('SPECTRUM_SIGN_INPUT', 'Process')
$outputPath = [Environment]::GetEnvironmentVariable('SPECTRUM_SIGN_OUTPUT', 'Process')
$expectedThumbprint = [Environment]::GetEnvironmentVariable('SPECTRUM_SIGN_THUMBPRINT', 'Process')
$expectedCommonName = [Environment]::GetEnvironmentVariable('SPECTRUM_SIGN_COMMON_NAME', 'Process')

$normalizedThumbprint = ($expectedThumbprint -replace '\s', '').ToUpperInvariant()
$certificate = Get-ChildItem -Path Cert:\CurrentUser\My |
    Where-Object { (($_.Thumbprint -replace '\s', '').ToUpperInvariant()) -eq $normalizedThumbprint } |
    Select-Object -First 1
if ($null -eq $certificate) {
    throw "Spectrum signing certificate was not found in Cert:\CurrentUser\My."
}
if (-not $certificate.HasPrivateKey) {
    throw "Spectrum signing certificate does not have an accessible private key."
}
$actualCommonName = $certificate.GetNameInfo(
    [System.Security.Cryptography.X509Certificates.X509NameType]::SimpleName,
    $false
)
if ($actualCommonName -ne $expectedCommonName) {
    throw "Spectrum signing certificate common name does not match the release policy."
}

$rsa = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($certificate)
if ($null -eq $rsa) {
    throw "Spectrum signing certificate does not expose an RSA private key."
}
try {
    $manifestBytes = [System.IO.File]::ReadAllBytes($inputPath)
    $signatureBytes = $rsa.SignData(
        $manifestBytes,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
    )
    [System.IO.File]::WriteAllBytes($outputPath, $signatureBytes)
}
finally {
    $rsa.Dispose()
}
"""


class SpectrumReleaseError(RuntimeError):
    """Spectrum 发布没有满足完整成功契约。"""


@dataclass(frozen=True)
class SignedRelease:
    manifest: dict[str, Any]
    manifest_bytes: bytes
    signature_bytes: bytes


def normalize_archive_relative_path(path_value: str) -> str:
    return PurePosixPath(path_value.replace("\\", "/")).as_posix()


def should_keep_runtime_path(path_value: str) -> bool:
    normalized = normalize_archive_relative_path(path_value).lower()
    if not normalized.startswith("runtimes/"):
        return True

    return normalized.startswith(ALLOWED_RUNTIME_PREFIXES)


def validate_four_part_version(version: str) -> str:
    version = version.strip()
    if FOUR_PART_VERSION_PATTERN.fullmatch(version) is None:
        raise SpectrumReleaseError(f"Spectrum 版本必须是四段数字版本号，实际为: {version!r}")
    if any(int(part) > 65535 for part in version.split(".")):
        raise SpectrumReleaseError(f"Spectrum 四段版本号的每一段都必须在 0-65535，实际为: {version!r}")
    return version


def get_version_from_pe(pe_path: str | Path) -> str | None:
    """从 PE 文件的 FileVersion 字段读取版本号（去掉 +hash 后缀）。"""
    try:
        import pefile

        pe = pefile.PE(str(pe_path))
        try:
            for fileinfo in pe.FileInfo:
                for entry in fileinfo:
                    if entry.Key == b"StringFileInfo":
                        for string_table in entry.StringTable:
                            version = string_table.entries.get(b"FileVersion")
                            if version:
                                version_text = version.decode("utf-8").strip()
                                match = re.match(r"^([0-9.]+)", version_text)
                                return match.group(1) if match else version_text
        finally:
            pe.close()
    except Exception as exc:
        print(f"读取版本号失败: {exc}")
    return None


def build_project(configuration: str, framework: str) -> str | None:
    """使用 dotnet publish 编译 Spectrum 项目，返回输出目录。"""
    output_dir = BUILD_DIR / framework
    command = [
        "dotnet",
        "publish",
        str(PROJECT_PATH),
        "-c",
        configuration,
        "-f",
        framework,
        "-p:Platform=x64",
        "--self-contained",
        "false",
        "-o",
        str(output_dir),
    ]
    print(f"编译命令: {' '.join(command)}")
    try:
        subprocess.run(command, check=True, cwd=REPO_ROOT)
        print("编译完成。")
        return str(output_dir)
    except subprocess.CalledProcessError as exc:
        print(f"编译失败: {exc}")
        return None


def _should_include(rel_path: str) -> bool:
    """判断文件是否应被打入独立 ZIP 包。"""
    normalized = normalize_archive_relative_path(rel_path).lower()
    filename = PurePosixPath(normalized).name

    if normalized.endswith(".pdb"):
        return False
    if filename in _FILE_EXCLUDE:
        return False
    return should_keep_runtime_path(normalized)


def _collect_files(folder_path: str | Path) -> list[tuple[str, str]]:
    """收集需要打包的文件，返回（绝对路径，相对 POSIX 路径）。"""
    folder_path = str(folder_path)
    result: list[tuple[str, str]] = []
    for root, directories, files in os.walk(folder_path):
        directories.sort()
        for file_name in sorted(files):
            absolute_path = os.path.join(root, file_name)
            relative_path = normalize_archive_relative_path(os.path.relpath(absolute_path, folder_path))
            if _should_include(relative_path):
                result.append((absolute_path, relative_path))
    return sorted(result, key=lambda item: item[1].lower())


def zip_folder(folder_path: str | Path, zip_path: str | Path) -> None:
    """将编译产物目录打包为 ZIP（已过滤）。"""
    zip_path = Path(zip_path)
    zip_path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as archive:
        for absolute_path, relative_path in _collect_files(folder_path):
            archive.write(absolute_path, relative_path)
    print(f"成功打包: {zip_path}")


def _find_extra_files() -> list[Path]:
    """在 Plugins/ 和 Plugins/Spectrum/ 目录下查找插件包额外文件。"""
    result: list[Path] = []
    for search_dir in (REPO_ROOT / "Plugins", PROJECT_DIR):
        for file_name in _CVXP_EXTRA_FILES:
            file_path = search_dir / file_name
            if file_path.is_file():
                result.append(file_path)
    return result


def build_cvxp(src_dir: str | Path, ref_dir: str | Path, cvxp_path: str | Path) -> bool:
    """对比插件输出与宿主输出，将差异文件打包为 .cvxp。"""
    src_dir = Path(src_dir)
    ref_dir = Path(ref_dir)
    cvxp_path = Path(cvxp_path)
    if not ref_dir.is_dir():
        print(f"ColorVision 参考目录不存在，跳过 cvxp 打包: {ref_dir}")
        return False

    temp_dir = BUILD_DIR / "_cvxp_temp"
    if temp_dir.exists():
        shutil.rmtree(temp_dir)

    project_path = temp_dir / PROJECT_NAME
    project_path.mkdir(parents=True)
    stripped_files: list[str] = []

    try:
        for file_path in sorted(path for path in src_dir.rglob("*") if path.is_file()):
            if file_path.suffix.lower() == ".pdb":
                continue
            relative_path = normalize_archive_relative_path(str(file_path.relative_to(src_dir)))
            if not should_keep_runtime_path(relative_path):
                continue
            reference_file = ref_dir / Path(relative_path)

            if not reference_file.exists():
                destination = project_path / Path(relative_path)
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(file_path, destination)
            else:
                stripped_files.append(relative_path)

        stripped_files.sort(key=str.lower)
        with (project_path / "stripped_files.json").open("w", encoding="utf-8") as stream:
            json.dump(stripped_files, stream, indent=2, ensure_ascii=False)
        print(f"stripped_files.json: {len(stripped_files)} entries")

        for extra_file in _find_extra_files():
            shutil.copy2(extra_file, project_path / extra_file.name)

        cvxp_path.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(cvxp_path, "w", zipfile.ZIP_DEFLATED) as archive:
            for file_path in sorted(path for path in temp_dir.rglob("*") if path.is_file()):
                archive.write(file_path, file_path.relative_to(temp_dir).as_posix())
    finally:
        shutil.rmtree(temp_dir, ignore_errors=True)

    print(f"成功打包: {cvxp_path}")
    return True


def synchronize_spectrum_manifest_versions(output_dir: str | Path, version: str) -> None:
    """让源码清单和已 publish 的清单都与主 EXE 四段版本同步。"""
    for manifest_path in (PROJECT_DIR / "manifest.json", Path(output_dir) / "manifest.json"):
        if not manifest_path.is_file():
            continue
        updated, previous_version = synchronize_manifest_version(manifest_path, version)
        if updated:
            print(f"已同步清单版本: {manifest_path} ({previous_version or '<missing>'} -> {version})")


def sha256_file(file_path: str | Path) -> str:
    digest = hashlib.sha256()
    with Path(file_path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def canonical_json_bytes(value: dict[str, Any]) -> bytes:
    """生成服务端和客户端共同校验的 canonical UTF-8 JSON。"""
    return json.dumps(
        value,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")


def build_release_manifest(
    version: str,
    release_notes: str,
    package_path: str | Path,
    *,
    published_at_utc: str | None = None,
) -> dict[str, Any]:
    version = validate_four_part_version(version)
    package_path = Path(package_path)
    if not package_path.is_file():
        raise SpectrumReleaseError(f"独立 ZIP 不存在: {package_path}")
    expected_names = {f"{PROJECT_NAME}{version}.zip", f"{PROJECT_NAME}-{version}.zip"}
    if package_path.name not in expected_names:
        raise SpectrumReleaseError(
            f"独立 ZIP 文件名必须是 {PROJECT_NAME}{version}.zip 或 {PROJECT_NAME}-{version}.zip"
        )
    if not isinstance(release_notes, str):
        raise SpectrumReleaseError("发布说明必须是字符串。")

    published_at_utc = published_at_utc or datetime.now(timezone.utc).isoformat(
        timespec="seconds"
    ).replace("+00:00", "Z")
    return {
        "schemaVersion": 1,
        "productId": PROJECT_NAME,
        "version": version,
        "publishedAtUtc": published_at_utc,
        "releaseNotes": release_notes,
        "package": {
            "fileName": package_path.name,
            "size": package_path.stat().st_size,
            "sha256": sha256_file(package_path),
        },
    }


def sign_manifest_bytes(
    manifest_bytes: bytes,
    *,
    thumbprint: str = SPECTRUM_SIGNING_CERTIFICATE_THUMBPRINT,
    common_name: str = SPECTRUM_SIGNING_CERTIFICATE_COMMON_NAME,
    powershell_executable: str | None = None,
    runner: Callable[..., Any] = subprocess.run,
) -> bytes:
    """用 CurrentUser/My 中指定 RSA 证书对原始清单字节做 PKCS1-SHA256 签名。"""
    if not manifest_bytes:
        raise SpectrumReleaseError("不能签名空发布清单。")

    powershell_executable = (
        powershell_executable
        or shutil.which("pwsh.exe")
        or shutil.which("pwsh")
        or shutil.which("powershell.exe")
        or shutil.which("powershell")
    )
    if not powershell_executable:
        raise SpectrumReleaseError("找不到 Windows PowerShell，无法签名 Spectrum 发布清单。")

    with tempfile.TemporaryDirectory(prefix="spectrum-sign-") as temp_dir_name:
        temp_dir = Path(temp_dir_name)
        manifest_path = temp_dir / "manifest.json"
        signature_path = temp_dir / "manifest.sig"
        manifest_path.write_bytes(manifest_bytes)

        signing_environment = os.environ.copy()
        signing_environment.update({
            "SPECTRUM_SIGN_INPUT": str(manifest_path),
            "SPECTRUM_SIGN_OUTPUT": str(signature_path),
            "SPECTRUM_SIGN_THUMBPRINT": thumbprint,
            "SPECTRUM_SIGN_COMMON_NAME": common_name,
        })
        completed = runner(
            [
                powershell_executable,
                "-NoLogo",
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                _POWERSHELL_SIGN_SCRIPT,
            ],
            capture_output=True,
            text=True,
            env=signing_environment,
            check=False,
        )
        if completed.returncode != 0:
            error_text = (completed.stderr or completed.stdout or "未知错误").strip()
            if len(error_text) > 600:
                error_text = error_text[-600:]
            raise SpectrumReleaseError(f"Spectrum 发布清单签名失败: {error_text}")
        if not signature_path.is_file():
            raise SpectrumReleaseError("PowerShell 签名命令成功返回，但没有生成签名字节。")

        signature_bytes = signature_path.read_bytes()
        if not signature_bytes:
            raise SpectrumReleaseError("PowerShell 生成了空签名。")
        return signature_bytes


def create_signed_release(
    version: str,
    release_notes: str,
    package_path: str | Path,
    *,
    published_at_utc: str | None = None,
    signer: Callable[[bytes], bytes] | None = None,
) -> SignedRelease:
    manifest = build_release_manifest(
        version,
        release_notes,
        package_path,
        published_at_utc=published_at_utc,
    )
    manifest_bytes = canonical_json_bytes(manifest)
    signature_bytes = (signer or sign_manifest_bytes)(manifest_bytes)
    if not isinstance(signature_bytes, bytes) or not signature_bytes:
        raise SpectrumReleaseError("发布清单签名器没有返回有效签名字节。")
    return SignedRelease(manifest, manifest_bytes, signature_bytes)


def _response_json(response: Any, description: str) -> dict[str, Any]:
    try:
        payload = response.json()
    except (TypeError, ValueError) as exc:
        raise SpectrumReleaseError(f"{description} 返回的不是 JSON 对象。") from exc
    if not isinstance(payload, dict):
        raise SpectrumReleaseError(f"{description} 返回的不是 JSON 对象。")
    return payload


def _response_error_text(response: Any) -> str:
    text = str(getattr(response, "text", "")).strip()
    return text[:600] if text else "<empty response>"


def publish_standalone_release(
    version: str,
    release_notes: str,
    package_path: str | Path,
    signed_release: SignedRelease,
    *,
    base_url: str,
    username: str,
    password: str,
    session: Any,
) -> SignedRelease:
    """调用专用接口；服务端在保存包和版本记录后才原子提交 latest。"""
    publish_url = f"{base_url.rstrip('/')}{SPECTRUM_PUBLISH_PATH}"
    package_path = Path(package_path)
    with package_path.open("rb") as package_stream:
        response = post_multipart_with_auth(
            publish_url,
            data={"Version": version, "ReleaseNotes": release_notes},
            files={
                "Manifest": ("manifest.json", signed_release.manifest_bytes, "application/json"),
                "Signature": ("manifest.sig", signed_release.signature_bytes, "application/octet-stream"),
                "Package": (package_path.name, package_stream, "application/zip"),
            },
            username=username,
            password=password,
            session=session,
        )

    if response.status_code not in {200, 201}:
        raise SpectrumReleaseError(
            f"独立 Spectrum 发布失败: HTTP {response.status_code} {_response_error_text(response)}"
        )

    payload = _response_json(response, "独立 Spectrum 发布接口")
    latest = payload.get("latest")
    release = payload.get("release")
    if not isinstance(latest, dict):
        raise SpectrumReleaseError("独立 Spectrum 发布响应缺少 latest。")
    returned_manifest_bytes = _decode_base64_field(latest, "manifestBase64", "独立 Spectrum 发布响应")
    returned_signature_bytes = _decode_base64_field(latest, "signatureBase64", "独立 Spectrum 发布响应")
    try:
        returned_manifest = json.loads(returned_manifest_bytes.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise SpectrumReleaseError("独立 Spectrum 发布响应中的 latest manifest 不是 UTF-8 JSON。") from exc
    if not isinstance(returned_manifest, dict) or canonical_json_bytes(returned_manifest) != returned_manifest_bytes:
        raise SpectrumReleaseError("独立 Spectrum 发布响应中的 latest manifest 不是 canonical JSON。")

    created = payload.get("created")
    if response.status_code == 201:
        if created is not True:
            raise SpectrumReleaseError("独立 Spectrum 新建发布响应缺少 created=true。")
        if returned_manifest_bytes != signed_release.manifest_bytes:
            raise SpectrumReleaseError("独立 Spectrum 新建发布响应中的 manifest 与本地清单不一致。")
        if returned_signature_bytes != signed_release.signature_bytes:
            raise SpectrumReleaseError("独立 Spectrum 新建发布响应中的 signature 与本地签名不一致。")
        effective_release = signed_release
    else:
        if created is not False:
            raise SpectrumReleaseError("独立 Spectrum 幂等发布响应缺少 created=false。")
        # 重跑同一包会产生新的发布时间和签名。服务保留首次签名清单；只有发布时间可沿用，
        # 版本、说明和包身份必须仍与本次请求完全一致，才能继续补交插件 latest。
        expected_without_timestamp = dict(signed_release.manifest)
        returned_without_timestamp = dict(returned_manifest)
        expected_without_timestamp.pop("publishedAtUtc", None)
        returned_without_timestamp.pop("publishedAtUtc", None)
        if returned_without_timestamp != expected_without_timestamp:
            raise SpectrumReleaseError("独立 Spectrum 已有版本与本次清单不一致，不能按幂等重试。")
        published_at_utc = returned_manifest.get("publishedAtUtc")
        if not isinstance(published_at_utc, str):
            raise SpectrumReleaseError("独立 Spectrum 已有版本缺少有效发布时间。")
        try:
            parsed_timestamp = datetime.fromisoformat(published_at_utc.replace("Z", "+00:00"))
        except ValueError as exc:
            raise SpectrumReleaseError("独立 Spectrum 已有版本的发布时间无效。") from exc
        if parsed_timestamp.tzinfo is None or parsed_timestamp.utcoffset() != timezone.utc.utcoffset(None):
            raise SpectrumReleaseError("独立 Spectrum 已有版本的发布时间不是 UTC。")
        effective_release = SignedRelease(
            returned_manifest,
            returned_manifest_bytes,
            returned_signature_bytes,
        )

    package = effective_release.manifest["package"]
    if not isinstance(release, dict) or release.get("version") != version:
        raise SpectrumReleaseError("独立 Spectrum 发布响应中的版本不一致。")
    if release.get("fileName") != package["fileName"]:
        raise SpectrumReleaseError("独立 Spectrum 发布响应中的包文件名不一致。")
    if release.get("size") != package["size"] or release.get("sha256") != package["sha256"]:
        raise SpectrumReleaseError("独立 Spectrum 发布响应中的包大小或 SHA-256 不一致。")
    if release.get("publishedAtUtc") != effective_release.manifest["publishedAtUtc"]:
        raise SpectrumReleaseError("独立 Spectrum 发布响应中的发布时间不一致。")
    if release.get("releaseNotes") != release_notes:
        raise SpectrumReleaseError("独立 Spectrum 发布响应中的发布说明不一致。")

    print(f"独立 Spectrum 发布接口已提交: {version} (HTTP {response.status_code})")
    return effective_release


def verify_plugin_latest(version: str, *, base_url: str, session: Any) -> None:
    url = f"{base_url.rstrip('/')}/api/plugins/{quote(PROJECT_NAME, safe='')}/latest-version"
    response = session.get(
        url,
        timeout=(DEFAULT_CONNECT_TIMEOUT, min(DEFAULT_READ_TIMEOUT, 15)),
    )
    if response.status_code != 200:
        raise SpectrumReleaseError(
            f"Spectrum 插件 latest 验证失败: HTTP {response.status_code} {_response_error_text(response)}"
        )
    remote_version = response.text.strip()
    if remote_version != version:
        raise SpectrumReleaseError(
            f"Spectrum 插件 latest 版本不一致: expected={version}, actual={remote_version or '<empty>'}"
        )
    print(f"Spectrum 插件 latest 已验证: {version}")


def verify_plugin_package(
    version: str,
    cvxp_path: str | Path,
    *,
    base_url: str,
    session: Any,
) -> None:
    cvxp_path = Path(cvxp_path)
    expected_size = cvxp_path.stat().st_size
    expected_sha256 = sha256_file(cvxp_path)
    url = f"{base_url.rstrip('/')}/api/packages/{quote(PROJECT_NAME, safe='')}/{quote(version, safe='.')}"
    response = session.get(
        url,
        stream=True,
        timeout=(DEFAULT_CONNECT_TIMEOUT, DEFAULT_READ_TIMEOUT),
    )
    try:
        if response.status_code != 200:
            raise SpectrumReleaseError(
                f"Spectrum 插件包验证失败: HTTP {response.status_code} {_response_error_text(response)}"
            )
        digest = hashlib.sha256()
        downloaded_size = 0
        for chunk in response.iter_content(chunk_size=1024 * 1024):
            if not chunk:
                continue
            downloaded_size += len(chunk)
            digest.update(chunk)
    finally:
        response.close()

    if downloaded_size != expected_size:
        raise SpectrumReleaseError(
            f"Spectrum 插件包大小不一致: expected={expected_size}, actual={downloaded_size}"
        )
    if digest.hexdigest() != expected_sha256:
        raise SpectrumReleaseError("Spectrum 插件包 SHA-256 与本地 .cvxp 不一致。")
    print(f"Spectrum 插件包大小和 SHA-256 已验证: {version}")


def _decode_base64_field(payload: dict[str, Any], field: str, description: str) -> bytes:
    value = payload.get(field)
    if not isinstance(value, str) or not value:
        raise SpectrumReleaseError(f"{description} 缺少 {field}。")
    try:
        return base64.b64decode(value, validate=True)
    except (ValueError, base64.binascii.Error) as exc:
        raise SpectrumReleaseError(f"{description} 的 {field} 不是有效 Base64。") from exc


def verify_standalone_release(
    version: str,
    package_path: str | Path,
    signed_release: SignedRelease,
    *,
    base_url: str,
    session: Any,
) -> None:
    base_url = base_url.rstrip("/")
    short_timeout = (DEFAULT_CONNECT_TIMEOUT, min(DEFAULT_READ_TIMEOUT, 15))

    latest_response = session.get(f"{base_url}{SPECTRUM_LATEST_PATH}", timeout=short_timeout)
    if latest_response.status_code != 200:
        raise SpectrumReleaseError(
            f"独立 Spectrum latest 验证失败: HTTP {latest_response.status_code} "
            f"{_response_error_text(latest_response)}"
        )
    latest_payload = _response_json(latest_response, "独立 Spectrum latest")
    remote_manifest = _decode_base64_field(latest_payload, "manifestBase64", "独立 Spectrum latest")
    remote_signature = _decode_base64_field(latest_payload, "signatureBase64", "独立 Spectrum latest")
    if remote_manifest != signed_release.manifest_bytes:
        raise SpectrumReleaseError("独立 Spectrum latest 的原始清单字节与本地不一致。")
    if remote_signature != signed_release.signature_bytes:
        raise SpectrumReleaseError("独立 Spectrum latest 的签名字节与本地不一致。")

    version_response = session.get(
        f"{base_url}{SPECTRUM_LATEST_VERSION_PATH}",
        timeout=short_timeout,
    )
    if version_response.status_code != 200:
        raise SpectrumReleaseError(
            f"独立 Spectrum latest-version 验证失败: HTTP {version_response.status_code} "
            f"{_response_error_text(version_response)}"
        )
    version_payload = _response_json(version_response, "独立 Spectrum latest-version")
    if version_payload.get("version") != version:
        raise SpectrumReleaseError(
            "独立 Spectrum latest-version 不一致: "
            f"expected={version}, actual={version_payload.get('version')!r}"
        )

    package_path = Path(package_path)
    expected_size = package_path.stat().st_size
    expected_sha256 = sha256_file(package_path)
    download_url = f"{base_url}{SPECTRUM_DOWNLOAD_PATH}/{quote(version, safe='.')}"
    download_response = session.get(
        download_url,
        headers={"Range": "bytes=0-"},
        stream=True,
        timeout=(DEFAULT_CONNECT_TIMEOUT, DEFAULT_READ_TIMEOUT),
    )
    try:
        if download_response.status_code != 206:
            raise SpectrumReleaseError(
                f"独立 Spectrum Range 下载验证失败: HTTP {download_response.status_code} "
                f"{_response_error_text(download_response)}"
            )
        content_range = download_response.headers.get("Content-Range", "")
        expected_content_range = f"bytes 0-{expected_size - 1}/{expected_size}"
        if content_range != expected_content_range:
            raise SpectrumReleaseError(
                "独立 Spectrum Range 响应范围不一致: "
                f"expected={expected_content_range!r}, actual={content_range!r}"
            )

        digest = hashlib.sha256()
        downloaded_size = 0
        for chunk in download_response.iter_content(chunk_size=1024 * 1024):
            if not chunk:
                continue
            downloaded_size += len(chunk)
            digest.update(chunk)
    finally:
        download_response.close()

    if downloaded_size != expected_size:
        raise SpectrumReleaseError(
            f"独立 Spectrum 下载大小不一致: expected={expected_size}, actual={downloaded_size}"
        )
    if digest.hexdigest() != expected_sha256:
        raise SpectrumReleaseError("独立 Spectrum 下载 SHA-256 与本地 ZIP 不一致。")
    print(f"独立 Spectrum latest、latest-version、Range 下载大小和 SHA-256 已验证: {version}")


def publish_built_artifacts(
    version: str,
    release_notes: str,
    zip_path: str | Path,
    cvxp_path: str | Path,
    signed_release: SignedRelease,
    *,
    base_url: str | None = None,
    username: str | None = None,
    password: str | None = None,
    session: Any | None = None,
) -> None:
    """先上传不可见的插件包，再发布独立包，最后提交插件 latest，之后只读验收。"""
    resolved_base_url = resolve_upload_base_url(base_url)
    resolved_username, resolved_password = resolve_upload_credentials(username, password)
    if not resolved_username or not resolved_password:
        raise SpectrumReleaseError(
            "正式发布需要 COLORVISION_UPLOAD_USERNAME 和 COLORVISION_UPLOAD_PASSWORD。"
        )

    http_session = session or create_http_session()
    plugin_settings = RemoteUploadSettings(
        base_url=resolved_base_url,
        folder_name=f"Plugins/{PROJECT_NAME}",
        username=resolved_username,
        password=resolved_password,
    )
    if not preflight_remote_upload(plugin_settings, session=http_session):
        raise SpectrumReleaseError("Spectrum 发布前后端预检失败。")
    if not backend_upload_file(cvxp_path, plugin_settings, session=http_session):
        raise SpectrumReleaseError("Spectrum 插件 .cvxp 上传失败。")
    # The package file is still invisible until LATEST_RELEASE changes. Verify its bytes now so
    # a truncated upload can never be advertised to plugin clients.
    verify_plugin_package(
        version,
        cvxp_path,
        base_url=resolved_base_url,
        session=http_session,
    )

    # 独立服务在同一次请求中保存 ZIP/版本记录并原子替换独立 latest。
    effective_release = publish_standalone_release(
        version,
        release_notes,
        zip_path,
        signed_release,
        base_url=resolved_base_url,
        username=resolved_username,
        password=resolved_password,
        session=http_session,
    )
    # 最后提交插件可见版本；独立发布失败时不会提前暴露新插件版本。
    if not backend_upload_content(
        version,
        "LATEST_RELEASE",
        plugin_settings,
        session=http_session,
    ):
        raise SpectrumReleaseError("Spectrum 插件 LATEST_RELEASE 上传失败。")

    verify_plugin_latest(version, base_url=resolved_base_url, session=http_session)
    verify_standalone_release(
        version,
        zip_path,
        effective_release,
        base_url=resolved_base_url,
        session=http_session,
    )


def _create_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="编译并打包/发布 Spectrum")
    parser.add_argument("-c", "--configuration", default="Release", help="编译配置 (默认: Release)")
    parser.add_argument(
        "-f",
        "--framework",
        default="net10.0-windows",
        help="目标框架 (默认: net10.0-windows)",
    )
    parser.add_argument("--no-zip", action="store_true", help="仅本地打包时跳过独立 ZIP")
    parser.add_argument("--no-cvxp", action="store_true", help="仅本地打包时跳过插件 .cvxp")
    parser.add_argument("--upload", action="store_true", help="签名并发布 ZIP 和 .cvxp 双通道")
    parser.add_argument("--release-notes", default="", help="独立发布清单和网站显示的发布说明")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _create_argument_parser().parse_args(argv)
    if args.upload and (args.no_zip or args.no_cvxp):
        print("正式 --upload 必须同时生成独立 ZIP 和插件 .cvxp。", file=sys.stderr)
        return 1

    output_dir_text = build_project(args.configuration, args.framework)
    if not output_dir_text:
        print("编译失败，终止。", file=sys.stderr)
        return 1

    output_dir = Path(output_dir_text)
    zip_path: Path | None = None
    cvxp_path: Path | None = None
    try:
        exe_path = output_dir / f"{PROJECT_NAME}.exe"
        version = get_version_from_pe(exe_path)
        if not version:
            raise SpectrumReleaseError("未能读取 Spectrum.exe 四段文件版本。")
        version = validate_four_part_version(version)
        print(f"版本号: {version}")

        if not args.no_cvxp:
            synchronize_spectrum_manifest_versions(output_dir, version)

        if not args.no_zip:
            zip_path = BUILD_DIR / f"{PROJECT_NAME}{version}.zip"
            zip_folder(output_dir, zip_path)

        if not args.no_cvxp:
            reference_dir = (
                REPO_ROOT
                / "ColorVision"
                / "bin"
                / "x64"
                / args.configuration
                / args.framework
            )
            cvxp_output_path = BUILD_DIR / f"{PROJECT_NAME}-{version}.cvxp"
            if build_cvxp(output_dir, reference_dir, cvxp_output_path):
                cvxp_path = cvxp_output_path
    except Exception as exc:
        print(f"Spectrum 打包失败: {exc}", file=sys.stderr)
        return 1
    finally:
        if output_dir.is_dir():
            shutil.rmtree(output_dir)
            print(f"已清理编译目录: {output_dir}")

    if not args.upload:
        print(
            "未指定 --upload：本地包已生成，但没有创建或发布签名清单。"
            "正式发布需要 CurrentUser/My 中配置的 xincheng RSA 证书。"
        )
        print("全部完成。")
        return 0

    try:
        if zip_path is None or not zip_path.is_file():
            raise SpectrumReleaseError("正式发布缺少独立 ZIP。")
        if cvxp_path is None or not cvxp_path.is_file():
            raise SpectrumReleaseError("正式发布缺少插件 .cvxp。")

        # 签名必须先于任何远端写入；证书不可用时本次发布不会改变远端状态。
        signed_release = create_signed_release(
            version,
            args.release_notes,
            zip_path,
        )
        publish_built_artifacts(
            version,
            args.release_notes,
            zip_path,
            cvxp_path,
            signed_release,
        )
    except Exception as exc:
        print(f"Spectrum 发布失败: {exc}", file=sys.stderr)
        print(f"本地 ZIP 保留: {zip_path}", file=sys.stderr)
        print(f"本地 .cvxp 保留: {cvxp_path}", file=sys.stderr)
        return 1

    cvxp_path.unlink()
    print(f"完整远程验证通过，已删除本地 cvxp: {cvxp_path}")
    print(f"独立 ZIP 保留: {zip_path}")
    print("全部完成。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

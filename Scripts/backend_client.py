import os
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable
from urllib.parse import quote

from tqdm import tqdm

DEFAULT_UPLOAD_URL = "http://xc213618.ddns.me:9998"
DEFAULT_UPLOAD_FOLDER = "ColorVision"
DEFAULT_UPLOAD_USERNAME = "xincheng"
DEFAULT_UPLOAD_PASSWORD = "xincheng"
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


def get_requests_module():
    try:
        import requests
    except ImportError:
        return None
    return requests


def resolve_upload_credentials(
    username: str | None = None,
    password: str | None = None,
) -> tuple[str, str]:
    resolved_username = (
        username
        if username is not None
        else os.environ.get("COLORVISION_UPLOAD_USERNAME", DEFAULT_UPLOAD_USERNAME)
    ).strip()
    resolved_password = (
        password
        if password is not None
        else os.environ.get("COLORVISION_UPLOAD_PASSWORD", DEFAULT_UPLOAD_PASSWORD)
    )
    return resolved_username, resolved_password


def resolve_upload_base_url(base_url: str | None = None) -> str:
    return (base_url or os.environ.get("COLORVISION_UPLOAD_URL") or DEFAULT_UPLOAD_URL).rstrip("/")


def build_upload_url(base_url: str, folder_name: str, file_name: str) -> str:
    normalized_folder = folder_name.replace("\\", "/").strip("/")
    encoded_parts = [quote(part, safe="") for part in normalized_folder.split("/") if part]
    encoded_parts.append(quote(file_name, safe=""))
    return f"{base_url.rstrip('/')}/upload/{'/'.join(encoded_parts)}"


def resolve_auth_tuple(settings: RemoteUploadSettings) -> tuple[str, str] | None:
    if not settings.username or not settings.password:
        return None
    return settings.username, settings.password


def _response_json_or_none(response) -> dict[str, Any] | None:
    try:
        payload = response.json()
    except ValueError:
        return None
    return payload if isinstance(payload, dict) else None


def _is_unsupported_preflight_response(status_code: int) -> bool:
    return status_code == 404


def preflight_remote_upload(
    settings: RemoteUploadSettings,
    *,
    session: Any | None = None,
) -> bool:
    if not settings.enabled:
        return True

    requests = get_requests_module()
    if requests is None:
        print("Remote upload preflight requires the requests package. Please install it first.")
        return False

    http_client = session or requests.Session()
    timeout = (settings.connect_timeout, min(settings.read_timeout, 15))
    health_url = f"{settings.base_url.rstrip('/')}/api/health"
    ready_url = f"{settings.base_url.rstrip('/')}/api/ready"

    try:
        health_response = http_client.get(health_url, timeout=timeout)
    except requests.RequestException as exc:
        print(f"Backend health check failed: {exc}")
        return False

    if _is_unsupported_preflight_response(health_response.status_code):
        print("Backend health endpoint is unavailable on this server; continuing with legacy upload flow.")
        return True

    if health_response.status_code != 200:
        print(
            f"Backend health check failed: HTTP {health_response.status_code} {health_response.text.strip()}"
        )
        return False

    health_payload = _response_json_or_none(health_response)
    if not health_payload or health_payload.get("status") != "ok":
        print("Backend health check failed: invalid health response payload.")
        return False

    try:
        ready_response = http_client.get(ready_url, timeout=timeout)
    except requests.RequestException as exc:
        print(f"Backend readiness check failed: {exc}")
        return False

    if _is_unsupported_preflight_response(ready_response.status_code):
        print("Backend readiness endpoint is unavailable on this server; continuing with legacy upload flow.")
        return True

    ready_payload = _response_json_or_none(ready_response)
    if not ready_payload:
        print("Backend readiness check failed: invalid readiness response payload.")
        return False
    if ready_response.status_code != 200 or not ready_payload.get("ready"):
        issues = ready_payload.get("issues") or []
        issue_text = "; ".join(str(item) for item in issues) if issues else ready_response.text.strip()
        print(f"Backend readiness check failed: HTTP {ready_response.status_code} {issue_text}")
        return False

    print("Backend preflight passed.")
    return True


def upload_file(
    file_path: str | Path,
    settings: RemoteUploadSettings,
    *,
    session: Any | None = None,
    progress_factory: Callable[..., Any] = tqdm,
) -> bool:
    requests = get_requests_module()
    if requests is None:
        print("Remote upload requires the requests package. Please install it first.")
        return False

    file_path = Path(file_path)
    file_size = file_path.stat().st_size
    upload_url = build_upload_url(settings.base_url, settings.folder_name, file_path.name)
    http_client = session or requests.Session()
    auth = resolve_auth_tuple(settings)
    last_error = ""

    if settings.enabled and auth is None:
        print(
            "Remote upload is enabled but credentials are missing. "
            "Set COLORVISION_UPLOAD_USERNAME and COLORVISION_UPLOAD_PASSWORD, or disable remote upload."
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


def post_multipart_with_auth(
    url: str,
    *,
    data: dict[str, Any],
    files: dict[str, Any],
    username: str,
    password: str,
    connect_timeout: int = DEFAULT_CONNECT_TIMEOUT,
    read_timeout: int = DEFAULT_READ_TIMEOUT,
    session: Any | None = None,
):
    requests = get_requests_module()
    if requests is None:
        raise RuntimeError("The requests package is required for backend API calls.")

    http_client = session or requests
    return http_client.post(
        url,
        data=data,
        files=files,
        auth=(username, password),
        timeout=(connect_timeout, read_timeout),
    )


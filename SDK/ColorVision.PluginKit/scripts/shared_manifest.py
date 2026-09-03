"""Version-pinned shared-file manifests for the standalone PluginKit."""

import hashlib
import json
import os
import re
import tempfile
from pathlib import Path
from urllib.parse import quote, urlsplit


MAX_MANIFEST_BYTES = 2 * 1024 * 1024
REMOTE_FOLDER = "Tool/PluginKit/shared-files"


def validate_target(host_version: str, framework: str, platform: str) -> None:
    if not isinstance(host_version, str) or not re.fullmatch(r"[0-9]+(?:\.[0-9]+){3}", host_version):
        raise ValueError("targetHostVersion must be an explicit four-part ColorVision host version, not 'latest'.")
    for name, value in (("framework", framework), ("platform", platform)):
        if not isinstance(value, str) or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9.-]*", value):
            raise ValueError(f"Invalid manifest {name}.")


def validate_url(url: str) -> str:
    parsed = urlsplit(url)
    if parsed.scheme not in {"https", "http"} or not parsed.hostname or parsed.username or parsed.password or parsed.fragment:
        raise ValueError("Shared manifest URL must be HTTP(S), without credentials or a fragment.")
    return url


def manifest_url(base_url: str, host_version: str, framework: str, platform: str) -> str:
    validate_target(host_version, framework, platform)
    validate_url(base_url)
    if urlsplit(base_url).query:
        raise ValueError("The manifest server base URL must not contain a query string.")
    return f"{base_url.rstrip('/')}/download/{REMOTE_FOLDER}/{quote(host_version)}/{quote(framework)}-{quote(platform)}.json"


def validate_manifest(data, *, host_version: str | None = None, framework: str = "net10.0-windows", platform: str = "x64") -> set[str]:
    if host_version is not None:
        validate_target(host_version, framework, platform)
        if not isinstance(data, dict) or type(data.get("version")) is not int or data["version"] != 1:
            raise ValueError("Unsupported versioned shared manifest schema.")
        for key, expected in (("host_version", host_version), ("framework", framework), ("platform", platform)):
            if data.get(key) != expected:
                raise ValueError(f"Shared manifest {key} mismatch: expected {expected!r}, got {data.get(key)!r}.")
    paths = data.get("shared_files") if isinstance(data, dict) else data
    if not isinstance(paths, list) or not paths:
        raise ValueError("shared_files must be a non-empty list of relative file paths.")
    result = set()
    for path in paths:
        if not isinstance(path, str):
            raise ValueError("shared_files must contain only strings.")
        normalized = path.replace("\\", "/")
        parts = normalized.split("/")
        if any(part in {"", ".", ".."} or part.endswith((" ", ".")) for part in parts) or any(char in normalized for char in ':*?"<>|') or any(ord(char) < 32 for char in normalized):
            raise ValueError(f"Unsafe shared manifest path: {path!r}")
        result.add(normalized)
    return result


def read_manifest(path: Path, **target) -> set[str]:
    with path.open("rb") as stream:
        raw = stream.read(MAX_MANIFEST_BYTES + 1)
    if len(raw) > MAX_MANIFEST_BYTES:
        raise ValueError("Shared manifest exceeds the 2 MiB limit.")
    return validate_manifest(json.loads(raw.decode("utf-8-sig")), **target)


def default_cache_dir() -> Path:
    return Path(os.environ.get("LOCALAPPDATA") or Path.home() / ".cache") / "ColorVision" / "PluginKit" / "shared-files"


def cache_path_for(url: str, cache_dir: Path, host_version: str, framework: str, platform: str) -> Path:
    validate_target(host_version, framework, platform)
    source_key = hashlib.sha256(url.encode("utf-8")).hexdigest()
    return cache_dir / source_key / host_version / f"{framework}-{platform}.json"


def write_cache(path: Path, raw: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(dir=path.parent, prefix=".manifest-", suffix=".tmp", delete=False) as stream:
            temporary = Path(stream.name)
            stream.write(raw)
        os.replace(temporary, path)
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def resolve_remote_manifest(url: str, cache_dir: Path, host_version: str, framework: str, platform: str, *, offline: bool = False) -> Path:
    validate_url(url)
    target = dict(host_version=host_version, framework=framework, platform=platform)
    cache_path = cache_path_for(url, cache_dir, **target)
    if offline:
        read_manifest(cache_path, **target)
        print(f"Shared manifest: offline cache for host {host_version}")
        return cache_path

    import requests

    if urlsplit(url).scheme == "http":
        print("Warning: shared manifest download uses unencrypted HTTP; use HTTPS on untrusted networks.")
    try:
        with requests.Session() as session:
            session.trust_env = False
            session.proxies.clear()
            # This public read must never send the package-upload credentials or follow redirects.
            with session.get(url, timeout=(5, 15), stream=True, allow_redirects=False, headers={"Cache-Control": "no-cache"}) as response:
                if response.status_code >= 500 or response.status_code in {408, 429}:
                    raise requests.RequestException(f"Manifest server unavailable: HTTP {response.status_code}")
                if response.status_code != 200:
                    raise ValueError(f"Shared manifest download failed: HTTP {response.status_code}; no fallback to another host version.")
                raw = bytearray()
                for chunk in response.iter_content(chunk_size=64 * 1024):
                    raw.extend(chunk)
                    if len(raw) > MAX_MANIFEST_BYTES:
                        raise ValueError("Shared manifest exceeds the 2 MiB limit.")
    except requests.RequestException as exc:
        try:
            read_manifest(cache_path, **target)
        except (OSError, ValueError) as cache_error:
            raise RuntimeError(f"Shared manifest unavailable for host {host_version}; no valid matching cache. Specify a trusted --shared-files file or retry.") from cache_error
        print(f"Warning: manifest download failed ({type(exc).__name__}); using matching cached host {host_version} manifest.")
        return cache_path

    validate_manifest(json.loads(raw.decode("utf-8-sig")), **target)
    write_cache(cache_path, bytes(raw))
    print(f"Shared manifest: downloaded and verified host {host_version} metadata")
    return cache_path

"""Export an offline ColorVision update folder from the update service.

The folder can be copied to a site without internet access (USB drive, bastion file transfer,
shared folder) and listed as an update source in ColorVision's "update server addresses" setting.
Its layout and index are read by ``UI/ColorVision.UI.Desktop/Update/OfflineUpdateFolder.cs``.

Examples:
    py Scripts/export_offline_updates.py --out D:\\CVUpdates --from-version 1.4.17.100
    py Scripts/export_offline_updates.py --out D:\\CVUpdates --from-version 1.4.17.100 --plugins ProjectARVRPro --no-installer

Only reads from the service; it never uploads, publishes or deletes anything remote.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

DEFAULT_SERVER = "http://xc213618.ddns.me:9998"
INDEX_FILE_NAME = "colorvision-updates.json"
CHUNK = 1024 * 1024

Version = tuple[int, int, int, int]


def parse_version(text: str | None) -> Version | None:
    try:
        parts = [int(p) for p in str(text).strip().split(".")]
    except (TypeError, ValueError):
        return None
    if not 2 <= len(parts) <= 4 or any(p < 0 for p in parts):
        return None
    while len(parts) < 4:
        parts.append(-1)
    return tuple(parts)  # type: ignore[return-value]


def version_text(v: Version) -> str:
    return ".".join(str(p) for p in v if p >= 0)


def incremental_chain(current: Version, latest: Version) -> list[Version] | None:
    """Mirror AutoUpdater.BuildUpdatePlan/BuildIncrementalUpdateChain; None means a full installer is required."""
    if latest <= current:
        return []
    if latest[:2] != current[:2]:
        return None
    chain: list[Version] = []
    if latest[2] != current[2]:
        chain.extend((current[0], current[1], build, 1) for build in range(current[2] + 1, latest[2] + 1))
    if not chain or chain[-1] < latest:
        chain.append(latest)
    return chain


def field(obj: dict[str, Any], name: str) -> Any:
    """Read a JSON field regardless of its casing (the client deserializes case-insensitively)."""
    for key, value in obj.items():
        if key.lower() == name.lower():
            return value
    return None


def set_field(obj: dict[str, Any], name: str, value: Any) -> None:
    for key in list(obj):
        if key.lower() == name.lower():
            obj[key] = value
            return
    obj[name] = value


class Exporter:
    def __init__(self, server: str, out: Path, use_system_proxy: bool) -> None:
        self.server = server.rstrip("/")
        self.out = out
        self.files: dict[str, str] = {}
        handlers = [] if use_system_proxy else [urllib.request.ProxyHandler({})]
        self.opener = urllib.request.build_opener(*handlers)

    def _open(self, path: str, timeout: int):
        request = urllib.request.Request(f"{self.server}/{path.lstrip('/')}", headers={"User-Agent": "ColorVision-OfflineExport"})
        return self.opener.open(request, timeout=timeout)

    def get_bytes(self, path: str) -> bytes:
        with self._open(path, 60) as response:
            return response.read()

    def get_json(self, path: str) -> Any:
        return json.loads(self.get_bytes(path).decode("utf-8"))

    def write_bytes(self, relative: str, data: bytes) -> None:
        target = self.out / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        self.files[relative] = hashlib.sha256(data).hexdigest()

    def download(self, path: str, relative: str, expected_sha256: str | None = None) -> None:
        target = self.out / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        partial = target.with_name(target.name + ".partial")
        digest = hashlib.sha256()
        started = time.monotonic()
        size = 0
        with self._open(path, 120) as response, partial.open("wb") as output:
            total = int(response.headers.get("Content-Length") or 0)
            while chunk := response.read(CHUNK):
                output.write(chunk)
                digest.update(chunk)
                size += len(chunk)
                if total:
                    print(f"\r  {relative}: {size * 100 // total}% of {total / CHUNK:.1f} MB", end="", flush=True)
        print(f"\r  {relative}: {size / CHUNK:.1f} MB in {time.monotonic() - started:.0f}s" + " " * 10)
        if size == 0 or (total and size != total):
            partial.unlink(missing_ok=True)
            raise RuntimeError(f"Incomplete download for {relative}: {size}/{total} bytes.")
        actual = digest.hexdigest()
        if expected_sha256 and actual.lower() != expected_sha256.strip().lower():
            partial.unlink(missing_ok=True)
            raise RuntimeError(f"SHA-256 mismatch for {relative}.")
        partial.replace(target)
        self.files[relative] = actual


def select_plugin_version(detail: dict[str, Any], host: Version) -> dict[str, Any] | None:
    """Mirror PluginUpdateCompatibility.SelectLatestCompatibleVersion for a fresh install on the exported host version."""
    detail_requires = field(detail, "RequiresVersion")
    versions = list(field(detail, "Versions") or []) + list(field(detail, "ArchivedVersions") or [])
    versions = [v for v in versions if parse_version(field(v, "Version"))]
    versions.sort(key=lambda v: (parse_version(field(v, "Version")), str(field(v, "CreatedAt") or "")), reverse=True)
    for info in versions:
        requires = field(info, "RequiresVersion") or detail_requires
        if not requires or (parse_version(requires) is not None and host >= parse_version(requires)):  # type: ignore[operator]
            return info
    return None


def run(args: argparse.Namespace) -> int:
    out = Path(args.out).resolve()
    if out.exists() and any(out.iterdir()):
        print(f"Output folder must be empty or not exist: {out}", file=sys.stderr)
        return 2
    if not args.from_version and args.no_installer:
        print("Nothing to export: pass --from-version for incremental packages or keep the installer.", file=sys.stderr)
        return 2
    bases: list[Version] = []
    for text in args.from_version:
        parsed = parse_version(text)
        if parsed is None:
            print(f"Invalid --from-version: {text}", file=sys.stderr)
            return 2
        bases.append(parsed)

    out.mkdir(parents=True, exist_ok=True)
    exporter = Exporter(args.server, out, args.use_system_proxy)

    latest_payload = exporter.get_bytes("api/app/latest-version")
    latest_text = field(json.loads(latest_payload), "version") if latest_payload.strip().startswith(b"{") else latest_payload.decode().strip()
    latest = parse_version(latest_text)
    if latest is None:
        raise RuntimeError(f"The service returned an invalid latest version: {latest_text!r}")
    print(f"Latest ColorVision version: {latest_text}")
    exporter.write_bytes("app/latest-version.json", json.dumps({"version": latest_text}).encode("utf-8"))

    needs_installer = not args.no_installer
    chain_versions: list[Version] = []
    for base in bases:
        chain = incremental_chain(base, latest)
        if chain is None:
            print(f"{version_text(base)} is in another major/minor series; only the full installer can update it.")
            needs_installer = True
            continue
        chain_versions.extend(v for v in chain if v not in chain_versions)
    for v in sorted(chain_versions):
        name = f"ColorVision-Update-[{version_text(v)}].cvx"
        exporter.download(f"api/app/updates/{version_text(v)}/download", f"app/updates/{name}")
    if needs_installer:
        exporter.download(f"api/app/releases/{latest_text}/download", f"app/releases/ColorVision-{latest_text}.exe")

    plugins: dict[str, str] = {}
    for plugin_id in [p.strip() for p in args.plugins.split(",") if p.strip()] if args.plugins else []:
        quoted = urllib.parse.quote(plugin_id, safe="")
        try:
            detail = exporter.get_json(f"api/plugins/{quoted}?view=update")
        except urllib.error.HTTPError as error:
            raise RuntimeError(f"Plugin {plugin_id} is not available from the service ({error.code}).") from error
        selected = select_plugin_version(detail, latest)
        if selected is None:
            raise RuntimeError(f"No version of {plugin_id} is compatible with ColorVision {latest_text}.")
        plugin_version = str(field(selected, "Version"))
        exporter.download(
            f"api/packages/{quoted}/{urllib.parse.quote(plugin_version, safe='')}",
            f"plugins/{plugin_id}/{plugin_id}-{plugin_version}.cvxp",
            field(selected, "FileHash"),
        )
        # Offer only the exported version so the client never selects a package the folder does not contain.
        set_field(detail, "LatestVersion", plugin_version)
        set_field(detail, "Versions", [selected])
        set_field(detail, "ArchivedVersions", [])
        exporter.write_bytes(f"plugins/{plugin_id}/update.json", json.dumps(detail, ensure_ascii=False, indent=2).encode("utf-8"))
        plugins[plugin_id] = plugin_version
        print(f"Plugin {plugin_id}: {plugin_version}")

    index = {
        "format": 1,
        "createdAt": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "appLatestVersion": latest_text,
        "plugins": plugins,
        "files": dict(sorted(exporter.files.items())),
    }
    (out / INDEX_FILE_NAME).write_text(json.dumps(index, ensure_ascii=False, indent=2), encoding="utf-8")
    (out / "README.txt").write_text(
        "ColorVision 离线更新文件夹\r\n\r\n"
        "把整个文件夹复制到客户电脑或共享目录，在 ColorVision 设置 → 更新 → 更新服务器地址 中填写该文件夹路径"
        "（例如 D:\\CVUpdates 或 \\\\server\\share\\CVUpdates），然后检查更新。\r\n"
        "不要单独修改或删除文件：每个文件都按 colorvision-updates.json 中的 SHA-256 校验。\r\n",
        encoding="utf-8",
    )
    print(f"Exported {len(exporter.files)} files to {out}")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Export an offline ColorVision update folder.")
    parser.add_argument("--out", required=True, help="Empty or new output folder.")
    parser.add_argument("--from-version", action="append", default=[], help="Installed version to build the incremental chain from; repeatable.")
    parser.add_argument("--plugins", help="Comma-separated plugin/project package IDs to include (latest version compatible with the exported app).")
    parser.add_argument("--no-installer", action="store_true", help="Skip the full installer (only valid when every site can use the incremental chain).")
    parser.add_argument("--server", default=DEFAULT_SERVER, help=f"Update service address (default {DEFAULT_SERVER}).")
    parser.add_argument("--use-system-proxy", action="store_true", help="Use the Windows/system proxy instead of a direct connection.")
    args = parser.parse_args(argv)
    try:
        return run(args)
    except (OSError, RuntimeError, ValueError) as error:
        print(f"Export failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())

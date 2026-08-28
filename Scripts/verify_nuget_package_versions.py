#!/usr/bin/env python3
"""Fail before publication when any packaged NuGet identity is already occupied."""

from __future__ import annotations

import argparse
import glob
import json
import sys
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from collections.abc import Callable, Iterable
from dataclasses import dataclass
from pathlib import Path
from xml.etree import ElementTree


DEFAULT_FLAT_CONTAINER = "https://api.nuget.org/v3-flatcontainer"


@dataclass(frozen=True)
class PackageIdentity:
    package_id: str
    version: str
    path: Path


def _metadata_value(root: ElementTree.Element, local_name: str) -> str:
    matches = [
        (element.text or "").strip()
        for element in root.iter()
        if element.tag.rsplit("}", 1)[-1] == local_name
    ]
    values = [value for value in matches if value]
    if len(values) != 1:
        raise ValueError(f"Expected one non-empty {local_name} in nuspec; found {values!r}.")
    return values[0]


def read_package_identity(path: Path) -> PackageIdentity:
    with zipfile.ZipFile(path) as archive:
        nuspecs = [name for name in archive.namelist() if name.casefold().endswith(".nuspec")]
        if len(nuspecs) != 1:
            raise ValueError(f"Expected one nuspec in {path}; found {nuspecs!r}.")
        root = ElementTree.fromstring(archive.read(nuspecs[0]))
    return PackageIdentity(
        package_id=_metadata_value(root, "id"),
        version=_metadata_value(root, "version"),
        path=path.resolve(),
    )


def resolve_packages(patterns: Iterable[str]) -> list[Path]:
    resolved: list[Path] = []
    for pattern in patterns:
        matches = [Path(value) for value in glob.glob(pattern)]
        if not matches:
            raise FileNotFoundError(f"NuGet package pattern matched no files: {pattern}")
        resolved.extend(path for path in matches if path.suffix.casefold() == ".nupkg")
    unique = sorted({path.resolve() for path in resolved}, key=lambda path: str(path).casefold())
    if not unique:
        raise FileNotFoundError("No .nupkg files were supplied.")
    return unique


def fetch_versions(package_id: str, flat_container: str = DEFAULT_FLAT_CONTAINER) -> set[str]:
    escaped_id = urllib.parse.quote(package_id.casefold(), safe="")
    request = urllib.request.Request(
        f"{flat_container.rstrip('/')}/{escaped_id}/index.json",
        headers={"User-Agent": "ColorVision-NuGet-Publish-Preflight/1.0"},
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            payload = json.load(response)
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return set()
        raise RuntimeError(f"NuGet version query failed for {package_id}: HTTP {error.code}") from error
    except (urllib.error.URLError, TimeoutError, json.JSONDecodeError) as error:
        raise RuntimeError(f"NuGet version query failed for {package_id}: {error}") from error

    versions = payload.get("versions")
    if not isinstance(versions, list) or any(not isinstance(version, str) for version in versions):
        raise RuntimeError(f"NuGet returned an invalid version index for {package_id}.")
    return {version.casefold() for version in versions}


def find_occupied_versions(
    packages: Iterable[Path],
    version_fetcher: Callable[[str], set[str]] = fetch_versions,
) -> list[PackageIdentity]:
    identities = [read_package_identity(path) for path in packages]
    seen: dict[tuple[str, str], Path] = {}
    for identity in identities:
        key = (identity.package_id.casefold(), identity.version.casefold())
        previous = seen.get(key)
        if previous is not None:
            raise ValueError(
                f"Duplicate packaged identity {identity.package_id} {identity.version}: "
                f"{previous} and {identity.path}"
            )
        seen[key] = identity.path

    version_cache: dict[str, set[str]] = {}
    occupied: list[PackageIdentity] = []
    for identity in identities:
        package_key = identity.package_id.casefold()
        if package_key not in version_cache:
            version_cache[package_key] = version_fetcher(identity.package_id)
        available = version_cache[package_key]
        if identity.version.casefold() in available:
            occupied.append(identity)
    return occupied


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Verify that every .nupkg ID/version is unused before the first NuGet push."
    )
    parser.add_argument("packages", nargs="+", help="Package files or glob patterns.")
    args = parser.parse_args(argv)

    try:
        packages = resolve_packages(args.packages)
        occupied = find_occupied_versions(packages)
    except (FileNotFoundError, ValueError, RuntimeError, zipfile.BadZipFile) as error:
        print(f"NuGet publish preflight failed: {error}", file=sys.stderr)
        return 2

    if occupied:
        for identity in occupied:
            print(
                f"NuGet package version is already occupied: "
                f"{identity.package_id} {identity.version} ({identity.path})",
                file=sys.stderr,
            )
        print("No packages were published. Bump the authoritative package version and rebuild all packages.", file=sys.stderr)
        return 1

    for path in packages:
        identity = read_package_identity(path)
        print(f"available: {identity.package_id} {identity.version} ({identity.path})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

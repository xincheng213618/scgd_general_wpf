"""Check delivered OpenCV binaries against the dependencies resolved by the build."""

import hashlib
import json
import os
import re
import shutil
import tempfile
import zipfile
from pathlib import Path
from xml.etree import ElementTree


NATIVE_PREFIX = "runtimes/win-x64/native/"


def file_hash(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def resolve_native_sources(solution_root: str | Path) -> dict[str, Path]:
    root = Path(solution_root)
    assets_path = root / "ColorVision/obj/project.assets.json"
    assets = json.loads(assets_path.read_text(encoding="utf-8-sig"))
    sources: dict[str, Path] = {}
    for target in assets["targets"].values():
        for package, contents in target.items():
            if not package.lower().startswith("opencvsharp4.runtime.win/"):
                continue
            package_path = assets["libraries"][package]["path"]
            entries = dict(contents.get("native", {}))
            entries.update(contents.get("runtimeTargets", {}))
            for relative, metadata in entries.items():
                if not relative.startswith(NATIVE_PREFIX) or not relative.lower().endswith(".dll"):
                    continue
                if metadata.get("assetType", "native") != "native":
                    continue
                candidates = [Path(folder) / package_path / relative for folder in assets["packageFolders"]]
                source = next((path for path in candidates if path.is_file()), None)
                if source is None:
                    raise FileNotFoundError(f"Resolved native dependency is missing: {package}/{relative}")
                if relative in sources and sources[relative] != source:
                    raise ValueError(f"Conflicting resolved native dependencies: {relative}")
                sources[relative] = source
    if NATIVE_PREFIX + "OpenCvSharpExtern.dll" not in sources:
        raise ValueError(f"OpenCvSharp win-x64 native assets were not resolved in {assets_path}")

    # Read the same Release property sheet as the C++ build; do not pin a second version here.
    props_path = root / "packages/OpenCV.Release.x64.props"
    ns = {"m": "http://schemas.microsoft.com/developer/msbuild/2003"}
    properties = {"MSBuildThisFileDirectory": str(props_path.parent.resolve()) + os.sep}

    def expand(value: str) -> str:
        def replace(match: re.Match) -> str:
            name = match.group(1)
            if name not in properties:
                raise ValueError(f"Unresolved native runtime property: {name}")
            return properties[name]
        return re.sub(r"\$\(([^)]+)\)", replace, value)

    try:
        props = ElementTree.parse(props_path)
    except ElementTree.ParseError as exc:
        raise ValueError(f"Invalid native runtime property sheet: {props_path}") from exc
    for prop in props.findall("m:PropertyGroup/*", ns):
        properties[prop.tag.rsplit("}", 1)[-1]] = expand(prop.text or "")
    native_items = props.findall("m:ItemGroup/m:OpenCVRuntimeDll", ns)
    if not native_items:
        raise ValueError(f"No native runtime files declared in {props_path}")
    for item in native_items:
        source = Path(expand(item.attrib["Include"]).replace("\\", os.sep))
        relative = NATIVE_PREFIX + source.name
        if relative in sources and sources[relative] != source:
            raise ValueError(f"Conflicting native runtime source: {relative}")
        sources[relative] = source
    return sources


def ensure_native_runtime_integrity(
    solution_root: str | Path, runtime_directory: str | Path, *, repair: bool = False,
) -> dict[str, str]:
    sources = resolve_native_sources(solution_root)
    # Validate every source before modifying output. Missing dependencies must stop delivery.
    expected = {relative: file_hash(source) for relative, source in sources.items()}
    runtime = Path(runtime_directory)
    for relative, source in sources.items():
        target = runtime / relative
        if target.is_file() and file_hash(target) == expected[relative]:
            continue
        if not repair:
            raise ValueError(f"Native runtime content differs from its source: {target}")
        target.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=target.parent, prefix=target.name + ".repair-", delete=False) as handle:
            staging = Path(handle.name)
        try:
            shutil.copy2(source, staging)
            if file_hash(staging) != expected[relative]:
                raise ValueError(f"Native runtime copy verification failed: {source} -> {staging}")
            os.replace(staging, target)
        finally:
            staging.unlink(missing_ok=True)
        if file_hash(target) != expected[relative]:
            raise ValueError(f"Native runtime verification failed after replacement: {target}")
        print(f"Repaired native runtime: {relative}")
    print(f"Verified {len(expected)} OpenCV native runtime files by SHA-256.")
    return expected


def validate_native_archive(archive_path: str | Path, expected: dict[str, str], *, require_all: bool) -> None:
    with zipfile.ZipFile(archive_path) as archive:
        names = archive.namelist()
        if require_all and set(expected) - set(names):
            raise ValueError(f"Native runtime files are missing from {archive_path}")
        for relative in set(expected).intersection(names):
            if names.count(relative) != 1:
                raise ValueError(f"Duplicate native runtime entry: {relative}")
            with archive.open(relative) as stream:
                actual = hashlib.file_digest(stream, "sha256").hexdigest()
            if actual != expected[relative]:
                raise ValueError(f"Packaged native runtime content differs from source: {relative}")

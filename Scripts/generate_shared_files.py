import argparse
import json
import os
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent
DEFAULT_ROOT_DIR = REPO_ROOT / "ColorVision" / "bin" / "x64" / "Release" / "net10.0-windows"
DEFAULT_OUTPUT_FILE = SCRIPT_DIR / "shared_files.json"
DEFAULT_OUTPUT_FILES = (
    DEFAULT_OUTPUT_FILE,
    REPO_ROOT / "SDK" / "ColorVision.PluginKit" / "scripts" / "shared_files.json",
)
EXCLUDED_DIR_NAMES = {"plugins", "log"}


def normalize_relative_path(path: str | Path) -> str:
    return Path(str(path).replace("\\", "/")).as_posix()


def collect_shared_files(root_dir: Path, *, excluded_files: Iterable[Path] = ()) -> list[str]:
    root_dir = root_dir.resolve()
    resolved_excluded_files = {file_path.resolve() for file_path in excluded_files}
    shared_files: list[str] = []
    for current_root, dir_names, file_names in os.walk(root_dir, topdown=True):
        dir_names[:] = sorted(dir_name for dir_name in dir_names if dir_name.lower() not in EXCLUDED_DIR_NAMES)
        current_root_path = Path(current_root)
        for file_name in sorted(file_names):
            if current_root_path == root_dir and file_name.lower() == "changelog.md":
                continue
            file_path = current_root_path / file_name
            if file_path.resolve() in resolved_excluded_files:
                continue
            shared_files.append(normalize_relative_path(file_path.relative_to(root_dir)))
    return shared_files


def build_manifest(shared_files: list[str]) -> dict:
    return {
        "version": 1,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "shared_files": sorted(set(shared_files)),
    }


def build_release_manifest(root_dir: Path, host_version: str, *, delivered_files: Iterable[str]) -> dict:
    """Describe one validated Release x64 host, independently of the SDK binary."""
    if not re.fullmatch(r"[0-9]+(?:\.[0-9]+){3}", host_version):
        raise ValueError("The host release manifest requires a four-part version.")
    if not root_dir.is_dir():
        raise FileNotFoundError(f"Host output directory not found: {root_dir}")
    delivered_set = {normalize_relative_path(path).casefold() for path in delivered_files}
    shared_files = [path for path in collect_shared_files(root_dir) if path.casefold() in delivered_set]
    if not shared_files:
        raise ValueError("Cannot publish an empty host shared-file manifest.")
    return {
        **build_manifest(shared_files),
        "host_version": host_version,
        "framework": "net10.0-windows",
        "platform": "x64",
    }


def load_shared_files_manifest(file_path: Path) -> set[str]:
    manifest_data = json.loads(file_path.read_text(encoding="utf-8-sig"))
    if isinstance(manifest_data, dict):
        if "shared_files" not in manifest_data:
            raise RuntimeError(f"Missing shared_files in manifest: {file_path}")
        shared_files = manifest_data["shared_files"]
    elif isinstance(manifest_data, list):
        shared_files = manifest_data
    else:
        raise RuntimeError(f"Unsupported shared_files.json format: {file_path}")

    if not isinstance(shared_files, list) or not all(isinstance(path_value, str) for path_value in shared_files):
        raise RuntimeError(f"shared_files must be a list of paths: {file_path}")
    return {normalize_relative_path(path_value) for path_value in shared_files}


def compare_shared_file_sets(runtime_files: Iterable[str], manifest_files: Iterable[str]) -> tuple[set[str], set[str]]:
    runtime_set = {normalize_relative_path(path_value) for path_value in runtime_files}
    manifest_set = {normalize_relative_path(path_value) for path_value in manifest_files}
    return manifest_set - runtime_set, runtime_set - manifest_set


def check_manifest(root_dir: Path, manifest_file: Path) -> tuple[set[str], set[str]]:
    runtime_files = collect_shared_files(root_dir, excluded_files=(manifest_file,))
    manifest_files = load_shared_files_manifest(manifest_file)
    return compare_shared_file_sets(runtime_files, manifest_files)


def print_difference(label: str, paths: set[str], *, limit: int = 20) -> None:
    print(f"{label}: {len(paths)}")
    for path_value in sorted(paths)[:limit]:
        print(f"  - {path_value}")
    if len(paths) > limit:
        print(f"  ... and {len(paths) - limit} more")


def write_manifest(output_file: Path, manifest: dict) -> None:
    output_file.parent.mkdir(parents=True, exist_ok=True)
    output_file.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")


def write_manifest_if_changed(output_file: Path, manifest: dict) -> bool:
    if output_file.is_file():
        try:
            existing_files = load_shared_files_manifest(output_file)
        except (json.JSONDecodeError, OSError, RuntimeError):
            pass
        else:
            if existing_files == set(manifest["shared_files"]):
                return False

    write_manifest(output_file, manifest)
    return True


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate or verify a shared-file manifest from a ColorVision host output directory.")
    parser.add_argument(
        "--root-dir",
        default=str(DEFAULT_ROOT_DIR),
        help=f"Host output directory to scan (default: {DEFAULT_ROOT_DIR})",
    )
    parser.add_argument(
        "--output",
        action="append",
        help="Path to generate or check. Repeat for multiple mirrors; defaults to the repository and Plugin Kit manifests.",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Compare only shared_files sets with the current host output; generated_at, list order, and duplicates are ignored.",
    )
    args = parser.parse_args()

    root_dir = Path(args.root_dir).expanduser().resolve()
    output_files = tuple(
        Path(output_value).expanduser().resolve()
        for output_value in (args.output or DEFAULT_OUTPUT_FILES)
    )

    if not root_dir.is_dir():
        raise FileNotFoundError(f"Host output directory not found: {root_dir}")

    if args.check:
        runtime_files = collect_shared_files(root_dir, excluded_files=output_files)
        print(f"Scanned host directory: {root_dir}")
        has_drift = False
        for manifest_file in output_files:
            if not manifest_file.is_file():
                print(f"Shared files manifest not found: {manifest_file}")
                has_drift = True
                continue

            manifest_files = load_shared_files_manifest(manifest_file)
            manifest_only, runtime_only = compare_shared_file_sets(runtime_files, manifest_files)
            print(f"Checked manifest: {manifest_file}")
            if manifest_only or runtime_only:
                has_drift = True
                print_difference("Manifest-only files", manifest_only)
                print_difference("Runtime-only files", runtime_only)
            else:
                print(f"Shared file set matches: {len(manifest_files)}")

        if has_drift:
            raise SystemExit(1)
        return

    shared_files = collect_shared_files(root_dir, excluded_files=output_files)
    manifest = build_manifest(shared_files)
    updated_outputs: list[tuple[Path, bool]] = []
    for output_file in output_files:
        updated_outputs.append((output_file, write_manifest_if_changed(output_file, manifest)))

    print(f"Scanned host directory: {root_dir}")
    print("Ignored directories: Plugins, Log")
    print(f"Shared file count: {len(shared_files)}")
    for output_file, was_updated in updated_outputs:
        action = "Generated manifest" if was_updated else "Manifest already current"
        print(f"{action}: {output_file}")


if __name__ == "__main__":
    main()

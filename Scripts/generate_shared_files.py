import argparse
import json
import os
from datetime import datetime, timezone
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent
DEFAULT_ROOT_DIR = REPO_ROOT / "ColorVision" / "bin" / "x64" / "Release" / "net10.0-windows"
DEFAULT_OUTPUT_FILE = SCRIPT_DIR / "shared_files.json"
EXCLUDED_DIR_NAMES = {"plugins", "log"}


def normalize_relative_path(path: Path) -> str:
    return path.as_posix()


def collect_shared_files(root_dir: Path) -> list[str]:
    shared_files: list[str] = []
    for current_root, dir_names, file_names in os.walk(root_dir, topdown=True):
        dir_names[:] = sorted(dir_name for dir_name in dir_names if dir_name.lower() not in EXCLUDED_DIR_NAMES)
        current_root_path = Path(current_root)
        for file_name in sorted(file_names):
            file_path = current_root_path / file_name
            shared_files.append(normalize_relative_path(file_path.relative_to(root_dir)))
    return shared_files


def build_manifest(shared_files: list[str]) -> dict:
    return {
        "version": 1,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "shared_files": shared_files,
    }


def write_manifest(output_file: Path, manifest: dict) -> None:
    output_file.parent.mkdir(parents=True, exist_ok=True)
    output_file.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description="Scan a ColorVision host output directory and generate shared_files.json.")
    parser.add_argument(
        "--root-dir",
        default=str(DEFAULT_ROOT_DIR),
        help=f"Host output directory to scan (default: {DEFAULT_ROOT_DIR})",
    )
    parser.add_argument(
        "--output",
        default=str(DEFAULT_OUTPUT_FILE),
        help=f"Path to the generated shared_files.json (default: {DEFAULT_OUTPUT_FILE})",
    )
    args = parser.parse_args()

    root_dir = Path(args.root_dir).expanduser().resolve()
    output_file = Path(args.output).expanduser().resolve()

    if not root_dir.is_dir():
        raise FileNotFoundError(f"Host output directory not found: {root_dir}")

    shared_files = collect_shared_files(root_dir)
    manifest = build_manifest(shared_files)
    write_manifest(output_file, manifest)

    print(f"Scanned host directory: {root_dir}")
    print("Ignored directories: Plugins, Log")
    print(f"Shared file count: {len(shared_files)}")
    print(f"Generated manifest: {output_file}")


if __name__ == "__main__":
    main()
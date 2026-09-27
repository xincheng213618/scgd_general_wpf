"""Discover the independently published Windows download assistant."""

from pathlib import Path


SETUP_RELATIVE_PATH = "Tool/ColorVisionSetup/ColorVisionSetup.exe"


def get_download_assistant(storage: Path) -> dict | None:
    root = storage.resolve()
    target = root / SETUP_RELATIVE_PATH
    try:
        # Never advertise an alias to private storage or a file outside storage.
        if target.resolve() != target or not target.is_file():
            return None
        size = target.stat().st_size
        if size == 0:
            return None
    except OSError:
        return None
    return {"relative_path": SETUP_RELATIVE_PATH, "size": size}

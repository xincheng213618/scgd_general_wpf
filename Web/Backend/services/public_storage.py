"""Policy for files and directories exposed by public storage routes."""

from __future__ import annotations

import re
from pathlib import PurePosixPath


_PUBLIC_DIRECTORY_ROOTS = frozenset({
    "history",
    "plugins",
    "spectrum",
    "tool",
    "update",
})
_PUBLIC_ROOT_FILES = frozenset({"changelog.md", "latest_release"})
_APP_RELEASE_RE = re.compile(
    r"^ColorVision-(?:Android-)?\d+(?:\.\d+)+\.(?:apk|exe|rar|zip)$",
    re.IGNORECASE,
)


def is_public_storage_path(relative_path: str) -> bool:
    """Return whether an already-normalized storage path is publicly visible."""
    parts = PurePosixPath((relative_path or "").replace("\\", "/")).parts
    if not parts:
        return True
    if parts[0].casefold() in _PUBLIC_DIRECTORY_ROOTS:
        return True
    if len(parts) != 1:
        return False
    return parts[0].casefold() in _PUBLIC_ROOT_FILES or _APP_RELEASE_RE.fullmatch(parts[0]) is not None

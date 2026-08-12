"""Atomic persistence for explicitly allowlisted live configuration values."""

from __future__ import annotations

import json
import os
import threading
import uuid
from pathlib import Path
from typing import Any, Mapping


_write_lock = threading.Lock()


def persist_config_values(
    config_path: Path,
    active_config: dict[str, Any],
    values: Mapping[str, Any],
) -> None:
    """Merge caller-validated values into config.json and the live mapping."""
    path = Path(config_path)
    temporary_path: Path | None = None
    with _write_lock:
        if path.exists():
            with path.open(encoding="utf-8") as stream:
                persisted = json.load(stream)
            if not isinstance(persisted, dict):
                raise ValueError("configuration file must contain a JSON object")
        else:
            persisted = {}

        updated = dict(persisted)
        updated.update(values)
        encoded = (json.dumps(updated, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
        temporary_path = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
        try:
            with temporary_path.open("xb") as stream:
                stream.write(encoded)
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temporary_path, path)
        finally:
            if temporary_path.exists():
                temporary_path.unlink()

        active_config.update(values)

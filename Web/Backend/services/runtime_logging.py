"""Persistent, rotating capture for production stdout and stderr."""

from __future__ import annotations

import atexit
import io
import os
import sys
import threading
from datetime import datetime, timezone
from pathlib import Path
from typing import TextIO


DEFAULT_MAX_BYTES = 10 * 1024 * 1024
DEFAULT_BACKUP_COUNT = 5
RUNTIME_LOG_RELATIVE_PATH = Path("Logs") / "Web" / "ColorVisionWeb.log"


class _RotatingTextSink:
    def __init__(self, path: Path, *, max_bytes: int, backup_count: int):
        self.path = Path(path)
        self.max_bytes = max(1, int(max_bytes))
        self.backup_count = max(0, int(backup_count))
        self._lock = threading.RLock()
        self._handle: TextIO | None = None
        self._size = 0
        self._closed = False
        self._open()

    def _open(self) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self._handle = self.path.open("a", encoding="utf-8", buffering=1, newline="")
        self._size = self.path.stat().st_size

    def _backup_path(self, index: int) -> Path:
        return self.path.with_name(f"{self.path.name}.{index}")

    def _rotate(self) -> None:
        if self._handle is not None:
            self._handle.close()
            self._handle = None

        if self.backup_count == 0:
            self.path.unlink(missing_ok=True)
        else:
            oldest = self._backup_path(self.backup_count)
            oldest.unlink(missing_ok=True)
            for index in range(self.backup_count - 1, 0, -1):
                source = self._backup_path(index)
                if source.exists():
                    source.replace(self._backup_path(index + 1))
            if self.path.exists():
                self.path.replace(self._backup_path(1))
        self._open()

    def write(self, text: str) -> int:
        if not text or self._closed:
            return len(text)
        encoded_size = len(text.encode("utf-8", errors="replace"))
        with self._lock:
            if self._size and self._size + encoded_size > self.max_bytes:
                self._rotate()
            assert self._handle is not None
            self._handle.write(text)
            self._handle.flush()
            self._size += encoded_size
        return len(text)

    def flush(self) -> None:
        with self._lock:
            if self._handle is not None:
                self._handle.flush()

    def close(self) -> None:
        with self._lock:
            self._closed = True
            if self._handle is not None:
                self._handle.flush()
                self._handle.close()
                self._handle = None


class _TeeTextStream(io.TextIOBase):
    def __init__(self, original: TextIO, sink: _RotatingTextSink):
        self._original = original
        self._sink = sink

    @property
    def encoding(self) -> str:
        return getattr(self._original, "encoding", None) or "utf-8"

    @property
    def errors(self) -> str:
        return getattr(self._original, "errors", None) or "replace"

    def write(self, text: str) -> int:
        try:
            self._original.write(text)
        except (OSError, ValueError):
            pass
        try:
            self._sink.write(text)
        except (OSError, ValueError):
            pass
        return len(text)

    def flush(self) -> None:
        try:
            self._original.flush()
        except (OSError, ValueError):
            pass
        try:
            self._sink.flush()
        except (OSError, ValueError):
            pass

    def isatty(self) -> bool:
        return bool(getattr(self._original, "isatty", lambda: False)())

    def fileno(self) -> int:
        return self._original.fileno()


_runtime_sink: _RotatingTextSink | None = None
_runtime_log_path: Path | None = None
_original_stdout: TextIO | None = None
_original_stderr: TextIO | None = None


def install_runtime_logging(
    storage: Path,
    *,
    max_bytes: int = DEFAULT_MAX_BYTES,
    backup_count: int = DEFAULT_BACKUP_COUNT,
) -> Path:
    """Tee process stdout/stderr into a bounded log under the storage root."""
    global _original_stderr, _original_stdout, _runtime_log_path, _runtime_sink

    if _runtime_log_path is not None:
        return _runtime_log_path

    log_path = Path(storage) / RUNTIME_LOG_RELATIVE_PATH
    sink = _RotatingTextSink(
        log_path,
        max_bytes=max_bytes,
        backup_count=backup_count,
    )
    marker = (
        f"\n[runtime] process_start pid={os.getpid()} "
        f"utc={datetime.now(timezone.utc).isoformat()}\n"
    )
    sink.write(marker)
    _original_stdout = sys.stdout
    _original_stderr = sys.stderr
    sys.stdout = _TeeTextStream(_original_stdout, sink)
    sys.stderr = _TeeTextStream(_original_stderr, sink)
    _runtime_sink = sink
    _runtime_log_path = log_path
    atexit.register(close_runtime_logging)
    return log_path


def close_runtime_logging() -> None:
    """Flush the active log and restore the process streams."""
    global _original_stderr, _original_stdout, _runtime_log_path, _runtime_sink

    sink = _runtime_sink
    if _original_stdout is not None:
        sys.stdout = _original_stdout
    if _original_stderr is not None:
        sys.stderr = _original_stderr
    _original_stdout = None
    _original_stderr = None
    _runtime_sink = None
    _runtime_log_path = None
    if sink is not None:
        sink.close()

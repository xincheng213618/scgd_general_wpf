"""Framework-neutral artifact delivery and completion event policy."""

from __future__ import annotations

import re
from collections.abc import Callable, Iterable, Iterator
from dataclasses import dataclass
from pathlib import Path
from typing import Any


_CONTENT_RANGE_RE = re.compile(r"^bytes 0-(\d+)/(\d+)$")


@dataclass(frozen=True, slots=True)
class ArtifactDownloadEvent:
    """Stable identity for one requested artifact representation."""

    artifact_type: str
    artifact_id: str
    version: str = ""
    relative_path: str = ""


@dataclass(frozen=True, slots=True)
class ArtifactDeliverySpec:
    path: Path
    event: ArtifactDownloadEvent
    download_name: str | None = None
    mimetype: str | None = None
    as_attachment: bool = True
    etag: bool | str = True
    max_age: int | None = None


class _CompletionTrackingIterator(Iterator[bytes]):
    def __init__(
        self,
        source: Iterable[bytes],
        *,
        expected_bytes: int,
        on_completed: Callable[[], None],
    ):
        self._source = source
        self._iterator = iter(source)
        self._expected_bytes = expected_bytes
        self._on_completed = on_completed
        self._bytes_yielded = 0
        self._finished = False

    def __iter__(self) -> _CompletionTrackingIterator:
        return self

    def __next__(self) -> bytes:
        try:
            chunk = next(self._iterator)
        except StopIteration:
            self._finish_if_complete()
            raise
        self._bytes_yielded += len(chunk)
        return chunk

    def close(self) -> None:
        close = getattr(self._source, "close", None)
        if close is not None:
            close()

    def _finish_if_complete(self) -> None:
        if self._finished or self._bytes_yielded != self._expected_bytes:
            return
        self._finished = True
        try:
            self._on_completed()
        except Exception as exc:
            print(f"[artifact_delivery] completion handler failed: {exc}")


class ArtifactDeliveryService:
    """Apply one response and completion-event contract to artifact downloads."""

    def deliver(
        self,
        spec: ArtifactDeliverySpec,
        *,
        request_method: str,
        response_factory: Callable[[ArtifactDeliverySpec], Any],
        on_completed: Callable[[ArtifactDownloadEvent], None] | None = None,
    ) -> Any:
        response = response_factory(spec)
        response.headers.setdefault("Accept-Ranges", "bytes")
        response.headers["X-Content-Type-Options"] = "nosniff"

        expected_bytes = self._completed_representation_bytes(
            request_method=request_method,
            status_code=response.status_code,
            headers=response.headers,
        )
        if expected_bytes is None or on_completed is None:
            return response

        response.response = _CompletionTrackingIterator(
            response.response,
            expected_bytes=expected_bytes,
            on_completed=lambda: on_completed(spec.event),
        )
        return response

    @staticmethod
    def _completed_representation_bytes(
        *,
        request_method: str,
        status_code: int,
        headers: Any,
    ) -> int | None:
        if request_method.upper() != "GET":
            return None

        raw_length = headers.get("Content-Length")
        try:
            content_length = int(raw_length)
        except (TypeError, ValueError):
            return None
        if content_length < 0:
            return None

        if status_code == 200:
            return content_length
        if status_code != 206:
            return None

        match = _CONTENT_RANGE_RE.fullmatch(str(headers.get("Content-Range") or ""))
        if match is None:
            return None
        end = int(match.group(1))
        total = int(match.group(2))
        if end + 1 != total or content_length != total:
            return None
        return content_length

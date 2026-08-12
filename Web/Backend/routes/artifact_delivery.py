"""Flask adapter for the framework-neutral artifact delivery service."""

from __future__ import annotations

from pathlib import Path
from typing import Callable

from flask import send_file

from services.artifact_delivery import (
    ArtifactDeliveryService,
    ArtifactDeliverySpec,
    ArtifactDownloadEvent,
)


def deliver_artifact(
    service: ArtifactDeliveryService,
    path: Path,
    *,
    request_method: str,
    event: ArtifactDownloadEvent,
    download_name: str | None = None,
    mimetype: str | None = None,
    as_attachment: bool = True,
    etag: bool | str = True,
    max_age: int | None = None,
    on_completed: Callable[[ArtifactDownloadEvent], None] | None = None,
):
    spec = ArtifactDeliverySpec(
        path=path,
        event=event,
        download_name=download_name,
        mimetype=mimetype,
        as_attachment=as_attachment,
        etag=etag,
        max_age=max_age,
    )
    return service.deliver(
        spec,
        request_method=request_method,
        response_factory=lambda delivery: send_file(
            delivery.path,
            mimetype=delivery.mimetype,
            as_attachment=delivery.as_attachment,
            download_name=delivery.download_name,
            conditional=True,
            etag=delivery.etag,
            max_age=delivery.max_age,
        ),
        on_completed=on_completed,
    )

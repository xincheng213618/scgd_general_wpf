"""HTTP response compression for JSON APIs."""

from __future__ import annotations

import gzip

from flask import request


DEFAULT_MINIMUM_SIZE = 1024


def _gzip_is_acceptable(value: str) -> bool:
    wildcard_quality: float | None = None
    for item in value.split(","):
        parts = [part.strip() for part in item.split(";")]
        encoding = parts[0].lower()
        quality = 1.0
        for parameter in parts[1:]:
            name, separator, raw_value = parameter.partition("=")
            if separator and name.strip().lower() == "q":
                try:
                    quality = float(raw_value.strip())
                except ValueError:
                    quality = 0.0
        if encoding == "gzip":
            return quality > 0
        if encoding == "*":
            wildcard_quality = quality
    return wildcard_quality is not None and wildcard_quality > 0


def _is_json_response(response) -> bool:
    mimetype = (response.mimetype or "").lower()
    return mimetype == "application/json" or (
        mimetype.startswith("application/") and mimetype.endswith("+json")
    )


def register_response_compression(app, *, minimum_size: int = DEFAULT_MINIMUM_SIZE) -> None:
    """Compress large, buffered JSON GET responses when the client accepts gzip."""

    @app.after_request
    def _compress_json_response(response):
        if (
            request.method not in {"GET", "HEAD"}
            or response.status_code != 200
            or response.direct_passthrough
            or response.is_streamed
            or response.headers.get("Content-Encoding")
            or response.headers.get("Content-Range")
            or response.headers.get("ETag")
            or not _is_json_response(response)
            or "no-transform" in response.headers.get("Cache-Control", "").lower()
        ):
            return response

        body = response.get_data()
        if len(body) < minimum_size:
            return response

        response.vary.add("Accept-Encoding")
        if not _gzip_is_acceptable(request.headers.get("Accept-Encoding", "")):
            return response

        compressed = gzip.compress(body, compresslevel=6, mtime=0)
        if len(compressed) >= len(body):
            return response

        response.set_data(compressed)
        response.headers["Content-Encoding"] = "gzip"
        response.headers["Content-Length"] = str(len(compressed))
        return response


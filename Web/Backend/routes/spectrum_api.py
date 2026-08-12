"""Public update and authenticated publish API for standalone Spectrum."""

from __future__ import annotations

from typing import Any

from flask import Blueprint, jsonify, request
from routes.artifact_delivery import deliver_artifact
from routes.browser_auth import apply_basic_auth_challenge
from routes.request_context import current_request_context, set_authenticated_request_context
from services.artifact_delivery import ArtifactDownloadEvent

from services.spectrum_release import (
    MAX_MANIFEST_BYTES,
    MAX_SIGNATURE_BYTES,
    SpectrumReleaseConflict,
    SpectrumReleaseError,
    SpectrumReleaseNotFound,
    is_spectrum_version,
    list_spectrum_releases,
    load_latest_spectrum_release,
    load_spectrum_release,
    publish_spectrum_release,
    read_latest_version,
    spectrum_latest_payload,
    spectrum_release_payload,
)


spectrum_api = Blueprint("spectrum_api", __name__)
_ctx = None


def register_spectrum_api(app, ctx) -> None:
    global _ctx
    _ctx = ctx
    app.register_blueprint(spectrum_api)


def _get_storage():
    return _ctx.storage


def _json_error(message: str, status: int):
    return jsonify({"error": message, "status": status}), status


def _json_auth_error():
    response = jsonify({"error": "Authentication required", "status": 401})
    response.status_code = 401
    return apply_basic_auth_challenge(response, "ColorVision Marketplace")


def _has_publish_auth() -> bool:
    request_context = current_request_context()
    decision = _ctx.auth_policy.authorize(
        request_context,
        ["release:publish"],
    )
    if decision.allowed:
        set_authenticated_request_context(request_context.with_actor(decision.principal))
    return decision.allowed


def _read_upload_bytes(field_name: str, limit: int) -> bytes:
    upload = request.files.get(field_name)
    if upload is None:
        raise SpectrumReleaseError(f"Missing multipart file field: {field_name}")
    data = upload.stream.read(limit + 1)
    if not data or len(data) > limit:
        raise SpectrumReleaseError(f"Multipart file field {field_name} is empty or too large")
    return data


def _no_store(response):
    response.headers["Cache-Control"] = "no-store"
    return response


@spectrum_api.route("/api/tool/spectrum/latest")
def api_spectrum_latest():
    try:
        release = load_latest_spectrum_release(_get_storage())
    except SpectrumReleaseNotFound as exc:
        return _json_error(str(exc), 404)
    except (OSError, SpectrumReleaseError) as exc:
        return _json_error(str(exc), 500)
    return _no_store(jsonify(spectrum_latest_payload(release)))


@spectrum_api.route("/api/tool/spectrum/latest-version")
def api_spectrum_latest_version():
    try:
        version = read_latest_version(_get_storage())
        if version is None:
            raise SpectrumReleaseNotFound("No signed Spectrum release has been published")
        # Do not advertise a dangling or invalid latest pointer.
        load_spectrum_release(_get_storage(), version)
    except SpectrumReleaseNotFound as exc:
        return _json_error(str(exc), 404)
    except (OSError, SpectrumReleaseError) as exc:
        return _json_error(str(exc), 500)
    return _no_store(jsonify({"version": version}))


@spectrum_api.route("/api/tool/spectrum/releases")
def api_spectrum_releases():
    try:
        latest_version = read_latest_version(_get_storage()) or ""
        if latest_version:
            load_spectrum_release(_get_storage(), latest_version)
        releases = list_spectrum_releases(_get_storage())
    except (OSError, SpectrumReleaseError) as exc:
        return _json_error(str(exc), 500)
    return _no_store(jsonify({
        "latestVersion": latest_version,
        "releases": releases,
        "count": len(releases),
    }))


@spectrum_api.route("/api/tool/spectrum/download/<version>")
def api_spectrum_download(version: str):
    if not is_spectrum_version(version):
        return _json_error("Invalid Spectrum version", 400)
    try:
        release = load_spectrum_release(_get_storage(), version)
    except SpectrumReleaseNotFound as exc:
        return _json_error(str(exc), 404)
    except (OSError, SpectrumReleaseError) as exc:
        return _json_error(str(exc), 500)

    return deliver_artifact(
        _ctx.artifact_delivery,
        release.package_path,
        request_method=request.method,
        event=ArtifactDownloadEvent(
            artifact_type="tool",
            artifact_id="Spectrum",
            version=version,
            relative_path=f"Spectrum/releases/{version}/{release.package_path.name}",
        ),
        mimetype="application/zip",
        download_name=release.manifest["package"]["fileName"],
        etag=release.manifest["package"]["sha256"],
        max_age=0,
    )


@spectrum_api.route("/api/tool/spectrum/publish", methods=["POST"])
def api_spectrum_publish():
    if not _has_publish_auth():
        return _json_auth_error()

    if "Version" not in request.form:
        return _json_error("Missing multipart form field: Version", 400)
    if "ReleaseNotes" not in request.form:
        return _json_error("Missing multipart form field: ReleaseNotes", 400)
    version = request.form["Version"]
    release_notes = request.form["ReleaseNotes"]
    package_upload = request.files.get("Package")
    if package_upload is None or not package_upload.filename:
        return _json_error("Missing multipart file field: Package", 400)

    try:
        manifest_bytes = _read_upload_bytes("Manifest", MAX_MANIFEST_BYTES)
        signature_bytes = _read_upload_bytes("Signature", MAX_SIGNATURE_BYTES)
        result = publish_spectrum_release(
            _get_storage(),
            version=version,
            release_notes=release_notes,
            manifest_bytes=manifest_bytes,
            signature_bytes=signature_bytes,
            package_stream=package_upload.stream,
            package_filename=package_upload.filename,
        )
    except SpectrumReleaseConflict as exc:
        return _json_error(str(exc), 409)
    except SpectrumReleaseError as exc:
        return _json_error(str(exc), 400)
    except OSError as exc:
        return _json_error(f"Failed to store Spectrum release: {exc}", 500)

    release_payload: dict[str, Any] = spectrum_release_payload(result.release)
    status = 201 if result.created else 200
    return jsonify({
        "message": "Spectrum release published" if result.created else "Spectrum release already published",
        "created": result.created,
        "version": result.release.manifest["version"],
        "latest": spectrum_latest_payload(result.release),
        "release": release_payload,
    }), status

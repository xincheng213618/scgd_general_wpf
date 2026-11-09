"""Native desktop activation API, authenticated by device-key proof."""

from cryptography.hazmat.primitives.asymmetric.rsa import RSAPrivateKey
from flask import Blueprint, jsonify, request

from services.activation import ACTIONS, ActivationError, ActivationService
from services.activation_http import decrypt_request, encrypt_response, invalid_envelope


def register_activation_api(
    app, service: ActivationService, *, secure_key: RSAPrivateKey | None = None, require_secure: bool = False,
) -> None:
    blueprint = Blueprint("activation_api", __name__, url_prefix="/api/activation")

    @blueprint.errorhandler(ActivationError)
    def activation_error(error: ActivationError):
        return jsonify(_error_payload(error)), error.status

    @blueprint.after_request
    def no_store(response):
        response.headers["Cache-Control"] = "no-store"
        return response

    @blueprint.before_request
    def block_plaintext_downgrade():
        if (require_secure or secure_key is not None) and request.endpoint != "activation_api.secure":
            raise ActivationError("secure_transport_required", "Encrypted activation is required.", 403)

    @blueprint.post("/secure")
    def secure():
        if secure_key is None:
            raise ActivationError("secure_transport_unavailable", "Encrypted activation is unavailable.", 503)
        try:
            envelope = _json_payload()
        except ActivationError:
            raise invalid_envelope() from None
        address = request.remote_addr or ""
        service.limit_http_decryption(address)
        action, payload, response_key = decrypt_request(secure_key, envelope)
        try:
            if action == "challenge":
                result = service.create_challenge(payload, address)
            else:
                result = service.execute(action, payload)
        except ActivationError as error:
            return jsonify(encrypt_response(response_key, error.status, _error_payload(error)))
        return jsonify(encrypt_response(response_key, 200, result))

    @blueprint.post("/challenge")
    def challenge():
        return jsonify(service.create_challenge(_json_payload(), request.remote_addr or ""))

    @blueprint.post("/<action>")
    def execute(action: str):
        if action not in ACTIONS:
            raise ActivationError("invalid_request", "Unknown activation operation.", 404)
        return jsonify(service.execute(action, _json_payload()))

    app.register_blueprint(blueprint)


def _error_payload(error: ActivationError) -> dict:
    return {"error": error.code, "message": str(error)}


def _json_payload() -> dict:
    try:
        payload = request.get_json(silent=True)
    except RecursionError:
        payload = None
    if not isinstance(payload, dict):
        raise ActivationError("invalid_request", "Expected a JSON object.")
    return payload

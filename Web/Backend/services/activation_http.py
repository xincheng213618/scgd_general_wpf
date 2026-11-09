"""Authenticated encryption for desktop activation on an HTTP origin."""

from __future__ import annotations

import base64
import json
import secrets
from pathlib import Path

from cryptography.exceptions import InvalidTag, UnsupportedAlgorithm
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding, rsa
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

from services.activation import ACTIONS, ActivationError


REQUEST_AAD = b"ColorVision Activation HTTP v1 request"
RESPONSE_AAD = b"ColorVision Activation HTTP v1 response"
_KEY_PADDING = padding.OAEP(mgf=padding.MGF1(hashes.SHA256()), algorithm=hashes.SHA256(), label=None)
_CIPHERTEXT_LIMIT = 4096 + 16


def load_http_private_key(path: Path) -> rsa.RSAPrivateKey:
    try:
        key = serialization.load_pem_private_key(path.read_bytes(), password=None)
    except (OSError, TypeError, ValueError, UnsupportedAlgorithm):
        raise ValueError("Activation HTTP key file must contain a readable unencrypted private PEM.") from None
    if not isinstance(key, rsa.RSAPrivateKey) or key.key_size < 3072:
        raise ValueError("Activation HTTP key must be RSA with at least 3072 bits.")
    return key


def invalid_envelope() -> ActivationError:
    return ActivationError("invalid_envelope", "Invalid encrypted activation request.")


def decrypt_request(key: rsa.RSAPrivateKey, envelope: dict) -> tuple[str, dict, bytes]:
    try:
        if set(envelope) != {"key", "nonce", "ciphertext"} or not all(isinstance(value, str) for value in envelope.values()):
            raise ValueError
        wrapped_key = base64.b64decode(envelope["key"], validate=True)
        nonce = base64.b64decode(envelope["nonce"], validate=True)
        ciphertext = base64.b64decode(envelope["ciphertext"], validate=True)
        if len(wrapped_key) != (key.key_size + 7) // 8 or len(nonce) != 12 or not 16 <= len(ciphertext) <= _CIPHERTEXT_LIMIT:
            raise ValueError
        keys = key.decrypt(wrapped_key, _KEY_PADDING)
        if len(keys) != 64 or keys[:32] == keys[32:]:
            raise ValueError
        plaintext = AESGCM(keys[:32]).decrypt(nonce, ciphertext, REQUEST_AAD)
        decoded = json.loads(plaintext.decode("utf-8"))
        if not isinstance(decoded, dict) or set(decoded) != {"action", "payload"}:
            raise ValueError
        action, payload = decoded["action"], decoded["payload"]
        if not isinstance(action, str) or (action != "challenge" and action not in ACTIONS) or not isinstance(payload, dict):
            raise ValueError
        return action, payload, keys[32:]
    except (InvalidTag, ValueError, RecursionError):
        raise invalid_envelope() from None


def encrypt_response(response_key: bytes, status: int, payload: dict) -> dict:
    nonce = secrets.token_bytes(12)
    plaintext = json.dumps({"status": status, "payload": payload}, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    ciphertext = AESGCM(response_key).encrypt(nonce, plaintext, RESPONSE_AAD)
    return {
        "nonce": base64.b64encode(nonce).decode("ascii"),
        "ciphertext": base64.b64encode(ciphertext).decode("ascii"),
    }

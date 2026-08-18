package com.colorvision.xcviewer;

import android.net.Uri;
import android.util.Base64;

import org.json.JSONObject;

import java.nio.charset.StandardCharsets;

final class OperationsPairingPayload {
    private static final String ERROR_INVALID = "pairing_qr_invalid";
    private static final String ERROR_UNSUPPORTED = "pairing_qr_unsupported";
    private static final String ERROR_SECURITY_INVALID = "pairing_qr_security_invalid";
    private static final int MAXIMUM_QR_CHARACTERS = 8_192;
    final String pairingId;
    final String nonce;
    final String hostId;
    final String endpoint;
    final String certificateSha256;
    final String expiresAt;

    private OperationsPairingPayload(
            String pairingId,
            String nonce,
            String hostId,
            String endpoint,
            String certificateSha256,
            String expiresAt) {
        this.pairingId = pairingId;
        this.nonce = nonce;
        this.hostId = hostId;
        this.endpoint = endpoint;
        this.certificateSha256 = certificateSha256;
        this.expiresAt = expiresAt;
    }

    static boolean isPairingInput(String raw) {
        String text = raw == null ? "" : raw.trim();
        return text.length() <= MAXIMUM_QR_CHARACTERS
                && (text.startsWith("colorvision://pair")
                || text.matches("[A-Za-z0-9_-]{100,}"));
    }

    static OperationsPairingPayload parse(String raw) throws Exception {
        try {
            String text = raw == null ? "" : raw.trim();
            if (text.isEmpty() || text.length() > MAXIMUM_QR_CHARACTERS) {
                throw invalid(ERROR_INVALID);
            }
            String encoded = text;
            if (!text.matches("[A-Za-z0-9_-]{100,}")) {
                Uri uri = Uri.parse(text);
                if (!"colorvision".equalsIgnoreCase(uri.getScheme())
                        || !"pair".equalsIgnoreCase(uri.getHost())) {
                    throw invalid(ERROR_INVALID);
                }
                encoded = uri.getQueryParameter("payload");
                if (encoded == null || encoded.isEmpty()) {
                    throw invalid(ERROR_INVALID);
                }
            }

            byte[] bytes = Base64.decode(encoded, Base64.URL_SAFE | Base64.NO_WRAP | Base64.NO_PADDING);
            JSONObject json = new JSONObject(new String(bytes, StandardCharsets.UTF_8));
            if (json.optInt("version", 0) != 1) {
                throw invalid(ERROR_UNSUPPORTED);
            }
            String endpoint = required(json, "endpoint");
            Uri endpointUri = Uri.parse(endpoint);
            if (!"https".equalsIgnoreCase(endpointUri.getScheme()) || endpointUri.getHost() == null) {
                throw invalid(ERROR_SECURITY_INVALID);
            }
            String pin = required(json, "certificateSha256").toLowerCase();
            if (!pin.matches("[0-9a-f]{64}")) {
                throw invalid(ERROR_SECURITY_INVALID);
            }
            String pairingId = required(json, "pairingId");
            String nonce = required(json, "nonce");
            String hostId = required(json, "hostId");
            if (!pairingId.matches("(?i)[0-9a-f]{32}")
                    || !nonce.matches("[A-Za-z0-9_-]{43}")
                    || !OperationsRelayPolicy.isSafeIdentifier(hostId)) {
                throw invalid(ERROR_SECURITY_INVALID);
            }
            String expiresAt = required(json, "expiresAt");
            PairingQrExpiryPolicy.validate(expiresAt, System.currentTimeMillis());
            return new OperationsPairingPayload(
                    pairingId,
                    nonce,
                    hostId,
                    endpoint,
                    pin,
                    expiresAt);
        } catch (IllegalArgumentException ex) {
            if (ex.getMessage() != null && ex.getMessage().startsWith("pairing_qr_")) {
                throw ex;
            }
            throw invalid(ERROR_INVALID, ex);
        } catch (Exception ex) {
            throw invalid(ERROR_INVALID, ex);
        }
    }

    String canonical(String deviceId, String deviceName) {
        return String.join("\n", "colorvision-pair-v1", pairingId, nonce, hostId,
                endpoint, deviceId, deviceName);
    }

    private static String required(JSONObject json, String name) {
        String value = json.optString(name, "").trim();
        if (value.isEmpty()) {
            throw invalid(ERROR_INVALID);
        }
        return value;
    }

    private static IllegalArgumentException invalid(String code) {
        return new IllegalArgumentException(code);
    }

    private static IllegalArgumentException invalid(String code, Exception cause) {
        return new IllegalArgumentException(code, cause);
    }
}

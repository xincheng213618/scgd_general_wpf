package com.colorvision.xcviewer;

import android.net.Uri;
import android.util.Base64;

import org.json.JSONObject;

import java.nio.charset.StandardCharsets;

final class OperationsPairingPayload {
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

    static OperationsPairingPayload parse(String raw) throws Exception {
        Uri uri = Uri.parse(raw);
        if (!"colorvision".equalsIgnoreCase(uri.getScheme()) || !"pair".equalsIgnoreCase(uri.getHost())) {
            throw new IllegalArgumentException("不是 ColorVision 安全配对码");
        }
        String encoded = uri.getQueryParameter("payload");
        if (encoded == null || encoded.isEmpty()) {
            throw new IllegalArgumentException("配对码缺少安全载荷");
        }

        byte[] bytes = Base64.decode(encoded, Base64.URL_SAFE | Base64.NO_WRAP | Base64.NO_PADDING);
        JSONObject json = new JSONObject(new String(bytes, StandardCharsets.UTF_8));
        if (json.optInt("version", 0) != 1) {
            throw new IllegalArgumentException("不支持的配对协议版本");
        }
        String endpoint = required(json, "endpoint");
        Uri endpointUri = Uri.parse(endpoint);
        if (!"https".equalsIgnoreCase(endpointUri.getScheme()) || endpointUri.getHost() == null) {
            throw new IllegalArgumentException("配对地址必须使用 HTTPS");
        }
        String pin = required(json, "certificateSha256").toLowerCase();
        if (!pin.matches("[0-9a-f]{64}")) {
            throw new IllegalArgumentException("证书指纹格式无效");
        }
        String expiresAt = required(json, "expiresAt");
        return new OperationsPairingPayload(
                required(json, "pairingId"),
                required(json, "nonce"),
                required(json, "hostId"),
                endpoint,
                pin,
                expiresAt);
    }

    String canonical(String deviceId, String deviceName) {
        return String.join("\n", "colorvision-pair-v1", pairingId, nonce, hostId,
                endpoint, deviceId, deviceName);
    }

    private static String required(JSONObject json, String name) {
        String value = json.optString(name, "").trim();
        if (value.isEmpty()) {
            throw new IllegalArgumentException("配对码缺少 " + name);
        }
        return value;
    }
}

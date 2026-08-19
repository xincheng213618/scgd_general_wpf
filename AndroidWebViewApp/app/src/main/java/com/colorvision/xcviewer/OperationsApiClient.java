package com.colorvision.xcviewer;

import android.util.Base64;

import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.util.Locale;

import javax.net.ssl.HttpsURLConnection;
import javax.net.ssl.SSLContext;
import javax.net.ssl.TrustManager;

final class OperationsApiClient {
    private static final int DEFAULT_CONNECT_TIMEOUT_MILLISECONDS = 7_000;
    private static final int DEFAULT_READ_TIMEOUT_MILLISECONDS = 10_000;

    private final String endpoint;
    private final String deviceId;
    private final OperationsDeviceIdentity identity;
    private final SSLContext sslContext;
    private final OperationsPinnedTlsPolicy tlsPolicy;
    private final int connectTimeoutMilliseconds;
    private final int readTimeoutMilliseconds;

    OperationsApiClient(String endpoint, String certificatePin, String deviceId, OperationsDeviceIdentity identity) throws Exception {
        this(endpoint, certificatePin, deviceId, identity,
                DEFAULT_CONNECT_TIMEOUT_MILLISECONDS, DEFAULT_READ_TIMEOUT_MILLISECONDS);
    }

    OperationsApiClient(
            String endpoint,
            String certificatePin,
            String deviceId,
            OperationsDeviceIdentity identity,
            int connectTimeoutMilliseconds,
            int readTimeoutMilliseconds) throws Exception {
        if (connectTimeoutMilliseconds < 1_000 || connectTimeoutMilliseconds > 30_000
                || readTimeoutMilliseconds < 1_000 || readTimeoutMilliseconds > 30_000) {
            throw new IllegalArgumentException("invalid_operations_timeout");
        }
        if (endpoint == null || endpoint.trim().isEmpty()) {
            throw new IllegalArgumentException("invalid_operations_endpoint");
        }
        this.endpoint = endpoint.trim().replaceAll("/+$", "");
        URL endpointUrl = new URL(this.endpoint);
        if (!"https".equalsIgnoreCase(endpointUrl.getProtocol()) || endpointUrl.getHost().isEmpty()) {
            throw new IllegalArgumentException("invalid_operations_endpoint");
        }
        this.deviceId = deviceId;
        this.identity = identity;
        this.connectTimeoutMilliseconds = connectTimeoutMilliseconds;
        this.readTimeoutMilliseconds = readTimeoutMilliseconds;
        tlsPolicy = new OperationsPinnedTlsPolicy(endpointUrl.getHost(), certificatePin);
        sslContext = SSLContext.getInstance("TLS");
        sslContext.init(null, new TrustManager[]{tlsPolicy}, new SecureRandom());
    }

    JSONObject submitClaim(OperationsPairingPayload payload, String deviceName) throws Exception {
        JSONObject body = new JSONObject();
        body.put("pairingId", payload.pairingId);
        body.put("deviceId", deviceId);
        body.put("deviceName", deviceName);
        body.put("publicKeySpki", identity.getPublicKeySpki());
        body.put("signature", identity.sign(payload.canonical(deviceId, deviceName)));
        return execute("POST", "/ops/v1/pairing/claim", "", body.toString().getBytes(StandardCharsets.UTF_8), false);
    }

    JSONObject pairingStatus(String pairingId) throws Exception {
        String query = "?pairingId=" + java.net.URLEncoder.encode(pairingId, "UTF-8")
                + "&deviceId=" + java.net.URLEncoder.encode(deviceId, "UTF-8");
        return execute("GET", "/ops/v1/pairing/status", query, new byte[0], false);
    }

    JSONObject get(String path) throws Exception {
        return execute("GET", path, "", new byte[0], true);
    }

    JSONObject post(String path, JSONObject body) throws Exception {
        return execute("POST", path, "", body.toString().getBytes(StandardCharsets.UTF_8), true);
    }

    byte[] getBytes(String path, int maximumBytes) throws Exception {
        return getBytes(path, maximumBytes, "application/zip", "diagnostic_bundle");
    }

    byte[] getBytes(String path, int maximumBytes, String expectedContentType, String errorPrefix) throws Exception {
        URL url = new URL(endpoint + path);
        HttpsURLConnection connection = (HttpsURLConnection) url.openConnection();
        try {
            connection.setSSLSocketFactory(sslContext.getSocketFactory());
            connection.setHostnameVerifier(tlsPolicy);
            connection.setRequestMethod("GET");
            connection.setConnectTimeout(7000);
            connection.setReadTimeout(30000);
            connection.setUseCaches(false);
            connection.setRequestProperty("Accept", expectedContentType);
            connection.setRequestProperty("X-Correlation-Id", java.util.UUID.randomUUID().toString());
            applySignedHeaders(connection, "GET", path, new byte[0]);

            int status = connection.getResponseCode();
            if (status < 200 || status >= 300) {
                String text = readAll(connection.getErrorStream());
                JSONObject response = text.isEmpty() ? new JSONObject() : new JSONObject(text);
                JSONObject error = response.optJSONObject("error");
                String code = error == null ? "http_" + status : error.optString("code", "http_" + status);
                throw new IllegalStateException(code);
            }
            String contentType = connection.getContentType();
            if (contentType == null || !contentType.toLowerCase(Locale.ROOT)
                    .startsWith(expectedContentType.toLowerCase(Locale.ROOT))) {
                throw new IllegalStateException(errorPrefix + "_type_rejected");
            }
            int contentLength = connection.getContentLength();
            if (contentLength <= 0 || contentLength > maximumBytes) {
                throw new IllegalStateException(errorPrefix + "_size_rejected");
            }
            byte[] data = readAllBytes(connection.getInputStream(), maximumBytes, errorPrefix);
            String expectedHash = connection.getHeaderField("X-CV-Content-SHA256");
            String actualHash = hex(MessageDigest.getInstance("SHA-256").digest(data));
            if (expectedHash == null || !MessageDigest.isEqual(
                    expectedHash.toLowerCase(Locale.ROOT).getBytes(StandardCharsets.US_ASCII),
                    actualHash.getBytes(StandardCharsets.US_ASCII))) {
                throw new SecurityException(errorPrefix + "_hash_mismatch");
            }
            return data;
        } finally {
            connection.disconnect();
        }
    }

    private JSONObject execute(String method, String path, String query, byte[] body, boolean signed) throws Exception {
        URL url = new URL(endpoint + path + query);
        HttpsURLConnection connection = (HttpsURLConnection) url.openConnection();
        connection.setSSLSocketFactory(sslContext.getSocketFactory());
        connection.setHostnameVerifier(tlsPolicy);
        connection.setRequestMethod(method);
        connection.setConnectTimeout(connectTimeoutMilliseconds);
        connection.setReadTimeout(readTimeoutMilliseconds);
        connection.setUseCaches(false);
        connection.setRequestProperty("Accept", "application/json");
        connection.setRequestProperty("X-Correlation-Id", java.util.UUID.randomUUID().toString());
        if (signed) {
            applySignedHeaders(connection, method, path, body);
        }
        if (body.length > 0) {
            connection.setDoOutput(true);
            connection.setRequestProperty("Content-Type", "application/json; charset=utf-8");
            connection.setFixedLengthStreamingMode(body.length);
            try (OutputStream output = connection.getOutputStream()) {
                output.write(body);
            }
        }

        int status = connection.getResponseCode();
        InputStream input = status >= 400 ? connection.getErrorStream() : connection.getInputStream();
        String text = readAll(input);
        connection.disconnect();
        JSONObject response = text.isEmpty() ? new JSONObject() : new JSONObject(text);
        if (status < 200 || status >= 300) {
            JSONObject error = response.optJSONObject("error");
            String code = error == null ? "http_" + status : error.optString("code", "http_" + status);
            throw new IllegalStateException(code);
        }
        return response;
    }

    private void applySignedHeaders(HttpsURLConnection connection, String method, String path, byte[] body) throws Exception {
        String timestamp = Long.toString(System.currentTimeMillis() / 1000L);
        String nonce = randomNonce();
        String bodyHash = hex(MessageDigest.getInstance("SHA-256").digest(body));
        String canonical = String.join("\n", method.toUpperCase(Locale.ROOT), path, timestamp, nonce, bodyHash);
        connection.setRequestProperty("X-CV-Device-Id", deviceId);
        connection.setRequestProperty("X-CV-Timestamp", timestamp);
        connection.setRequestProperty("X-CV-Nonce", nonce);
        connection.setRequestProperty("X-CV-Signature", identity.sign(canonical));
    }

    private static String readAll(InputStream input) throws Exception {
        if (input == null) {
            return "";
        }
        StringBuilder text = new StringBuilder();
        try (BufferedReader reader = new BufferedReader(new InputStreamReader(input, StandardCharsets.UTF_8))) {
            String line;
            while ((line = reader.readLine()) != null) {
                text.append(line);
            }
        }
        return text.toString();
    }

    private static byte[] readAllBytes(InputStream input, int maximumBytes, String errorPrefix) throws Exception {
        if (input == null) {
            throw new IllegalStateException("empty_" + errorPrefix);
        }
        try (InputStream source = input; ByteArrayOutputStream output = new ByteArrayOutputStream()) {
            byte[] buffer = new byte[8192];
            int read;
            int total = 0;
            while ((read = source.read(buffer)) >= 0) {
                total += read;
                if (total > maximumBytes) {
                    throw new IllegalStateException(errorPrefix + "_size_rejected");
                }
                output.write(buffer, 0, read);
            }
            if (total == 0) {
                throw new IllegalStateException("empty_" + errorPrefix);
            }
            return output.toByteArray();
        }
    }

    private static String randomNonce() {
        byte[] bytes = new byte[24];
        new SecureRandom().nextBytes(bytes);
        return Base64.encodeToString(bytes, Base64.URL_SAFE | Base64.NO_WRAP | Base64.NO_PADDING);
    }

    private static String hex(byte[] bytes) {
        StringBuilder text = new StringBuilder(bytes.length * 2);
        for (byte value : bytes) {
            text.append(String.format(Locale.ROOT, "%02x", value & 0xff));
        }
        return text.toString();
    }

}

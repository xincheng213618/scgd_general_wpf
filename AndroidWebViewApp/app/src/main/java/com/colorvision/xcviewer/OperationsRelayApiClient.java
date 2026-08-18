package com.colorvision.xcviewer;

import android.util.Base64;

import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.security.Signature;
import java.security.cert.CertificateFactory;
import java.security.cert.X509Certificate;
import java.security.interfaces.RSAPublicKey;
import java.util.Locale;
import java.util.UUID;

final class OperationsRelayApiClient {
    private static final int MAXIMUM_RESPONSE_CHARACTERS = 128 * 1024;
    private static final int DEFAULT_CONNECT_TIMEOUT_MILLISECONDS = 7_000;
    private static final int DEFAULT_READ_TIMEOUT_MILLISECONDS = 12_000;

    private final String endpoint;
    private final String hostId;
    private final String deviceId;
    private final String certificatePin;
    private final OperationsDeviceIdentity identity;
    private final int connectTimeoutMilliseconds;
    private final int readTimeoutMilliseconds;

    OperationsRelayApiClient(
            String hostId,
            String deviceId,
            String certificatePin,
            OperationsDeviceIdentity identity) throws Exception {
        this(hostId, deviceId, certificatePin, identity,
                DEFAULT_CONNECT_TIMEOUT_MILLISECONDS, DEFAULT_READ_TIMEOUT_MILLISECONDS);
    }

    OperationsRelayApiClient(
            String hostId,
            String deviceId,
            String certificatePin,
            OperationsDeviceIdentity identity,
            int connectTimeoutMilliseconds,
            int readTimeoutMilliseconds) throws Exception {
        if (connectTimeoutMilliseconds < 1_000 || connectTimeoutMilliseconds > 30_000
                || readTimeoutMilliseconds < 1_000 || readTimeoutMilliseconds > 30_000) {
            throw new IllegalArgumentException("invalid_relay_timeout");
        }
        if (!OperationsRelayPolicy.isSafeIdentifier(hostId)
                || !OperationsRelayPolicy.isSafeIdentifier(deviceId)) {
            throw new SecurityException("invalid_relay_identity");
        }
        endpoint = OperationsRelayPolicy.fixedBaseUrl().toExternalForm().replaceAll("/+$", "");
        this.hostId = hostId;
        this.deviceId = deviceId;
        this.certificatePin = certificatePin == null
                ? "" : certificatePin.toLowerCase(Locale.ROOT);
        if (!this.certificatePin.matches("[0-9a-f]{64}")) {
            throw new SecurityException("invalid_host_certificate_pin");
        }
        this.identity = identity;
        this.connectTimeoutMilliseconds = connectTimeoutMilliseconds;
        this.readTimeoutMilliseconds = readTimeoutMilliseconds;
    }

    JSONObject getSnapshot() throws Exception {
        JSONObject response = post(
                "/api/ops/v1/device-relay/hosts/" + hostId + "/snapshot",
                new JSONObject());
        return verifySnapshotResponse(response);
    }

    JSONObject createTask(String capabilityId, JSONObject payload) throws Exception {
        if (!OperationsRelayPolicy.isAllowedTaskCapability(capabilityId)) {
            throw new SecurityException("task_capability_not_allowed");
        }
        if (OperationsRelayPolicy.CAPABILITY_CAPTURE_WINDOW_SNAPSHOT.equals(capabilityId)) {
            OperationsRemoteWindowSnapshot.validateRequestPayload(payload);
        }
        JSONObject body = new JSONObject();
        body.put("hostId", hostId);
        body.put("capabilityId", capabilityId);
        body.put("payload", payload == null ? new JSONObject() : payload);
        String idempotencyKey = UUID.randomUUID().toString().replace("-", "");
        body.put("idempotencyKey", idempotencyKey);
        body.put("ttlSeconds", OperationsRelayPolicy.remoteTaskTtlSeconds(capabilityId));
        JSONObject response = post("/api/ops/v1/device-relay/tasks", body);
        response.put("requestIdempotencyKey", idempotencyKey);
        return response;
    }

    JSONObject getTask(String taskId, String expectedIdempotencyKey) throws Exception {
        if (!OperationsRelayPolicy.isSafeIdentifier(taskId)
                || !OperationsRelayPolicy.isSafeIdentifier(expectedIdempotencyKey)) {
            throw new SecurityException("invalid_task_id");
        }
        JSONObject body = new JSONObject();
        body.put("hostId", hostId);
        return verifyTaskResponse(
                post("/api/ops/v1/device-relay/tasks/" + taskId, body),
                taskId,
                expectedIdempotencyKey);
    }

    byte[] downloadWindowSnapshot(
            String taskId, int expectedBytes, String expectedSha256) throws Exception {
        if (!OperationsRelayPolicy.isSafeIdentifier(taskId)
                || expectedBytes < OperationsRemoteWindowSnapshot.MINIMUM_SEALED_BYTES
                || expectedBytes > OperationsRemoteWindowSnapshot.MAXIMUM_SEALED_BYTES
                || expectedSha256 == null
                || !expectedSha256.matches("[0-9a-f]{64}")) {
            throw new SecurityException("invalid_window_snapshot_download_request");
        }
        String path = "/api/ops/v1/device-relay/tasks/" + taskId + "/window-snapshot";
        JSONObject body = new JSONObject();
        body.put("hostId", hostId);
        byte[] requestBytes = body.toString().getBytes(StandardCharsets.UTF_8);
        URL url = new URL(endpoint + path);
        if (!OperationsRelayPolicy.isAllowedRequestUrl(url)) {
            throw new SecurityException("relay_request_origin_rejected");
        }

        HttpURLConnection connection = (HttpURLConnection) url.openConnection();
        try {
            connection.setInstanceFollowRedirects(false);
            connection.setRequestMethod("POST");
            connection.setConnectTimeout(7_000);
            connection.setReadTimeout(30_000);
            connection.setUseCaches(false);
            connection.setDoOutput(true);
            connection.setRequestProperty("Accept", "application/octet-stream");
            connection.setRequestProperty("Content-Type", "application/json; charset=utf-8");
            connection.setRequestProperty("Cache-Control", "no-store");
            connection.setRequestProperty("X-Correlation-Id", UUID.randomUUID().toString());
            connection.setFixedLengthStreamingMode(requestBytes.length);
            applySignedHeaders(connection, path, requestBytes);
            try (OutputStream output = connection.getOutputStream()) {
                output.write(requestBytes);
            }

            int status = connection.getResponseCode();
            if (status != 200) {
                throw new IllegalStateException(readErrorCode(connection, status));
            }
            String contentType = connection.getContentType();
            if (contentType == null
                    || !"application/octet-stream".equalsIgnoreCase(
                            contentType.split(";", 2)[0].trim())) {
                throw new SecurityException("window_snapshot_type_rejected");
            }
            String contentEncoding = connection.getHeaderField("Content-Encoding");
            if (contentEncoding != null && !contentEncoding.trim().isEmpty()) {
                throw new SecurityException("window_snapshot_encoding_rejected");
            }
            int contentLength = connection.getContentLength();
            if (contentLength != expectedBytes
                    || contentLength < OperationsRemoteWindowSnapshot.MINIMUM_SEALED_BYTES
                    || contentLength > OperationsRemoteWindowSnapshot.MAXIMUM_SEALED_BYTES) {
                throw new SecurityException("window_snapshot_size_rejected");
            }
            byte[] sealed = readAllBytes(
                    connection.getInputStream(), OperationsRemoteWindowSnapshot.MAXIMUM_SEALED_BYTES);
            if (sealed.length != expectedBytes
                    || !MessageDigest.isEqual(
                            expectedSha256.getBytes(StandardCharsets.US_ASCII),
                            hex(MessageDigest.getInstance("SHA-256").digest(sealed))
                                    .getBytes(StandardCharsets.US_ASCII))) {
                throw new SecurityException("window_snapshot_sealed_hash_mismatch");
            }
            return sealed;
        } finally {
            connection.disconnect();
        }
    }

    void consumeWindowSnapshot(String taskId, String sealedSha256) throws Exception {
        if (!OperationsRelayPolicy.isSafeIdentifier(taskId)
                || sealedSha256 == null || !sealedSha256.matches("[0-9a-f]{64}")) {
            throw new SecurityException("invalid_window_snapshot_consume_request");
        }
        JSONObject body = new JSONObject();
        body.put("hostId", hostId);
        body.put("sealedSha256", sealedSha256);
        post("/api/ops/v1/device-relay/tasks/" + taskId + "/window-snapshot/consume", body);
    }

    private JSONObject verifySnapshotResponse(JSONObject response) throws Exception {
        X509Certificate certificate = verifyHostCertificate(
                response.optString("hostCertificateDer", ""));
        JSONObject signed = verifyHostEnvelope(
                certificate,
                "colorvision-relay-snapshot-v1",
                response.optJSONObject("hostEnvelope"));
        if (signed.length() != 6
                || !hostId.equals(signed.optString("hostId", ""))
                || !(signed.opt("capabilities") instanceof org.json.JSONArray)
                || !(signed.opt("snapshot") instanceof JSONObject)
                || signed.optLong("signedAt", 0L) <= 0L) {
            throw new SecurityException("invalid_signed_host_snapshot");
        }
        JSONObject host = new JSONObject();
        host.put("hostId", hostId);
        host.put("displayName", "ColorVision 工作站");
        host.put("appVersion", signed.optString("appVersion", ""));
        host.put("status", signed.optString("status", "unknown"));
        host.put("capabilities", signed.getJSONArray("capabilities"));
        host.put("snapshot", signed.getJSONObject("snapshot"));
        host.put("signedAt", signed.getLong("signedAt"));
        JSONObject verified = new JSONObject();
        verified.put("ok", true);
        verified.put("host", host);
        return verified;
    }

    private JSONObject verifyTaskResponse(
            JSONObject response,
            String expectedTaskId,
            String expectedIdempotencyKey) throws Exception {
        X509Certificate certificate = verifyHostCertificate(
                response.optString("hostCertificateDer", ""));
        JSONObject task = response.optJSONObject("task");
        if (task == null || !expectedTaskId.equals(task.optString("taskId", ""))) {
            throw new SecurityException("invalid_relay_task_response");
        }
        org.json.JSONArray verifiedReceipts = new org.json.JSONArray();
        org.json.JSONArray receipts = task.optJSONArray("receipts");
        if (receipts != null) {
            for (int index = 0; index < receipts.length(); index++) {
                JSONObject receipt = receipts.optJSONObject(index);
                JSONObject envelope = receipt == null ? null : receipt.optJSONObject("hostEnvelope");
                if (envelope == null) {
                    continue;
                }
                JSONObject signed = verifyHostEnvelope(
                        certificate, "colorvision-relay-receipt-v1", envelope);
                if (signed.length() != 6
                        || !hostId.equals(signed.optString("hostId", ""))
                        || !expectedTaskId.equals(signed.optString("taskId", ""))
                        || !expectedIdempotencyKey.equals(
                                signed.optString("idempotencyKey", ""))
                        || !(signed.opt("evidence") instanceof JSONObject)
                        || signed.optLong("signedAt", 0L) <= 0L) {
                    throw new SecurityException("invalid_signed_task_receipt");
                }
                JSONObject verifiedReceipt = new JSONObject();
                verifiedReceipt.put("status", signed.optString("status", ""));
                verifiedReceipt.put("evidence", signed.getJSONObject("evidence"));
                verifiedReceipt.put("signedAt", signed.getLong("signedAt"));
                verifiedReceipts.put(verifiedReceipt);
            }
        }
        JSONObject verifiedTask = new JSONObject();
        verifiedTask.put("taskId", expectedTaskId);
        verifiedTask.put("receipts", verifiedReceipts);
        JSONObject verified = new JSONObject();
        verified.put("ok", true);
        verified.put("task", verifiedTask);
        return verified;
    }

    private X509Certificate verifyHostCertificate(String certificateDer) throws Exception {
        byte[] der;
        try {
            der = Base64.decode(certificateDer, Base64.DEFAULT);
        } catch (IllegalArgumentException ex) {
            throw new SecurityException("invalid_host_certificate", ex);
        }
        String actualPin = hex(MessageDigest.getInstance("SHA-256").digest(der));
        if (!MessageDigest.isEqual(
                actualPin.getBytes(StandardCharsets.US_ASCII),
                certificatePin.getBytes(StandardCharsets.US_ASCII))) {
            throw new SecurityException("host_certificate_pin_mismatch");
        }
        X509Certificate certificate = (X509Certificate) CertificateFactory
                .getInstance("X.509")
                .generateCertificate(new java.io.ByteArrayInputStream(der));
        certificate.checkValidity();
        certificate.verify(certificate.getPublicKey());
        if (!(certificate.getPublicKey() instanceof RSAPublicKey)
                || ((RSAPublicKey) certificate.getPublicKey()).getModulus().bitLength() < 3072
                || !("CN=ColorVision Operations " + hostId).equals(
                        certificate.getSubjectX500Principal().getName())) {
            throw new SecurityException("invalid_host_certificate_identity");
        }
        return certificate;
    }

    private JSONObject verifyHostEnvelope(
            X509Certificate certificate,
            String prefix,
            JSONObject envelope) throws Exception {
        if (envelope == null || envelope.length() != 2) {
            throw new SecurityException("signed_host_envelope_required");
        }
        String body = envelope.optString("body", "");
        String encodedSignature = envelope.optString("signature", "");
        if (body.isEmpty() || body.length() > 65_536 || encodedSignature.isEmpty()) {
            throw new SecurityException("invalid_signed_host_envelope");
        }
        Signature verifier = Signature.getInstance("SHA256withRSA");
        verifier.initVerify(certificate.getPublicKey());
        verifier.update((prefix + "\n" + body).getBytes(StandardCharsets.UTF_8));
        if (!verifier.verify(Base64.decode(encodedSignature, Base64.DEFAULT))) {
            throw new SecurityException("invalid_signed_host_envelope");
        }
        return new JSONObject(body);
    }

    private JSONObject post(String path, JSONObject body) throws Exception {
        byte[] bytes = body.toString().getBytes(StandardCharsets.UTF_8);
        URL url = new URL(endpoint + path);
        if (!OperationsRelayPolicy.isAllowedRequestUrl(url)) {
            throw new SecurityException("relay_request_origin_rejected");
        }

        HttpURLConnection connection = (HttpURLConnection) url.openConnection();
        try {
            connection.setInstanceFollowRedirects(false);
            connection.setRequestMethod("POST");
            connection.setConnectTimeout(connectTimeoutMilliseconds);
            connection.setReadTimeout(readTimeoutMilliseconds);
            connection.setUseCaches(false);
            connection.setDoOutput(true);
            connection.setRequestProperty("Accept", "application/json");
            connection.setRequestProperty("Content-Type", "application/json; charset=utf-8");
            connection.setRequestProperty("X-Correlation-Id", UUID.randomUUID().toString());
            connection.setFixedLengthStreamingMode(bytes.length);
            applySignedHeaders(connection, path, bytes);
            try (OutputStream output = connection.getOutputStream()) {
                output.write(bytes);
            }

            int status = connection.getResponseCode();
            InputStream input = status >= 400 ? connection.getErrorStream() : connection.getInputStream();
            String text = readAll(input);
            JSONObject response = text.isEmpty() ? new JSONObject() : new JSONObject(text);
            if (status < 200 || status >= 300) {
                Object rawError = response.opt("error");
                String code;
                if (rawError instanceof JSONObject) {
                    code = ((JSONObject) rawError).optString("code", "http_" + status);
                } else {
                    code = response.optString("error", "http_" + status);
                }
                throw new IllegalStateException(code);
            }
            return response;
        } finally {
            connection.disconnect();
        }
    }

    private void applySignedHeaders(HttpURLConnection connection, String path, byte[] body) throws Exception {
        String timestamp = Long.toString(System.currentTimeMillis() / 1000L);
        String nonce = randomNonce();
        String bodyHash = hex(MessageDigest.getInstance("SHA-256").digest(body));
        String canonical = String.join("\n", "POST", path, timestamp, nonce, bodyHash);
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
        try (BufferedReader reader = new BufferedReader(
                new InputStreamReader(input, StandardCharsets.UTF_8))) {
            char[] buffer = new char[4096];
            int read;
            while ((read = reader.read(buffer)) >= 0) {
                if (text.length() + read > MAXIMUM_RESPONSE_CHARACTERS) {
                    throw new IllegalStateException("relay_response_too_large");
                }
                text.append(buffer, 0, read);
            }
        }
        return text.toString();
    }

    private static byte[] readAllBytes(InputStream input, int maximumBytes) throws Exception {
        if (input == null) {
            throw new IllegalStateException("empty_window_snapshot");
        }
        try (InputStream source = input;
             ByteArrayOutputStream output = new ByteArrayOutputStream()) {
            byte[] buffer = new byte[8192];
            int read;
            int total = 0;
            while ((read = source.read(buffer)) >= 0) {
                total += read;
                if (total > maximumBytes) {
                    throw new SecurityException("window_snapshot_size_rejected");
                }
                output.write(buffer, 0, read);
            }
            if (total == 0) {
                throw new IllegalStateException("empty_window_snapshot");
            }
            return output.toByteArray();
        }
    }

    private static String readErrorCode(HttpURLConnection connection, int status) throws Exception {
        String text = readAll(connection.getErrorStream());
        JSONObject response = text.isEmpty() ? new JSONObject() : new JSONObject(text);
        Object rawError = response.opt("error");
        if (rawError instanceof JSONObject) {
            return ((JSONObject) rawError).optString("code", "http_" + status);
        }
        return response.optString("error", "http_" + status);
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

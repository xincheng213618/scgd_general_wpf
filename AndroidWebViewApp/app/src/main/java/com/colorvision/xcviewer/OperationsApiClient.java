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
import java.security.cert.X509Certificate;
import java.util.Locale;

import javax.net.ssl.HttpsURLConnection;
import javax.net.ssl.SSLContext;
import javax.net.ssl.TrustManager;
import javax.net.ssl.X509TrustManager;

final class OperationsApiClient {
    private final String endpoint;
    private final String certificatePin;
    private final String deviceId;
    private final OperationsDeviceIdentity identity;
    private final SSLContext sslContext;

    OperationsApiClient(String endpoint, String certificatePin, String deviceId, OperationsDeviceIdentity identity) throws Exception {
        this.endpoint = endpoint.replaceAll("/+$", "");
        this.certificatePin = certificatePin.toLowerCase(Locale.ROOT);
        this.deviceId = deviceId;
        this.identity = identity;
        sslContext = SSLContext.getInstance("TLS");
        sslContext.init(null, new TrustManager[]{new PinnedTrustManager(this.certificatePin)}, new SecureRandom());
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

    private JSONObject execute(String method, String path, String query, byte[] body, boolean signed) throws Exception {
        URL url = new URL(endpoint + path + query);
        HttpsURLConnection connection = (HttpsURLConnection) url.openConnection();
        connection.setSSLSocketFactory(sslContext.getSocketFactory());
        connection.setHostnameVerifier((hostname, session) -> hostname.equalsIgnoreCase(url.getHost()));
        connection.setRequestMethod(method);
        connection.setConnectTimeout(7000);
        connection.setReadTimeout(10000);
        connection.setUseCaches(false);
        connection.setRequestProperty("Accept", "application/json");
        connection.setRequestProperty("X-Correlation-Id", java.util.UUID.randomUUID().toString());
        if (signed) {
            String timestamp = Long.toString(System.currentTimeMillis() / 1000L);
            String nonce = randomNonce();
            String bodyHash = hex(MessageDigest.getInstance("SHA-256").digest(body));
            String canonical = String.join("\n", method.toUpperCase(Locale.ROOT), path, timestamp, nonce, bodyHash);
            connection.setRequestProperty("X-CV-Device-Id", deviceId);
            connection.setRequestProperty("X-CV-Timestamp", timestamp);
            connection.setRequestProperty("X-CV-Nonce", nonce);
            connection.setRequestProperty("X-CV-Signature", identity.sign(canonical));
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

    private static final class PinnedTrustManager implements X509TrustManager {
        private final String expectedPin;

        PinnedTrustManager(String expectedPin) {
            this.expectedPin = expectedPin;
        }

        @Override
        public void checkClientTrusted(X509Certificate[] chain, String authType) {
            throw new SecurityException("Client certificates are not accepted");
        }

        @Override
        public void checkServerTrusted(X509Certificate[] chain, String authType) throws java.security.cert.CertificateException {
            if (chain == null || chain.length != 1) {
                throw new java.security.cert.CertificateException("Unexpected server certificate chain");
            }
            try {
                String actual = hex(MessageDigest.getInstance("SHA-256").digest(chain[0].getEncoded()));
                if (!MessageDigest.isEqual(actual.getBytes(StandardCharsets.US_ASCII), expectedPin.getBytes(StandardCharsets.US_ASCII))) {
                    throw new java.security.cert.CertificateException("Certificate pin mismatch");
                }
                chain[0].checkValidity();
            } catch (java.security.cert.CertificateException ex) {
                throw ex;
            } catch (Exception ex) {
                throw new java.security.cert.CertificateException(ex);
            }
        }

        @Override
        public X509Certificate[] getAcceptedIssuers() {
            return new X509Certificate[0];
        }
    }
}

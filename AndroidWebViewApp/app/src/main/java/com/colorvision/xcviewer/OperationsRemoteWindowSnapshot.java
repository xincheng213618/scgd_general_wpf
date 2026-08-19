package com.colorvision.xcviewer;

import android.annotation.SuppressLint;

import org.json.JSONObject;

import java.nio.charset.StandardCharsets;
import java.security.AlgorithmParameters;
import java.security.KeyFactory;
import java.security.MessageDigest;
import java.security.PublicKey;
import java.security.interfaces.ECPublicKey;
import java.security.spec.ECFieldFp;
import java.security.spec.ECGenParameterSpec;
import java.security.spec.ECParameterSpec;
import java.security.spec.X509EncodedKeySpec;
import java.time.Duration;
import java.time.Instant;
import java.time.OffsetDateTime;
import java.time.format.DateTimeFormatter;
import java.util.Arrays;
import java.util.Base64;
import java.util.HashSet;
import java.util.Iterator;
import java.util.Locale;
import java.util.Set;
import java.util.regex.Pattern;

import javax.crypto.Cipher;
import javax.crypto.Mac;
import javax.crypto.spec.GCMParameterSpec;
import javax.crypto.spec.SecretKeySpec;

// Every executable entry point is exposed only when OperationsE2eIdentity.isSupported()
// (Android 12 / API 31). Keeping the protocol code together also lets JVM tests exercise
// the exact Java/.NET wire format without a second parser.
@SuppressLint("NewApi")
final class OperationsRemoteWindowSnapshot {
    static final String CAPABILITY_ID = "ops.window.snapshot.capture";
    static final String SCHEME = "p256-hkdf-sha256-aes256gcm-v1";
    static final String RECEIPT_KIND = "window-snapshot-encrypted-v1";
    static final String ERROR_KIND = "window-snapshot-error-v1";
    static final String ERROR_CODE = "window_snapshot_unavailable";
    static final int MAXIMUM_PLAINTEXT_BYTES = 1536 * 1024;
    static final int MAXIMUM_SEALED_BYTES = MAXIMUM_PLAINTEXT_BYTES + 60;
    static final int MINIMUM_SEALED_BYTES = 61;
    static final int MINIMUM_ANDROID_SDK = 31;
    static final long CLOCK_TOLERANCE_MILLISECONDS = 125_000L;

    private static final int SALT_BYTES = 32;
    private static final int NONCE_BYTES = 12;
    private static final int GCM_TAG_BITS = 128;
    private static final Pattern LOWER_SHA256 = Pattern.compile("^[0-9a-f]{64}$");
    private static final Pattern RFC3339 = Pattern.compile(
            "^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(?:\\.\\d{1,9})?"
                    + "(?:Z|[+-](?:(?:0\\d|1[0-7]):[0-5]\\d|18:00))$");
    private static final Set<String> COMPLETED_KEYS = Set.of(
            "kind", "scheme", "jobId", "hostEphemeralPublicKeySpki",
            "sealedSha256", "sealedBytes", "capturedAt", "expiresAt");
    private static final Set<String> ERROR_KEYS = Set.of("kind", "code");
    private static final Set<String> REQUEST_KEYS = Set.of(
            "scheme", "recipientPublicKeySpki");

    private OperationsRemoteWindowSnapshot() {
    }

    static JSONObject createRequestPayload(String recipientPublicKeySpki) throws Exception {
        JSONObject payload = new JSONObject();
        payload.put("scheme", SCHEME);
        payload.put("recipientPublicKeySpki", recipientPublicKeySpki);
        validateRequestPayload(payload);
        return payload;
    }

    static void validateRequestPayload(JSONObject payload) {
        if (payload == null || !keys(payload).equals(REQUEST_KEYS)
                || !SCHEME.equals(strictString(payload, "scheme"))
                || !isCanonicalP256PublicKey(
                        strictString(payload, "recipientPublicKeySpki"))) {
            throw new SecurityException("window_snapshot_payload_not_allowed");
        }
    }

    static final class Receipt {
        final String jobId;
        final String hostEphemeralPublicKeySpki;
        final String sealedSha256;
        final int sealedBytes;
        final String capturedAt;
        final String expiresAt;

        Receipt(
                String jobId,
                String hostEphemeralPublicKeySpki,
                String sealedSha256,
                int sealedBytes,
                String capturedAt,
                String expiresAt) {
            this.jobId = jobId;
            this.hostEphemeralPublicKeySpki = hostEphemeralPublicKeySpki;
            this.sealedSha256 = sealedSha256;
            this.sealedBytes = sealedBytes;
            this.capturedAt = capturedAt;
            this.expiresAt = expiresAt;
        }
    }

    static Receipt parseCompletedReceipt(JSONObject evidence, long nowMilliseconds) {
        if (evidence == null || !keys(evidence).equals(COMPLETED_KEYS)
                || !RECEIPT_KIND.equals(strictString(evidence, "kind"))
                || !SCHEME.equals(strictString(evidence, "scheme"))) {
            throw invalidReceipt();
        }

        String jobId = strictString(evidence, "jobId");
        String hostPublicKey = strictString(evidence, "hostEphemeralPublicKeySpki");
        String sealedSha256 = strictString(evidence, "sealedSha256");
        int sealedBytes = strictInteger(evidence, "sealedBytes");
        String capturedAt = strictString(evidence, "capturedAt");
        String expiresAt = strictString(evidence, "expiresAt");
        if (!OperationsRelayPolicy.isSafeIdentifier(jobId)
                || !isCanonicalP256PublicKey(hostPublicKey)
                || !LOWER_SHA256.matcher(sealedSha256).matches()
                || sealedBytes < MINIMUM_SEALED_BYTES
                || sealedBytes > MAXIMUM_SEALED_BYTES) {
            throw invalidReceipt();
        }

        OffsetDateTime captured = parseRfc3339(capturedAt);
        OffsetDateTime expires = parseRfc3339(expiresAt);
        Duration lifetime = Duration.between(captured.toInstant(), expires.toInstant());
        Instant now = Instant.ofEpochMilli(nowMilliseconds);
        if (lifetime.isZero() || lifetime.isNegative()
                || lifetime.compareTo(Duration.ofMinutes(5)) > 0
                || captured.toInstant().isAfter(now.plusMillis(CLOCK_TOLERANCE_MILLISECONDS))
                || expires.toInstant().isBefore(now.minusMillis(CLOCK_TOLERANCE_MILLISECONDS))) {
            throw invalidReceipt();
        }
        return new Receipt(jobId, hostPublicKey, sealedSha256, sealedBytes,
                capturedAt, expiresAt);
    }

    static void validateFailedReceipt(JSONObject evidence) {
        if (evidence == null || !keys(evidence).equals(ERROR_KEYS)
                || !ERROR_KIND.equals(strictString(evidence, "kind"))
                || !ERROR_CODE.equals(strictString(evidence, "code"))) {
            throw invalidReceipt();
        }
    }

    static boolean isCanonicalP256PublicKey(String encoded) {
        try {
            parseP256PublicKey(encoded);
            return true;
        } catch (Exception ignored) {
            return false;
        }
    }

    static ECPublicKey parseP256PublicKey(String encoded) throws Exception {
        if (encoded == null || encoded.isEmpty() || encoded.length() > 256) {
            throw new SecurityException("invalid_window_snapshot_public_key");
        }
        byte[] der;
        try {
            der = Base64.getDecoder().decode(encoded);
        } catch (IllegalArgumentException ex) {
            throw new SecurityException("invalid_window_snapshot_public_key", ex);
        }
        if (!Base64.getEncoder().encodeToString(der).equals(encoded)) {
            throw new SecurityException("invalid_window_snapshot_public_key");
        }
        PublicKey value = KeyFactory.getInstance("EC").generatePublic(new X509EncodedKeySpec(der));
        if (!(value instanceof ECPublicKey)) {
            throw new SecurityException("invalid_window_snapshot_public_key");
        }
        if (!MessageDigest.isEqual(der, value.getEncoded())) {
            throw new SecurityException("invalid_window_snapshot_public_key");
        }
        ECPublicKey key = (ECPublicKey) value;
        AlgorithmParameters parameters = AlgorithmParameters.getInstance("EC");
        parameters.init(new ECGenParameterSpec("secp256r1"));
        ECParameterSpec expected = parameters.getParameterSpec(ECParameterSpec.class);
        if (!sameCurve(key.getParams(), expected)) {
            throw new SecurityException("invalid_window_snapshot_public_key");
        }
        return key;
    }

    static String canonicalPublicKey(PublicKey key) throws Exception {
        String encoded = Base64.getEncoder().encodeToString(key.getEncoded());
        parseP256PublicKey(encoded);
        return encoded;
    }

    static byte[] decrypt(
            byte[] sealed,
            byte[] sharedSecret,
            Receipt receipt,
            String hostId,
            String deviceId,
            String taskId,
            String idempotencyKey,
            String recipientPublicKeySpki) throws Exception {
        if (sharedSecret == null || sharedSecret.length != 32) {
            throw new SecurityException("invalid_window_snapshot_shared_secret");
        }
        if (sealed == null || sealed.length != receipt.sealedBytes
                || sealed.length < MINIMUM_SEALED_BYTES
                || sealed.length > MAXIMUM_SEALED_BYTES
                || !MessageDigest.isEqual(
                        receipt.sealedSha256.getBytes(StandardCharsets.US_ASCII),
                        sha256Hex(sealed).getBytes(StandardCharsets.US_ASCII))) {
            throw new SecurityException("window_snapshot_sealed_hash_mismatch");
        }
        if (!OperationsRelayPolicy.isSafeIdentifier(hostId)
                || !OperationsRelayPolicy.isSafeIdentifier(deviceId)
                || !OperationsRelayPolicy.isSafeIdentifier(taskId)
                || !OperationsRelayPolicy.isSafeIdentifier(idempotencyKey)
                || !isCanonicalP256PublicKey(recipientPublicKeySpki)) {
            throw new SecurityException("invalid_window_snapshot_context");
        }

        byte[] salt = Arrays.copyOfRange(sealed, 0, SALT_BYTES);
        byte[] nonce = Arrays.copyOfRange(sealed, SALT_BYTES, SALT_BYTES + NONCE_BYTES);
        byte[] ciphertextAndTag = Arrays.copyOfRange(sealed, SALT_BYTES + NONCE_BYTES, sealed.length);
        byte[] derivedKey = null;
        try {
            byte[] info = buildInfo(hostId, deviceId, taskId, idempotencyKey,
                    recipientPublicKeySpki, receipt.hostEphemeralPublicKeySpki);
            derivedKey = hkdfSha256(sharedSecret, salt, info, 32);
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, new SecretKeySpec(derivedKey, "AES"),
                    new GCMParameterSpec(GCM_TAG_BITS, nonce));
            cipher.updateAAD(buildAad(hostId, deviceId, taskId, idempotencyKey, receipt));
            byte[] plaintext = cipher.doFinal(ciphertextAndTag);
            if (!isValidJpegEnvelope(plaintext)) {
                Arrays.fill(plaintext, (byte) 0);
                throw new SecurityException("window_snapshot_format_rejected");
            }
            return plaintext;
        } finally {
            Arrays.fill(sharedSecret, (byte) 0);
            if (derivedKey != null) {
                Arrays.fill(derivedKey, (byte) 0);
            }
            Arrays.fill(ciphertextAndTag, (byte) 0);
        }
    }

    static byte[] buildInfo(
            String hostId,
            String deviceId,
            String taskId,
            String idempotencyKey,
            String recipientPublicKeySpki,
            String hostEphemeralPublicKeySpki) {
        return String.join("\n",
                "colorvision-window-snapshot-key-v1",
                hostId,
                deviceId,
                taskId,
                idempotencyKey,
                recipientPublicKeySpki,
                hostEphemeralPublicKeySpki).getBytes(StandardCharsets.UTF_8);
    }

    static byte[] buildAad(
            String hostId,
            String deviceId,
            String taskId,
            String idempotencyKey,
            Receipt receipt) {
        return String.join("\n",
                "colorvision-window-snapshot-aad-v1",
                hostId,
                deviceId,
                taskId,
                idempotencyKey,
                receipt.jobId,
                receipt.capturedAt,
                receipt.expiresAt,
                "image/jpeg").getBytes(StandardCharsets.UTF_8);
    }

    static byte[] hkdfSha256(byte[] inputKeyMaterial, byte[] salt, byte[] info, int length)
            throws Exception {
        if (inputKeyMaterial == null || inputKeyMaterial.length == 0
                || salt == null || length <= 0 || length > 255 * 32) {
            throw new IllegalArgumentException("invalid_hkdf_input");
        }
        Mac mac = Mac.getInstance("HmacSHA256");
        mac.init(new SecretKeySpec(salt, "HmacSHA256"));
        byte[] pseudoRandomKey = mac.doFinal(inputKeyMaterial);
        byte[] output = new byte[length];
        byte[] previous = new byte[0];
        int offset = 0;
        try {
            for (int counter = 1; offset < length; counter++) {
                mac.init(new SecretKeySpec(pseudoRandomKey, "HmacSHA256"));
                mac.update(previous);
                if (info != null) {
                    mac.update(info);
                }
                mac.update((byte) counter);
                byte[] block = mac.doFinal();
                Arrays.fill(previous, (byte) 0);
                previous = block;
                int copied = Math.min(block.length, length - offset);
                System.arraycopy(block, 0, output, offset, copied);
                offset += copied;
            }
            return output;
        } finally {
            Arrays.fill(pseudoRandomKey, (byte) 0);
            Arrays.fill(previous, (byte) 0);
        }
    }

    static boolean isValidJpegEnvelope(byte[] data) {
        return data != null
                && data.length >= 4
                && data.length <= MAXIMUM_PLAINTEXT_BYTES
                && (data[0] & 0xff) == 0xff
                && (data[1] & 0xff) == 0xd8
                && (data[data.length - 2] & 0xff) == 0xff
                && (data[data.length - 1] & 0xff) == 0xd9;
    }

    static String sha256Hex(byte[] bytes) throws Exception {
        byte[] digest = MessageDigest.getInstance("SHA-256").digest(bytes);
        StringBuilder text = new StringBuilder(64);
        for (byte value : digest) {
            text.append(String.format(Locale.ROOT, "%02x", value & 0xff));
        }
        return text.toString();
    }

    private static boolean sameCurve(ECParameterSpec actual, ECParameterSpec expected) {
        if (actual == null || expected == null
                || !(actual.getCurve().getField() instanceof ECFieldFp)
                || !(expected.getCurve().getField() instanceof ECFieldFp)) {
            return false;
        }
        return ((ECFieldFp) actual.getCurve().getField()).getP().equals(
                ((ECFieldFp) expected.getCurve().getField()).getP())
                && actual.getCurve().getA().equals(expected.getCurve().getA())
                && actual.getCurve().getB().equals(expected.getCurve().getB())
                && actual.getGenerator().equals(expected.getGenerator())
                && actual.getOrder().equals(expected.getOrder())
                && actual.getCofactor() == expected.getCofactor();
    }

    private static OffsetDateTime parseRfc3339(String value) {
        if (!RFC3339.matcher(value).matches()) {
            throw invalidReceipt();
        }
        try {
            return OffsetDateTime.parse(value, DateTimeFormatter.ISO_OFFSET_DATE_TIME);
        } catch (Exception ex) {
            throw invalidReceipt();
        }
    }

    private static Set<String> keys(JSONObject value) {
        Set<String> names = new HashSet<>();
        Iterator<String> iterator = value.keys();
        while (iterator.hasNext()) {
            names.add(iterator.next());
        }
        return names;
    }

    private static String strictString(JSONObject value, String name) {
        Object raw = value == null ? null : value.opt(name);
        if (!(raw instanceof String) || ((String) raw).isEmpty()) {
            throw invalidReceipt();
        }
        return (String) raw;
    }

    private static int strictInteger(JSONObject value, String name) {
        Object raw = value == null ? null : value.opt(name);
        if (!(raw instanceof Integer) && !(raw instanceof Long)) {
            throw invalidReceipt();
        }
        long number = ((Number) raw).longValue();
        if (number < Integer.MIN_VALUE || number > Integer.MAX_VALUE) {
            throw invalidReceipt();
        }
        return (int) number;
    }

    private static SecurityException invalidReceipt() {
        return new SecurityException("invalid_window_snapshot_receipt");
    }
}

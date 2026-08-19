package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import java.nio.charset.StandardCharsets;
import java.security.KeyPair;
import java.security.KeyPairGenerator;
import java.security.KeyFactory;
import java.security.PrivateKey;
import java.security.PublicKey;
import java.security.SecureRandom;
import java.security.spec.ECGenParameterSpec;
import java.security.spec.PKCS8EncodedKeySpec;
import java.security.spec.X509EncodedKeySpec;
import java.time.Instant;
import java.util.Arrays;
import java.util.Base64;

import javax.crypto.Cipher;
import javax.crypto.KeyAgreement;
import javax.crypto.spec.GCMParameterSpec;
import javax.crypto.spec.SecretKeySpec;

import static org.junit.Assert.assertArrayEquals;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertThrows;
import static org.junit.Assert.assertTrue;

public class OperationsRemoteWindowSnapshotTest {
    private static final String HOST_ID = "host_01";
    private static final String DEVICE_ID = "device_01";
    private static final String TASK_ID = "task_01";
    private static final String IDEMPOTENCY_KEY = "idem_01";
    private static final String JOB_ID = "job_01";
    private static final String CAPTURED_AT = "2026-08-13T04:00:00Z";
    private static final String EXPIRES_AT = "2026-08-13T04:05:00Z";
    private static final long NOW = Instant.parse("2026-08-13T04:02:00Z").toEpochMilli();

    @Test
    public void completedReceiptRequiresTheExactEncryptedSchemaAndFiveMinuteWindow()
            throws Exception {
        KeyPair hostKey = p256KeyPair();
        JSONObject valid = completedReceipt(
                OperationsRemoteWindowSnapshot.canonicalPublicKey(hostKey.getPublic()),
                OperationsRemoteWindowSnapshot.MINIMUM_SEALED_BYTES,
                "0".repeat(64),
                CAPTURED_AT,
                EXPIRES_AT);

        OperationsRemoteWindowSnapshot.Receipt parsed =
                OperationsRemoteWindowSnapshot.parseCompletedReceipt(valid, NOW);
        assertEquals(JOB_ID, parsed.jobId);
        assertEquals(OperationsRemoteWindowSnapshot.MINIMUM_SEALED_BYTES,
                parsed.sealedBytes);

        JSONObject extra = new JSONObject(valid.toString()).put("url", "http://example.com");
        assertThrows(SecurityException.class, () ->
                OperationsRemoteWindowSnapshot.parseCompletedReceipt(extra, NOW));

        JSONObject zeroLifetime = new JSONObject(valid.toString())
                .put("expiresAt", CAPTURED_AT);
        assertThrows(SecurityException.class, () ->
                OperationsRemoteWindowSnapshot.parseCompletedReceipt(zeroLifetime, NOW));

        JSONObject overFiveMinutes = new JSONObject(valid.toString())
                .put("expiresAt", "2026-08-13T04:05:01Z");
        assertThrows(SecurityException.class, () ->
                OperationsRemoteWindowSnapshot.parseCompletedReceipt(overFiveMinutes, NOW));

        JSONObject expired = new JSONObject(valid.toString());
        assertThrows(SecurityException.class, () ->
                OperationsRemoteWindowSnapshot.parseCompletedReceipt(
                        expired, Instant.parse("2026-08-13T04:07:06Z").toEpochMilli()));
    }

    @Test
    public void receiptRejectsNonCanonicalOrNonP256PublicKeys() throws Exception {
        KeyPair p256 = p256KeyPair();
        byte[] trailing = Arrays.copyOf(
                p256.getPublic().getEncoded(), p256.getPublic().getEncoded().length + 1);
        String nonCanonical = Base64.getEncoder().encodeToString(trailing);
        assertFalse(OperationsRemoteWindowSnapshot.isCanonicalP256PublicKey(nonCanonical));

        KeyPairGenerator p384Generator = KeyPairGenerator.getInstance("EC");
        p384Generator.initialize(new ECGenParameterSpec("secp384r1"));
        String p384 = Base64.getEncoder().encodeToString(
                p384Generator.generateKeyPair().getPublic().getEncoded());
        assertFalse(OperationsRemoteWindowSnapshot.isCanonicalP256PublicKey(p384));
        assertTrue(OperationsRemoteWindowSnapshot.isCanonicalP256PublicKey(
                OperationsRemoteWindowSnapshot.canonicalPublicKey(p256.getPublic())));
    }

    @Test
    public void failedReceiptAllowsOnlyTheFrozenError() throws Exception {
        JSONObject valid = new JSONObject()
                .put("kind", OperationsRemoteWindowSnapshot.ERROR_KIND)
                .put("code", OperationsRemoteWindowSnapshot.ERROR_CODE);
        OperationsRemoteWindowSnapshot.validateFailedReceipt(valid);

        assertThrows(SecurityException.class, () ->
                OperationsRemoteWindowSnapshot.validateFailedReceipt(
                        new JSONObject(valid.toString()).put("detail", "path")));
        assertThrows(SecurityException.class, () ->
                OperationsRemoteWindowSnapshot.validateFailedReceipt(
                        new JSONObject(valid.toString()).put("code", "other")));
    }

    @Test
    public void requestPayloadAllowsOnlySchemeAndCanonicalRecipientKey() throws Exception {
        String recipientSpki = OperationsRemoteWindowSnapshot.canonicalPublicKey(
                p256KeyPair().getPublic());
        JSONObject payload = OperationsRemoteWindowSnapshot.createRequestPayload(recipientSpki);
        assertEquals(2, payload.length());
        assertEquals(OperationsRemoteWindowSnapshot.SCHEME,
                payload.getString("scheme"));
        assertEquals(recipientSpki, payload.getString("recipientPublicKeySpki"));

        assertThrows(SecurityException.class, () ->
                OperationsRemoteWindowSnapshot.validateRequestPayload(
                        new JSONObject(payload.toString()).put("width", 1280)));
        assertThrows(SecurityException.class, () ->
                OperationsRemoteWindowSnapshot.validateRequestPayload(
                        new JSONObject(payload.toString()).put("scheme", "other")));
    }

    @Test
    public void hkdfMatchesRfc5869Sha256TestCaseOne() throws Exception {
        byte[] ikm = hex("0b".repeat(22));
        byte[] salt = hex("000102030405060708090a0b0c");
        byte[] info = hex("f0f1f2f3f4f5f6f7f8f9");
        byte[] expected = hex(
                "3cb25f25faacd57a90434f64d0362f2a"
                        + "2d2d0a90cf1a5a4c5db02d56ecc4c5bf"
                        + "34007208d5b887185865");
        assertArrayEquals(expected,
                OperationsRemoteWindowSnapshot.hkdfSha256(ikm, salt, info, 42));
    }

    @Test
    public void encryptedSnapshotRoundTripsAndFailsClosedOnContextOrTagChanges()
            throws Exception {
        KeyPair recipient = p256KeyPair();
        KeyPair host = p256KeyPair();
        String recipientSpki = OperationsRemoteWindowSnapshot.canonicalPublicKey(
                recipient.getPublic());
        String hostSpki = OperationsRemoteWindowSnapshot.canonicalPublicKey(host.getPublic());
        byte[] sharedForEncrypt = sharedSecret(host, recipient);
        byte[] sharedForDecrypt = sharedSecret(recipient, host);
        assertArrayEquals(sharedForEncrypt, sharedForDecrypt);

        byte[] plaintext = hex("ffd8ffe00001ffd9");
        byte[] salt = new byte[32];
        byte[] nonce = new byte[12];
        new SecureRandom().nextBytes(salt);
        new SecureRandom().nextBytes(nonce);
        OperationsRemoteWindowSnapshot.Receipt aadReceipt = receipt(
                hostSpki, "0".repeat(64), 61, CAPTURED_AT, EXPIRES_AT);
        byte[] key = OperationsRemoteWindowSnapshot.hkdfSha256(
                sharedForEncrypt,
                salt,
                OperationsRemoteWindowSnapshot.buildInfo(
                        HOST_ID, DEVICE_ID, TASK_ID, IDEMPOTENCY_KEY,
                        recipientSpki, hostSpki),
                32);
        Cipher encrypt = Cipher.getInstance("AES/GCM/NoPadding");
        encrypt.init(Cipher.ENCRYPT_MODE, new SecretKeySpec(key, "AES"),
                new GCMParameterSpec(128, nonce));
        encrypt.updateAAD(OperationsRemoteWindowSnapshot.buildAad(
                HOST_ID, DEVICE_ID, TASK_ID, IDEMPOTENCY_KEY, aadReceipt));
        byte[] cipherAndTag = encrypt.doFinal(plaintext);
        byte[] sealed = join(salt, nonce, cipherAndTag);
        OperationsRemoteWindowSnapshot.Receipt receipt = receipt(
                hostSpki,
                OperationsRemoteWindowSnapshot.sha256Hex(sealed),
                sealed.length,
                CAPTURED_AT,
                EXPIRES_AT);

        byte[] decrypted = OperationsRemoteWindowSnapshot.decrypt(
                sealed.clone(), sharedForDecrypt.clone(), receipt,
                HOST_ID, DEVICE_ID, TASK_ID, IDEMPOTENCY_KEY, recipientSpki);
        assertArrayEquals(plaintext, decrypted);

        assertThrows(Exception.class, () -> OperationsRemoteWindowSnapshot.decrypt(
                sealed.clone(), sharedSecret(recipient, host), receipt,
                HOST_ID, DEVICE_ID, "other_task", IDEMPOTENCY_KEY, recipientSpki));
        byte[] badTag = sealed.clone();
        badTag[badTag.length - 1] ^= 1;
        OperationsRemoteWindowSnapshot.Receipt badTagReceipt = receipt(
                hostSpki,
                OperationsRemoteWindowSnapshot.sha256Hex(badTag),
                badTag.length,
                CAPTURED_AT,
                EXPIRES_AT);
        assertThrows(Exception.class, () -> OperationsRemoteWindowSnapshot.decrypt(
                badTag, sharedSecret(recipient, host), badTagReceipt,
                HOST_ID, DEVICE_ID, TASK_ID, IDEMPOTENCY_KEY, recipientSpki));
        assertThrows(SecurityException.class, () -> OperationsRemoteWindowSnapshot.decrypt(
                sealed.clone(), new byte[31], receipt,
                HOST_ID, DEVICE_ID, TASK_ID, IDEMPOTENCY_KEY, recipientSpki));

        Arrays.fill(sharedForEncrypt, (byte) 0);
        Arrays.fill(sharedForDecrypt, (byte) 0);
        Arrays.fill(key, (byte) 0);
        Arrays.fill(decrypted, (byte) 0);
    }

    @Test
    public void decryptsTheDeterministicDesktopDotNetVectorWithoutNormalizingTimestamps()
            throws Exception {
        String recipientSpki = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEniJVQZdwBMKNsgN7an9/"
                + "MpuNp9ARnc/o33HQaP6AFMsYo6cqMZrv0Cv0mZn1VFevMl74y3SklrVgljYGqIjY3Q==";
        String recipientPkcs8 = "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgckEdAEDT"
                + "wuGBYepGZjxrwDo5dAN7LDiodc2zctnF1bChRANCAASeIlVBl3AEwo2yA3tqf38y"
                + "m42n0BGdz+jfcdBo/oAUyxijpyoxmu/QK/SZmfVUV68yXvjLdKSWtWCWNgaoiNjd";
        String hostSpki = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEhVW1KPP6Adqh3lnB1lv1PAA1"
                + "b/Ki7TMPGv9y8T8MqOTuwVXfnA2vHzzmjjV2M00TR5ai2AWRt5DguKevplWl8w==";
        String capturedAt = "2026-08-13T01:02:03.4560000+00:00";
        String expiresAt = "2026-08-13T01:07:03.4560000+00:00";
        byte[] sealed = Base64.getDecoder().decode(
                "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh+goaKjpKWmp6ipqqvod6qc"
                        + "WKJ0++eMbNccUQD+RwF3ctV2AUM=");
        OperationsRemoteWindowSnapshot.Receipt receipt =
                new OperationsRemoteWindowSnapshot.Receipt(
                        "job-vector",
                        hostSpki,
                        "0ec61440fd6f4435107a94a05963f80e12dfdfa806e24fb72088fffca11cb999",
                        sealed.length,
                        capturedAt,
                        expiresAt);

        KeyFactory factory = KeyFactory.getInstance("EC");
        PrivateKey recipientPrivate = factory.generatePrivate(new PKCS8EncodedKeySpec(
                Base64.getDecoder().decode(recipientPkcs8)));
        PublicKey hostPublic = factory.generatePublic(new X509EncodedKeySpec(
                Base64.getDecoder().decode(hostSpki)));
        KeyAgreement agreement = KeyAgreement.getInstance("ECDH");
        agreement.init(recipientPrivate);
        agreement.doPhase(hostPublic, true);
        byte[] sharedSecret = agreement.generateSecret();

        byte[] plaintext = OperationsRemoteWindowSnapshot.decrypt(
                sealed,
                sharedSecret,
                receipt,
                "host-vector",
                "device-vector",
                "task-vector",
                "idempotency-vector",
                recipientSpki);
        assertArrayEquals(hex("ffd810203040ffd9"), plaintext);
        assertEquals(capturedAt, receipt.capturedAt);
        assertArrayEquals(
                String.join("\n",
                        "colorvision-window-snapshot-aad-v1",
                        "host-vector",
                        "device-vector",
                        "task-vector",
                        "idempotency-vector",
                        "job-vector",
                        capturedAt,
                        expiresAt,
                        "image/jpeg").getBytes(StandardCharsets.UTF_8),
                OperationsRemoteWindowSnapshot.buildAad(
                        "host-vector",
                        "device-vector",
                        "task-vector",
                        "idempotency-vector",
                        receipt));
        Arrays.fill(plaintext, (byte) 0);
    }

    @Test
    public void canonicalContextStringsMatchTheFrozenProtocol() throws Exception {
        KeyPair recipient = p256KeyPair();
        KeyPair host = p256KeyPair();
        String recipientSpki = OperationsRemoteWindowSnapshot.canonicalPublicKey(
                recipient.getPublic());
        String hostSpki = OperationsRemoteWindowSnapshot.canonicalPublicKey(host.getPublic());
        OperationsRemoteWindowSnapshot.Receipt receipt = receipt(
                hostSpki, "0".repeat(64), 61, CAPTURED_AT, EXPIRES_AT);

        assertEquals(String.join("\n",
                        "colorvision-window-snapshot-key-v1", HOST_ID, DEVICE_ID,
                        TASK_ID, IDEMPOTENCY_KEY, recipientSpki, hostSpki),
                new String(OperationsRemoteWindowSnapshot.buildInfo(
                        HOST_ID, DEVICE_ID, TASK_ID, IDEMPOTENCY_KEY,
                        recipientSpki, hostSpki), StandardCharsets.UTF_8));
        assertEquals(String.join("\n",
                        "colorvision-window-snapshot-aad-v1", HOST_ID, DEVICE_ID,
                        TASK_ID, IDEMPOTENCY_KEY, JOB_ID, CAPTURED_AT, EXPIRES_AT,
                        "image/jpeg"),
                new String(OperationsRemoteWindowSnapshot.buildAad(
                        HOST_ID, DEVICE_ID, TASK_ID, IDEMPOTENCY_KEY, receipt),
                        StandardCharsets.UTF_8));
    }

    private static JSONObject completedReceipt(
            String hostSpki,
            int sealedBytes,
            String sealedSha256,
            String capturedAt,
            String expiresAt) throws Exception {
        return new JSONObject()
                .put("kind", OperationsRemoteWindowSnapshot.RECEIPT_KIND)
                .put("scheme", OperationsRemoteWindowSnapshot.SCHEME)
                .put("jobId", JOB_ID)
                .put("hostEphemeralPublicKeySpki", hostSpki)
                .put("sealedSha256", sealedSha256)
                .put("sealedBytes", sealedBytes)
                .put("capturedAt", capturedAt)
                .put("expiresAt", expiresAt);
    }

    private static OperationsRemoteWindowSnapshot.Receipt receipt(
            String hostSpki,
            String sealedSha256,
            int sealedBytes,
            String capturedAt,
            String expiresAt) {
        return new OperationsRemoteWindowSnapshot.Receipt(
                JOB_ID, hostSpki, sealedSha256, sealedBytes, capturedAt, expiresAt);
    }

    private static KeyPair p256KeyPair() throws Exception {
        KeyPairGenerator generator = KeyPairGenerator.getInstance("EC");
        generator.initialize(new ECGenParameterSpec("secp256r1"));
        return generator.generateKeyPair();
    }

    private static byte[] sharedSecret(KeyPair own, KeyPair peer) throws Exception {
        KeyAgreement agreement = KeyAgreement.getInstance("ECDH");
        agreement.init(own.getPrivate());
        agreement.doPhase(peer.getPublic(), true);
        return agreement.generateSecret();
    }

    private static byte[] join(byte[]... values) {
        int size = 0;
        for (byte[] value : values) {
            size += value.length;
        }
        byte[] result = new byte[size];
        int offset = 0;
        for (byte[] value : values) {
            System.arraycopy(value, 0, result, offset, value.length);
            offset += value.length;
        }
        return result;
    }

    private static byte[] hex(String value) {
        byte[] result = new byte[value.length() / 2];
        for (int index = 0; index < result.length; index++) {
            result[index] = (byte) Integer.parseInt(
                    value.substring(index * 2, index * 2 + 2), 16);
        }
        return result;
    }
}

package com.colorvision.xcviewer;

import android.annotation.SuppressLint;

import java.security.MessageDigest;
import java.security.cert.Certificate;
import java.security.cert.CertificateException;
import java.security.cert.X509Certificate;
import java.util.Locale;

import javax.net.ssl.HostnameVerifier;
import javax.net.ssl.SSLSession;
import javax.net.ssl.X509TrustManager;

/**
 * Authenticates the self-signed desktop certificate captured during local pairing.
 *
 * <p>The desktop keeps one certificate while its DHCP address may change, so its certificate
 * cannot use the current IP address as a stable SAN. The exact leaf certificate fingerprint is
 * therefore the trust anchor, and the hostname callback is accepted only for the endpoint host
 * that created this policy and the same pinned, currently valid certificate.</p>
 */
@SuppressLint("CustomX509TrustManager")
final class OperationsPinnedTlsPolicy implements X509TrustManager, HostnameVerifier {
    private static final int SHA256_BYTES = 32;
    private static final X509Certificate[] NO_ACCEPTED_ISSUERS = new X509Certificate[0];

    private final String expectedHost;
    private final byte[] expectedFingerprint;

    OperationsPinnedTlsPolicy(String expectedHost, String certificateSha256) {
        if (expectedHost == null || expectedHost.trim().isEmpty()) {
            throw new IllegalArgumentException("invalid_operations_host");
        }
        this.expectedHost = expectedHost.trim().toLowerCase(Locale.ROOT);
        this.expectedFingerprint = decodeFingerprint(certificateSha256);
    }

    @Override
    public void checkClientTrusted(X509Certificate[] chain, String authType) throws CertificateException {
        throw new CertificateException("Client certificates are not accepted");
    }

    @Override
    public void checkServerTrusted(X509Certificate[] chain, String authType) throws CertificateException {
        if (chain == null || chain.length != 1 || chain[0] == null) {
            throw new CertificateException("Unexpected server certificate chain");
        }
        verifyPinnedLeaf(chain[0]);
    }

    @Override
    public X509Certificate[] getAcceptedIssuers() {
        return NO_ACCEPTED_ISSUERS.clone();
    }

    @Override
    public boolean verify(String hostname, SSLSession session) {
        if (hostname == null || session == null || !isExpectedHost(hostname)) {
            return false;
        }
        try {
            Certificate[] certificates = session.getPeerCertificates();
            return certificates.length > 0
                    && certificates[0] instanceof X509Certificate
                    && verify(hostname, (X509Certificate) certificates[0]);
        } catch (Exception ex) {
            return false;
        }
    }

    boolean verify(String hostname, X509Certificate certificate) {
        if (hostname == null || certificate == null || !isExpectedHost(hostname)) {
            return false;
        }
        try {
            verifyPinnedLeaf(certificate);
            return true;
        } catch (CertificateException ex) {
            return false;
        }
    }

    private boolean isExpectedHost(String hostname) {
        return expectedHost.equals(hostname.trim().toLowerCase(Locale.ROOT));
    }

    private void verifyPinnedLeaf(X509Certificate certificate) throws CertificateException {
        try {
            byte[] actualFingerprint = MessageDigest.getInstance("SHA-256").digest(certificate.getEncoded());
            if (!MessageDigest.isEqual(actualFingerprint, expectedFingerprint)) {
                throw new CertificateException("Certificate pin mismatch");
            }
            certificate.checkValidity();
        } catch (CertificateException ex) {
            throw ex;
        } catch (Exception ex) {
            throw new CertificateException("Unable to verify server certificate", ex);
        }
    }

    private static byte[] decodeFingerprint(String value) {
        String normalized = value == null ? "" : value.trim().toLowerCase(Locale.ROOT);
        if (!normalized.matches("[0-9a-f]{64}")) {
            throw new IllegalArgumentException("invalid_host_certificate_pin");
        }
        byte[] decoded = new byte[SHA256_BYTES];
        for (int index = 0; index < decoded.length; index++) {
            int offset = index * 2;
            decoded[index] = (byte) Integer.parseInt(normalized.substring(offset, offset + 2), 16);
        }
        return decoded;
    }
}

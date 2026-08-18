package com.colorvision.xcviewer;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertThrows;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

import java.io.InputStream;
import java.security.cert.CertificateException;
import java.security.cert.CertificateFactory;
import java.security.cert.X509Certificate;

public class OperationsPinnedTlsPolicyTest {
    private static final String VALID_PIN =
            "686695afddae0519c600269b791fe9b9d2fc4e43c69651d26f5bdde450b66d9e";
    private static final String EXPIRED_PIN =
            "2d1407ccd477948d4c1c6428c072549326e041bd4e7ee0d6d6a9f5be301536f2";

    @Test
    public void matchingCurrentLeafIsAcceptedForThePairedHost() throws Exception {
        X509Certificate certificate = certificate("operations_tls_valid.pem");
        OperationsPinnedTlsPolicy policy = new OperationsPinnedTlsPolicy(
                "192.168.1.25", VALID_PIN.toUpperCase());

        policy.checkServerTrusted(new X509Certificate[]{certificate}, "RSA");

        assertTrue(policy.verify("192.168.1.25", certificate));
        assertFalse(policy.verify("192.168.1.26", certificate));
    }

    @Test
    public void mismatchedLeafIsRejectedByTrustAndHostnameChecks() throws Exception {
        X509Certificate certificate = certificate("operations_tls_valid.pem");
        OperationsPinnedTlsPolicy policy = new OperationsPinnedTlsPolicy(
                "192.168.1.25", EXPIRED_PIN);

        assertThrows(CertificateException.class,
                () -> policy.checkServerTrusted(new X509Certificate[]{certificate}, "RSA"));
        assertFalse(policy.verify("192.168.1.25", certificate));
    }

    @Test
    public void expiredPinnedLeafIsRejectedEvenWhenFingerprintMatches() throws Exception {
        X509Certificate certificate = certificate("operations_tls_expired.pem");
        OperationsPinnedTlsPolicy policy = new OperationsPinnedTlsPolicy(
                "192.168.1.25", EXPIRED_PIN);

        assertThrows(CertificateException.class,
                () -> policy.checkServerTrusted(new X509Certificate[]{certificate}, "RSA"));
        assertFalse(policy.verify("192.168.1.25", certificate));
    }

    @Test
    public void emptyAndMultiCertificateChainsAreRejected() throws Exception {
        X509Certificate certificate = certificate("operations_tls_valid.pem");
        OperationsPinnedTlsPolicy policy = new OperationsPinnedTlsPolicy(
                "192.168.1.25", VALID_PIN);

        assertThrows(CertificateException.class,
                () -> policy.checkServerTrusted(null, "RSA"));
        assertThrows(CertificateException.class,
                () -> policy.checkServerTrusted(new X509Certificate[0], "RSA"));
        assertThrows(CertificateException.class,
                () -> policy.checkServerTrusted(new X509Certificate[]{certificate, certificate}, "RSA"));
    }

    @Test
    public void clientCertificatesAndMalformedPinsAreRejected() throws Exception {
        X509Certificate certificate = certificate("operations_tls_valid.pem");
        OperationsPinnedTlsPolicy policy = new OperationsPinnedTlsPolicy(
                "192.168.1.25", VALID_PIN);

        assertThrows(CertificateException.class,
                () -> policy.checkClientTrusted(new X509Certificate[]{certificate}, "RSA"));
        assertThrows(IllegalArgumentException.class,
                () -> new OperationsPinnedTlsPolicy("192.168.1.25", ""));
        assertThrows(IllegalArgumentException.class,
                () -> new OperationsPinnedTlsPolicy("192.168.1.25", VALID_PIN.substring(1)));
        assertThrows(IllegalArgumentException.class,
                () -> new OperationsPinnedTlsPolicy("192.168.1.25", VALID_PIN.replace('a', 'z')));
        assertThrows(IllegalArgumentException.class,
                () -> new OperationsPinnedTlsPolicy("", VALID_PIN));
    }

    private static X509Certificate certificate(String resource) throws Exception {
        try (InputStream input = OperationsPinnedTlsPolicyTest.class.getClassLoader()
                .getResourceAsStream(resource)) {
            if (input == null) {
                throw new IllegalStateException("Missing TLS test certificate: " + resource);
            }
            return (X509Certificate) CertificateFactory.getInstance("X.509").generateCertificate(input);
        }
    }
}

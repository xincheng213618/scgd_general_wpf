package com.colorvision.xcviewer;

import org.junit.Test;

import java.net.ConnectException;
import java.net.SocketTimeoutException;

import javax.net.ssl.SSLHandshakeException;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

public class PairingFailurePresentationTest {
    @Test
    public void expiredServerAndQrFailuresShareRefreshRecovery() {
        assertEquals(
                PairingFailurePresentation.EXPIRED_QR,
                PairingFailurePresentation.reasonFor(
                        new IllegalStateException("pairing_challenge_invalid_or_expired")));
        assertEquals(
                PairingFailurePresentation.EXPIRED_QR,
                PairingFailurePresentation.reasonFor(
                        new IllegalArgumentException("pairing_qr_expired")));
        assertEquals("配对码已过期",
                PairingFailurePresentation.title(PairingFailurePresentation.EXPIRED_QR));
        assertTrue(PairingFailurePresentation.message(PairingFailurePresentation.EXPIRED_QR)
                .contains("刷新配对码"));
    }

    @Test
    public void malformedAndUnsupportedQrHaveDistinctGuidance() {
        assertEquals(
                PairingFailurePresentation.INVALID_QR,
                PairingFailurePresentation.reasonFor(
                        new IllegalArgumentException("pairing_qr_invalid")));
        assertEquals(
                PairingFailurePresentation.UNSUPPORTED_QR,
                PairingFailurePresentation.reasonFor(
                        new IllegalArgumentException("pairing_qr_unsupported")));
        assertTrue(PairingFailurePresentation.message(PairingFailurePresentation.UNSUPPORTED_QR)
                .contains("更新"));
    }

    @Test
    public void certificateAndProofFailuresAreSecurityRejections() {
        assertEquals(
                PairingFailurePresentation.SECURITY_REJECTED,
                PairingFailurePresentation.reasonFor(new SSLHandshakeException("handshake")));
        assertEquals(
                PairingFailurePresentation.SECURITY_REJECTED,
                PairingFailurePresentation.reasonFor(
                        new IllegalStateException("invalid_pairing_signature")));
    }

    @Test
    public void networkFailuresAreRecoverableWithoutDeletingProfiles() {
        assertEquals(
                PairingFailurePresentation.COMPUTER_UNREACHABLE,
                PairingFailurePresentation.reasonFor(new SocketTimeoutException("timeout")));
        assertEquals(
                PairingFailurePresentation.COMPUTER_UNREACHABLE,
                PairingFailurePresentation.reasonFor(new ConnectException("offline")));
        assertTrue(PairingFailurePresentation.preservationNote(true).contains("均已保留"));
        assertEquals("返回当前电脑", PairingFailurePresentation.secondaryAction(true));
        assertEquals("返回设置", PairingFailurePresentation.secondaryAction(false));
    }
}

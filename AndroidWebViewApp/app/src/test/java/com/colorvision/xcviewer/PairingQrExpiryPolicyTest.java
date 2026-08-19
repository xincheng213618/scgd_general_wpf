package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertThrows;

public class PairingQrExpiryPolicyTest {
    private static final long NOW = PairingQrExpiryPolicy.parse("2026-08-18T06:00:00Z");

    @Test
    public void currentChallengeIsAccepted() {
        PairingQrExpiryPolicy.validate("2026-08-18T06:02:00.1234567+00:00", NOW);
    }

    @Test
    public void smallClockSkewIsAccepted() {
        PairingQrExpiryPolicy.validate("2026-08-18T05:59:40Z", NOW);
    }

    @Test
    public void expiredChallengeIsRejected() {
        IllegalArgumentException exception = assertThrows(
                IllegalArgumentException.class,
                () -> PairingQrExpiryPolicy.validate("2026-08-18T05:59:29Z", NOW));
        assertEquals(PairingQrExpiryPolicy.ERROR_EXPIRED, exception.getMessage());
    }

    @Test
    public void offsetsAndFractionsAreParsedPrecisely() {
        assertEquals(
                PairingQrExpiryPolicy.parse("2026-08-18T06:00:00.250Z"),
                PairingQrExpiryPolicy.parse("2026-08-18T14:00:00.250+08:00"));
    }

    @Test
    public void malformedTimestampIsRejected() {
        IllegalArgumentException exception = assertThrows(
                IllegalArgumentException.class,
                () -> PairingQrExpiryPolicy.parse("2026-08-18 06:00:00"));
        assertEquals(PairingQrExpiryPolicy.ERROR_INVALID, exception.getMessage());
    }
}

package com.colorvision.xcviewer;

final class PairingQrExpiryPolicy {
    static final String ERROR_INVALID = "pairing_qr_invalid";
    static final String ERROR_EXPIRED = "pairing_qr_expired";
    private static final long CLOCK_TOLERANCE_MILLISECONDS = 30_000L;
    private PairingQrExpiryPolicy() {
    }

    static void validate(String expiresAt, long nowMilliseconds) {
        long expiresAtMilliseconds = parse(expiresAt);
        if (expiresAtMilliseconds < nowMilliseconds - CLOCK_TOLERANCE_MILLISECONDS) {
            throw new IllegalArgumentException(ERROR_EXPIRED);
        }
    }

    static long parse(String value) {
        try {
            return Rfc3339Timestamp.parseMilliseconds(value);
        } catch (Exception exception) {
            throw new IllegalArgumentException(ERROR_INVALID, exception);
        }
    }
}

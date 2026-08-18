package com.colorvision.xcviewer;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

final class PairingQrExpiryPolicy {
    static final String ERROR_INVALID = "pairing_qr_invalid";
    static final String ERROR_EXPIRED = "pairing_qr_expired";
    private static final long CLOCK_TOLERANCE_MILLISECONDS = 30_000L;
    private static final Pattern RFC3339 = Pattern.compile(
            "^(\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2})(?:\\.(\\d{1,9}))?(Z|[+-]\\d{2}:\\d{2})$");

    private PairingQrExpiryPolicy() {
    }

    static void validate(String expiresAt, long nowMilliseconds) {
        long expiresAtMilliseconds = parse(expiresAt);
        if (expiresAtMilliseconds < nowMilliseconds - CLOCK_TOLERANCE_MILLISECONDS) {
            throw new IllegalArgumentException(ERROR_EXPIRED);
        }
    }

    static long parse(String value) {
        Matcher matcher = RFC3339.matcher(value == null ? "" : value.trim());
        if (!matcher.matches()) {
            throw new IllegalArgumentException(ERROR_INVALID);
        }
        try {
            SimpleDateFormat parser = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.ROOT);
            parser.setLenient(false);
            parser.setTimeZone(TimeZone.getTimeZone("UTC"));
            Date parsed = parser.parse(matcher.group(1));
            if (parsed == null) {
                throw new IllegalArgumentException(ERROR_INVALID);
            }
            String fraction = matcher.group(2);
            int fractionMilliseconds = fraction == null
                    ? 0 : Integer.parseInt((fraction + "000").substring(0, 3));
            int offsetMinutes = offsetMinutes(matcher.group(3));
            return parsed.getTime() + fractionMilliseconds - offsetMinutes * 60_000L;
        } catch (IllegalArgumentException ex) {
            throw ex;
        } catch (Exception ex) {
            throw new IllegalArgumentException(ERROR_INVALID, ex);
        }
    }

    private static int offsetMinutes(String offset) {
        if ("Z".equals(offset)) {
            return 0;
        }
        int hours = Integer.parseInt(offset.substring(1, 3));
        int minutes = Integer.parseInt(offset.substring(4, 6));
        if (hours > 18 || minutes > 59 || (hours == 18 && minutes != 0)) {
            throw new IllegalArgumentException(ERROR_INVALID);
        }
        int total = hours * 60 + minutes;
        return offset.charAt(0) == '-' ? -total : total;
    }
}

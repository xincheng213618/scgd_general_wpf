package com.colorvision.xcviewer;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

final class Rfc3339Timestamp {
    private static final Pattern VALUE = Pattern.compile(
            "^(\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2})(?:\\.(\\d{1,9}))?"
                    + "(Z|[+-]\\d{2}:\\d{2})$");

    private Rfc3339Timestamp() {
    }

    static long parseMilliseconds(String value) {
        Matcher matcher = VALUE.matcher(value == null ? "" : value.trim());
        if (!matcher.matches()) {
            throw new IllegalArgumentException("invalid_rfc3339_timestamp");
        }
        try {
            SimpleDateFormat parser = new SimpleDateFormat(
                    "yyyy-MM-dd'T'HH:mm:ss", Locale.ROOT);
            parser.setLenient(false);
            parser.setTimeZone(TimeZone.getTimeZone("UTC"));
            Date parsed = parser.parse(matcher.group(1));
            if (parsed == null) {
                throw new IllegalArgumentException("invalid_rfc3339_timestamp");
            }
            String fraction = matcher.group(2);
            int fractionMilliseconds = fraction == null
                    ? 0 : Integer.parseInt((fraction + "000").substring(0, 3));
            return parsed.getTime()
                    + fractionMilliseconds
                    - offsetMinutes(matcher.group(3)) * 60_000L;
        } catch (IllegalArgumentException exception) {
            throw exception;
        } catch (Exception exception) {
            throw new IllegalArgumentException("invalid_rfc3339_timestamp", exception);
        }
    }

    private static int offsetMinutes(String offset) {
        if ("Z".equals(offset)) {
            return 0;
        }
        int hours = Integer.parseInt(offset.substring(1, 3));
        int minutes = Integer.parseInt(offset.substring(4, 6));
        if (hours > 18 || minutes > 59 || (hours == 18 && minutes != 0)) {
            throw new IllegalArgumentException("invalid_rfc3339_timestamp");
        }
        int total = hours * 60 + minutes;
        return offset.charAt(0) == '-' ? -total : total;
    }
}

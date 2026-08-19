package com.colorvision.xcviewer;

final class OperationsWatchFreshnessPolicy {
    static final long STALE_AFTER_MILLISECONDS = 10L * 60L * 1000L;
    static final long MAXIMUM_FUTURE_SKEW_MILLISECONDS = 60_000L;

    enum Freshness {
        MISSING,
        FRESH,
        STALE,
        FUTURE
    }

    private OperationsWatchFreshnessPolicy() {
    }

    static Freshness classify(long checkedAtMilliseconds, long nowMilliseconds) {
        if (checkedAtMilliseconds <= 0L) {
            return Freshness.MISSING;
        }
        if (checkedAtMilliseconds > nowMilliseconds
                && checkedAtMilliseconds - nowMilliseconds
                > MAXIMUM_FUTURE_SKEW_MILLISECONDS) {
            return Freshness.FUTURE;
        }
        if (nowMilliseconds > checkedAtMilliseconds
                && nowMilliseconds - checkedAtMilliseconds
                > STALE_AFTER_MILLISECONDS) {
            return Freshness.STALE;
        }
        return Freshness.FRESH;
    }
}

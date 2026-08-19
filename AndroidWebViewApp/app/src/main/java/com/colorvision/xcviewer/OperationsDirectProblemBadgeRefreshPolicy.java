package com.colorvision.xcviewer;

final class OperationsDirectProblemBadgeRefreshPolicy {
    static final long REFRESH_INTERVAL_MILLISECONDS = 5L * 60L * 1000L;
    static final long RETRY_INTERVAL_MILLISECONDS = 10_000L;

    private OperationsDirectProblemBadgeRefreshPolicy() {
    }

    static boolean shouldRefresh(
            boolean remoteDashboard,
            boolean refreshInFlight,
            boolean problemCenterRefreshInFlight,
            boolean authoritativeCountAvailable,
            String authoritativeMonitorRevision,
            String currentMonitorRevision,
            long lastAttemptMilliseconds,
            long nowMilliseconds) {
        if (remoteDashboard
                || refreshInFlight
                || problemCenterRefreshInFlight
                || nowMilliseconds <= 0L) {
            return false;
        }
        long elapsed = lastAttemptMilliseconds <= 0L
                || nowMilliseconds < lastAttemptMilliseconds
                ? Long.MAX_VALUE
                : nowMilliseconds - lastAttemptMilliseconds;
        if (elapsed < RETRY_INTERVAL_MILLISECONDS) {
            return false;
        }
        boolean evidenceChanged = authoritativeCountAvailable
                && authoritativeMonitorRevision != null
                && !authoritativeMonitorRevision.isEmpty()
                && currentMonitorRevision != null
                && !currentMonitorRevision.isEmpty()
                && !authoritativeMonitorRevision.equals(currentMonitorRevision);
        return !authoritativeCountAvailable
                || evidenceChanged
                || elapsed >= REFRESH_INTERVAL_MILLISECONDS;
    }
}

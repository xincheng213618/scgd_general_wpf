package com.colorvision.xcviewer;

import java.util.Locale;

final class PairingApprovalWaitPolicy {
    static final long AUTOMATIC_CHECK_WINDOW_MILLISECONDS = 120_000L;
    static final long POLL_INTERVAL_MILLISECONDS = 2_000L;
    static final int PROGRESS_MAXIMUM = 1_000;

    private PairingApprovalWaitPolicy() {
    }

    static long deadlineFrom(long startedAtMilliseconds) {
        return startedAtMilliseconds + AUTOMATIC_CHECK_WINDOW_MILLISECONDS;
    }

    static long remainingMilliseconds(long deadlineMilliseconds, long nowMilliseconds) {
        return Math.max(0L, deadlineMilliseconds - nowMilliseconds);
    }

    static int remainingSeconds(long deadlineMilliseconds, long nowMilliseconds) {
        long remaining = remainingMilliseconds(deadlineMilliseconds, nowMilliseconds);
        return (int) ((remaining + 999L) / 1_000L);
    }

    static int elapsedProgress(long deadlineMilliseconds, long nowMilliseconds) {
        long remaining = remainingMilliseconds(deadlineMilliseconds, nowMilliseconds);
        long elapsed = AUTOMATIC_CHECK_WINDOW_MILLISECONDS - remaining;
        long bounded = Math.max(0L, Math.min(AUTOMATIC_CHECK_WINDOW_MILLISECONDS, elapsed));
        return (int) (bounded * PROGRESS_MAXIMUM / AUTOMATIC_CHECK_WINDOW_MILLISECONDS);
    }

    static boolean shouldContinue(long deadlineMilliseconds, long nowMilliseconds) {
        return remainingMilliseconds(deadlineMilliseconds, nowMilliseconds) > 0L;
    }

    static String formatCountdown(int remainingSeconds) {
        int bounded = Math.max(0, remainingSeconds);
        return String.format(Locale.ROOT, "%02d:%02d", bounded / 60, bounded % 60);
    }
}

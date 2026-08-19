package com.colorvision.xcviewer;

import java.util.List;

final class OperationsBackgroundFleetPolicy {
    static final long SECONDARY_CHECK_INTERVAL_MILLISECONDS = 10 * 60_000L;
    private static final String ATTENTION_NOTIFICATION_TAG_PREFIX =
            "operations_attention:";

    private OperationsBackgroundFleetPolicy() {
    }

    static OperationsProfileRegistry.Profile selectSecondaryProfile(
            List<OperationsProfileRegistry.Profile> profiles,
            String activeHostId,
            long nowMilliseconds) {
        OperationsProfileRegistry.Profile selected = null;
        for (OperationsProfileRegistry.Profile profile : profiles) {
            if (profile.revoked || profile.hostId.equals(activeHostId)
                    || !needsCheck(profile.watchCheckedAt, nowMilliseconds)) {
                continue;
            }
            if (selected == null || profile.watchCheckedAt < selected.watchCheckedAt) {
                selected = profile;
            }
        }
        return selected;
    }

    static boolean needsCheck(long checkedAtMilliseconds, long nowMilliseconds) {
        return checkedAtMilliseconds <= 0L
                || checkedAtMilliseconds > nowMilliseconds + 60_000L
                || nowMilliseconds - checkedAtMilliseconds
                        >= SECONDARY_CHECK_INTERVAL_MILLISECONDS;
    }

    static String latestState(String watchHistory, long nowMilliseconds) {
        List<OperationsWatchHistory.Entry> entries = OperationsWatchHistory.parse(
                watchHistory, nowMilliseconds);
        return entries.isEmpty() ? "" : entries.get(entries.size() - 1).state;
    }

    static String attentionNotificationTag(String hostId) {
        return ATTENTION_NOTIFICATION_TAG_PREFIX + (hostId == null ? "" : hostId);
    }
}

package com.colorvision.xcviewer;

import org.junit.Test;

import java.util.Arrays;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

public class OperationsBackgroundFleetPolicyTest {
    private static final long NOW = 2_000_000L;

    @Test
    public void selectsTheOldestDueNonActiveUsableComputer() {
        OperationsProfileRegistry.Profile active = profile("host_active", 0L, false);
        OperationsProfileRegistry.Profile recent = profile(
                "host_recent",
                NOW - OperationsBackgroundFleetPolicy.SECONDARY_CHECK_INTERVAL_MILLISECONDS + 1L,
                false);
        OperationsProfileRegistry.Profile stale = profile("host_stale", NOW - 900_000L, false);
        OperationsProfileRegistry.Profile never = profile("host_never", 0L, false);
        OperationsProfileRegistry.Profile revoked = profile("host_revoked", 0L, true);

        assertEquals("host_never", OperationsBackgroundFleetPolicy.selectSecondaryProfile(
                Arrays.asList(active, recent, stale, never, revoked),
                active.hostId,
                NOW).hostId);
        assertNull(OperationsBackgroundFleetPolicy.selectSecondaryProfile(
                Arrays.asList(active, recent), active.hostId, NOW));
    }

    @Test
    public void invalidFutureTimesAndExactBoundaryAreDue() {
        assertTrue(OperationsBackgroundFleetPolicy.needsCheck(0L, NOW));
        assertTrue(OperationsBackgroundFleetPolicy.needsCheck(
                NOW - OperationsBackgroundFleetPolicy.SECONDARY_CHECK_INTERVAL_MILLISECONDS,
                NOW));
        assertTrue(OperationsBackgroundFleetPolicy.needsCheck(NOW + 60_001L, NOW));
        assertFalse(OperationsBackgroundFleetPolicy.needsCheck(NOW - 599_999L, NOW));
    }

    @Test
    public void notificationIdentityIsStablePerComputer() {
        assertEquals(
                OperationsBackgroundFleetPolicy.attentionNotificationTag("host_1"),
                OperationsBackgroundFleetPolicy.attentionNotificationTag("host_1"));
        assertFalse(OperationsBackgroundFleetPolicy.attentionNotificationTag("host_1")
                .equals(OperationsBackgroundFleetPolicy.attentionNotificationTag("host_2")));
    }

    @Test
    public void latestStateUsesTheBoundedWatchHistoryParser() {
        String history = OperationsWatchHistory.transition(
                "", OperationsWatchHistory.STATE_ONLINE, NOW - 2_000L).serializedHistory;
        history = OperationsWatchHistory.transition(
                history, OperationsWatchHistory.attentionState(
                        OperationsWatchPolicy.ATTENTION_DEVICES), NOW - 1_000L).serializedHistory;

        assertEquals(OperationsWatchHistory.attentionState(
                        OperationsWatchPolicy.ATTENTION_DEVICES),
                OperationsBackgroundFleetPolicy.latestState(history, NOW));
        assertEquals("", OperationsBackgroundFleetPolicy.latestState("invalid", NOW));
    }

    private static OperationsProfileRegistry.Profile profile(
            String hostId, long watchCheckedAt, boolean revoked) {
        return new OperationsProfileRegistry.Profile(
                "https://127.0.0.1:9088",
                "a".repeat(64),
                hostId,
                OperationsConnectionPreference.DIRECT,
                revoked,
                true,
                "",
                "",
                watchCheckedAt,
                "",
                "",
                "");
    }
}

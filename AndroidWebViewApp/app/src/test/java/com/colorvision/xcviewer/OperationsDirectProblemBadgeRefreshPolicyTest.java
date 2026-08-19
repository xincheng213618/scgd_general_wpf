package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsDirectProblemBadgeRefreshPolicyTest {
    @Test
    public void missingExactCountRequestsAnImmediateRefresh() {
        assertTrue(shouldRefresh(
                false, false, false, false, "", "current", 0L, 100_000L));
    }

    @Test
    public void unchangedExactCountSurvivesLivePollingUntilPeriodicRefresh() {
        assertFalse(shouldRefresh(
                false, false, false, true, "same", "same", 100_000L, 399_999L));
        assertTrue(shouldRefresh(
                false, false, false, true, "same", "same", 100_000L, 400_000L));
    }

    @Test
    public void materialMonitorEvidenceRequestsAnExactRefreshAfterRetryGuard() {
        assertFalse(shouldRefresh(
                false, false, false, true, "before", "after", 100_000L, 109_999L));
        assertTrue(shouldRefresh(
                false, false, false, true, "before", "after", 100_000L, 110_000L));
    }

    @Test
    public void remoteAndExistingRefreshesNeverStartADuplicateDirectRequest() {
        assertFalse(shouldRefresh(
                true, false, false, false, "", "current", 0L, 100_000L));
        assertFalse(shouldRefresh(
                false, true, false, false, "", "current", 0L, 100_000L));
        assertFalse(shouldRefresh(
                false, false, true, false, "", "current", 0L, 100_000L));
        assertTrue(shouldRefresh(
                false, false, false, false, "", "current", 100_000L, 90_000L));
    }

    private static boolean shouldRefresh(
            boolean remote,
            boolean refreshInFlight,
            boolean triageRefreshInFlight,
            boolean authoritative,
            String baseline,
            String current,
            long lastAttempt,
            long now) {
        return OperationsDirectProblemBadgeRefreshPolicy.shouldRefresh(
                remote,
                refreshInFlight,
                triageRefreshInFlight,
                authoritative,
                baseline,
                current,
                lastAttempt,
                now);
    }
}

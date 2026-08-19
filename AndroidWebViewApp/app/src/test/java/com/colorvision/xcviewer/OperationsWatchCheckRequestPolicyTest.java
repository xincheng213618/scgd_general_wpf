package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class OperationsWatchCheckRequestPolicyTest {
    @Test
    public void inactiveWatchIgnoresImmediateRequests() {
        assertEquals(
                OperationsWatchCheckRequestPolicy.Decision.IGNORE,
                OperationsWatchCheckRequestPolicy.decide(false, false));
    }

    @Test
    public void idleWatchRunsImmediateRequestNow() {
        assertEquals(
                OperationsWatchCheckRequestPolicy.Decision.RUN_NOW,
                OperationsWatchCheckRequestPolicy.decide(true, false));
    }

    @Test
    public void inFlightWatchCoalescesRequestsIntoOneFollowUp() {
        assertEquals(
                OperationsWatchCheckRequestPolicy.Decision.RUN_AFTER_CURRENT,
                OperationsWatchCheckRequestPolicy.decide(true, true));
    }
}

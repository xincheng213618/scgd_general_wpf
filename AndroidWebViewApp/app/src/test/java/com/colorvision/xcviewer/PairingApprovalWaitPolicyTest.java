package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class PairingApprovalWaitPolicyTest {
    private static final long STARTED_AT = 40_000L;
    private static final long DEADLINE = PairingApprovalWaitPolicy.deadlineFrom(STARTED_AT);

    @Test
    public void automaticWindowStartsAtTwoMinutes() {
        assertEquals(120, PairingApprovalWaitPolicy.remainingSeconds(DEADLINE, STARTED_AT));
        assertEquals("02:00", PairingApprovalWaitPolicy.formatCountdown(120));
        assertEquals(0, PairingApprovalWaitPolicy.elapsedProgress(DEADLINE, STARTED_AT));
    }

    @Test
    public void partialSecondsRoundUpForVisibleCountdown() {
        assertEquals(2, PairingApprovalWaitPolicy.remainingSeconds(DEADLINE, DEADLINE - 1_001L));
        assertEquals(1, PairingApprovalWaitPolicy.remainingSeconds(DEADLINE, DEADLINE - 1L));
        assertEquals("00:01", PairingApprovalWaitPolicy.formatCountdown(1));
    }

    @Test
    public void elapsedProgressIsBounded() {
        assertEquals(500, PairingApprovalWaitPolicy.elapsedProgress(DEADLINE, STARTED_AT + 60_000L));
        assertEquals(PairingApprovalWaitPolicy.PROGRESS_MAXIMUM,
                PairingApprovalWaitPolicy.elapsedProgress(DEADLINE, DEADLINE + 5_000L));
    }

    @Test
    public void pollingStopsAtDeadline() {
        assertTrue(PairingApprovalWaitPolicy.shouldContinue(DEADLINE, DEADLINE - 1L));
        assertFalse(PairingApprovalWaitPolicy.shouldContinue(DEADLINE, DEADLINE));
        assertEquals(0, PairingApprovalWaitPolicy.remainingMilliseconds(DEADLINE, DEADLINE + 1L));
    }
}

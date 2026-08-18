package com.colorvision.xcviewer;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsConnectionRecoveryPolicyTest {
    @Test
    public void visibleRecoveryPageKeepsTheForegroundRetryLoopActive() {
        assertTrue(OperationsConnectionRecoveryPolicy.shouldSchedule(
                true, true, false, true, true));
        assertTrue(OperationsConnectionRecoveryPolicy.shouldStart(
                true, true, false, true, true, false));
    }

    @Test
    public void regularDashboardKeepsItsExistingHeartbeat() {
        assertTrue(OperationsConnectionRecoveryPolicy.shouldSchedule(
                true, true, true, false, true));
    }

    @Test
    public void backgroundDetailsMissingClientsAndInflightRequestsDoNotRetry() {
        assertFalse(OperationsConnectionRecoveryPolicy.shouldSchedule(
                false, true, false, true, true));
        assertFalse(OperationsConnectionRecoveryPolicy.shouldSchedule(
                true, true, false, false, true));
        assertFalse(OperationsConnectionRecoveryPolicy.shouldSchedule(
                true, true, false, true, false));
        assertFalse(OperationsConnectionRecoveryPolicy.shouldStart(
                true, true, false, true, true, true));
    }
}

package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsFleetCheckCompletionPolicyTest {
    @Test
    public void problemCenterRefreshReturnsInPlaceWhileTheTargetStaysUsable() {
        OperationsFleetCheckCompletionPolicy.Decision decision =
                OperationsFleetCheckCompletionPolicy.decide(
                        OperationsDestinationState.TRIAGE, "host_1", "host_1");

        assertEquals(OperationsDestinationState.TRIAGE, decision.destination);
        assertTrue(decision.returnsToProblemCenter());
        assertFalse(decision.activeTargetChanged);
    }

    @Test
    public void revokedActiveTargetRequiresReconnectBeforeReturningToProblems() {
        OperationsFleetCheckCompletionPolicy.Decision decision =
                OperationsFleetCheckCompletionPolicy.decide(
                        OperationsDestinationState.TRIAGE, "host_1", "host_2");

        assertTrue(decision.returnsToProblemCenter());
        assertTrue(decision.activeTargetChanged);
    }

    @Test
    public void connectionManagementRemainsTheSafeDefaultDestination() {
        OperationsFleetCheckCompletionPolicy.Decision unknown =
                OperationsFleetCheckCompletionPolicy.decide(
                        "arbitrary", "host_1", "host_1");
        OperationsFleetCheckCompletionPolicy.Decision emptyTargets =
                OperationsFleetCheckCompletionPolicy.decide(null, null, "");

        assertEquals(OperationsDestinationState.CONNECTIONS, unknown.destination);
        assertFalse(unknown.returnsToProblemCenter());
        assertFalse(unknown.activeTargetChanged);
        assertFalse(emptyTargets.activeTargetChanged);
    }
}

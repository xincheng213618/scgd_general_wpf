package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsDestinationStateTest {
    @Test
    public void knownDestinationsSurviveSystemStateRoundTrip() {
        assertEquals(OperationsDestinationState.CONNECTIONS,
                OperationsDestinationState.normalize("connections"));
        assertEquals(OperationsDestinationState.TOOLS,
                OperationsDestinationState.normalize("tools"));
        assertEquals(OperationsDestinationState.CONNECTION_CHECK,
                OperationsDestinationState.normalize("connection_check"));
        assertEquals(OperationsDestinationState.FLEET_ISSUES,
                OperationsDestinationState.normalize("fleet_issues"));
        assertEquals(OperationsDestinationState.LIVE_MONITOR,
                OperationsDestinationState.normalize("live_monitor"));
        assertEquals(OperationsDestinationState.CAPABILITY_DETAIL,
                OperationsDestinationState.normalize("capability_detail"));
    }

    @Test
    public void unknownOrEmptyStateFallsBackToOverview() {
        assertEquals(OperationsDestinationState.OVERVIEW,
                OperationsDestinationState.normalize(null));
        assertEquals(OperationsDestinationState.OVERVIEW,
                OperationsDestinationState.normalize(""));
        assertEquals(OperationsDestinationState.OVERVIEW,
                OperationsDestinationState.normalize("arbitrary_remote_path"));
    }

    @Test
    public void transientOverviewPairingAndCapabilityDetailsAreNeverAutomaticallyReplayed() {
        assertFalse(OperationsDestinationState.shouldRestore(
                OperationsDestinationState.OVERVIEW));
        assertFalse(OperationsDestinationState.shouldRestore(
                OperationsDestinationState.PAIRING));
        assertFalse(OperationsDestinationState.shouldRestore(
                OperationsDestinationState.CAPABILITY_DETAIL));
        assertTrue(OperationsDestinationState.shouldRestore(
                OperationsDestinationState.CONNECTION_CHECK));
        assertTrue(OperationsDestinationState.shouldRestore(
                OperationsDestinationState.TOOLS));
        assertTrue(OperationsDestinationState.shouldRestore(
                OperationsDestinationState.HISTORY));
    }

    @Test
    public void remoteSafeProblemsRestoreWhileOperationalToolsWaitForDirectConnection() {
        assertFalse(OperationsDestinationState.requiresDirectConnection(
                OperationsDestinationState.TRIAGE));
        assertTrue(OperationsDestinationState.requiresDirectConnection(
                OperationsDestinationState.TOOLS));
        assertTrue(OperationsDestinationState.requiresDirectConnection(
                OperationsDestinationState.JOBS));
        assertTrue(OperationsDestinationState.requiresDirectConnection(
                OperationsDestinationState.SUPPORT));
        assertTrue(OperationsDestinationState.requiresDirectConnection(
                OperationsDestinationState.LIVE_MONITOR));
        assertTrue(OperationsDestinationState.requiresDirectConnection(
                OperationsDestinationState.CAPABILITY_DETAIL));
        assertFalse(OperationsDestinationState.requiresDirectConnection(
                OperationsDestinationState.CONNECTION_CHECK));
        assertFalse(OperationsDestinationState.requiresDirectConnection(
                OperationsDestinationState.HISTORY));
    }

    @Test
    public void triageContextCanRemoveNavigationBackToItself() {
        assertTrue(OperationsDestinationState.isTriage(OperationsDestinationState.TRIAGE));
        assertFalse(OperationsDestinationState.isTriage(OperationsDestinationState.OVERVIEW));
        assertFalse(OperationsDestinationState.isTriage(null));
    }

    @Test
    public void pairingPayloadIsNeverSilentlyResubmittedAfterSystemRecreation() {
        assertTrue(OperationsDestinationState.shouldSubmitPairingAutomatically(false, true));
        assertFalse(OperationsDestinationState.shouldSubmitPairingAutomatically(true, true));
        assertFalse(OperationsDestinationState.shouldSubmitPairingAutomatically(false, false));
    }
}

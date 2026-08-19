package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsMessageChannelRecoveryPolicyTest {
    private static final String MESSAGE_PATH = "/ops/v1/messaging/health";

    @Test
    public void sameOriginCanReceiveItsRecoveryResult() {
        assertTrue(isVisible(
                OperationsDestinationState.TRIAGE, "",
                OperationsDestinationState.TRIAGE, ""));
        assertTrue(isVisible(
                OperationsDestinationState.TOOLS, "",
                OperationsDestinationState.TOOLS, ""));
        assertTrue(isVisible(
                OperationsDestinationState.CAPABILITY_DETAIL, MESSAGE_PATH,
                OperationsDestinationState.CAPABILITY_DETAIL, MESSAGE_PATH));
    }

    @Test
    public void navigationOrDifferentDetailCannotBeOverwrittenByLateResult() {
        assertFalse(isVisible(
                OperationsDestinationState.TRIAGE, "",
                OperationsDestinationState.TOOLS, ""));
        assertFalse(isVisible(
                OperationsDestinationState.CAPABILITY_DETAIL, MESSAGE_PATH,
                OperationsDestinationState.CAPABILITY_DETAIL,
                "/ops/v1/diagnostics/recent-events"));
        assertFalse(isVisible(
                OperationsDestinationState.CAPABILITY_DETAIL,
                "/ops/v1/diagnostics/recent-events",
                OperationsDestinationState.CAPABILITY_DETAIL,
                "/ops/v1/diagnostics/recent-events"));
    }

    private static boolean isVisible(
            String originDestination,
            String originDetailPath,
            String currentDestination,
            String currentDetailPath) {
        return OperationsMessageChannelRecoveryPolicy.isOriginVisible(
                originDestination,
                originDetailPath,
                currentDestination,
                currentDetailPath,
                MESSAGE_PATH);
    }
}

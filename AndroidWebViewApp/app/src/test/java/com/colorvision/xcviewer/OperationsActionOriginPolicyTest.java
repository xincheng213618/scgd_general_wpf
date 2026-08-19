package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsActionOriginPolicyTest {
    private static final String MESSAGE_PATH = "/ops/v1/messaging/health";
    private static final String SERVICE_PATH = "/ops/v1/services/health";

    @Test
    public void sameTopLevelOriginCanReceiveItsActionResult() {
        assertTrue(isVisible(
                OperationsDestinationState.TRIAGE, "",
                OperationsDestinationState.TRIAGE, "", MESSAGE_PATH));
        assertTrue(isVisible(
                OperationsDestinationState.TOOLS, "",
                OperationsDestinationState.TOOLS, "", SERVICE_PATH));
    }

    @Test
    public void sameExpectedDetailCanReceiveItsActionResult() {
        assertTrue(isVisible(
                OperationsDestinationState.CAPABILITY_DETAIL, MESSAGE_PATH,
                OperationsDestinationState.CAPABILITY_DETAIL, MESSAGE_PATH, MESSAGE_PATH));
        assertTrue(isVisible(
                OperationsDestinationState.CAPABILITY_DETAIL, SERVICE_PATH,
                OperationsDestinationState.CAPABILITY_DETAIL, SERVICE_PATH, SERVICE_PATH));
    }

    @Test
    public void navigationOrDifferentDetailCannotBeOverwrittenByLateResult() {
        assertFalse(isVisible(
                OperationsDestinationState.TRIAGE, "",
                OperationsDestinationState.TOOLS, "", MESSAGE_PATH));
        assertFalse(isVisible(
                OperationsDestinationState.CAPABILITY_DETAIL, MESSAGE_PATH,
                OperationsDestinationState.CAPABILITY_DETAIL, SERVICE_PATH, MESSAGE_PATH));
        assertFalse(isVisible(
                OperationsDestinationState.CAPABILITY_DETAIL, MESSAGE_PATH,
                OperationsDestinationState.CAPABILITY_DETAIL, MESSAGE_PATH, SERVICE_PATH));
    }

    private static boolean isVisible(
            String originDestination,
            String originDetailPath,
            String currentDestination,
            String currentDetailPath,
            String expectedDetailPath) {
        return OperationsActionOriginPolicy.isVisible(
                originDestination,
                originDetailPath,
                currentDestination,
                currentDetailPath,
                expectedDetailPath);
    }
}

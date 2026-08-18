package com.colorvision.xcviewer;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsInPageNavigationPolicyTest {
    @Test
    public void connectedSubpagesReturnToTheOperationsOverview() {
        assertTrue(OperationsInPageNavigationPolicy.shouldReturnToOverview(
                true, true, false, false));
    }

    @Test
    public void rootRecoveryAndUnpairedStatesKeepSystemBackBehavior() {
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToOverview(
                true, true, true, false));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToOverview(
                true, true, false, true));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToOverview(
                false, true, false, false));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToOverview(
                true, false, false, false));
    }

    @Test
    public void triageDetailsReturnToTheirParentBeforeTheOverview() {
        assertTrue(OperationsInPageNavigationPolicy.shouldReturnToTriage(
                true, true, false, false, true));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToTriage(
                true, true, false, false, false));
    }

    @Test
    public void triageParentReturnKeepsRootAndRecoveryBoundaries() {
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToTriage(
                true, true, true, false, true));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToTriage(
                true, true, false, true, true));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToTriage(
                false, true, false, false, true));
    }
}

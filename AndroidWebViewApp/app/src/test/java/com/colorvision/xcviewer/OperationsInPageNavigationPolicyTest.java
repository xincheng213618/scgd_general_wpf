package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsInPageNavigationPolicyTest {
    @Test
    public void topAppBarUsesTheNearestMeaningfulParent() {
        assertEquals(OperationsDestinationState.OVERVIEW,
                OperationsInPageNavigationPolicy.parentDestination(
                        OperationsDestinationState.CONNECTIONS, false));
        assertEquals(OperationsDestinationState.CONNECTIONS,
                OperationsInPageNavigationPolicy.parentDestination(
                        OperationsDestinationState.CONNECTION_CHECK, false));
        assertEquals(OperationsDestinationState.CONNECTIONS,
                OperationsInPageNavigationPolicy.parentDestination(
                        OperationsDestinationState.FLEET_ISSUES, false));
        assertEquals(OperationsDestinationState.TRIAGE,
                OperationsInPageNavigationPolicy.parentDestination(
                        OperationsDestinationState.JOBS, true));
        assertEquals(OperationsDestinationState.TRIAGE,
                OperationsInPageNavigationPolicy.parentDestination(
                        OperationsDestinationState.TRIAGE, true));
        assertEquals(OperationsDestinationState.OVERVIEW,
                OperationsInPageNavigationPolicy.parentDestination(
                        OperationsDestinationState.JOBS, false));
        assertEquals("",
                OperationsInPageNavigationPolicy.parentDestination(
                        OperationsDestinationState.OVERVIEW, false));
    }

    @Test
    public void topAppBarLabelsExplainWhereNavigateUpReturns() {
        assertEquals("返回电脑与连接",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.CONNECTION_CHECK,
                        false, false, false));
        assertEquals("返回远程排障中心",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.JOBS,
                        true, false, false));
        assertEquals("返回现场运维概览",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.LIVE_MONITOR,
                        false, false, false));
        assertEquals("返回现场运维概览",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.OVERVIEW,
                        false, false, false));
    }

    @Test
    public void topAppBarOnlyShowsNavigateUpForConnectedDetailPages() {
        assertTrue(OperationsInPageNavigationPolicy.showsNavigateUp(
                true, true, OperationsDestinationState.CONNECTIONS,
                false, false, false));
        assertTrue(OperationsInPageNavigationPolicy.showsNavigateUp(
                true, true, OperationsDestinationState.OVERVIEW,
                false, false, false));
        assertFalse(OperationsInPageNavigationPolicy.showsNavigateUp(
                true, true, OperationsDestinationState.OVERVIEW,
                false, true, false));
        assertFalse(OperationsInPageNavigationPolicy.showsNavigateUp(
                false, true, OperationsDestinationState.CONNECTIONS,
                false, false, false));
        assertFalse(OperationsInPageNavigationPolicy.showsNavigateUp(
                true, false, OperationsDestinationState.CONNECTIONS,
                false, false, false));
        assertFalse(OperationsInPageNavigationPolicy.showsNavigateUp(
                true, true, OperationsDestinationState.OVERVIEW,
                false, false, true));
    }

    @Test
    public void hierarchicalNavigationUsesMaterialSharedAxisDirection() {
        assertEquals(AppScreenMotion.DIRECTION_FORWARD,
                OperationsInPageNavigationPolicy.motionDirection(
                        OperationsDestinationState.OVERVIEW,
                        OperationsDestinationState.CONNECTIONS,
                        false));
        assertEquals(AppScreenMotion.DIRECTION_FORWARD,
                OperationsInPageNavigationPolicy.motionDirection(
                        OperationsDestinationState.CONNECTIONS,
                        OperationsDestinationState.CONNECTION_CHECK,
                        false));
        assertEquals(AppScreenMotion.DIRECTION_BACKWARD,
                OperationsInPageNavigationPolicy.motionDirection(
                        OperationsDestinationState.CONNECTION_CHECK,
                        OperationsDestinationState.CONNECTIONS,
                        false));
        assertEquals(AppScreenMotion.DIRECTION_BACKWARD,
                OperationsInPageNavigationPolicy.motionDirection(
                        OperationsDestinationState.JOBS,
                        OperationsDestinationState.TRIAGE,
                        true));
        assertEquals(AppScreenMotion.DIRECTION_NONE,
                OperationsInPageNavigationPolicy.motionDirection(
                        OperationsDestinationState.FLEET_ALL,
                        OperationsDestinationState.FLEET_ISSUES,
                        false));
    }

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

package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsInPageNavigationPolicyTest {
    @Test
    public void topAppBarUsesTheNearestMeaningfulParent() {
        assertEquals(OperationsDestinationState.OVERVIEW,
                parent(OperationsDestinationState.CONNECTIONS, false, false));
        assertEquals(OperationsDestinationState.CONNECTIONS,
                parent(OperationsDestinationState.CONNECTION_CHECK, false, false));
        assertEquals(OperationsDestinationState.CONNECTIONS,
                parent(OperationsDestinationState.FLEET_ISSUES, false, false));
        assertEquals(OperationsDestinationState.TRIAGE,
                parent(OperationsDestinationState.JOBS, true, false));
        assertEquals(OperationsDestinationState.TRIAGE,
                parent(OperationsDestinationState.CAPABILITY_DETAIL, true, false));
        assertEquals(OperationsDestinationState.TRIAGE,
                parent(OperationsDestinationState.TRIAGE, true, false));
        assertEquals("", parent(OperationsDestinationState.TRIAGE, false, false));
        assertEquals(OperationsDestinationState.OVERVIEW,
                parent(OperationsDestinationState.JOBS, false, false));
        assertEquals("", parent(OperationsDestinationState.OVERVIEW, false, false));
    }

    @Test
    public void toolboxDetailsReturnToTheToolsDestination() {
        assertEquals(OperationsDestinationState.TOOLS,
                parent(OperationsDestinationState.JOBS, false, true));
        assertEquals(OperationsDestinationState.TOOLS,
                parent(OperationsDestinationState.SUPPORT, false, true));
        assertEquals(OperationsDestinationState.TOOLS,
                parent(OperationsDestinationState.HISTORY, false, true));
        assertEquals("", parent(OperationsDestinationState.TOOLS, false, false));
    }

    @Test
    public void topAppBarLabelsExplainWhereNavigateUpReturns() {
        assertEquals("返回电脑与连接",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.CONNECTION_CHECK,
                        false, false, false, false));
        assertEquals("返回问题中心",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.JOBS,
                        true, false, false, false));
        assertEquals("返回运维工具",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.JOBS,
                        false, true, false, false));
        assertEquals("返回现场运维概览",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.LIVE_MONITOR,
                        false, false, false, false));
        assertEquals("返回现场运维概览",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.OVERVIEW,
                        false, false, false, false));
    }

    @Test
    public void topAppBarOnlyShowsNavigateUpForConnectedDetailPages() {
        assertTrue(showsNavigateUp(
                true, true, OperationsDestinationState.CONNECTIONS,
                false, false, false, false));
        assertTrue(showsNavigateUp(
                true, true, OperationsDestinationState.OVERVIEW,
                false, false, false, false));
        assertFalse(showsNavigateUp(
                true, true, OperationsDestinationState.OVERVIEW,
                false, false, true, false));
        assertFalse(showsNavigateUp(
                false, true, OperationsDestinationState.CONNECTIONS,
                false, false, false, false));
        assertFalse(showsNavigateUp(
                true, false, OperationsDestinationState.CONNECTIONS,
                false, false, false, false));
        assertFalse(showsNavigateUp(
                true, true, OperationsDestinationState.OVERVIEW,
                false, false, false, true));
        assertFalse(showsNavigateUp(
                true, true, OperationsDestinationState.TOOLS,
                false, false, false, false));
        assertFalse(showsNavigateUp(
                true, true, OperationsDestinationState.TRIAGE,
                false, false, false, false));
        assertFalse(showsNavigateUp(
                true, true, OperationsDestinationState.SETTINGS,
                false, false, false, false));
        assertTrue(showsNavigateUp(
                true, true, OperationsDestinationState.TRIAGE,
                true, false, false, false));
    }

    @Test
    public void hierarchicalNavigationUsesMaterialSharedAxisDirection() {
        assertEquals(AppScreenMotion.DIRECTION_FORWARD,
                motion(OperationsDestinationState.OVERVIEW,
                        OperationsDestinationState.CONNECTIONS, false, false));
        assertEquals(AppScreenMotion.DIRECTION_FORWARD,
                motion(OperationsDestinationState.CONNECTIONS,
                        OperationsDestinationState.CONNECTION_CHECK, false, false));
        assertEquals(AppScreenMotion.DIRECTION_BACKWARD,
                motion(OperationsDestinationState.CONNECTION_CHECK,
                        OperationsDestinationState.CONNECTIONS, false, false));
        assertEquals(AppScreenMotion.DIRECTION_BACKWARD,
                motion(OperationsDestinationState.JOBS,
                        OperationsDestinationState.TRIAGE, true, false));
        assertEquals(AppScreenMotion.DIRECTION_FORWARD,
                motion(OperationsDestinationState.TRIAGE,
                        OperationsDestinationState.LIVE_MONITOR, true, false));
        assertEquals(AppScreenMotion.DIRECTION_BACKWARD,
                motion(OperationsDestinationState.LIVE_MONITOR,
                        OperationsDestinationState.TRIAGE, true, false));
        assertEquals(AppScreenMotion.DIRECTION_FORWARD,
                motion(OperationsDestinationState.TRIAGE,
                        OperationsDestinationState.CAPABILITY_DETAIL, true, false));
        assertEquals(AppScreenMotion.DIRECTION_BACKWARD,
                motion(OperationsDestinationState.CAPABILITY_DETAIL,
                        OperationsDestinationState.TRIAGE, true, false));
        assertEquals(AppScreenMotion.DIRECTION_FORWARD,
                motion(OperationsDestinationState.TOOLS,
                        OperationsDestinationState.JOBS, false, true));
        assertEquals(AppScreenMotion.DIRECTION_BACKWARD,
                motion(OperationsDestinationState.JOBS,
                        OperationsDestinationState.TOOLS, false, true));
        assertEquals(AppScreenMotion.DIRECTION_FORWARD,
                motion(OperationsDestinationState.TOOLS,
                        OperationsDestinationState.SETTINGS, false, false));
        assertEquals(AppScreenMotion.DIRECTION_FORWARD,
                motion(OperationsDestinationState.TRIAGE,
                        OperationsDestinationState.TOOLS, false, false));
        assertEquals(AppScreenMotion.DIRECTION_BACKWARD,
                motion(OperationsDestinationState.TOOLS,
                        OperationsDestinationState.TRIAGE, false, false));
        assertEquals(AppScreenMotion.DIRECTION_FORWARD,
                motion(OperationsDestinationState.OVERVIEW,
                        OperationsDestinationState.SETTINGS, false, false));
        assertEquals(AppScreenMotion.DIRECTION_BACKWARD,
                motion(OperationsDestinationState.SETTINGS,
                        OperationsDestinationState.TRIAGE, false, false));
        assertEquals(AppScreenMotion.DIRECTION_BACKWARD,
                OperationsInPageNavigationPolicy.motionDirection(
                        OperationsDestinationState.CONNECTIONS,
                        OperationsDestinationState.SETTINGS,
                        false,
                        false,
                        true));
        assertEquals(AppScreenMotion.DIRECTION_FORWARD,
                OperationsInPageNavigationPolicy.motionDirection(
                        OperationsDestinationState.SETTINGS,
                        OperationsDestinationState.CONNECTIONS,
                        false,
                        false,
                        true));
        assertEquals(AppScreenMotion.DIRECTION_NONE,
                motion(OperationsDestinationState.FLEET_ALL,
                        OperationsDestinationState.FLEET_ISSUES, false, false));
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

    @Test
    public void systemBackReturnsBottomDestinationsToTheOverview() {
        assertTrue(OperationsInPageNavigationPolicy.shouldReturnToStartDestination(
                OperationsDestinationState.TRIAGE, false, false));
        assertTrue(OperationsInPageNavigationPolicy.shouldReturnToStartDestination(
                OperationsDestinationState.TOOLS, false, false));
        assertTrue(OperationsInPageNavigationPolicy.shouldReturnToStartDestination(
                OperationsDestinationState.SETTINGS, false, false));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToStartDestination(
                OperationsDestinationState.LIVE_MONITOR, true, false));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToStartDestination(
                OperationsDestinationState.JOBS, false, true));
    }

    @Test
    public void settingsOwnedConnectionPageReturnsToSettingsOnlyAtItsRoot() {
        assertTrue(OperationsInPageNavigationPolicy.shouldReturnToSettings(
                OperationsDestinationState.CONNECTIONS, true));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToSettings(
                OperationsDestinationState.CONNECTIONS, false));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToSettings(
                OperationsDestinationState.CONNECTION_CHECK, true));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToSettings(
                OperationsDestinationState.OVERVIEW, true));
    }

    private static String parent(
            String destination, boolean fromTriage, boolean fromToolbox) {
        return OperationsInPageNavigationPolicy.parentDestination(
                destination, fromTriage, fromToolbox);
    }

    private static boolean showsNavigateUp(
            boolean paired,
            boolean dashboardVisible,
            String destination,
            boolean fromTriage,
            boolean fromToolbox,
            boolean summaryVisible,
            boolean recoveryVisible) {
        return OperationsInPageNavigationPolicy.showsNavigateUp(
                paired,
                dashboardVisible,
                destination,
                fromTriage,
                fromToolbox,
                summaryVisible,
                recoveryVisible);
    }

    private static int motion(
            String from, String to, boolean fromTriage, boolean fromToolbox) {
        return OperationsInPageNavigationPolicy.motionDirection(
                from, to, fromTriage, fromToolbox, false);
    }
}

package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsInPageNavigationPolicyTest {
    @Test
    public void detailParentStateIsExclusiveAndMigratesLegacyFlags() {
        assertEquals(OperationsDestinationState.TRIAGE,
                OperationsInPageNavigationPolicy.normalizeDetailParent(
                        OperationsDestinationState.TRIAGE));
        assertEquals(OperationsDestinationState.TOOLS,
                OperationsInPageNavigationPolicy.normalizeDetailParent(
                        OperationsDestinationState.TOOLS));
        assertEquals(OperationsDestinationState.SETTINGS,
                OperationsInPageNavigationPolicy.normalizeDetailParent(
                        OperationsDestinationState.SETTINGS));
        assertEquals(OperationsDestinationState.TOOLS,
                OperationsInPageNavigationPolicy.restoreDetailParent(
                        OperationsDestinationState.TOOLS, true, false));
        assertEquals(OperationsDestinationState.TRIAGE,
                OperationsInPageNavigationPolicy.restoreDetailParent(null, true, true));
        assertEquals(OperationsDestinationState.TOOLS,
                OperationsInPageNavigationPolicy.restoreDetailParent(null, false, true));
        assertEquals(OperationsDestinationState.OVERVIEW,
                OperationsInPageNavigationPolicy.restoreDetailParent(null, false, false));
    }

    @Test
    public void topAppBarUsesTheNearestMeaningfulParent() {
        assertEquals(OperationsDestinationState.OVERVIEW,
                parent(OperationsDestinationState.CONNECTIONS, false, false));
        assertEquals(OperationsDestinationState.CONNECTIONS,
                parent(OperationsDestinationState.CONNECTION_CHECK, false, false));
        assertEquals(OperationsDestinationState.TRIAGE,
                parent(OperationsDestinationState.CONNECTION_CHECK, true, false));
        assertEquals(OperationsDestinationState.CONNECTIONS,
                parent(OperationsDestinationState.FLEET_ISSUES, false, false));
        assertEquals(OperationsDestinationState.TRIAGE,
                parent(OperationsDestinationState.JOBS, true, false));
        assertEquals(OperationsDestinationState.TRIAGE,
                parent(OperationsDestinationState.CAPABILITY_DETAIL, true, false));
        assertEquals("",
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
        assertEquals(OperationsDestinationState.SETTINGS,
                OperationsInPageNavigationPolicy.parentDestination(
                        OperationsDestinationState.HISTORY,
                        OperationsDestinationState.SETTINGS));
        assertEquals("", parent(OperationsDestinationState.TOOLS, false, false));
        assertEquals("", parent(OperationsDestinationState.TOOLS, false, true));
    }

    @Test
    public void topAppBarLabelsExplainWhereNavigateUpReturns() {
        assertEquals("返回电脑与连接",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.CONNECTION_CHECK,
                        detailParent(false, false), false, false));
        assertEquals("返回问题中心",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.JOBS,
                        detailParent(true, false), false, false));
        assertEquals("返回问题中心",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.CONNECTION_CHECK,
                        detailParent(true, false), false, false));
        assertEquals("返回运维工具",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.JOBS,
                        detailParent(false, true), false, false));
        assertEquals("返回设置",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.HISTORY,
                        OperationsDestinationState.SETTINGS, false, false));
        assertEquals("返回现场运维概览",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.LIVE_MONITOR,
                        detailParent(false, false), false, false));
        assertEquals("返回现场运维概览",
                OperationsInPageNavigationPolicy.navigateUpLabel(
                        OperationsDestinationState.OVERVIEW,
                        detailParent(false, false), false, false));
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
        assertFalse(showsNavigateUp(
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
                        OperationsDestinationState.OVERVIEW,
                        OperationsDestinationState.SETTINGS));
        assertEquals(AppScreenMotion.DIRECTION_FORWARD,
                OperationsInPageNavigationPolicy.motionDirection(
                        OperationsDestinationState.SETTINGS,
                        OperationsDestinationState.CONNECTIONS,
                        OperationsDestinationState.OVERVIEW,
                        OperationsDestinationState.SETTINGS));
        assertEquals(AppScreenMotion.DIRECTION_NONE,
                motion(OperationsDestinationState.FLEET_ALL,
                        OperationsDestinationState.FLEET_ISSUES, false, false));
        assertEquals(AppScreenMotion.DIRECTION_FORWARD,
                OperationsInPageNavigationPolicy.motionDirection(
                        OperationsDestinationState.SETTINGS,
                        OperationsDestinationState.HISTORY,
                        OperationsDestinationState.SETTINGS,
                        OperationsDestinationState.OVERVIEW));
        assertEquals(AppScreenMotion.DIRECTION_BACKWARD,
                OperationsInPageNavigationPolicy.motionDirection(
                        OperationsDestinationState.HISTORY,
                        OperationsDestinationState.SETTINGS,
                        OperationsDestinationState.SETTINGS,
                        OperationsDestinationState.OVERVIEW));
    }

    @Test
    public void onlyBottomNavigationDestinationsUseFadeThrough() {
        assertTrue(OperationsInPageNavigationPolicy.isTopLevelTransition(
                OperationsDestinationState.OVERVIEW,
                OperationsDestinationState.SETTINGS));
        assertTrue(OperationsInPageNavigationPolicy.isTopLevelTransition(
                OperationsDestinationState.SETTINGS,
                OperationsDestinationState.TRIAGE));
        assertTrue(OperationsInPageNavigationPolicy.isTopLevelTransition(
                OperationsDestinationState.TRIAGE,
                OperationsDestinationState.TOOLS));
        assertFalse(OperationsInPageNavigationPolicy.isTopLevelTransition(
                OperationsDestinationState.SETTINGS,
                OperationsDestinationState.CONNECTIONS));
        assertFalse(OperationsInPageNavigationPolicy.isTopLevelTransition(
                OperationsDestinationState.CONNECTIONS,
                OperationsDestinationState.SETTINGS));
        assertFalse(OperationsInPageNavigationPolicy.isTopLevelTransition(
                OperationsDestinationState.SETTINGS,
                OperationsDestinationState.SETTINGS));
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
                true, true, false, false, detailParent(true, false)));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToTriage(
                true, true, false, false, detailParent(false, false)));
    }

    @Test
    public void triageParentReturnKeepsRootAndRecoveryBoundaries() {
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToTriage(
                true, true, true, false, detailParent(true, false)));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToTriage(
                true, true, false, true, detailParent(true, false)));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToTriage(
                false, true, false, false, detailParent(true, false)));
    }

    @Test
    public void systemBackReturnsBottomDestinationsToTheOverview() {
        assertTrue(OperationsInPageNavigationPolicy.shouldReturnToStartDestination(
                OperationsDestinationState.TRIAGE));
        assertTrue(OperationsInPageNavigationPolicy.shouldReturnToStartDestination(
                OperationsDestinationState.TOOLS));
        assertTrue(OperationsInPageNavigationPolicy.shouldReturnToStartDestination(
                OperationsDestinationState.SETTINGS));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToStartDestination(
                OperationsDestinationState.LIVE_MONITOR));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToStartDestination(
                OperationsDestinationState.JOBS));
    }

    @Test
    public void connectionManagementReturnsToItsOwningBottomDestinationOnlyAtItsRoot() {
        assertTrue(OperationsInPageNavigationPolicy.shouldReturnToConnectionsParent(
                OperationsDestinationState.CONNECTIONS,
                OperationsDestinationState.SETTINGS));
        assertTrue(OperationsInPageNavigationPolicy.shouldReturnToConnectionsParent(
                OperationsDestinationState.CONNECTIONS,
                OperationsDestinationState.TOOLS));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToConnectionsParent(
                OperationsDestinationState.CONNECTIONS,
                OperationsDestinationState.OVERVIEW));
        assertFalse(OperationsInPageNavigationPolicy.shouldReturnToConnectionsParent(
                OperationsDestinationState.CONNECTION_CHECK,
                OperationsDestinationState.SETTINGS));
        assertEquals("返回问题中心",
                OperationsInPageNavigationPolicy.connectionsParentLabel(
                        OperationsDestinationState.TRIAGE));
        assertEquals("返回运维工具",
                OperationsInPageNavigationPolicy.connectionsParentLabel(
                        OperationsDestinationState.TOOLS));
        assertEquals("返回设置",
                OperationsInPageNavigationPolicy.connectionsParentLabel(
                        OperationsDestinationState.SETTINGS));
    }

    @Test
    public void targetManagementKeepsTheNearestBottomDestination() {
        assertEquals(OperationsDestinationState.OVERVIEW,
                targetParent(OperationsDestinationState.OVERVIEW, false, false, ""));
        assertEquals(OperationsDestinationState.TRIAGE,
                targetParent(OperationsDestinationState.TRIAGE, false, false, ""));
        assertEquals(OperationsDestinationState.TOOLS,
                targetParent(OperationsDestinationState.TOOLS, false, false, ""));
        assertEquals(OperationsDestinationState.SETTINGS,
                targetParent(OperationsDestinationState.SETTINGS, false, false, ""));
        assertEquals(OperationsDestinationState.TRIAGE,
                targetParent(
                        OperationsDestinationState.CAPABILITY_DETAIL,
                        true,
                        false,
                        OperationsDestinationState.OVERVIEW));
        assertEquals(OperationsDestinationState.SETTINGS,
                targetParent(
                        OperationsDestinationState.CONNECTION_CHECK,
                        false,
                        false,
                        OperationsDestinationState.SETTINGS));
    }

    private static String parent(
            String destination, boolean fromTriage, boolean fromToolbox) {
        return OperationsInPageNavigationPolicy.parentDestination(
                destination, detailParent(fromTriage, fromToolbox));
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
                detailParent(fromTriage, fromToolbox),
                summaryVisible,
                recoveryVisible);
    }

    private static int motion(
            String from, String to, boolean fromTriage, boolean fromToolbox) {
        return OperationsInPageNavigationPolicy.motionDirection(
                from,
                to,
                detailParent(fromTriage, fromToolbox),
                OperationsDestinationState.OVERVIEW);
    }

    private static String targetParent(
            String destination,
            boolean fromTriage,
            boolean fromToolbox,
            String existingParent) {
        return OperationsInPageNavigationPolicy.targetManagementParentDestination(
                destination, detailParent(fromTriage, fromToolbox), existingParent);
    }

    private static String detailParent(boolean fromTriage, boolean fromToolbox) {
        if (fromTriage) {
            return OperationsDestinationState.TRIAGE;
        }
        if (fromToolbox) {
            return OperationsDestinationState.TOOLS;
        }
        return OperationsDestinationState.OVERVIEW;
    }
}

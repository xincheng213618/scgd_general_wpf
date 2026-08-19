package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class AppNavigationPolicyTest {
    private static final int OPERATIONS = 0;
    private static final int TOOLS = 1;
    private static final int SETTINGS = 2;
    private static final int PROBLEMS = 3;

    @Test
    public void pairedOperationsLaunchUsesTheFullOperationsSurface() {
        assertTrue(AppNavigationPolicy.shouldOpenPairedWorkspace(true, true));
        assertFalse(AppNavigationPolicy.shouldOpenPairedWorkspace(false, true));
        assertFalse(AppNavigationPolicy.shouldOpenPairedWorkspace(true, false));
        assertEquals(OperationsDestinationState.OVERVIEW,
                pairedDestination(OPERATIONS));
        assertEquals(OperationsDestinationState.TRIAGE,
                pairedDestination(PROBLEMS));
        assertEquals(OperationsDestinationState.TOOLS,
                pairedDestination(TOOLS));
        assertEquals(OperationsDestinationState.SETTINGS,
                pairedDestination(SETTINGS));
    }

    @Test
    public void updateAndRelayUseOneApplicationOwnedServiceOrigin() {
        assertEquals("http://xc213618.ddns.me:9998/", AppNavigationPolicy.FIXED_SERVICE_ORIGIN);
    }

    @Test
    public void fourTopLevelDestinationsNormalizeWithoutRevivingRemovedDownloadStation() {
        assertEquals(PROBLEMS, normalize(PROBLEMS, OPERATIONS));
        assertEquals(TOOLS, normalize(TOOLS, OPERATIONS));
        assertEquals(SETTINGS, normalize(SETTINGS, OPERATIONS));
        assertEquals(PROBLEMS, normalize(-1, PROBLEMS));
        assertEquals(OPERATIONS, normalize(9, 9));
        assertTrue(AppNavigationPolicy.isTopLevelTab(
                PROBLEMS, OPERATIONS, PROBLEMS, TOOLS, SETTINGS));
        assertFalse(AppNavigationPolicy.isTopLevelTab(
                9, OPERATIONS, PROBLEMS, TOOLS, SETTINGS));
    }

    @Test
    public void activityRecreationRestoresTheVisibleDestination() {
        assertEquals(PROBLEMS, resolve(true, PROBLEMS, OPERATIONS));
        assertEquals(OPERATIONS, resolve(true, -1, OPERATIONS));
        assertEquals(TOOLS, resolve(false, OPERATIONS, TOOLS));
    }

    private static String pairedDestination(int tab) {
        return AppNavigationPolicy.pairedDestinationForTab(
                tab, OPERATIONS, PROBLEMS, TOOLS, SETTINGS);
    }

    private static int normalize(int requested, int persisted) {
        return AppNavigationPolicy.normalizeStartTab(
                requested, persisted, OPERATIONS, PROBLEMS, TOOLS, SETTINGS);
    }

    private static int resolve(boolean restoring, int restored, int requested) {
        return AppNavigationPolicy.resolveCreationTab(
                restoring,
                restored,
                requested,
                OPERATIONS,
                PROBLEMS,
                TOOLS,
                SETTINGS);
    }
}

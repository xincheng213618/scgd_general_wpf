package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class AppNavigationPolicyTest {
    @Test
    public void pairedOperationsLaunchUsesTheFullOperationsSurface() {
        assertTrue(AppNavigationPolicy.shouldOpenOperationsDirectly(true, true));
        assertFalse(AppNavigationPolicy.shouldOpenOperationsDirectly(false, true));
        assertFalse(AppNavigationPolicy.shouldOpenOperationsDirectly(true, false));
    }

    @Test
    public void updateAndRelayUseOneApplicationOwnedServiceOrigin() {
        assertEquals("http://xc213618.ddns.me:9998/", AppNavigationPolicy.FIXED_SERVICE_ORIGIN);
    }

    @Test
    public void removedDownloadTabFallsBackToOperations() {
        assertEquals(0, AppNavigationPolicy.normalizeStartTab(1, 1, 0, 2));
        assertEquals(2, AppNavigationPolicy.normalizeStartTab(2, 0, 0, 2));
        assertEquals(2, AppNavigationPolicy.normalizeStartTab(-1, 2, 0, 2));
    }
}

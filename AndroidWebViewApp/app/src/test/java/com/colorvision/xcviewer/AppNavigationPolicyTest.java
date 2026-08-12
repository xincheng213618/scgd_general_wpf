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
    public void downloadStationHasOneApplicationOwnedAddress() {
        assertEquals("http://xc213618.ddns.me:9998/", AppNavigationPolicy.FIXED_DOWNLOAD_URL);
    }
}

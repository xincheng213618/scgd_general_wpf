package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

public class AppWindowInsetsPolicyTest {
    @Test
    public void statusBarInsetProtectsAppViewport() {
        assertEquals(116, AppWindowInsetsPolicy.topContentInset(116, 0));
    }

    @Test
    public void displayCutoutCanExtendTheSafeTopEdge() {
        assertEquals(144, AppWindowInsetsPolicy.topContentInset(116, 144));
    }

    @Test
    public void invalidNegativeInsetsDoNotCreateNegativePadding() {
        assertEquals(0, AppWindowInsetsPolicy.topContentInset(-1, -4));
    }

    @Test
    public void navigationRailKeepsContentAboveTheGestureInset() {
        assertEquals(52, AppWindowInsetsPolicy.bottomContentInset(true, 52));
        assertEquals(0, AppWindowInsetsPolicy.bottomContentInset(true, -1));
    }

    @Test
    public void bottomNavigationOwnsItsSystemInset() {
        assertEquals(0, AppWindowInsetsPolicy.bottomContentInset(false, 52));
    }
}

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
}

package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

public class OperationsWindowInsetsPolicyTest {
    @Test
    public void statusBarInsetProtectsScrollableViewport() {
        assertEquals(116, OperationsWindowInsetsPolicy.topContentInset(116, 0));
    }

    @Test
    public void displayCutoutCanExtendTheSafeTopEdge() {
        assertEquals(144, OperationsWindowInsetsPolicy.topContentInset(116, 144));
    }

    @Test
    public void invalidNegativeInsetsDoNotCreateNegativePadding() {
        assertEquals(0, OperationsWindowInsetsPolicy.topContentInset(-1, -4));
    }
}

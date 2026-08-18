package com.colorvision.xcviewer;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsResponsiveLayoutTest {
    @Test
    public void standardFontKeepsCompactTwoColumnLayout() {
        assertFalse(OperationsResponsiveLayout.usesSingleColumn(1.0f));
        assertFalse(OperationsResponsiveLayout.usesSingleColumn(1.19f));
    }

    @Test
    public void largeFontUsesSingleColumnLayout() {
        assertTrue(OperationsResponsiveLayout.usesSingleColumn(1.2f));
        assertTrue(OperationsResponsiveLayout.usesSingleColumn(1.3f));
    }

    @Test
    public void invalidFontScaleDoesNotForceExpandedLayout() {
        assertFalse(OperationsResponsiveLayout.usesSingleColumn(Float.NaN));
        assertFalse(OperationsResponsiveLayout.usesSingleColumn(Float.POSITIVE_INFINITY));
    }
}

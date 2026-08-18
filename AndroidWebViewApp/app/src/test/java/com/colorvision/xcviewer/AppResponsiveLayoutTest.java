package com.colorvision.xcviewer;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class AppResponsiveLayoutTest {
    @Test
    public void standardFontKeepsCompactLayouts() {
        assertFalse(AppResponsiveLayout.usesSingleColumn(1.0f));
        assertFalse(AppResponsiveLayout.usesSingleColumn(1.19f));
    }

    @Test
    public void largeFontUsesSingleColumnLayoutsAcrossTheApp() {
        assertTrue(AppResponsiveLayout.usesSingleColumn(1.2f));
        assertTrue(AppResponsiveLayout.usesSingleColumn(1.3f));
    }

    @Test
    public void invalidFontScaleDoesNotForceExpandedLayouts() {
        assertFalse(AppResponsiveLayout.usesSingleColumn(Float.NaN));
        assertFalse(AppResponsiveLayout.usesSingleColumn(Float.POSITIVE_INFINITY));
    }
}

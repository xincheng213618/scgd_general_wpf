package com.colorvision.xcviewer;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class AppResponsiveLayoutTest {
    @Test
    public void compactWindowUsesSingleColumnAtStandardFontScale() {
        assertTrue(AppResponsiveLayout.usesSingleColumn(360, 1.0f));
        assertTrue(AppResponsiveLayout.usesSingleColumn(599, 1.0f));
    }

    @Test
    public void expandedWindowKeepsMultiColumnAtStandardFontScale() {
        assertFalse(AppResponsiveLayout.usesSingleColumn(600, 1.0f));
        assertFalse(AppResponsiveLayout.usesSingleColumn(840, 1.19f));
    }

    @Test
    public void compactWindowsUseBottomNavigation() {
        assertFalse(AppResponsiveLayout.usesNavigationRail(360));
        assertFalse(AppResponsiveLayout.usesNavigationRail(599));
    }

    @Test
    public void mediumAndExpandedWindowsUseNavigationRail() {
        assertTrue(AppResponsiveLayout.usesNavigationRail(600));
        assertTrue(AppResponsiveLayout.usesNavigationRail(840));
        assertFalse(AppResponsiveLayout.usesNavigationRail(0));
    }

    @Test
    public void largeFontUsesSingleColumnLayoutsAcrossTheApp() {
        assertTrue(AppResponsiveLayout.usesSingleColumn(600, 1.2f));
        assertTrue(AppResponsiveLayout.usesSingleColumn(840, 1.3f));
    }

    @Test
    public void undefinedWidthAndInvalidFontScaleDoNotForceSingleColumn() {
        assertFalse(AppResponsiveLayout.usesSingleColumn(0, Float.NaN));
        assertFalse(AppResponsiveLayout.usesSingleColumn(-1, Float.POSITIVE_INFINITY));
    }

    @Test
    public void extremeFontScaleStacksTrailingControlsOnlyWhenSpaceIsTight() {
        assertFalse(AppResponsiveLayout.usesStackedControlRow(360, 1.5f));
        assertTrue(AppResponsiveLayout.usesStackedControlRow(360, 2.0f));
        assertFalse(AppResponsiveLayout.usesStackedControlRow(600, 2.0f));
    }

    @Test
    public void invalidDimensionsDoNotStackTrailingControls() {
        assertFalse(AppResponsiveLayout.usesStackedControlRow(0, 2.0f));
        assertFalse(AppResponsiveLayout.usesStackedControlRow(360, Float.NaN));
        assertFalse(AppResponsiveLayout.usesStackedControlRow(360, 0f));
    }
}

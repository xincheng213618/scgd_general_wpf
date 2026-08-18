package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class AppScreenMotionTest {
    private static final int OPERATIONS = 0;
    private static final int SETTINGS = 2;

    @Test
    public void settingsUsesForwardMotionFromOperations() {
        assertEquals(
                AppScreenMotion.DIRECTION_FORWARD,
                AppScreenMotion.directionBetween(OPERATIONS, SETTINGS, OPERATIONS, SETTINGS));
    }

    @Test
    public void operationsUsesBackwardMotionFromSettings() {
        assertEquals(
                AppScreenMotion.DIRECTION_BACKWARD,
                AppScreenMotion.directionBetween(SETTINGS, OPERATIONS, OPERATIONS, SETTINGS));
    }

    @Test
    public void reselectingOrUnknownDestinationsDoNotAnimate() {
        assertEquals(
                AppScreenMotion.DIRECTION_NONE,
                AppScreenMotion.directionBetween(OPERATIONS, OPERATIONS, OPERATIONS, SETTINGS));
        assertEquals(
                AppScreenMotion.DIRECTION_NONE,
                AppScreenMotion.directionBetween(SETTINGS, SETTINGS, OPERATIONS, SETTINGS));
        assertEquals(
                AppScreenMotion.DIRECTION_NONE,
                AppScreenMotion.directionBetween(OPERATIONS, 1, OPERATIONS, SETTINGS));
    }

    @Test
    public void onlyDirectionalMovesUseSharedAxis() {
        assertTrue(AppScreenMotion.usesSharedAxis(AppScreenMotion.DIRECTION_FORWARD));
        assertTrue(AppScreenMotion.usesSharedAxis(AppScreenMotion.DIRECTION_BACKWARD));
        assertFalse(AppScreenMotion.usesSharedAxis(AppScreenMotion.DIRECTION_NONE));
    }

    @Test
    public void ltrActivityMotionFollowsForwardAndBackwardDirection() {
        assertTrue(AppScreenMotion.entersFromRight(true, false));
        assertFalse(AppScreenMotion.entersFromRight(false, false));
    }

    @Test
    public void rtlActivityMotionMirrorsForwardAndBackwardDirection() {
        assertFalse(AppScreenMotion.entersFromRight(true, true));
        assertTrue(AppScreenMotion.entersFromRight(false, true));
    }
}

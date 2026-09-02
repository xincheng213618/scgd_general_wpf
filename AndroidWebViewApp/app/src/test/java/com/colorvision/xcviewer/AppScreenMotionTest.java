package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class AppScreenMotionTest {
    private static final int OPERATIONS = 0;
    private static final int TOOLS = 1;
    private static final int SETTINGS = 2;
    private static final int PROBLEMS = 3;

    @Test
    public void settingsUsesForwardMotionFromOperations() {
        assertEquals(
                AppScreenMotion.DIRECTION_FORWARD,
                AppScreenMotion.directionBetween(
                        OPERATIONS,
                        SETTINGS,
                        OPERATIONS,
                        PROBLEMS,
                        TOOLS,
                        SETTINGS));
    }

    @Test
    public void operationsUsesBackwardMotionFromSettings() {
        assertEquals(
                AppScreenMotion.DIRECTION_BACKWARD,
                AppScreenMotion.directionBetween(
                        SETTINGS,
                        OPERATIONS,
                        OPERATIONS,
                        PROBLEMS,
                        TOOLS,
                        SETTINGS));
    }

    @Test
    public void problemsAndToolsFollowTheirBottomNavigationOrder() {
        assertEquals(
                AppScreenMotion.DIRECTION_FORWARD,
                AppScreenMotion.directionBetween(
                        OPERATIONS,
                        PROBLEMS,
                        OPERATIONS,
                        PROBLEMS,
                        TOOLS,
                        SETTINGS));
        assertEquals(
                AppScreenMotion.DIRECTION_FORWARD,
                AppScreenMotion.directionBetween(
                        PROBLEMS,
                        TOOLS,
                        OPERATIONS,
                        PROBLEMS,
                        TOOLS,
                        SETTINGS));
        assertEquals(
                AppScreenMotion.DIRECTION_BACKWARD,
                AppScreenMotion.directionBetween(
                        SETTINGS,
                        TOOLS,
                        OPERATIONS,
                        PROBLEMS,
                        TOOLS,
                        SETTINGS));
    }

    @Test
    public void reselectingOrUnknownDestinationsDoNotAnimate() {
        assertEquals(
                AppScreenMotion.DIRECTION_NONE,
                AppScreenMotion.directionBetween(
                        OPERATIONS,
                        OPERATIONS,
                        OPERATIONS,
                        PROBLEMS,
                        TOOLS,
                        SETTINGS));
        assertEquals(
                AppScreenMotion.DIRECTION_NONE,
                AppScreenMotion.directionBetween(
                        SETTINGS,
                        SETTINGS,
                        OPERATIONS,
                        PROBLEMS,
                        TOOLS,
                        SETTINGS));
        assertEquals(
                AppScreenMotion.DIRECTION_NONE,
                AppScreenMotion.directionBetween(
                        OPERATIONS,
                        9,
                        OPERATIONS,
                        PROBLEMS,
                        TOOLS,
                        SETTINGS));
    }

    @Test
    public void topLevelDirectionalMovesUseFadeThrough() {
        assertTrue(AppScreenMotion.usesFadeThrough(
                AppScreenMotion.DIRECTION_FORWARD, true));
        assertTrue(AppScreenMotion.usesFadeThrough(
                AppScreenMotion.DIRECTION_BACKWARD, true));
        assertFalse(AppScreenMotion.usesFadeThrough(
                AppScreenMotion.DIRECTION_NONE, true));
        assertFalse(AppScreenMotion.usesFadeThrough(
                AppScreenMotion.DIRECTION_FORWARD, false));
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

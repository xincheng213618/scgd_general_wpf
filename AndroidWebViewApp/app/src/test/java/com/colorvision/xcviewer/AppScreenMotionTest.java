package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

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
}

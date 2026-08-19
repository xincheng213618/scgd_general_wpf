package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class RuntimePermissionDialogStateTest {
    @Test
    public void visibleSystemDialogCompletesDeniedResultWithoutBlockedRecovery() {
        RuntimePermissionDialogState state = new RuntimePermissionDialogState();
        int generation = state.begin();

        state.observe(generation, false, false);

        assertTrue(state.completeFromSystemResult(false));
        assertFalse(state.shouldRecoverAsBlocked(generation, false, true));
    }

    @Test
    public void deniedResultWithoutAVisibleDialogRecoversAsBlocked() {
        RuntimePermissionDialogState state = new RuntimePermissionDialogState();
        int generation = state.begin();

        assertFalse(state.completeFromSystemResult(false));
        assertTrue(state.shouldRecoverAsBlocked(generation, false, true));
    }

    @Test
    public void activeDialogAtRecoveryTimeIsObservedInsteadOfBlocked() {
        RuntimePermissionDialogState state = new RuntimePermissionDialogState();
        int generation = state.begin();

        assertFalse(state.shouldRecoverAsBlocked(generation, false, false));
        assertTrue(state.completeFromSystemResult(false));
    }

    @Test
    public void staleCallbacksCannotChangeTheLatestRequest() {
        RuntimePermissionDialogState state = new RuntimePermissionDialogState();
        int staleGeneration = state.begin();
        int currentGeneration = state.begin();

        state.observe(staleGeneration, false, false);

        assertFalse(state.shouldRecoverAsBlocked(staleGeneration, false, true));
        assertTrue(state.shouldRecoverAsBlocked(currentGeneration, false, true));
    }
}

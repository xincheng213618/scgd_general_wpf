package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsTopLevelStateTest {
    @Test
    public void eachNavigationDestinationKeepsAnIndependentScrollPosition() {
        OperationsTopLevelState state = new OperationsTopLevelState();

        state.rememberScroll(OperationsDestinationState.OVERVIEW, 120);
        state.rememberScroll(OperationsDestinationState.TRIAGE, 340);
        state.rememberScroll(OperationsDestinationState.TOOLS, 860);
        state.rememberScroll(OperationsDestinationState.SETTINGS, 45);

        assertEquals(120, state.scrollY(OperationsDestinationState.OVERVIEW));
        assertEquals(340, state.scrollY(OperationsDestinationState.TRIAGE));
        assertEquals(860, state.scrollY(OperationsDestinationState.TOOLS));
        assertEquals(45, state.scrollY(OperationsDestinationState.SETTINGS));
    }

    @Test
    public void detailPagesCannotOverwriteTheirParentDestinationPosition() {
        OperationsTopLevelState state = new OperationsTopLevelState();
        state.rememberScroll(OperationsDestinationState.TOOLS, 860);

        state.rememberScroll(OperationsDestinationState.CAPABILITY_DETAIL, 0);
        state.rememberScroll(OperationsDestinationState.JOBS, 40);

        assertEquals(860, state.scrollY(OperationsDestinationState.TOOLS));
        assertEquals(0, state.scrollY(OperationsDestinationState.CAPABILITY_DETAIL));
    }

    @Test
    public void reselectResetOnlyChangesTheRequestedDestination() {
        OperationsTopLevelState state = new OperationsTopLevelState();
        state.rememberScroll(OperationsDestinationState.TRIAGE, 300);
        state.rememberScroll(OperationsDestinationState.TOOLS, 900);

        state.resetScroll(OperationsDestinationState.TOOLS);

        assertEquals(300, state.scrollY(OperationsDestinationState.TRIAGE));
        assertEquals(0, state.scrollY(OperationsDestinationState.TOOLS));
    }

    @Test
    public void invalidOffsetsAndDestinationKindsAreBounded() {
        OperationsTopLevelState state = new OperationsTopLevelState();

        state.rememberScroll(OperationsDestinationState.SETTINGS, -20);

        assertEquals(0, state.scrollY(OperationsDestinationState.SETTINGS));
        assertTrue(OperationsTopLevelState.isDashboardTopLevel(
                OperationsDestinationState.TRIAGE));
        assertFalse(OperationsTopLevelState.isDashboardTopLevel(
                OperationsDestinationState.SETTINGS));
    }
}

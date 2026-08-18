package com.colorvision.xcviewer;

import org.junit.Test;

import java.util.List;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

public class OperationsDashboardShortcutPresentationTest {
    @Test
    public void directDashboardPromotesFourDistinctTopLevelDestinations() {
        List<OperationsDashboardShortcutPresentation.Shortcut> shortcuts =
                OperationsDashboardShortcutPresentation.direct();

        assertEquals(4, shortcuts.size());
        assertTrue(OperationsDashboardShortcutPresentation.hasUniqueActionIds(shortcuts));
        assertEquals(OperationsDashboardShortcutPresentation.ACTION_TRIAGE,
                shortcuts.get(0).actionId);
        assertEquals(OperationsDashboardShortcutPresentation.ACTION_TOOLBOX,
                shortcuts.get(3).actionId);
    }

    @Test
    public void shortcutsExplainTheirDestinationToAssistiveTechnology() {
        for (OperationsDashboardShortcutPresentation.Shortcut shortcut
                : OperationsDashboardShortcutPresentation.direct()) {
            assertTrue(shortcut.accessibilityLabel().contains(shortcut.label));
            assertTrue(shortcut.accessibilityLabel().contains(shortcut.summary));
        }
    }
}

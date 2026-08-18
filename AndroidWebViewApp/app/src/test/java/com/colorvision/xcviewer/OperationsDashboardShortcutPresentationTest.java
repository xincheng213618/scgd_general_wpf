package com.colorvision.xcviewer;

import org.junit.Test;

import java.util.List;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

public class OperationsDashboardShortcutPresentationTest {
    @Test
    public void directDashboardKeepsOnlyConnectionShortcutsOutsideBottomNavigation() {
        List<OperationsDashboardShortcutPresentation.Shortcut> shortcuts =
                OperationsDashboardShortcutPresentation.direct();

        assertEquals(2, shortcuts.size());
        assertTrue(OperationsDashboardShortcutPresentation.hasUniqueActionIds(shortcuts));
        assertEquals(OperationsDashboardShortcutPresentation.ACTION_CONNECTION_CHECK,
                shortcuts.get(0).actionId);
        assertEquals(OperationsDashboardShortcutPresentation.ACTION_CONNECTIONS,
                shortcuts.get(1).actionId);
        assertTrue(shortcuts.get(0).tonal);
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

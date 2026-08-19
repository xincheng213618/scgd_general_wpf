package com.colorvision.xcviewer;

final class AppNavigationPolicy {
    static final String FIXED_SERVICE_ORIGIN = "http://xc213618.ddns.me:9998/";

    private AppNavigationPolicy() {
    }

    static boolean shouldOpenPairedWorkspace(
            boolean hasOperationsProfile,
            boolean topLevelDestinationRequested) {
        return hasOperationsProfile && topLevelDestinationRequested;
    }

    static boolean isTopLevelTab(
            int tab,
            int operationsTab,
            int problemsTab,
            int toolsTab,
            int settingsTab) {
        return tab == operationsTab
                || tab == problemsTab
                || tab == toolsTab
                || tab == settingsTab;
    }

    static String pairedDestinationForTab(
            int tab,
            int operationsTab,
            int problemsTab,
            int toolsTab,
            int settingsTab) {
        if (tab == problemsTab) {
            return OperationsDestinationState.TRIAGE;
        }
        if (tab == toolsTab) {
            return OperationsDestinationState.TOOLS;
        }
        if (tab == settingsTab) {
            return OperationsDestinationState.SETTINGS;
        }
        return OperationsDestinationState.OVERVIEW;
    }

    static int normalizeStartTab(
            int requestedTab,
            int persistedTab,
            int operationsTab,
            int problemsTab,
            int toolsTab,
            int settingsTab) {
        if (isTopLevelTab(
                requestedTab, operationsTab, problemsTab, toolsTab, settingsTab)) {
            return requestedTab;
        }
        if (persistedTab == problemsTab
                || persistedTab == toolsTab
                || persistedTab == settingsTab) {
            return persistedTab;
        }
        return operationsTab;
    }

    static int resolveCreationTab(
            boolean restoring,
            int restoredTab,
            int requestedTab,
            int operationsTab,
            int problemsTab,
            int toolsTab,
            int settingsTab) {
        return normalizeStartTab(
                restoring ? restoredTab : requestedTab,
                requestedTab,
                operationsTab,
                problemsTab,
                toolsTab,
                settingsTab);
    }
}

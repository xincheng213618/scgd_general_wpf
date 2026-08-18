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

    static String pairedDestinationForTab(
            int tab,
            int operationsTab,
            int toolsTab,
            int settingsTab) {
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
            int toolsTab,
            int settingsTab) {
        if (requestedTab == operationsTab
                || requestedTab == toolsTab
                || requestedTab == settingsTab) {
            return requestedTab;
        }
        if (persistedTab == toolsTab || persistedTab == settingsTab) {
            return persistedTab;
        }
        return operationsTab;
    }

    static int resolveCreationTab(
            boolean restoring,
            int restoredTab,
            int requestedTab,
            int operationsTab,
            int toolsTab,
            int settingsTab) {
        return normalizeStartTab(
                restoring ? restoredTab : requestedTab,
                requestedTab,
                operationsTab,
                toolsTab,
                settingsTab);
    }
}

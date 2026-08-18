package com.colorvision.xcviewer;

final class AppNavigationPolicy {
    static final String FIXED_SERVICE_ORIGIN = "http://xc213618.ddns.me:9998/";

    private AppNavigationPolicy() {
    }

    static boolean shouldOpenOperationsDirectly(boolean hasOperationsProfile, boolean operationsRequested) {
        return hasOperationsProfile && operationsRequested;
    }

    static int normalizeStartTab(int requestedTab, int persistedTab, int operationsTab, int settingsTab) {
        if (requestedTab == operationsTab || requestedTab == settingsTab) {
            return requestedTab;
        }
        return persistedTab == settingsTab ? settingsTab : operationsTab;
    }
}

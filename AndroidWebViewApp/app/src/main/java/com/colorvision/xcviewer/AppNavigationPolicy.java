package com.colorvision.xcviewer;

final class AppNavigationPolicy {
    static final String FIXED_DOWNLOAD_URL = "http://xc213618.ddns.me:9998/";

    private AppNavigationPolicy() {
    }

    static boolean shouldOpenOperationsDirectly(boolean hasOperationsProfile, boolean operationsRequested) {
        return hasOperationsProfile && operationsRequested;
    }
}

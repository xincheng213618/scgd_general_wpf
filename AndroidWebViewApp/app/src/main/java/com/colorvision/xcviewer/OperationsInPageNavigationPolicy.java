package com.colorvision.xcviewer;

final class OperationsInPageNavigationPolicy {
    private OperationsInPageNavigationPolicy() {
    }

    static boolean shouldReturnToOverview(
            boolean hasOperationsProfile,
            boolean dashboardVisible,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible) {
        return hasOperationsProfile
                && dashboardVisible
                && !showingDashboardSummary
                && !connectionRecoveryVisible;
    }
}

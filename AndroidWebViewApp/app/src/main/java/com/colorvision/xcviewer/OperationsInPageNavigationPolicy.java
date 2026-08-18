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

    static boolean shouldReturnToTriage(
            boolean hasOperationsProfile,
            boolean dashboardVisible,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible,
            boolean detailOpenedFromTriage) {
        return detailOpenedFromTriage && shouldReturnToOverview(
                hasOperationsProfile,
                dashboardVisible,
                showingDashboardSummary,
                connectionRecoveryVisible);
    }
}

package com.colorvision.xcviewer;

final class OperationsConnectionRecoveryPolicy {
    private OperationsConnectionRecoveryPolicy() {
    }

    static boolean shouldSchedule(
            boolean activityResumed,
            boolean dashboardVisible,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible,
            boolean hasOperationsClient) {
        return activityResumed
                && dashboardVisible
                && (showingDashboardSummary || connectionRecoveryVisible)
                && hasOperationsClient;
    }

    static boolean shouldStart(
            boolean activityResumed,
            boolean dashboardVisible,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible,
            boolean hasOperationsClient,
            boolean requestInFlight) {
        return shouldSchedule(
                activityResumed,
                dashboardVisible,
                showingDashboardSummary,
                connectionRecoveryVisible,
                hasOperationsClient)
                && !requestInFlight;
    }
}

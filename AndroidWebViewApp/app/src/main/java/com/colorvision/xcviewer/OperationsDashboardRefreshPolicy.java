package com.colorvision.xcviewer;

final class OperationsDashboardRefreshPolicy {
    enum Decision {
        REJECT,
        JOIN,
        START
    }

    private OperationsDashboardRefreshPolicy() {
    }

    static boolean showsToolbarAction(
            boolean hasOperationsProfile,
            boolean dashboardVisible,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible,
            boolean hasOperationsClient) {
        return hasOperationsProfile
                && dashboardVisible
                && showingDashboardSummary
                && !connectionRecoveryVisible
                && hasOperationsClient;
    }

    static boolean toolbarActionEnabled(
            boolean toolbarActionVisible, boolean manualRefreshInFlight) {
        return toolbarActionVisible && !manualRefreshInFlight;
    }

    static Decision decide(
            boolean activityResumed,
            boolean dashboardVisible,
            boolean showingDashboardSummary,
            boolean hasOperationsProfile,
            boolean hasOperationsClient,
            boolean requestInFlight) {
        if (!activityResumed || !dashboardVisible || !showingDashboardSummary
                || !hasOperationsProfile || !hasOperationsClient) {
            return Decision.REJECT;
        }
        return requestInFlight ? Decision.JOIN : Decision.START;
    }

    static String completionMessage(
            boolean success,
            boolean summaryAvailable,
            boolean relay,
            boolean hostFresh) {
        if (!success) {
            return "刷新失败 · 连接仍不可达";
        }
        if (!summaryAvailable) {
            return "刷新未完成 · 实时摘要不可用";
        }
        if (!relay) {
            return "刷新完成 · 现场直连";
        }
        return hostFresh
                ? "刷新完成 · 电脑在线"
                : "刷新完成 · 电脑仍未上线";
    }
}

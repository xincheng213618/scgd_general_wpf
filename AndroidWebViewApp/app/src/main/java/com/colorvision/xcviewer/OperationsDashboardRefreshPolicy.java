package com.colorvision.xcviewer;

final class OperationsDashboardRefreshPolicy {
    enum Decision {
        REJECT,
        JOIN,
        START
    }

    private OperationsDashboardRefreshPolicy() {
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

    static String completionMessage(boolean success, boolean relay, boolean hostFresh) {
        if (!success) {
            return "刷新失败 · 连接仍不可达";
        }
        if (!relay) {
            return "刷新完成 · 现场直连";
        }
        return hostFresh
                ? "刷新完成 · 电脑在线"
                : "刷新完成 · 电脑仍未上线";
    }
}

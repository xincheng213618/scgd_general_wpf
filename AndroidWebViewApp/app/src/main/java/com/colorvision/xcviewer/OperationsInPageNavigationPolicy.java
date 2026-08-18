package com.colorvision.xcviewer;

final class OperationsInPageNavigationPolicy {
    private static final String NO_PARENT = "";

    private OperationsInPageNavigationPolicy() {
    }

    static String parentDestination(String destination, boolean detailOpenedFromTriage) {
        String normalized = OperationsDestinationState.normalize(destination);
        if (OperationsDestinationState.TRIAGE.equals(normalized) && detailOpenedFromTriage) {
            return OperationsDestinationState.TRIAGE;
        }
        if (OperationsDestinationState.CONNECTION_CHECK.equals(normalized)
                || OperationsDestinationState.FLEET_ALL.equals(normalized)
                || OperationsDestinationState.FLEET_ISSUES.equals(normalized)) {
            return OperationsDestinationState.CONNECTIONS;
        }
        if (OperationsDestinationState.JOBS.equals(normalized) && detailOpenedFromTriage) {
            return OperationsDestinationState.TRIAGE;
        }
        if (OperationsDestinationState.CONNECTIONS.equals(normalized)
                || OperationsDestinationState.HISTORY.equals(normalized)
                || OperationsDestinationState.TRIAGE.equals(normalized)
                || OperationsDestinationState.JOBS.equals(normalized)
                || OperationsDestinationState.SUPPORT.equals(normalized)
                || OperationsDestinationState.LIVE_MONITOR.equals(normalized)) {
            return OperationsDestinationState.OVERVIEW;
        }
        return NO_PARENT;
    }

    static String activeParentDestination(
            String destination,
            boolean detailOpenedFromTriage,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible) {
        String parent = parentDestination(destination, detailOpenedFromTriage);
        if (!parent.isEmpty()) {
            return parent;
        }
        return !showingDashboardSummary && !connectionRecoveryVisible
                ? OperationsDestinationState.OVERVIEW
                : NO_PARENT;
    }

    static boolean showsNavigateUp(
            boolean hasOperationsProfile,
            boolean dashboardVisible,
            String destination,
            boolean detailOpenedFromTriage,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible) {
        return hasOperationsProfile
                && dashboardVisible
                && !activeParentDestination(
                        destination,
                        detailOpenedFromTriage,
                        showingDashboardSummary,
                        connectionRecoveryVisible).isEmpty();
    }

    static String navigateUpLabel(
            String destination,
            boolean detailOpenedFromTriage,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible) {
        String parent = activeParentDestination(
                destination,
                detailOpenedFromTriage,
                showingDashboardSummary,
                connectionRecoveryVisible);
        if (OperationsDestinationState.CONNECTIONS.equals(parent)) {
            return "返回电脑与连接";
        }
        if (OperationsDestinationState.TRIAGE.equals(parent)) {
            return "返回远程排障中心";
        }
        if (OperationsDestinationState.OVERVIEW.equals(parent)) {
            return "返回现场运维概览";
        }
        return "";
    }

    static int motionDirection(
            String fromDestination,
            String toDestination,
            boolean detailOpenedFromTriage) {
        String from = OperationsDestinationState.normalize(fromDestination);
        String to = OperationsDestinationState.normalize(toDestination);
        if (from.equals(to)) {
            return AppScreenMotion.DIRECTION_NONE;
        }
        if (to.equals(parentDestination(from, detailOpenedFromTriage))) {
            return AppScreenMotion.DIRECTION_BACKWARD;
        }
        if (from.equals(parentDestination(to, detailOpenedFromTriage))) {
            return AppScreenMotion.DIRECTION_FORWARD;
        }
        if (OperationsDestinationState.OVERVIEW.equals(to)) {
            return AppScreenMotion.DIRECTION_BACKWARD;
        }
        if (OperationsDestinationState.OVERVIEW.equals(from)) {
            return AppScreenMotion.DIRECTION_FORWARD;
        }
        return AppScreenMotion.DIRECTION_NONE;
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

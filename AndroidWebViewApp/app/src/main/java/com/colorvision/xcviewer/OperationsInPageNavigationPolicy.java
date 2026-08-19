package com.colorvision.xcviewer;

final class OperationsInPageNavigationPolicy {
    private static final String NO_PARENT = "";

    private OperationsInPageNavigationPolicy() {
    }

    static String normalizeConnectionsParent(String destination) {
        String normalized = OperationsDestinationState.normalize(destination);
        if (OperationsDestinationState.TRIAGE.equals(normalized)
                || OperationsDestinationState.TOOLS.equals(normalized)
                || OperationsDestinationState.SETTINGS.equals(normalized)) {
            return normalized;
        }
        return OperationsDestinationState.OVERVIEW;
    }

    static String targetManagementParentDestination(
            String destination,
            boolean detailOpenedFromTriage,
            boolean detailOpenedFromToolbox,
            String existingConnectionsParent) {
        String normalized = OperationsDestinationState.normalize(destination);
        if (OperationsDestinationState.SETTINGS.equals(normalized)) {
            return OperationsDestinationState.SETTINGS;
        }
        if (OperationsDestinationState.TRIAGE.equals(normalized) || detailOpenedFromTriage) {
            return OperationsDestinationState.TRIAGE;
        }
        if (OperationsDestinationState.TOOLS.equals(normalized) || detailOpenedFromToolbox) {
            return OperationsDestinationState.TOOLS;
        }
        if (OperationsDestinationState.CONNECTIONS.equals(normalized)
                || OperationsDestinationState.CONNECTIONS.equals(
                        parentDestination(normalized, false, false))) {
            return normalizeConnectionsParent(existingConnectionsParent);
        }
        return OperationsDestinationState.OVERVIEW;
    }

    static boolean shouldReturnToConnectionsParent(
            String destination, String connectionsParent) {
        return OperationsDestinationState.CONNECTIONS.equals(
                        OperationsDestinationState.normalize(destination))
                && !OperationsDestinationState.OVERVIEW.equals(
                        normalizeConnectionsParent(connectionsParent));
    }

    static String connectionsParentLabel(String connectionsParent) {
        String normalized = normalizeConnectionsParent(connectionsParent);
        if (OperationsDestinationState.SETTINGS.equals(normalized)) {
            return "返回设置";
        }
        if (OperationsDestinationState.TRIAGE.equals(normalized)) {
            return "返回问题中心";
        }
        if (OperationsDestinationState.TOOLS.equals(normalized)) {
            return "返回运维工具";
        }
        return "返回现场运维概览";
    }

    static String parentDestination(
            String destination,
            boolean detailOpenedFromTriage,
            boolean detailOpenedFromToolbox) {
        String normalized = OperationsDestinationState.normalize(destination);
        if (detailOpenedFromTriage
                && !OperationsDestinationState.TRIAGE.equals(normalized)
                && !OperationsDestinationState.OVERVIEW.equals(normalized)
                && !OperationsDestinationState.PAIRING.equals(normalized)) {
            return OperationsDestinationState.TRIAGE;
        }
        if (detailOpenedFromToolbox
                && !OperationsDestinationState.TOOLS.equals(normalized)
                && !OperationsDestinationState.OVERVIEW.equals(normalized)
                && !OperationsDestinationState.PAIRING.equals(normalized)) {
            return OperationsDestinationState.TOOLS;
        }
        if (OperationsDestinationState.CONNECTION_CHECK.equals(normalized)
                || OperationsDestinationState.FLEET_ALL.equals(normalized)
                || OperationsDestinationState.FLEET_ISSUES.equals(normalized)) {
            return OperationsDestinationState.CONNECTIONS;
        }
        if (OperationsDestinationState.CONNECTIONS.equals(normalized)
                || OperationsDestinationState.HISTORY.equals(normalized)
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
            boolean detailOpenedFromToolbox,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible) {
        String parent = parentDestination(
                destination, detailOpenedFromTriage, detailOpenedFromToolbox);
        if (!parent.isEmpty()) {
            return parent;
        }
        String normalized = OperationsDestinationState.normalize(destination);
        if (OperationsDestinationState.TOOLS.equals(normalized)
                || OperationsDestinationState.SETTINGS.equals(normalized)
                || OperationsDestinationState.TRIAGE.equals(normalized)) {
            return NO_PARENT;
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
            boolean detailOpenedFromToolbox,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible) {
        return hasOperationsProfile
                && dashboardVisible
                && !activeParentDestination(
                        destination,
                        detailOpenedFromTriage,
                        detailOpenedFromToolbox,
                        showingDashboardSummary,
                        connectionRecoveryVisible).isEmpty();
    }

    static String navigateUpLabel(
            String destination,
            boolean detailOpenedFromTriage,
            boolean detailOpenedFromToolbox,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible) {
        String parent = activeParentDestination(
                destination,
                detailOpenedFromTriage,
                detailOpenedFromToolbox,
                showingDashboardSummary,
                connectionRecoveryVisible);
        if (OperationsDestinationState.CONNECTIONS.equals(parent)) {
            return "返回电脑与连接";
        }
        if (OperationsDestinationState.TRIAGE.equals(parent)) {
            return "返回问题中心";
        }
        if (OperationsDestinationState.TOOLS.equals(parent)) {
            return "返回运维工具";
        }
        if (OperationsDestinationState.OVERVIEW.equals(parent)) {
            return "返回现场运维概览";
        }
        return "";
    }

    static int motionDirection(
            String fromDestination,
            String toDestination,
            boolean detailOpenedFromTriage,
            boolean detailOpenedFromToolbox,
            String connectionsParent) {
        String from = OperationsDestinationState.normalize(fromDestination);
        String to = OperationsDestinationState.normalize(toDestination);
        if (from.equals(to)) {
            return AppScreenMotion.DIRECTION_NONE;
        }
        String normalizedConnectionsParent = normalizeConnectionsParent(connectionsParent);
        if (OperationsDestinationState.CONNECTIONS.equals(from)
                && normalizedConnectionsParent.equals(to)) {
            return AppScreenMotion.DIRECTION_BACKWARD;
        }
        if (normalizedConnectionsParent.equals(from)
                && OperationsDestinationState.CONNECTIONS.equals(to)) {
            return AppScreenMotion.DIRECTION_FORWARD;
        }
        int topLevelDirection = topLevelMotionDirection(from, to);
        if (topLevelDirection != AppScreenMotion.DIRECTION_NONE) {
            return topLevelDirection;
        }
        if (OperationsDestinationState.SETTINGS.equals(to)) {
            return AppScreenMotion.DIRECTION_FORWARD;
        }
        if (OperationsDestinationState.SETTINGS.equals(from)) {
            return AppScreenMotion.DIRECTION_BACKWARD;
        }
        if (to.equals(parentDestination(
                from, detailOpenedFromTriage, detailOpenedFromToolbox))) {
            return AppScreenMotion.DIRECTION_BACKWARD;
        }
        if (from.equals(parentDestination(
                to, detailOpenedFromTriage, detailOpenedFromToolbox))) {
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

    static boolean isTopLevelTransition(String fromDestination, String toDestination) {
        String from = OperationsDestinationState.normalize(fromDestination);
        String to = OperationsDestinationState.normalize(toDestination);
        return !from.equals(to) && topLevelIndex(from) >= 0 && topLevelIndex(to) >= 0;
    }

    private static int topLevelMotionDirection(String from, String to) {
        int fromIndex = topLevelIndex(from);
        int toIndex = topLevelIndex(to);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex) {
            return AppScreenMotion.DIRECTION_NONE;
        }
        return fromIndex < toIndex
                ? AppScreenMotion.DIRECTION_FORWARD
                : AppScreenMotion.DIRECTION_BACKWARD;
    }

    private static int topLevelIndex(String destination) {
        if (OperationsDestinationState.OVERVIEW.equals(destination)) {
            return 0;
        }
        if (OperationsDestinationState.TRIAGE.equals(destination)) {
            return 1;
        }
        if (OperationsDestinationState.TOOLS.equals(destination)) {
            return 2;
        }
        return OperationsDestinationState.SETTINGS.equals(destination) ? 3 : -1;
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

    static boolean shouldReturnToStartDestination(String destination) {
        String normalized = OperationsDestinationState.normalize(destination);
        return OperationsDestinationState.TRIAGE.equals(normalized)
                || OperationsDestinationState.TOOLS.equals(normalized)
                || OperationsDestinationState.SETTINGS.equals(normalized);
    }

}

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

    static String normalizeDetailParent(String destination) {
        String normalized = OperationsDestinationState.normalize(destination);
        if (OperationsDestinationState.TRIAGE.equals(normalized)
                || OperationsDestinationState.TOOLS.equals(normalized)
                || OperationsDestinationState.SETTINGS.equals(normalized)) {
            return normalized;
        }
        return OperationsDestinationState.OVERVIEW;
    }

    static String restoreDetailParent(
            String savedParent,
            boolean legacyTriageParent,
            boolean legacyToolboxParent) {
        if (savedParent != null && !savedParent.trim().isEmpty()) {
            return normalizeDetailParent(savedParent);
        }
        if (legacyTriageParent) {
            return OperationsDestinationState.TRIAGE;
        }
        if (legacyToolboxParent) {
            return OperationsDestinationState.TOOLS;
        }
        return OperationsDestinationState.OVERVIEW;
    }

    static String targetManagementParentDestination(
            String destination,
            String detailParent,
            String existingConnectionsParent) {
        String normalized = OperationsDestinationState.normalize(destination);
        String normalizedDetailParent = normalizeDetailParent(detailParent);
        if (OperationsDestinationState.SETTINGS.equals(normalized)) {
            return OperationsDestinationState.SETTINGS;
        }
        if (OperationsDestinationState.TRIAGE.equals(normalized)
                || OperationsDestinationState.TRIAGE.equals(normalizedDetailParent)) {
            return OperationsDestinationState.TRIAGE;
        }
        if (OperationsDestinationState.TOOLS.equals(normalized)
                || OperationsDestinationState.TOOLS.equals(normalizedDetailParent)) {
            return OperationsDestinationState.TOOLS;
        }
        if (OperationsDestinationState.CONNECTIONS.equals(normalized)
                || OperationsDestinationState.CONNECTIONS.equals(
                        parentDestination(
                                normalized, OperationsDestinationState.OVERVIEW))) {
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
            String detailParent) {
        String normalized = OperationsDestinationState.normalize(destination);
        String normalizedDetailParent = normalizeDetailParent(detailParent);
        if (!OperationsDestinationState.OVERVIEW.equals(normalizedDetailParent)
                && !normalizedDetailParent.equals(normalized)
                && !OperationsDestinationState.OVERVIEW.equals(normalized)
                && !OperationsDestinationState.PAIRING.equals(normalized)) {
            return normalizedDetailParent;
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
            String detailParent,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible) {
        String parent = parentDestination(destination, detailParent);
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
            String detailParent,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible) {
        return hasOperationsProfile
                && dashboardVisible
                && !activeParentDestination(
                        destination,
                        detailParent,
                        showingDashboardSummary,
                        connectionRecoveryVisible).isEmpty();
    }

    static String navigateUpLabel(
            String destination,
            String detailParent,
            boolean showingDashboardSummary,
            boolean connectionRecoveryVisible) {
        String parent = activeParentDestination(
                destination,
                detailParent,
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
        if (OperationsDestinationState.SETTINGS.equals(parent)) {
            return "返回设置";
        }
        if (OperationsDestinationState.OVERVIEW.equals(parent)) {
            return "返回现场运维概览";
        }
        return "";
    }

    static int motionDirection(
            String fromDestination,
            String toDestination,
            String detailParent,
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
        if (to.equals(parentDestination(from, detailParent))) {
            return AppScreenMotion.DIRECTION_BACKWARD;
        }
        if (from.equals(parentDestination(to, detailParent))) {
            return AppScreenMotion.DIRECTION_FORWARD;
        }
        if (OperationsDestinationState.SETTINGS.equals(to)) {
            return AppScreenMotion.DIRECTION_FORWARD;
        }
        if (OperationsDestinationState.SETTINGS.equals(from)) {
            return AppScreenMotion.DIRECTION_BACKWARD;
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
            String detailParent) {
        return OperationsDestinationState.TRIAGE.equals(
                        normalizeDetailParent(detailParent))
                && shouldReturnToOverview(
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

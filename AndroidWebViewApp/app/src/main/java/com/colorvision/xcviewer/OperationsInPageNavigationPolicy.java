package com.colorvision.xcviewer;

final class OperationsInPageNavigationPolicy {
    private static final String NO_PARENT = "";

    private OperationsInPageNavigationPolicy() {
    }

    static String parentDestination(
            String destination,
            boolean detailOpenedFromTriage,
            boolean detailOpenedFromToolbox) {
        String normalized = OperationsDestinationState.normalize(destination);
        if (detailOpenedFromTriage
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
            boolean detailOpenedFromSettings) {
        String from = OperationsDestinationState.normalize(fromDestination);
        String to = OperationsDestinationState.normalize(toDestination);
        if (from.equals(to)) {
            return AppScreenMotion.DIRECTION_NONE;
        }
        if (detailOpenedFromSettings
                && OperationsDestinationState.SETTINGS.equals(to)) {
            return AppScreenMotion.DIRECTION_BACKWARD;
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

    static boolean shouldReturnToStartDestination(
            String destination,
            boolean detailOpenedFromTriage,
            boolean detailOpenedFromToolbox) {
        String normalized = OperationsDestinationState.normalize(destination);
        return (OperationsDestinationState.TRIAGE.equals(normalized)
                        && !detailOpenedFromTriage)
                || (OperationsDestinationState.TOOLS.equals(normalized)
                        && !detailOpenedFromToolbox)
                || OperationsDestinationState.SETTINGS.equals(normalized);
    }

    static boolean shouldReturnToSettings(
            String destination, boolean openedFromSettings) {
        return openedFromSettings
                && OperationsDestinationState.CONNECTIONS.equals(
                        OperationsDestinationState.normalize(destination));
    }
}

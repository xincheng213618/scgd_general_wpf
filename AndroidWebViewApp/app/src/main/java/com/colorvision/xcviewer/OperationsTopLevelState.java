package com.colorvision.xcviewer;

final class OperationsTopLevelState {
    private int overviewScrollY;
    private int problemScrollY;
    private int toolsScrollY;
    private int settingsScrollY;

    void rememberScroll(String destination, int scrollY) {
        int boundedScrollY = Math.max(0, scrollY);
        switch (value(destination)) {
            case OperationsDestinationState.OVERVIEW:
                overviewScrollY = boundedScrollY;
                return;
            case OperationsDestinationState.TRIAGE:
                problemScrollY = boundedScrollY;
                return;
            case OperationsDestinationState.TOOLS:
                toolsScrollY = boundedScrollY;
                return;
            case OperationsDestinationState.SETTINGS:
                settingsScrollY = boundedScrollY;
                return;
            default:
                return;
        }
    }

    int scrollY(String destination) {
        switch (value(destination)) {
            case OperationsDestinationState.OVERVIEW:
                return overviewScrollY;
            case OperationsDestinationState.TRIAGE:
                return problemScrollY;
            case OperationsDestinationState.TOOLS:
                return toolsScrollY;
            case OperationsDestinationState.SETTINGS:
                return settingsScrollY;
            default:
                return 0;
        }
    }

    void resetScroll(String destination) {
        rememberScroll(destination, 0);
    }

    static boolean isDashboardTopLevel(String destination) {
        String value = value(destination);
        return OperationsDestinationState.OVERVIEW.equals(value)
                || OperationsDestinationState.TRIAGE.equals(value)
                || OperationsDestinationState.TOOLS.equals(value);
    }

    static boolean isTopLevel(String destination) {
        return isDashboardTopLevel(destination)
                || OperationsDestinationState.SETTINGS.equals(
                        value(destination));
    }

    private static String value(String destination) {
        return destination == null ? "" : destination.trim();
    }
}

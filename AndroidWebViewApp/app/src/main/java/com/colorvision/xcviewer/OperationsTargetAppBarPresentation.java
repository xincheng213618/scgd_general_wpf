package com.colorvision.xcviewer;

final class OperationsTargetAppBarPresentation {
    private OperationsTargetAppBarPresentation() {
    }

    static ViewModel from(
            boolean paired,
            boolean dashboardVisible,
            String destination,
            String activeProfileLabel) {
        boolean visible = paired
                && dashboardVisible
                && !OperationsDestinationState.CONNECTIONS.equals(destination);
        String subtitle = visible ? safe(activeProfileLabel) : "";
        return new ViewModel(
                visible,
                subtitle,
                visible ? "当前操作电脑：" + subtitle + "，点按管理或切换电脑" : "");
    }

    private static String safe(String value) {
        return value == null || value.trim().isEmpty() ? "未命名电脑" : value.trim();
    }

    static final class ViewModel {
        final boolean visible;
        final String subtitle;
        final String actionLabel;

        ViewModel(boolean visible, String subtitle, String actionLabel) {
            this.visible = visible;
            this.subtitle = subtitle;
            this.actionLabel = actionLabel;
        }
    }
}

package com.colorvision.xcviewer;

final class OperationsLiveMonitorControlsPresentation {
    private OperationsLiveMonitorControlsPresentation() {
    }

    static ViewModel from(
            boolean autoRefresh,
            boolean refreshInFlight,
            boolean cancelInFlight) {
        return new ViewModel(
                autoRefresh,
                !cancelInFlight,
                !refreshInFlight && !cancelInFlight,
                autoRefresh
                        ? "前台每 10 秒更新；进入后台时暂停"
                        : "当前快照与本次样本仍保留",
                refreshInFlight ? "正在刷新…" : "立即刷新",
                refreshInFlight
                        ? "正在采集新的脱敏聚合快照"
                        : "采集一份新的脱敏聚合快照");
    }

    static final class ViewModel {
        final boolean autoRefresh;
        final boolean toggleEnabled;
        final boolean refreshEnabled;
        final String autoRefreshSummary;
        final String refreshLabel;
        final String refreshSummary;

        ViewModel(
                boolean autoRefresh,
                boolean toggleEnabled,
                boolean refreshEnabled,
                String autoRefreshSummary,
                String refreshLabel,
                String refreshSummary) {
            this.autoRefresh = autoRefresh;
            this.toggleEnabled = toggleEnabled;
            this.refreshEnabled = refreshEnabled;
            this.autoRefreshSummary = autoRefreshSummary;
            this.refreshLabel = refreshLabel;
            this.refreshSummary = refreshSummary;
        }

        String autoRefreshAccessibilityLabel() {
            return "自动观察，" + (autoRefresh ? "已开启，" : "已暂停，")
                    + autoRefreshSummary;
        }

        String refreshAccessibilityLabel() {
            return refreshLabel + "，" + refreshSummary;
        }
    }
}

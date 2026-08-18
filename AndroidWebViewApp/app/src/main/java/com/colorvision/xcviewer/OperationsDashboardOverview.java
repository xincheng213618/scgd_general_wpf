package com.colorvision.xcviewer;

final class OperationsDashboardOverview {
    private static final long MINUTE_MILLISECONDS = 60_000L;
    private static final long HOUR_MILLISECONDS = 60L * MINUTE_MILLISECONDS;
    private static final long DAY_MILLISECONDS = 24L * HOUR_MILLISECONDS;

    private OperationsDashboardOverview() {
    }

    static String directConnectionState(boolean relayPreferred) {
        return relayPreferred
                ? "现场直连（临时） · 中继恢复后自动切回"
                : "现场直连 · 自动保持";
    }

    static String remoteConnectionState(boolean hostFresh, boolean relayPreferred) {
        if (!hostFresh) {
            return "固定中继在线 · 电脑未上线";
        }
        return relayPreferred
                ? "固定中继 · 自动保持"
                : "固定中继（临时） · 直连恢复后自动切回";
    }

    static String remoteSummary(
            boolean hostFresh,
            boolean applicationRunning,
            boolean windowExists,
            boolean windowVisible,
            boolean monitorAvailable,
            long signedAtSeconds,
            long nowMilliseconds) {
        String application = applicationRunning ? "运行中" : "暂未确认";
        String window = !windowExists ? "暂未确认"
                : windowVisible ? "可见" : "隐藏或最小化";
        String updated = relativeUpdate(signedAtSeconds, nowMilliseconds);
        if (hostFresh) {
            return "ColorVision：" + application + " · 主窗口" + window
                    + "\n更新：" + updated
                    + (monitorAvailable ? "" : " · 详细状态待更新");
        }
        return "上次状态：ColorVision " + application + " · 主窗口" + window
                + "\n更新：" + updated + "（已过期）";
    }

    private static String relativeUpdate(long signedAtSeconds, long nowMilliseconds) {
        if (signedAtSeconds <= 0L) {
            return "等待电脑更新";
        }
        long timestamp = signedAtSeconds * 1_000L;
        long age = timestamp >= nowMilliseconds ? 0L : nowMilliseconds - timestamp;
        if (age < MINUTE_MILLISECONDS) {
            return "刚刚";
        }
        if (age < HOUR_MILLISECONDS) {
            return Math.max(1L, age / MINUTE_MILLISECONDS) + " 分钟前";
        }
        if (age < DAY_MILLISECONDS) {
            return Math.max(1L, age / HOUR_MILLISECONDS) + " 小时前";
        }
        if (age < 7L * DAY_MILLISECONDS) {
            return Math.max(1L, age / DAY_MILLISECONDS) + " 天前";
        }
        return "超过 7 天";
    }
}

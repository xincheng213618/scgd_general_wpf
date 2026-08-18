package com.colorvision.xcviewer;

import org.json.JSONObject;

final class OperationsDashboardAdvisor {
    static final String ACTION_NONE = "none";
    static final String ACTION_CONNECTION_CHECK = "connection_check";
    static final String ACTION_FLOW = "flow";
    static final String ACTION_DEVICES = "devices";
    static final String ACTION_MESSAGE = "message";
    static final String ACTION_ALERTS = "alerts";
    static final String ACTION_PERFORMANCE = "performance";
    static final String ACTION_MONITOR = "monitor";
    static final String ACTION_NOTIFICATION_SETTINGS = "notification_settings";

    private OperationsDashboardAdvisor() {
    }

    static Recommendation waiting() {
        return new Recommendation("正在分析运行状态…", ACTION_NONE);
    }

    static Recommendation staleRemoteSnapshot() {
        return new Recommendation("电脑未上线 · 运行连接自检", ACTION_CONNECTION_CHECK);
    }

    static Recommendation unavailable() {
        return new Recommendation("实时状态暂不可用 · 运行连接自检", ACTION_CONNECTION_CHECK);
    }

    static Recommendation fromMonitor(JSONObject monitor) {
        return fromMonitor(monitor, true);
    }

    static Recommendation fromMonitor(JSONObject monitor, boolean remindersAvailable) {
        if (monitor == null) {
            return unavailable();
        }

        JSONObject flow = monitor.optJSONObject("flow");
        JSONObject devices = monitor.optJSONObject("devices");
        JSONObject messageChannel = monitor.optJSONObject("messageChannel");
        JSONObject alerts = monitor.optJSONObject("alerts");
        JSONObject performance = monitor.optJSONObject("performance");
        JSONObject mainUi = performance == null ? null : performance.optJSONObject("mainUi");

        String uiState = mainUi == null ? "unavailable"
                : mainUi.optString("state", "unavailable");
        int criticalCount = count(alerts, "criticalCount");
        int errorCount = count(alerts, "errorCount");
        int warningCount = count(alerts, "warningCount");
        int deviceAttentionCount = count(devices, "attentionCount");
        boolean messageAttention = messageChannel != null
                && messageChannel.optBoolean("available", false)
                && messageChannel.optBoolean("attentionRequired", false);

        String attentionKey = OperationsWatchPolicy.attentionKey(
                uiState, criticalCount, errorCount, deviceAttentionCount, messageAttention);
        switch (attentionKey) {
            case OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE:
                return new Recommendation("主界面无响应 · 查看性能", ACTION_PERFORMANCE);
            case OperationsWatchPolicy.ATTENTION_CRITICAL:
                return new Recommendation("严重告警 " + criticalCount + " 个 · 查看告警", ACTION_ALERTS);
            case OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL:
                return new Recommendation("消息通道需处置 · 查看消息", ACTION_MESSAGE);
            case OperationsWatchPolicy.ATTENTION_DEVICES:
                return new Recommendation(
                        "设备需关注 " + deviceAttentionCount + " 个 · 查看设备", ACTION_DEVICES);
            case OperationsWatchPolicy.ATTENTION_ERRORS:
                return new Recommendation("错误事件 " + errorCount + " 个 · 查看告警", ACTION_ALERTS);
            default:
                break;
        }

        if ("slow".equals(uiState)) {
            return new Recommendation("主界面响应偏慢 · 查看性能", ACTION_PERFORMANCE);
        }
        if (warningCount > 0) {
            return new Recommendation("警告 " + warningCount + " 个 · 查看告警", ACTION_ALERTS);
        }
        if (!isAvailable(flow) || !isAvailable(devices) || !isAvailable(messageChannel)
                || performance == null || alerts == null) {
            return new Recommendation("部分状态暂不可用 · 查看状态", ACTION_MONITOR);
        }
        if (flow != null
                && flow.optBoolean("isActive", false)) {
            return new Recommendation("检测运行中 · 查看进度", ACTION_FLOW);
        }
        if (!remindersAvailable) {
            return new Recommendation(
                    "运维提醒未开启 · 前往设置", ACTION_NOTIFICATION_SETTINGS);
        }
        return new Recommendation("当前运行稳定 · 查看状态", ACTION_MONITOR);
    }

    private static int count(JSONObject value, String name) {
        return value == null ? 0 : Math.max(0, Math.min(999, value.optInt(name, 0)));
    }

    private static boolean isAvailable(JSONObject value) {
        return value != null && value.optBoolean("available", false);
    }

    static final class Recommendation {
        final String label;
        final String action;

        Recommendation(String label, String action) {
            this.label = label;
            this.action = action;
        }
    }
}

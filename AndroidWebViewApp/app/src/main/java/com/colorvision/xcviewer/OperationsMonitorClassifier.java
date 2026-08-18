package com.colorvision.xcviewer;

import org.json.JSONObject;

final class OperationsMonitorClassifier {
    private OperationsMonitorClassifier() {
    }

    static String watchState(JSONObject monitor, String healthyState) {
        if (monitor == null) {
            return OperationsWatchHistory.STATE_OFFLINE;
        }
        String attentionState = OperationsWatchHistory.attentionState(attentionKey(monitor));
        if (!attentionState.isEmpty()) {
            return attentionState;
        }
        if (OperationsWatchHistory.STATE_ONLINE.equals(healthyState)
                || OperationsWatchHistory.STATE_REMOTE_ONLINE.equals(healthyState)) {
            return healthyState;
        }
        return OperationsWatchHistory.STATE_OFFLINE;
    }

    static String status(JSONObject monitor) {
        JSONObject safeMonitor = monitor == null ? new JSONObject() : monitor;
        JSONObject flow = safeMonitor.optJSONObject("flow");
        JSONObject performance = safeMonitor.optJSONObject("performance");
        JSONObject mainUi = performance == null ? null : performance.optJSONObject("mainUi");
        JSONObject alerts = safeMonitor.optJSONObject("alerts");
        JSONObject devices = safeMonitor.optJSONObject("devices");
        JSONObject messageChannel = safeMonitor.optJSONObject("messageChannel");
        return OperationsWatchPolicy.healthyStatus(
                mainUi == null ? "unavailable" : mainUi.optString("state", "unavailable"),
                flow != null && flow.optBoolean("isActive", false),
                alerts == null ? 0 : alerts.optInt("criticalCount", 0),
                alerts == null ? 0 : alerts.optInt("errorCount", 0),
                devices == null || !devices.optBoolean("available", false)
                        ? 0 : devices.optInt("attentionCount", 0),
                messageChannel != null
                        && messageChannel.optBoolean("available", false)
                        && messageChannel.optBoolean("attentionRequired", false));
    }

    static String attentionKey(JSONObject monitor) {
        JSONObject safeMonitor = monitor == null ? new JSONObject() : monitor;
        JSONObject performance = safeMonitor.optJSONObject("performance");
        JSONObject mainUi = performance == null ? null : performance.optJSONObject("mainUi");
        JSONObject alerts = safeMonitor.optJSONObject("alerts");
        JSONObject devices = safeMonitor.optJSONObject("devices");
        JSONObject messageChannel = safeMonitor.optJSONObject("messageChannel");
        return OperationsWatchPolicy.attentionKey(
                mainUi == null ? "unavailable" : mainUi.optString("state", "unavailable"),
                alerts == null ? 0 : alerts.optInt("criticalCount", 0),
                alerts == null ? 0 : alerts.optInt("errorCount", 0),
                devices == null || !devices.optBoolean("available", false)
                        ? 0 : devices.optInt("attentionCount", 0),
                messageChannel != null
                        && messageChannel.optBoolean("available", false)
                        && messageChannel.optBoolean("attentionRequired", false));
    }
}

package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;

final class OperationsAlertPresentation {
    private OperationsAlertPresentation() {
    }

    static String safeSource(String source) {
        if (source == null) {
            return "";
        }
        switch (source.trim()) {
            case "安全运维":
            case "消息服务":
            case "设备与图像":
            case "流程":
            case "更新与下载":
            case "Copilot":
            case "服务":
            case "应用":
                return source.trim();
            default:
                return "";
        }
    }

    static String primarySourceFromDetails(JSONObject summary, JSONObject response) {
        if (summary == null || response == null) {
            return "";
        }
        String targetSeverity = summary.optInt("criticalCount", 0) > 0
                ? "critical"
                : summary.optInt("errorCount", 0) > 0 ? "error" : "warning";
        JSONObject data = response.optJSONObject("data");
        JSONObject payload = data == null ? response : data;
        JSONArray alerts = payload.optJSONArray("alerts");
        if (alerts == null) {
            return "";
        }
        for (int index = 0; index < alerts.length(); index++) {
            JSONObject alert = alerts.optJSONObject(index);
            if (alert == null || !targetSeverity.equals(alert.optString("severity", ""))) {
                continue;
            }
            String source = safeSource(alert.optString("source", ""));
            if (!source.isEmpty()) {
                return source;
            }
        }
        return "";
    }
}

package com.colorvision.xcviewer;

import org.json.JSONObject;

import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Date;
import java.util.List;
import java.util.Locale;

final class OperationsLiveMonitorPresentation {
    private static final int DEFAULT_REFRESH_SECONDS = 10;

    private OperationsLiveMonitorPresentation() {
    }

    static ViewModel from(
            JSONObject snapshot,
            OperationsLiveMonitorTrend.Summary trend,
            boolean autoRefresh,
            String capturedAtLabel) {
        OperationsLiveMonitorTrend.Summary currentTrend = trend == null
                ? OperationsLiveMonitorTrend.Summary.empty() : trend;
        int refreshSeconds = snapshot == null
                ? DEFAULT_REFRESH_SECONDS
                : Math.max(1, Math.min(300,
                        snapshot.optInt("suggestedRefreshSeconds", DEFAULT_REFRESH_SECONDS)));
        String refreshMode = autoRefresh
                ? "自动观察每 " + refreshSeconds + " 秒"
                : "自动观察已暂停";
        String captured = capturedAtLabel == null || capturedAtLabel.trim().isEmpty()
                ? "最近采集时间未知" : "最近采集 " + capturedAtLabel.trim();
        String overview = refreshMode
                + " · 本次样本 " + currentTrend.sampleCount + "/"
                + OperationsLiveMonitorTrend.MAX_SAMPLES
                + "\n" + captured + " · 后台守护每 60 秒";

        List<OperationsDashboardStatusFormatter.Item> statuses = new ArrayList<>();
        JSONObject flow = child(snapshot, "flow");
        statuses.add(OperationsDashboardStatusFormatter.flow(
                flow != null && flow.optBoolean("available", false),
                flow != null && flow.optBoolean("isActive", false),
                flow == null ? "unavailable" : flow.optString("phase", "unavailable")));

        JSONObject devices = child(snapshot, "devices");
        DeviceHealthPresentation.ViewModel deviceHealth =
                DeviceHealthPresentation.from(devices);
        statuses.add(OperationsDashboardStatusFormatter.devices(
                devices != null && devices.optBoolean("available", false),
                devices != null && devices.optBoolean("hasConfiguredDevices", false),
                count(devices, "readyCount"),
                count(devices, "busyCount"),
                count(devices, "attentionCount"),
                count(devices, "totalCount"),
                deviceHealth.compactAttentionSummary()));

        JSONObject message = child(snapshot, "messageChannel");
        statuses.add(OperationsDashboardStatusFormatter.messageChannel(
                message != null && message.optBoolean("available", false),
                message != null && message.optBoolean("connected", false),
                message != null && message.optBoolean("subscriptionReady", false),
                count(message, "activeSubscriptionCount"),
                count(message, "registeredSubscriptionCount")));

        JSONObject performance = child(snapshot, "performance");
        JSONObject mainUi = child(performance, "mainUi");
        statuses.add(OperationsDashboardStatusFormatter.performance(
                performance != null,
                performance == null ? 0 : performance.optDouble("cpuPercent", 0),
                mainUi == null ? "unavailable"
                        : mainUi.optString("state", "unavailable")));

        JSONObject alerts = child(snapshot, "alerts");
        statuses.add(OperationsDashboardStatusFormatter.alerts(
                alerts != null,
                count(alerts, "warningCount"),
                count(alerts, "errorCount"),
                count(alerts, "criticalCount"),
                alerts == null ? "" : alerts.optString("primarySource", "")));

        JSONObject recovery = child(snapshot, "applicationRecovery");
        statuses.add(OperationsDashboardStatusFormatter.recovery(
                recovery != null,
                recovery != null && recovery.optBoolean("supported", false),
                recovery != null && recovery.optBoolean("registered", false),
                recovery != null && recovery.optBoolean("automaticWatchdogActive", false)));

        return new ViewModel(
                overview,
                "当前电脑的脱敏聚合快照；异常状态会明确标为需关注。",
                Collections.unmodifiableList(statuses),
                trendSummary(currentTrend),
                currentTrend.sampleCount < 2
                        ? OperationsDashboardStatusFormatter.TONE_MUTED
                        : trendTone(currentTrend),
                "仅在手机内存保留最近 30 个脱敏样本，离开本页即清空；服务器不保存采样历史。");
    }

    private static JSONObject child(JSONObject parent, String name) {
        return parent == null ? null : parent.optJSONObject(name);
    }

    private static int count(JSONObject value, String name) {
        return value == null ? 0 : Math.max(0, value.optInt(name, 0));
    }

    static int attentionAlertCount(JSONObject alerts) {
        return count(alerts, "warningCount")
                + count(alerts, "errorCount")
                + count(alerts, "criticalCount");
    }

    private static String trendSummary(OperationsLiveMonitorTrend.Summary summary) {
        if (summary.sampleCount < 2) {
            String recovery = deviceRecoverySummary(summary);
            if (!recovery.isEmpty()) {
                return recovery;
            }
            return "再采集 1 个样本后显示本次趋势";
        }
        String latency = summary.maximumUiLatencyMilliseconds == null
                ? "界面延迟未知"
                : "界面最大延迟 " + summary.maximumUiLatencyMilliseconds + " ms";
        String base = String.format(
                Locale.CHINA,
                "%d 个样本 · %s–%s · %s\n"
                        + "CPU 平均 %.1f%% / 峰值 %.1f%%\n"
                        + "工作集 %.1f–%.1f MB · %s\n"
                        + "界面偏慢 %d 次 · 超时 %d 次\n"
                        + "检测阶段 %s · 切换 %d 次 · 告警最高 %d",
                summary.sampleCount,
                clock(summary.startedAtMilliseconds),
                clock(summary.endedAtMilliseconds),
                elapsed(summary.endedAtMilliseconds - summary.startedAtMilliseconds),
                summary.averageCpuPercent,
                summary.maximumCpuPercent,
                summary.minimumWorkingSetMb,
                summary.maximumWorkingSetMb,
                latency,
                summary.slowUiSampleCount,
                summary.unresponsiveUiSampleCount,
                flowPhase(summary.latestFlowPhase),
                summary.flowPhaseTransitionCount,
                summary.maximumAlertCount);
        String recovery = deviceRecoverySummary(summary);
        return recovery.isEmpty() ? base : base + "\n" + recovery;
    }

    static String deviceRecoverySummary(OperationsLiveMonitorTrend.Summary summary) {
        if (summary == null || !summary.deviceRecoveryTracked) {
            return "";
        }
        if (!summary.latestDeviceHealthAvailable) {
            return "设备恢复暂无法确认 · 当前状态不可用";
        }
        if (summary.latestDeviceAttentionCount > 0) {
            return "设备恢复待确认 · 当前仍有 "
                    + summary.latestDeviceAttentionCount + " 台需关注";
        }
        if (summary.deviceRecoveryConfirmed()) {
            return "设备恢复已确认 · 连续 "
                    + summary.consecutiveHealthyDeviceSamples
                    + " 个样本正常（开始需关注 "
                    + summary.initialDeviceAttentionCount + " 台）";
        }
        return "设备状态暂时正常 · 再采集 1 个正常样本后确认恢复";
    }

    private static String clock(long milliseconds) {
        if (milliseconds <= 0) {
            return "时间未知";
        }
        return new SimpleDateFormat("HH:mm:ss", Locale.CHINA).format(new Date(milliseconds));
    }

    private static String elapsed(long milliseconds) {
        long seconds = Math.max(0, milliseconds) / 1_000;
        long minutes = seconds / 60;
        long remainder = seconds % 60;
        return minutes > 0 ? minutes + " 分 " + remainder + " 秒" : remainder + " 秒";
    }

    private static String flowPhase(String value) {
        if ("preparing".equals(value)) {
            return "准备中";
        }
        if ("running".equals(value)) {
            return "执行中";
        }
        if ("finalizing".equals(value)) {
            return "收尾中";
        }
        if ("idle".equals(value)) {
            return "空闲";
        }
        return "未知";
    }

    private static int trendTone(OperationsLiveMonitorTrend.Summary summary) {
        return summary.unresponsiveUiSampleCount > 0
                || summary.slowUiSampleCount > 0
                || summary.maximumAlertCount > 0
                ? OperationsDashboardStatusFormatter.TONE_ATTENTION
                : OperationsDashboardStatusFormatter.TONE_DEFAULT;
    }

    static final class ViewModel {
        final String overview;
        final String statusCaption;
        final List<OperationsDashboardStatusFormatter.Item> statuses;
        final String trendSummary;
        final int trendTone;
        final String privacyNote;

        ViewModel(
                String overview,
                String statusCaption,
                List<OperationsDashboardStatusFormatter.Item> statuses,
                String trendSummary,
                int trendTone,
                String privacyNote) {
            this.overview = overview;
            this.statusCaption = statusCaption;
            this.statuses = statuses;
            this.trendSummary = trendSummary;
            this.trendTone = trendTone;
            this.privacyNote = privacyNote;
        }
    }
}

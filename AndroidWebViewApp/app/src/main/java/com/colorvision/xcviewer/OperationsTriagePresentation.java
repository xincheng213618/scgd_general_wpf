package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

final class OperationsTriagePresentation {
    static final int TONE_NORMAL = 0;
    static final int TONE_ATTENTION = 1;
    static final int TONE_ERROR = 2;
    static final int TONE_MUTED = 3;

    private OperationsTriagePresentation() {
    }

    static ViewModel from(JSONObject report, TimeFormatter timeFormatter) {
        String state = report.optString("state", "attention");
        int criticalCount = report.optInt("criticalCount", 0);
        int errorCount = report.optInt("errorCount", 0);
        int warningCount = report.optInt("warningCount", 0);
        int pendingJobCount = report.optInt("pendingJobCount", 0);
        int activeSubscriptions = report.optInt("messageChannelActiveSubscriptionCount", 0);
        int registeredSubscriptions = report.optInt("messageChannelRegisteredSubscriptionCount", 0);
        String messageState = report.optString("messageChannelState", "unavailable");
        int deviceAttentionCount = report.optInt("deviceAttentionCount", 0);
        int deviceTotalCount = report.optInt("deviceTotalCount", 0);

        List<Metric> metrics = new ArrayList<>();
        metrics.add(new Metric(
                "近期事件",
                "严重 " + criticalCount + " · 错误 " + errorCount + " · 警告 " + warningCount,
                criticalCount > 0 || errorCount > 0
                        ? TONE_ERROR : warningCount > 0 ? TONE_ATTENTION : TONE_NORMAL));
        metrics.add(new Metric(
                "待处理作业",
                pendingJobCount == 0 ? "当前没有待处理作业" : pendingJobCount + " 个等待处理",
                pendingJobCount > 0 ? TONE_ATTENTION : TONE_NORMAL));
        metrics.add(new Metric(
                "消息通道",
                messageChannelLabel(messageState)
                        + " · 订阅 " + activeSubscriptions + '/' + registeredSubscriptions,
                messageTone(messageState, activeSubscriptions, registeredSubscriptions)));
        metrics.add(new Metric(
                "检测设备",
                deviceSummary(report),
                deviceAttentionCount > 0 ? TONE_ATTENTION
                        : deviceTotalCount == 0 ? TONE_MUTED : TONE_NORMAL));

        List<Finding> findings = new ArrayList<>();
        JSONArray sourceFindings = report.optJSONArray("findings");
        if (sourceFindings != null) {
            for (int index = 0; index < sourceFindings.length(); index++) {
                JSONObject source = sourceFindings.optJSONObject(index);
                if (source == null) {
                    continue;
                }
                String severity = source.optString("severity", "info");
                List<Action> actions = new ArrayList<>();
                JSONArray sourceActions = source.optJSONArray("actions");
                if (sourceActions != null) {
                    for (int actionIndex = 0; actionIndex < sourceActions.length(); actionIndex++) {
                        JSONObject action = sourceActions.optJSONObject(actionIndex);
                        if (action == null) {
                            continue;
                        }
                        actions.add(new Action(
                                action.optString("actionId", ""),
                                action.optString("title", "查看详情"),
                                action.optString("description", ""),
                                action.optString("riskLevel", "read-only"),
                                action.optBoolean("requiresConfirmation", false),
                                action.optBoolean("requiresLocalCoSign", false)));
                    }
                }
                String latestAt = timeFormatter.format(source.optString("latestAt", ""));
                findings.add(new Finding(
                        severity,
                        severityLabel(severity),
                        categoryLabel(source.optString("category", "")),
                        source.optString("title", "需要关注"),
                        source.optString("summary", ""),
                        Math.max(0, source.optInt("evidenceCount", 0)),
                        latestAt,
                        Collections.unmodifiableList(actions)));
            }
        }

        String summary = report.optString("summary", findings.isEmpty()
                ? "当前有界证据中没有需要处理的项目。" : "排障建议已生成。");
        return new ViewModel(
                stateLabel(state),
                summary,
                stateTone(state),
                Collections.unmodifiableList(metrics),
                Collections.unmodifiableList(findings),
                report.optString("safetyNotice",
                        "建议仅引用有界脱敏摘要；远程恢复或取证动作仍需明确确认。"));
    }

    static boolean isSupportedAction(String actionId) {
        switch (actionId) {
            case "triage.events.view":
            case "triage.window.show":
            case "triage.jobs.review":
            case "triage.mqtt.restart.request":
            case "triage.devices.view":
            case "triage.messaging.view":
            case "triage.messaging.reconnect.request":
            case "triage.failures.view":
                return true;
            default:
                return false;
        }
    }

    private static String deviceSummary(JSONObject report) {
        int total = report.optInt("deviceTotalCount", 0);
        if (total <= 0) {
            return "当前没有可汇总的检测设备";
        }
        List<String> values = new ArrayList<>();
        int attention = report.optInt("deviceAttentionCount", 0);
        if (attention > 0) {
            values.add("需关注 " + attention + " / 共 " + total);
        } else {
            values.add("共 " + total);
        }
        addCount(values, "就绪", report.optInt("deviceReadyCount", 0));
        addCount(values, "忙碌", report.optInt("deviceBusyCount", 0));
        addCount(values, "已关闭", report.optInt("deviceClosedCount", 0));
        addCount(values, "离线", report.optInt("deviceOfflineCount", 0));
        addCount(values, "未初始化", report.optInt("deviceUninitializedCount", 0));
        addCount(values, "未授权", report.optInt("deviceUnauthorizedCount", 0));
        addCount(values, "未归类", report.optInt("deviceUnclassifiedUnavailableCount", 0));
        return String.join(" · ", values);
    }

    private static void addCount(List<String> values, String label, int count) {
        if (count > 0) {
            values.add(label + " " + count);
        }
    }

    private static int messageTone(String state, int active, int registered) {
        if ("disconnected".equals(state)) {
            return TONE_ERROR;
        }
        if ("degraded".equals(state) || "unconfigured".equals(state)
                || (registered > 0 && active < registered)) {
            return TONE_ATTENTION;
        }
        return "connected".equals(state) ? TONE_NORMAL : TONE_MUTED;
    }

    private static String messageChannelLabel(String value) {
        switch (value) {
            case "connected": return "已连接 · 订阅就绪";
            case "degraded": return "已连接 · 订阅未完全恢复";
            case "disconnected": return "ColorVision 未连接消息服务";
            case "unconfigured": return "消息通道未配置";
            default: return "状态暂不可用";
        }
    }

    private static String stateLabel(String value) {
        if ("critical".equalsIgnoreCase(value)) {
            return "发现严重事件 · 请优先复核";
        }
        if ("attention".equalsIgnoreCase(value)) {
            return "发现需要关注的状态";
        }
        return "当前有界证据正常";
    }

    private static int stateTone(String value) {
        if ("critical".equalsIgnoreCase(value)) {
            return TONE_ERROR;
        }
        if ("attention".equalsIgnoreCase(value)) {
            return TONE_ATTENTION;
        }
        return TONE_NORMAL;
    }

    private static String severityLabel(String severity) {
        switch (severity) {
            case "critical": return "严重";
            case "error": return "错误";
            case "warning": return "警告";
            default: return "提示";
        }
    }

    private static String categoryLabel(String category) {
        switch (category) {
            case "devices": return "检测设备";
            case "message-channel": return "消息通道";
            case "services": return "系统服务";
            case "diagnostics": return "诊断事件";
            case "message-service": return "消息服务";
            case "desktop": return "主窗口";
            case "approvals": return "作业审批";
            case "failure-evidence": return "崩溃与卡死";
            default: return "运行状态";
        }
    }

    interface TimeFormatter {
        String format(String value);
    }

    static final class ViewModel {
        final String stateLabel;
        final String summary;
        final int tone;
        final List<Metric> metrics;
        final List<Finding> findings;
        final String safetyNotice;

        ViewModel(
                String stateLabel,
                String summary,
                int tone,
                List<Metric> metrics,
                List<Finding> findings,
                String safetyNotice) {
            this.stateLabel = stateLabel;
            this.summary = summary;
            this.tone = tone;
            this.metrics = metrics;
            this.findings = findings;
            this.safetyNotice = safetyNotice;
        }
    }

    static final class Metric {
        final String label;
        final String summary;
        final int tone;

        Metric(String label, String summary, int tone) {
            this.label = label;
            this.summary = summary;
            this.tone = tone;
        }

        String accessibilityLabel() {
            return label + "，" + summary.replace(" · ", "，");
        }
    }

    static final class Finding {
        final String severity;
        final String severityLabel;
        final String categoryLabel;
        final String title;
        final String summary;
        final int evidenceCount;
        final String latestAt;
        final List<Action> actions;

        Finding(
                String severity,
                String severityLabel,
                String categoryLabel,
                String title,
                String summary,
                int evidenceCount,
                String latestAt,
                List<Action> actions) {
            this.severity = severity;
            this.severityLabel = severityLabel;
            this.categoryLabel = categoryLabel;
            this.title = title;
            this.summary = summary;
            this.evidenceCount = evidenceCount;
            this.latestAt = latestAt;
            this.actions = actions;
        }

        int tone() {
            if ("critical".equals(severity) || "error".equals(severity)) {
                return TONE_ERROR;
            }
            if ("warning".equals(severity)) {
                return TONE_ATTENTION;
            }
            return TONE_MUTED;
        }

        String evidenceLabel() {
            if (evidenceCount <= 0) {
                return severityLabel + " · " + categoryLabel;
            }
            return severityLabel + " · " + categoryLabel + " · " + evidenceCount + " 条证据";
        }
    }

    static final class Action {
        final String actionId;
        final String title;
        final String description;
        final String riskLevel;
        final boolean requiresConfirmation;
        final boolean requiresLocalCoSign;

        Action(
                String actionId,
                String title,
                String description,
                String riskLevel,
                boolean requiresConfirmation,
                boolean requiresLocalCoSign) {
            this.actionId = actionId;
            this.title = title;
            this.description = description;
            this.riskLevel = riskLevel;
            this.requiresConfirmation = requiresConfirmation;
            this.requiresLocalCoSign = requiresLocalCoSign;
        }

        String buttonLabel() {
            if (requiresLocalCoSign) {
                return title + "（需电脑共签）";
            }
            if (requiresConfirmation) {
                return title + "（需确认）";
            }
            return title;
        }

        boolean readOnly() {
            return "read-only".equals(riskLevel);
        }
    }
}

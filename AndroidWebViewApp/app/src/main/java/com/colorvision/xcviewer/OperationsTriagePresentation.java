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
                        ? TONE_ERROR : warningCount > 0 ? TONE_ATTENTION : TONE_NORMAL,
                "triage.events.view"));
        metrics.add(new Metric(
                "待处理作业",
                pendingJobCount == 0 ? "当前没有待处理作业" : pendingJobCount + " 个等待处理",
                pendingJobCount > 0 ? TONE_ATTENTION : TONE_NORMAL,
                "triage.jobs.review"));
        metrics.add(new Metric(
                "消息通道",
                messageChannelLabel(messageState)
                        + " · 订阅 " + activeSubscriptions + '/' + registeredSubscriptions,
                messageTone(messageState, activeSubscriptions, registeredSubscriptions),
                "triage.messaging.view"));
        metrics.add(new Metric(
                "检测设备",
                compactDeviceSummary(report),
                deviceSummary(report),
                deviceAttentionCount > 0 ? TONE_ATTENTION
                        : deviceTotalCount == 0 ? TONE_MUTED : TONE_NORMAL,
                "triage.devices.view"));

        List<Finding> findings = new ArrayList<>();
        JSONArray sourceFindings = report.optJSONArray("findings");
        if (sourceFindings != null) {
            for (int index = 0; index < sourceFindings.length(); index++) {
                JSONObject source = sourceFindings.optJSONObject(index);
                if (source == null) {
                    continue;
                }
                String severity = source.optString("severity", "info");
                String category = source.optString("category", "");
                String title = source.optString("title", "需要关注");
                String findingSummary = source.optString("summary", "");
                int evidenceCount = Math.max(0, source.optInt("evidenceCount", 0));
                String rawLatestAt = source.optString("latestAt", "");
                String findingId = OperationsTriageFindingRevision.findingId(
                        source.optString("findingId", ""), category, title);
                String revision = OperationsTriageFindingRevision.revision(
                        findingId,
                        severity,
                        category,
                        title,
                        findingSummary,
                        evidenceCount,
                        rawLatestAt);
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
                String latestAt = timeFormatter.format(rawLatestAt);
                findings.add(new Finding(
                        findingId,
                        revision,
                        false,
                        severity,
                        severityLabel(severity),
                        category,
                        categoryLabel(category),
                        title,
                        findingSummary,
                        evidenceCount,
                        latestAt,
                        Collections.unmodifiableList(actions)));
            }
        }

        String summary = report.optString("summary", findings.isEmpty()
                ? "当前有界证据中没有需要处理的项目。" : "排障建议已生成。");
        return new ViewModel(
                state,
                stateLabel(state, findings.size(), 0),
                summary,
                stateTone(state),
                Collections.unmodifiableList(metrics),
                Collections.unmodifiableList(findings),
                Collections.unmodifiableList(findings),
                Collections.emptyList(),
                report.optString("safetyNotice",
                        "建议仅引用有界脱敏摘要；远程恢复或取证动作仍需明确确认。"));
    }

    static ViewModel withAcknowledgements(
            ViewModel model, AcknowledgementLookup acknowledgementLookup) {
        if (model == null || acknowledgementLookup == null) {
            return model;
        }
        List<Finding> all = new ArrayList<>();
        List<Finding> pending = new ArrayList<>();
        List<Finding> reviewed = new ArrayList<>();
        for (Finding source : model.findings) {
            boolean acknowledged = acknowledgementLookup.isAcknowledged(
                    source.findingId, source.revision);
            Finding finding = source.withAcknowledged(acknowledged);
            all.add(finding);
            (acknowledged ? reviewed : pending).add(finding);
        }
        return new ViewModel(
                model.reportState,
                stateLabel(model.reportState, pending.size(), reviewed.size()),
                model.summary,
                model.tone,
                model.metrics,
                Collections.unmodifiableList(all),
                Collections.unmodifiableList(pending),
                Collections.unmodifiableList(reviewed),
                model.safetyNotice);
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

    static String failureDetails(String readableError, boolean previousReportVisible) {
        String detail = readableError == null || readableError.trim().isEmpty()
                ? "电脑端安全通道当前不可达。"
                : readableError.trim();
        if (!previousReportVisible) {
            return detail;
        }
        return detail + "\n\n下方保留上次成功的排障摘要，仅供参考；"
                + "恢复连接后请重新刷新。";
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

    private static String compactDeviceSummary(JSONObject report) {
        int total = report.optInt("deviceTotalCount", 0);
        if (total <= 0) {
            return "当前没有可汇总的检测设备";
        }
        List<String> values = new ArrayList<>();
        int attention = report.optInt("deviceAttentionCount", 0);
        values.add(attention > 0
                ? "需关注\u00a0" + attention + '/' + total
                : "共\u00a0" + total);
        addCompactCount(values, "就绪", report.optInt("deviceReadyCount", 0));
        addCompactCount(values, "忙碌", report.optInt("deviceBusyCount", 0));
        addCompactCount(values, "关闭", report.optInt("deviceClosedCount", 0));
        addCompactCount(values, "离线", report.optInt("deviceOfflineCount", 0));
        addCompactCount(values, "未初始化", report.optInt("deviceUninitializedCount", 0));
        addCompactCount(values, "未授权", report.optInt("deviceUnauthorizedCount", 0));
        addCompactCount(values, "未归类", report.optInt("deviceUnclassifiedUnavailableCount", 0));
        return String.join(" · ", values);
    }

    private static void addCount(List<String> values, String label, int count) {
        if (count > 0) {
            values.add(label + " " + count);
        }
    }

    private static void addCompactCount(List<String> values, String label, int count) {
        if (count > 0) {
            values.add(label + '\u00a0' + count);
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

    private static String stateLabel(String value, int pendingCount, int reviewedCount) {
        if (pendingCount == 0 && reviewedCount > 0) {
            return reviewedCount + " 项已复核 · 状态仍存在";
        }
        if ("critical".equalsIgnoreCase(value)) {
            return pendingCount > 0
                    ? "严重事件 · " + pendingCount + " 项待复核"
                    : "发现严重事件 · 请优先复核";
        }
        if ("attention".equalsIgnoreCase(value)) {
            return pendingCount > 0
                    ? "需要关注 · " + pendingCount + " 项待复核"
                    : "发现需要关注的状态";
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

    interface AcknowledgementLookup {
        boolean isAcknowledged(String findingId, String revision);
    }

    static final class ViewModel {
        final String reportState;
        final String stateLabel;
        final String summary;
        final int tone;
        final List<Metric> metrics;
        final List<Finding> findings;
        final List<Finding> pendingFindings;
        final List<Finding> reviewedFindings;
        final String safetyNotice;

        ViewModel(
                String reportState,
                String stateLabel,
                String summary,
                int tone,
                List<Metric> metrics,
                List<Finding> findings,
                List<Finding> pendingFindings,
                List<Finding> reviewedFindings,
                String safetyNotice) {
            this.reportState = reportState;
            this.stateLabel = stateLabel;
            this.summary = summary;
            this.tone = tone;
            this.metrics = metrics;
            this.findings = findings;
            this.pendingFindings = pendingFindings;
            this.reviewedFindings = reviewedFindings;
            this.safetyNotice = safetyNotice;
        }

        String prioritySectionLabel() {
            return pendingFindings.size() == 1
                    ? "优先处理"
                    : "优先处理 · " + pendingFindings.size();
        }

        String reviewedSectionLabel() {
            return reviewedFindings.size() == 1
                    ? "已复核 · 状态仍存在"
                    : "已复核 · " + reviewedFindings.size() + " 项状态仍存在";
        }

        String watchState() {
            if (tone == TONE_NORMAL || findings.isEmpty()) {
                return OperationsWatchHistory.STATE_ONLINE;
            }
            if ("critical".equalsIgnoreCase(reportState)) {
                return OperationsWatchHistory.attentionState(
                        OperationsWatchPolicy.ATTENTION_CRITICAL);
            }
            boolean devices = false;
            boolean messaging = false;
            for (Finding finding : findings) {
                if ("desktop".equals(finding.category)) {
                    return OperationsWatchHistory.attentionState(
                            OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE);
                }
                devices |= "devices".equals(finding.category);
                messaging |= "message-channel".equals(finding.category)
                        || "message-service".equals(finding.category);
            }
            if (messaging) {
                return OperationsWatchHistory.attentionState(
                        OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL);
            }
            if (devices) {
                return OperationsWatchHistory.attentionState(
                        OperationsWatchPolicy.ATTENTION_DEVICES);
            }
            return OperationsWatchHistory.attentionState(
                    OperationsWatchPolicy.ATTENTION_ERRORS);
        }
    }

    static final class Metric {
        final String label;
        final String summary;
        final String spokenSummary;
        final int tone;
        final String actionId;

        Metric(String label, String summary, int tone, String actionId) {
            this(label, summary, summary, tone, actionId);
        }

        Metric(
                String label,
                String summary,
                String spokenSummary,
                int tone,
                String actionId) {
            this.label = label;
            this.summary = summary;
            this.spokenSummary = spokenSummary;
            this.tone = tone;
            this.actionId = actionId;
        }

        String accessibilityLabel() {
            return label + "，" + spokenSummary.replace(" · ", "，")
                    + "，点按查看详情";
        }
    }

    static final class Finding {
        final String findingId;
        final String revision;
        final boolean acknowledged;
        final String severity;
        final String severityLabel;
        final String category;
        final String categoryLabel;
        final String title;
        final String summary;
        final int evidenceCount;
        final String latestAt;
        final List<Action> actions;

        Finding(
                String findingId,
                String revision,
                boolean acknowledged,
                String severity,
                String severityLabel,
                String category,
                String categoryLabel,
                String title,
                String summary,
                int evidenceCount,
                String latestAt,
                List<Action> actions) {
            this.findingId = findingId;
            this.revision = revision;
            this.acknowledged = acknowledged;
            this.severity = severity;
            this.severityLabel = severityLabel;
            this.category = category;
            this.categoryLabel = categoryLabel;
            this.title = title;
            this.summary = summary;
            this.evidenceCount = evidenceCount;
            this.latestAt = latestAt;
            this.actions = actions;
        }

        Finding withAcknowledged(boolean value) {
            if (acknowledged == value) {
                return this;
            }
            return new Finding(
                    findingId,
                    revision,
                    value,
                    severity,
                    severityLabel,
                    category,
                    categoryLabel,
                    title,
                    summary,
                    evidenceCount,
                    latestAt,
                    actions);
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

        String listMetaLabel() {
            String label = latestAt.isEmpty()
                    ? evidenceLabel()
                    : evidenceLabel() + " · " + latestAt;
            return acknowledged ? "已复核 · " + label : label;
        }

        Action primaryCardAction() {
            for (Action action : actions) {
                if (action.readOnly() && isSupportedAction(action.actionId)) {
                    return action;
                }
            }
            return null;
        }

        String cardAccessibilityLabel(Action action) {
            StringBuilder label = new StringBuilder();
            appendSentence(label, evidenceLabel());
            appendSentence(label, title);
            appendSentence(label, summary);
            appendSentence(label, latestAt.isEmpty() ? "" : "最近证据 " + latestAt);
            appendSentence(label, acknowledged ? "已在此手机复核，电脑状态仍存在" : "");
            appendSentence(label, "点按" + action.buttonLabel());
            return label.toString();
        }

        private static void appendSentence(StringBuilder target, String value) {
            if (value == null || value.isEmpty()) {
                return;
            }
            if (target.length() > 0 && target.charAt(target.length() - 1) != '。') {
                target.append('。');
            }
            target.append(value);
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

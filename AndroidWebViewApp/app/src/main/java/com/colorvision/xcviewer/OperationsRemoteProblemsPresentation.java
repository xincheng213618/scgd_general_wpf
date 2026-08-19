package com.colorvision.xcviewer;

import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

final class OperationsRemoteProblemsPresentation {
    static final String SAFETY_NOTICE = "本页只读电脑签名摘要，不会直接执行操作。"
            + "完整证据与现场处置仅限安全直连；受控远程操作从概览进入并再次确认。";

    private OperationsRemoteProblemsPresentation() {
    }

    static ViewModel from(JSONObject monitor, boolean fresh) {
        if (monitor == null) {
            return unavailable(
                    "远程问题状态暂不可用",
                    "电脑签名快照尚未包含运行摘要。刷新后仍会重新核验电脑签名，不会把未知状态当作正常。");
        }
        if (!fresh) {
            return unavailable(
                    "等待电脑更新远程状态",
                    "上次电脑签名快照已过期，旧问题不会作为当前问题展示。请刷新或改用现场直连。");
        }

        JSONObject flow = monitor.optJSONObject("flow");
        JSONObject devices = monitor.optJSONObject("devices");
        JSONObject messageChannel = monitor.optJSONObject("messageChannel");
        JSONObject alerts = monitor.optJSONObject("alerts");
        JSONObject performance = monitor.optJSONObject("performance");
        JSONObject mainUi = performance == null ? null : performance.optJSONObject("mainUi");
        JSONObject recovery = monitor.optJSONObject("applicationRecovery");
        DeviceHealthPresentation.ViewModel deviceHealth = DeviceHealthPresentation.from(devices);

        List<Issue> issues = new ArrayList<>();
        addIfAttention(issues, "flow", monitor, OperationsDashboardStatusFormatter.flow(
                isAvailable(flow),
                flow != null && flow.optBoolean("isActive", false),
                flow == null ? "idle" : flow.optString("phase", "idle")));
        addIfAttention(issues, "devices", monitor, OperationsDashboardStatusFormatter.devices(
                isAvailable(devices),
                devices != null && devices.optBoolean("hasConfiguredDevices", false),
                count(devices, "readyCount"),
                count(devices, "busyCount"),
                count(devices, "attentionCount"),
                count(devices, "totalCount"),
                deviceHealth.compactAttentionSummary()));
        addIfAttention(issues, "message", monitor,
                OperationsDashboardStatusFormatter.messageChannel(
                isAvailable(messageChannel),
                messageChannel != null && messageChannel.optBoolean("connected", false),
                messageChannel != null && messageChannel.optBoolean("subscriptionReady", false),
                count(messageChannel, "activeSubscriptionCount"),
                count(messageChannel, "registeredSubscriptionCount")));
        addIfAttention(issues, "alerts", monitor, OperationsDashboardStatusFormatter.alerts(
                alerts != null,
                count(alerts, "warningCount"),
                count(alerts, "errorCount"),
                count(alerts, "criticalCount"),
                alerts == null ? "" : alerts.optString("primarySource", "")));
        addIfAttention(issues, "performance", monitor,
                OperationsDashboardStatusFormatter.performance(
                performance != null,
                performance == null ? 0 : performance.optDouble("cpuPercent", 0),
                mainUi == null ? "unavailable" : mainUi.optString("state", "unavailable")));
        addIfAttention(issues, "recovery", monitor, OperationsDashboardStatusFormatter.recovery(
                recovery != null,
                recovery != null && recovery.optBoolean("supported", false),
                recovery != null && recovery.optBoolean("registered", false),
                recovery != null && recovery.optBoolean("automaticWatchdogActive", false)));
        Collections.sort(issues, (left, right) ->
                OperationsDashboardStatusOrder.compare(left.status, right.status));

        int incompleteCount = 0;
        incompleteCount += isAvailable(flow) ? 0 : 1;
        incompleteCount += isAvailable(devices) ? 0 : 1;
        incompleteCount += isAvailable(messageChannel) ? 0 : 1;
        incompleteCount += alerts == null ? 1 : 0;
        incompleteCount += performance == null
                || mainUi == null
                || "unavailable".equals(mainUi.optString("state", "unavailable")) ? 1 : 0;
        incompleteCount += recovery == null ? 1 : 0;

        String stateLabel;
        String summary;
        if (!issues.isEmpty()) {
            stateLabel = issues.size() + " 项需要关注";
            summary = "来自当前电脑刚刚签名的只读快照，已按处置优先级排列。";
            if (incompleteCount > 0) {
                summary += "另有 " + incompleteCount + " 类状态尚未确认。";
            }
        } else if (incompleteCount > 0) {
            stateLabel = "问题状态不完整";
            summary = "未发现明确需关注项，但仍有 " + incompleteCount
                    + " 类状态尚未确认，暂不能判断为全部正常。";
        } else {
            stateLabel = "未发现需要关注项目";
            summary = "电脑签名状态未发现需要关注项目；这是只读快照，不替代电脑端现场确认。";
        }
        return new ViewModel(
                true,
                stateLabel,
                summary,
                incompleteCount,
                Collections.unmodifiableList(issues),
                Collections.unmodifiableList(issues),
                Collections.emptyList());
    }

    static FocusedViewModel focus(ViewModel model, String attentionKey) {
        String normalized = OperationsAttentionFocus.normalize(attentionKey);
        if (model == null || normalized.isEmpty()) {
            return new FocusedViewModel(model, "");
        }
        List<Issue> issues = prioritize(model.issues, normalized);
        boolean found = !issues.isEmpty()
                && OperationsAttentionFocus.matchesRemoteSection(
                        normalized, issues.get(0).section);
        return new FocusedViewModel(
                new ViewModel(
                        model.snapshotAvailable,
                        model.stateLabel,
                        model.summary,
                        model.incompleteCount,
                        issues,
                        prioritize(model.pendingIssues, normalized),
                        prioritize(model.reviewedIssues, normalized)),
                OperationsAttentionFocus.contextMessage(
                        normalized, found, model.snapshotAvailable));
    }

    static ViewModel withAcknowledgements(
            ViewModel model, AcknowledgementLookup acknowledgementLookup) {
        if (model == null || acknowledgementLookup == null) {
            return model;
        }
        List<Issue> all = new ArrayList<>();
        List<Issue> pending = new ArrayList<>();
        List<Issue> reviewed = new ArrayList<>();
        for (Issue source : model.issues) {
            boolean acknowledged = acknowledgementLookup.isAcknowledged(
                    source.findingId, source.revision);
            Issue issue = source.withAcknowledged(acknowledged);
            all.add(issue);
            (acknowledged ? reviewed : pending).add(issue);
        }
        String stateLabel = model.stateLabel;
        if (!pending.isEmpty()) {
            stateLabel = pending.size() + " 项待复核";
        } else if (!reviewed.isEmpty()) {
            stateLabel = reviewed.size() + " 项已复核 · 状态仍存在";
        }
        return new ViewModel(
                model.snapshotAvailable,
                stateLabel,
                model.summary,
                model.incompleteCount,
                Collections.unmodifiableList(all),
                Collections.unmodifiableList(pending),
                Collections.unmodifiableList(reviewed));
    }

    private static ViewModel unavailable(String stateLabel, String summary) {
        return new ViewModel(
                false,
                stateLabel,
                summary,
                0,
                Collections.emptyList(),
                Collections.emptyList(),
                Collections.emptyList());
    }

    private static void addIfAttention(
            List<Issue> issues,
            String section,
            JSONObject monitor,
            OperationsDashboardStatusFormatter.Item status) {
        if (status.tone == OperationsDashboardStatusFormatter.TONE_ATTENTION) {
            OperationsRemoteProblemRevision.Identity identity =
                    OperationsRemoteProblemRevision.capture(section, monitor, status);
            if (identity.available()) {
                issues.add(new Issue(
                        section,
                        status,
                        identity.findingId,
                        identity.revision,
                        false));
            }
        }
    }

    private static boolean isAvailable(JSONObject value) {
        return value != null && value.optBoolean("available", false);
    }

    private static int count(JSONObject value, String field) {
        return value == null ? 0 : Math.max(0, Math.min(999, value.optInt(field, 0)));
    }

    private static List<Issue> prioritize(List<Issue> source, String attentionKey) {
        if (source == null || source.isEmpty()) {
            return Collections.emptyList();
        }
        List<Issue> prioritized = new ArrayList<>(source.size());
        for (Issue issue : source) {
            if (OperationsAttentionFocus.matchesRemoteSection(attentionKey, issue.section)) {
                prioritized.add(issue);
            }
        }
        for (Issue issue : source) {
            if (!OperationsAttentionFocus.matchesRemoteSection(attentionKey, issue.section)) {
                prioritized.add(issue);
            }
        }
        return Collections.unmodifiableList(prioritized);
    }

    interface AcknowledgementLookup {
        boolean isAcknowledged(String findingId, String revision);
    }

    static final class FocusedViewModel {
        final ViewModel model;
        final String contextMessage;

        FocusedViewModel(ViewModel model, String contextMessage) {
            this.model = model;
            this.contextMessage = contextMessage;
        }
    }

    static final class ViewModel {
        final boolean snapshotAvailable;
        final String stateLabel;
        final String summary;
        final int incompleteCount;
        final List<Issue> issues;
        final List<Issue> pendingIssues;
        final List<Issue> reviewedIssues;

        ViewModel(
                boolean snapshotAvailable,
                String stateLabel,
                String summary,
                int incompleteCount,
                List<Issue> issues,
                List<Issue> pendingIssues,
                List<Issue> reviewedIssues) {
            this.snapshotAvailable = snapshotAvailable;
            this.stateLabel = stateLabel;
            this.summary = summary;
            this.incompleteCount = Math.max(0, incompleteCount);
            this.issues = issues;
            this.pendingIssues = pendingIssues;
            this.reviewedIssues = reviewedIssues;
        }
    }

    static final class Issue {
        final String section;
        final OperationsDashboardStatusFormatter.Item status;
        final String findingId;
        final String revision;
        final boolean acknowledged;

        Issue(
                String section,
                OperationsDashboardStatusFormatter.Item status,
                String findingId,
                String revision,
                boolean acknowledged) {
            this.section = section;
            this.status = status;
            this.findingId = findingId;
            this.revision = revision;
            this.acknowledged = acknowledged;
        }

        Issue withAcknowledged(boolean value) {
            if (acknowledged == value) {
                return this;
            }
            return new Issue(section, status, findingId, revision, value);
        }

        String accessibilityLabel() {
            return (acknowledged ? "已复核，" : "")
                    + status.title + "，" + status.summary + "，查看电脑签名详情";
        }
    }
}

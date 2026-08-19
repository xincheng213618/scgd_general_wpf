package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

final class OperationsAuditPresentation {
    static final int TONE_NORMAL = 0;
    static final int TONE_ATTENTION = 1;
    static final int TONE_ERROR = 2;
    static final int MAXIMUM_ENTRIES = 30;
    static final String PRIVACY_NOTICE = "只显示最近 30 条去标识记录；不返回设备 ID、人员名称、"
            + "操作目标或内部关联 ID。内容不能用于识别具体人员。";

    private OperationsAuditPresentation() {
    }

    static ViewModel from(JSONObject payload, TimeFormatter timeFormatter) {
        JSONArray sourceEntries = payload == null ? null : payload.optJSONArray("entries");
        List<Entry> entries = new ArrayList<>();
        List<Entry> focusedEntries = new ArrayList<>();
        int successCount = 0;
        int attentionCount = 0;
        int errorCount = 0;
        if (sourceEntries != null) {
            int count = Math.min(sourceEntries.length(), MAXIMUM_ENTRIES);
            for (int index = 0; index < count; index++) {
                JSONObject source = sourceEntries.optJSONObject(index);
                if (source == null) {
                    continue;
                }
                String action = source.optString("action", "");
                String outcome = source.optString("outcome", "");
                Entry entry = new Entry(
                        actionLabel(action),
                        outcomeLabel(outcome),
                        actorLabel(source.optString("actorType", "")),
                        timeFormatter.format(source.optString("timestamp", "")),
                        outcomeTone(outcome),
                        "monitor.read".equals(action));
                entries.add(entry);
                if (!entry.routine) {
                    focusedEntries.add(entry);
                }
                if (entry.tone == TONE_ERROR) {
                    errorCount++;
                } else if (entry.tone == TONE_ATTENTION) {
                    attentionCount++;
                } else {
                    successCount++;
                }
            }
        }

        int hiddenEntryCount = sourceEntries == null
                ? 0
                : Math.max(0, sourceEntries.length() - entries.size());
        List<Entry> immutableEntries = Collections.unmodifiableList(entries);
        List<Entry> immutableFocused = Collections.unmodifiableList(focusedEntries);
        return new ViewModel(
                entries.isEmpty() ? "暂无近期操作记录" : entries.size() + " 条近期记录",
                successCount,
                attentionCount,
                errorCount,
                entries.size() - focusedEntries.size(),
                hiddenEntryCount,
                immutableEntries,
                immutableFocused);
    }

    static String actionLabel(String value) {
        switch (value) {
            case "job.create": return "创建运维作业";
            case "job.approve": return "手机批准作业";
            case "job.reject": return "手机拒绝作业";
            case "job.local_cosign": return "电脑端共签作业";
            case "job.local_reject": return "电脑端拒绝作业";
            case "job.execution.start": return "开始执行受控作业";
            case "job.complete": return "作业执行完成";
            case "job.evidence.consume": return "读取一次性作业证据";
            case "desktop.action.execute": return "执行主窗口控制";
            case "diagnostics.performance.read": return "读取进程性能快照";
            case "diagnostics.failure-evidence.read": return "读取崩溃与卡死线索";
            case "flow.runtime.read": return "读取当前检测状态";
            case "monitor.read": return "持续观察运行状态";
            case "messaging.health.read": return "读取消息通道健康";
            case "diagnostic.bundle.download": return "下载安全诊断包";
            case "window.snapshot.download": return "读取主窗口安全快照";
            case "deployment.receipt.create": return "提交部署确认";
            case "support.request": return "申请引导支持会话";
            case "support.local_consent": return "电脑端同意支持会话";
            case "support.local_reject": return "电脑端拒绝支持会话";
            case "support.message.send": return "手机发送支持消息";
            case "support.message.receive": return "接收支持中继消息";
            default: return "受控运维活动";
        }
    }

    static String actorLabel(String value) {
        switch (value) {
            case "device": return "已配对手机";
            case "local-user": return "电脑本机人员";
            case "system": return "运维系统";
            case "support-relay": return "支持中继";
            default: return "受控运维通道";
        }
    }

    static String outcomeLabel(String value) {
        switch (value) {
            case "success":
            case "completed":
            case "accepted":
            case "approved_local":
            case "active":
            case "consumed": return "成功";
            case "rejected":
            case "rejected_local": return "已拒绝";
            case "failed": return "失败";
            case "awaiting_mobile_approval": return "等待手机批准";
            case "executing": return "执行中";
            case "awaiting_local_cosign":
            case "awaiting_local_consent": return "等待电脑确认";
            default: return "已记录";
        }
    }

    private static int outcomeTone(String value) {
        if ("failed".equals(value)) {
            return TONE_ERROR;
        }
        if ("rejected".equals(value)
                || "rejected_local".equals(value)
                || "awaiting_mobile_approval".equals(value)
                || "executing".equals(value)
                || "awaiting_local_cosign".equals(value)
                || "awaiting_local_consent".equals(value)) {
            return TONE_ATTENTION;
        }
        return TONE_NORMAL;
    }

    interface TimeFormatter {
        String format(String value);
    }

    static final class ViewModel {
        final String stateLabel;
        final int successCount;
        final int attentionCount;
        final int errorCount;
        final int routineCount;
        final int hiddenEntryCount;
        final List<Entry> entries;
        final List<Entry> focusedEntries;

        ViewModel(
                String stateLabel,
                int successCount,
                int attentionCount,
                int errorCount,
                int routineCount,
                int hiddenEntryCount,
                List<Entry> entries,
                List<Entry> focusedEntries) {
            this.stateLabel = stateLabel;
            this.successCount = successCount;
            this.attentionCount = attentionCount;
            this.errorCount = errorCount;
            this.routineCount = routineCount;
            this.hiddenEntryCount = hiddenEntryCount;
            this.entries = entries;
            this.focusedEntries = focusedEntries;
        }

        boolean defaultsToFocusedEntries() {
            return !focusedEntries.isEmpty() && routineCount > 0;
        }

        String summaryLabel() {
            StringBuilder label = new StringBuilder("成功 ").append(successCount);
            if (attentionCount > 0) {
                label.append(" · 待复核 ").append(attentionCount);
            }
            if (errorCount > 0) {
                label.append(" · 失败 ").append(errorCount);
            }
            return label.toString();
        }

        String plainText() {
            if (entries.isEmpty()) {
                return "当前没有远程操作记录。\n\n" + PRIVACY_NOTICE;
            }
            StringBuilder text = new StringBuilder("近期远程操作：")
                    .append(entries.size()).append(" 条");
            for (int index = 0; index < entries.size(); index++) {
                Entry entry = entries.get(index);
                text.append("\n\n").append(index + 1).append(". ")
                        .append(entry.actionLabel)
                        .append("\n结果：").append(entry.outcomeLabel)
                        .append(" · 发起方：").append(entry.actorLabel);
                if (!entry.timestamp.isEmpty()) {
                    text.append("\n时间：").append(entry.timestamp);
                }
            }
            return text.append("\n\n").append(PRIVACY_NOTICE).toString();
        }
    }

    static final class Entry {
        final String actionLabel;
        final String outcomeLabel;
        final String actorLabel;
        final String timestamp;
        final int tone;
        final boolean routine;

        Entry(
                String actionLabel,
                String outcomeLabel,
                String actorLabel,
                String timestamp,
                int tone,
                boolean routine) {
            this.actionLabel = actionLabel;
            this.outcomeLabel = outcomeLabel;
            this.actorLabel = actorLabel;
            this.timestamp = timestamp;
            this.tone = tone;
            this.routine = routine;
        }

        String metadataLabel() {
            StringBuilder label = new StringBuilder(outcomeLabel)
                    .append(" · ").append(actorLabel);
            if (!timestamp.isEmpty()) {
                label.append(" · ").append(timestamp);
            }
            return label.toString();
        }

        String accessibilityLabel() {
            return actionLabel + "。" + metadataLabel().replace(" · ", "，");
        }
    }
}

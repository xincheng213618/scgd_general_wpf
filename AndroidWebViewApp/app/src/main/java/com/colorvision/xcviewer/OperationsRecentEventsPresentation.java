package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

final class OperationsRecentEventsPresentation {
    static final int TONE_MUTED = 0;
    static final int TONE_ATTENTION = 1;
    static final int TONE_ERROR = 2;
    private static final int MAXIMUM_EVENTS = 12;

    private OperationsRecentEventsPresentation() {
    }

    static ViewModel from(JSONObject payload, TimeFormatter timeFormatter) {
        if (payload == null || !payload.optBoolean("available", false)) {
            return new ViewModel(
                    false,
                    "近期事件不可用",
                    "当前没有可读取的应用日志摘要。",
                    "",
                    "",
                    "电脑端不会为此页面创建或搜索其他文件。",
                    "电脑端不会为此接口创建或搜索其他文件，也不会返回目录信息。",
                    TONE_MUTED,
                    0,
                    Collections.emptyList());
        }

        int infoCount = boundedCount(payload, "infoCount");
        int warningCount = boundedCount(payload, "warningCount");
        int errorCount = boundedCount(payload, "errorCount");
        int criticalCount = boundedCount(payload, "criticalCount");
        int attentionCount = warningCount + errorCount + criticalCount;

        List<Event> events = new ArrayList<>();
        JSONArray sourceEvents = payload.optJSONArray("recentEvents");
        if (sourceEvents != null) {
            int count = Math.min(sourceEvents.length(), MAXIMUM_EVENTS);
            for (int index = 0; index < count; index++) {
                JSONObject source = sourceEvents.optJSONObject(index);
                if (source == null) {
                    continue;
                }
                String severity = source.optString("severity", "warning");
                events.add(new Event(
                        severityLabel(severity),
                        source.optString("source", "应用"),
                        timeFormatter.format(source.optString("occurredAt", "")),
                        source.optString("summary", "无摘要"),
                        tone(severity)));
            }
        }

        return new ViewModel(
                true,
                attentionCount > 0
                        ? attentionCount + " 条近期异常"
                        : "近期没有异常事件",
                "扫描 " + boundedCount(payload, "scannedLineCount")
                        + " 行 · 识别 " + boundedCount(payload, "parsedEventCount") + " 个事件",
                "信息 " + infoCount
                        + " · 警告 " + warningCount
                        + " · 错误 " + errorCount
                        + " · 严重 " + criticalCount,
                categorySummary(payload.optJSONArray("categories")),
                payload.optBoolean("tailWasBounded", false)
                        ? "日志较大，仅分析固定大小的最近尾部"
                        : "最近日志尾部 · 最多 500 行 / 256 KiB",
                payload.optString("privacyNotice",
                        "仅返回有界聚合与脱敏事件；不返回完整日志或凭据。"),
                criticalCount > 0 || errorCount > 0
                        ? TONE_ERROR
                        : warningCount > 0 ? TONE_ATTENTION : TONE_MUTED,
                Math.max(0, attentionCount - events.size()),
                Collections.unmodifiableList(events));
    }

    private static String categorySummary(JSONArray categories) {
        if (categories == null) {
            return "";
        }
        List<String> values = new ArrayList<>();
        for (int index = 0; index < categories.length(); index++) {
            JSONObject category = categories.optJSONObject(index);
            if (category == null) {
                continue;
            }
            int count = boundedCount(category, "count");
            if (count > 0) {
                values.add(category.optString("category", "应用") + ' ' + count);
            }
        }
        return String.join(" · ", values);
    }

    private static int boundedCount(JSONObject value, String name) {
        return value == null ? 0 : Math.max(0, Math.min(999_999, value.optInt(name, 0)));
    }

    private static String severityLabel(String severity) {
        if ("critical".equalsIgnoreCase(severity)) {
            return "严重";
        }
        if ("error".equalsIgnoreCase(severity)) {
            return "错误";
        }
        if ("warning".equalsIgnoreCase(severity)) {
            return "警告";
        }
        return "信息";
    }

    private static int tone(String severity) {
        if ("critical".equalsIgnoreCase(severity)
                || "error".equalsIgnoreCase(severity)) {
            return TONE_ERROR;
        }
        if ("warning".equalsIgnoreCase(severity)) {
            return TONE_ATTENTION;
        }
        return TONE_MUTED;
    }

    interface TimeFormatter {
        String format(String value);
    }

    static final class ViewModel {
        final boolean available;
        final String stateLabel;
        final String sampleSummary;
        final String severitySummary;
        final String categorySummary;
        final String rangeSummary;
        final String privacyNotice;
        final int tone;
        final int hiddenEventCount;
        final List<Event> events;

        ViewModel(
                boolean available,
                String stateLabel,
                String sampleSummary,
                String severitySummary,
                String categorySummary,
                String rangeSummary,
                String privacyNotice,
                int tone,
                int hiddenEventCount,
                List<Event> events) {
            this.available = available;
            this.stateLabel = stateLabel;
            this.sampleSummary = sampleSummary;
            this.severitySummary = severitySummary;
            this.categorySummary = categorySummary;
            this.rangeSummary = rangeSummary;
            this.privacyNotice = privacyNotice;
            this.tone = tone;
            this.hiddenEventCount = hiddenEventCount;
            this.events = events;
        }

        String eventsSectionLabel() {
            return events.isEmpty()
                    ? "近期异常事件"
                    : "近期异常事件 · " + events.size();
        }
    }

    static final class Event {
        final String severityLabel;
        final String source;
        final String occurredAt;
        final String summary;
        final int tone;

        Event(
                String severityLabel,
                String source,
                String occurredAt,
                String summary,
                int tone) {
            this.severityLabel = severityLabel;
            this.source = source;
            this.occurredAt = occurredAt;
            this.summary = summary;
            this.tone = tone;
        }

        String metadataLabel() {
            StringBuilder label = new StringBuilder(severityLabel)
                    .append(" · ").append(source);
            if (!occurredAt.isEmpty()) {
                label.append(" · ").append(occurredAt);
            }
            return label.toString();
        }

        String accessibilityLabel() {
            return metadataLabel().replace(" · ", "，") + "。" + summary;
        }
    }
}

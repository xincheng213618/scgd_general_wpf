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
    static final String ACTION_CONNECTION_CHECK = "events.connection.check";
    static final String ACTION_MESSAGE_CHANNEL = "events.message.channel";
    static final String ACTION_DEVICE_HEALTH = "events.device.health";
    static final String ACTION_SERVICE_HEALTH = "events.service.health";
    static final String ACTION_LIVE_MONITOR = "events.live.monitor";
    private static final int MAXIMUM_EVENTS = 12;
    private static final int MAXIMUM_RECOMMENDED_ACTIONS = 2;

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
                    Collections.emptyList(),
                    Collections.emptyList());
        }

        int infoCount = boundedCount(payload, "infoCount");
        int warningCount = boundedCount(payload, "warningCount");
        int errorCount = boundedCount(payload, "errorCount");
        int criticalCount = boundedCount(payload, "criticalCount");
        int attentionCount = warningCount + errorCount + criticalCount;

        List<Event> events = new ArrayList<>();
        int sampledEventCount = 0;
        JSONArray sourceEvents = payload.optJSONArray("recentEvents");
        if (sourceEvents != null) {
            int count = Math.min(sourceEvents.length(), MAXIMUM_EVENTS);
            for (int index = 0; index < count; index++) {
                JSONObject source = sourceEvents.optJSONObject(index);
                if (source == null) {
                    continue;
                }
                sampledEventCount++;
                String severity = source.optString("severity", "warning");
                Event event = new Event(
                        severityLabel(severity),
                        source.optString("source", "应用"),
                        timeFormatter.format(source.optString("occurredAt", "")),
                        source.optString("summary", "无摘要"),
                        tone(severity),
                        1);
                int existingIndex = matchingEventIndex(events, event);
                if (existingIndex >= 0) {
                    Event existing = events.get(existingIndex);
                    events.set(existingIndex, existing.withOccurrenceCount(
                            existing.occurrenceCount + 1));
                } else {
                    events.add(event);
                }
            }
        }

        List<Action> recommendedActions = recommendedActions(events);

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
                Math.max(0, attentionCount - sampledEventCount),
                Collections.unmodifiableList(events),
                Collections.unmodifiableList(recommendedActions));
    }

    private static int matchingEventIndex(List<Event> events, Event candidate) {
        for (int index = 0; index < events.size(); index++) {
            if (events.get(index).matches(candidate)) {
                return index;
            }
        }
        return -1;
    }

    private static List<Action> recommendedActions(List<Event> events) {
        List<Action> actions = new ArrayList<>();
        for (Event event : events) {
            Action action = recommendationFor(event);
            if (action == null || containsAction(actions, action.actionId)) {
                continue;
            }
            actions.add(action);
            if (actions.size() == MAXIMUM_RECOMMENDED_ACTIONS) {
                break;
            }
        }
        return actions;
    }

    private static Action recommendationFor(Event event) {
        String evidence = event.source + ' ' + event.summary;
        if (event.source.contains("安全运维") || evidence.contains("安全运维通道")) {
            return new Action(
                    ACTION_CONNECTION_CHECK,
                    "运行连接自检",
                    "核对手机网络、安全通道、证书固定与设备签名");
        }
        if (event.source.contains("消息") || evidence.contains("消息通道")) {
            return new Action(
                    ACTION_MESSAGE_CHANNEL,
                    "查看消息通道",
                    "检查连接、订阅和当前运行状态");
        }
        if (event.source.contains("设备") || event.source.contains("图像")) {
            return new Action(
                    ACTION_DEVICE_HEALTH,
                    "查看设备状态",
                    "定位不可用的设备类型和聚合原因");
        }
        if (event.source.contains("服务")) {
            return new Action(
                    ACTION_SERVICE_HEALTH,
                    "查看服务健康",
                    "检查服务、依赖和当前运行状态");
        }
        if (event.source.contains("流程")) {
            return new Action(
                    ACTION_LIVE_MONITOR,
                    "开始持续观察",
                    "每 10 秒采样关键状态，确认异常是否持续");
        }
        return null;
    }

    private static boolean containsAction(List<Action> actions, String actionId) {
        for (Action action : actions) {
            if (action.actionId.equals(actionId)) {
                return true;
            }
        }
        return false;
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
        final List<Action> recommendedActions;

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
                List<Event> events,
                List<Action> recommendedActions) {
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
            this.recommendedActions = recommendedActions;
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
        final int occurrenceCount;

        Event(
                String severityLabel,
                String source,
                String occurredAt,
                String summary,
                int tone,
                int occurrenceCount) {
            this.severityLabel = severityLabel;
            this.source = source;
            this.occurredAt = occurredAt;
            this.summary = summary;
            this.tone = tone;
            this.occurrenceCount = occurrenceCount;
        }

        String metadataLabel() {
            StringBuilder label = new StringBuilder(severityLabel)
                    .append(" · ").append(source);
            if (!occurredAt.isEmpty()) {
                label.append(" · ").append(occurredAt);
            }
            if (occurrenceCount > 1) {
                label.append(" · ").append(occurrenceCount).append(" 次");
            }
            return label.toString();
        }

        String accessibilityLabel() {
            return metadataLabel().replace(" · ", "，") + "。" + summary;
        }

        boolean matches(Event other) {
            return severityLabel.equals(other.severityLabel)
                    && source.equals(other.source)
                    && summary.equals(other.summary)
                    && tone == other.tone;
        }

        Event withOccurrenceCount(int count) {
            return new Event(
                    severityLabel, source, occurredAt, summary, tone, count);
        }
    }

    static final class Action {
        final String actionId;
        final String title;
        final String summary;

        Action(String actionId, String title, String summary) {
            this.actionId = actionId;
            this.title = title;
            this.summary = summary;
        }

        String accessibilityLabel() {
            return title + "。" + summary;
        }
    }
}

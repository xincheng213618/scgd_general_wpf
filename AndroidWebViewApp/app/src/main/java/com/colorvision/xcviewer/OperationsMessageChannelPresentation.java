package com.colorvision.xcviewer;

import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

final class OperationsMessageChannelPresentation {
    static final int TONE_HEALTHY = 0;
    static final int TONE_ATTENTION = 1;
    static final int TONE_UNAVAILABLE = 2;

    private static final String STATE_CONNECTED = "connected";
    private static final String STATE_DEGRADED = "degraded";
    private static final String STATE_DISCONNECTED = "disconnected";
    private static final String STATE_UNCONFIGURED = "unconfigured";
    private static final int MAXIMUM_COUNT = 9999;

    private OperationsMessageChannelPresentation() {
    }

    static ViewModel from(JSONObject payload, TimeFormatter timeFormatter) {
        if (payload == null || !payload.optBoolean("available", false)) {
            return unavailable();
        }

        String reportedState = payload.optString("state", "unavailable");
        boolean knownState = STATE_CONNECTED.equals(reportedState)
                || STATE_DEGRADED.equals(reportedState)
                || STATE_DISCONNECTED.equals(reportedState)
                || STATE_UNCONFIGURED.equals(reportedState);
        boolean hasExplicitState = payload.has("configured")
                && payload.has("connected")
                && payload.has("subscriptionReady");
        if (!knownState && !hasExplicitState) {
            return unavailable();
        }
        boolean configured = payload.has("configured")
                ? payload.optBoolean("configured", false)
                : !STATE_UNCONFIGURED.equals(reportedState);
        boolean connected = configured && payload.optBoolean(
                "connected",
                STATE_CONNECTED.equals(reportedState) || STATE_DEGRADED.equals(reportedState));
        int registered = boundedCount(payload.optInt("registeredSubscriptionCount", 0));
        int active = boundedCount(payload.optInt("activeSubscriptionCount", 0));
        boolean subscriptionReady = connected && payload.optBoolean(
                "subscriptionReady", active >= registered);
        String state = !configured
                ? STATE_UNCONFIGURED
                : !connected
                        ? STATE_DISCONNECTED
                        : subscriptionReady ? STATE_CONNECTED : STATE_DEGRADED;

        List<ActivityItem> activityItems = new ArrayList<>();
        addActivity(activityItems, "最近连接",
                timeFormatter.format(payload.optString("lastConnectedAt", "")));
        addActivity(activityItems, "最近断开",
                timeFormatter.format(payload.optString("lastDisconnectedAt", "")));
        addActivity(activityItems, "最近接收活动",
                timeFormatter.format(payload.optString("lastInboundActivityAt", "")));
        addActivity(activityItems, "最近发送活动",
                timeFormatter.format(payload.optString("lastOutboundActivityAt", "")));
        addActivity(activityItems, "本次观测",
                timeFormatter.format(payload.optString("observedAt", "")));

        boolean canRecover = STATE_DISCONNECTED.equals(state) || STATE_DEGRADED.equals(state);
        return new ViewModel(
                true,
                stateLabel(state),
                summary(state, active, registered),
                connected ? "已建立" : "未建立",
                subscriptionReady
                        ? active + "/" + registered + " · 已就绪"
                        : active + "/" + registered + " · 未就绪",
                recoverySummary(state),
                privacyNotice(),
                STATE_CONNECTED.equals(state) ? TONE_HEALTHY : TONE_ATTENTION,
                canRecover,
                Collections.unmodifiableList(activityItems));
    }

    private static ViewModel unavailable() {
        return new ViewModel(
                false,
                "消息通道状态不可用",
                "当前无法读取 ColorVision 消息客户端状态。",
                "连接状态尚未确认",
                "订阅状态尚未确认",
                "恢复安全直连后可重新读取；不会仅凭未知状态执行恢复。",
                privacyNotice(),
                TONE_UNAVAILABLE,
                false,
                Collections.emptyList());
    }

    private static int boundedCount(int value) {
        return Math.min(MAXIMUM_COUNT, Math.max(0, value));
    }

    private static void addActivity(List<ActivityItem> items, String label, String value) {
        if (value != null && !value.isEmpty()) {
            items.add(new ActivityItem(label, value));
        }
    }

    private static String stateLabel(String state) {
        switch (state) {
            case STATE_CONNECTED:
                return "消息通道正常";
            case STATE_DEGRADED:
                return "消息订阅需要恢复";
            case STATE_DISCONNECTED:
                return "消息连接已断开";
            case STATE_UNCONFIGURED:
                return "消息通道尚未配置";
            default:
                return "消息通道状态不可用";
        }
    }

    private static String summary(String state, int active, int registered) {
        switch (state) {
            case STATE_CONNECTED:
                return "连接和已登记订阅均已就绪";
            case STATE_DEGRADED:
                return "已连接，但只恢复了 " + active + '/' + registered + " 个已登记订阅";
            case STATE_DISCONNECTED:
                return "ColorVision 当前没有建立消息服务连接";
            case STATE_UNCONFIGURED:
                return "电脑端当前没有有效的消息服务连接配置";
            default:
                return "当前无法确认消息连接与订阅状态";
        }
    }

    private static String recoverySummary(String state) {
        switch (state) {
            case STATE_CONNECTED:
                return "当前通道健康，无需执行恢复。";
            case STATE_DEGRADED:
                return "确认后只使用电脑当前配置恢复已登记订阅；不会修改地址、Topic 或凭据。";
            case STATE_DISCONNECTED:
                return "确认后只使用电脑当前配置重建 ColorVision 消息连接并恢复已登记订阅。";
            case STATE_UNCONFIGURED:
                return "需要先在电脑端完成配置；手机不会填写地址、端口、Topic 或凭据。";
            default:
                return "状态未确认时不开放恢复；请先刷新消息通道。";
        }
    }

    private static String privacyNotice() {
        return "只显示 ColorVision 客户端的规范化连接状态、订阅计数和聚合活动时间；"
                + "不返回地址、端口、端点、Topic、消息载荷、客户端或设备标识、"
                + "配置、凭据、证书或原始日志。";
    }

    interface TimeFormatter {
        String format(String value);
    }

    static final class ViewModel {
        final boolean available;
        final String stateLabel;
        final String summary;
        final String connectionLabel;
        final String subscriptionLabel;
        final String recoverySummary;
        final String privacyNotice;
        final int tone;
        final boolean canRecover;
        final List<ActivityItem> activityItems;

        ViewModel(
                boolean available,
                String stateLabel,
                String summary,
                String connectionLabel,
                String subscriptionLabel,
                String recoverySummary,
                String privacyNotice,
                int tone,
                boolean canRecover,
                List<ActivityItem> activityItems) {
            this.available = available;
            this.stateLabel = stateLabel;
            this.summary = summary;
            this.connectionLabel = connectionLabel;
            this.subscriptionLabel = subscriptionLabel;
            this.recoverySummary = recoverySummary;
            this.privacyNotice = privacyNotice;
            this.tone = tone;
            this.canRecover = canRecover;
            this.activityItems = activityItems;
        }
    }

    static final class ActivityItem {
        final String label;
        final String value;

        ActivityItem(String label, String value) {
            this.label = label;
            this.value = value;
        }

        String accessibilityLabel() {
            return label + "，" + value;
        }
    }
}

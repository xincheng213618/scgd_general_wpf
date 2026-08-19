package com.colorvision.xcviewer;

import java.util.List;

final class OperationsProfileOverview {
    private static final long MINUTE_MILLISECONDS = 60_000L;
    private static final long HOUR_MILLISECONDS = 60L * MINUTE_MILLISECONDS;
    private static final long DAY_MILLISECONDS = 24L * HOUR_MILLISECONDS;

    private OperationsProfileOverview() {
    }

    static String summary(
            String serializedHistory,
            long lastCheckedAt,
            boolean revoked,
            long nowMilliseconds) {
        if (revoked) {
            return "授权失效 · 点按移除";
        }
        List<OperationsWatchHistory.Entry> entries = OperationsWatchHistory.parse(
                serializedHistory, nowMilliseconds);
        OperationsWatchHistory.Entry latest = entries.isEmpty()
                ? null : entries.get(entries.size() - 1);
        if (latest == null && lastCheckedAt <= 0L) {
            return "尚未检查 · 切换后自动守护";
        }
        String state = latest == null ? "状态未知" : compactStateLabel(latest.state);
        if (lastCheckedAt > 0L) {
            return state + " · " + relativeTime(lastCheckedAt, nowMilliseconds, "检查");
        }
        return state + " · " + relativeTime(latest.timestampMilliseconds,
                nowMilliseconds, "记录");
    }

    private static String compactStateLabel(String state) {
        if (OperationsWatchHistory.STATE_ONLINE.equals(state)) {
            return "现场在线";
        }
        if (OperationsWatchHistory.STATE_REMOTE_ONLINE.equals(state)) {
            return "远程在线";
        }
        if (OperationsWatchHistory.STATE_REMOTE_WAITING.equals(state)) {
            return "等待电脑上线";
        }
        if (OperationsWatchHistory.STATE_OFFLINE.equals(state)) {
            return "连接中断";
        }
        if (OperationsWatchHistory.STATE_REVOKED.equals(state)) {
            return "授权失效";
        }
        String attentionKey = OperationsWatchHistory.attentionKey(state);
        if (OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE.equals(attentionKey)) {
            return "主界面无响应";
        }
        if (OperationsWatchPolicy.ATTENTION_CRITICAL.equals(attentionKey)) {
            return "严重告警";
        }
        if (OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL.equals(attentionKey)) {
            return "消息需关注";
        }
        if (OperationsWatchPolicy.ATTENTION_DEVICES.equals(attentionKey)) {
            return "设备需关注";
        }
        if (OperationsWatchPolicy.ATTENTION_ERRORS.equals(attentionKey)) {
            return "错误事件";
        }
        return "状态未知";
    }

    private static String relativeTime(long timestamp, long nowMilliseconds, String event) {
        long age = timestamp >= nowMilliseconds ? 0L : nowMilliseconds - timestamp;
        if (age < MINUTE_MILLISECONDS) {
            return "刚刚" + event;
        }
        if (age < HOUR_MILLISECONDS) {
            return Math.max(1L, age / MINUTE_MILLISECONDS) + " 分钟前" + event;
        }
        if (age < DAY_MILLISECONDS) {
            return Math.max(1L, age / HOUR_MILLISECONDS) + " 小时前" + event;
        }
        if (age < OperationsWatchHistory.RETENTION_MILLISECONDS) {
            return Math.max(1L, age / DAY_MILLISECONDS) + " 天前" + event;
        }
        return "超过 7 天未" + event;
    }
}

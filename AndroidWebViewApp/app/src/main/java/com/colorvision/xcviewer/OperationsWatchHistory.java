package com.colorvision.xcviewer;

import java.util.ArrayList;
import java.util.List;

final class OperationsWatchHistory {
    static final String STATE_ONLINE = "online";
    static final String STATE_REMOTE_ONLINE = "remote-online";
    static final String STATE_REMOTE_WAITING = "remote-waiting";
    static final String STATE_OFFLINE = "offline";
    static final String STATE_REVOKED = "revoked";
    static final int MAX_ENTRIES = 40;
    static final long RETENTION_MILLISECONDS = 7L * 24L * 60L * 60L * 1000L;
    private static final String ATTENTION_PREFIX = "attention:";

    private OperationsWatchHistory() {
    }

    static String attentionState(String attentionKey) {
        if (OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE.equals(attentionKey)
                || OperationsWatchPolicy.ATTENTION_CRITICAL.equals(attentionKey)
                || OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL.equals(attentionKey)
                || OperationsWatchPolicy.ATTENTION_DEVICES.equals(attentionKey)
                || OperationsWatchPolicy.ATTENTION_ERRORS.equals(attentionKey)) {
            return ATTENTION_PREFIX + attentionKey;
        }
        return "";
    }

    static boolean isOnlineState(String state) {
        String normalized = normalizeState(state);
        return STATE_ONLINE.equals(normalized)
                || STATE_REMOTE_ONLINE.equals(normalized)
                || STATE_REMOTE_WAITING.equals(normalized)
                || normalized.startsWith(ATTENTION_PREFIX);
    }

    static String attentionKey(String state) {
        String normalized = normalizeState(state);
        return normalized.startsWith(ATTENTION_PREFIX)
                ? normalized.substring(ATTENTION_PREFIX.length())
                : "";
    }

    static Transition transition(String serializedHistory, String requestedState, long nowMilliseconds) {
        String currentState = normalizeState(requestedState);
        List<Entry> entries = parse(serializedHistory, nowMilliseconds);
        String normalizedPrevious = entries.isEmpty()
                ? ""
                : entries.get(entries.size() - 1).state;
        boolean changed = !currentState.isEmpty() && !currentState.equals(normalizedPrevious);
        if (changed) {
            entries.add(new Entry(nowMilliseconds, currentState));
            while (entries.size() > MAX_ENTRIES) {
                entries.remove(0);
            }
            normalizedPrevious = currentState;
        }
        return new Transition(normalizedPrevious, serialize(entries), changed);
    }

    static List<Entry> parse(String serializedHistory, long nowMilliseconds) {
        if (serializedHistory == null || serializedHistory.isEmpty()) {
            return new ArrayList<>();
        }
        long cutoff = nowMilliseconds - RETENTION_MILLISECONDS;
        long latestAllowed = nowMilliseconds + 24L * 60L * 60L * 1000L;
        List<Entry> entries = new ArrayList<>();
        for (String line : serializedHistory.split("\n")) {
            int separator = line.indexOf('|');
            if (separator <= 0 || separator == line.length() - 1) {
                continue;
            }
            try {
                long timestamp = Long.parseLong(line.substring(0, separator));
                String state = normalizeState(line.substring(separator + 1));
                if (!state.isEmpty() && timestamp >= cutoff && timestamp <= latestAllowed) {
                    entries.add(new Entry(timestamp, state));
                }
            } catch (NumberFormatException ignored) {
            }
        }
        if (entries.size() > MAX_ENTRIES) {
            entries = new ArrayList<>(entries.subList(entries.size() - MAX_ENTRIES, entries.size()));
        }
        return entries;
    }

    static String label(String state) {
        String normalized = normalizeState(state);
        switch (normalized) {
            case STATE_ONLINE:
                return "连接在线 · 当前状态稳定";
            case STATE_REMOTE_ONLINE:
                return "远程中继在线 · 电脑已连接";
            case STATE_REMOTE_WAITING:
                return "远程中继在线 · 等待电脑上线";
            case STATE_OFFLINE:
                return "连接中断 · 后台自动重试";
            case STATE_REVOKED:
                return "配对授权已失效";
            case ATTENTION_PREFIX + OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE:
                return "在线 · 主界面响应超时";
            case ATTENTION_PREFIX + OperationsWatchPolicy.ATTENTION_CRITICAL:
                return "在线 · 发现严重告警";
            case ATTENTION_PREFIX + OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL:
                return "在线 · 消息通道需要关注";
            case ATTENTION_PREFIX + OperationsWatchPolicy.ATTENTION_DEVICES:
                return "在线 · 检测设备需要关注";
            case ATTENTION_PREFIX + OperationsWatchPolicy.ATTENTION_ERRORS:
                return "在线 · 发现错误事件";
            default:
                return "未知状态";
        }
    }

    private static String normalizeState(String state) {
        if (STATE_ONLINE.equals(state)
                || STATE_REMOTE_ONLINE.equals(state)
                || STATE_REMOTE_WAITING.equals(state)
                || STATE_OFFLINE.equals(state)
                || STATE_REVOKED.equals(state)) {
            return state;
        }
        if (state != null && state.startsWith(ATTENTION_PREFIX)
                && !attentionState(state.substring(ATTENTION_PREFIX.length())).isEmpty()) {
            return state;
        }
        return "";
    }

    private static String serialize(List<Entry> entries) {
        StringBuilder value = new StringBuilder();
        for (Entry entry : entries) {
            if (value.length() > 0) {
                value.append('\n');
            }
            value.append(entry.timestampMilliseconds).append('|').append(entry.state);
        }
        return value.toString();
    }

    static final class Entry {
        final long timestampMilliseconds;
        final String state;

        Entry(long timestampMilliseconds, String state) {
            this.timestampMilliseconds = timestampMilliseconds;
            this.state = state;
        }
    }

    static final class Transition {
        final String currentState;
        final String serializedHistory;
        final boolean changed;

        Transition(String currentState, String serializedHistory, boolean changed) {
            this.currentState = currentState;
            this.serializedHistory = serializedHistory;
            this.changed = changed;
        }
    }
}

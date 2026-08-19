package com.colorvision.xcviewer;

final class OperationsAttentionFocus {
    private OperationsAttentionFocus() {
    }

    static String normalize(String attentionKey) {
        if (OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE.equals(attentionKey)
                || OperationsWatchPolicy.ATTENTION_CRITICAL.equals(attentionKey)
                || OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL.equals(attentionKey)
                || OperationsWatchPolicy.ATTENTION_DEVICES.equals(attentionKey)
                || OperationsWatchPolicy.ATTENTION_ERRORS.equals(attentionKey)) {
            return attentionKey;
        }
        return "";
    }

    static String fromWatchState(String watchState) {
        return normalize(OperationsWatchHistory.attentionKey(watchState));
    }

    static boolean matchesFinding(String attentionKey, String category, String severity) {
        String normalized = normalize(attentionKey);
        String safeCategory = category == null ? "" : category;
        String safeSeverity = severity == null ? "" : severity;
        switch (normalized) {
            case OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE:
                return "desktop".equals(safeCategory)
                        || "failure-evidence".equals(safeCategory);
            case OperationsWatchPolicy.ATTENTION_CRITICAL:
                return "critical".equals(safeSeverity);
            case OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL:
                return "message-channel".equals(safeCategory)
                        || "message-service".equals(safeCategory);
            case OperationsWatchPolicy.ATTENTION_DEVICES:
                return "devices".equals(safeCategory);
            case OperationsWatchPolicy.ATTENTION_ERRORS:
                return "error".equals(safeSeverity);
            default:
                return false;
        }
    }

    static boolean matchesRemoteSection(String attentionKey, String section) {
        String normalized = normalize(attentionKey);
        String safeSection = section == null ? "" : section;
        switch (normalized) {
            case OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE:
                return "performance".equals(safeSection);
            case OperationsWatchPolicy.ATTENTION_CRITICAL:
            case OperationsWatchPolicy.ATTENTION_ERRORS:
                return "alerts".equals(safeSection);
            case OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL:
                return "message".equals(safeSection);
            case OperationsWatchPolicy.ATTENTION_DEVICES:
                return "devices".equals(safeSection);
            default:
                return false;
        }
    }

    static String contextMessage(
            String attentionKey, boolean found, boolean currentStateAvailable) {
        String normalized = normalize(attentionKey);
        if (normalized.isEmpty()) {
            return "";
        }
        String label = label(normalized);
        if (!currentStateAvailable) {
            return "来自后台提醒 · 当前脱敏状态暂不可用，尚不能确认“"
                    + label + "”是否仍存在。";
        }
        if (found) {
            return "来自后台提醒 · 已定位“" + label + "”相关证据并优先显示。";
        }
        return "来自后台提醒 · 当前证据中已不再发现“" + label
                + "”相关问题，状态可能已恢复或变化。";
    }

    static String label(String attentionKey) {
        switch (normalize(attentionKey)) {
            case OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE:
                return "主界面无响应";
            case OperationsWatchPolicy.ATTENTION_CRITICAL:
                return "严重告警";
            case OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL:
                return "消息通道";
            case OperationsWatchPolicy.ATTENTION_DEVICES:
                return "检测设备";
            case OperationsWatchPolicy.ATTENTION_ERRORS:
                return "错误事件";
            default:
                return "";
        }
    }
}

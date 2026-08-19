package com.colorvision.xcviewer;

final class OperationsWatchPolicy {
    static final String ATTENTION_UI_UNRESPONSIVE = "ui_unresponsive";
    static final String ATTENTION_CRITICAL = "critical";
    static final String ATTENTION_MESSAGE_CHANNEL = "message_channel";
    static final String ATTENTION_DEVICES = "devices";
    static final String ATTENTION_ERRORS = "errors";
    static final String ATTENTION_OFFLINE = "offline";
    static final String ATTENTION_REVOKED = "revoked";
    static final String DESTINATION_TRIAGE = "triage";
    static final String DESTINATION_CONNECTION_CHECK = "connection_check";
    static final String DESTINATION_CONNECTIONS = "connections";
    static final long HEALTHY_CHECK_MILLISECONDS = 60_000L;
    static final long FIRST_RETRY_MILLISECONDS = 30_000L;
    static final long MAXIMUM_RETRY_MILLISECONDS = 5 * 60_000L;
    static final int OFFLINE_CONFIRMATION_FAILURES = 3;
    static final long OFFLINE_CONFIRMATION_MILLISECONDS = 60_000L;

    private OperationsWatchPolicy() {
    }

    static long retryDelayMilliseconds(int consecutiveFailures) {
        int boundedFailures = Math.max(1, Math.min(consecutiveFailures, 5));
        long delay = FIRST_RETRY_MILLISECONDS << (boundedFailures - 1);
        return Math.min(delay, MAXIMUM_RETRY_MILLISECONDS);
    }

    static boolean shouldConfirmOffline(
            int consecutiveFailures,
            long firstFailureAtElapsedMilliseconds,
            long nowElapsedMilliseconds) {
        return consecutiveFailures >= OFFLINE_CONFIRMATION_FAILURES
                && firstFailureAtElapsedMilliseconds > 0L
                && nowElapsedMilliseconds >= firstFailureAtElapsedMilliseconds
                && nowElapsedMilliseconds - firstFailureAtElapsedMilliseconds
                >= OFFLINE_CONFIRMATION_MILLISECONDS;
    }

    static String healthyStatus(
            String uiState,
            boolean flowActive,
            int criticalCount,
            int errorCount,
            int deviceAttentionCount,
            boolean messageChannelAttention) {
        if ("unresponsive".equals(uiState)) {
            return "在线 · 主界面响应超时";
        }
        if (criticalCount > 0) {
            return "在线 · 发现严重告警";
        }
        if (messageChannelAttention) {
            return "在线 · 消息通道需要关注";
        }
        if (deviceAttentionCount > 0) {
            return "在线 · 检测设备需要关注";
        }
        if (errorCount > 0) {
            return "在线 · 发现错误事件";
        }
        if ("slow".equals(uiState)) {
            return "在线 · 主界面响应偏慢";
        }
        if (flowActive) {
            return "在线 · 检测正在进行";
        }
        return "在线 · 当前状态稳定";
    }

    static String successfulCheckNotification(String status, boolean reconnected) {
        return (reconnected ? "连接已恢复 · " : "") + status + " · 刚刚检查";
    }

    static String attentionKey(
            String uiState,
            int criticalCount,
            int errorCount,
            int deviceAttentionCount,
            boolean messageChannelAttention) {
        if ("unresponsive".equals(uiState)) {
            return ATTENTION_UI_UNRESPONSIVE;
        }
        if (criticalCount > 0) {
            return ATTENTION_CRITICAL;
        }
        if (messageChannelAttention) {
            return ATTENTION_MESSAGE_CHANNEL;
        }
        if (deviceAttentionCount > 0) {
            return ATTENTION_DEVICES;
        }
        if (errorCount > 0) {
            return ATTENTION_ERRORS;
        }
        return "";
    }

    static String attentionMessage(String attentionKey, boolean newEvidence) {
        String message;
        switch (attentionKey) {
            case ATTENTION_UI_UNRESPONSIVE:
                message = "主界面响应超时 · 点击进入问题中心";
                break;
            case ATTENTION_CRITICAL:
                message = "发现严重告警 · 点击查看脱敏证据";
                break;
            case ATTENTION_MESSAGE_CHANNEL:
                message = "消息通道需要关注 · 可在手机确认恢复";
                break;
            case ATTENTION_DEVICES:
                message = "检测设备状态需要关注 · 点击查看汇总";
                break;
            case ATTENTION_ERRORS:
                message = "发现错误事件 · 点击进入问题中心";
                break;
            case ATTENTION_OFFLINE:
                message = "已配对主机连接中断 · 后台正在自动重试";
                break;
            case ATTENTION_REVOKED:
                message = "配对授权已失效 · 点击管理已配对电脑";
                break;
            default:
                return "";
        }
        return newEvidence ? "同类异常出现新证据 · " + message : message;
    }

    static String attentionDestination(String attentionKey) {
        switch (attentionKey) {
            case ATTENTION_UI_UNRESPONSIVE:
            case ATTENTION_CRITICAL:
            case ATTENTION_MESSAGE_CHANNEL:
            case ATTENTION_DEVICES:
            case ATTENTION_ERRORS:
                return DESTINATION_TRIAGE;
            case ATTENTION_OFFLINE:
                return DESTINATION_CONNECTION_CHECK;
            case ATTENTION_REVOKED:
                return DESTINATION_CONNECTIONS;
            default:
                return "";
        }
    }

    static String normalizeDestination(String destination) {
        if (DESTINATION_TRIAGE.equals(destination)
                || DESTINATION_CONNECTION_CHECK.equals(destination)
                || DESTINATION_CONNECTIONS.equals(destination)) {
            return destination;
        }
        return "";
    }

    static boolean shouldPostAttention(
            String currentAttentionKey,
            String lastAttentionKey,
            OperationsMonitorEvidenceRevision.Evidence currentEvidence,
            OperationsMonitorEvidenceRevision.Evidence lastEvidence) {
        if (currentAttentionKey.isEmpty()) {
            return false;
        }
        if (!currentAttentionKey.equals(lastAttentionKey)) {
            return true;
        }
        return isEvidenceUpdate(
                currentAttentionKey,
                lastAttentionKey,
                currentEvidence,
                lastEvidence);
    }

    static boolean isEvidenceUpdate(
            String currentAttentionKey,
            String lastAttentionKey,
            OperationsMonitorEvidenceRevision.Evidence currentEvidence,
            OperationsMonitorEvidenceRevision.Evidence lastEvidence) {
        return !currentAttentionKey.isEmpty()
                && currentAttentionKey.equals(lastAttentionKey)
                && currentEvidence != null
                && lastEvidence != null
                && currentEvidence.available()
                && lastEvidence.available()
                && !currentEvidence.revision.equals(lastEvidence.revision)
                && (currentEvidence.sequence > lastEvidence.sequence
                || currentEvidence.sequence == lastEvidence.sequence
                && currentEvidence.burden > lastEvidence.burden);
    }

    static boolean shouldPostOffline(
            boolean previousStateOnline,
            boolean offlineJustConfirmed,
            String lastAttentionKey) {
        return previousStateOnline
                && offlineJustConfirmed
                && !ATTENTION_OFFLINE.equals(lastAttentionKey);
    }

    static boolean isCurrentProfileCheck(
            String expectedHostId, String activeHostId, int generation, int activeGeneration) {
        return generation == activeGeneration
                && expectedHostId != null
                && expectedHostId.equals(activeHostId);
    }
}

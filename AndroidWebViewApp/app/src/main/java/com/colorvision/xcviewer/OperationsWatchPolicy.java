package com.colorvision.xcviewer;

final class OperationsWatchPolicy {
    static final long HEALTHY_CHECK_MILLISECONDS = 60_000L;
    static final long FIRST_RETRY_MILLISECONDS = 30_000L;
    static final long MAXIMUM_RETRY_MILLISECONDS = 5 * 60_000L;

    private OperationsWatchPolicy() {
    }

    static long retryDelayMilliseconds(int consecutiveFailures) {
        int boundedFailures = Math.max(1, Math.min(consecutiveFailures, 5));
        long delay = FIRST_RETRY_MILLISECONDS << (boundedFailures - 1);
        return Math.min(delay, MAXIMUM_RETRY_MILLISECONDS);
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
}

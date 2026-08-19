package com.colorvision.xcviewer;

final class OperationsRecoveryOverview {
    private static final int MAX_REASON_LENGTH = 72;

    private OperationsRecoveryOverview() {
    }

    static String failureSummary(String directFailure, String relayFailure) {
        String directReason = compactReason(directFailure);
        String relayReason = compactReason(relayFailure);
        String channels = directReason.equals(relayReason)
                ? "现场直连与固定中继当前均不可达"
                : "现场直连：" + directReason + "\n固定中继：" + relayReason;
        return channels + "\n\n配对资料已安全保留，无需重新扫码。";
    }

    static String waitingStatus() {
        return "电脑暂时不可达 · 将自动重试";
    }

    static String checkingStatus() {
        return "正在自动重试安全连接…";
    }

    static String automaticRetryNote() {
        return "停留在此页面时每 30 秒自动重试；离开后后台守护仍会保留配对并继续检查。";
    }

    private static String compactReason(String value) {
        if (value == null || value.trim().isEmpty()) {
            return "暂不可达";
        }
        String normalized = value.trim().replaceAll("\\s+", " ");
        int sentenceEnd = normalized.indexOf('。');
        if (sentenceEnd > 0) {
            normalized = normalized.substring(0, sentenceEnd);
        }
        if (normalized.length() > MAX_REASON_LENGTH) {
            return normalized.substring(0, MAX_REASON_LENGTH - 1) + "…";
        }
        return normalized;
    }
}

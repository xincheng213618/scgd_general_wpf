package com.colorvision.xcviewer;

final class OperationsRecoveryOverview {
    private static final int MAX_REASON_LENGTH = 72;

    private OperationsRecoveryOverview() {
    }

    static String failureSummary(String directFailure, String relayFailure) {
        return "现场直连：" + compactReason(directFailure)
                + "\n固定中继：" + compactReason(relayFailure)
                + "\n\n配对资料已保留，无需重新扫码。";
    }

    static String pairingRemovalNote() {
        return "配对资料包含当前电脑的设备密钥。只有确认不再使用这台电脑时才移除。";
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

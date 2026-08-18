package com.colorvision.xcviewer;

final class OperationsDashboardFreshness {
    static final int TONE_NORMAL = 0;
    static final int TONE_ATTENTION = 1;
    static final int TONE_MUTED = 2;

    private OperationsDashboardFreshness() {
    }

    static Presentation loading() {
        return new Presentation("状态时间 · 正在读取", TONE_MUTED);
    }

    static Presentation updated(String timeLabel, boolean relay, boolean sourceFresh) {
        String time = safe(timeLabel, "时间未知");
        if (!relay) {
            return new Presentation("状态更新 · " + time + " · 现场直连", TONE_NORMAL);
        }
        return sourceFresh
                ? new Presentation("电脑签名状态 · " + time + " · 在线", TONE_NORMAL)
                : new Presentation(
                        "电脑签名状态 · " + time + " · 可能已过期", TONE_ATTENTION);
    }

    static Presentation unavailable(String reason, String lastSuccessfulTimeLabel) {
        String detail = safe(reason, "读取失败");
        String lastSuccess = lastSuccessfulTimeLabel == null
                || lastSuccessfulTimeLabel.trim().isEmpty()
                ? "尚无成功摘要"
                : "上次成功 " + lastSuccessfulTimeLabel.trim();
        return new Presentation(
                "状态未更新 · " + detail + " · " + lastSuccess,
                TONE_ATTENTION);
    }

    private static String safe(String value, String fallback) {
        return value == null || value.trim().isEmpty() ? fallback : value.trim();
    }

    static final class Presentation {
        final String label;
        final int tone;

        Presentation(String label, int tone) {
            this.label = label;
            this.tone = tone;
        }
    }
}

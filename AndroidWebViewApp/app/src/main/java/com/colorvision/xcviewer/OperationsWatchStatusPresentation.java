package com.colorvision.xcviewer;

final class OperationsWatchStatusPresentation {
    static final long STALE_AFTER_MILLISECONDS = OperationsFleetOverview.RECENT_CHECK_MILLISECONDS;
    private static final long MAXIMUM_FUTURE_SKEW_MILLISECONDS = 60_000L;

    private OperationsWatchStatusPresentation() {
    }

    static Presentation create(
            boolean paired,
            boolean enabled,
            String state,
            long checkedAtMilliseconds,
            long nowMilliseconds) {
        if (!paired) {
            return new Presentation(
                    "配对后开始记录",
                    "配对电脑并开启持续守护后，这里会显示最近一次后台检查及状态变化。",
                    false);
        }
        if (!enabled) {
            return new Presentation(
                    "持续守护已关闭",
                    "当前不会进行后台检查。重新开启持续守护后，可在这里立即检查或查看此前的本机时间线。",
                    false);
        }
        String normalizedState = state == null ? "" : state.trim();
        if (checkedAtMilliseconds <= 0L || normalizedState.isEmpty()) {
            return new Presentation(
                    "等待首次后台检查",
                    "持续守护已开启，但还没有完成第一轮后台检查。可以立即检查；结果只保存为本机脱敏状态记录。",
                    false);
        }
        if (checkedAtMilliseconds > nowMilliseconds + MAXIMUM_FUTURE_SKEW_MILLISECONDS) {
            return new Presentation(
                    "检查时间记录异常",
                    "最近检查时间晚于手机当前时间，不能据此判断守护是否正常。请校准手机时间后立即检查。",
                    true);
        }

        long ageMilliseconds = Math.max(0L, nowMilliseconds - checkedAtMilliseconds);
        String stateLabel = OperationsWatchHistory.label(normalizedState);
        if (ageMilliseconds > STALE_AFTER_MILLISECONDS) {
            return new Presentation(
                    "超过 10 分钟未更新",
                    "持续守护仍处于开启偏好，但后台状态已经超过 10 分钟未更新。最近记录："
                            + stateLabel + "。可以立即检查以恢复守护。",
                    true);
        }
        String checkedLabel = checkedLabel(ageMilliseconds);
        return new Presentation(
                checkedLabel + " · " + stateLabel,
                completedCheckLabel(ageMilliseconds) + "当前记录：" + stateLabel
                        + "。异常首次出现或同类状态产生新脱敏证据时会发送运维提醒；"
                        + "普通轮询时间变化不会重复打扰。",
                false);
    }

    private static String checkedLabel(long ageMilliseconds) {
        long minutes = ageMilliseconds / 60_000L;
        if (minutes <= 0L) {
            return "刚刚检查";
        }
        return minutes + " 分钟前检查";
    }

    private static String completedCheckLabel(long ageMilliseconds) {
        long minutes = ageMilliseconds / 60_000L;
        if (minutes <= 0L) {
            return "最近一轮后台检查刚刚完成。";
        }
        return "最近一轮后台检查在 " + minutes + " 分钟前完成。";
    }

    static final class Presentation {
        final String summary;
        final String details;
        final boolean attention;

        Presentation(String summary, String details, boolean attention) {
            this.summary = summary;
            this.details = details;
            this.attention = attention;
        }
    }
}

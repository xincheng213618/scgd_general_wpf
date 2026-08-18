package com.colorvision.xcviewer;

final class OperationsWatchPreferencePolicy {
    private OperationsWatchPreferencePolicy() {
    }

    static boolean shouldRun(boolean hasOperationsProfile, boolean userEnabled) {
        return hasOperationsProfile && userEnabled;
    }

    static String status(
            boolean hasOperationsProfile,
            boolean userEnabled,
            boolean remindersAvailable) {
        if (!userEnabled) {
            return "已关闭";
        }
        if (!hasOperationsProfile) {
            return "配对后自动启动";
        }
        return remindersAvailable
                ? "后台检查与异常提醒已开启"
                : "后台检查已开启 · 提醒未开启";
    }

    static String enabledFeedback(
            boolean hasOperationsProfile,
            boolean remindersAvailable) {
        if (!hasOperationsProfile) {
            return "配对电脑后将自动开启持续守护";
        }
        return remindersAvailable
                ? "后台检查与运维提醒已开启"
                : "后台检查已开启；提醒尚未开启";
    }

    static boolean shouldOfferReminderAction(
            boolean hasOperationsProfile,
            boolean remindersAvailable) {
        return hasOperationsProfile && !remindersAvailable;
    }
}

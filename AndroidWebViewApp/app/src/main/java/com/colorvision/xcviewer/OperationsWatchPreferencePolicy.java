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
            boolean remindersAvailable,
            int usableProfileCount) {
        if (!userEnabled) {
            return "已关闭";
        }
        if (!hasOperationsProfile) {
            return "配对后自动启动";
        }
        if (usableProfileCount > 1) {
            return remindersAvailable
                    ? usableProfileCount + " 台电脑后台轮巡与异常提醒已开启"
                    : usableProfileCount + " 台电脑后台轮巡已开启 · 提醒未开启";
        }
        return remindersAvailable
                ? "后台检查与异常提醒已开启"
                : "后台检查已开启 · 提醒未开启";
    }

    static String enabledFeedback(
            boolean hasOperationsProfile,
            boolean remindersAvailable,
            int usableProfileCount) {
        if (!hasOperationsProfile) {
            return "配对电脑后将自动开启持续守护";
        }
        if (usableProfileCount > 1) {
            return remindersAvailable
                    ? usableProfileCount + " 台电脑后台轮巡与运维提醒已开启"
                    : usableProfileCount + " 台电脑后台轮巡已开启；提醒尚未开启";
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

    static String fleetScopeDetails(int usableProfileCount) {
        return usableProfileCount > 1
                ? "当前电脑每分钟检查；其他已配对电脑在超过 10 分钟未更新时依次补查。"
                        + "提醒会标明具体电脑，点开后才切换操作目标。"
                : "";
    }
}

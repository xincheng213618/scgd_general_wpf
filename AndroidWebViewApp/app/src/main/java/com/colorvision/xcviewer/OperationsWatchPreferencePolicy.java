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
            int usableProfileCount,
            int remindersEnabledProfileCount) {
        if (!userEnabled) {
            return "已关闭";
        }
        if (!hasOperationsProfile) {
            return "配对后自动启动";
        }
        int reminderCount = reminderCount(
                usableProfileCount, remindersEnabledProfileCount);
        if (usableProfileCount > 1) {
            if (!remindersAvailable) {
                return usableProfileCount + " 台电脑后台轮巡已开启 · 提醒未开启";
            }
            if (reminderCount == 0) {
                return usableProfileCount + " 台电脑后台轮巡已开启 · 每台提醒均暂停";
            }
            return reminderCount == usableProfileCount
                    ? usableProfileCount + " 台电脑后台轮巡与异常提醒已开启"
                    : usableProfileCount + " 台电脑后台轮巡 · "
                            + reminderCount + " 台异常提醒已开启";
        }
        if (!remindersAvailable) {
            return "后台检查已开启 · 提醒未开启";
        }
        return reminderCount > 0
                ? "后台检查与异常提醒已开启"
                : "后台检查已开启 · 当前电脑提醒已暂停";
    }

    static String enabledFeedback(
            boolean hasOperationsProfile,
            boolean remindersAvailable,
            int usableProfileCount,
            int remindersEnabledProfileCount) {
        if (!hasOperationsProfile) {
            return "配对电脑后将自动开启持续守护";
        }
        int reminderCount = reminderCount(
                usableProfileCount, remindersEnabledProfileCount);
        if (usableProfileCount > 1) {
            if (!remindersAvailable) {
                return usableProfileCount + " 台电脑后台轮巡已开启；提醒尚未开启";
            }
            if (reminderCount == 0) {
                return usableProfileCount + " 台电脑后台轮巡已开启；每台提醒均暂停";
            }
            return reminderCount == usableProfileCount
                    ? usableProfileCount + " 台电脑后台轮巡与运维提醒已开启"
                    : usableProfileCount + " 台电脑后台轮巡已开启；"
                            + reminderCount + " 台接收异常提醒";
        }
        if (!remindersAvailable) {
            return "后台检查已开启；提醒尚未开启";
        }
        return reminderCount > 0
                ? "后台检查与运维提醒已开启"
                : "后台检查已开启；当前电脑提醒已暂停";
    }

    static boolean shouldOfferReminderAction(
            boolean hasOperationsProfile,
            boolean remindersAvailable) {
        return hasOperationsProfile && !remindersAvailable;
    }

    static String fleetScopeDetails(
            int usableProfileCount, int remindersEnabledProfileCount) {
        int reminderCount = reminderCount(
                usableProfileCount, remindersEnabledProfileCount);
        String scope = usableProfileCount > 1
                ? "当前电脑每分钟检查；其他已配对电脑在超过 10 分钟未更新时依次补查。"
                        + "提醒会标明具体电脑，点开后才切换操作目标。"
                : "";
        int pausedCount = Math.max(0, usableProfileCount - reminderCount);
        if (pausedCount == 0) {
            return scope;
        }
        String reminderScope = reminderCount == 0
                ? (usableProfileCount == 1
                        ? "当前电脑异常提醒已暂停；后台状态记录与手动检查仍保留。"
                        : "每台电脑的异常提醒均已暂停；后台轮巡与状态记录仍保留。")
                : reminderCount + " 台电脑接收异常提醒，" + pausedCount + " 台已暂停。";
        return scope.isEmpty() ? reminderScope : scope + reminderScope;
    }

    private static int reminderCount(int usableProfileCount, int requestedCount) {
        return Math.max(0, Math.min(Math.max(0, usableProfileCount), requestedCount));
    }
}

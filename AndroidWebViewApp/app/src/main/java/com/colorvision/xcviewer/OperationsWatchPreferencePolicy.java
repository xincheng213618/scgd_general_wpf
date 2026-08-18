package com.colorvision.xcviewer;

final class OperationsWatchPreferencePolicy {
    private OperationsWatchPreferencePolicy() {
    }

    static boolean shouldRun(boolean hasOperationsProfile, boolean userEnabled) {
        return hasOperationsProfile && userEnabled;
    }

    static String status(boolean hasOperationsProfile, boolean userEnabled) {
        if (!userEnabled) {
            return "已关闭";
        }
        return hasOperationsProfile ? "检查当前电脑并提醒" : "配对后自动启动";
    }
}

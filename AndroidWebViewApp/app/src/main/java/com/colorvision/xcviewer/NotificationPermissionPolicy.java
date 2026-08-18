package com.colorvision.xcviewer;

final class NotificationPermissionPolicy {
    static final int ACTION_REQUEST = 1;
    static final int ACTION_OPEN_SETTINGS = 2;
    static final int ACTION_MANAGE = 3;

    private NotificationPermissionPolicy() {
    }

    static int action(
            int androidSdk,
            boolean runtimePermissionGranted,
            boolean appNotificationsEnabled,
            boolean attentionChannelEnabled,
            boolean runtimeRequestBlocked,
            boolean shouldShowRationale) {
        boolean runtimeGranted = androidSdk < 33 || runtimePermissionGranted;
        if (runtimeGranted && appNotificationsEnabled && attentionChannelEnabled) {
            return ACTION_MANAGE;
        }
        if (androidSdk >= 33
                && !runtimePermissionGranted
                && (!runtimeRequestBlocked || shouldShowRationale)) {
            return ACTION_REQUEST;
        }
        return ACTION_OPEN_SETTINGS;
    }

    static String status(
            int androidSdk,
            boolean runtimePermissionGranted,
            boolean appNotificationsEnabled,
            boolean attentionChannelEnabled,
            boolean runtimeRequestBlocked,
            boolean shouldShowRationale) {
        int action = action(
                androidSdk,
                runtimePermissionGranted,
                appNotificationsEnabled,
                attentionChannelEnabled,
                runtimeRequestBlocked,
                shouldShowRationale);
        if (action == ACTION_MANAGE) {
            return "已授权";
        }
        boolean runtimeGranted = androidSdk < 33 || runtimePermissionGranted;
        if (runtimeGranted && appNotificationsEnabled && !attentionChannelEnabled) {
            return "提醒已关闭";
        }
        if (action == ACTION_REQUEST) {
            return "未授权";
        }
        return "需在系统设置开启";
    }

    static boolean canPostAttention(
            int androidSdk,
            boolean runtimePermissionGranted,
            boolean appNotificationsEnabled,
            boolean attentionChannelEnabled) {
        boolean runtimeGranted = androidSdk < 33 || runtimePermissionGranted;
        return runtimeGranted && appNotificationsEnabled && attentionChannelEnabled;
    }
}

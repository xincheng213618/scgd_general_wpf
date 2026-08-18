package com.colorvision.xcviewer;

final class OperationsConnectionOverview {
    private OperationsConnectionOverview() {
    }

    static String summary(
            String profileLabel,
            String profileState,
            String activeChannel,
            int profileCount,
            int maximumProfiles) {
        return "当前电脑：" + safe(profileLabel, "未选择")
                + "\n当前状态：" + safe(profileState, "尚未检查")
                + "\n当前通道：" + safe(activeChannel, "正在确认")
                + "\n已配对电脑：" + Math.max(0, profileCount)
                + " / " + Math.max(0, maximumProfiles);
    }

    static String connectionNote() {
        return "安全通道始终使用设备密钥和 TLS 证书固定。"
                + "首选通道临时不可用时会安全回退，恢复后自动切回；"
                + "固定中继地址由应用内置。只有当前电脑持续后台检查，"
                + "其他电脑通过只读巡检刷新。";
    }

    private static String safe(String value, String fallback) {
        return value == null || value.trim().isEmpty() ? fallback : value.trim();
    }
}

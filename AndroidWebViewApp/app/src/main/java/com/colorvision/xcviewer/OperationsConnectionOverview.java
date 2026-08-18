package com.colorvision.xcviewer;

final class OperationsConnectionOverview {
    private OperationsConnectionOverview() {
    }

    static String pageStatus(int profileCount, String profileState, String fleetSummary) {
        return profileCount > 1
                ? "全部电脑 · " + safe(fleetSummary, "待巡检")
                : safe(profileState, "尚未检查");
    }

    static String summary(
            String preferredChannel,
            String activeChannel,
            int profileCount,
            int maximumProfiles) {
        return "当前使用 " + safe(activeChannel, "正在确认")
                + " · 首选 " + safe(preferredChannel, "正在确认")
                + "\n已配对电脑 " + Math.max(0, profileCount)
                + " / " + Math.max(0, maximumProfiles);
    }

    static boolean showsFleetTools(int profileCount) {
        return profileCount > 1;
    }

    static String connectionNote() {
        return "安全通道始终使用设备密钥和 TLS 证书固定。"
                + "首选通道不可用时自动安全回退，恢复后切回；"
                + "固定中继地址由应用内置，不能修改。";
    }

    static String removalNote() {
        return "仅当不再使用当前电脑时移除。此操作会删除手机中的独立密钥、"
                + "证书指纹、时间线和最近任务；其他电脑不受影响。";
    }

    private static String safe(String value, String fallback) {
        return value == null || value.trim().isEmpty() ? fallback : value.trim();
    }
}

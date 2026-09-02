package com.colorvision.xcviewer;

final class SettingsInformationArchitecture {
    static final String CONNECTION_SECTION = "连接";
    static final String BACKGROUND_SECTION = "后台运行";
    static final String APPLICATION_SECTION = "外观与更新";
    static final String COMPUTER_CONNECTIONS = "电脑与连接";
    static final String ADD_COMPUTER = "添加电脑";
    static final String CONNECT_COMPUTER = "连接电脑";
    static final String OPERATIONS_WATCH = "持续守护";
    static final String OPERATIONS_WATCH_STATUS = "守护状态";
    static final String NOTIFICATION_PERMISSION = "运维提醒";
    static final String THEME_MODE = "主题模式";
    static final String APP_UPDATE = "应用更新";

    private SettingsInformationArchitecture() {
    }

    static String connectionSupportingText(boolean paired, String computerSummary) {
        if (!paired) {
            return "扫描安全配对码 · 配对后启用设备密钥 + TLS 证书固定";
        }
        String summary = computerSummary == null ? "" : computerSummary.trim();
        return summary.isEmpty()
                ? "设备密钥 + TLS 证书固定"
                : summary + " · 设备密钥 + TLS 证书固定";
    }
}

package com.colorvision.xcviewer;

import java.util.Arrays;
import java.util.List;

final class SettingsInformationArchitecture {
    static final String CONNECTION_SECTION = "电脑连接";
    static final String BACKGROUND_SECTION = "后台运行";
    static final String PERMISSION_SECTION = "权限";
    static final String APPLICATION_SECTION = "应用";
    static final String OPERATIONS = "现场运维";
    static final String SECURE_CHANNEL = "安全通道";
    static final String ADD_COMPUTER = "添加电脑";
    static final String CONNECT_COMPUTER = "连接电脑";
    static final String OPERATIONS_WATCH = "持续守护";
    static final String NOTIFICATION_PERMISSION = "通知权限";
    static final String CAMERA_PERMISSION = "相机权限";
    static final String THEME_MODE = "主题模式";
    static final String APP_UPDATE = "应用更新";

    private SettingsInformationArchitecture() {
    }

    static List<String> sectionHeadings() {
        return Arrays.asList(
                CONNECTION_SECTION,
                BACKGROUND_SECTION,
                PERMISSION_SECTION,
                APPLICATION_SECTION);
    }

    static List<String> visibleRows(boolean paired) {
        return Arrays.asList(
                OPERATIONS,
                SECURE_CHANNEL,
                paired ? ADD_COMPUTER : CONNECT_COMPUTER,
                OPERATIONS_WATCH,
                NOTIFICATION_PERMISSION,
                CAMERA_PERMISSION,
                THEME_MODE,
                APP_UPDATE);
    }
}

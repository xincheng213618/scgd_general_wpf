package com.colorvision.xcviewer;

import java.util.Arrays;
import java.util.List;

final class SettingsInformationArchitecture {
    static final String CONNECTION_SECTION = "连接";
    static final String BACKGROUND_SECTION = "后台运行";
    static final String PERMISSION_SECTION = "权限";
    static final String APPLICATION_SECTION = "外观与更新";
    static final String COMPUTER_CONNECTIONS = "电脑与连接";
    static final String SECURE_CHANNEL = "安全通道";
    static final String ADD_COMPUTER = "添加电脑";
    static final String CONNECT_COMPUTER = "连接电脑";
    static final String OPERATIONS_WATCH = "持续守护";
    static final String NOTIFICATION_PERMISSION = "运维提醒";
    static final String CAMERA_PERMISSION = "相机权限";
    static final String THEME_MODE = "主题模式";
    static final String APP_UPDATE = "应用更新";

    private SettingsInformationArchitecture() {
    }

    static List<String> sectionHeadings() {
        return Arrays.asList(
                CONNECTION_SECTION,
                BACKGROUND_SECTION,
                APPLICATION_SECTION,
                PERMISSION_SECTION);
    }

    static List<String> visibleRows(boolean paired) {
        if (paired) {
            return Arrays.asList(
                    COMPUTER_CONNECTIONS,
                    SECURE_CHANNEL,
                    ADD_COMPUTER,
                    OPERATIONS_WATCH,
                    NOTIFICATION_PERMISSION,
                    THEME_MODE,
                    APP_UPDATE,
                    CAMERA_PERMISSION);
        }
        return Arrays.asList(
                CONNECT_COMPUTER,
                SECURE_CHANNEL,
                OPERATIONS_WATCH,
                NOTIFICATION_PERMISSION,
                THEME_MODE,
                APP_UPDATE,
                CAMERA_PERMISSION);
    }
}

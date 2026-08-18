package com.colorvision.xcviewer;

import java.util.Arrays;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

final class OperationsDashboardShortcutPresentation {
    static final String ACTION_CONNECTION_CHECK = "dashboard.connection_check";
    static final String ACTION_CONNECTIONS = "dashboard.connections";

    private OperationsDashboardShortcutPresentation() {
    }

    static List<Shortcut> direct() {
        return Collections.unmodifiableList(Arrays.asList(
                shortcut(ACTION_CONNECTION_CHECK, "连接自检", "检查网络、证书与设备签名", true),
                shortcut(ACTION_CONNECTIONS, "电脑与连接", "管理、切换电脑与连接方式", false)));
    }

    static boolean hasUniqueActionIds(List<Shortcut> shortcuts) {
        Set<String> actionIds = new HashSet<>();
        for (Shortcut shortcut : shortcuts) {
            if (!actionIds.add(shortcut.actionId)) {
                return false;
            }
        }
        return true;
    }

    private static Shortcut shortcut(
            String actionId, String label, String summary, boolean tonal) {
        return new Shortcut(actionId, label, summary, tonal);
    }

    static final class Shortcut {
        final String actionId;
        final String label;
        final String summary;
        final boolean tonal;

        Shortcut(String actionId, String label, String summary, boolean tonal) {
            this.actionId = actionId;
            this.label = label;
            this.summary = summary;
            this.tonal = tonal;
        }

        String accessibilityLabel() {
            return label + "，" + summary;
        }
    }
}

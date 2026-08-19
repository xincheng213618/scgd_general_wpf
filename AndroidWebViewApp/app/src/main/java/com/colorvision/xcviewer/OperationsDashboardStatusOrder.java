package com.colorvision.xcviewer;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

final class OperationsDashboardStatusOrder {
    private OperationsDashboardStatusOrder() {
    }

    static List<OperationsDashboardStatusFormatter.Item> prioritized(
            List<OperationsDashboardStatusFormatter.Item> items) {
        List<OperationsDashboardStatusFormatter.Item> ordered = new ArrayList<>(items);
        Collections.sort(ordered, OperationsDashboardStatusOrder::compare);
        return Collections.unmodifiableList(ordered);
    }

    static int compare(
            OperationsDashboardStatusFormatter.Item left,
            OperationsDashboardStatusFormatter.Item right) {
        int priority = Integer.compare(right.priority, left.priority);
        if (priority != 0) {
            return priority;
        }
        return Integer.compare(categoryOrder(left.title), categoryOrder(right.title));
    }

    static int attentionCount(List<OperationsDashboardStatusFormatter.Item> items) {
        int count = 0;
        for (OperationsDashboardStatusFormatter.Item item : items) {
            if (item != null && item.tone == OperationsDashboardStatusFormatter.TONE_ATTENTION) {
                count++;
            }
        }
        return count;
    }

    static String sectionTitle(boolean remote, boolean fresh, int attentionCount) {
        String base = OperationsDashboardStatusFormatter.sectionTitle(remote, fresh);
        return attentionCount > 0 ? base + " · " + attentionCount + " 项需关注" : base;
    }

    static String sectionCaption(boolean remote, boolean fresh, int attentionCount) {
        if (attentionCount > 0) {
            if (remote && !fresh) {
                return "快照已过期；上次需关注项按优先级排列，仅供参考。";
            }
            if (remote) {
                return "电脑签名状态中的需关注项已置顶；点击任一项查看详情。";
            }
            return "需关注项已按优先级置顶；点击任一项查看详细状态。";
        }
        return OperationsDashboardStatusFormatter.sectionCaption(remote, fresh);
    }

    private static int categoryOrder(String title) {
        if ("应用".equals(title)) {
            return 0;
        }
        if ("检测".equals(title)) {
            return 1;
        }
        if ("设备".equals(title)) {
            return 2;
        }
        if ("消息".equals(title)) {
            return 3;
        }
        if ("告警".equals(title)) {
            return 4;
        }
        if ("性能".equals(title)) {
            return 5;
        }
        if ("恢复".equals(title)) {
            return 6;
        }
        return Integer.MAX_VALUE;
    }
}

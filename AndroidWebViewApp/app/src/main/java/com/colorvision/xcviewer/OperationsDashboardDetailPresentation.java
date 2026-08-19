package com.colorvision.xcviewer;

final class OperationsDashboardDetailPresentation {
    static final class Item {
        final String title;
        final String refreshLabel;

        Item(String title, String refreshLabel) {
            this.title = title;
            this.refreshLabel = refreshLabel;
        }
    }

    private OperationsDashboardDetailPresentation() {
    }

    static Item forPath(String path) {
        if ("/ops/v1/snapshot".equals(path)) {
            return new Item("应用概况", "刷新应用概况");
        }
        if ("/ops/v1/services/health".equals(path)) {
            return new Item("服务健康", "刷新服务健康");
        }
        if ("/ops/v1/flow/runtime".equals(path)) {
            return new Item("检测状态", "刷新检测状态");
        }
        if ("/ops/v1/messaging/health".equals(path)) {
            return new Item("消息通道", "刷新消息通道");
        }
        if ("/ops/v1/alerts".equals(path)) {
            return new Item("告警详情", "刷新告警");
        }
        if ("/ops/v1/diagnostics/performance".equals(path)) {
            return new Item("性能状态", "刷新性能状态");
        }
        if ("/ops/v1/diagnostics/recent-events".equals(path)) {
            return new Item("近期事件", "刷新近期事件");
        }
        if ("/ops/v1/diagnostics/failures".equals(path)) {
            return new Item("崩溃与卡死", "刷新崩溃与卡死");
        }
        if ("/ops/v1/audit".equals(path)) {
            return new Item("近期操作记录", "刷新操作记录");
        }
        return new Item("运维详情", "刷新详情");
    }
}

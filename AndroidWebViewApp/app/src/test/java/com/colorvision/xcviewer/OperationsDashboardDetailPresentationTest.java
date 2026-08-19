package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class OperationsDashboardDetailPresentationTest {
    @Test
    public void knownDashboardRowsReceiveTaskSpecificDetailCopy() {
        assertItem("应用概况", "刷新应用概况", "/ops/v1/snapshot");
        assertItem("服务健康", "刷新服务健康", "/ops/v1/services/health");
        assertItem("检测状态", "刷新检测状态", "/ops/v1/flow/runtime");
        assertItem("消息通道", "刷新消息通道", "/ops/v1/messaging/health");
        assertItem("告警详情", "刷新告警", "/ops/v1/alerts");
        assertItem("性能状态", "刷新性能状态", "/ops/v1/diagnostics/performance");
        assertItem("近期事件", "刷新近期事件", "/ops/v1/diagnostics/recent-events");
        assertItem("崩溃与卡死", "刷新崩溃与卡死", "/ops/v1/diagnostics/failures");
        assertItem("近期操作记录", "刷新操作记录", "/ops/v1/audit");
    }

    @Test
    public void unknownDashboardRowsKeepABoundedFallback() {
        assertItem("运维详情", "刷新详情", "/ops/v1/unknown");
    }

    private static void assertItem(String title, String refreshLabel, String path) {
        OperationsDashboardDetailPresentation.Item item =
                OperationsDashboardDetailPresentation.forPath(path);
        assertEquals(title, item.title);
        assertEquals(refreshLabel, item.refreshLabel);
    }
}

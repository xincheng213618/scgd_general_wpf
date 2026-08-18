package com.colorvision.xcviewer;

import org.junit.Test;

import java.util.Arrays;
import java.util.List;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

public class OperationsDashboardStatusOrderTest {
    @Test
    public void operationalUrgencyMovesAttentionAndActiveRowsAheadOfStableRows() {
        List<OperationsDashboardStatusFormatter.Item> ordered =
                OperationsDashboardStatusOrder.prioritized(Arrays.asList(
                        OperationsDashboardStatusFormatter.application(
                                true, "1.0", true, true, "Normal", 256),
                        OperationsDashboardStatusFormatter.flow(true, true, "running"),
                        OperationsDashboardStatusFormatter.devices(
                                true, true, 3, 0, 2, 5, "相机 · 离线 2"),
                        OperationsDashboardStatusFormatter.messageChannel(
                                true, false, false, 0, 5),
                        OperationsDashboardStatusFormatter.alerts(true, 1, 0, 0, "安全运维"),
                        OperationsDashboardStatusFormatter.performance(
                                true, 18, "unresponsive"),
                        OperationsDashboardStatusFormatter.recovery(
                                true, true, true, true)));

        assertEquals(Arrays.asList("性能", "消息", "设备", "告警", "检测", "应用", "恢复"),
                titles(ordered));
        assertEquals(4, OperationsDashboardStatusOrder.attentionCount(ordered));
    }

    @Test
    public void stableRowsReturnToTheStandardOverviewOrder() {
        List<OperationsDashboardStatusFormatter.Item> ordered =
                OperationsDashboardStatusOrder.prioritized(Arrays.asList(
                        OperationsDashboardStatusFormatter.alerts(true, 0, 0, 0, ""),
                        OperationsDashboardStatusFormatter.devices(
                                true, true, 4, 0, 0, 4, ""),
                        OperationsDashboardStatusFormatter.application(
                                true, "1.0", true, true, "Normal", 256),
                        OperationsDashboardStatusFormatter.flow(true, false, "idle")));

        assertEquals(Arrays.asList("应用", "检测", "设备", "告警"), titles(ordered));
        assertEquals(0, OperationsDashboardStatusOrder.attentionCount(ordered));
    }

    @Test
    public void sectionCopyExplainsAttentionPriorityWithoutHidingStaleRemoteState() {
        assertEquals("实时状态 · 2 项需关注",
                OperationsDashboardStatusOrder.sectionTitle(false, true, 2));
        assertTrue(OperationsDashboardStatusOrder.sectionCaption(false, true, 2)
                .contains("优先级置顶"));
        assertEquals("上次电脑签名状态 · 1 项需关注",
                OperationsDashboardStatusOrder.sectionTitle(true, false, 1));
        assertTrue(OperationsDashboardStatusOrder.sectionCaption(true, false, 1)
                .contains("快照已过期"));
    }

    private static List<String> titles(
            List<OperationsDashboardStatusFormatter.Item> items) {
        java.util.ArrayList<String> titles = new java.util.ArrayList<>();
        for (OperationsDashboardStatusFormatter.Item item : items) {
            titles.add(item.title);
        }
        return titles;
    }
}

package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class OperationsDashboardStatusFormatterTest {
    @Test
    public void attentionStatesStayVisibleInCompactLabels() {
        assertEquals("检测\n运行中", OperationsDashboardStatusFormatter.flow(true, true, "running"));
        assertEquals("设备\n需关注 2", OperationsDashboardStatusFormatter.devices(true, true, 3, 1, 2, 6));
        assertEquals("消息\n订阅 3 / 5",
                OperationsDashboardStatusFormatter.messageChannel(true, true, false, 3, 5));
        assertEquals("告警\n严重 1", OperationsDashboardStatusFormatter.alerts(4, 2, 1));
        assertEquals("性能\nCPU 13% · 无响应",
                OperationsDashboardStatusFormatter.performance(true, 12.6, "unresponsive"));
        assertEquals("恢复\n自动看门狗",
                OperationsDashboardStatusFormatter.recovery(true, true, true));
    }

    @Test
    public void healthyStatesStayShort() {
        assertEquals("检测\n空闲", OperationsDashboardStatusFormatter.flow(true, false, "idle"));
        assertEquals("设备\n就绪 4 / 4", OperationsDashboardStatusFormatter.devices(true, true, 4, 0, 0, 4));
        assertEquals("消息\n已连接",
                OperationsDashboardStatusFormatter.messageChannel(true, true, true, 5, 5));
        assertEquals("告警\n暂无异常", OperationsDashboardStatusFormatter.alerts(0, 0, 0));
        assertEquals("恢复\nWindows 后备",
                OperationsDashboardStatusFormatter.recovery(true, true, false));
    }
}

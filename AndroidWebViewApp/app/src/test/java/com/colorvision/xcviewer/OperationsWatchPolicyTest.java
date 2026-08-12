package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class OperationsWatchPolicyTest {
    @Test
    public void retryDelayUsesBoundedExponentialBackoff() {
        assertEquals(30_000L, OperationsWatchPolicy.retryDelayMilliseconds(0));
        assertEquals(30_000L, OperationsWatchPolicy.retryDelayMilliseconds(1));
        assertEquals(60_000L, OperationsWatchPolicy.retryDelayMilliseconds(2));
        assertEquals(120_000L, OperationsWatchPolicy.retryDelayMilliseconds(3));
        assertEquals(240_000L, OperationsWatchPolicy.retryDelayMilliseconds(4));
        assertEquals(300_000L, OperationsWatchPolicy.retryDelayMilliseconds(5));
        assertEquals(300_000L, OperationsWatchPolicy.retryDelayMilliseconds(50));
    }

    @Test
    public void healthyStatusPrioritizesActionableEvidence() {
        assertEquals("在线 · 主界面响应超时",
                OperationsWatchPolicy.healthyStatus("unresponsive", true, 2, 3, 4, true));
        assertEquals("在线 · 发现严重告警",
                OperationsWatchPolicy.healthyStatus("ready", true, 2, 3, 4, true));
        assertEquals("在线 · 消息通道需要关注",
                OperationsWatchPolicy.healthyStatus("ready", true, 0, 3, 4, true));
        assertEquals("在线 · 检测设备需要关注",
                OperationsWatchPolicy.healthyStatus("ready", true, 0, 3, 4, false));
        assertEquals("在线 · 发现错误事件",
                OperationsWatchPolicy.healthyStatus("ready", true, 0, 3, 0, false));
        assertEquals("在线 · 主界面响应偏慢",
                OperationsWatchPolicy.healthyStatus("slow", true, 0, 0, 0, false));
        assertEquals("在线 · 检测正在进行",
                OperationsWatchPolicy.healthyStatus("ready", true, 0, 0, 0, false));
        assertEquals("在线 · 当前状态稳定",
                OperationsWatchPolicy.healthyStatus("ready", false, 0, 0, 0, false));
    }
}

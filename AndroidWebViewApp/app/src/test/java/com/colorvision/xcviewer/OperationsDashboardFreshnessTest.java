package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

public class OperationsDashboardFreshnessTest {
    @Test
    public void loadingAndDirectUpdatesExplainWhetherDataIsCurrent() {
        assertEquals("状态时间 · 正在读取",
                OperationsDashboardFreshness.loading().label);
        assertEquals(OperationsDashboardFreshness.TONE_MUTED,
                OperationsDashboardFreshness.loading().tone);
        assertEquals("状态更新 · 02:41:08 · 现场直连",
                OperationsDashboardFreshness.updated(
                        "02:41:08", false, true).label);
    }

    @Test
    public void relayUpdatesDistinguishFreshAndExpiredComputerSignatures() {
        OperationsDashboardFreshness.Presentation fresh =
                OperationsDashboardFreshness.updated("02:41:08", true, true);
        OperationsDashboardFreshness.Presentation stale =
                OperationsDashboardFreshness.updated("02:35:00", true, false);

        assertEquals("电脑签名状态 · 02:41:08 · 在线", fresh.label);
        assertEquals(OperationsDashboardFreshness.TONE_NORMAL, fresh.tone);
        assertEquals("电脑签名状态 · 02:35:00 · 可能已过期", stale.label);
        assertEquals(OperationsDashboardFreshness.TONE_ATTENTION, stale.tone);
    }

    @Test
    public void failuresRetainTheLastKnownGoodTimeWithoutClaimingFreshness() {
        assertEquals("状态未更新 · 连接不可达 · 上次成功 02:41:08",
                OperationsDashboardFreshness.unavailable(
                        "连接不可达", "02:41:08").label);
        assertEquals("状态未更新 · 实时摘要不可用 · 尚无成功摘要",
                OperationsDashboardFreshness.unavailable(
                        "实时摘要不可用", "").label);
        assertEquals(OperationsDashboardFreshness.TONE_ATTENTION,
                OperationsDashboardFreshness.unavailable(
                        "连接不可达", "02:41:08").tone);
    }
}

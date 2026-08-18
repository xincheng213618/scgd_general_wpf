package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;

import org.junit.Test;

public class OperationsDashboardOverviewTest {
    @Test
    public void staleSummaryClearlyLabelsHistoricalState() {
        long now = 20L * 60L * 60L * 1_000L;

        assertEquals(
                "上次状态：ColorVision 运行中 · 主窗口可见\n"
                        + "更新：2 小时前（已过期）",
                OperationsDashboardOverview.remoteSummary(
                        false, true, true, true, true,
                        18L * 60L * 60L, now));
    }

    @Test
    public void freshSummaryKeepsOnlyCurrentOperationalFacts() {
        long now = 10L * 60L * 1_000L;

        assertEquals(
                "ColorVision：运行中 · 主窗口隐藏或最小化\n更新：刚刚",
                OperationsDashboardOverview.remoteSummary(
                        true, true, true, false, true,
                        now / 1_000L, now));
    }

    @Test
    public void missingMonitorIsDeclaredWithoutTechnicalSecurityCopy() {
        String summary = OperationsDashboardOverview.remoteSummary(
                true, false, false, false, false, 0L, 0L);

        assertEquals(
                "ColorVision：暂未确认 · 主窗口暂未确认\n"
                        + "更新：等待电脑更新 · 详细状态待更新",
                summary);
        assertFalse(summary.contains("密钥"));
        assertFalse(summary.contains("TLS"));
    }

    @Test
    public void connectionStateUsesTextInsteadOfDecorativeGlyphs() {
        assertEquals("现场直连 · 自动保持",
                OperationsDashboardOverview.directConnectionState(false));
        assertEquals("现场直连（临时） · 中继恢复后自动切回",
                OperationsDashboardOverview.directConnectionState(true));
        assertEquals("固定中继在线 · 电脑未上线",
                OperationsDashboardOverview.remoteConnectionState(false, true));
        assertEquals("固定中继（临时） · 直连恢复后自动切回",
                OperationsDashboardOverview.remoteConnectionState(true, false));
    }
}

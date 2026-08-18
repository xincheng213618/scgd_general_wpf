package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsRecoveryOverviewTest {
    @Test
    public void failureSummaryKeepsBothChannelsWithoutRepeatingRecoveryCopy() {
        String summary = OperationsRecoveryOverview.failureSummary(
                "电脑端安全通道当前不可达。配对资料已保留，请运行连接自检。",
                "无法解析电脑地址，请检查当前网络。配对资料已保留。 ");

        assertEquals(
                "现场直连：电脑端安全通道当前不可达\n"
                        + "固定中继：无法解析电脑地址，请检查当前网络\n\n"
                        + "配对资料已保留，无需重新扫码。",
                summary);
        assertFalse(summary.contains("请运行连接自检"));
    }

    @Test
    public void failureSummaryUsesSafeFallbacksAndBoundsLongReasons() {
        String summary = OperationsRecoveryOverview.failureSummary(
                " ",
                "a".repeat(100));

        assertTrue(summary.startsWith("现场直连：暂不可达\n固定中继："));
        assertTrue(summary.contains("…"));
        assertFalse(summary.contains("a".repeat(73)));
    }

    @Test
    public void removalNoteMakesTheDestructiveBoundaryExplicit() {
        String note = OperationsRecoveryOverview.pairingRemovalNote();

        assertTrue(note.contains("设备密钥"));
        assertTrue(note.contains("确认不再使用"));
    }
}

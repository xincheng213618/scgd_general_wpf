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
                        + "配对资料已安全保留，无需重新扫码。",
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
    public void identicalChannelFailuresAreCombinedInsteadOfRepeated() {
        String summary = OperationsRecoveryOverview.failureSummary(
                "电脑端安全通道当前不可达。",
                "电脑端安全通道当前不可达。");

        assertEquals(
                "现场直连与固定中继当前均不可达\n\n"
                        + "配对资料已安全保留，无需重新扫码。",
                summary);
        assertFalse(summary.contains("\n固定中继："));
        assertFalse(summary.contains("电脑端安全通道当前不可达"));
    }

    @Test
    public void recoveryCopyExplainsAutomaticRetryWithoutSuggestingRepairOrRemoval() {
        assertEquals("电脑暂时不可达 · 将自动重试",
                OperationsRecoveryOverview.waitingStatus());
        assertEquals("正在自动重试安全连接…",
                OperationsRecoveryOverview.checkingStatus());
        String note = OperationsRecoveryOverview.automaticRetryNote();

        assertTrue(note.contains("每 30 秒自动重试"));
        assertTrue(note.contains("后台守护"));
        assertFalse(note.contains("移除"));
        assertFalse(note.contains("重新扫码"));
    }
}

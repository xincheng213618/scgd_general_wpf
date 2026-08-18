package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsConnectionOverviewTest {
    @Test
    public void singleComputerStatusUsesTheCurrentProfileState() {
        assertEquals("远程在线 · 刚刚检查",
                OperationsConnectionOverview.pageStatus(
                        1, "远程在线 · 刚刚检查", "稳定 1"));
    }

    @Test
    public void multiComputerStatusUsesTheFleetSummary() {
        assertEquals("全部电脑 · 需关注 1 · 稳定 2",
                OperationsConnectionOverview.pageStatus(
                        3, "远程在线 · 刚刚检查", "需关注 1 · 稳定 2"));
    }

    @Test
    public void summaryKeepsActiveAndPreferredChannelsCompact() {
        assertEquals(
                "当前电脑 实验室电脑\n"
                        + "当前使用 固定中继 · 首选 现场直连\n"
                        + "已配对电脑 2 / 6",
                OperationsConnectionOverview.summary(
                        " 实验室电脑 ", " 现场直连 ", "固定中继", 2, 6));
    }

    @Test
    public void summaryUsesSafeFallbacksAndBoundedCounts() {
        assertEquals(
                "当前电脑 未命名电脑\n"
                        + "当前使用 正在确认 · 首选 正在确认\n"
                        + "已配对电脑 0 / 0",
                OperationsConnectionOverview.summary(null, null, " ", -1, -2));
        assertEquals("尚未检查",
                OperationsConnectionOverview.pageStatus(1, "", null));
    }

    @Test
    public void fleetToolsOnlyAppearWhenThereIsSomethingToSwitchOrBatchCheck() {
        assertFalse(OperationsConnectionOverview.showsFleetTools(0));
        assertFalse(OperationsConnectionOverview.showsFleetTools(1));
        assertTrue(OperationsConnectionOverview.showsFleetTools(2));
    }

    @Test
    public void connectionNoteKeepsSecurityAndRefreshBoundaries() {
        String note = OperationsConnectionOverview.connectionNote();

        assertTrue(note.contains("设备密钥和 TLS 证书固定"));
        assertTrue(note.contains("安全回退"));
        assertTrue(note.contains("不能修改"));
        assertFalse(note.contains("http"));
    }

    @Test
    public void removalNoteKeepsTheDangerousOperationSeparateAndScoped() {
        String note = OperationsConnectionOverview.removalNote();

        assertTrue(note.contains("仅当不再使用当前电脑时移除"));
        assertTrue(note.contains("独立密钥"));
        assertTrue(note.contains("其他电脑不受影响"));
    }
}

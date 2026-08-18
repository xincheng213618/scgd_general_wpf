package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsConnectionOverviewTest {
    @Test
    public void pageStatusReportsTheLiveConnectionInsteadOfDeviceHealth() {
        assertEquals("正在确认安全连接…",
                OperationsConnectionOverview.pageStatus(
                        OperationsConnectionOverviewProbe.checking()));
        assertEquals("安全连接可用 · 现场直连",
                OperationsConnectionOverview.pageStatus(new OperationsConnectionOverviewProbe.Result(
                        OperationsConnectionOverviewProbe.Channel.DIRECT,
                        true, false, null, null)));
        assertEquals("固定中继可用 · 电脑尚未上线",
                OperationsConnectionOverview.pageStatus(new OperationsConnectionOverviewProbe.Result(
                        OperationsConnectionOverviewProbe.Channel.RELAY,
                        false, false, null, null)));
        assertEquals("需要处理 · 两种连接均不可达",
                OperationsConnectionOverview.pageStatus(new OperationsConnectionOverviewProbe.Result(
                        OperationsConnectionOverviewProbe.Channel.UNAVAILABLE,
                        false, false, null, null)));
    }

    @Test
    public void summaryKeepsActiveAndPreferredChannelsCompact() {
        assertEquals(
                "当前电脑 实验室电脑\n"
                        + "当前通道 固定中继 · 首选 现场直连\n"
                        + "已配对电脑 2 / 6",
                OperationsConnectionOverview.summary(
                        " 实验室电脑 ",
                        " 现场直连 ",
                        new OperationsConnectionOverviewProbe.Result(
                                OperationsConnectionOverviewProbe.Channel.RELAY,
                                true, false, null, null),
                        2,
                        6));
    }

    @Test
    public void summaryUsesSafeFallbacksAndBoundedCounts() {
        assertEquals(
                "当前电脑 未命名电脑\n"
                        + "当前通道 正在确认 · 首选 正在确认\n"
                        + "已配对电脑 0 / 0",
                OperationsConnectionOverview.summary(
                        null, null, OperationsConnectionOverviewProbe.checking(), -1, -2));
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

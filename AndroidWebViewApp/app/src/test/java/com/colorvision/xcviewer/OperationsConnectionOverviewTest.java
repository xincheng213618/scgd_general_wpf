package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsConnectionOverviewTest {
    @Test
    public void summaryPutsCurrentTargetAndStateBeforeCapacity() {
        assertEquals(
                "当前电脑：一号线\n"
                        + "当前状态：远程在线 · 刚刚检查\n"
                        + "当前通道：固定中继\n"
                        + "已配对电脑：2 / 6",
                OperationsConnectionOverview.summary(
                        " 一号线 ", "远程在线 · 刚刚检查", "固定中继", 2, 6));
    }

    @Test
    public void summaryUsesSafeFallbacksAndBoundedCounts() {
        assertEquals(
                "当前电脑：未选择\n"
                        + "当前状态：尚未检查\n"
                        + "当前通道：正在确认\n"
                        + "已配对电脑：0 / 0",
                OperationsConnectionOverview.summary(null, "", " ", -1, -2));
    }

    @Test
    public void connectionNoteKeepsSecurityAndRefreshBoundaries() {
        String note = OperationsConnectionOverview.connectionNote();

        assertTrue(note.contains("设备密钥和 TLS 证书固定"));
        assertTrue(note.contains("安全回退"));
        assertTrue(note.contains("只有当前电脑持续后台检查"));
        assertFalse(note.contains("http"));
    }
}

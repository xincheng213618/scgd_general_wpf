package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsLiveMonitorControlsPresentationTest {
    @Test
    public void activeObservationUsesAStandardEnabledSwitchAndRefreshAction() {
        OperationsLiveMonitorControlsPresentation.ViewModel model =
                OperationsLiveMonitorControlsPresentation.from(true, false, false);

        assertTrue(model.autoRefresh);
        assertTrue(model.toggleEnabled);
        assertTrue(model.refreshEnabled);
        assertEquals("前台每 10 秒更新；进入后台时暂停", model.autoRefreshSummary);
        assertTrue(model.autoRefreshAccessibilityLabel().contains("已开启"));
        assertEquals("立即刷新，采集一份新的脱敏聚合快照",
                model.refreshAccessibilityLabel());
    }

    @Test
    public void pausedObservationKeepsTheCurrentEvidenceExplicit() {
        OperationsLiveMonitorControlsPresentation.ViewModel model =
                OperationsLiveMonitorControlsPresentation.from(false, false, false);

        assertFalse(model.autoRefresh);
        assertEquals("当前快照与本次样本仍保留", model.autoRefreshSummary);
        assertEquals("自动观察，已暂停，当前快照与本次样本仍保留",
                model.autoRefreshAccessibilityLabel());
        assertTrue(model.autoRefreshAccessibilityLabel().contains("已暂停"));
    }

    @Test
    public void busyStatesDisableOnlyTheUnsafeConcurrentActions() {
        OperationsLiveMonitorControlsPresentation.ViewModel refreshing =
                OperationsLiveMonitorControlsPresentation.from(true, true, false);
        assertTrue(refreshing.toggleEnabled);
        assertFalse(refreshing.refreshEnabled);
        assertEquals("正在刷新…", refreshing.refreshLabel);

        OperationsLiveMonitorControlsPresentation.ViewModel cancelling =
                OperationsLiveMonitorControlsPresentation.from(true, false, true);
        assertFalse(cancelling.toggleEnabled);
        assertFalse(cancelling.refreshEnabled);
    }
}

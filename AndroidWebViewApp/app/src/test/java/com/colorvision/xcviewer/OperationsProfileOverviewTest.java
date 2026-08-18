package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class OperationsProfileOverviewTest {
    private static final long NOW = 2_000_000_000_000L;

    @Test
    public void currentChecksUseTheLastCheckTimeInsteadOfTransitionTime() {
        String history = OperationsWatchHistory.transition(
                "", OperationsWatchHistory.STATE_REMOTE_ONLINE,
                NOW - 2L * 24L * 60L * 60L * 1000L).serializedHistory;

        assertEquals("远程在线 · 3 分钟前检查", OperationsProfileOverview.summary(
                history, NOW - 3L * 60L * 1000L, false, NOW));
    }

    @Test
    public void migratedHistoryIsLabeledAsARecordNotARecentCheck() {
        String history = OperationsWatchHistory.transition(
                "", OperationsWatchHistory.STATE_OFFLINE,
                NOW - 4L * 60L * 60L * 1000L).serializedHistory;

        assertEquals("连接中断 · 4 小时前记录", OperationsProfileOverview.summary(
                history, 0L, false, NOW));
    }

    @Test
    public void attentionAndWaitingStatesRemainCompactAndTruthful() {
        String attention = OperationsWatchHistory.transition(
                "", OperationsWatchHistory.attentionState(
                        OperationsWatchPolicy.ATTENTION_DEVICES), NOW).serializedHistory;
        String waiting = OperationsWatchHistory.transition(
                "", OperationsWatchHistory.STATE_REMOTE_WAITING, NOW).serializedHistory;

        assertEquals("设备需关注 · 刚刚检查", OperationsProfileOverview.summary(
                attention, NOW, false, NOW));
        assertEquals("等待电脑上线 · 2 天前检查", OperationsProfileOverview.summary(
                waiting, NOW - 2L * 24L * 60L * 60L * 1000L, false, NOW));
    }

    @Test
    public void emptyAndRevokedProfilesHaveExplicitActions() {
        assertEquals("尚未检查 · 切换后自动守护",
                OperationsProfileOverview.summary("", 0L, false, NOW));
        assertEquals("授权失效 · 点按移除",
                OperationsProfileOverview.summary("", 0L, true, NOW));
    }

    @Test
    public void corruptHistoryDoesNotPretendToKnowTheState() {
        assertEquals("状态未知 · 2 小时前检查", OperationsProfileOverview.summary(
                "corrupt", NOW - 2L * 60L * 60L * 1000L, false, NOW));
    }
}

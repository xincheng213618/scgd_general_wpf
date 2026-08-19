package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsWatchStatusPresentationTest {
    private static final long NOW = 2_000_000_000_000L;

    @Test
    public void statusSeparatesUserPreferenceFromActualBackgroundFreshness() {
        OperationsWatchStatusPresentation.Presentation current =
                OperationsWatchStatusPresentation.create(
                        true, true, OperationsWatchHistory.STATE_ONLINE, NOW - 75_000L, NOW);
        assertEquals("1 分钟前检查 · 连接在线 · 当前状态稳定", current.summary);
        assertTrue(current.details.startsWith("最近一轮后台检查在 1 分钟前完成。"));
        assertTrue(current.details.contains("同类状态产生新脱敏证据"));
        assertTrue(current.details.contains("普通轮询时间变化不会重复打扰"));
        assertFalse(current.attention);

        OperationsWatchStatusPresentation.Presentation stale =
                OperationsWatchStatusPresentation.create(
                        true,
                        true,
                        OperationsWatchHistory.STATE_OFFLINE,
                        NOW - OperationsWatchStatusPresentation.STALE_AFTER_MILLISECONDS - 1L,
                        NOW);
        assertEquals("超过 10 分钟未更新", stale.summary);
        assertTrue(stale.details.contains("持续守护仍处于开启偏好"));
        assertTrue(stale.attention);
    }

    @Test
    public void disabledUnpairedAndUncheckedStatesNeverPretendMonitoringIsHealthy() {
        assertEquals("配对后开始记录",
                OperationsWatchStatusPresentation.create(false, true, "", 0L, NOW).summary);
        assertEquals("持续守护已关闭",
                OperationsWatchStatusPresentation.create(
                        true, false, OperationsWatchHistory.STATE_ONLINE, NOW, NOW).summary);
        assertEquals("等待首次后台检查",
                OperationsWatchStatusPresentation.create(true, true, "", 0L, NOW).summary);
    }

    @Test
    public void untrustedFutureTimestampIsFlaggedForRecovery() {
        OperationsWatchStatusPresentation.Presentation result =
                OperationsWatchStatusPresentation.create(
                        true,
                        true,
                        OperationsWatchHistory.STATE_ONLINE,
                        NOW + 60_001L,
                        NOW);

        assertEquals("检查时间记录异常", result.summary);
        assertTrue(result.attention);
    }
}

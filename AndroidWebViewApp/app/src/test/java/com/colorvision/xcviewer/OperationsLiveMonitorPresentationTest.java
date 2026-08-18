package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

public class OperationsLiveMonitorPresentationTest {
    @Test
    public void snapshotBecomesShortScannableStatusItems() throws Exception {
        JSONObject snapshot = snapshot();
        OperationsLiveMonitorTrend trend = new OperationsLiveMonitorTrend();
        trend.add(new OperationsLiveMonitorTrend.Sample(
                1_000, 10, 470, "responsive", 12L, "idle", 0));

        OperationsLiveMonitorPresentation.ViewModel model =
                OperationsLiveMonitorPresentation.from(
                        snapshot, trend.summarize(), true, "08-19 01:31");

        assertTrue(model.overview.contains("自动观察每 10 秒"));
        assertTrue(model.overview.contains("本次样本 1/30"));
        assertEquals(6, model.statuses.size());
        assertStatus("检测", "空闲", OperationsDashboardStatusFormatter.TONE_DEFAULT,
                model.statuses.get(0));
        assertStatus("设备", "需关注 2", OperationsDashboardStatusFormatter.TONE_ATTENTION,
                model.statuses.get(1));
        assertStatus("消息", "已连接", OperationsDashboardStatusFormatter.TONE_DEFAULT,
                model.statuses.get(2));
        assertEquals("再采集 1 个样本后显示本次趋势", model.trendSummary);
    }

    @Test
    public void trendAndPauseStateRemainExplicit() throws Exception {
        OperationsLiveMonitorTrend trend = new OperationsLiveMonitorTrend();
        trend.add(new OperationsLiveMonitorTrend.Sample(
                1_000, 10, 470, "responsive", 12L, "idle", 0));
        trend.add(new OperationsLiveMonitorTrend.Sample(
                11_000, 20, 490, "slow", 85L, "running", 1));

        OperationsLiveMonitorPresentation.ViewModel model =
                OperationsLiveMonitorPresentation.from(
                        snapshot(), trend.summarize(), false, "08-19 01:32");

        assertTrue(model.overview.startsWith("自动观察已暂停"));
        assertTrue(model.trendSummary.contains("CPU 平均 15.0% / 峰值 20.0%"));
        assertTrue(model.trendSummary.contains("界面偏慢 1 次"));
        assertTrue(model.trendSummary.contains("检测阶段 执行中 · 切换 1 次"));
        assertEquals(OperationsDashboardStatusFormatter.TONE_ATTENTION, model.trendTone);
        assertTrue(model.privacyNote.contains("离开本页即清空"));
    }

    private static JSONObject snapshot() throws Exception {
        return new JSONObject("{"
                + "\"suggestedRefreshSeconds\":10,"
                + "\"flow\":{\"available\":true,\"isActive\":false,\"phase\":\"idle\"},"
                + "\"devices\":{\"available\":true,\"hasConfiguredDevices\":true,"
                + "\"totalCount\":6,\"readyCount\":2,\"attentionCount\":2},"
                + "\"messageChannel\":{\"available\":true,\"connected\":true,"
                + "\"subscriptionReady\":true,\"activeSubscriptionCount\":8,"
                + "\"registeredSubscriptionCount\":8},"
                + "\"performance\":{\"cpuPercent\":10,"
                + "\"mainUi\":{\"state\":\"responsive\"}},"
                + "\"alerts\":{\"warningCount\":0,\"errorCount\":0,\"criticalCount\":0},"
                + "\"applicationRecovery\":{\"supported\":true,\"registered\":true,"
                + "\"automaticWatchdogActive\":true}"
                + "}");
    }

    private static void assertStatus(
            String title,
            String summary,
            int tone,
            OperationsDashboardStatusFormatter.Item item) {
        assertEquals(title, item.title);
        assertEquals(summary, item.summary);
        assertEquals(tone, item.tone);
    }
}

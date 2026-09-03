package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class OperationsDashboardAdvisorTest {
    @Test
    public void staleRemoteSnapshotTakesPriorityOverHistoricalMonitorData() {
        OperationsDashboardAdvisor.Recommendation recommendation =
                OperationsDashboardAdvisor.staleRemoteSnapshot();

        assertEquals("电脑未上线 · 运行连接自检", recommendation.label);
        assertEquals(OperationsDashboardAdvisor.ACTION_CONNECTION_CHECK, recommendation.action);
    }

    @Test
    public void actionableConditionsUseTheBackgroundWatchPriority() throws Exception {
        JSONObject monitor = monitor("unresponsive", true, 3, 4, 5, true, 2);
        assertRecommendation(monitor, "主界面无响应 · 查看性能",
                OperationsDashboardAdvisor.ACTION_PERFORMANCE);

        monitor = monitor("ready", true, 3, 4, 5, true, 2);
        assertRecommendation(monitor, "严重告警 3 个 · 打开问题中心",
                OperationsDashboardAdvisor.ACTION_ALERTS);

        monitor = monitor("ready", true, 0, 4, 5, true, 2);
        assertRecommendation(monitor, "消息通道需处置 · 查看消息",
                OperationsDashboardAdvisor.ACTION_MESSAGE);

        monitor = monitor("ready", true, 0, 4, 5, false, 2);
        assertRecommendation(monitor, "设备需关注 5 个 · 查看设备",
                OperationsDashboardAdvisor.ACTION_DEVICES);

        monitor = monitor("ready", true, 0, 4, 0, false, 2);
        assertRecommendation(monitor, "错误事件 4 个 · 打开问题中心",
                OperationsDashboardAdvisor.ACTION_ALERTS);
    }

    @Test
    public void lowerPriorityStatesStillLeadToAUsefulNextStep() throws Exception {
        assertRecommendation(monitor("slow", false, 0, 0, 0, false, 0),
                "主界面响应偏慢 · 查看性能", OperationsDashboardAdvisor.ACTION_PERFORMANCE);
        assertRecommendation(monitor("ready", false, 0, 0, 0, false, 2),
                "警告 2 个 · 打开问题中心", OperationsDashboardAdvisor.ACTION_ALERTS);
        assertRecommendation(monitor("ready", true, 0, 0, 0, false, 0),
                "检测运行中 · 查看进度", OperationsDashboardAdvisor.ACTION_FLOW);
        assertRecommendation(monitor("ready", false, 0, 0, 0, false, 0),
                "当前运行稳定 · 查看状态", OperationsDashboardAdvisor.ACTION_MONITOR);
    }

    @Test
    public void warningRecommendationNamesTheSafePrimarySource() throws Exception {
        JSONObject monitor = monitor("ready", false, 0, 0, 0, false, 2);
        monitor.getJSONObject("alerts").put("primarySource", "安全运维");

        assertRecommendation(monitor, "安全运维 · 警告 2 · 打开问题中心",
                OperationsDashboardAdvisor.ACTION_ALERTS);
    }

    @Test
    public void deviceRecommendationNamesAffectedTypesAndReason() throws Exception {
        JSONObject monitor = monitor("ready", false, 0, 0, 2, false, 0);
        monitor.put("devices", new JSONObject("{"
                + "\"available\":true,"
                + "\"hasConfiguredDevices\":true,"
                + "\"totalCount\":6,"
                + "\"attentionCount\":2,"
                + "\"unavailableCount\":2,"
                + "\"offlineCount\":2,"
                + "\"categories\":["
                + "{\"category\":\"camera\",\"totalCount\":1,\"unavailableCount\":1},"
                + "{\"category\":\"spectrum\",\"totalCount\":1,\"unavailableCount\":1}"
                + "]}"));

        assertRecommendation(monitor, "相机 离线 1 · 光谱 离线 1 · 查看设备",
                OperationsDashboardAdvisor.ACTION_DEVICES);
    }

    @Test
    public void untrustedCountsAreBoundedBeforeTheyReachTheUi() throws Exception {
        JSONObject monitor = monitor("ready", false, 5_000, 0, 0, false, 0);
        assertRecommendation(monitor, "严重告警 999 个 · 打开问题中心",
                OperationsDashboardAdvisor.ACTION_ALERTS);
    }

    @Test
    public void partialMonitorNeverClaimsTheHostIsStable() throws Exception {
        JSONObject monitor = monitor("ready", false, 0, 0, 0, false, 0);
        monitor.getJSONObject("devices").put("available", false);

        assertRecommendation(monitor, "部分状态暂不可用 · 查看状态",
                OperationsDashboardAdvisor.ACTION_MONITOR);
    }

    @Test
    public void stableHostMakesDisabledAttentionRemindersActionable() throws Exception {
        JSONObject stable = monitor("ready", false, 0, 0, 0, false, 0);

        OperationsDashboardAdvisor.Recommendation withoutReminders =
                OperationsDashboardAdvisor.fromMonitor(stable, false);
        OperationsDashboardAdvisor.Recommendation withReminders =
                OperationsDashboardAdvisor.fromMonitor(stable, true);

        assertEquals("运维提醒未开启 · 前往设置", withoutReminders.label);
        assertEquals(OperationsDashboardAdvisor.ACTION_NOTIFICATION_SETTINGS,
                withoutReminders.action);
        assertEquals("当前运行稳定 · 查看状态", withReminders.label);
        assertEquals(OperationsDashboardAdvisor.ACTION_MONITOR, withReminders.action);
    }

    @Test
    public void operationalProblemsRemainAheadOfReminderSetup() throws Exception {
        JSONObject warning = monitor("ready", false, 0, 0, 0, false, 2);
        JSONObject activeFlow = monitor("ready", true, 0, 0, 0, false, 0);

        assertRecommendationWithoutReminders(warning,
                "警告 2 个 · 打开问题中心", OperationsDashboardAdvisor.ACTION_ALERTS);
        assertRecommendationWithoutReminders(activeFlow,
                "检测运行中 · 查看进度", OperationsDashboardAdvisor.ACTION_FLOW);
    }

    private static void assertRecommendation(JSONObject monitor, String label, String action) {
        OperationsDashboardAdvisor.Recommendation recommendation =
                OperationsDashboardAdvisor.fromMonitor(monitor, true);
        assertEquals(label, recommendation.label);
        assertEquals(action, recommendation.action);
    }

    private static void assertRecommendationWithoutReminders(
            JSONObject monitor, String label, String action) {
        OperationsDashboardAdvisor.Recommendation recommendation =
                OperationsDashboardAdvisor.fromMonitor(monitor, false);
        assertEquals(label, recommendation.label);
        assertEquals(action, recommendation.action);
    }

    private static JSONObject monitor(
            String uiState,
            boolean flowActive,
            int criticalCount,
            int errorCount,
            int attentionCount,
            boolean messageAttention,
            int warningCount) throws Exception {
        return new JSONObject()
                .put("flow", new JSONObject()
                        .put("available", true)
                        .put("isActive", flowActive))
                .put("devices", new JSONObject()
                        .put("available", true)
                        .put("attentionCount", attentionCount))
                .put("messageChannel", new JSONObject()
                        .put("available", true)
                        .put("attentionRequired", messageAttention))
                .put("alerts", new JSONObject()
                        .put("criticalCount", criticalCount)
                        .put("errorCount", errorCount)
                        .put("warningCount", warningCount))
                .put("performance", new JSONObject()
                        .put("mainUi", new JSONObject().put("state", uiState)));
    }
}

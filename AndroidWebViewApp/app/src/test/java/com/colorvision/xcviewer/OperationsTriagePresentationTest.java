package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsTriagePresentationTest {
    @Test
    public void attentionReportSeparatesMetricsFindingsAndSafeActions() throws Exception {
        JSONObject report = new JSONObject("{"
                + "\"state\":\"attention\","
                + "\"summary\":\"发现 1 项需要关注的状态。\","
                + "\"criticalCount\":0,"
                + "\"errorCount\":0,"
                + "\"warningCount\":0,"
                + "\"pendingJobCount\":0,"
                + "\"messageChannelState\":\"connected\","
                + "\"messageChannelActiveSubscriptionCount\":8,"
                + "\"messageChannelRegisteredSubscriptionCount\":8,"
                + "\"deviceTotalCount\":6,"
                + "\"deviceReadyCount\":2,"
                + "\"deviceClosedCount\":2,"
                + "\"deviceAttentionCount\":2,"
                + "\"deviceOfflineCount\":2,"
                + "\"findings\":[{"
                + "\"severity\":\"warning\","
                + "\"category\":\"devices\","
                + "\"title\":\"检测设备存在不可用状态\","
                + "\"summary\":\"相机类与光谱类需要复核。\","
                + "\"evidenceCount\":2,"
                + "\"latestAt\":\"2026-08-18T08:32:00Z\","
                + "\"actions\":[{"
                + "\"actionId\":\"triage.devices.view\","
                + "\"title\":\"查看设备状态概览\","
                + "\"description\":\"只查看脱敏汇总。\","
                + "\"riskLevel\":\"read-only\"}]}]}");

        OperationsTriagePresentation.ViewModel model =
                OperationsTriagePresentation.from(report, value -> "格式化 " + value);

        assertEquals("发现需要关注的状态", model.stateLabel);
        assertEquals(OperationsTriagePresentation.TONE_ATTENTION, model.tone);
        assertEquals(4, model.metrics.size());
        assertEquals("已连接 · 订阅就绪 · 订阅 8/8", model.metrics.get(2).summary);
        assertEquals("需关注 2 / 共 6 · 就绪 2 · 已关闭 2 · 离线 2",
                model.metrics.get(3).summary);
        assertEquals(1, model.findings.size());
        OperationsTriagePresentation.Finding finding = model.findings.get(0);
        assertEquals("警告 · 检测设备 · 2 条证据", finding.evidenceLabel());
        assertEquals("格式化 2026-08-18T08:32:00Z", finding.latestAt);
        assertEquals(1, finding.actions.size());
        assertTrue(finding.actions.get(0).readOnly());
        assertTrue(OperationsTriagePresentation.isSupportedAction(
                finding.actions.get(0).actionId));
    }

    @Test
    public void criticalReportUsesErrorTone() throws Exception {
        OperationsTriagePresentation.ViewModel model = OperationsTriagePresentation.from(
                new JSONObject("{\"state\":\"critical\",\"criticalCount\":2}"), value -> value);

        assertEquals("发现严重事件 · 请优先复核", model.stateLabel);
        assertEquals(OperationsTriagePresentation.TONE_ERROR, model.tone);
        assertEquals(OperationsTriagePresentation.TONE_ERROR, model.metrics.get(0).tone);
    }

    @Test
    public void healthyReportKeepsAnExplicitEmptyState() throws Exception {
        OperationsTriagePresentation.ViewModel model = OperationsTriagePresentation.from(
                new JSONObject("{"
                        + "\"state\":\"healthy\","
                        + "\"summary\":\"当前有界证据中没有需要处理的项目。\","
                        + "\"messageChannelState\":\"connected\"}"),
                value -> value);

        assertEquals("当前有界证据正常", model.stateLabel);
        assertEquals(OperationsTriagePresentation.TONE_NORMAL, model.tone);
        assertTrue(model.findings.isEmpty());
        assertEquals("当前有界证据中没有需要处理的项目。", model.summary);
    }

    @Test
    public void actionRequirementsStayVisibleAndUnknownActionsStayUnavailable() throws Exception {
        JSONObject report = new JSONObject("{\"findings\":[{\"actions\":[{"
                + "\"actionId\":\"triage.jobs.review\","
                + "\"title\":\"批准作业\","
                + "\"requiresConfirmation\":true,"
                + "\"requiresLocalCoSign\":true}]}]}");

        OperationsTriagePresentation.Action action = OperationsTriagePresentation
                .from(report, value -> value).findings.get(0).actions.get(0);

        assertEquals("批准作业（需电脑共签）", action.buttonLabel());
        assertTrue(OperationsTriagePresentation.isSupportedAction("triage.jobs.review"));
        assertFalse(OperationsTriagePresentation.isSupportedAction("triage.unknown"));
    }
}

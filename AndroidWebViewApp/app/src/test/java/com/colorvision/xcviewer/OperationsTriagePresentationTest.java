package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertSame;
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

        assertEquals("需要关注 · 1 项待复核", model.stateLabel);
        assertEquals(OperationsTriagePresentation.TONE_ATTENTION, model.tone);
        assertEquals(4, model.metrics.size());
        assertEquals("triage.events.view", model.metrics.get(0).actionId);
        assertEquals("triage.jobs.review", model.metrics.get(1).actionId);
        assertEquals("triage.messaging.view", model.metrics.get(2).actionId);
        assertEquals("triage.devices.view", model.metrics.get(3).actionId);
        assertEquals("已连接 · 订阅就绪 · 订阅 8/8", model.metrics.get(2).summary);
        assertEquals("需关注\u00a02/6 · 就绪\u00a02 · 关闭\u00a02 · 离线\u00a02",
                model.metrics.get(3).summary);
        assertEquals("检测设备，需关注 2 / 共 6，就绪 2，已关闭 2，离线 2，点按查看详情",
                model.metrics.get(3).accessibilityLabel());
        assertEquals(1, model.findings.size());
        assertEquals("优先处理", model.prioritySectionLabel());
        OperationsTriagePresentation.Finding finding = model.findings.get(0);
        assertEquals("警告 · 检测设备 · 2 条证据", finding.evidenceLabel());
        assertEquals("格式化 2026-08-18T08:32:00Z", finding.latestAt);
        assertEquals("警告 · 检测设备 · 2 条证据 · 格式化 2026-08-18T08:32:00Z",
                finding.listMetaLabel());
        assertEquals(1, finding.actions.size());
        assertTrue(finding.actions.get(0).readOnly());
        assertSame(finding.actions.get(0), finding.primaryCardAction());
        assertEquals("警告 · 检测设备 · 2 条证据。检测设备存在不可用状态。"
                        + "相机类与光谱类需要复核。最近证据 格式化 2026-08-18T08:32:00Z。"
                        + "点按查看设备状态概览",
                finding.cardAccessibilityLabel(finding.actions.get(0)));
        assertTrue(OperationsTriagePresentation.isSupportedAction(
                finding.actions.get(0).actionId));
    }

    @Test
    public void criticalReportUsesErrorTone() throws Exception {
        OperationsTriagePresentation.ViewModel model = OperationsTriagePresentation.from(
                new JSONObject("{\"state\":\"critical\",\"criticalCount\":2,"
                        + "\"findings\":[{},{}]}"), value -> value);

        assertEquals("严重事件 · 2 项待复核", model.stateLabel);
        assertEquals("优先处理 · 2", model.prioritySectionLabel());
        assertEquals(OperationsTriagePresentation.TONE_ERROR, model.tone);
        assertEquals(OperationsTriagePresentation.TONE_ERROR, model.metrics.get(0).tone);
    }

    @Test
    public void findingWithoutTimestampKeepsItsMetadataCompact() {
        OperationsTriagePresentation.Finding finding = new OperationsTriagePresentation.Finding(
                "diagnostics-warning",
                "a".repeat(64),
                false,
                "warning",
                "警告",
                "diagnostics",
                "诊断事件",
                "近期存在警告事件",
                "有界日志摘要需要复核。",
                3,
                "",
                java.util.Collections.emptyList());

        assertEquals("警告 · 诊断事件 · 3 条证据", finding.listMetaLabel());
    }

    @Test
    public void localReviewSeparatesPendingEvidenceWithoutClaimingResolution() throws Exception {
        JSONObject report = new JSONObject("{\"state\":\"attention\",\"findings\":["
                + "{\"findingId\":\"devices\",\"severity\":\"warning\","
                + "\"category\":\"devices\",\"title\":\"设备离线\","
                + "\"summary\":\"离线 2 台\",\"evidenceCount\":2},"
                + "{\"findingId\":\"diagnostics\",\"severity\":\"warning\","
                + "\"category\":\"diagnostics\",\"title\":\"近期警告\","
                + "\"summary\":\"警告 3 条\",\"evidenceCount\":3}]} ");
        OperationsTriagePresentation.ViewModel raw =
                OperationsTriagePresentation.from(report, value -> value);

        OperationsTriagePresentation.ViewModel partiallyReviewed =
                OperationsTriagePresentation.withAcknowledgements(
                        raw, (findingId, revision) -> "devices".equals(findingId));

        assertEquals("需要关注 · 1 项待复核", partiallyReviewed.stateLabel);
        assertEquals(2, partiallyReviewed.findings.size());
        assertEquals(1, partiallyReviewed.pendingFindings.size());
        assertEquals(1, partiallyReviewed.reviewedFindings.size());
        assertTrue(partiallyReviewed.reviewedFindings.get(0).listMetaLabel()
                .startsWith("已复核 · "));
        assertEquals("优先处理", partiallyReviewed.prioritySectionLabel());
        assertEquals("已复核 · 状态仍存在", partiallyReviewed.reviewedSectionLabel());
        assertEquals(OperationsWatchHistory.attentionState(
                        OperationsWatchPolicy.ATTENTION_DEVICES),
                partiallyReviewed.watchState());

        OperationsTriagePresentation.ViewModel allReviewed =
                OperationsTriagePresentation.withAcknowledgements(
                        raw, (findingId, revision) -> true);
        assertEquals("2 项已复核 · 状态仍存在", allReviewed.stateLabel);
        assertTrue(allReviewed.pendingFindings.isEmpty());
        assertEquals(2, allReviewed.reviewedFindings.size());
        assertEquals(OperationsWatchHistory.attentionState(
                        OperationsWatchPolicy.ATTENTION_DEVICES),
                allReviewed.watchState());
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
                + "\"riskLevel\":\"high-risk\","
                + "\"requiresConfirmation\":true,"
                + "\"requiresLocalCoSign\":true}]}]}");

        OperationsTriagePresentation.Action action = OperationsTriagePresentation
                .from(report, value -> value).findings.get(0).actions.get(0);

        assertEquals("批准作业（需电脑共签）", action.buttonLabel());
        assertNull(OperationsTriagePresentation
                .from(report, value -> value).findings.get(0).primaryCardAction());
        assertTrue(OperationsTriagePresentation.isSupportedAction("triage.jobs.review"));
        assertFalse(OperationsTriagePresentation.isSupportedAction("triage.unknown"));
    }

    @Test
    public void failedRefreshLabelsAVisiblePreviousReportAsReferenceOnly() {
        assertEquals("电脑端暂不可达。",
                OperationsTriagePresentation.failureDetails(
                        "电脑端暂不可达。", false));
        assertEquals("电脑端暂不可达。\n\n"
                        + "下方保留上次成功的排障摘要，仅供参考；恢复连接后请重新刷新。",
                OperationsTriagePresentation.failureDetails(
                        "电脑端暂不可达。", true));
    }
}

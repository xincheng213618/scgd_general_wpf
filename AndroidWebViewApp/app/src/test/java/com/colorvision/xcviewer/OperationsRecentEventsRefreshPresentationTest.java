package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsRecentEventsRefreshPresentationTest {
    @Test
    public void unchangedEvidenceConfirmsSuccessfulRefreshWithoutNewAbnormality()
            throws Exception {
        OperationsRecentEventsRefreshPresentation.Snapshot previous = snapshot(
                2, 0, 0,
                event("warning-a", "warning", "10:00"),
                event("warning-b", "warning", "10:01"));
        OperationsRecentEventsRefreshPresentation.Snapshot current = snapshot(
                2, 0, 0,
                event("warning-a", "warning", "10:00"),
                event("warning-b", "warning", "10:01"));

        assertEquals("刷新完成 · 未发现新增异常证据",
                OperationsRecentEventsRefreshPresentation.feedback(previous, current));
        assertFalse(OperationsRecentEventsRefreshPresentation.hasNewEvidence(
                previous, current));
    }

    @Test
    public void changedEvidenceIsDetectedEvenWhenBoundedTotalsStayEqual()
            throws Exception {
        OperationsRecentEventsRefreshPresentation.Snapshot previous = snapshot(
                1, 1, 0,
                event("warning-a", "warning", "10:00"),
                event("error-a", "error", "10:01"));
        OperationsRecentEventsRefreshPresentation.Snapshot current = snapshot(
                1, 1, 0,
                event("error-new", "error", "10:02"),
                event("warning-a", "warning", "10:00"));

        assertEquals("刷新完成 · 发现 1 条新增异常证据 · 错误 1",
                OperationsRecentEventsRefreshPresentation.feedback(previous, current));
        assertTrue(OperationsRecentEventsRefreshPresentation.hasNewEvidence(
                previous, current));
    }

    @Test
    public void aggregateIncreaseStillReportsEvidenceOutsideReturnedSample()
            throws Exception {
        OperationsRecentEventsRefreshPresentation.Snapshot previous = snapshot(2, 0, 0);
        OperationsRecentEventsRefreshPresentation.Snapshot current = snapshot(4, 1, 0);

        assertEquals("刷新完成 · 异常计数增加 · 错误 +1 · 警告 +2",
                OperationsRecentEventsRefreshPresentation.feedback(previous, current));
        assertTrue(OperationsRecentEventsRefreshPresentation.hasNewEvidence(
                previous, current));
    }

    @Test
    public void lowerBoundedCountDoesNotClaimRecovery() throws Exception {
        OperationsRecentEventsRefreshPresentation.Snapshot previous = snapshot(5, 1, 0);
        OperationsRecentEventsRefreshPresentation.Snapshot current = snapshot(2, 0, 0);

        assertEquals("刷新完成 · 日志窗口已更新，当前异常 2 条；计数下降不代表已恢复",
                OperationsRecentEventsRefreshPresentation.feedback(previous, current));
        assertFalse(OperationsRecentEventsRefreshPresentation.hasNewEvidence(
                previous, current));
    }

    @Test
    public void unavailableTransitionsRemainExplicit() throws Exception {
        OperationsRecentEventsRefreshPresentation.Snapshot unavailable =
                OperationsRecentEventsRefreshPresentation.capture(
                        new JSONObject().put("available", false));
        OperationsRecentEventsRefreshPresentation.Snapshot available = snapshot(1, 0, 0);

        assertEquals("刷新完成，但近期事件当前不可用",
                OperationsRecentEventsRefreshPresentation.feedback(available, unavailable));
        assertEquals("刷新完成 · 近期事件已恢复可用 · 当前异常 1 条",
                OperationsRecentEventsRefreshPresentation.feedback(unavailable, available));
    }

    @Test
    public void missingAlertIdUsesStableInMemoryFallbackRevision() throws Exception {
        JSONObject event = new JSONObject()
                .put("severity", "warning")
                .put("source", "安全运维")
                .put("occurredAt", "2026-08-19T10:00:00Z")
                .put("summary", "脱敏摘要");
        OperationsRecentEventsRefreshPresentation.Snapshot previous = snapshot(1, 0, 0, event);
        OperationsRecentEventsRefreshPresentation.Snapshot current = snapshot(1, 0, 0, event);

        assertEquals("刷新完成 · 未发现新增异常证据",
                OperationsRecentEventsRefreshPresentation.feedback(previous, current));
    }

    private static OperationsRecentEventsRefreshPresentation.Snapshot snapshot(
            int warnings,
            int errors,
            int critical,
            JSONObject... events) throws Exception {
        JSONArray recentEvents = new JSONArray();
        for (JSONObject event : events) {
            recentEvents.put(event);
        }
        return OperationsRecentEventsRefreshPresentation.capture(new JSONObject()
                .put("available", true)
                .put("warningCount", warnings)
                .put("errorCount", errors)
                .put("criticalCount", critical)
                .put("recentEvents", recentEvents));
    }

    private static JSONObject event(String id, String severity, String occurredAt)
            throws Exception {
        return new JSONObject()
                .put("alertId", id)
                .put("severity", severity)
                .put("source", "应用")
                .put("occurredAt", occurredAt)
                .put("summary", "脱敏摘要");
    }
}

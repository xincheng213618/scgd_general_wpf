package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsRecentEventsPresentationTest {
    @Test
    public void availableSampleSeparatesSummarySourcesEventsAndPrivacyBoundary()
            throws Exception {
        JSONObject payload = new JSONObject("{"
                + "\"available\":true,"
                + "\"scannedLineCount\":122,"
                + "\"parsedEventCount\":115,"
                + "\"infoCount\":113,"
                + "\"warningCount\":2,"
                + "\"errorCount\":0,"
                + "\"criticalCount\":0,"
                + "\"tailWasBounded\":false,"
                + "\"categories\":["
                + "{\"category\":\"应用\",\"count\":88},"
                + "{\"category\":\"服务\",\"count\":10}],"
                + "\"recentEvents\":[{"
                + "\"severity\":\"warning\","
                + "\"source\":\"安全运维\","
                + "\"occurredAt\":\"2026-08-19T06:33:00Z\","
                + "\"summary\":\"安全运维通道处理请求失败。\"}],"
                + "\"privacyNotice\":\"只返回脱敏事件。\"}");

        OperationsRecentEventsPresentation.ViewModel model =
                OperationsRecentEventsPresentation.from(
                        payload, value -> "格式化 " + value);

        assertTrue(model.available);
        assertEquals("2 条近期异常", model.stateLabel);
        assertEquals("扫描 122 行 · 识别 115 个事件", model.sampleSummary);
        assertEquals("信息 113 · 警告 2 · 错误 0 · 严重 0", model.severitySummary);
        assertEquals("应用 88 · 服务 10", model.categorySummary);
        assertEquals("最近日志尾部 · 最多 500 行 / 256 KiB", model.rangeSummary);
        assertEquals("只返回脱敏事件。", model.privacyNotice);
        assertEquals(OperationsRecentEventsPresentation.TONE_ATTENTION, model.tone);
        assertEquals(1, model.hiddenEventCount);
        assertEquals("近期异常事件 · 1", model.eventsSectionLabel());

        OperationsRecentEventsPresentation.Event event = model.events.get(0);
        assertEquals("警告 · 安全运维 · 格式化 2026-08-19T06:33:00Z",
                event.metadataLabel());
        assertEquals("警告，安全运维，格式化 2026-08-19T06:33:00Z。"
                        + "安全运维通道处理请求失败。",
                event.accessibilityLabel());
        assertEquals(OperationsRecentEventsPresentation.TONE_ATTENTION, event.tone);
    }

    @Test
    public void unavailableAndHealthySamplesKeepExplicitStates() throws Exception {
        OperationsRecentEventsPresentation.ViewModel unavailable =
                OperationsRecentEventsPresentation.from(
                        new JSONObject("{\"available\":false}"), value -> value);
        assertFalse(unavailable.available);
        assertEquals("近期事件不可用", unavailable.stateLabel);
        assertTrue(unavailable.events.isEmpty());

        OperationsRecentEventsPresentation.ViewModel healthy =
                OperationsRecentEventsPresentation.from(
                        new JSONObject("{\"available\":true}"), value -> value);
        assertTrue(healthy.available);
        assertEquals("近期没有异常事件", healthy.stateLabel);
        assertEquals("近期异常事件", healthy.eventsSectionLabel());
        assertTrue(healthy.events.isEmpty());
    }

    @Test
    public void eventListIsBoundedAndErrorToneWins() throws Exception {
        JSONArray events = new JSONArray();
        for (int index = 0; index < 15; index++) {
            events.put(new JSONObject()
                    .put("severity", index == 0 ? "critical" : "warning")
                    .put("source", "应用")
                    .put("summary", "事件 " + index));
        }
        JSONObject payload = new JSONObject()
                .put("available", true)
                .put("criticalCount", 1)
                .put("warningCount", 14)
                .put("recentEvents", events);

        OperationsRecentEventsPresentation.ViewModel model =
                OperationsRecentEventsPresentation.from(payload, value -> value);

        assertEquals(12, model.events.size());
        assertEquals(3, model.hiddenEventCount);
        assertEquals(OperationsRecentEventsPresentation.TONE_ERROR, model.tone);
        assertEquals(OperationsRecentEventsPresentation.TONE_ERROR,
                model.events.get(0).tone);
    }
}

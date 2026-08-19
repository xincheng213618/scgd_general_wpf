package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsAuditPresentationTest {
    @Test
    public void routineMonitorReadsAreVisibleButFoldedFromTheDefaultSignalList()
            throws Exception {
        JSONObject payload = new JSONObject("{\"entries\":["
                + "{\"action\":\"monitor.read\",\"outcome\":\"success\","
                + "\"actorType\":\"device\",\"timestamp\":\"2026-08-19T08:51:00Z\"},"
                + "{\"action\":\"diagnostics.failure-evidence.read\","
                + "\"outcome\":\"failed\",\"actorType\":\"device\","
                + "\"timestamp\":\"2026-08-19T08:53:00Z\"},"
                + "{\"action\":\"job.local_reject\",\"outcome\":\"rejected_local\","
                + "\"actorType\":\"local-user\"}]}");

        OperationsAuditPresentation.ViewModel model = OperationsAuditPresentation.from(
                payload, value -> "格式化 " + value);

        assertEquals("3 条近期记录", model.stateLabel);
        assertEquals(1, model.successCount);
        assertEquals(1, model.attentionCount);
        assertEquals(1, model.errorCount);
        assertEquals(1, model.routineCount);
        assertEquals(3, model.entries.size());
        assertEquals(2, model.focusedEntries.size());
        assertTrue(model.defaultsToFocusedEntries());
        assertEquals("成功 1 · 待复核 1 · 失败 1", model.summaryLabel());

        OperationsAuditPresentation.Entry failed = model.focusedEntries.get(0);
        assertEquals("读取崩溃与卡死线索", failed.actionLabel);
        assertEquals("失败 · 已配对手机 · 格式化 2026-08-19T08:53:00Z",
                failed.metadataLabel());
        assertEquals(OperationsAuditPresentation.TONE_ERROR, failed.tone);
        assertEquals("读取崩溃与卡死线索。失败，已配对手机，"
                        + "格式化 2026-08-19T08:53:00Z",
                failed.accessibilityLabel());
    }

    @Test
    public void emptyTimelineHasAnExplicitStateAndPrivacyBoundary() throws Exception {
        OperationsAuditPresentation.ViewModel model = OperationsAuditPresentation.from(
                new JSONObject(), value -> value);

        assertEquals("暂无近期操作记录", model.stateLabel);
        assertTrue(model.entries.isEmpty());
        assertFalse(model.defaultsToFocusedEntries());
        assertTrue(model.plainText().contains("当前没有远程操作记录"));
        assertTrue(model.plainText().contains("不返回设备 ID"));
    }

    @Test
    public void timelineIsBoundedToThirtyEntries() throws Exception {
        JSONArray entries = new JSONArray();
        for (int index = 0; index < 35; index++) {
            entries.put(new JSONObject()
                    .put("action", "desktop.action.execute")
                    .put("outcome", "completed")
                    .put("actorType", "device"));
        }

        OperationsAuditPresentation.ViewModel model = OperationsAuditPresentation.from(
                new JSONObject().put("entries", entries), value -> value);

        assertEquals(30, model.entries.size());
        assertEquals(5, model.hiddenEntryCount);
        assertEquals(30, model.focusedEntries.size());
        assertFalse(model.defaultsToFocusedEntries());
    }

    @Test
    public void fixedLabelsDoNotExposeRawProtocolValues() {
        assertEquals("手机批准作业",
                OperationsAuditPresentation.actionLabel("job.approve"));
        assertEquals("支持中继",
                OperationsAuditPresentation.actorLabel("support-relay"));
        assertEquals("等待电脑确认",
                OperationsAuditPresentation.outcomeLabel("awaiting_local_consent"));
        assertEquals("受控运维活动",
                OperationsAuditPresentation.actionLabel("arbitrary.action"));
    }
}

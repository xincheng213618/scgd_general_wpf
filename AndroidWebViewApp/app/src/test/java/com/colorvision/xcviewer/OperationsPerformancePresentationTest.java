package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsPerformancePresentationTest {
    @Test
    public void healthySampleSeparatesUiCpuMemoryResourcesAndCaptureTime()
            throws Exception {
        JSONObject payload = new JSONObject()
                .put("cpuPercent", 12.6)
                .put("sampleMilliseconds", 304)
                .put("workingSetMb", 421.9)
                .put("privateMemoryMb", 396.0)
                .put("managedHeapMb", 56.8)
                .put("threadCount", 25)
                .put("handleCount", 1602)
                .put("capturedAt", "2026-08-19T02:00:00Z")
                .put("garbageCollection", new JSONObject()
                        .put("gen0Collections", 103)
                        .put("gen1Collections", 24)
                        .put("gen2Collections", 5))
                .put("mainUi", new JSONObject()
                        .put("available", true)
                        .put("state", "responsive")
                        .put("latencyMilliseconds", 34));

        OperationsPerformancePresentation.ViewModel model =
                OperationsPerformancePresentation.from(
                        payload, value -> "格式化 " + value, false);

        assertTrue(model.available);
        assertEquals("性能正常 · CPU 12.6%", model.stateLabel);
        assertEquals("主界面响应正常 · 34 ms", model.summaryLabel);
        assertEquals("CPU 12.6% · 短采样 304 ms", model.sampleLabel);
        assertEquals("采集于 格式化 2026-08-19T02:00:00Z", model.capturedLabel);
        assertEquals("现场短采样", model.sourceLabel);
        assertEquals("", model.integrityNotice);
        assertEquals(OperationsPerformancePresentation.TONE_NORMAL, model.tone);
        assertEquals("工作集", model.memoryMetrics.get(0).label);
        assertEquals("421.9 MB", model.memoryMetrics.get(0).value);
        assertEquals("396 MB", model.memoryMetrics.get(1).value);
        assertEquals("GC 次数", model.resourceMetrics.get(2).label);
        assertEquals("Gen0 103 · Gen1 24 · Gen2 5",
                model.resourceMetrics.get(2).value);
    }

    @Test
    public void slowUnresponsiveAndUnavailableUiUseDistinctStates() throws Exception {
        JSONObject payload = new JSONObject()
                .put("cpuPercent", 2)
                .put("mainUi", new JSONObject()
                        .put("available", true)
                        .put("state", "slow")
                        .put("latencyMilliseconds", 720));
        OperationsPerformancePresentation.ViewModel slow =
                OperationsPerformancePresentation.from(payload, value -> value, false);
        assertEquals("主界面响应偏慢 · CPU 2%", slow.stateLabel);
        assertEquals(OperationsPerformancePresentation.TONE_ATTENTION, slow.tone);

        payload.put("mainUi", new JSONObject()
                .put("available", true)
                .put("state", "unresponsive"));
        OperationsPerformancePresentation.ViewModel unresponsive =
                OperationsPerformancePresentation.from(payload, value -> value, false);
        assertEquals("主界面无响应 · CPU 2%", unresponsive.stateLabel);
        assertEquals(OperationsPerformancePresentation.TONE_ERROR, unresponsive.tone);

        payload.put("mainUi", new JSONObject().put("available", false));
        OperationsPerformancePresentation.ViewModel unavailableUi =
                OperationsPerformancePresentation.from(payload, value -> value, false);
        assertEquals("主界面状态不可用 · CPU 2%", unavailableUi.stateLabel);
        assertEquals("主界面响应当前不可探测", unavailableUi.summaryLabel);
        assertEquals(OperationsPerformancePresentation.TONE_ATTENTION, unavailableUi.tone);
    }

    @Test
    public void signedSnapshotMakesIntegrityBoundaryExplicit() throws Exception {
        OperationsPerformancePresentation.ViewModel model =
                OperationsPerformancePresentation.from(
                        new JSONObject().put("mainUi", new JSONObject()
                                .put("available", true)
                                .put("state", "responsive")),
                        value -> value,
                        true);

        assertEquals("电脑签名短采样", model.sourceLabel);
        assertEquals(OperationsPerformancePresentation.SIGNED_NOTICE,
                model.integrityNotice);
    }

    @Test
    public void missingAndOutOfRangeValuesStayBounded() throws Exception {
        OperationsPerformancePresentation.ViewModel missing =
                OperationsPerformancePresentation.from(null, value -> value, true);
        assertFalse(missing.available);
        assertEquals("性能状态不可用", missing.stateLabel);

        JSONObject payload = new JSONObject()
                .put("cpuPercent", 500)
                .put("sampleMilliseconds", 50_000)
                .put("workingSetMb", -1)
                .put("privateMemoryMb", 2_000_000)
                .put("threadCount", -4)
                .put("handleCount", Integer.MAX_VALUE)
                .put("mainUi", new JSONObject()
                        .put("available", true)
                        .put("state", "responsive")
                        .put("latencyMilliseconds", 500_000));

        OperationsPerformancePresentation.ViewModel bounded =
                OperationsPerformancePresentation.from(payload, value -> value, false);

        assertEquals("性能正常 · CPU 100%", bounded.stateLabel);
        assertEquals("主界面响应正常 · 60000 ms", bounded.summaryLabel);
        assertEquals("CPU 100% · 短采样 2000 ms", bounded.sampleLabel);
        assertEquals("0 MB", bounded.memoryMetrics.get(0).value);
        assertEquals("1048576 MB", bounded.memoryMetrics.get(1).value);
        assertEquals("0", bounded.resourceMetrics.get(0).value);
        assertEquals("999999999", bounded.resourceMetrics.get(1).value);
    }
}

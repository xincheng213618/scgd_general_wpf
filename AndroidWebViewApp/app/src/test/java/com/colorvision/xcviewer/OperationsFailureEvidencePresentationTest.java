package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

public class OperationsFailureEvidencePresentationTest {
    @Test
    public void boundedNoFindingStateSeparatesConclusionCountsAndScanCoverage()
            throws Exception {
        OperationsFailureEvidence.Snapshot snapshot =
                OperationsFailureEvidence.fromLocalPayload(new JSONObject()
                        .put("eventLogAvailable", true)
                        .put("dumpFolderAvailable", true)
                        .put("eventScanLimited", true)
                        .put("dumpScanLimited", false)
                        .put("hasEvidence", false)
                        .put("windowDays", 7));

        OperationsFailureEvidencePresentation.ViewModel model =
                OperationsFailureEvidencePresentation.from(snapshot, "");

        assertEquals("最近 7 天 · 未发现线索 · 扫描有界", model.stateLabel);
        assertEquals("安全上限内未发现崩溃、卡死或本机转储线索", model.summaryLabel);
        assertEquals("失败事件 0 · 本机转储 0", model.countSummary);
        assertEquals(OperationsFailureEvidencePresentation.TONE_ATTENTION, model.tone);
        assertEquals(5, model.categories.size());
        assertEquals("应用崩溃", model.categories.get(0).label);
        assertEquals("0 条", model.categories.get(0).countLabel());
        assertEquals("已读取 · 扫描有界", model.sources.get(0).statusLabel);
        assertEquals("结果仅覆盖安全上限内的条目。",
                model.sources.get(0).supportingLabel);
        assertEquals("已读取", model.sources.get(1).statusLabel);
    }

    @Test
    public void evidenceStateShowsAggregateCountsCategoriesAndLatestTime()
            throws Exception {
        OperationsFailureEvidence.Snapshot snapshot =
                OperationsFailureEvidence.fromLocalPayload(new JSONObject()
                        .put("eventLogAvailable", true)
                        .put("dumpFolderAvailable", true)
                        .put("hasEvidence", true)
                        .put("failureEventCount", 6)
                        .put("crashCount", 2)
                        .put("hangCount", 1)
                        .put("managedRuntimeFailureCount", 2)
                        .put("windowsErrorReportCount", 1)
                        .put("dumpCount", 3)
                        .put("latestEvidenceAt", "2026-08-19T01:30:00Z"));

        OperationsFailureEvidencePresentation.ViewModel model =
                OperationsFailureEvidencePresentation.from(snapshot, "08-19 09:30");

        assertEquals("最近 7 天 · 9 条聚合线索", model.stateLabel);
        assertEquals("发现崩溃、卡死或本机转储线索", model.summaryLabel);
        assertEquals("失败事件 6 · 本机转储 3", model.countSummary);
        assertEquals("最近线索 · 08-19 09:30", model.latestLabel);
        assertEquals(OperationsFailureEvidencePresentation.TONE_ERROR, model.tone);
        assertEquals("2 条", model.categories.get(0).countLabel());
        assertEquals("1 条", model.categories.get(1).countLabel());
        assertEquals("3 个", model.categories.get(4).countLabel());
    }

    @Test
    public void partialAndUnavailableSourcesRemainExplicit() throws Exception {
        OperationsFailureEvidence.Snapshot partialSnapshot =
                OperationsFailureEvidence.fromLocalPayload(new JSONObject()
                        .put("eventLogAvailable", true)
                        .put("dumpFolderAvailable", false));
        OperationsFailureEvidencePresentation.ViewModel partial =
                OperationsFailureEvidencePresentation.from(partialSnapshot, "");

        assertEquals("最近 7 天 · 未发现线索 · 部分来源不可用", partial.stateLabel);
        assertEquals("当前可读取来源中未发现故障线索", partial.summaryLabel);
        assertEquals("不可读取", partial.sources.get(1).statusLabel);
        assertEquals(OperationsFailureEvidencePresentation.TONE_ERROR,
                partial.sources.get(1).tone);
        assertTrue(partial.sources.get(1).accessibilityLabel()
                .contains("本次结果不包含此来源"));

        OperationsFailureEvidence.Snapshot unavailableSnapshot =
                OperationsFailureEvidence.fromLocalPayload(new JSONObject());
        OperationsFailureEvidencePresentation.ViewModel unavailable =
                OperationsFailureEvidencePresentation.from(unavailableSnapshot, "");

        assertEquals("故障线索不可用", unavailable.stateLabel);
        assertEquals("当前无法读取 Windows 应用事件和本机转储", unavailable.summaryLabel);
        assertEquals(OperationsFailureEvidencePresentation.TONE_ERROR, unavailable.tone);
    }

    @Test
    public void localDisplayClampsCountsAndDerivesEvidenceFromBoundedValues()
            throws Exception {
        OperationsFailureEvidence.Snapshot snapshot =
                OperationsFailureEvidence.fromLocalPayload(new JSONObject()
                        .put("eventLogAvailable", true)
                        .put("dumpFolderAvailable", true)
                        .put("hasEvidence", false)
                        .put("failureEventCount", -12)
                        .put("crashCount", -3)
                        .put("dumpCount", 5000)
                        .put("latestEvidenceAt", "2026-08-19T01:30:00Z"));

        OperationsFailureEvidencePresentation.ViewModel model =
                OperationsFailureEvidencePresentation.from(snapshot, "08-19 09:30");

        assertEquals("最近 7 天 · 999 条聚合线索", model.stateLabel);
        assertEquals("失败事件 0 · 本机转储 999", model.countSummary);
        assertEquals("0 条", model.categories.get(0).countLabel());
        assertEquals("999 个", model.categories.get(4).countLabel());
        assertEquals("最近线索 · 08-19 09:30", model.latestLabel);
    }

    @Test
    public void categoryEvidenceCannotBeHiddenByContradictoryAggregateCount()
            throws Exception {
        OperationsFailureEvidence.Snapshot snapshot =
                OperationsFailureEvidence.fromLocalPayload(new JSONObject()
                        .put("eventLogAvailable", true)
                        .put("dumpFolderAvailable", true)
                        .put("hasEvidence", false)
                        .put("failureEventCount", 0)
                        .put("crashCount", 2)
                        .put("hangCount", 1));

        OperationsFailureEvidencePresentation.ViewModel model =
                OperationsFailureEvidencePresentation.from(snapshot, "");

        assertEquals("最近 7 天 · 3 条聚合线索", model.stateLabel);
        assertEquals("失败事件 3 · 本机转储 0", model.countSummary);
        assertEquals(OperationsFailureEvidencePresentation.TONE_ERROR, model.tone);
    }
}

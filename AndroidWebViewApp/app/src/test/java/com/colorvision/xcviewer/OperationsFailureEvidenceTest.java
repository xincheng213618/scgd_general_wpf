package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;
import static org.junit.Assert.fail;

public class OperationsFailureEvidenceTest {
    @Test
    public void exactCompletedReceiptValidatesAndRendersExistingSafeSummary() throws Exception {
        JSONObject evidence = validEvidence();

        OperationsFailureEvidence.Snapshot snapshot =
                OperationsFailureEvidence.parseStrictReceipt(evidence);
        String rendered = OperationsFailureEvidence.format(snapshot, "08-13 11:30");

        assertEquals(3, snapshot.failureEventCount);
        assertEquals("2026-08-13T11:30:00Z", snapshot.latestEvidenceAt);
        assertTrue(rendered.contains("最近 7 天聚合线索"));
        assertTrue(rendered.contains("失败事件：3 条"));
        assertTrue(rendered.contains("本机转储：1 个"));
        assertTrue(rendered.contains("最近线索：08-13 11:30"));
        assertTrue(rendered.contains("不返回事件正文、文件名、路径、转储内容"));
    }

    @Test
    public void exactSchemaRejectsMissingExtraAndWrongPrimitiveTypes() throws Exception {
        expectInvalid(without(validEvidence(), "kind"));
        expectInvalid(copy(validEvidence()).put("extra", true));
        expectInvalid(copy(validEvidence()).put("kind", "failure-evidence-v2"));
        expectInvalid(copy(validEvidence()).put("hasEvidence", "true"));
        expectInvalid(copy(validEvidence()).put("failureEventCount", 3.0d));
        expectInvalid(copy(validEvidence()).put("failureEventCount", -1));
        expectInvalid(copy(validEvidence()).put("failureEventCount", 1_000));
        expectInvalid(copy(validEvidence()).put("windowDays", 6));
        expectInvalid(copy(validEvidence()).put("latestEventAt", "2026-08-13 10:00:00"));
        expectInvalid(copy(validEvidence()).put("observedAt", JSONObject.NULL));
    }

    @Test
    public void semanticValidationRejectsContradictoryCountsAndEvidenceFlags() throws Exception {
        JSONObject noEvidence = validNoEvidence();
        OperationsFailureEvidence.parseStrictReceipt(noEvidence);

        expectInvalid(copy(noEvidence).put("hasEvidence", true));
        expectInvalid(copy(noEvidence).put("crashCount", 1));
        expectInvalid(copy(validEvidence()).put("hasEvidence", false));
        expectInvalid(copy(validEvidence()).put("latestEventAt", JSONObject.NULL));
        expectInvalid(copy(validEvidence()).put("failureEventCount", 0));
        expectInvalid(copy(validEvidence()).put("latestDumpAt", JSONObject.NULL));
        expectInvalid(copy(validEvidence()).put("dumpCount", 0));
    }

    @Test
    public void semanticValidationRequiresWindowedAndTrulyLatestTimestamps() throws Exception {
        expectInvalid(copy(validEvidence()).put("windowStartedAt", "2026-08-14T12:00:00Z"));
        expectInvalid(copy(validEvidence()).put("latestEventAt", "2026-08-01T10:00:00Z"));
        expectInvalid(copy(validEvidence()).put("latestDumpAt", "2026-08-14T10:00:00Z"));
        expectInvalid(copy(validEvidence()).put("latestEvidenceAt", "2026-08-13T10:00:00Z"));

        JSONObject sameInstantDifferentOffset = copy(validEvidence())
                .put("latestEvidenceAt", "2026-08-13T19:30:00+08:00");
        OperationsFailureEvidence.parseStrictReceipt(sameInstantDifferentOffset);
    }

    @Test
    public void emptyEvidenceRendersTheBoundedNoFindingState() throws Exception {
        OperationsFailureEvidence.Snapshot snapshot =
                OperationsFailureEvidence.parseStrictReceipt(validNoEvidence());
        String rendered = OperationsFailureEvidence.format(snapshot, "");

        assertTrue(rendered.contains("最近 7 天未发现 ColorVision 崩溃、卡死或本机转储线索"));
    }

    @Test
    public void failedReceiptAcceptsOnlyTheExactBoundedUnavailableError() throws Exception {
        JSONObject valid = new JSONObject()
                .put("kind", "failure-evidence-error-v1")
                .put("code", "failure_evidence_unavailable");
        OperationsFailureEvidence.validateStrictErrorReceipt(valid);

        expectInvalidError(without(valid, "code"));
        expectInvalidError(copy(valid).put("extra", true));
        expectInvalidError(copy(valid).put("kind", "failure-evidence-v1"));
        expectInvalidError(copy(valid).put("code", "access_denied"));
        expectInvalidError(copy(valid).put("code", JSONObject.NULL));
    }

    private static JSONObject validEvidence() throws Exception {
        return new JSONObject()
                .put("kind", "failure-evidence-v1")
                .put("eventLogAvailable", true)
                .put("dumpFolderAvailable", true)
                .put("eventScanLimited", false)
                .put("dumpScanLimited", false)
                .put("hasEvidence", true)
                .put("windowDays", 7)
                .put("failureEventCount", 3)
                .put("crashCount", 1)
                .put("hangCount", 1)
                .put("managedRuntimeFailureCount", 1)
                .put("windowsErrorReportCount", 0)
                .put("dumpCount", 1)
                .put("latestEventAt", "2026-08-13T10:00:00.1234567Z")
                .put("latestDumpAt", "2026-08-13T11:30:00Z")
                .put("latestEvidenceAt", "2026-08-13T11:30:00Z")
                .put("windowStartedAt", "2026-08-06T12:00:00Z")
                .put("observedAt", "2026-08-13T12:00:00Z");
    }

    private static JSONObject validNoEvidence() throws Exception {
        return new JSONObject()
                .put("kind", "failure-evidence-v1")
                .put("eventLogAvailable", true)
                .put("dumpFolderAvailable", false)
                .put("eventScanLimited", false)
                .put("dumpScanLimited", false)
                .put("hasEvidence", false)
                .put("windowDays", 7)
                .put("failureEventCount", 0)
                .put("crashCount", 0)
                .put("hangCount", 0)
                .put("managedRuntimeFailureCount", 0)
                .put("windowsErrorReportCount", 0)
                .put("dumpCount", 0)
                .put("latestEventAt", JSONObject.NULL)
                .put("latestDumpAt", JSONObject.NULL)
                .put("latestEvidenceAt", JSONObject.NULL)
                .put("windowStartedAt", "2026-08-06T12:00:00Z")
                .put("observedAt", "2026-08-13T12:00:00Z");
    }

    private static JSONObject copy(JSONObject source) throws Exception {
        return new JSONObject(source.toString());
    }

    private static JSONObject without(JSONObject source, String field) throws Exception {
        JSONObject copy = copy(source);
        copy.remove(field);
        return copy;
    }

    private static void expectInvalid(JSONObject evidence) {
        try {
            OperationsFailureEvidence.parseStrictReceipt(evidence);
            fail("Expected invalid_failure_evidence_receipt");
        } catch (SecurityException ex) {
            assertEquals("invalid_failure_evidence_receipt", ex.getMessage());
        }
    }

    private static void expectInvalidError(JSONObject evidence) {
        try {
            OperationsFailureEvidence.validateStrictErrorReceipt(evidence);
            fail("Expected invalid_failure_evidence_receipt");
        } catch (SecurityException ex) {
            assertEquals("invalid_failure_evidence_receipt", ex.getMessage());
        }
    }
}

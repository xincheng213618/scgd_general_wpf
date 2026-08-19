package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertSame;
import static org.junit.Assert.assertTrue;

public class OperationsTriageDetailReviewPresentationTest {
    @Test
    public void pendingFindingOffersExplicitReviewWithoutClaimingResolution()
            throws Exception {
        OperationsTriagePresentation.Finding finding = finding(false, "old evidence");

        OperationsTriageDetailReviewPresentation.ViewModel model =
                OperationsTriageDetailReviewPresentation.from(finding, false);

        assertTrue(model.visible);
        assertTrue(model.enabled);
        assertFalse(model.acknowledged);
        assertEquals("标记此问题已复核", model.label);
        assertTrue(model.contentDescription.contains("电脑状态不会改变"));
        assertTrue(model.contentDescription.contains("最新证据"));
        assertEquals(OperationsTriageDetailReviewPresentation.SURFACE_RECENT_EVENTS,
                OperationsTriageDetailReviewPresentation.surfaceFor(finding));
    }

    @Test
    public void reviewedFindingOffersUndoAndLoadingPreventsDuplicateRequests()
            throws Exception {
        OperationsTriagePresentation.Finding finding = finding(true, "old evidence");

        OperationsTriageDetailReviewPresentation.ViewModel reviewed =
                OperationsTriageDetailReviewPresentation.from(finding, false);
        OperationsTriageDetailReviewPresentation.ViewModel loading =
                OperationsTriageDetailReviewPresentation.from(finding, true);

        assertEquals("撤销此问题复核", reviewed.label);
        assertTrue(reviewed.enabled);
        assertTrue(reviewed.acknowledged);
        assertEquals("正在核对最新问题证据…", loading.label);
        assertFalse(loading.enabled);
    }

    @Test
    public void latestReportMatchesByStableFindingIdAcrossEvidenceRevision()
            throws Exception {
        OperationsTriagePresentation.Finding reference = finding(false, "old evidence");
        OperationsTriagePresentation.ViewModel latest = OperationsTriagePresentation.from(
                report("new evidence"), value -> value);

        OperationsTriagePresentation.Finding current =
                OperationsTriageDetailReviewPresentation.findCurrent(latest, reference);

        assertSame(latest.findings.get(0), current);
        assertFalse(reference.revision.equals(current.revision));
        assertTrue(OperationsTriageDetailReviewPresentation.requiresEvidenceRefresh(
                current, true, true));
        assertFalse(OperationsTriageDetailReviewPresentation.requiresEvidenceRefresh(
                current, false, true));
        assertFalse(OperationsTriageDetailReviewPresentation.requiresEvidenceRefresh(
                current, true, false));
    }

    @Test
    public void alreadyReviewedLatestEvidenceDoesNotDemandAnotherRefresh()
            throws Exception {
        OperationsTriagePresentation.Finding viewed = finding(false, "old evidence");
        OperationsTriagePresentation.Finding latest = finding(true, "new evidence");

        assertFalse(OperationsTriageDetailReviewPresentation.requiresEvidenceRefresh(
                latest, true, true));
    }

    @Test
    public void disappearedFindingHasNoReviewTarget() throws Exception {
        OperationsTriagePresentation.Finding reference = finding(false, "old evidence");
        OperationsTriagePresentation.ViewModel healthy = OperationsTriagePresentation.from(
                new JSONObject().put("state", "healthy"), value -> value);

        assertNull(OperationsTriageDetailReviewPresentation.findCurrent(healthy, reference));
        assertFalse(OperationsTriageDetailReviewPresentation.from(null, false).visible);
    }

    @Test
    public void deviceFindingUsesDeviceHealthReviewSurface() throws Exception {
        OperationsTriagePresentation.Finding device = OperationsTriagePresentation.from(
                new JSONObject("{\"findings\":[{\"findingId\":\"devices\","
                        + "\"category\":\"devices\"}]}"), value -> value).findings.get(0);

        assertEquals(OperationsTriageDetailReviewPresentation.SURFACE_DEVICE_HEALTH,
                OperationsTriageDetailReviewPresentation.surfaceFor(device));
    }

    private static OperationsTriagePresentation.Finding finding(
            boolean acknowledged,
            String summary) throws Exception {
        OperationsTriagePresentation.ViewModel model = OperationsTriagePresentation.from(
                report(summary), value -> value);
        return model.findings.get(0).withAcknowledged(acknowledged);
    }

    private static JSONObject report(String summary) throws Exception {
        return new JSONObject("{\"state\":\"attention\",\"findings\":[{"
                + "\"findingId\":\"diagnostics\","
                + "\"severity\":\"warning\","
                + "\"category\":\"diagnostics\","
                + "\"title\":\"近期存在警告事件\","
                + "\"summary\":\"" + summary + "\","
                + "\"evidenceCount\":2,"
                + "\"latestAt\":\"2026-08-19T10:00:00Z\"}]} ");
    }
}

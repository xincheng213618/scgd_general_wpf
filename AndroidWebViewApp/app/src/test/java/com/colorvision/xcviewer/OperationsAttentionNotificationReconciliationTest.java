package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsAttentionNotificationReconciliationTest {
    @Test
    public void reviewedDirectProblemClearsOnlyItsCurrentAttentionCategory()
            throws Exception {
        OperationsTriagePresentation.ViewModel raw = directProblems(
                "{\"findingId\":\"devices\",\"category\":\"devices\","
                        + "\"severity\":\"warning\"},"
                        + "{\"findingId\":\"events\",\"category\":\"diagnostics\","
                        + "\"severity\":\"error\"}");
        OperationsTriagePresentation.ViewModel model =
                OperationsTriagePresentation.withAcknowledgements(
                        raw, (findingId, revision) -> "devices".equals(findingId));

        assertTrue(OperationsAttentionNotificationReconciliation.shouldClear(
                attention(OperationsWatchPolicy.ATTENTION_DEVICES), model));
        assertFalse(OperationsAttentionNotificationReconciliation.shouldClear(
                attention(OperationsWatchPolicy.ATTENTION_ERRORS), model));
        assertFalse(OperationsAttentionNotificationReconciliation.shouldClear(
                OperationsWatchHistory.STATE_OFFLINE, model));
    }

    @Test
    public void anotherPendingDirectMatchKeepsTheNotification() throws Exception {
        OperationsTriagePresentation.ViewModel raw = directProblems(
                "{\"findingId\":\"camera\",\"category\":\"devices\","
                        + "\"severity\":\"warning\"},"
                        + "{\"findingId\":\"spectrometer\",\"category\":\"devices\","
                        + "\"severity\":\"warning\"}");
        OperationsTriagePresentation.ViewModel model =
                OperationsTriagePresentation.withAcknowledgements(
                        raw, (findingId, revision) -> "camera".equals(findingId));

        assertFalse(OperationsAttentionNotificationReconciliation.shouldClear(
                attention(OperationsWatchPolicy.ATTENTION_DEVICES), model));
    }

    @Test
    public void reviewedSignedProblemUsesTheSamePerCategoryRule() throws Exception {
        JSONObject monitor = new JSONObject()
                .put("devices", new JSONObject()
                        .put("available", true)
                        .put("hasConfiguredDevices", true)
                        .put("readyCount", 1)
                        .put("attentionCount", 1)
                        .put("totalCount", 2)
                        .put("offlineCount", 1))
                .put("alerts", new JSONObject()
                        .put("warningCount", 0)
                        .put("errorCount", 1)
                        .put("criticalCount", 0));
        OperationsRemoteProblemsPresentation.ViewModel raw =
                OperationsRemoteProblemsPresentation.from(monitor, true);
        OperationsRemoteProblemsPresentation.ViewModel model =
                OperationsRemoteProblemsPresentation.withAcknowledgements(
                        raw, (findingId, revision) -> "relay-devices".equals(findingId));

        assertTrue(OperationsAttentionNotificationReconciliation.shouldClear(
                attention(OperationsWatchPolicy.ATTENTION_DEVICES), model));
        assertFalse(OperationsAttentionNotificationReconciliation.shouldClear(
                attention(OperationsWatchPolicy.ATTENTION_ERRORS), model));
    }

    @Test
    public void unavailableSignedSnapshotNeverClaimsTheReminderWasConsumed() {
        OperationsRemoteProblemsPresentation.ViewModel model =
                OperationsRemoteProblemsPresentation.from(null, false);

        assertFalse(OperationsAttentionNotificationReconciliation.shouldClear(
                attention(OperationsWatchPolicy.ATTENTION_DEVICES), model));
        assertFalse(OperationsAttentionNotificationReconciliation.shouldClear(
                attention(OperationsWatchPolicy.ATTENTION_DEVICES),
                (OperationsRemoteProblemsPresentation.ViewModel) null));
    }

    private static OperationsTriagePresentation.ViewModel directProblems(String findings)
            throws Exception {
        return OperationsTriagePresentation.from(
                new JSONObject("{\"state\":\"attention\",\"findings\":["
                        + findings + "]}"), value -> value);
    }

    private static String attention(String attentionKey) {
        return OperationsWatchHistory.attentionState(attentionKey);
    }
}

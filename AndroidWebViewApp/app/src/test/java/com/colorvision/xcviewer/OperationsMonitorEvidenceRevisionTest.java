package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsMonitorEvidenceRevisionTest {
    @Test
    public void alertEvidenceTracksCountsAndLatestEventButNotPollingTime() throws Exception {
        JSONObject monitor = monitor();
        OperationsMonitorEvidenceRevision.Evidence first =
                OperationsMonitorEvidenceRevision.capture(
                monitor, OperationsWatchPolicy.ATTENTION_CRITICAL);
        monitor.put("capturedAt", "2026-08-19T02:00:00Z");
        assertEquals(first.revision, OperationsMonitorEvidenceRevision.capture(
                monitor, OperationsWatchPolicy.ATTENTION_CRITICAL).revision);

        monitor.getJSONObject("alerts").put("latestOccurredAt", "2026-08-19T02:00:00Z");
        OperationsMonitorEvidenceRevision.Evidence latestChanged =
                OperationsMonitorEvidenceRevision.capture(
                monitor, OperationsWatchPolicy.ATTENTION_CRITICAL);
        assertFalse(first.revision.equals(latestChanged.revision));
        assertTrue(latestChanged.sequence > first.sequence);
        monitor.getJSONObject("alerts").put("warningCount", 4);
        OperationsMonitorEvidenceRevision.Evidence countChanged =
                OperationsMonitorEvidenceRevision.capture(
                        monitor, OperationsWatchPolicy.ATTENTION_CRITICAL);
        assertFalse(latestChanged.revision.equals(countChanged.revision));
        assertTrue(countChanged.burden > latestChanged.burden);
        assertTrue(first.revision.matches("[0-9a-f]{64}"));
    }

    @Test
    public void liveStateEvidenceIgnoresObservationTimeButTracksMaterialCounts()
            throws Exception {
        JSONObject monitor = monitor();
        OperationsMonitorEvidenceRevision.Evidence devices =
                OperationsMonitorEvidenceRevision.capture(
                monitor, OperationsWatchPolicy.ATTENTION_DEVICES);
        monitor.getJSONObject("devices").put("observedAt", "2026-08-19T02:00:00Z");
        assertEquals(devices.revision, OperationsMonitorEvidenceRevision.capture(
                monitor, OperationsWatchPolicy.ATTENTION_DEVICES).revision);
        monitor.getJSONObject("devices").put("offlineCount", 3);
        assertFalse(devices.revision.equals(OperationsMonitorEvidenceRevision.capture(
                monitor, OperationsWatchPolicy.ATTENTION_DEVICES).revision));

        OperationsMonitorEvidenceRevision.Evidence message =
                OperationsMonitorEvidenceRevision.capture(
                monitor, OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL);
        monitor.getJSONObject("messageChannel")
                .put("observedAt", "2026-08-19T02:01:00Z");
        assertEquals(message.revision, OperationsMonitorEvidenceRevision.capture(
                monitor, OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL).revision);
        monitor.getJSONObject("messageChannel").put("activeSubscriptionCount", 2);
        OperationsMonitorEvidenceRevision.Evidence recovered =
                OperationsMonitorEvidenceRevision.capture(
                        monitor, OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL);
        assertFalse(message.revision.equals(recovered.revision));
        assertTrue(recovered.burden < message.burden);
    }

    @Test
    public void uiTimeoutDoesNotRepeatForLatencySamplingAlone() throws Exception {
        JSONObject monitor = monitor();
        OperationsMonitorEvidenceRevision.Evidence first =
                OperationsMonitorEvidenceRevision.capture(
                monitor, OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE);
        monitor.getJSONObject("performance").getJSONObject("mainUi")
                .put("latencyMilliseconds", 9_999);
        assertEquals(first.revision, OperationsMonitorEvidenceRevision.capture(
                monitor, OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE).revision);
        assertFalse(OperationsMonitorEvidenceRevision.capture(
                monitor, OperationsWatchPolicy.ATTENTION_OFFLINE).available());
    }

    private static JSONObject monitor() throws Exception {
        return new JSONObject()
                .put("capturedAt", "2026-08-19T01:00:00Z")
                .put("performance", new JSONObject()
                        .put("mainUi", new JSONObject()
                                .put("state", "unresponsive")
                                .put("latencyMilliseconds", 5_000)))
                .put("alerts", new JSONObject()
                        .put("count", 6)
                        .put("warningCount", 3)
                        .put("errorCount", 2)
                        .put("criticalCount", 1)
                        .put("primarySource", "设备与图像")
                        .put("latestOccurredAt", "2026-08-19T01:00:00Z"))
                .put("devices", new JSONObject()
                        .put("available", true)
                        .put("totalCount", 5)
                        .put("readyCount", 3)
                        .put("attentionCount", 2)
                        .put("unavailableCount", 2)
                        .put("offlineCount", 2)
                        .put("observedAt", "2026-08-19T01:00:00Z"))
                .put("messageChannel", new JSONObject()
                        .put("available", true)
                        .put("state", "degraded")
                        .put("connected", true)
                        .put("subscriptionReady", false)
                        .put("registeredSubscriptionCount", 4)
                        .put("activeSubscriptionCount", 1)
                        .put("attentionRequired", true)
                        .put("observedAt", "2026-08-19T01:00:00Z"));
    }
}

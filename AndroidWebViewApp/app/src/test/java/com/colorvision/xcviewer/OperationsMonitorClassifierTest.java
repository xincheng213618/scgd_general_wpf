package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class OperationsMonitorClassifierTest {
    @Test
    public void healthyMonitorKeepsTheRequestedBoundedOnlineChannel() throws Exception {
        JSONObject monitor = monitor("responsive", 0, 0, 0, false);

        assertEquals(OperationsWatchHistory.STATE_ONLINE,
                OperationsMonitorClassifier.watchState(
                        monitor, OperationsWatchHistory.STATE_ONLINE));
        assertEquals(OperationsWatchHistory.STATE_REMOTE_ONLINE,
                OperationsMonitorClassifier.watchState(
                        monitor, OperationsWatchHistory.STATE_REMOTE_ONLINE));
        assertEquals(OperationsWatchHistory.STATE_OFFLINE,
                OperationsMonitorClassifier.watchState(monitor, "arbitrary"));
    }

    @Test
    public void fixedAttentionPriorityIsSharedByBackgroundAndFleetChecks() throws Exception {
        JSONObject monitor = monitor("unresponsive", 3, 2, 4, true);

        assertEquals(OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE,
                OperationsMonitorClassifier.attentionKey(monitor));
        assertEquals(OperationsWatchHistory.attentionState(
                        OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE),
                OperationsMonitorClassifier.watchState(
                        monitor, OperationsWatchHistory.STATE_REMOTE_ONLINE));
    }

    @Test
    public void missingMonitorUsesBoundedUnavailableSemantics() {
        assertEquals("", OperationsMonitorClassifier.attentionKey(null));
        assertEquals(OperationsWatchHistory.STATE_OFFLINE,
                OperationsMonitorClassifier.watchState(
                        null, OperationsWatchHistory.STATE_ONLINE));
    }

    private static JSONObject monitor(
            String uiState,
            int critical,
            int errors,
            int deviceAttention,
            boolean messageAttention) throws Exception {
        return new JSONObject()
                .put("flow", new JSONObject().put("isActive", false))
                .put("performance", new JSONObject()
                        .put("mainUi", new JSONObject().put("state", uiState)))
                .put("alerts", new JSONObject()
                        .put("criticalCount", critical)
                        .put("errorCount", errors))
                .put("devices", new JSONObject()
                        .put("available", true)
                        .put("attentionCount", deviceAttention))
                .put("messageChannel", new JSONObject()
                        .put("available", true)
                        .put("attentionRequired", messageAttention));
    }
}

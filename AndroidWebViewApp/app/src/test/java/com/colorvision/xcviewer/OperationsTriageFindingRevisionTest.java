package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsTriageFindingRevisionTest {
    @Test
    public void liveStateRevisionIgnoresPollingTimeButTracksMaterialChanges() {
        String first = revision("devices", "设备离线", "离线 2 台", 2, "2026-08-19T01:00:00Z");
        String laterPoll = revision(
                "devices", "设备离线", "离线 2 台", 2, "2026-08-19T01:01:00Z");
        String changed = revision(
                "devices", "设备离线", "离线 3 台", 3, "2026-08-19T01:01:00Z");

        assertEquals(first, laterPoll);
        assertFalse(first.equals(changed));
        assertTrue(first.matches("[0-9a-f]{64}"));
    }

    @Test
    public void eventEvidenceRevisionChangesWhenNewEvidenceArrives() {
        String first = OperationsTriageFindingRevision.revision(
                "recent-warnings",
                "warning",
                "diagnostics",
                "近期警告",
                "警告 11 条",
                11,
                "2026-08-19T01:00:00Z");
        String later = OperationsTriageFindingRevision.revision(
                "recent-warnings",
                "warning",
                "diagnostics",
                "近期警告",
                "警告 11 条",
                11,
                "2026-08-19T01:05:00Z");

        assertFalse(first.equals(later));
    }

    @Test
    public void reportedIdIsPreferredAndLegacyFallbackIsDeterministic() {
        assertEquals("fixed-finding",
                OperationsTriageFindingRevision.findingId(
                        "fixed-finding", "devices", "设备离线"));
        String first = OperationsTriageFindingRevision.findingId("", "devices", "设备离线");
        String second = OperationsTriageFindingRevision.findingId(null, "devices", "设备离线");
        assertEquals(first, second);
        assertTrue(first.startsWith("legacy-"));
    }

    private static String revision(
            String category, String title, String summary, int count, String latestAt) {
        return OperationsTriageFindingRevision.revision(
                "fixed-finding", "warning", category, title, summary, count, latestAt);
    }
}

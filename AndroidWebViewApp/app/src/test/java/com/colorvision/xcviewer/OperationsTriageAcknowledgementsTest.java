package com.colorvision.xcviewer;

import org.junit.Test;

import java.util.Collections;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.Map;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsTriageAcknowledgementsTest {
    private static final long NOW = 2_000_000_000_000L;
    private static final String REVISION_A = "a".repeat(64);
    private static final String REVISION_B = "b".repeat(64);

    @Test
    public void acknowledgementIsScopedToComputerFindingAndExactEvidenceRevision() {
        String state = OperationsTriageAcknowledgements.acknowledge(
                "", "host-a", "devices", REVISION_A, NOW);

        assertTrue(OperationsTriageAcknowledgements.contains(
                state, "host-a", "devices", REVISION_A, NOW));
        assertFalse(OperationsTriageAcknowledgements.contains(
                state, "host-b", "devices", REVISION_A, NOW));
        assertFalse(OperationsTriageAcknowledgements.contains(
                state, "host-a", "devices", REVISION_B, NOW));
    }

    @Test
    public void newEvidenceAndDisappearanceClearTheOldReview() {
        String state = OperationsTriageAcknowledgements.acknowledge(
                "", "host-a", "devices", REVISION_A, NOW);
        Map<String, String> changed = new HashMap<>();
        changed.put("devices", REVISION_B);

        state = OperationsTriageAcknowledgements.reconcile(
                state, "host-a", changed, NOW + 1L);
        assertFalse(OperationsTriageAcknowledgements.contains(
                state, "host-a", "devices", REVISION_A, NOW + 1L));

        state = OperationsTriageAcknowledgements.acknowledge(
                state, "host-a", "devices", REVISION_B, NOW + 2L);
        state = OperationsTriageAcknowledgements.reconcile(
                state, "host-a", Collections.emptyMap(), NOW + 3L);
        assertFalse(OperationsTriageAcknowledgements.contains(
                state, "host-a", "devices", REVISION_B, NOW + 3L));
    }

    @Test
    public void reconcileAndRemovalNeverAffectAnotherComputer() {
        String state = OperationsTriageAcknowledgements.acknowledge(
                "", "host-a", "devices", REVISION_A, NOW);
        state = OperationsTriageAcknowledgements.acknowledge(
                state, "host-b", "devices", REVISION_B, NOW + 1L);
        state = OperationsTriageAcknowledgements.reconcile(
                state, "host-a", Collections.emptyMap(), NOW + 2L);

        assertTrue(OperationsTriageAcknowledgements.contains(
                state, "host-b", "devices", REVISION_B, NOW + 2L));
        state = OperationsTriageAcknowledgements.removeHost(
                state, "host-b", NOW + 3L);
        assertFalse(OperationsTriageAcknowledgements.contains(
                state, "host-b", "devices", REVISION_B, NOW + 3L));
    }

    @Test
    public void batchReviewAndUndoStayScopedToTheSelectedComputer() {
        String state = OperationsTriageAcknowledgements.acknowledge(
                "", "host-b", "devices", REVISION_B, NOW);
        Map<String, String> pending = new LinkedHashMap<>();
        pending.put("devices", REVISION_A);
        pending.put("messages", REVISION_B);

        state = OperationsTriageAcknowledgements.updateAll(
                state, "host-a", pending, true, NOW + 1L);
        assertTrue(OperationsTriageAcknowledgements.contains(
                state, "host-a", "devices", REVISION_A, NOW + 1L));
        assertTrue(OperationsTriageAcknowledgements.contains(
                state, "host-a", "messages", REVISION_B, NOW + 1L));
        assertTrue(OperationsTriageAcknowledgements.contains(
                state, "host-b", "devices", REVISION_B, NOW + 1L));

        state = OperationsTriageAcknowledgements.updateAll(
                state, "host-a", pending, false, NOW + 2L);
        assertFalse(OperationsTriageAcknowledgements.contains(
                state, "host-a", "devices", REVISION_A, NOW + 2L));
        assertFalse(OperationsTriageAcknowledgements.contains(
                state, "host-a", "messages", REVISION_B, NOW + 2L));
        assertTrue(OperationsTriageAcknowledgements.contains(
                state, "host-b", "devices", REVISION_B, NOW + 2L));
    }

    @Test
    public void expiredAndMalformedRecordsAreIgnored() {
        String expired = OperationsTriageAcknowledgements.acknowledge(
                "",
                "host-a",
                "devices",
                REVISION_A,
                NOW - OperationsTriageAcknowledgements.RETENTION_MILLISECONDS - 1L);
        assertFalse(OperationsTriageAcknowledgements.contains(
                expired, "host-a", "devices", REVISION_A, NOW));
        assertFalse(OperationsTriageAcknowledgements.contains(
                "not-json", "host-a", "devices", REVISION_A, NOW));
    }
}

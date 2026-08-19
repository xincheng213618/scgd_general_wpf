package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsWatchEvidenceMemoryTest {
    private static final String REVISION_1 = "a".repeat(64);
    private static final String REVISION_2 = "b".repeat(64);
    private static final OperationsMonitorEvidenceRevision.Evidence EVIDENCE_1 =
            new OperationsMonitorEvidenceRevision.Evidence(REVISION_1, 100L, 10L);
    private static final OperationsMonitorEvidenceRevision.Evidence EVIDENCE_2 =
            new OperationsMonitorEvidenceRevision.Evidence(REVISION_2, 200L, 20L);

    @Test
    public void revisionsStayIsolatedByComputerAndAttentionCategory() {
        String memory = OperationsWatchEvidenceMemory.update(
                "", "host_1", OperationsWatchPolicy.ATTENTION_DEVICES, EVIDENCE_1);
        memory = OperationsWatchEvidenceMemory.update(
                memory, "host_2", OperationsWatchPolicy.ATTENTION_ERRORS, EVIDENCE_2);

        assertEquals(REVISION_1, OperationsWatchEvidenceMemory.evidence(
                memory, "host_1", OperationsWatchPolicy.ATTENTION_DEVICES).revision);
        assertFalse(OperationsWatchEvidenceMemory.evidence(
                memory, "host_1", OperationsWatchPolicy.ATTENTION_ERRORS).available());
        OperationsMonitorEvidenceRevision.Evidence restored =
                OperationsWatchEvidenceMemory.evidence(
                        memory, "host_2", OperationsWatchPolicy.ATTENTION_ERRORS);
        assertEquals(REVISION_2, restored.revision);
        assertEquals(200L, restored.sequence);
        assertEquals(20L, restored.burden);
    }

    @Test
    public void categoryChangeReplacesTheOldRevisionAndHealthyRemovesIt() {
        String memory = OperationsWatchEvidenceMemory.update(
                "", "host_1", OperationsWatchPolicy.ATTENTION_DEVICES, EVIDENCE_1);
        memory = OperationsWatchEvidenceMemory.update(
                memory, "host_1", OperationsWatchPolicy.ATTENTION_CRITICAL, EVIDENCE_2);
        assertFalse(OperationsWatchEvidenceMemory.evidence(
                memory, "host_1", OperationsWatchPolicy.ATTENTION_DEVICES).available());
        assertEquals(REVISION_2, OperationsWatchEvidenceMemory.evidence(
                memory, "host_1", OperationsWatchPolicy.ATTENTION_CRITICAL).revision);

        assertEquals("", OperationsWatchEvidenceMemory.removeHost(memory, "host_1"));
    }

    @Test
    public void manualCheckBaselineSuppressesTheSameEvidenceButNotLaterGrowth() {
        String memory = OperationsWatchEvidenceMemory.update(
                "", "host_1", OperationsWatchPolicy.ATTENTION_ERRORS, EVIDENCE_1);
        OperationsMonitorEvidenceRevision.Evidence baseline =
                OperationsWatchEvidenceMemory.evidence(
                        memory, "host_1", OperationsWatchPolicy.ATTENTION_ERRORS);

        assertFalse(OperationsWatchPolicy.shouldPostAttention(
                OperationsWatchPolicy.ATTENTION_ERRORS,
                OperationsWatchPolicy.ATTENTION_ERRORS,
                EVIDENCE_1,
                baseline));
        assertTrue(OperationsWatchPolicy.shouldPostAttention(
                OperationsWatchPolicy.ATTENTION_ERRORS,
                OperationsWatchPolicy.ATTENTION_ERRORS,
                EVIDENCE_2,
                baseline));
    }

    @Test
    public void malformedOrUntrustedValuesAreDiscarded() {
        assertFalse(OperationsWatchEvidenceMemory.evidence(
                "not-json", "host_1", OperationsWatchPolicy.ATTENTION_DEVICES).available());
        assertEquals("", OperationsWatchEvidenceMemory.update(
                "", "../../host", OperationsWatchPolicy.ATTENTION_DEVICES, EVIDENCE_1));
        assertEquals("", OperationsWatchEvidenceMemory.update(
                "", "host_1", "arbitrary", EVIDENCE_1));
        assertEquals("", OperationsWatchEvidenceMemory.update(
                "",
                "host_1",
                OperationsWatchPolicy.ATTENTION_DEVICES,
                new OperationsMonitorEvidenceRevision.Evidence("bad", 1L, 1L)));
    }

    @Test
    public void storageRemainsBoundedToTheProfileLimit() {
        String memory = "";
        for (int index = 0; index < OperationsProfileRegistry.MAX_PROFILES + 2; index++) {
            memory = OperationsWatchEvidenceMemory.update(
                    memory,
                    "host_" + index,
                    OperationsWatchPolicy.ATTENTION_DEVICES,
                    new OperationsMonitorEvidenceRevision.Evidence(
                            String.format("%064x", index + 1), index + 1L, index + 1L));
        }
        assertFalse(OperationsWatchEvidenceMemory.evidence(
                memory, "host_0", OperationsWatchPolicy.ATTENTION_DEVICES).available());
        assertTrue(memory.length() < 4_096);
    }
}

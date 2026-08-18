package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsFleetOverviewTest {
    private static final long NOW = 2_000_000_000_000L;
    private static final String PIN = "a".repeat(64);

    @Test
    public void fleetSummarySeparatesRecentOperationalStatesFromStaleRecords() {
        OperationsProfileRegistry.State state = profiles(6)
                .rename("host_1", "一号线");
        state = withState(state, "host_1", OperationsWatchHistory.attentionState(
                OperationsWatchPolicy.ATTENTION_CRITICAL), NOW - 30_000L);
        state = withState(state, "host_2", OperationsWatchHistory.STATE_ONLINE,
                NOW - 60_000L);
        state = withState(state, "host_3", OperationsWatchHistory.STATE_REMOTE_WAITING,
                NOW - 90_000L);
        state = withState(state, "host_4", OperationsWatchHistory.STATE_OFFLINE,
                NOW - 120_000L);
        state = withState(state, "host_5", OperationsWatchHistory.STATE_ONLINE,
                NOW - OperationsFleetOverview.RECENT_CHECK_MILLISECONDS - 1L);
        state = state.revoke("host_6");

        OperationsFleetOverview.Assessment result = OperationsFleetOverview.assess(
                state.profiles, NOW);

        assertEquals("需关注 1 · 暂不可达 2 · 稳定 1 · 待巡检 1 · 授权失效 1",
                result.summary);
        assertEquals("host_1", result.priorityHostId);
        assertEquals(OperationsFleetOverview.ACTION_OPEN, result.priorityAction);
        assertEquals("处理首要电脑 · 一号线", result.priorityButtonLabel);
    }

    @Test
    public void fixedAttentionPriorityWinsRegardlessOfProfileOrder() {
        OperationsProfileRegistry.State state = profiles(5);
        state = withState(state, "host_1", OperationsWatchHistory.attentionState(
                OperationsWatchPolicy.ATTENTION_ERRORS), NOW);
        assertEquals("host_1", OperationsFleetOverview.assess(
                state.profiles, NOW).priorityHostId);
        state = withState(state, "host_2", OperationsWatchHistory.attentionState(
                OperationsWatchPolicy.ATTENTION_DEVICES), NOW);
        assertEquals("host_2", OperationsFleetOverview.assess(
                state.profiles, NOW).priorityHostId);
        state = withState(state, "host_3", OperationsWatchHistory.attentionState(
                OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL), NOW);
        assertEquals("host_3", OperationsFleetOverview.assess(
                state.profiles, NOW).priorityHostId);
        state = withState(state, "host_4", OperationsWatchHistory.attentionState(
                OperationsWatchPolicy.ATTENTION_CRITICAL), NOW);
        assertEquals("host_4", OperationsFleetOverview.assess(
                state.profiles, NOW).priorityHostId);
        state = withState(state, "host_5", OperationsWatchHistory.attentionState(
                OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE), NOW);

        OperationsFleetOverview.Assessment result = OperationsFleetOverview.assess(
                state.profiles, NOW);

        assertEquals("host_5", result.priorityHostId);
        assertEquals("处理首要电脑 · 电脑 5", result.priorityButtonLabel);
    }

    @Test
    public void connectionLossWinsOverWaitingAndRevokedCleanup() {
        OperationsProfileRegistry.State state = profiles(3);
        state = withState(state, "host_1", OperationsWatchHistory.STATE_REMOTE_WAITING, NOW);
        state = withState(state, "host_2", OperationsWatchHistory.STATE_OFFLINE, NOW);
        state = state.revoke("host_3");

        OperationsFleetOverview.Assessment result = OperationsFleetOverview.assess(
                state.profiles, NOW);

        assertEquals("host_2", result.priorityHostId);
        assertEquals("检查首要电脑 · 电脑 2", result.priorityButtonLabel);
        assertEquals(OperationsFleetOverview.ACTION_OPEN, result.priorityAction);
    }

    @Test
    public void revokedProfileGetsAnExplicitLocalCleanupActionWhenNothingElseIsWrong() {
        OperationsProfileRegistry.State state = profiles(2);
        state = withState(state, "host_1", OperationsWatchHistory.STATE_ONLINE, NOW);
        state = state.revoke("host_2");

        OperationsFleetOverview.Assessment result = OperationsFleetOverview.assess(
                state.profiles, NOW);

        assertEquals("稳定 1 · 授权失效 1", result.summary);
        assertEquals("host_2", result.priorityHostId);
        assertEquals(OperationsFleetOverview.ACTION_REMOVE, result.priorityAction);
        assertEquals("移除失效配对 · 电脑 2", result.priorityButtonLabel);
        assertTrue(result.hasPriorityAction());
    }

    @Test
    public void healthyOrUntrustedRecordsNeverCreateAFakePriority() {
        OperationsProfileRegistry.State healthy = withState(
                profiles(1), "host_1", OperationsWatchHistory.STATE_REMOTE_ONLINE, NOW);
        OperationsFleetOverview.Assessment healthyResult = OperationsFleetOverview.assess(
                healthy.profiles, NOW);
        assertEquals("稳定 1", healthyResult.summary);
        assertFalse(healthyResult.hasPriorityAction());

        OperationsProfileRegistry.State untrusted = profiles(3)
                .updateWatchHistory("host_1", "corrupt", NOW)
                .updateWatchHistory("host_2", history(OperationsWatchHistory.STATE_OFFLINE, NOW),
                        NOW + 2L * 60L * 1000L);
        OperationsFleetOverview.Assessment untrustedResult = OperationsFleetOverview.assess(
                untrusted.profiles, NOW);
        assertEquals("待巡检 3", untrustedResult.summary);
        assertFalse(untrustedResult.hasPriorityAction());
    }

    private static OperationsProfileRegistry.State profiles(int count) {
        OperationsProfileRegistry.State state = OperationsProfileRegistry.empty();
        for (int index = 1; index <= count; index++) {
            state = state.upsert("https://192.168.1." + (20 + index) + ":5800",
                    PIN, "host_" + index);
        }
        return state;
    }

    private static OperationsProfileRegistry.State withState(
            OperationsProfileRegistry.State state,
            String hostId,
            String watchState,
            long checkedAt) {
        return state.updateWatchHistory(hostId, history(watchState, checkedAt), checkedAt);
    }

    private static String history(String watchState, long timestamp) {
        return OperationsWatchHistory.transition("", watchState, timestamp).serializedHistory;
    }
}

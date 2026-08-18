package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsFleetTimelineTest {
    private static final long NOW = 2_000_000_000_000L;
    private static final String PIN = "a".repeat(64);

    @Test
    public void entriesFromEveryComputerAreMergedNewestFirst() {
        OperationsProfileRegistry.State state = profiles(2)
                .rename("host_1", "一号线")
                .rename("host_2", "一号线");
        String firstHistory = append("", OperationsWatchHistory.STATE_ONLINE, NOW - 180_000L);
        firstHistory = append(firstHistory, OperationsWatchHistory.STATE_OFFLINE, NOW - 60_000L);
        state = state.updateWatchHistory("host_1", firstHistory, NOW - 60_000L)
                .updateWatchHistory("host_2", append("",
                        OperationsWatchHistory.STATE_REMOTE_WAITING, NOW - 120_000L),
                        NOW - 120_000L);

        OperationsFleetTimeline.Timeline timeline = OperationsFleetTimeline.build(
                state.profiles, "host_2", NOW, false);

        assertEquals("近 7 天 · 2 台电脑 · 3 条变化", timeline.summary);
        assertEquals(3, timeline.entries.size());
        assertEquals(OperationsWatchHistory.STATE_OFFLINE, timeline.entries.get(0).state);
        assertEquals("一号线（电脑 1）", timeline.entries.get(0).profileLabel);
        assertEquals(OperationsWatchHistory.STATE_REMOTE_WAITING,
                timeline.entries.get(1).state);
        assertEquals("一号线（电脑 2）（当前）", timeline.entries.get(1).profileLabel);
        assertEquals(OperationsWatchHistory.STATE_ONLINE, timeline.entries.get(2).state);
    }

    @Test
    public void issueFilterKeepsOnlyFixedAttentionAndUnavailableTransitions() {
        OperationsProfileRegistry.State state = profiles(2);
        String firstHistory = append("", OperationsWatchHistory.STATE_ONLINE, NOW - 500L);
        firstHistory = append(firstHistory, OperationsWatchHistory.attentionState(
                OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL), NOW - 400L);
        firstHistory = append(firstHistory, OperationsWatchHistory.STATE_OFFLINE, NOW - 300L);
        String secondHistory = append("", OperationsWatchHistory.STATE_REMOTE_ONLINE, NOW - 200L);
        secondHistory = append(secondHistory,
                OperationsWatchHistory.STATE_REMOTE_WAITING, NOW - 100L);
        state = state.updateWatchHistory("host_1", firstHistory, NOW)
                .updateWatchHistory("host_2", secondHistory, NOW);

        OperationsFleetTimeline.Timeline issues = OperationsFleetTimeline.build(
                state.profiles, "host_2", NOW, true);

        assertEquals("近 7 天 · 2 台电脑 · 3 条需关注变化", issues.summary);
        assertEquals(5, issues.totalEntryCount);
        assertEquals(3, issues.issueEntryCount);
        assertEquals(3, issues.matchingEntryCount);
        assertEquals(3, issues.entries.size());
        assertTrue(issues.entries.stream().allMatch(entry -> entry.issue));
    }

    @Test
    public void visibleTimelineIsBoundedWhileSummaryKeepsTheRealCount() {
        OperationsProfileRegistry.State state = profiles(2)
                .updateWatchHistory("host_1", alternatingHistory(40, NOW - 1_000L), NOW)
                .updateWatchHistory("host_2", alternatingHistory(40, NOW - 2_000L), NOW);

        OperationsFleetTimeline.Timeline timeline = OperationsFleetTimeline.build(
                state.profiles, "host_1", NOW, false);

        assertEquals(80, timeline.totalEntryCount);
        assertEquals(80, timeline.matchingEntryCount);
        assertEquals(OperationsFleetTimeline.MAX_VISIBLE_ENTRIES, timeline.entries.size());
        assertEquals("近 7 天 · 2 台电脑 · 80 条变化", timeline.summary);
        assertTrue(timeline.truncated());
    }

    @Test
    public void corruptExpiredAndImplausiblyFutureEntriesAreExcluded() {
        long expired = NOW - OperationsWatchHistory.RETENTION_MILLISECONDS - 1L;
        String unsafeHistory = expired + "|online\n"
                + (NOW + 120_000L) + "|offline\n"
                + "corrupt\n"
                + NOW + "|remote-online";
        OperationsProfileRegistry.State state = profiles(2)
                .updateWatchHistory("host_1", unsafeHistory, NOW);

        OperationsFleetTimeline.Timeline timeline = OperationsFleetTimeline.build(
                state.profiles, "host_1", NOW, false);
        OperationsFleetTimeline.Timeline issues = OperationsFleetTimeline.build(
                state.profiles, "host_1", NOW, true);

        assertEquals(1, timeline.entries.size());
        assertEquals(OperationsWatchHistory.STATE_REMOTE_ONLINE, timeline.entries.get(0).state);
        assertEquals("近 7 天 · 1 台电脑 · 1 条变化", timeline.summary);
        assertEquals("近 7 天 · 暂无需关注变化", issues.summary);
        assertFalse(issues.truncated());
    }

    @Test
    public void emptyFleetHasAnExplicitTruthfulSummary() {
        OperationsFleetTimeline.Timeline all = OperationsFleetTimeline.build(
                null, "", NOW, false);
        OperationsFleetTimeline.Timeline issues = OperationsFleetTimeline.build(
                null, "", NOW, true);

        assertEquals("近 7 天 · 暂无状态变化", all.summary);
        assertEquals("近 7 天 · 暂无需关注变化", issues.summary);
        assertTrue(all.entries.isEmpty());
        assertTrue(issues.entries.isEmpty());
    }

    private static OperationsProfileRegistry.State profiles(int count) {
        OperationsProfileRegistry.State state = OperationsProfileRegistry.empty();
        for (int index = 1; index <= count; index++) {
            state = state.upsert("https://192.168.1." + (20 + index) + ":5800",
                    PIN, "host_" + index);
        }
        return state;
    }

    private static String alternatingHistory(int count, long firstTimestamp) {
        String history = "";
        for (int index = 0; index < count; index++) {
            String state = index % 2 == 0
                    ? OperationsWatchHistory.STATE_ONLINE
                    : OperationsWatchHistory.STATE_OFFLINE;
            history = append(history, state, firstTimestamp + index);
        }
        return history;
    }

    private static String append(String history, String state, long timestamp) {
        return OperationsWatchHistory.transition(history, state, timestamp).serializedHistory;
    }
}

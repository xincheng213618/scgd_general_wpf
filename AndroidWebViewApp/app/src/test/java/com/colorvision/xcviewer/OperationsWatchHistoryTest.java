package com.colorvision.xcviewer;

import org.junit.Test;

import java.util.List;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsWatchHistoryTest {
    private static final long NOW = 2_000_000_000_000L;

    @Test
    public void transitionsRecordOnlyFixedStateChanges() {
        OperationsWatchHistory.Transition online = OperationsWatchHistory.transition(
                "", OperationsWatchHistory.STATE_ONLINE, NOW);
        assertTrue(online.changed);
        assertEquals(OperationsWatchHistory.STATE_ONLINE, online.currentState);

        OperationsWatchHistory.Transition duplicate = OperationsWatchHistory.transition(
                online.serializedHistory, OperationsWatchHistory.STATE_ONLINE, NOW + 1_000L);
        assertFalse(duplicate.changed);
        assertEquals(online.serializedHistory, duplicate.serializedHistory);

        String attentionState = OperationsWatchHistory.attentionState(
                OperationsWatchPolicy.ATTENTION_DEVICES);
        OperationsWatchHistory.Transition attention = OperationsWatchHistory.transition(
                duplicate.serializedHistory, attentionState, NOW + 2_000L);
        assertTrue(attention.changed);
        assertEquals(OperationsWatchPolicy.ATTENTION_DEVICES,
                OperationsWatchHistory.attentionKey(attention.currentState));
        assertTrue(OperationsWatchHistory.isOnlineState(attention.currentState));
        assertEquals(2, OperationsWatchHistory.parse(
                attention.serializedHistory, NOW + 2_000L).size());

        OperationsWatchHistory.Transition rejected = OperationsWatchHistory.transition(
                attention.serializedHistory, "attention:arbitrary", NOW + 3_000L);
        assertFalse(rejected.changed);
        assertEquals(attention.currentState, rejected.currentState);
    }

    @Test
    public void historyDropsExpiredCorruptAndExcessEntries() {
        String history = (NOW - OperationsWatchHistory.RETENTION_MILLISECONDS - 1L)
                + "|online\ncorrupt\n";
        for (int index = 0; index < OperationsWatchHistory.MAX_ENTRIES + 5; index++) {
            String nextState = index % 2 == 0
                    ? OperationsWatchHistory.STATE_ONLINE
                    : OperationsWatchHistory.STATE_OFFLINE;
            OperationsWatchHistory.Transition transition = OperationsWatchHistory.transition(
                    history, nextState, NOW + index);
            history = transition.serializedHistory;
        }

        List<OperationsWatchHistory.Entry> entries = OperationsWatchHistory.parse(
                history, NOW + OperationsWatchHistory.MAX_ENTRIES + 5L);
        assertEquals(OperationsWatchHistory.MAX_ENTRIES, entries.size());
        assertEquals(NOW + 5L, entries.get(0).timestampMilliseconds);
        assertEquals(OperationsWatchHistory.STATE_ONLINE,
                entries.get(entries.size() - 1).state);
    }

    @Test
    public void labelsContainOnlyBoundedOperationalCategories() {
        assertEquals("连接在线 · 当前状态稳定",
                OperationsWatchHistory.label(OperationsWatchHistory.STATE_ONLINE));
        assertEquals("连接中断 · 后台自动重试",
                OperationsWatchHistory.label(OperationsWatchHistory.STATE_OFFLINE));
        assertEquals("在线 · 消息通道需要关注",
                OperationsWatchHistory.label(OperationsWatchHistory.attentionState(
                        OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL)));
        assertEquals("", OperationsWatchHistory.attentionState("custom"));
        assertFalse(OperationsWatchHistory.isOnlineState("custom"));
    }
}

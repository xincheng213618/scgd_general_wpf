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
                    : OperationsWatchHistory.attentionState(
                            OperationsWatchPolicy.ATTENTION_DEVICES);
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

    @Test
    public void remoteRelayStatesRemainTruthfulAndOnline() {
        assertEquals("远程中继在线 · 电脑已连接",
                OperationsWatchHistory.label(OperationsWatchHistory.STATE_REMOTE_ONLINE));
        assertEquals("远程中继在线 · 等待电脑上线",
                OperationsWatchHistory.label(OperationsWatchHistory.STATE_REMOTE_WAITING));
        assertTrue(OperationsWatchHistory.isOnlineState(
                OperationsWatchHistory.STATE_REMOTE_ONLINE));
        assertTrue(OperationsWatchHistory.isOnlineState(
                OperationsWatchHistory.STATE_REMOTE_WAITING));
    }

    @Test
    public void shortOfflineFlapsAreCompactedWithoutHidingPersistentOutages() {
        OperationsWatchHistory.Transition waiting = OperationsWatchHistory.transition(
                "", OperationsWatchHistory.STATE_REMOTE_WAITING, NOW);
        OperationsWatchHistory.Transition shortOffline = OperationsWatchHistory.transition(
                waiting.serializedHistory, OperationsWatchHistory.STATE_OFFLINE,
                NOW + 30_000L);
        OperationsWatchHistory.Transition recovered = OperationsWatchHistory.transition(
                shortOffline.serializedHistory, OperationsWatchHistory.STATE_REMOTE_WAITING,
                NOW + 90_000L);

        List<OperationsWatchHistory.Entry> compacted = OperationsWatchHistory.parse(
                recovered.serializedHistory, NOW + 90_000L);
        assertEquals(1, compacted.size());
        assertEquals(OperationsWatchHistory.STATE_REMOTE_WAITING, compacted.get(0).state);

        OperationsWatchHistory.Transition persistentOffline = OperationsWatchHistory.transition(
                waiting.serializedHistory, OperationsWatchHistory.STATE_OFFLINE,
                NOW + 30_000L);
        OperationsWatchHistory.Transition lateRecovery = OperationsWatchHistory.transition(
                persistentOffline.serializedHistory, OperationsWatchHistory.STATE_REMOTE_WAITING,
                NOW + 30_000L + OperationsWatchHistory.TRANSIENT_OFFLINE_WINDOW_MILLISECONDS);
        assertEquals(3, OperationsWatchHistory.parse(
                lateRecovery.serializedHistory,
                NOW + 30_000L + OperationsWatchHistory.TRANSIENT_OFFLINE_WINDOW_MILLISECONDS).size());
    }

    @Test
    public void legacyAlternatingRelayHistoryIsCompactedOnRead() {
        String legacyHistory = NOW + "|remote-waiting\n"
                + (NOW + 30_000L) + "|offline\n"
                + (NOW + 60_000L) + "|remote-waiting\n"
                + (NOW + 90_000L) + "|offline\n"
                + (NOW + 120_000L) + "|remote-waiting";

        List<OperationsWatchHistory.Entry> compacted = OperationsWatchHistory.parse(
                legacyHistory, NOW + 120_000L);

        assertEquals(1, compacted.size());
        assertEquals(OperationsWatchHistory.STATE_REMOTE_WAITING, compacted.get(0).state);
        assertEquals(NOW, compacted.get(0).timestampMilliseconds);
    }
}

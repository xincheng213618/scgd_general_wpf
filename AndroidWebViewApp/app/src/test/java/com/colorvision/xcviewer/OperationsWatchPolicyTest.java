package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsWatchPolicyTest {
    @Test
    public void retryDelayUsesBoundedExponentialBackoff() {
        assertEquals(30_000L, OperationsWatchPolicy.retryDelayMilliseconds(0));
        assertEquals(30_000L, OperationsWatchPolicy.retryDelayMilliseconds(1));
        assertEquals(60_000L, OperationsWatchPolicy.retryDelayMilliseconds(2));
        assertEquals(120_000L, OperationsWatchPolicy.retryDelayMilliseconds(3));
        assertEquals(240_000L, OperationsWatchPolicy.retryDelayMilliseconds(4));
        assertEquals(300_000L, OperationsWatchPolicy.retryDelayMilliseconds(5));
        assertEquals(300_000L, OperationsWatchPolicy.retryDelayMilliseconds(50));
    }

    @Test
    public void offlineRequiresRepeatedFailuresAcrossTheConfirmationWindow() {
        long firstFailureAt = 1_000_000L;

        assertFalse(OperationsWatchPolicy.shouldConfirmOffline(
                1, firstFailureAt, firstFailureAt + 120_000L));
        assertFalse(OperationsWatchPolicy.shouldConfirmOffline(
                2, firstFailureAt, firstFailureAt + 120_000L));
        assertFalse(OperationsWatchPolicy.shouldConfirmOffline(
                3, firstFailureAt, firstFailureAt + 59_999L));
        assertTrue(OperationsWatchPolicy.shouldConfirmOffline(
                3, firstFailureAt, firstFailureAt + 60_000L));
        assertFalse(OperationsWatchPolicy.shouldConfirmOffline(
                3, firstFailureAt, firstFailureAt - 1L));
    }

    @Test
    public void healthyStatusPrioritizesActionableEvidence() {
        assertEquals("在线 · 主界面响应超时",
                OperationsWatchPolicy.healthyStatus("unresponsive", true, 2, 3, 4, true));
        assertEquals("在线 · 发现严重告警",
                OperationsWatchPolicy.healthyStatus("ready", true, 2, 3, 4, true));
        assertEquals("在线 · 消息通道需要关注",
                OperationsWatchPolicy.healthyStatus("ready", true, 0, 3, 4, true));
        assertEquals("在线 · 检测设备需要关注",
                OperationsWatchPolicy.healthyStatus("ready", true, 0, 3, 4, false));
        assertEquals("在线 · 发现错误事件",
                OperationsWatchPolicy.healthyStatus("ready", true, 0, 3, 0, false));
        assertEquals("在线 · 主界面响应偏慢",
                OperationsWatchPolicy.healthyStatus("slow", true, 0, 0, 0, false));
        assertEquals("在线 · 检测正在进行",
                OperationsWatchPolicy.healthyStatus("ready", true, 0, 0, 0, false));
        assertEquals("在线 · 当前状态稳定",
                OperationsWatchPolicy.healthyStatus("ready", false, 0, 0, 0, false));
    }

    @Test
    public void successfulCheckDistinguishesInitialConnectionFromRecovery() {
        assertEquals("在线 · 当前状态稳定 · 刚刚检查",
                OperationsWatchPolicy.successfulCheckNotification(
                        "在线 · 当前状态稳定", false));
        assertEquals("连接已恢复 · 在线 · 当前状态稳定 · 刚刚检查",
                OperationsWatchPolicy.successfulCheckNotification(
                        "在线 · 当前状态稳定", true));
    }

    @Test
    public void attentionPolicyAlertsOnlyWhenTheActionableStateChanges() {
        assertEquals(OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE,
                OperationsWatchPolicy.attentionKey("unresponsive", 2, 3, 4, true));
        assertEquals(OperationsWatchPolicy.ATTENTION_CRITICAL,
                OperationsWatchPolicy.attentionKey("ready", 2, 3, 4, true));
        assertEquals(OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL,
                OperationsWatchPolicy.attentionKey("ready", 0, 3, 4, true));
        assertEquals(OperationsWatchPolicy.ATTENTION_DEVICES,
                OperationsWatchPolicy.attentionKey("ready", 0, 3, 4, false));
        assertEquals(OperationsWatchPolicy.ATTENTION_ERRORS,
                OperationsWatchPolicy.attentionKey("ready", 0, 3, 0, false));
        assertEquals("", OperationsWatchPolicy.attentionKey("slow", 0, 0, 0, false));

        assertTrue(OperationsWatchPolicy.shouldPostAttention(
                OperationsWatchPolicy.ATTENTION_ERRORS,
                "",
                evidence('a', 100L, 10L),
                OperationsMonitorEvidenceRevision.Evidence.EMPTY));
        assertFalse(OperationsWatchPolicy.shouldPostAttention(
                OperationsWatchPolicy.ATTENTION_ERRORS,
                OperationsWatchPolicy.ATTENTION_ERRORS,
                evidence('a', 100L, 10L),
                OperationsMonitorEvidenceRevision.Evidence.EMPTY));
        assertTrue(OperationsWatchPolicy.shouldPostAttention(
                OperationsWatchPolicy.ATTENTION_ERRORS,
                OperationsWatchPolicy.ATTENTION_ERRORS,
                evidence('b', 200L, 8L),
                evidence('a', 100L, 10L)));
        assertTrue(OperationsWatchPolicy.shouldPostAttention(
                OperationsWatchPolicy.ATTENTION_DEVICES,
                OperationsWatchPolicy.ATTENTION_DEVICES,
                evidence('b', 0L, 20L),
                evidence('a', 0L, 10L)));
        assertFalse(OperationsWatchPolicy.shouldPostAttention(
                OperationsWatchPolicy.ATTENTION_DEVICES,
                OperationsWatchPolicy.ATTENTION_DEVICES,
                evidence('b', 0L, 5L),
                evidence('a', 0L, 10L)));
        assertFalse(OperationsWatchPolicy.shouldPostAttention(
                OperationsWatchPolicy.ATTENTION_ERRORS,
                OperationsWatchPolicy.ATTENTION_ERRORS,
                evidence('b', 50L, 100L),
                evidence('a', 100L, 10L)));
        assertFalse(OperationsWatchPolicy.shouldPostAttention(
                OperationsWatchPolicy.ATTENTION_ERRORS,
                OperationsWatchPolicy.ATTENTION_ERRORS,
                evidence('a', 100L, 10L),
                evidence('a', 100L, 10L)));
        assertFalse(OperationsWatchPolicy.shouldPostAttention(
                "",
                OperationsWatchPolicy.ATTENTION_ERRORS,
                OperationsMonitorEvidenceRevision.Evidence.EMPTY,
                evidence('a', 100L, 10L)));
        assertTrue(OperationsWatchPolicy.isEvidenceUpdate(
                OperationsWatchPolicy.ATTENTION_ERRORS,
                OperationsWatchPolicy.ATTENTION_ERRORS,
                evidence('b', 200L, 8L),
                evidence('a', 100L, 10L)));
        assertEquals("同类异常出现新证据 · 发现错误事件 · 点击进入问题中心",
                OperationsWatchPolicy.attentionMessage(
                        OperationsWatchPolicy.ATTENTION_ERRORS, true));
    }

    @Test
    public void offlineAlertRequiresARealOnlineToOfflineTransition() {
        assertFalse(OperationsWatchPolicy.shouldPostOffline(false, true, ""));
        assertFalse(OperationsWatchPolicy.shouldPostOffline(true, false, ""));
        assertTrue(OperationsWatchPolicy.shouldPostOffline(true, true, ""));
        assertFalse(OperationsWatchPolicy.shouldPostOffline(
                true, true, OperationsWatchPolicy.ATTENTION_OFFLINE));
    }

    @Test
    public void attentionDestinationsStayInsideFixedOperationsScreens() {
        assertEquals(OperationsWatchPolicy.DESTINATION_TRIAGE,
                OperationsWatchPolicy.attentionDestination(OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE));
        assertEquals(OperationsWatchPolicy.DESTINATION_TRIAGE,
                OperationsWatchPolicy.attentionDestination(OperationsWatchPolicy.ATTENTION_CRITICAL));
        assertEquals(OperationsWatchPolicy.DESTINATION_TRIAGE,
                OperationsWatchPolicy.attentionDestination(OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL));
        assertEquals(OperationsWatchPolicy.DESTINATION_TRIAGE,
                OperationsWatchPolicy.attentionDestination(OperationsWatchPolicy.ATTENTION_DEVICES));
        assertEquals(OperationsWatchPolicy.DESTINATION_TRIAGE,
                OperationsWatchPolicy.attentionDestination(OperationsWatchPolicy.ATTENTION_ERRORS));
        assertEquals(OperationsWatchPolicy.DESTINATION_CONNECTION_CHECK,
                OperationsWatchPolicy.attentionDestination(OperationsWatchPolicy.ATTENTION_OFFLINE));
        assertEquals(OperationsWatchPolicy.DESTINATION_CONNECTIONS,
                OperationsWatchPolicy.attentionDestination(OperationsWatchPolicy.ATTENTION_REVOKED));
        assertEquals("", OperationsWatchPolicy.attentionDestination("unknown"));

        assertEquals(OperationsWatchPolicy.DESTINATION_TRIAGE,
                OperationsWatchPolicy.normalizeDestination(OperationsWatchPolicy.DESTINATION_TRIAGE));
        assertEquals(OperationsWatchPolicy.DESTINATION_CONNECTION_CHECK,
                OperationsWatchPolicy.normalizeDestination(OperationsWatchPolicy.DESTINATION_CONNECTION_CHECK));
        assertEquals(OperationsWatchPolicy.DESTINATION_CONNECTIONS,
                OperationsWatchPolicy.normalizeDestination(
                        OperationsWatchPolicy.DESTINATION_CONNECTIONS));
        assertEquals("", OperationsWatchPolicy.normalizeDestination("/ops/v1/audit"));
        assertEquals("", OperationsWatchPolicy.normalizeDestination(null));
    }

    @Test
    public void completedBackgroundCheckMustStillBelongToTheActiveComputer() {
        assertTrue(OperationsWatchPolicy.isCurrentProfileCheck(
                "host_1", "host_1", 7, 7));
        assertFalse(OperationsWatchPolicy.isCurrentProfileCheck(
                "host_1", "host_2", 7, 7));
        assertFalse(OperationsWatchPolicy.isCurrentProfileCheck(
                "host_1", "host_1", 6, 7));
        assertFalse(OperationsWatchPolicy.isCurrentProfileCheck(
                null, "host_1", 7, 7));
    }

    private static OperationsMonitorEvidenceRevision.Evidence evidence(
            char revisionCharacter, long sequence, long burden) {
        return new OperationsMonitorEvidenceRevision.Evidence(
                String.valueOf(revisionCharacter).repeat(64), sequence, burden);
    }
}

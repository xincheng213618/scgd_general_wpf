package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsAttentionFocusTest {
    @Test
    public void onlyProblemCenterAttentionCategoriesAreAccepted() {
        assertEquals(OperationsWatchPolicy.ATTENTION_DEVICES,
                OperationsAttentionFocus.normalize(OperationsWatchPolicy.ATTENTION_DEVICES));
        assertEquals(OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE,
                OperationsAttentionFocus.normalize(
                        OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE));
        assertEquals("", OperationsAttentionFocus.normalize(
                OperationsWatchPolicy.ATTENTION_OFFLINE));
        assertEquals("", OperationsAttentionFocus.normalize("../../arbitrary"));
        assertEquals("", OperationsAttentionFocus.normalize(null));
        assertEquals(OperationsWatchPolicy.ATTENTION_DEVICES,
                OperationsAttentionFocus.fromWatchState(
                        OperationsWatchHistory.attentionState(
                                OperationsWatchPolicy.ATTENTION_DEVICES)));
        assertEquals("", OperationsAttentionFocus.fromWatchState(
                OperationsWatchHistory.STATE_OFFLINE));
    }

    @Test
    public void directFindingsMatchTheBoundedAttentionCategory() {
        assertTrue(OperationsAttentionFocus.matchesFinding(
                OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE,
                "desktop",
                "error"));
        assertTrue(OperationsAttentionFocus.matchesFinding(
                OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE,
                "failure-evidence",
                "error"));
        assertTrue(OperationsAttentionFocus.matchesFinding(
                OperationsWatchPolicy.ATTENTION_CRITICAL,
                "diagnostics",
                "critical"));
        assertTrue(OperationsAttentionFocus.matchesFinding(
                OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL,
                "message-service",
                "warning"));
        assertTrue(OperationsAttentionFocus.matchesFinding(
                OperationsWatchPolicy.ATTENTION_DEVICES,
                "devices",
                "warning"));
        assertTrue(OperationsAttentionFocus.matchesFinding(
                OperationsWatchPolicy.ATTENTION_ERRORS,
                "diagnostics",
                "error"));
        assertFalse(OperationsAttentionFocus.matchesFinding(
                OperationsWatchPolicy.ATTENTION_DEVICES,
                "diagnostics",
                "warning"));
    }

    @Test
    public void signedSnapshotSectionsUseTheSameAttentionMeaning() {
        assertTrue(OperationsAttentionFocus.matchesRemoteSection(
                OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE, "performance"));
        assertTrue(OperationsAttentionFocus.matchesRemoteSection(
                OperationsWatchPolicy.ATTENTION_CRITICAL, "alerts"));
        assertTrue(OperationsAttentionFocus.matchesRemoteSection(
                OperationsWatchPolicy.ATTENTION_ERRORS, "alerts"));
        assertTrue(OperationsAttentionFocus.matchesRemoteSection(
                OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL, "message"));
        assertTrue(OperationsAttentionFocus.matchesRemoteSection(
                OperationsWatchPolicy.ATTENTION_DEVICES, "devices"));
        assertFalse(OperationsAttentionFocus.matchesRemoteSection(
                OperationsWatchPolicy.ATTENTION_DEVICES, "alerts"));
    }

    @Test
    public void contextExplainsFocusedResolvedAndUnavailableOutcomes() {
        assertEquals("来自后台提醒 · 已定位“检测设备”相关证据并优先显示。",
                OperationsAttentionFocus.contextMessage(
                        OperationsWatchPolicy.ATTENTION_DEVICES, true, true));
        assertTrue(OperationsAttentionFocus.contextMessage(
                        OperationsWatchPolicy.ATTENTION_DEVICES, false, true)
                .contains("已不再发现“检测设备”"));
        assertTrue(OperationsAttentionFocus.contextMessage(
                        OperationsWatchPolicy.ATTENTION_DEVICES, false, false)
                .contains("尚不能确认“检测设备”"));
        assertEquals("", OperationsAttentionFocus.contextMessage(
                "arbitrary", true, true));
    }
}

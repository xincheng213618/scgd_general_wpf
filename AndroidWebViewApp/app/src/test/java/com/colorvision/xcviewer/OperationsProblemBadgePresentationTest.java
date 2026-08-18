package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsProblemBadgePresentationTest {
    @Test
    public void healthyAndUnpairedDestinationsDoNotShowAProblemBadge() {
        assertFalse(model(false, OperationsWatchHistory.STATE_OFFLINE).visible);
        assertFalse(model(true, "").visible);
        assertFalse(model(true, OperationsWatchHistory.STATE_ONLINE).visible);
        assertFalse(model(true, OperationsWatchHistory.STATE_REMOTE_ONLINE).visible);
        assertFalse(model(true, "arbitrary").visible);
    }

    @Test
    public void connectionStatesThatNeedActionUseANumberlessBadge() {
        assertTrue(model(true, OperationsWatchHistory.STATE_OFFLINE).visible);
        assertTrue(model(true, OperationsWatchHistory.STATE_REMOTE_WAITING).visible);
        assertTrue(model(true, OperationsWatchHistory.STATE_REVOKED).visible);
        assertEquals(
                "有待关注状态，连接中断 · 后台自动重试",
                model(true, OperationsWatchHistory.STATE_OFFLINE).contentDescription);
    }

    @Test
    public void boundedMonitorAttentionStatesExposeTheirMeaningToAssistiveTechnology() {
        String state = OperationsWatchHistory.attentionState(
                OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL);
        OperationsProblemBadgePresentation.ViewModel model = model(true, state);

        assertTrue(model.visible);
        assertEquals(
                "有待关注状态，在线 · 消息通道需要关注",
                model.contentDescription);
    }

    private static OperationsProblemBadgePresentation.ViewModel model(
            boolean hasStoredProfile, String state) {
        return OperationsProblemBadgePresentation.create(hasStoredProfile, state);
    }
}

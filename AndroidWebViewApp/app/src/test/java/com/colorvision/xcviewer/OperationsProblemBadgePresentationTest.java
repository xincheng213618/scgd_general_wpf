package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsProblemBadgePresentationTest {
    @Test
    public void healthyAndUnpairedDestinationsDoNotShowAProblemBadge() {
        assertFalse(model(false, OperationsWatchHistory.STATE_OFFLINE, 3).visible);
        assertFalse(model(true, "", 0).visible);
        assertFalse(model(true, OperationsWatchHistory.STATE_ONLINE, 0).visible);
        assertFalse(model(true, OperationsWatchHistory.STATE_REMOTE_ONLINE, 0).visible);
        assertFalse(model(true, "arbitrary", 0).visible);
    }

    @Test
    public void connectionStatesThatNeedActionUseANumberlessBadge() {
        assertTrue(model(true, OperationsWatchHistory.STATE_OFFLINE, 0).visible);
        assertTrue(model(true, OperationsWatchHistory.STATE_REMOTE_WAITING, 0).visible);
        assertTrue(model(true, OperationsWatchHistory.STATE_REVOKED, 0).visible);
        assertEquals(0, model(true, OperationsWatchHistory.STATE_OFFLINE, 0).number);
        assertEquals(
                "有待关注状态，连接中断 · 后台自动重试",
                model(true, OperationsWatchHistory.STATE_OFFLINE, 0).contentDescription);
    }

    @Test
    public void knownIssueCountUsesANumericBadgeEvenBeforeTheWatchStateCatchesUp() {
        OperationsProblemBadgePresentation.ViewModel model =
                model(true, OperationsWatchHistory.STATE_ONLINE, 2);

        assertTrue(model.visible);
        assertEquals(2, model.number);
        assertEquals("2 项待复核", model.contentDescription);
        assertEquals(999,
                model(true, OperationsWatchHistory.STATE_ONLINE, 4_000).number);
    }

    @Test
    public void boundedMonitorAttentionStatesExposeTheirMeaningToAssistiveTechnology() {
        String state = OperationsWatchHistory.attentionState(
                OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL);
        OperationsProblemBadgePresentation.ViewModel model = model(true, state, 0);

        assertTrue(model.visible);
        assertEquals(
                "有待关注状态，在线 · 消息通道需要关注",
                model.contentDescription);
    }

    private static OperationsProblemBadgePresentation.ViewModel model(
            boolean hasStoredProfile, String state, int issueCount) {
        return OperationsProblemBadgePresentation.create(
                hasStoredProfile, state, issueCount);
    }
}

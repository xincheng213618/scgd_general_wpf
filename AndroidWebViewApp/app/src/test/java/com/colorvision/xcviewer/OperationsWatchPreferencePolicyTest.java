package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsWatchPreferencePolicyTest {
    @Test
    public void watchRunsOnlyForAnEnabledPairedProfile() {
        assertTrue(OperationsWatchPreferencePolicy.shouldRun(true, true));
        assertFalse(OperationsWatchPreferencePolicy.shouldRun(false, true));
        assertFalse(OperationsWatchPreferencePolicy.shouldRun(true, false));
    }

    @Test
    public void statusExplainsEnabledDisabledAndUnpairedStates() {
        assertEquals("后台检查与异常提醒已开启",
                OperationsWatchPreferencePolicy.status(true, true, true));
        assertEquals("后台检查已开启 · 提醒未开启",
                OperationsWatchPreferencePolicy.status(true, true, false));
        assertEquals("配对后自动启动",
                OperationsWatchPreferencePolicy.status(false, true, false));
        assertEquals("已关闭",
                OperationsWatchPreferencePolicy.status(true, false, false));
    }

    @Test
    public void enabledFeedbackDoesNotPromiseUnavailableReminders() {
        assertEquals("后台检查与运维提醒已开启",
                OperationsWatchPreferencePolicy.enabledFeedback(true, true));
        assertEquals("后台检查已开启；提醒尚未开启",
                OperationsWatchPreferencePolicy.enabledFeedback(true, false));
        assertEquals("配对电脑后将自动开启持续守护",
                OperationsWatchPreferencePolicy.enabledFeedback(false, false));
        assertTrue(OperationsWatchPreferencePolicy.shouldOfferReminderAction(true, false));
        assertFalse(OperationsWatchPreferencePolicy.shouldOfferReminderAction(true, true));
        assertFalse(OperationsWatchPreferencePolicy.shouldOfferReminderAction(false, false));
    }
}

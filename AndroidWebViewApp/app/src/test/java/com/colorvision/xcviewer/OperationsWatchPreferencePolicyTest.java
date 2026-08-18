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
        assertEquals("检查当前电脑并提醒",
                OperationsWatchPreferencePolicy.status(true, true));
        assertEquals("配对后自动启动",
                OperationsWatchPreferencePolicy.status(false, true));
        assertEquals("已关闭",
                OperationsWatchPreferencePolicy.status(true, false));
    }
}

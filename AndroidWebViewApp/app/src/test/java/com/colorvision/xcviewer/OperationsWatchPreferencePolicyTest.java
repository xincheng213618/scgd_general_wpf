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
                OperationsWatchPreferencePolicy.status(true, true, true, 1, 1));
        assertEquals("后台检查已开启 · 提醒未开启",
                OperationsWatchPreferencePolicy.status(true, true, false, 1, 1));
        assertEquals("3 台电脑后台轮巡与异常提醒已开启",
                OperationsWatchPreferencePolicy.status(true, true, true, 3, 3));
        assertEquals("3 台电脑后台轮巡 · 2 台异常提醒已开启",
                OperationsWatchPreferencePolicy.status(true, true, true, 3, 2));
        assertEquals("3 台电脑后台轮巡已开启 · 每台提醒均暂停",
                OperationsWatchPreferencePolicy.status(true, true, true, 3, 0));
        assertEquals("后台检查已开启 · 当前电脑提醒已暂停",
                OperationsWatchPreferencePolicy.status(true, true, true, 1, 0));
        assertEquals("3 台电脑后台轮巡已开启 · 提醒未开启",
                OperationsWatchPreferencePolicy.status(true, true, false, 3, 2));
        assertEquals("配对后自动启动",
                OperationsWatchPreferencePolicy.status(false, true, false, 0, 0));
        assertEquals("已关闭",
                OperationsWatchPreferencePolicy.status(true, false, false, 1, 1));
    }

    @Test
    public void enabledFeedbackDoesNotPromiseUnavailableReminders() {
        assertEquals("后台检查与运维提醒已开启",
                OperationsWatchPreferencePolicy.enabledFeedback(true, true, 1, 1));
        assertEquals("后台检查已开启；提醒尚未开启",
                OperationsWatchPreferencePolicy.enabledFeedback(true, false, 1, 1));
        assertEquals("2 台电脑后台轮巡与运维提醒已开启",
                OperationsWatchPreferencePolicy.enabledFeedback(true, true, 2, 2));
        assertEquals("2 台电脑后台轮巡已开启；1 台接收异常提醒",
                OperationsWatchPreferencePolicy.enabledFeedback(true, true, 2, 1));
        assertEquals("配对电脑后将自动开启持续守护",
                OperationsWatchPreferencePolicy.enabledFeedback(false, false, 0, 0));
        assertTrue(OperationsWatchPreferencePolicy.shouldOfferReminderAction(true, false));
        assertFalse(OperationsWatchPreferencePolicy.shouldOfferReminderAction(true, true));
        assertFalse(OperationsWatchPreferencePolicy.shouldOfferReminderAction(false, false));
        assertEquals("", OperationsWatchPreferencePolicy.fleetScopeDetails(1, 1));
        assertTrue(OperationsWatchPreferencePolicy.fleetScopeDetails(3, 3)
                .contains("其他已配对电脑"));
        assertTrue(OperationsWatchPreferencePolicy.fleetScopeDetails(3, 3)
                .contains("点开后才切换"));
        assertTrue(OperationsWatchPreferencePolicy.fleetScopeDetails(3, 2)
                .contains("1 台已暂停"));
        assertTrue(OperationsWatchPreferencePolicy.fleetScopeDetails(1, 0)
                .contains("当前电脑异常提醒已暂停"));
    }
}

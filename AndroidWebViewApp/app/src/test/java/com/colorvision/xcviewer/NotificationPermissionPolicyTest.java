package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class NotificationPermissionPolicyTest {
    @Test
    public void onlyAnExplicitDeniedResultIsRecordedAsBlocked() {
        assertTrue(NotificationPermissionPolicy.shouldRecordDeniedRequest(
                false, 1, 1));
        assertFalse(NotificationPermissionPolicy.shouldRecordDeniedRequest(
                true, 1, 1));
        assertFalse(NotificationPermissionPolicy.shouldRecordDeniedRequest(
                false, 0, 0));
    }

    @Test
    public void firstOrDismissedAndroid13RequestStaysUserInitiated() {
        assertEquals(NotificationPermissionPolicy.ACTION_REQUEST,
                NotificationPermissionPolicy.action(33, false, false, true, false, false));
        assertEquals("尚未开启",
                NotificationPermissionPolicy.status(33, false, false, true, false, false));
    }

    @Test
    public void deniedPermissionCanBeRequestedAgainOnlyWithSystemRationale() {
        assertEquals(NotificationPermissionPolicy.ACTION_REQUEST,
                NotificationPermissionPolicy.action(35, false, false, true, true, true));
        assertEquals(NotificationPermissionPolicy.ACTION_OPEN_SETTINGS,
                NotificationPermissionPolicy.action(35, false, false, true, true, false));
        assertEquals("需在系统设置开启",
                NotificationPermissionPolicy.status(35, false, false, true, true, false));
    }

    @Test
    public void grantedPermissionStillSurfacesDisabledAttentionChannel() {
        assertEquals(NotificationPermissionPolicy.ACTION_MANAGE,
                NotificationPermissionPolicy.action(35, true, true, true, true, false));
        assertEquals("已开启 · 可测试",
                NotificationPermissionPolicy.status(35, true, true, true, true, false));
        assertEquals(NotificationPermissionPolicy.ACTION_OPEN_SETTINGS,
                NotificationPermissionPolicy.action(35, true, true, false, true, false));
        assertEquals("提醒渠道已关闭",
                NotificationPermissionPolicy.status(35, true, true, false, true, false));
    }

    @Test
    public void preAndroid13UsesSystemNotificationSettingsWhenGloballyDisabled() {
        assertEquals(NotificationPermissionPolicy.ACTION_MANAGE,
                NotificationPermissionPolicy.action(32, false, true, true, false, false));
        assertEquals(NotificationPermissionPolicy.ACTION_OPEN_SETTINGS,
                NotificationPermissionPolicy.action(32, false, false, true, false, false));
    }

    @Test
    public void attentionAvailabilityRequiresEveryNotificationBoundary() {
        assertTrue(NotificationPermissionPolicy.canPostAttention(
                35, true, true, true));
        assertFalse(NotificationPermissionPolicy.canPostAttention(
                35, false, true, true));
        assertFalse(NotificationPermissionPolicy.canPostAttention(
                35, true, false, true));
        assertFalse(NotificationPermissionPolicy.canPostAttention(
                35, true, true, false));
        assertTrue(NotificationPermissionPolicy.canPostAttention(
                32, false, true, true));
    }
}

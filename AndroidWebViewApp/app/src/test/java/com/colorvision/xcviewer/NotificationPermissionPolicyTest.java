package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class NotificationPermissionPolicyTest {
    @Test
    public void firstOrDismissedAndroid13RequestStaysUserInitiated() {
        assertEquals(NotificationPermissionPolicy.ACTION_REQUEST,
                NotificationPermissionPolicy.action(33, false, false, true, false, false));
        assertEquals("未授权",
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
        assertEquals("已授权",
                NotificationPermissionPolicy.status(35, true, true, true, true, false));
        assertEquals(NotificationPermissionPolicy.ACTION_OPEN_SETTINGS,
                NotificationPermissionPolicy.action(35, true, true, false, true, false));
        assertEquals("提醒已关闭",
                NotificationPermissionPolicy.status(35, true, true, false, true, false));
    }

    @Test
    public void preAndroid13UsesSystemNotificationSettingsWhenGloballyDisabled() {
        assertEquals(NotificationPermissionPolicy.ACTION_MANAGE,
                NotificationPermissionPolicy.action(32, false, true, true, false, false));
        assertEquals(NotificationPermissionPolicy.ACTION_OPEN_SETTINGS,
                NotificationPermissionPolicy.action(32, false, false, true, false, false));
    }
}

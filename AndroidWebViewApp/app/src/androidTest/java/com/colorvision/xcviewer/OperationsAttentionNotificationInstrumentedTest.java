package com.colorvision.xcviewer;

import android.Manifest;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.content.Context;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.SystemClock;
import android.service.notification.StatusBarNotification;

import androidx.core.app.NotificationCompat;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import org.junit.Test;
import org.junit.runner.RunWith;

import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertTrue;

@RunWith(AndroidJUnit4.class)
public final class OperationsAttentionNotificationInstrumentedTest {
    private static final String TEST_CHANNEL_ID = "operations_attention_test";
    private static final String HOST_A = "instrumented_host_a";
    private static final String HOST_B = "instrumented_host_b";

    @Test
    public void dismissingReviewedHostKeepsAnotherComputerNotification() {
        Context context = InstrumentationRegistry.getInstrumentation().getTargetContext();
        NotificationManager manager = context.getSystemService(NotificationManager.class);
        assertNotNull(manager);
        boolean grantedNotificationPermissionForTest = false;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU
                && context.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)
                        != PackageManager.PERMISSION_GRANTED) {
            InstrumentationRegistry.getInstrumentation().getUiAutomation()
                    .grantRuntimePermission(
                            context.getPackageName(), Manifest.permission.POST_NOTIFICATIONS);
            grantedNotificationPermissionForTest = true;
        }
        String tagA = OperationsBackgroundFleetPolicy.attentionNotificationTag(HOST_A);
        String tagB = OperationsBackgroundFleetPolicy.attentionNotificationTag(HOST_B);
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                manager.createNotificationChannel(new NotificationChannel(
                        TEST_CHANNEL_ID,
                        "运维提醒隔离测试",
                        NotificationManager.IMPORTANCE_MIN));
            }
            Notification notification = new NotificationCompat.Builder(context, TEST_CHANNEL_ID)
                    .setSmallIcon(R.drawable.ic_devices_24)
                    .setContentTitle("运维提醒隔离测试")
                    .setContentText("仅验证按电脑清除，不触发声音或振动")
                    .setSilent(true)
                    .setPriority(NotificationCompat.PRIORITY_MIN)
                    .build();
            manager.notify(tagA, OperationsWatchService.ATTENTION_NOTIFICATION_ID, notification);
            manager.notify(tagB, OperationsWatchService.ATTENTION_NOTIFICATION_ID, notification);
            assertTrue(awaitNotification(manager, tagA, true));
            assertTrue(awaitNotification(manager, tagB, true));

            OperationsWatchService.dismissAttentionNotification(context, HOST_A);

            assertTrue(awaitNotification(manager, tagA, false));
            assertTrue(awaitNotification(manager, tagB, true));
        } finally {
            manager.cancel(tagA, OperationsWatchService.ATTENTION_NOTIFICATION_ID);
            manager.cancel(tagB, OperationsWatchService.ATTENTION_NOTIFICATION_ID);
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                manager.deleteNotificationChannel(TEST_CHANNEL_ID);
            }
            if (grantedNotificationPermissionForTest) {
                InstrumentationRegistry.getInstrumentation().getUiAutomation()
                        .revokeRuntimePermission(
                                context.getPackageName(), Manifest.permission.POST_NOTIFICATIONS);
            }
        }
    }

    private static boolean hasNotification(NotificationManager manager, String tag) {
        for (StatusBarNotification notification : manager.getActiveNotifications()) {
            if (OperationsWatchService.ATTENTION_NOTIFICATION_ID == notification.getId()
                    && tag.equals(notification.getTag())) {
                return true;
            }
        }
        return false;
    }

    private static boolean awaitNotification(
            NotificationManager manager, String tag, boolean expected) {
        long deadline = SystemClock.elapsedRealtime() + 1_000L;
        do {
            if (hasNotification(manager, tag) == expected) {
                return true;
            }
            SystemClock.sleep(50L);
        } while (SystemClock.elapsedRealtime() < deadline);
        return hasNotification(manager, tag) == expected;
    }
}

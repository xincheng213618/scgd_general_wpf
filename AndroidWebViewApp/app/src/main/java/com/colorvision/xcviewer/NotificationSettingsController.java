package com.colorvision.xcviewer;

import android.Manifest;
import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;
import android.widget.Toast;

import com.google.android.material.dialog.MaterialAlertDialogBuilder;

final class NotificationSettingsController {
    static final int REQUEST_NOTIFICATION_PERMISSION = 2505;

    private final Activity activity;
    private final AppPreferences preferences;
    private final Host host;
    private final RuntimePermissionDialogState permissionDialogState =
            new RuntimePermissionDialogState();
    private String lastStatus;

    NotificationSettingsController(
            Activity activity,
            AppPreferences preferences,
            Host host) {
        this.activity = activity;
        this.preferences = preferences;
        this.host = host;
        lastStatus = status();
    }

    String status() {
        return NotificationPermissionPolicy.status(
                Build.VERSION.SDK_INT,
                NotificationPermissionState.hasRuntimePermission(activity),
                NotificationPermissionState.appNotificationsEnabled(activity),
                NotificationPermissionState.attentionChannelEnabled(activity),
                preferences.isNotificationPermissionBlocked(),
                shouldShowRationale());
    }

    boolean remindersAvailable() {
        return NotificationPermissionPolicy.canPostAttention(
                Build.VERSION.SDK_INT,
                NotificationPermissionState.hasRuntimePermission(activity),
                NotificationPermissionState.appNotificationsEnabled(activity),
                NotificationPermissionState.attentionChannelEnabled(activity));
    }

    void manage() {
        int action = NotificationPermissionPolicy.action(
                Build.VERSION.SDK_INT,
                NotificationPermissionState.hasRuntimePermission(activity),
                NotificationPermissionState.appNotificationsEnabled(activity),
                NotificationPermissionState.attentionChannelEnabled(activity),
                preferences.isNotificationPermissionBlocked(),
                shouldShowRationale());
        if (action == NotificationPermissionPolicy.ACTION_REQUEST) {
            showPermissionExplanation();
        } else if (action == NotificationPermissionPolicy.ACTION_MANAGE) {
            showManagementDialog();
        } else {
            openSystemSettings();
        }
    }

    boolean handlePermissionResult(
            int requestCode,
            String[] permissions,
            int[] grantResults) {
        if (requestCode != REQUEST_NOTIFICATION_PERMISSION) {
            return false;
        }
        boolean granted = NotificationPermissionState.hasRuntimePermission(activity);
        permissionDialogState.completeFromSystemResult(granted);
        if (NotificationPermissionPolicy.shouldRecordDeniedRequest(
                granted, permissions.length, grantResults.length)) {
            preferences.saveNotificationPermissionBlocked(true);
        } else if (granted) {
            preferences.saveNotificationPermissionBlocked(false);
        }
        lastStatus = status();
        host.onSettingsChanged();
        if (granted) {
            OperationsWatchService.start(activity);
            Toast.makeText(activity, "运维提醒已开启", Toast.LENGTH_SHORT).show();
        } else {
            host.showFeedback(
                    activity.getString(R.string.operations_reminder_permission_denied),
                    false);
        }
        return true;
    }

    boolean refreshOnResume() {
        if (NotificationPermissionState.hasRuntimePermission(activity)
                || shouldShowRationale()) {
            preferences.saveNotificationPermissionBlocked(false);
        }
        String currentStatus = status();
        boolean changed = !currentStatus.equals(lastStatus);
        lastStatus = currentStatus;
        return changed;
    }

    private boolean shouldShowRationale() {
        return Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU
                && activity.shouldShowRequestPermissionRationale(
                        Manifest.permission.POST_NOTIFICATIONS);
    }

    private void showManagementDialog() {
        new MaterialAlertDialogBuilder(activity)
                .setTitle(R.string.operations_reminder_manage_title)
                .setMessage(R.string.operations_reminder_manage_message)
                .setNegativeButton(
                        R.string.operations_reminder_system_settings_action,
                        (dialog, which) -> openSystemSettings())
                .setPositiveButton(
                        R.string.operations_reminder_test_action,
                        (dialog, which) -> sendReminderTest())
                .show();
    }

    private void sendReminderTest() {
        boolean posted = OperationsWatchService.postReminderTest(activity);
        if (!posted) {
            lastStatus = status();
            host.onSettingsChanged();
        }
        host.showFeedback(
                activity.getString(posted
                        ? R.string.operations_reminder_test_sent
                        : R.string.operations_reminder_test_unavailable),
                !posted);
    }

    private void showPermissionExplanation() {
        new MaterialAlertDialogBuilder(activity)
                .setTitle(R.string.operations_reminder_permission_title)
                .setMessage(R.string.operations_reminder_permission_message)
                .setNegativeButton(R.string.operations_reminder_permission_later, null)
                .setPositiveButton(
                        R.string.operations_reminder_permission_action,
                        (dialog, which) -> requestPermission())
                .show();
    }

    private void requestPermission() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU) {
            openSystemSettings();
            return;
        }
        int requestGeneration = permissionDialogState.begin();
        activity.requestPermissions(
                new String[]{Manifest.permission.POST_NOTIFICATIONS},
                REQUEST_NOTIFICATION_PERMISSION);
        activity.getWindow().getDecorView().postDelayed(
                () -> permissionDialogState.observe(
                        requestGeneration,
                        NotificationPermissionState.hasRuntimePermission(activity),
                        activity.hasWindowFocus()),
                RuntimePermissionDialogState.OBSERVE_DELAY_MILLISECONDS);
        activity.getWindow().getDecorView().postDelayed(
                () -> recoverBlockedRequest(requestGeneration),
                RuntimePermissionDialogState.NO_DIALOG_RECOVERY_DELAY_MILLISECONDS);
    }

    private void recoverBlockedRequest(int requestGeneration) {
        if (activity.isFinishing()
                || !permissionDialogState.shouldRecoverAsBlocked(
                        requestGeneration,
                        NotificationPermissionState.hasRuntimePermission(activity),
                        activity.hasWindowFocus())) {
            return;
        }
        preferences.saveNotificationPermissionBlocked(true);
        lastStatus = status();
        host.onSettingsChanged();
        Toast.makeText(
                activity,
                "请在系统通知设置中开启运维提醒",
                Toast.LENGTH_LONG).show();
        openSystemSettings();
    }

    private void openSystemSettings() {
        try {
            Intent settings = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                    ? new Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS)
                            .putExtra(Settings.EXTRA_APP_PACKAGE, activity.getPackageName())
                    : new Intent(
                            Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                            Uri.parse("package:" + activity.getPackageName()));
            activity.startActivity(settings);
        } catch (Exception ex) {
            try {
                activity.startActivity(new Intent(
                        Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                        Uri.parse("package:" + activity.getPackageName())));
            } catch (Exception ignored) {
                Toast.makeText(
                        activity,
                        "无法打开系统通知设置",
                        Toast.LENGTH_LONG).show();
            }
        }
    }

    interface Host {
        void onSettingsChanged();

        void showFeedback(String message, boolean offerReminderAction);
    }
}

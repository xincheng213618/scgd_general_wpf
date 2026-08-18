package com.colorvision.xcviewer;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.provider.Settings;

import com.google.android.material.dialog.MaterialAlertDialogBuilder;

final class QrScanRecoveryDialog {
    private QrScanRecoveryDialog() {
    }

    static void show(Activity activity, String reason, Runnable retry) {
        MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(activity)
                .setTitle(QrScanFailurePresentation.title(reason))
                .setMessage(QrScanFailurePresentation.message(reason))
                .setNegativeButton("暂不", null);
        if (QrScanFailurePresentation.opensSystemSettings(reason)) {
            builder.setPositiveButton(
                    QrScanFailurePresentation.primaryAction(reason),
                    (dialog, which) -> openApplicationSettings(activity));
        } else {
            builder.setPositiveButton(
                    QrScanFailurePresentation.primaryAction(reason),
                    (dialog, which) -> retry.run());
        }
        builder.show();
    }

    private static void openApplicationSettings(Activity activity) {
        Intent intent = new Intent(
                Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                Uri.parse("package:" + activity.getPackageName()));
        activity.startActivity(intent);
    }
}

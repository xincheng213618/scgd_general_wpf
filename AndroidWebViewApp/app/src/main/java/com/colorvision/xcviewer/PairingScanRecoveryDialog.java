package com.colorvision.xcviewer;

import android.app.Activity;

import com.google.android.material.dialog.MaterialAlertDialogBuilder;

final class PairingScanRecoveryDialog {
    private PairingScanRecoveryDialog() {
    }

    static void show(Activity activity, String reason, Runnable retryScan) {
        new MaterialAlertDialogBuilder(activity)
                .setTitle(PairingFailurePresentation.title(reason))
                .setMessage(PairingFailurePresentation.message(reason))
                .setNegativeButton("暂不", null)
                .setPositiveButton("重新扫描", (dialog, which) -> retryScan.run())
                .show();
    }
}

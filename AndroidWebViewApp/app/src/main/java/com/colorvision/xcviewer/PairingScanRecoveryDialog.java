package com.colorvision.xcviewer;

import android.app.Activity;

import com.google.android.material.dialog.MaterialAlertDialogBuilder;

final class PairingScanRecoveryDialog {
    private PairingScanRecoveryDialog() {
    }

    static void show(Activity activity, String reason, Runnable retryScan) {
        MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(activity)
                .setTitle(PairingFailurePresentation.title(reason))
                .setMessage(PairingFailurePresentation.message(reason))
                .setNegativeButton("暂不", null);
        if (PairingFailurePresentation.opensPairingHelp(reason)) {
            builder.setPositiveButton(
                    PairingFailurePresentation.primaryAction(reason),
                    (dialog, which) -> PairingHelpDialog.show(activity, retryScan));
        } else {
            builder.setPositiveButton(
                    PairingFailurePresentation.primaryAction(reason),
                    (dialog, which) -> retryScan.run());
        }
        builder.show();
    }
}

package com.colorvision.xcviewer;

import android.app.Activity;

import com.google.android.material.dialog.MaterialAlertDialogBuilder;

final class PairingHelpDialog {
    private PairingHelpDialog() {
    }

    static void show(Activity activity, Runnable startScan) {
        new MaterialAlertDialogBuilder(activity)
                .setTitle(PairingHelpPresentation.title())
                .setMessage(PairingHelpPresentation.message())
                .setNegativeButton("稍后", null)
                .setPositiveButton("开始扫描", (dialog, which) -> startScan.run())
                .show();
    }
}

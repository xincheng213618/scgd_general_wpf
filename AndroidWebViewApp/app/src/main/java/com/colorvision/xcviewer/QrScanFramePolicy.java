package com.colorvision.xcviewer;

final class QrScanFramePolicy {
    private QrScanFramePolicy() {
    }

    static boolean shouldAnalyze(
            boolean completed, boolean pairingHelpVisible, int planeCount) {
        return !completed && !pairingHelpVisible && planeCount > 0;
    }
}

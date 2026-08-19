package com.colorvision.xcviewer;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class QrScanFramePolicyTest {
    @Test
    public void helpPausesFrameAnalysisWithoutCompletingTheScan() {
        assertTrue(QrScanFramePolicy.shouldAnalyze(false, false, 1));
        assertFalse(QrScanFramePolicy.shouldAnalyze(false, true, 1));
        assertFalse(QrScanFramePolicy.shouldAnalyze(true, false, 1));
        assertFalse(QrScanFramePolicy.shouldAnalyze(false, false, 0));
    }
}

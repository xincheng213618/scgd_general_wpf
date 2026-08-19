package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class QrScanFailurePresentationTest {
    @Test
    public void firstDenialOffersAnInContextRetry() {
        String reason = QrScanFailurePresentation.CAMERA_PERMISSION_DENIED;

        assertEquals("需要相机权限", QrScanFailurePresentation.title(reason));
        assertEquals("重新扫描", QrScanFailurePresentation.primaryAction(reason));
        assertFalse(QrScanFailurePresentation.opensSystemSettings(reason));
    }

    @Test
    public void permanentDenialRoutesToApplicationSettings() {
        String reason = QrScanFailurePresentation.CAMERA_PERMISSION_BLOCKED;

        assertEquals("请在系统设置开启相机", QrScanFailurePresentation.title(reason));
        assertEquals("打开系统设置", QrScanFailurePresentation.primaryAction(reason));
        assertTrue(QrScanFailurePresentation.opensSystemSettings(reason));
    }

    @Test
    public void unavailableCameraKeepsRetryInsideTheApp() {
        String reason = QrScanFailurePresentation.CAMERA_UNAVAILABLE;

        assertEquals("相机暂时无法使用", QrScanFailurePresentation.title(reason));
        assertEquals("重新扫描", QrScanFailurePresentation.primaryAction(reason));
        assertFalse(QrScanFailurePresentation.opensSystemSettings(reason));
    }
}

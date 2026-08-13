package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class AndroidUpdatePolicyTest {
    @Test
    public void manifestAndDownloadStayOnTheFixedStation() throws Exception {
        assertEquals(
                "http://xc213618.ddns.me:9998/api/android/update",
                AndroidUpdatePolicy.manifestUrl().toString());
        assertEquals(
                "http://xc213618.ddns.me:9998/api/android/update/2.34/download",
                AndroidUpdatePolicy.validatedDownloadUrl("/api/android/update/2.34/download").toString());
    }

    @Test(expected = java.net.MalformedURLException.class)
    public void externalDownloadOriginIsRejected() throws Exception {
        AndroidUpdatePolicy.validatedDownloadUrl("https://example.com/update.apk");
    }

    @Test
    public void releaseContractIsStrictAndBounded() {
        String hash = "a".repeat(64);
        assertTrue(AndroidUpdatePolicy.isValidRelease(
                "2.34", "ColorVision-Android-2.34.apk", 4_000_000L, hash,
                "/api/android/update/2.34/download"));
        assertFalse(AndroidUpdatePolicy.isValidRelease(
                "2.34", "renamed.apk", 4_000_000L, hash,
                "/api/android/update/2.34/download"));
        assertFalse(AndroidUpdatePolicy.isValidRelease(
                "2.34", "ColorVision-Android-2.34.apk", AndroidUpdatePolicy.MAX_APK_BYTES + 1L, hash,
                "/api/android/update/2.34/download"));
        assertFalse(AndroidUpdatePolicy.isValidRelease(
                "2.34", "ColorVision-Android-2.34.apk", 4_000_000L, hash,
                "/download/ColorVision-Android-2.34.apk"));
    }

    @Test
    public void dottedVersionsCompareNumericallyWithoutDowngrades() {
        assertTrue(AndroidUpdatePolicy.isNewerVersion("2.10", "2.9"));
        assertTrue(AndroidUpdatePolicy.isNewerVersion("2.34.1", "2.34"));
        assertFalse(AndroidUpdatePolicy.isNewerVersion("2.34", "2.34.0"));
        assertFalse(AndroidUpdatePolicy.isNewerVersion("1.6", "2.33"));
    }
}

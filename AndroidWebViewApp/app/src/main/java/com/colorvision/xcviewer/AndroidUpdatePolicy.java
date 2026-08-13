package com.colorvision.xcviewer;

import java.net.MalformedURLException;
import java.net.URL;
import java.util.Locale;

final class AndroidUpdatePolicy {
    static final String MANIFEST_PATH = "api/android/update";
    static final long MAX_APK_BYTES = 128L * 1024L * 1024L;
    static final int MAX_MANIFEST_BYTES = 64 * 1024;

    private AndroidUpdatePolicy() {
    }

    static URL manifestUrl() throws MalformedURLException {
        return new URL(new URL(AppNavigationPolicy.FIXED_DOWNLOAD_URL), MANIFEST_PATH);
    }

    static URL validatedDownloadUrl(String downloadPath) throws MalformedURLException {
        URL base = new URL(AppNavigationPolicy.FIXED_DOWNLOAD_URL);
        URL resolved = new URL(base, downloadPath);
        if (!sameOrigin(base, resolved)) {
            throw new MalformedURLException("android_update_download_origin_rejected");
        }
        return resolved;
    }

    static boolean isValidRelease(String version, String filename, long size, String sha256, String downloadPath) {
        return version != null
                && version.matches("\\d+(?:\\.\\d+)+")
                && ("ColorVision-Android-" + version + ".apk").equals(filename)
                && size > 0
                && size <= MAX_APK_BYTES
                && sha256 != null
                && sha256.matches("[0-9a-fA-F]{64}")
                && ("/api/android/update/" + version + "/download").equals(downloadPath);
    }

    static boolean isNewerVersion(String candidate, String current) {
        int[] candidateParts = versionParts(candidate);
        int[] currentParts = versionParts(current);
        int length = Math.max(candidateParts.length, currentParts.length);
        for (int index = 0; index < length; index++) {
            int candidatePart = index < candidateParts.length ? candidateParts[index] : 0;
            int currentPart = index < currentParts.length ? currentParts[index] : 0;
            if (candidatePart != currentPart) {
                return candidatePart > currentPart;
            }
        }
        return false;
    }

    static boolean isApkContentType(String value) {
        if (value == null) {
            return false;
        }
        String normalized = value.toLowerCase(Locale.ROOT);
        return normalized.startsWith("application/vnd.android.package-archive");
    }

    private static int[] versionParts(String value) {
        if (value == null || !value.matches("\\d+(?:\\.\\d+)+")) {
            return new int[0];
        }
        String[] raw = value.split("\\.");
        int[] parts = new int[raw.length];
        try {
            for (int index = 0; index < raw.length; index++) {
                parts[index] = Integer.parseInt(raw[index]);
            }
            return parts;
        } catch (NumberFormatException ignored) {
            return new int[0];
        }
    }

    private static boolean sameOrigin(URL left, URL right) {
        return left.getProtocol().equalsIgnoreCase(right.getProtocol())
                && left.getHost().equalsIgnoreCase(right.getHost())
                && effectivePort(left) == effectivePort(right);
    }

    private static int effectivePort(URL value) {
        return value.getPort() >= 0 ? value.getPort() : value.getDefaultPort();
    }
}

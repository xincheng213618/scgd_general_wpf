package com.colorvision.xcviewer;

import java.net.URL;

final class OperationsRelayPolicy {
    static final String CAPABILITY_SHOW_WINDOW = "ops.window.show";
    static final String CAPABILITY_REQUEST_DIAGNOSTICS = "ops.diagnostics.request";
    static final long HOST_FRESH_MILLISECONDS = 180_000L;
    private static final long FUTURE_TOLERANCE_MILLISECONDS = 125_000L;

    private OperationsRelayPolicy() {
    }

    static URL fixedBaseUrl() throws Exception {
        URL url = new URL(AppNavigationPolicy.FIXED_DOWNLOAD_URL);
        if (!"http".equalsIgnoreCase(url.getProtocol())
                || !"xc213618.ddns.me".equalsIgnoreCase(url.getHost())
                || effectivePort(url) != 9998
                || !"/".equals(url.getPath())
                || url.getQuery() != null
                || url.getRef() != null
                || url.getUserInfo() != null) {
            throw new SecurityException("invalid_fixed_relay_origin");
        }
        return url;
    }

    static boolean isAllowedRequestUrl(URL url) {
        try {
            URL fixed = fixedBaseUrl();
            return fixed.getProtocol().equalsIgnoreCase(url.getProtocol())
                    && fixed.getHost().equalsIgnoreCase(url.getHost())
                    && effectivePort(fixed) == effectivePort(url)
                    && url.getUserInfo() == null
                    && url.getQuery() == null
                    && url.getRef() == null
                    && url.getPath().startsWith("/api/ops/v1/device-relay/");
        } catch (Exception ignored) {
            return false;
        }
    }

    static boolean isSafeIdentifier(String value) {
        return value != null && value.matches("[A-Za-z0-9_-]{1,64}");
    }

    static boolean isHostFresh(long signedAtSeconds, long nowMilliseconds) {
        if (signedAtSeconds <= 0) {
            return false;
        }
        long age = nowMilliseconds - signedAtSeconds * 1_000L;
        return age >= -FUTURE_TOLERANCE_MILLISECONDS && age <= HOST_FRESH_MILLISECONDS;
    }

    private static int effectivePort(URL url) {
        return url.getPort() >= 0 ? url.getPort() : url.getDefaultPort();
    }
}

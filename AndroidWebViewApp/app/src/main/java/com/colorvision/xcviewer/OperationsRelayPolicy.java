package com.colorvision.xcviewer;

import java.net.URL;

final class OperationsRelayPolicy {
    static final String CAPABILITY_SHOW_WINDOW = "ops.window.show";
    static final String CAPABILITY_MINIMIZE_WINDOW = "ops.window.minimize";
    static final String CAPABILITY_RECOVER_MESSAGE_CHANNEL = "ops.messaging.reconnect";
    static final String CAPABILITY_RESTART_MQTT = "ops.service.restart";
    static final String CAPABILITY_CANCEL_FLOW = "ops.flow.cancel";
    static final String CAPABILITY_RESTART_APPLICATION = "ops.application.restart";
    static final String CAPABILITY_REQUEST_DIAGNOSTICS = "ops.diagnostics.request";
    static final String CAPABILITY_READ_FAILURE_EVIDENCE = "ops.diagnostics.failures.read";
    static final String CAPABILITY_CAPTURE_WINDOW_SNAPSHOT =
            OperationsRemoteWindowSnapshot.CAPABILITY_ID;
    static final long HOST_FRESH_MILLISECONDS = 180_000L;
    private static final long FUTURE_TOLERANCE_MILLISECONDS = 125_000L;

    private OperationsRelayPolicy() {
    }

    static URL fixedBaseUrl() throws Exception {
        URL url = new URL(AppNavigationPolicy.FIXED_SERVICE_ORIGIN);
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

    static boolean isAllowedTaskCapability(String capabilityId) {
        return CAPABILITY_SHOW_WINDOW.equals(capabilityId)
                || CAPABILITY_MINIMIZE_WINDOW.equals(capabilityId)
                || CAPABILITY_RECOVER_MESSAGE_CHANNEL.equals(capabilityId)
                || CAPABILITY_RESTART_MQTT.equals(capabilityId)
                || CAPABILITY_CANCEL_FLOW.equals(capabilityId)
                || CAPABILITY_RESTART_APPLICATION.equals(capabilityId)
                || CAPABILITY_REQUEST_DIAGNOSTICS.equals(capabilityId)
                || CAPABILITY_READ_FAILURE_EVIDENCE.equals(capabilityId)
                || CAPABILITY_CAPTURE_WINDOW_SNAPSHOT.equals(capabilityId);
    }

    static boolean isHostFresh(long signedAtSeconds, long nowMilliseconds) {
        if (signedAtSeconds <= 0) {
            return false;
        }
        long age = nowMilliseconds - signedAtSeconds * 1_000L;
        return age >= -FUTURE_TOLERANCE_MILLISECONDS && age <= HOST_FRESH_MILLISECONDS;
    }

    static boolean canRestartMqttService(
            boolean capabilityAvailable,
            boolean hostFresh,
            boolean flowAvailable,
            boolean flowActive,
            boolean serviceAvailable,
            String serviceStatus,
            boolean maintenanceSupported) {
        return capabilityAvailable
                && hostFresh
                && flowAvailable
                && !flowActive
                && serviceAvailable
                && maintenanceSupported
                && isStableMqttServiceStatus(serviceStatus);
    }

    static boolean canReadFailureEvidence(boolean capabilityAvailable, boolean hostFresh) {
        return capabilityAvailable && hostFresh;
    }

    static boolean canCaptureWindowSnapshot(
            boolean capabilityAvailable, boolean hostFresh, int androidSdk) {
        return capabilityAvailable
                && hostFresh
                && androidSdk >= OperationsRemoteWindowSnapshot.MINIMUM_ANDROID_SDK;
    }

    static int remoteTaskPollingAttempts(String capabilityId) {
        if (CAPABILITY_RESTART_MQTT.equals(capabilityId)) {
            return 61;
        }
        if (CAPABILITY_RESTART_APPLICATION.equals(capabilityId)) {
            return 46;
        }
        if (CAPABILITY_CAPTURE_WINDOW_SNAPSHOT.equals(capabilityId)) {
            return 31;
        }
        return 13;
    }

    static int remoteTaskTtlSeconds(String capabilityId) {
        return CAPABILITY_CAPTURE_WINDOW_SNAPSHOT.equals(capabilityId) ? 300 : 900;
    }

    private static boolean isStableMqttServiceStatus(String status) {
        return "running".equals(status)
                || "stopped".equals(status)
                || "paused".equals(status);
    }

    private static int effectivePort(URL url) {
        return url.getPort() >= 0 ? url.getPort() : url.getDefaultPort();
    }
}

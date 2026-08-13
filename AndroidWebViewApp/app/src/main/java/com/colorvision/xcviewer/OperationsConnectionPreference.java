package com.colorvision.xcviewer;

final class OperationsConnectionPreference {
    static final String DIRECT = "direct";
    static final String RELAY = "relay";

    private OperationsConnectionPreference() {
    }

    static String normalize(String value) {
        return RELAY.equals(value) ? RELAY : DIRECT;
    }

    static boolean prefersRelay(String value) {
        return RELAY.equals(normalize(value));
    }

    static boolean canFallbackAfter(String errorCode) {
        return errorCode == null || !errorCode.contains("unknown_or_revoked_device");
    }
}

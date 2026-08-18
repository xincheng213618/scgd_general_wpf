package com.colorvision.xcviewer;

import java.util.Arrays;
import java.util.HashSet;
import java.util.Set;

final class OperationsDestinationState {
    static final String OVERVIEW = "overview";
    static final String TOOLS = "tools";
    static final String PAIRING = "pairing";
    static final String CONNECTIONS = "connections";
    static final String CONNECTION_CHECK = "connection_check";
    static final String HISTORY = "history";
    static final String FLEET_ALL = "fleet_all";
    static final String FLEET_ISSUES = "fleet_issues";
    static final String TRIAGE = "triage";
    static final String JOBS = "jobs";
    static final String SUPPORT = "support";
    static final String LIVE_MONITOR = "live_monitor";
    static final String CAPABILITY_DETAIL = "capability_detail";

    private static final Set<String> KNOWN_DESTINATIONS = new HashSet<>(Arrays.asList(
            OVERVIEW,
            TOOLS,
            PAIRING,
            CONNECTIONS,
            CONNECTION_CHECK,
            HISTORY,
            FLEET_ALL,
            FLEET_ISSUES,
            TRIAGE,
            JOBS,
            SUPPORT,
            LIVE_MONITOR,
            CAPABILITY_DETAIL));

    private OperationsDestinationState() {
    }

    static String normalize(String value) {
        String normalized = value == null ? "" : value.trim();
        return KNOWN_DESTINATIONS.contains(normalized) ? normalized : OVERVIEW;
    }

    static boolean shouldRestore(String destination) {
        String normalized = normalize(destination);
        return !OVERVIEW.equals(normalized)
                && !PAIRING.equals(normalized)
                && !CAPABILITY_DETAIL.equals(normalized);
    }

    static boolean requiresDirectConnection(String destination) {
        String normalized = normalize(destination);
        return TRIAGE.equals(normalized)
                || TOOLS.equals(normalized)
                || JOBS.equals(normalized)
                || SUPPORT.equals(normalized)
                || LIVE_MONITOR.equals(normalized)
                || CAPABILITY_DETAIL.equals(normalized);
    }

    static boolean isTriage(String destination) {
        return TRIAGE.equals(normalize(destination));
    }

    static boolean shouldSubmitPairingAutomatically(boolean restoring, boolean hasPairingPayload) {
        return hasPairingPayload && !restoring;
    }
}

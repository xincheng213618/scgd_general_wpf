package com.colorvision.xcviewer;

final class OperationsFleetCheckCompletionPolicy {
    private OperationsFleetCheckCompletionPolicy() {
    }

    static Decision decide(
            String requestedDestination,
            String activeHostBefore,
            String activeHostAfter) {
        String destination = OperationsDestinationState.TRIAGE.equals(requestedDestination)
                ? OperationsDestinationState.TRIAGE
                : OperationsDestinationState.CONNECTIONS;
        boolean activeTargetChanged = !normalized(activeHostBefore).equals(
                normalized(activeHostAfter));
        return new Decision(destination, activeTargetChanged);
    }

    private static String normalized(String value) {
        return value == null ? "" : value.trim();
    }

    static final class Decision {
        final String destination;
        final boolean activeTargetChanged;

        Decision(String destination, boolean activeTargetChanged) {
            this.destination = destination;
            this.activeTargetChanged = activeTargetChanged;
        }

        boolean returnsToProblemCenter() {
            return OperationsDestinationState.TRIAGE.equals(destination);
        }
    }
}

package com.colorvision.xcviewer;

final class OperationsWatchCheckRequestPolicy {
    enum Decision {
        IGNORE,
        RUN_NOW,
        RUN_AFTER_CURRENT
    }

    private OperationsWatchCheckRequestPolicy() {
    }

    static Decision decide(boolean monitoring, boolean checkInFlight) {
        if (!monitoring) {
            return Decision.IGNORE;
        }
        return checkInFlight ? Decision.RUN_AFTER_CURRENT : Decision.RUN_NOW;
    }
}

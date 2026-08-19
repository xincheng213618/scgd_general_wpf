package com.colorvision.xcviewer;

final class RuntimePermissionDialogState {
    static final long OBSERVE_DELAY_MILLISECONDS = 250L;
    static final long NO_DIALOG_RECOVERY_DELAY_MILLISECONDS = 800L;

    private boolean inFlight;
    private boolean dialogPresented;
    private int generation;

    int begin() {
        inFlight = true;
        dialogPresented = false;
        return ++generation;
    }

    void observe(
            int requestGeneration,
            boolean permissionGranted,
            boolean windowHasFocus) {
        if (isCurrent(requestGeneration) && !permissionGranted && !windowHasFocus) {
            dialogPresented = true;
        }
    }

    boolean completeFromSystemResult(boolean permissionGranted) {
        if (!inFlight) {
            return true;
        }
        if (permissionGranted || dialogPresented) {
            inFlight = false;
            return true;
        }
        return false;
    }

    boolean shouldRecoverAsBlocked(
            int requestGeneration,
            boolean permissionGranted,
            boolean windowHasFocus) {
        if (!isCurrent(requestGeneration) || permissionGranted) {
            return false;
        }
        if (!windowHasFocus) {
            dialogPresented = true;
            return false;
        }
        if (dialogPresented) {
            inFlight = false;
            return false;
        }
        inFlight = false;
        return true;
    }

    private boolean isCurrent(int requestGeneration) {
        return inFlight && requestGeneration == generation;
    }
}

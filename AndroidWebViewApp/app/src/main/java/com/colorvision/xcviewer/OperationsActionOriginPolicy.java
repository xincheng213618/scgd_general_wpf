package com.colorvision.xcviewer;

final class OperationsActionOriginPolicy {
    private OperationsActionOriginPolicy() {
    }

    static boolean isVisible(
            String originDestination,
            String originDetailPath,
            String currentDestination,
            String currentDetailPath,
            String expectedDetailPath) {
        if (originDestination == null || !originDestination.equals(currentDestination)) {
            return false;
        }
        if (!OperationsDestinationState.CAPABILITY_DETAIL.equals(originDestination)) {
            return true;
        }
        return expectedDetailPath.equals(originDetailPath)
                && originDetailPath.equals(currentDetailPath);
    }

    static boolean matchesRequest(
            int requestGeneration,
            int currentGeneration,
            String originHostId,
            String currentHostId) {
        return requestGeneration == currentGeneration
                && originHostId != null
                && originHostId.equals(currentHostId);
    }
}

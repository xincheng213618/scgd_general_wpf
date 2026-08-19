package com.colorvision.xcviewer;

final class OperationsMessageChannelRecoveryPolicy {
    private OperationsMessageChannelRecoveryPolicy() {
    }

    static boolean isOriginVisible(
            String originDestination,
            String originDetailPath,
            String currentDestination,
            String currentDetailPath,
            String messageChannelPath) {
        if (originDestination == null || !originDestination.equals(currentDestination)) {
            return false;
        }
        if (!OperationsDestinationState.CAPABILITY_DETAIL.equals(originDestination)) {
            return true;
        }
        return messageChannelPath.equals(originDetailPath)
                && originDetailPath.equals(currentDetailPath);
    }
}

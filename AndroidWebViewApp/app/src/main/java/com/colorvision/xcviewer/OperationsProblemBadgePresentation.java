package com.colorvision.xcviewer;

final class OperationsProblemBadgePresentation {
    private OperationsProblemBadgePresentation() {
    }

    static ViewModel create(
            boolean hasStoredProfile,
            String watchState,
            int issueCount,
            int otherComputerProblemCount) {
        if (!hasStoredProfile) {
            return new ViewModel(false, 0, "");
        }
        int boundedIssueCount = Math.max(0, Math.min(999, issueCount));
        int boundedOtherComputerCount = Math.max(
                0, Math.min(OperationsProfileRegistry.MAX_PROFILES - 1,
                        otherComputerProblemCount));
        if (boundedOtherComputerCount > 0) {
            String current = boundedIssueCount > 0
                    ? "当前电脑 " + boundedIssueCount + " 项待复核"
                    : requiresAttention(watchState)
                            ? "当前电脑有待关注状态，"
                                    + OperationsWatchHistory.label(watchState)
                            : "";
            String others = "其他 " + boundedOtherComputerCount + " 台电脑需关注";
            return new ViewModel(
                    true,
                    0,
                    current.isEmpty() ? others : current + "；" + others);
        }
        if (boundedIssueCount > 0) {
            return new ViewModel(
                    true,
                    boundedIssueCount,
                    boundedIssueCount + " 项待复核");
        }
        if (!requiresAttention(watchState)) {
            return new ViewModel(false, 0, "");
        }
        return new ViewModel(
                true,
                0,
                "有待关注状态，" + OperationsWatchHistory.label(watchState));
    }

    private static boolean requiresAttention(String watchState) {
        return !OperationsWatchHistory.attentionKey(watchState).isEmpty()
                || OperationsWatchHistory.STATE_OFFLINE.equals(watchState)
                || OperationsWatchHistory.STATE_REMOTE_WAITING.equals(watchState)
                || OperationsWatchHistory.STATE_REVOKED.equals(watchState);
    }

    static final class ViewModel {
        final boolean visible;
        final int number;
        final String contentDescription;

        ViewModel(boolean visible, int number, String contentDescription) {
            this.visible = visible;
            this.number = number;
            this.contentDescription = contentDescription;
        }
    }
}

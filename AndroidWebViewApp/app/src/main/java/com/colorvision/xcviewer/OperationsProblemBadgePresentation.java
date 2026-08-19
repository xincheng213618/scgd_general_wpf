package com.colorvision.xcviewer;

final class OperationsProblemBadgePresentation {
    private OperationsProblemBadgePresentation() {
    }

    static ViewModel create(boolean hasStoredProfile, String watchState, int issueCount) {
        if (!hasStoredProfile) {
            return new ViewModel(false, 0, "");
        }
        int boundedIssueCount = Math.max(0, Math.min(999, issueCount));
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

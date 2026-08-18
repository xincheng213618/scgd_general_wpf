package com.colorvision.xcviewer;

final class OperationsProblemBadgePresentation {
    private OperationsProblemBadgePresentation() {
    }

    static ViewModel create(boolean hasStoredProfile, String watchState) {
        if (!hasStoredProfile || !requiresAttention(watchState)) {
            return new ViewModel(false, "");
        }
        return new ViewModel(
                true,
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
        final String contentDescription;

        ViewModel(boolean visible, String contentDescription) {
            this.visible = visible;
            this.contentDescription = contentDescription;
        }
    }
}

package com.colorvision.xcviewer;

final class OperationsAttentionNotificationReconciliation {
    private OperationsAttentionNotificationReconciliation() {
    }

    static boolean shouldClear(
            String watchState, OperationsTriagePresentation.ViewModel model) {
        String attentionKey = OperationsAttentionFocus.fromWatchState(watchState);
        if (attentionKey.isEmpty() || model == null) {
            return false;
        }
        boolean reviewedMatch = false;
        for (OperationsTriagePresentation.Finding finding : model.reviewedFindings) {
            reviewedMatch |= OperationsAttentionFocus.matchesFinding(
                    attentionKey, finding.category, finding.severity);
        }
        if (!reviewedMatch) {
            return false;
        }
        for (OperationsTriagePresentation.Finding finding : model.pendingFindings) {
            if (OperationsAttentionFocus.matchesFinding(
                    attentionKey, finding.category, finding.severity)) {
                return false;
            }
        }
        return true;
    }

    static boolean shouldClear(
            String watchState, OperationsRemoteProblemsPresentation.ViewModel model) {
        String attentionKey = OperationsAttentionFocus.fromWatchState(watchState);
        if (attentionKey.isEmpty() || model == null || !model.snapshotAvailable) {
            return false;
        }
        boolean reviewedMatch = false;
        for (OperationsRemoteProblemsPresentation.Issue issue : model.reviewedIssues) {
            reviewedMatch |= OperationsAttentionFocus.matchesRemoteSection(
                    attentionKey, issue.section);
        }
        if (!reviewedMatch) {
            return false;
        }
        for (OperationsRemoteProblemsPresentation.Issue issue : model.pendingIssues) {
            if (OperationsAttentionFocus.matchesRemoteSection(
                    attentionKey, issue.section)) {
                return false;
            }
        }
        return true;
    }
}

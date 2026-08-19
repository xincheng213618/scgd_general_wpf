package com.colorvision.xcviewer;

final class OperationsConnectionPresentation {
    private OperationsConnectionPresentation() {
    }

    static ViewModel from(
            String activeProfileLabel,
            String preferredChannel,
            OperationsConnectionOverviewProbe.Result result,
            int profileCount,
            int maximumProfiles) {
        int safeMaximum = Math.max(0, maximumProfiles);
        int safeCount = Math.min(Math.max(0, profileCount), safeMaximum);
        return new ViewModel(
                safe(activeProfileLabel, "未命名电脑"),
                OperationsConnectionOverview.activeChannelLabel(result),
                safe(preferredChannel, "正在确认"),
                safeCount + " / " + safeMaximum);
    }

    private static String safe(String value, String fallback) {
        return value == null || value.trim().isEmpty() ? fallback : value.trim();
    }

    static final class ViewModel {
        final String computerLabel;
        final String activeChannelLabel;
        final String preferredChannelLabel;
        final String pairedComputersLabel;

        ViewModel(
                String computerLabel,
                String activeChannelLabel,
                String preferredChannelLabel,
                String pairedComputersLabel) {
            this.computerLabel = computerLabel;
            this.activeChannelLabel = activeChannelLabel;
            this.preferredChannelLabel = preferredChannelLabel;
            this.pairedComputersLabel = pairedComputersLabel;
        }
    }
}

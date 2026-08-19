package com.colorvision.xcviewer;

final class AppResponsiveLayout {
    private static final int COMPACT_WIDTH_MAX_DP = 599;
    private static final float SINGLE_COLUMN_FONT_SCALE = 1.2f;
    private static final float STACKED_CONTROL_EFFECTIVE_WIDTH_DP = 220f;

    private AppResponsiveLayout() {
    }

    static boolean usesSingleColumn(int screenWidthDp, float fontScale) {
        boolean compactWidth = screenWidthDp > 0 && screenWidthDp <= COMPACT_WIDTH_MAX_DP;
        boolean largeFont = usesLargeFont(fontScale);
        return compactWidth || largeFont;
    }

    static boolean usesNavigationRail(int screenWidthDp) {
        return screenWidthDp > COMPACT_WIDTH_MAX_DP;
    }

    static boolean usesStackedControlRow(int screenWidthDp, float fontScale) {
        return screenWidthDp > 0
                && Float.isFinite(fontScale)
                && fontScale > 0f
                && screenWidthDp / fontScale <= STACKED_CONTROL_EFFECTIVE_WIDTH_DP;
    }

    private static boolean usesLargeFont(float fontScale) {
        return Float.isFinite(fontScale) && fontScale >= SINGLE_COLUMN_FONT_SCALE;
    }
}

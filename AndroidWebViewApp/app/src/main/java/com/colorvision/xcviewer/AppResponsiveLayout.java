package com.colorvision.xcviewer;

final class AppResponsiveLayout {
    private static final int COMPACT_WIDTH_MAX_DP = 599;
    private static final float SINGLE_COLUMN_FONT_SCALE = 1.2f;

    private AppResponsiveLayout() {
    }

    static boolean usesSingleColumn(int screenWidthDp, float fontScale) {
        boolean compactWidth = screenWidthDp > 0 && screenWidthDp <= COMPACT_WIDTH_MAX_DP;
        boolean largeFont = usesLargeFont(fontScale);
        return compactWidth || largeFont;
    }

    private static boolean usesLargeFont(float fontScale) {
        return Float.isFinite(fontScale) && fontScale >= SINGLE_COLUMN_FONT_SCALE;
    }
}

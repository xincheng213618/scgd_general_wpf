package com.colorvision.xcviewer;

final class AppResponsiveLayout {
    private static final int COMPACT_WIDTH_MAX_DP = 599;
    private static final int DASHBOARD_SHORTCUT_GRID_MIN_DP = 360;
    private static final float SINGLE_COLUMN_FONT_SCALE = 1.2f;

    private AppResponsiveLayout() {
    }

    static boolean usesSingleColumn(int screenWidthDp, float fontScale) {
        boolean compactWidth = screenWidthDp > 0 && screenWidthDp <= COMPACT_WIDTH_MAX_DP;
        boolean largeFont = usesLargeFont(fontScale);
        return compactWidth || largeFont;
    }

    static boolean usesSingleColumnDashboardShortcuts(int screenWidthDp, float fontScale) {
        boolean narrowWidth = screenWidthDp > 0
                && screenWidthDp < DASHBOARD_SHORTCUT_GRID_MIN_DP;
        return narrowWidth || usesLargeFont(fontScale);
    }

    private static boolean usesLargeFont(float fontScale) {
        return Float.isFinite(fontScale) && fontScale >= SINGLE_COLUMN_FONT_SCALE;
    }
}

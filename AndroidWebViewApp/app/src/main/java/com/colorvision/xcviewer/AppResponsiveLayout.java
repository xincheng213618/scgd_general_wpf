package com.colorvision.xcviewer;

final class AppResponsiveLayout {
    private static final float SINGLE_COLUMN_FONT_SCALE = 1.2f;

    private AppResponsiveLayout() {
    }

    static boolean usesSingleColumn(float fontScale) {
        return Float.isFinite(fontScale) && fontScale >= SINGLE_COLUMN_FONT_SCALE;
    }
}

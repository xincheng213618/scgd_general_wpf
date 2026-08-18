package com.colorvision.xcviewer;

final class OperationsResponsiveLayout {
    private static final float SINGLE_COLUMN_FONT_SCALE = 1.2f;

    private OperationsResponsiveLayout() {
    }

    static boolean usesSingleColumn(float fontScale) {
        return Float.isFinite(fontScale) && fontScale >= SINGLE_COLUMN_FONT_SCALE;
    }
}

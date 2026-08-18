package com.colorvision.xcviewer;

final class SettingsRowAccessibility {
    private SettingsRowAccessibility() {
    }

    static String contentDescription(String label, String value) {
        String normalizedLabel = label == null ? "" : label.trim();
        String normalizedValue = value == null ? "" : value.trim();
        if (normalizedLabel.isEmpty()) {
            return normalizedValue;
        }
        if (normalizedValue.isEmpty()) {
            return normalizedLabel;
        }
        return normalizedLabel + "，" + normalizedValue;
    }
}

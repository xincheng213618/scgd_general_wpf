package com.colorvision.xcviewer;

final class OperationsTargetPolicy {
    private OperationsTargetPolicy() {
    }

    static boolean isSameTarget(String expectedHostId, String activeHostId) {
        return expectedHostId != null
                && !expectedHostId.isEmpty()
                && expectedHostId.equals(activeHostId);
    }

    static String confirmationMessage(String label, String body) {
        return "操作目标：" + displayLabel(label) + "\n\n" + body;
    }

    static String watchNotificationTitle(String label) {
        return "ColorVision · " + displayLabel(label);
    }

    static String attentionNotificationTitle(String label) {
        return displayLabel(label) + " 需要关注";
    }

    private static String displayLabel(String label) {
        return label == null || label.isEmpty() ? "当前电脑" : label;
    }
}

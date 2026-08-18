package com.colorvision.xcviewer;

final class PairingSuccessPresentation {
    private PairingSuccessPresentation() {
    }

    static String message(boolean existingProfile, String profileLabel) {
        return (existingProfile ? "已更新安全配对" : "已安全配对")
                + " · 当前电脑：" + displayLabel(profileLabel);
    }

    static String renameAction() {
        return "命名电脑";
    }

    private static String displayLabel(String profileLabel) {
        String value = profileLabel == null ? "" : profileLabel.trim();
        return value.isEmpty() ? "这台电脑" : value;
    }
}

package com.colorvision.xcviewer;

final class QrScanFailurePresentation {
    static final String CAMERA_PERMISSION_DENIED = "camera_permission_denied";
    static final String CAMERA_PERMISSION_BLOCKED = "camera_permission_blocked";
    static final String CAMERA_UNAVAILABLE = "camera_unavailable";

    private QrScanFailurePresentation() {
    }

    static String title(String reason) {
        if (CAMERA_PERMISSION_BLOCKED.equals(reason)) {
            return "请在系统设置开启相机";
        }
        if (CAMERA_UNAVAILABLE.equals(reason)) {
            return "相机暂时无法使用";
        }
        return "需要相机权限";
    }

    static String message(String reason) {
        if (CAMERA_PERMISSION_BLOCKED.equals(reason)) {
            return "系统已停止再次询问。请在应用设置的“权限”中允许相机，然后返回继续扫码。相机只用于读取二维码，不会保存画面。";
        }
        if (CAMERA_UNAVAILABLE.equals(reason)) {
            return "请确认没有其他应用占用相机后重试。相机只用于读取二维码；已配对电脑不受影响。";
        }
        return "相机只用于读取电脑端二维码，不会拍照、录制或保存画面。没有此权限仍可使用已配对电脑。";
    }

    static String primaryAction(String reason) {
        return CAMERA_PERMISSION_BLOCKED.equals(reason) ? "打开系统设置" : "重新扫描";
    }

    static boolean opensSystemSettings(String reason) {
        return CAMERA_PERMISSION_BLOCKED.equals(reason);
    }
}

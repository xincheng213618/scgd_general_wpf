package com.colorvision.xcviewer;

final class PairingApprovalPresentation {
    private PairingApprovalPresentation() {
    }

    static String waitingState(int remainingSeconds) {
        return "第 2 步（共 2 步） · 等待电脑端批准\n自动检查剩余 "
                + PairingApprovalWaitPolicy.formatCountdown(remainingSeconds);
    }

    static String waitingDetails(String deviceName) {
        return "在电脑端打开“设置 > 局域网控制”，在“待批准设备”中选择“"
                + displayDeviceName(deviceName)
                + "”，再点击“批准受控运维权限”。\n\n"
                + "手机会每 2 秒安全检查一次。设备私钥只保存在 Android Keystore；"
                + "已提交的设备证明不会因上方自动检查结束而丢失。";
    }

    static String timeoutDetails(String deviceName) {
        return "电脑端的待批准记录仍然保留。请在“设置 > 局域网控制 > 待批准设备”中选择“"
                + displayDeviceName(deviceName)
                + "”并批准，然后继续自动检查；无需刷新二维码或重新创建设备密钥。";
    }

    private static String displayDeviceName(String deviceName) {
        String value = deviceName == null ? "这台手机" : deviceName.trim();
        return value.isEmpty() ? "这台手机" : value;
    }
}

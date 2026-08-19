package com.colorvision.xcviewer;

final class PairingHelpPresentation {
    private PairingHelpPresentation() {
    }

    static String title() {
        return "在电脑上打开配对码";
    }

    static String message() {
        return "1. 打开 ColorVision 设置。\n"
                + "2. 进入“局域网控制”。\n"
                + "3. 开启“现场运维伴侣”，点击“刷新配对码”。\n\n"
                + "配对码两分钟失效且只能提交一次。扫描后还需在电脑端批准这台手机。";
    }
}

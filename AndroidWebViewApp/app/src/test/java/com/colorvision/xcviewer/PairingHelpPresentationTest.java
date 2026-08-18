package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertTrue;

public class PairingHelpPresentationTest {
    @Test
    public void helpNamesTheDesktopRouteAndPairingSafetyBoundary() {
        String message = PairingHelpPresentation.message();

        assertTrue(message.contains("ColorVision 设置"));
        assertTrue(message.contains("局域网控制"));
        assertTrue(message.contains("现场运维伴侣"));
        assertTrue(message.contains("两分钟"));
        assertTrue(message.contains("只能提交一次"));
        assertTrue(message.contains("电脑端批准"));
    }
}

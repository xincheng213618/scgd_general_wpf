package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

public class PairingApprovalPresentationTest {
    @Test
    public void waitingCopyNamesTheExactDesktopApprovalPath() {
        String details = PairingApprovalPresentation.waitingDetails("Xiaomi 15");

        assertTrue(details.contains("设置 > 局域网控制"));
        assertTrue(details.contains("待批准设备"));
        assertTrue(details.contains("批准受控运维权限"));
        assertTrue(details.contains("Xiaomi 15"));
    }

    @Test
    public void countdownIsDescribedAsAutomaticCheckingNotClaimExpiry() {
        String state = PairingApprovalPresentation.waitingState(73);
        String details = PairingApprovalPresentation.waitingDetails("Phone");

        assertTrue(state.contains("自动检查剩余 01:13"));
        assertTrue(details.contains("不会因上方自动检查结束而丢失"));
    }

    @Test
    public void timeoutKeepsTheSubmittedProofRecoverable() {
        String details = PairingApprovalPresentation.timeoutDetails("");

        assertTrue(details.contains("待批准记录仍然保留"));
        assertTrue(details.contains("再检查 2 分钟"));
        assertTrue(details.contains("无需刷新二维码"));
        assertTrue(details.contains("这台手机"));
    }

    @Test
    public void waitingActionsMakePauseAndRetryDurationExplicit() {
        assertEquals("暂停自动检查", PairingApprovalPresentation.pauseAction());
        assertEquals("再检查 2 分钟", PairingApprovalPresentation.retryAction());
    }
}

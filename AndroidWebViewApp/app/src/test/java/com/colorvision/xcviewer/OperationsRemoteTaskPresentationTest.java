package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsRemoteTaskPresentationTest {
    @Test
    public void completedCapabilitiesUseSpecificOutcomeText() {
        assertCompletedState(OperationsRelayPolicy.CAPABILITY_SHOW_WINDOW, "电脑主窗口已显示");
        assertCompletedState(OperationsRelayPolicy.CAPABILITY_MINIMIZE_WINDOW, "电脑主窗口已最小化");
        assertCompletedState(OperationsRelayPolicy.CAPABILITY_RECOVER_MESSAGE_CHANNEL, "电脑消息通道已就绪");
        assertCompletedState(OperationsRelayPolicy.CAPABILITY_RESTART_MQTT, "MQTT 消息服务已远程重启");
        assertCompletedState(OperationsRelayPolicy.CAPABILITY_RESTART_APPLICATION, "ColorVision 已远程重启并重新上线");
    }

    @Test
    public void evidenceCompletionPreservesValidatedFormattedResult() {
        OperationsRemoteTaskPresentation.Presentation result =
                OperationsRemoteTaskPresentation.create(
                        OperationsRelayPolicy.CAPABILITY_READ_FAILURE_EVIDENCE,
                        "completed",
                        "近七天：崩溃 0 · 卡死 1");

        assertEquals("崩溃与卡死线索已刷新", result.state);
        assertEquals("近七天：崩溃 0 · 卡死 1", result.details);
        assertFalse(result.clearFlowCancelAvailability);
    }

    @Test
    public void rejectedActionsExplainTheExactOperationThatDidNotRun() {
        assertFailedState(OperationsRelayPolicy.CAPABILITY_SHOW_WINDOW, "电脑主窗口未显示");
        assertFailedState(OperationsRelayPolicy.CAPABILITY_MINIMIZE_WINDOW, "电脑主窗口未最小化");
        assertFailedState(OperationsRelayPolicy.CAPABILITY_RECOVER_MESSAGE_CHANNEL, "电脑消息通道未恢复");
        assertFailedState(OperationsRelayPolicy.CAPABILITY_RESTART_MQTT, "电脑端未执行 MQTT 重启");
        assertFailedState(OperationsRelayPolicy.CAPABILITY_RESTART_APPLICATION, "ColorVision 未完成重启");
        assertFailedState(OperationsRelayPolicy.CAPABILITY_REQUEST_DIAGNOSTICS, "电脑未生成诊断包");
    }

    @Test
    public void cancelAvailabilityClearsOnlyAfterTerminalCancelReceipt() {
        OperationsRemoteTaskPresentation.Presentation completed =
                OperationsRemoteTaskPresentation.create(
                        OperationsRelayPolicy.CAPABILITY_CANCEL_FLOW, "completed", "");
        OperationsRemoteTaskPresentation.Presentation rejected =
                OperationsRemoteTaskPresentation.create(
                        OperationsRelayPolicy.CAPABILITY_CANCEL_FLOW, "rejected", "");
        OperationsRemoteTaskPresentation.Presentation accepted =
                OperationsRemoteTaskPresentation.create(
                        OperationsRelayPolicy.CAPABILITY_CANCEL_FLOW, "accepted", "");

        assertTrue(completed.clearFlowCancelAvailability);
        assertTrue(rejected.clearFlowCancelAvailability);
        assertFalse(accepted.clearFlowCancelAvailability);
        assertEquals("当前检测未取消", rejected.state);
    }

    @Test
    public void acceptedRestartStatesRemainDistinctFromCompletion() {
        OperationsRemoteTaskPresentation.Presentation application =
                OperationsRemoteTaskPresentation.create(
                        OperationsRelayPolicy.CAPABILITY_RESTART_APPLICATION, "accepted", "");
        OperationsRemoteTaskPresentation.Presentation mqtt =
                OperationsRemoteTaskPresentation.create(
                        OperationsRelayPolicy.CAPABILITY_RESTART_MQTT, "accepted", "");

        assertEquals("重启已受理，等待电脑重新上线", application.state);
        assertEquals("MQTT 重启已受理，等待服务恢复", mqtt.state);
    }

    @Test
    public void consentExpiredAndUnknownStatusesUseSafeBoundedFallbacks() {
        assertEquals("诊断请求已到达电脑",
                OperationsRemoteTaskPresentation.create(
                        OperationsRelayPolicy.CAPABILITY_REQUEST_DIAGNOSTICS,
                        "awaiting_local_consent",
                        "").state);
        assertEquals("远程请求已过期",
                OperationsRemoteTaskPresentation.create(
                        OperationsRelayPolicy.CAPABILITY_SHOW_WINDOW, "expired", "").state);
        assertEquals("远程请求已安全排队",
                OperationsRemoteTaskPresentation.create("unknown", "unexpected", "").state);
    }

    private static void assertCompletedState(String capability, String expected) {
        OperationsRemoteTaskPresentation.Presentation result =
                OperationsRemoteTaskPresentation.create(capability, "completed", "");
        assertEquals(expected, result.state);
    }

    private static void assertFailedState(String capability, String expected) {
        OperationsRemoteTaskPresentation.Presentation result =
                OperationsRemoteTaskPresentation.create(capability, "rejected", "");
        assertEquals(expected, result.state);
    }
}

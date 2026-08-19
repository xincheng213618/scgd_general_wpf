package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsDirectToolboxPresentationTest {
    @Test
    public void unknownLiveStateKeepsStateChangingActionsClosed() {
        OperationsDirectToolboxPresentation.ViewModel model =
                OperationsDirectToolboxPresentation.from(null, true);

        assertEquals(16, model.toolbox.enabledActionCount());
        assertTrue(model.stateLabel.contains("正在核对运行状态"));
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_CANCEL_FLOW).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RESTART_MQTT).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RESTART_APPLICATION).enabled);
    }

    @Test
    public void healthyIdleStateOnlyEnablesApplicableRestartActions() throws Exception {
        OperationsDirectToolboxPresentation.ViewModel model =
                OperationsDirectToolboxPresentation.from(monitor(false, false), false);

        assertEquals(18, model.toolbox.enabledActionCount());
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_CANCEL_FLOW).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE).enabled);
        assertTrue(action(model, OperationsToolboxPresentation.ACTION_RESTART_MQTT).enabled);
        assertTrue(action(model, OperationsToolboxPresentation.ACTION_RESTART_APPLICATION).enabled);
        assertTrue(action(model, OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE)
                .summary.contains("当前健康"));
    }

    @Test
    public void activeFlowAndDegradedMessageInvertTheAvailableActions() throws Exception {
        OperationsDirectToolboxPresentation.ViewModel model =
                OperationsDirectToolboxPresentation.from(monitor(true, true), false);

        assertEquals(18, model.toolbox.enabledActionCount());
        assertTrue(action(model, OperationsToolboxPresentation.ACTION_CANCEL_FLOW).enabled);
        assertTrue(action(model, OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RESTART_MQTT).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RESTART_APPLICATION).enabled);
        assertTrue(action(model, OperationsToolboxPresentation.ACTION_RESTART_MQTT)
                .summary.contains("主检测运行时"));
    }

    @Test
    public void unconfiguredMessageChannelDoesNotOfferARecoveryThatCannotWork()
            throws Exception {
        JSONObject monitor = monitor(false, true);
        monitor.getJSONObject("messageChannel").put("state", "unconfigured");

        OperationsDirectToolboxPresentation.ViewModel model =
                OperationsDirectToolboxPresentation.from(monitor, false);

        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE).enabled);
    }

    private static JSONObject monitor(boolean flowActive, boolean messageAttention)
            throws Exception {
        return new JSONObject()
                .put("flow", new JSONObject()
                        .put("available", true)
                        .put("isActive", flowActive)
                        .put("cancelAvailable", flowActive))
                .put("messageChannel", new JSONObject()
                        .put("available", true)
                        .put("state", messageAttention ? "degraded" : "connected")
                        .put("attentionRequired", messageAttention))
                .put("mqttService", new JSONObject()
                        .put("available", true)
                        .put("status", "running")
                        .put("maintenanceSupported", true));
    }

    private static OperationsToolboxPresentation.Action action(
            OperationsDirectToolboxPresentation.ViewModel model,
            String actionId) {
        for (OperationsToolboxPresentation.Section section : model.toolbox.sections) {
            for (OperationsToolboxPresentation.Action action : section.actions) {
                if (actionId.equals(action.actionId)) {
                    return action;
                }
            }
        }
        throw new AssertionError("Missing action: " + actionId);
    }
}

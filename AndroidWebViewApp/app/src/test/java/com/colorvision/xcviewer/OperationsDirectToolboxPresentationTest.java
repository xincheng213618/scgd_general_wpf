package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsDirectToolboxPresentationTest {
    @Test
    public void unknownCatalogAndLiveStateOnlyKeepsPhoneLocalTools() {
        OperationsDirectToolboxPresentation.ViewModel model =
                OperationsDirectToolboxPresentation.from(null, null, true);

        assertEquals(2, model.toolbox.enabledActionCount());
        assertTrue(model.stateLabel.contains("正在核对电脑能力"));
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_CANCEL_FLOW).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RESTART_MQTT).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RESTART_APPLICATION).enabled);
    }

    @Test
    public void knownCapabilitiesWithoutLiveStateOnlyClosesStateDependentActions()
            throws Exception {
        OperationsDirectToolboxPresentation.ViewModel model =
                OperationsDirectToolboxPresentation.from(null, capabilities(), true);

        assertEquals(16, model.toolbox.enabledActionCount());
        assertTrue(model.stateLabel.contains("电脑能力已核对"));
        assertTrue(action(model, OperationsToolboxPresentation.ACTION_LIVE_MONITOR).enabled);
        assertTrue(action(model, OperationsToolboxPresentation.ACTION_SHOW_WINDOW).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_CANCEL_FLOW).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RESTART_MQTT).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RESTART_APPLICATION).enabled);
    }

    @Test
    public void healthyIdleStateOnlyEnablesApplicableRestartActions() throws Exception {
        OperationsDirectToolboxPresentation.ViewModel model =
                OperationsDirectToolboxPresentation.from(
                        monitor(false, false), capabilities(), false);

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
                OperationsDirectToolboxPresentation.from(
                        monitor(true, true), capabilities(), false);

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
                OperationsDirectToolboxPresentation.from(monitor, capabilities(), false);

        assertFalse(action(model, OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE).enabled);
    }

    @Test
    public void unavailableCatalogOnlyKeepsPhoneLocalRecoveryTools() throws Exception {
        OperationsDirectToolboxPresentation.ViewModel model =
                OperationsDirectToolboxPresentation.from(monitor(false, false), null, false);

        assertEquals(2, model.toolbox.enabledActionCount());
        assertTrue(action(model, OperationsToolboxPresentation.ACTION_CONNECTION_CHECK).enabled);
        assertTrue(action(model, OperationsToolboxPresentation.ACTION_TIMELINE).enabled);
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_LIVE_MONITOR).enabled);
        assertTrue(model.stateLabel.contains("能力目录不可用"));
    }

    @Test
    public void unavailableComputerCapabilityClosesOnlyItsTool() throws Exception {
        JSONObject capabilities = capabilities();
        JSONArray entries = capabilities.getJSONArray("capabilities");
        for (int index = 0; index < entries.length(); index++) {
            JSONObject entry = entries.getJSONObject(index);
            if ("ops.window.show".equals(entry.optString("id"))) {
                entry.put("available", false);
            }
        }

        OperationsDirectToolboxPresentation.ViewModel model =
                OperationsDirectToolboxPresentation.from(
                        monitor(false, false), capabilities, false);

        assertEquals(17, model.toolbox.enabledActionCount());
        assertFalse(action(model, OperationsToolboxPresentation.ACTION_SHOW_WINDOW).enabled);
        assertTrue(action(model, OperationsToolboxPresentation.ACTION_MINIMIZE_WINDOW).enabled);
        assertTrue(action(model, OperationsToolboxPresentation.ACTION_SHOW_WINDOW)
                .summary.contains("未开放"));
    }

    @Test
    public void compoundSummaryToolRequiresEverySourceCapability() throws Exception {
        JSONObject capabilities = capabilities();
        JSONArray entries = capabilities.getJSONArray("capabilities");
        for (int index = 0; index < entries.length(); index++) {
            JSONObject entry = entries.getJSONObject(index);
            if ("ops.diagnostics.performance.read".equals(entry.optString("id"))) {
                entry.put("discoverableOn", new JSONArray().put("desktop"));
            }
        }

        OperationsDirectToolboxPresentation.ViewModel model =
                OperationsDirectToolboxPresentation.from(
                        monitor(false, false), capabilities, false);

        assertFalse(action(model, OperationsToolboxPresentation.ACTION_SHARE_SUMMARY).enabled);
        assertTrue(action(model, OperationsToolboxPresentation.ACTION_RECENT_EVENTS).enabled);
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

    private static JSONObject capabilities() throws Exception {
        String[] ids = {
                "ops.status.read",
                "ops.diagnostics.events.read",
                "ops.diagnostics.performance.read",
                "ops.flow.runtime.read",
                "ops.flow.cancel",
                "ops.monitor.read",
                "ops.audit.read",
                "ops.services.health.read",
                "ops.devices.health.read",
                "ops.messaging.health.read",
                "ops.window.show",
                "ops.jobs.manage",
                "ops.deployment.receipt.create",
                "ops.support.session.request",
                "ops.support.message.exchange",
                "ops.window.minimize",
                "ops.window.snapshot.capture",
                "ops.window.snapshot.download",
                "ops.diagnostics.bundle.create",
                "ops.diagnostics.bundle.download",
                "ops.application.restart",
                "ops.messaging.reconnect",
                "ops.diagnostics.failures.read",
                "ops.service.restart"
        };
        JSONArray values = new JSONArray();
        for (String id : ids) {
            values.put(new JSONObject()
                    .put("id", id)
                    .put("available", true)
                    .put("discoverableOn", new JSONArray().put("android")));
        }
        return new JSONObject().put("capabilities", values);
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

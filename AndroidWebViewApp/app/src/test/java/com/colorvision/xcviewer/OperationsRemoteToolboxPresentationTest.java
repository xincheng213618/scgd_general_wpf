package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsRemoteToolboxPresentationTest {
    private static final long NOW = 1_900_000_000_000L;

    @Test
    public void freshIdleComputerExposesSafeRemoteToolsWithoutEnablingCancel() throws Exception {
        OperationsRemoteToolboxPresentation.ViewModel model =
                OperationsRemoteToolboxPresentation.from(
                        response(false, false), true, 31, NOW);

        assertTrue(model.hostFresh);
        assertEquals(4, model.toolbox.sections.size());
        assertEquals("窗口与检测", model.toolbox.sections.get(0).title);
        assertEquals("恢复", model.toolbox.sections.get(1).title);
        assertEquals("诊断与取证", model.toolbox.sections.get(2).title);
        assertEquals("记录", model.toolbox.sections.get(3).title);
        assertEquals("窗口检测", model.toolbox.sections.get(0).shortcutLabel());
        assertEquals("跳到诊断与取证分组",
                model.toolbox.sections.get(2).shortcutAccessibilityLabel());
        assertEquals(11, model.toolbox.actionCount());
        assertEquals(10, model.toolbox.enabledActionCount());
        assertTrue(model.toolbox.hasUniqueActionIds());
        assertFalse(find(model, OperationsToolboxPresentation.ACTION_CANCEL_FLOW).enabled);
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_RESTART_MQTT).enabled);
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_RESTART_APPLICATION).enabled);
        assertEquals("10 / 11 项可用 · 电脑签名状态已核验", model.compactStateLabel);
    }

    @Test
    public void activeCancellableFlowSwapsCancelForRestartActions() throws Exception {
        OperationsRemoteToolboxPresentation.ViewModel model =
                OperationsRemoteToolboxPresentation.from(
                        response(true, true), true, 31, NOW);

        assertTrue(find(model, OperationsToolboxPresentation.ACTION_CANCEL_FLOW).enabled);
        assertFalse(find(model, OperationsToolboxPresentation.ACTION_RESTART_MQTT).enabled);
        assertFalse(find(model,
                OperationsToolboxPresentation.ACTION_RESTART_APPLICATION).enabled);
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE).enabled);
        assertEquals(9, model.toolbox.enabledActionCount());
    }

    @Test
    public void staleComputerKeepsOnlyQueueableDiagnosticsAndLocalTimeline() throws Exception {
        JSONObject response = response(false, false);
        response.getJSONObject("host").put("signedAt",
                (NOW - OperationsRelayPolicy.HOST_FRESH_MILLISECONDS - 1_000L) / 1_000L);

        OperationsRemoteToolboxPresentation.ViewModel model =
                OperationsRemoteToolboxPresentation.from(response, false, 31, NOW);

        assertFalse(model.hostFresh);
        assertEquals(2, model.toolbox.enabledActionCount());
        assertTrue(find(model,
                OperationsToolboxPresentation.ACTION_CREATE_DIAGNOSTIC).enabled);
        assertEquals("排队请求诊断", find(model,
                OperationsToolboxPresentation.ACTION_CREATE_DIAGNOSTIC).title);
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_TIMELINE).enabled);
        assertFalse(find(model, OperationsToolboxPresentation.ACTION_SHOW_WINDOW).enabled);
        assertFalse(find(model,
                OperationsRemoteToolboxPresentation.ACTION_RECENT_REMOTE_TASK).enabled);
        assertTrue(model.summary.contains("窗口、恢复与取证操作已暂停"));
        assertEquals("2 / 11 项可用 · 仅显示安全可用项", model.compactStateLabel);
    }

    @Test
    public void unsupportedAndroidKeepsEncryptedSnapshotDisabled() throws Exception {
        OperationsRemoteToolboxPresentation.ViewModel model =
                OperationsRemoteToolboxPresentation.from(
                        response(false, false), true, 30, NOW);
        OperationsToolboxPresentation.Action snapshot = find(
                model, OperationsToolboxPresentation.ACTION_CREATE_SNAPSHOT);

        assertFalse(snapshot.enabled);
        assertTrue(snapshot.summary.contains("Android 12"));
        assertTrue(snapshot.accessibilityLabel().endsWith("当前不可用"));
    }

    @Test
    public void missingRelaySnapshotDoesNotExposeStateChangingOperations() {
        OperationsRemoteToolboxPresentation.ViewModel model =
                OperationsRemoteToolboxPresentation.from(null, false, 31, NOW);

        assertFalse(model.hostFresh);
        assertEquals(1, model.toolbox.enabledActionCount());
        assertEquals("1 / 11 项可用 · 仅显示安全可用项", model.compactStateLabel);
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_TIMELINE).enabled);
        assertFalse(find(model, OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE).enabled);
        assertFalse(find(model, OperationsToolboxPresentation.ACTION_CREATE_DIAGNOSTIC).enabled);
    }

    private static JSONObject response(boolean flowActive, boolean cancelAvailable)
            throws Exception {
        JSONArray capabilities = new JSONArray()
                .put(OperationsRelayPolicy.CAPABILITY_SHOW_WINDOW)
                .put(OperationsRelayPolicy.CAPABILITY_MINIMIZE_WINDOW)
                .put(OperationsRelayPolicy.CAPABILITY_CANCEL_FLOW)
                .put(OperationsRelayPolicy.CAPABILITY_RECOVER_MESSAGE_CHANNEL)
                .put(OperationsRelayPolicy.CAPABILITY_RESTART_MQTT)
                .put(OperationsRelayPolicy.CAPABILITY_RESTART_APPLICATION)
                .put(OperationsRelayPolicy.CAPABILITY_REQUEST_DIAGNOSTICS)
                .put(OperationsRelayPolicy.CAPABILITY_READ_FAILURE_EVIDENCE)
                .put(OperationsRelayPolicy.CAPABILITY_CAPTURE_WINDOW_SNAPSHOT);
        JSONObject monitor = new JSONObject()
                .put("flow", new JSONObject()
                        .put("available", true)
                        .put("isActive", flowActive)
                        .put("cancelAvailable", cancelAvailable))
                .put("mqttService", new JSONObject()
                        .put("available", true)
                        .put("status", "running")
                        .put("maintenanceSupported", true));
        return new JSONObject().put("host", new JSONObject()
                .put("signedAt", NOW / 1_000L)
                .put("capabilities", capabilities)
                .put("snapshot", new JSONObject().put("monitor", monitor)));
    }

    private static OperationsToolboxPresentation.Action find(
            OperationsRemoteToolboxPresentation.ViewModel model,
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

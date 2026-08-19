package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import java.util.Arrays;
import java.util.List;
import java.util.stream.Collectors;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsRemoteProblemsPresentationTest {
    @Test
    public void freshSignedSnapshotShowsOnlyAttentionItemsInOperationalPriority() throws Exception {
        JSONObject monitor = completeMonitor()
                .put("flow", new JSONObject()
                        .put("available", true)
                        .put("isActive", true)
                        .put("phase", "paused"))
                .put("devices", new JSONObject()
                        .put("available", true)
                        .put("hasConfiguredDevices", true)
                        .put("readyCount", 2)
                        .put("attentionCount", 1)
                        .put("totalCount", 3)
                        .put("offlineCount", 1))
                .put("messageChannel", new JSONObject()
                        .put("available", true)
                        .put("connected", false)
                        .put("subscriptionReady", false)
                        .put("activeSubscriptionCount", 0)
                        .put("registeredSubscriptionCount", 4))
                .put("alerts", new JSONObject()
                        .put("warningCount", 2)
                        .put("errorCount", 1)
                        .put("criticalCount", 1))
                .put("performance", new JSONObject()
                        .put("cpuPercent", 18.4)
                        .put("mainUi", new JSONObject().put("state", "unresponsive")))
                .put("applicationRecovery", new JSONObject()
                        .put("supported", true)
                        .put("registered", false)
                        .put("automaticWatchdogActive", false));

        OperationsRemoteProblemsPresentation.ViewModel model =
                OperationsRemoteProblemsPresentation.from(monitor, true);

        assertTrue(model.snapshotAvailable);
        assertEquals("6 项需要关注", model.stateLabel);
        assertEquals(0, model.incompleteCount);
        assertEquals(
                Arrays.asList("performance", "alerts", "message", "devices", "flow", "recovery"),
                sections(model));
        assertEquals("性能，CPU 18% · 无响应，查看电脑签名详情",
                model.issues.get(0).accessibilityLabel());
    }

    @Test
    public void healthyCompleteSnapshotDoesNotInventProblems() throws Exception {
        OperationsRemoteProblemsPresentation.ViewModel model =
                OperationsRemoteProblemsPresentation.from(completeMonitor(), true);

        assertTrue(model.snapshotAvailable);
        assertTrue(model.issues.isEmpty());
        assertEquals(0, model.incompleteCount);
        assertEquals("未发现需要关注项目", model.stateLabel);
        assertTrue(model.summary.contains("只读快照"));
    }

    @Test
    public void incompleteSnapshotDoesNotClaimEverythingIsHealthy() throws Exception {
        JSONObject monitor = completeMonitor();
        monitor.remove("performance");
        monitor.getJSONObject("messageChannel").put("available", false);

        OperationsRemoteProblemsPresentation.ViewModel model =
                OperationsRemoteProblemsPresentation.from(monitor, true);

        assertTrue(model.snapshotAvailable);
        assertTrue(model.issues.isEmpty());
        assertEquals(2, model.incompleteCount);
        assertEquals("问题状态不完整", model.stateLabel);
        assertTrue(model.summary.contains("暂不能判断为全部正常"));
    }

    @Test
    public void staleSnapshotNeverPresentsOldProblemsAsCurrent() throws Exception {
        JSONObject monitor = completeMonitor().put("alerts", new JSONObject()
                .put("warningCount", 0)
                .put("errorCount", 4)
                .put("criticalCount", 0));

        OperationsRemoteProblemsPresentation.ViewModel model =
                OperationsRemoteProblemsPresentation.from(monitor, false);

        assertFalse(model.snapshotAvailable);
        assertTrue(model.issues.isEmpty());
        assertEquals("等待电脑更新远程状态", model.stateLabel);
        assertTrue(model.summary.contains("旧问题不会作为当前问题展示"));
    }

    @Test
    public void missingMonitorHasExplicitRecoveryCopy() {
        OperationsRemoteProblemsPresentation.ViewModel model =
                OperationsRemoteProblemsPresentation.from(null, true);

        assertFalse(model.snapshotAvailable);
        assertTrue(model.issues.isEmpty());
        assertEquals("远程问题状态暂不可用", model.stateLabel);
        assertTrue(model.summary.contains("不会把未知状态当作正常"));
    }

    private static JSONObject completeMonitor() throws Exception {
        return new JSONObject()
                .put("flow", new JSONObject()
                        .put("available", true)
                        .put("isActive", false)
                        .put("phase", "idle"))
                .put("devices", new JSONObject()
                        .put("available", true)
                        .put("hasConfiguredDevices", true)
                        .put("readyCount", 4)
                        .put("attentionCount", 0)
                        .put("totalCount", 4))
                .put("messageChannel", new JSONObject()
                        .put("available", true)
                        .put("connected", true)
                        .put("subscriptionReady", true)
                        .put("activeSubscriptionCount", 4)
                        .put("registeredSubscriptionCount", 4))
                .put("alerts", new JSONObject()
                        .put("warningCount", 0)
                        .put("errorCount", 0)
                        .put("criticalCount", 0))
                .put("performance", new JSONObject()
                        .put("cpuPercent", 8.2)
                        .put("mainUi", new JSONObject().put("state", "responsive")))
                .put("applicationRecovery", new JSONObject()
                        .put("supported", true)
                        .put("registered", true)
                        .put("automaticWatchdogActive", true));
    }

    private static List<String> sections(
            OperationsRemoteProblemsPresentation.ViewModel model) {
        return model.issues.stream().map(issue -> issue.section).collect(Collectors.toList());
    }
}

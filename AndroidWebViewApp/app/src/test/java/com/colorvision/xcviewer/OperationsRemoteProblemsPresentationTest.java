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
    public void signedProblemsCanBeReviewedWithoutHidingCurrentState() throws Exception {
        JSONObject monitor = completeMonitor()
                .put("devices", new JSONObject()
                        .put("available", true)
                        .put("hasConfiguredDevices", true)
                        .put("readyCount", 2)
                        .put("attentionCount", 1)
                        .put("totalCount", 3)
                        .put("offlineCount", 1))
                .put("alerts", new JSONObject()
                        .put("warningCount", 0)
                        .put("errorCount", 2)
                        .put("criticalCount", 0));
        OperationsRemoteProblemsPresentation.ViewModel raw =
                OperationsRemoteProblemsPresentation.from(monitor, true);

        OperationsRemoteProblemsPresentation.ViewModel model =
                OperationsRemoteProblemsPresentation.withAcknowledgements(
                        raw, (findingId, revision) -> "relay-devices".equals(findingId));

        assertEquals(2, model.issues.size());
        assertEquals(1, model.pendingIssues.size());
        assertEquals("alerts", model.pendingIssues.get(0).section);
        assertEquals(1, model.reviewedIssues.size());
        assertEquals("devices", model.reviewedIssues.get(0).section);
        assertTrue(model.reviewedIssues.get(0).acknowledged);
        assertEquals("1 项待复核", model.stateLabel);
        assertTrue(model.reviewedIssues.get(0).accessibilityLabel().startsWith("已复核，"));

        OperationsRemoteProblemsPresentation.FocusedViewModel focused =
                OperationsRemoteProblemsPresentation.focus(
                        model, OperationsWatchPolicy.ATTENTION_DEVICES);
        assertEquals("devices", focused.model.issues.get(0).section);
        assertEquals("alerts", focused.model.pendingIssues.get(0).section);
        assertEquals("devices", focused.model.reviewedIssues.get(0).section);
        assertTrue(focused.contextMessage.contains("已定位“检测设备”"));
    }

    @Test
    public void allReviewedSignedProblemsRemainExplicitlyPresent() throws Exception {
        JSONObject monitor = completeMonitor().put("alerts", new JSONObject()
                .put("warningCount", 1)
                .put("errorCount", 0)
                .put("criticalCount", 0));

        OperationsRemoteProblemsPresentation.ViewModel model =
                OperationsRemoteProblemsPresentation.withAcknowledgements(
                        OperationsRemoteProblemsPresentation.from(monitor, true),
                        (findingId, revision) -> true);

        assertTrue(model.pendingIssues.isEmpty());
        assertEquals(1, model.reviewedIssues.size());
        assertEquals("1 项已复核 · 状态仍存在", model.stateLabel);
    }

    @Test
    public void signedProblemRevisionIgnoresPollingTimeButTracksNewEvidence()
            throws Exception {
        JSONObject alerts = new JSONObject()
                .put("count", 1)
                .put("warningCount", 0)
                .put("errorCount", 1)
                .put("criticalCount", 0)
                .put("latestOccurredAt", "2026-08-19T01:00:00Z");
        JSONObject monitor = completeMonitor().put("alerts", alerts);
        String first = issue(
                OperationsRemoteProblemsPresentation.from(monitor, true), "alerts").revision;
        assertTrue(first.matches("[0-9a-f]{64}"));

        monitor.put("capturedAt", "2026-08-19T02:00:00Z");
        alerts.put("observedAt", "2026-08-19T02:00:00Z");
        assertEquals(first, issue(
                OperationsRemoteProblemsPresentation.from(monitor, true), "alerts").revision);

        alerts.put("latestOccurredAt", "2026-08-19T02:00:00Z");
        assertFalse(first.equals(issue(
                OperationsRemoteProblemsPresentation.from(monitor, true), "alerts").revision));
    }

    @Test
    public void signedDeviceRevisionTracksMaterialCountsNotObservationTime()
            throws Exception {
        JSONObject devices = new JSONObject()
                .put("available", true)
                .put("hasConfiguredDevices", true)
                .put("readyCount", 2)
                .put("attentionCount", 1)
                .put("unavailableCount", 1)
                .put("totalCount", 3)
                .put("offlineCount", 1)
                .put("observedAt", "2026-08-19T01:00:00Z");
        JSONObject monitor = completeMonitor().put("devices", devices);
        String first = issue(
                OperationsRemoteProblemsPresentation.from(monitor, true), "devices").revision;

        devices.put("observedAt", "2026-08-19T02:00:00Z");
        assertEquals(first, issue(
                OperationsRemoteProblemsPresentation.from(monitor, true), "devices").revision);
        devices.put("offlineCount", 2);
        assertFalse(first.equals(issue(
                OperationsRemoteProblemsPresentation.from(monitor, true), "devices").revision));
    }

    @Test
    public void remainingSignedProblemRevisionsUseOnlyFixedMaterialState()
            throws Exception {
        JSONObject monitor = completeMonitor();
        monitor.getJSONObject("flow").put("isActive", true).put("phase", "paused");
        monitor.getJSONObject("messageChannel")
                .put("connected", false)
                .put("subscriptionReady", false);
        monitor.getJSONObject("performance").getJSONObject("mainUi")
                .put("state", "unresponsive");
        monitor.getJSONObject("applicationRecovery").put("registered", false);
        OperationsDashboardStatusFormatter.Item status =
                new OperationsDashboardStatusFormatter.Item(
                        "固定状态", "仅用于版本测试",
                        OperationsDashboardStatusFormatter.TONE_ATTENTION);

        OperationsRemoteProblemRevision.Identity flow =
                OperationsRemoteProblemRevision.capture("flow", monitor, status);
        OperationsRemoteProblemRevision.Identity message =
                OperationsRemoteProblemRevision.capture("message", monitor, status);
        OperationsRemoteProblemRevision.Identity performance =
                OperationsRemoteProblemRevision.capture("performance", monitor, status);
        OperationsRemoteProblemRevision.Identity recovery =
                OperationsRemoteProblemRevision.capture("recovery", monitor, status);
        assertEquals("relay-flow", flow.findingId);
        assertEquals("relay-message", message.findingId);
        assertEquals("relay-performance", performance.findingId);
        assertEquals("relay-recovery", recovery.findingId);

        monitor.put("capturedAt", "2026-08-19T03:00:00Z");
        monitor.getJSONObject("messageChannel").put("observedAt", "2026-08-19T03:00:00Z");
        monitor.getJSONObject("performance").put("cpuPercent", 99.0);
        assertEquals(message.revision,
                OperationsRemoteProblemRevision.capture("message", monitor, status).revision);
        assertEquals(performance.revision,
                OperationsRemoteProblemRevision.capture("performance", monitor, status).revision);

        monitor.getJSONObject("flow").put("isActive", false);
        monitor.getJSONObject("messageChannel").put("activeSubscriptionCount", 3);
        monitor.getJSONObject("performance").getJSONObject("mainUi").put("state", "slow");
        monitor.getJSONObject("applicationRecovery").put("automaticWatchdogActive", false);
        assertFalse(flow.revision.equals(
                OperationsRemoteProblemRevision.capture("flow", monitor, status).revision));
        assertFalse(message.revision.equals(
                OperationsRemoteProblemRevision.capture("message", monitor, status).revision));
        assertFalse(performance.revision.equals(
                OperationsRemoteProblemRevision.capture("performance", monitor, status).revision));
        assertFalse(recovery.revision.equals(
                OperationsRemoteProblemRevision.capture("recovery", monitor, status).revision));
        assertFalse(OperationsRemoteProblemRevision.capture(
                "arbitrary", monitor, status).available());
    }

    @Test
    public void notificationFocusMovesTheMatchingSignedIssueFirst() throws Exception {
        JSONObject monitor = completeMonitor()
                .put("devices", new JSONObject()
                        .put("available", true)
                        .put("hasConfiguredDevices", true)
                        .put("readyCount", 2)
                        .put("attentionCount", 1)
                        .put("totalCount", 3)
                        .put("offlineCount", 1))
                .put("alerts", new JSONObject()
                        .put("warningCount", 0)
                        .put("errorCount", 2)
                        .put("criticalCount", 0));
        OperationsRemoteProblemsPresentation.ViewModel model =
                OperationsRemoteProblemsPresentation.from(monitor, true);

        OperationsRemoteProblemsPresentation.FocusedViewModel focused =
                OperationsRemoteProblemsPresentation.focus(
                        model, OperationsWatchPolicy.ATTENTION_DEVICES);

        assertEquals(Arrays.asList("devices", "alerts"), sections(focused.model));
        assertEquals("来自后台提醒 · 已定位“检测设备”相关证据并优先显示。",
                focused.contextMessage);
    }

    @Test
    public void notificationFocusDistinguishesResolvedFromUnavailableState() throws Exception {
        OperationsRemoteProblemsPresentation.FocusedViewModel resolved =
                OperationsRemoteProblemsPresentation.focus(
                        OperationsRemoteProblemsPresentation.from(completeMonitor(), true),
                        OperationsWatchPolicy.ATTENTION_DEVICES);
        assertTrue(resolved.contextMessage.contains("不再发现“检测设备”"));

        OperationsRemoteProblemsPresentation.FocusedViewModel unavailable =
                OperationsRemoteProblemsPresentation.focus(
                        OperationsRemoteProblemsPresentation.from(null, true),
                        OperationsWatchPolicy.ATTENTION_DEVICES);
        assertTrue(unavailable.contextMessage.contains("尚不能确认“检测设备”"));
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

    private static OperationsRemoteProblemsPresentation.Issue issue(
            OperationsRemoteProblemsPresentation.ViewModel model, String section) {
        return model.issues.stream()
                .filter(value -> section.equals(value.section))
                .findFirst()
                .orElseThrow(AssertionError::new);
    }
}

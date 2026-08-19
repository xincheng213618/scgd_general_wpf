package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class OperationsDashboardRefreshPolicyTest {
    @Test
    public void toolbarRefreshOnlyAppearsOnARefreshableOverview() {
        assertTrue(OperationsDashboardRefreshPolicy.showsToolbarAction(
                true, true, true, false, true));
        assertFalse(OperationsDashboardRefreshPolicy.showsToolbarAction(
                true, true, false, false, true));
        assertFalse(OperationsDashboardRefreshPolicy.showsToolbarAction(
                true, true, true, true, true));
        assertFalse(OperationsDashboardRefreshPolicy.showsToolbarAction(
                false, true, true, false, true));
        assertFalse(OperationsDashboardRefreshPolicy.showsToolbarAction(
                true, true, true, false, false));
    }

    @Test
    public void toolbarRefreshDisablesWhileItsManualRequestIsInFlight() {
        assertTrue(OperationsDashboardRefreshPolicy.toolbarActionEnabled(true, false));
        assertFalse(OperationsDashboardRefreshPolicy.toolbarActionEnabled(true, true));
        assertFalse(OperationsDashboardRefreshPolicy.toolbarActionEnabled(false, false));
    }

    @Test
    public void remoteToolboxReusesTheToolbarRefreshAction() {
        assertTrue(OperationsDashboardRefreshPolicy.showsRemoteToolboxAction(
                true, true, true, true, false, true));
        assertFalse(OperationsDashboardRefreshPolicy.showsRemoteToolboxAction(
                true, true, true, false, false, true));
        assertFalse(OperationsDashboardRefreshPolicy.showsRemoteToolboxAction(
                true, true, false, true, false, true));
        assertFalse(OperationsDashboardRefreshPolicy.showsRemoteToolboxAction(
                true, true, true, true, true, true));
        assertFalse(OperationsDashboardRefreshPolicy.showsRemoteToolboxAction(
                true, true, true, true, false, false));
    }

    @Test
    public void problemCenterReusesTheToolbarRefreshAction() {
        assertTrue(OperationsDashboardRefreshPolicy.showsProblemCenterAction(
                true, true, true, false, true));
        assertFalse(OperationsDashboardRefreshPolicy.showsProblemCenterAction(
                true, true, false, false, true));
        assertFalse(OperationsDashboardRefreshPolicy.showsProblemCenterAction(
                true, true, true, true, true));
        assertFalse(OperationsDashboardRefreshPolicy.showsProblemCenterAction(
                false, true, true, false, true));
        assertFalse(OperationsDashboardRefreshPolicy.showsProblemCenterAction(
                true, true, true, false, false));
    }

    @Test
    public void directDetailReusesTheToolbarRefreshAction() {
        assertTrue(OperationsDashboardRefreshPolicy.showsDetailAction(
                true, true, true, false, true, "/ops/v1/services/health"));
        assertFalse(OperationsDashboardRefreshPolicy.showsDetailAction(
                true, true, true, false, true, ""));
        assertFalse(OperationsDashboardRefreshPolicy.showsDetailAction(
                true, true, false, false, true, "/ops/v1/services/health"));
        assertFalse(OperationsDashboardRefreshPolicy.showsDetailAction(
                true, true, true, true, true, "/ops/v1/services/health"));
        assertFalse(OperationsDashboardRefreshPolicy.showsDetailAction(
                true, true, true, false, false, "/ops/v1/services/health"));
    }

    @Test
    public void visibleDashboardStartsANewRefresh() {
        assertEquals(
                OperationsDashboardRefreshPolicy.Decision.START,
                OperationsDashboardRefreshPolicy.decide(
                        true, true, true, true, true, false));
    }

    @Test
    public void manualRefreshJoinsAnAutomaticHeartbeatAlreadyInFlight() {
        assertEquals(
                OperationsDashboardRefreshPolicy.Decision.JOIN,
                OperationsDashboardRefreshPolicy.decide(
                        true, true, true, true, true, true));
    }

    @Test
    public void nonDashboardAndUnavailableClientsRejectTheGesture() {
        assertEquals(
                OperationsDashboardRefreshPolicy.Decision.REJECT,
                OperationsDashboardRefreshPolicy.decide(
                        true, true, false, true, true, false));
        assertEquals(
                OperationsDashboardRefreshPolicy.Decision.REJECT,
                OperationsDashboardRefreshPolicy.decide(
                        true, true, true, true, false, false));
    }

    @Test
    public void completionCopyDistinguishesDirectStaleAndFailedRefreshes() {
        assertEquals("刷新完成 · 现场直连",
                OperationsDashboardRefreshPolicy.completionMessage(
                        true, true, false, true));
        assertEquals("刷新完成 · 电脑在线",
                OperationsDashboardRefreshPolicy.completionMessage(
                        true, true, true, true));
        assertEquals("刷新完成 · 电脑仍未上线",
                OperationsDashboardRefreshPolicy.completionMessage(
                        true, true, true, false));
        assertEquals("刷新未完成 · 实时摘要不可用",
                OperationsDashboardRefreshPolicy.completionMessage(
                        true, false, false, true));
        assertEquals("刷新失败 · 连接仍不可达",
                OperationsDashboardRefreshPolicy.completionMessage(
                        false, false, false, false));
    }
}

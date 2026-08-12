package com.colorvision.xcviewer;

import org.junit.Test;

import java.net.URL;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsRelayPolicyTest {
    @Test
    public void relayRequestsStayOnTheFixedApplicationOrigin() throws Exception {
        assertTrue(OperationsRelayPolicy.isAllowedRequestUrl(new URL(
                "http://xc213618.ddns.me:9998/api/ops/v1/device-relay/tasks")));
        assertFalse(OperationsRelayPolicy.isAllowedRequestUrl(new URL(
                "https://xc213618.ddns.me:9998/api/ops/v1/device-relay/tasks")));
        assertFalse(OperationsRelayPolicy.isAllowedRequestUrl(new URL(
                "http://example.com:9998/api/ops/v1/device-relay/tasks")));
        assertFalse(OperationsRelayPolicy.isAllowedRequestUrl(new URL(
                "http://xc213618.ddns.me:9998/api/ops/v1/hosts")));
        assertFalse(OperationsRelayPolicy.isAllowedRequestUrl(new URL(
                "http://xc213618.ddns.me:9998/api/ops/v1/device-relay/tasks?next=other")));
    }

    @Test
    public void hostFreshnessUsesTheHostSignedTimestamp() {
        assertTrue(OperationsRelayPolicy.isHostFresh(1_000L, 1_060_000L));
        assertFalse(OperationsRelayPolicy.isHostFresh(1_000L, 1_181_000L));
        assertFalse(OperationsRelayPolicy.isHostFresh(0L, 1_000L));
    }

    @Test
    public void relayIdentifiersRemainPathSafe() {
        assertTrue(OperationsRelayPolicy.isSafeIdentifier("abc_123-XYZ"));
        assertFalse(OperationsRelayPolicy.isSafeIdentifier("../host"));
        assertFalse(OperationsRelayPolicy.isSafeIdentifier(""));
    }

    @Test
    public void remoteTaskCatalogAllowsOnlyTheFiveBoundedIntents() {
        assertTrue(OperationsRelayPolicy.isAllowedTaskCapability(
                OperationsRelayPolicy.CAPABILITY_SHOW_WINDOW));
        assertTrue(OperationsRelayPolicy.isAllowedTaskCapability(
                OperationsRelayPolicy.CAPABILITY_MINIMIZE_WINDOW));
        assertTrue(OperationsRelayPolicy.isAllowedTaskCapability(
                OperationsRelayPolicy.CAPABILITY_REQUEST_DIAGNOSTICS));
        assertTrue(OperationsRelayPolicy.isAllowedTaskCapability(
                OperationsRelayPolicy.CAPABILITY_RECOVER_MESSAGE_CHANNEL));
        assertTrue(OperationsRelayPolicy.isAllowedTaskCapability(
                OperationsRelayPolicy.CAPABILITY_CANCEL_FLOW));
        assertFalse(OperationsRelayPolicy.isAllowedTaskCapability("ops.application.restart"));
        assertFalse(OperationsRelayPolicy.isAllowedTaskCapability("cmd.exe"));
    }
}

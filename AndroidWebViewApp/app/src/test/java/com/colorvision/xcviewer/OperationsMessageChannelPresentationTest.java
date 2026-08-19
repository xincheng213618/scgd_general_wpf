package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsMessageChannelPresentationTest {
    @Test
    public void healthyChannelShowsConnectionSubscriptionsAndActivityWithoutRecovery()
            throws Exception {
        JSONObject payload = new JSONObject("{"
                + "\"available\":true,"
                + "\"configured\":true,"
                + "\"state\":\"connected\","
                + "\"connected\":true,"
                + "\"subscriptionReady\":true,"
                + "\"registeredSubscriptionCount\":6,"
                + "\"activeSubscriptionCount\":6,"
                + "\"lastConnectedAt\":\"2026-08-19T01:30:00Z\","
                + "\"observedAt\":\"2026-08-19T01:31:00Z\"}");

        OperationsMessageChannelPresentation.ViewModel model =
                OperationsMessageChannelPresentation.from(
                        payload, value -> value.isEmpty() ? "" : "时间 " + value);

        assertTrue(model.available);
        assertEquals("消息通道正常", model.stateLabel);
        assertEquals("已建立", model.connectionLabel);
        assertEquals("6/6 · 已就绪", model.subscriptionLabel);
        assertEquals(OperationsMessageChannelPresentation.TONE_HEALTHY, model.tone);
        assertFalse(model.canRecover);
        assertEquals(2, model.activityItems.size());
        assertEquals("最近连接，时间 2026-08-19T01:30:00Z",
                model.activityItems.get(0).accessibilityLabel());
    }

    @Test
    public void disconnectedAndDegradedChannelsOfferBoundedRecovery() throws Exception {
        OperationsMessageChannelPresentation.ViewModel disconnected =
                OperationsMessageChannelPresentation.from(
                        new JSONObject("{\"available\":true,\"configured\":true,"
                                + "\"state\":\"connected\",\"connected\":false,"
                                + "\"subscriptionReady\":true,"
                                + "\"registeredSubscriptionCount\":-2,"
                                + "\"activeSubscriptionCount\":20000}"),
                        value -> value);
        assertEquals("消息连接已断开", disconnected.stateLabel);
        assertEquals("未建立", disconnected.connectionLabel);
        assertEquals("9999/0 · 未就绪", disconnected.subscriptionLabel);
        assertTrue(disconnected.canRecover);
        assertTrue(disconnected.recoverySummary.contains("电脑当前配置"));

        OperationsMessageChannelPresentation.ViewModel degraded =
                OperationsMessageChannelPresentation.from(
                        new JSONObject("{\"available\":true,\"configured\":true,"
                                + "\"connected\":true,\"subscriptionReady\":false,"
                                + "\"registeredSubscriptionCount\":8,"
                                + "\"activeSubscriptionCount\":3}"),
                        value -> value);
        assertEquals("消息订阅需要恢复", degraded.stateLabel);
        assertEquals("3/8 · 未就绪", degraded.subscriptionLabel);
        assertTrue(degraded.canRecover);
    }

    @Test
    public void unconfiguredAndUnavailableStatesNeverOfferRemoteConfiguration()
            throws Exception {
        OperationsMessageChannelPresentation.ViewModel unconfigured =
                OperationsMessageChannelPresentation.from(
                        new JSONObject("{\"available\":true,\"configured\":false,"
                                + "\"connected\":true,\"subscriptionReady\":true}"),
                        value -> value);
        assertEquals("消息通道尚未配置", unconfigured.stateLabel);
        assertFalse(unconfigured.canRecover);
        assertTrue(unconfigured.recoverySummary.contains("电脑端完成配置"));

        OperationsMessageChannelPresentation.ViewModel unavailable =
                OperationsMessageChannelPresentation.from(
                        new JSONObject("{\"available\":false}"), value -> value);
        assertFalse(unavailable.available);
        assertEquals("消息通道状态不可用", unavailable.stateLabel);
        assertFalse(unavailable.canRecover);
        assertTrue(unavailable.activityItems.isEmpty());

        OperationsMessageChannelPresentation.ViewModel unknown =
                OperationsMessageChannelPresentation.from(
                        new JSONObject("{\"available\":true,\"state\":\"invented\"}"),
                        value -> value);
        assertFalse(unknown.available);
        assertFalse(unknown.canRecover);
    }

    @Test
    public void olderPayloadWithoutConfiguredFlagStillUsesNormalizedState() throws Exception {
        OperationsMessageChannelPresentation.ViewModel connected =
                OperationsMessageChannelPresentation.from(
                        new JSONObject("{\"available\":true,\"state\":\"connected\","
                                + "\"registeredSubscriptionCount\":4,"
                                + "\"activeSubscriptionCount\":4}"),
                        value -> value);

        assertEquals("消息通道正常", connected.stateLabel);
        assertFalse(connected.canRecover);
    }
}

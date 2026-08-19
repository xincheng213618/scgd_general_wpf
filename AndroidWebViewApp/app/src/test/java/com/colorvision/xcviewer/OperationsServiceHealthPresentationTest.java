package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsServiceHealthPresentationTest {
    @Test
    public void healthyServicesSeparateSummaryStatusSourceAndMaintenanceBoundary()
            throws Exception {
        JSONObject payload = new JSONObject("{"
                + "\"available\":true,"
                + "\"allHealthy\":true,"
                + "\"services\":[{"
                + "\"serviceId\":\"colorvision-service-host\","
                + "\"title\":\"不可信标题\","
                + "\"status\":\"running\","
                + "\"healthy\":true,"
                + "\"statusSource\":\"windows-service-control-manager\","
                + "\"observedAt\":\"2026-08-19T01:30:00Z\","
                + "\"maintenanceSupported\":false},{"
                + "\"serviceId\":\"mqtt-broker\","
                + "\"status\":\"running\","
                + "\"healthy\":true,"
                + "\"statusSource\":\"windows-service-control-manager\","
                + "\"observedAt\":\"2026-08-19T01:30:00Z\","
                + "\"maintenanceSupported\":true}],"
                + "\"privacyNotice\":\"只返回白名单状态。\"}");

        OperationsServiceHealthPresentation.ViewModel model =
                OperationsServiceHealthPresentation.from(
                        payload, value -> "格式化 " + value);

        assertTrue(model.available);
        assertEquals("2 项服务 · 均正常", model.stateLabel);
        assertEquals("白名单服务均正常", model.summaryLabel);
        assertEquals("正常 2 · 需关注 0", model.countLabel);
        assertEquals("白名单服务 · 2", model.servicesSectionLabel());
        assertEquals(0, model.attentionCount);
        assertEquals("只返回白名单状态。", model.privacyNotice);

        OperationsServiceHealthPresentation.Service service = model.services.get(0);
        assertEquals("ColorVision 后台服务", service.title);
        assertEquals("运行中 · 正常", service.statusSummary());
        assertEquals("Windows 服务控制管理器 · 格式化 2026-08-19T01:30:00Z",
                service.observationSummary());
        assertEquals("手机维护 · 不提供远程操作", service.maintenanceLabel);

        OperationsServiceHealthPresentation.Service mqtt = model.services.get(1);
        assertEquals("MQTT 消息服务", mqtt.title);
        assertEquals("手机维护 · 确认后可重启固定 MQTT 服务", mqtt.maintenanceLabel);
        assertTrue(model.plainText().contains("MQTT 消息服务\n状态：运行中 · 正常"));
    }

    @Test
    public void itemStateWinsOverStaleAggregateAndUsesAttentionTone() throws Exception {
        JSONObject payload = new JSONObject()
                .put("available", true)
                .put("allHealthy", true)
                .put("services", new JSONArray().put(new JSONObject()
                        .put("serviceId", "mqtt-broker")
                        .put("status", "stopped")
                        .put("healthy", false)
                        .put("statusSource", "application-config")
                        .put("maintenanceSupported", true)));

        OperationsServiceHealthPresentation.ViewModel model =
                OperationsServiceHealthPresentation.from(payload, value -> value);

        assertEquals("1 项服务 · 1 项需关注", model.stateLabel);
        assertEquals("有白名单服务需要关注", model.summaryLabel);
        assertEquals("需关注 1 · 正常 0", model.countLabel);
        assertEquals(OperationsServiceHealthPresentation.TONE_ATTENTION, model.tone);
        assertEquals("已停止 · 需关注", model.services.get(0).statusSummary());
        assertEquals("应用配置", model.services.get(0).observationSummary());
    }

    @Test
    public void unknownAndDuplicateServicesCannotExpandFixedAllowlist() throws Exception {
        JSONArray services = new JSONArray()
                .put(new JSONObject().put("serviceId", "unknown-service")
                        .put("title", "任意服务").put("healthy", true))
                .put(new JSONObject().put("serviceId", "mqtt-broker")
                        .put("healthy", true).put("maintenanceSupported", true))
                .put(new JSONObject().put("serviceId", "mqtt-broker")
                        .put("healthy", false).put("maintenanceSupported", false))
                .put(new JSONObject().put("serviceId", "colorvision-service-host")
                        .put("healthy", true));

        OperationsServiceHealthPresentation.ViewModel model =
                OperationsServiceHealthPresentation.from(
                        new JSONObject().put("available", true).put("services", services),
                        value -> value);

        assertEquals(2, model.services.size());
        assertEquals("MQTT 消息服务", model.services.get(0).title);
        assertEquals("ColorVision 后台服务", model.services.get(1).title);
        assertEquals("2 项服务 · 均正常", model.stateLabel);
    }

    @Test
    public void unavailableAndEmptyResponsesRemainExplicit() throws Exception {
        OperationsServiceHealthPresentation.ViewModel unavailable =
                OperationsServiceHealthPresentation.from(
                        new JSONObject("{\"available\":false}"), value -> value);
        assertFalse(unavailable.available);
        assertEquals("服务状态不可用", unavailable.stateLabel);
        assertEquals(OperationsServiceHealthPresentation.TONE_UNAVAILABLE, unavailable.tone);
        assertTrue(unavailable.services.isEmpty());

        OperationsServiceHealthPresentation.ViewModel empty =
                OperationsServiceHealthPresentation.from(
                        new JSONObject("{\"available\":true}"), value -> value);
        assertTrue(empty.available);
        assertEquals("未发现适用服务", empty.stateLabel);
        assertEquals("白名单服务", empty.servicesSectionLabel());
        assertTrue(empty.services.isEmpty());
    }
}

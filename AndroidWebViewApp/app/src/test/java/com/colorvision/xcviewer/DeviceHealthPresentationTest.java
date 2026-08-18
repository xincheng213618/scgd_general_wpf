package com.colorvision.xcviewer;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class DeviceHealthPresentationTest {
    @Test
    public void attentionOverviewPrioritizesReasonsAndAffectedCategories() throws Exception {
        JSONObject payload = new JSONObject("{"
                + "\"available\":true,"
                + "\"hasConfiguredDevices\":true,"
                + "\"totalCount\":6,"
                + "\"attentionCount\":2,"
                + "\"readyCount\":2,"
                + "\"closedCount\":2,"
                + "\"unavailableCount\":2,"
                + "\"offlineCount\":2,"
                + "\"observedAt\":\"2026-08-18T08:11:00Z\","
                + "\"categories\":["
                + "{\"category\":\"camera\",\"totalCount\":1,\"readyCount\":0,\"unavailableCount\":1},"
                + "{\"category\":\"algorithm\",\"totalCount\":1,\"readyCount\":1}"
                + "]}");

        DeviceHealthPresentation.ViewModel model = DeviceHealthPresentation.from(payload);

        assertTrue(model.available);
        assertTrue(model.attentionRequired);
        assertEquals("2 台设备需要关注", model.headline);
        assertEquals("共 6 台 · 就绪 2 · 已关闭 2 · 不可用 2", model.summary);
        assertEquals("离线 2", model.unavailableReasons);
        assertEquals(2, model.categories.size());
        assertEquals("相机类", model.categories.get(0).label);
        assertTrue(model.categories.get(0).attentionRequired);
        assertEquals("相机类，共 1 台，就绪 0，不可用 1",
                model.categories.get(0).accessibilityLabel());
        assertFalse(model.categories.get(1).attentionRequired);
        assertTrue(model.accessibilitySummary().contains("不可用原因，离线 2"));
    }

    @Test
    public void closedDevicesAreDistinguishedFromFaults() throws Exception {
        JSONObject payload = new JSONObject("{"
                + "\"available\":true,"
                + "\"hasConfiguredDevices\":true,"
                + "\"totalCount\":2,"
                + "\"readyCount\":0,"
                + "\"closedCount\":2} ");

        DeviceHealthPresentation.ViewModel model = DeviceHealthPresentation.from(payload);

        assertFalse(model.attentionRequired);
        assertEquals("2 台设备已关闭", model.headline);
        assertTrue(model.guidance.contains("不等同故障"));
    }

    @Test
    public void unavailableStateKeepsRecoveryBounded() {
        DeviceHealthPresentation.ViewModel model = DeviceHealthPresentation.from(null);

        assertFalse(model.available);
        assertTrue(model.attentionRequired);
        assertEquals("当前无法读取检测设备状态", model.headline);
        assertEquals("不会自动重连或重启设备", model.summary);
        assertTrue(model.categories.isEmpty());
    }

    @Test
    public void emptyConfigurationExplainsTheComputerSideNextStep() throws Exception {
        DeviceHealthPresentation.ViewModel model = DeviceHealthPresentation.from(
                new JSONObject("{\"available\":true,\"hasConfiguredDevices\":false}"));

        assertTrue(model.available);
        assertFalse(model.hasConfiguredDevices);
        assertFalse(model.attentionRequired);
        assertEquals("尚未发现已加载的检测设备", model.headline);
        assertTrue(model.guidance.contains("电脑端"));
    }
}

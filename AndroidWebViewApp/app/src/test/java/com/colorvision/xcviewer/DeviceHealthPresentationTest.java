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
                + "{\"category\":\"algorithm\",\"totalCount\":1,\"readyCount\":1},"
                + "{\"category\":\"camera\",\"totalCount\":1,\"readyCount\":0,\"unavailableCount\":1},"
                + "{\"category\":\"spectrum\",\"totalCount\":1,\"readyCount\":0,\"unavailableCount\":1}"
                + "]}");

        DeviceHealthPresentation.ViewModel model = DeviceHealthPresentation.from(payload);

        assertTrue(model.available);
        assertTrue(model.attentionRequired);
        assertEquals("2 台设备需要关注", model.headline);
        assertEquals("共 6 台 · 就绪 2 · 已关闭 2 · 不可用 2", model.summary);
        assertEquals("离线 2", model.unavailableReasons);
        assertEquals(3, model.categories.size());
        assertEquals("相机类", model.categories.get(0).label);
        assertEquals("光谱类", model.categories.get(1).label);
        assertEquals("算法类", model.categories.get(2).label);
        assertEquals(2, model.attentionCategories().size());
        assertEquals(1, model.otherCategories().size());
        assertEquals("相机类、光谱类", model.attentionCategorySummary());
        assertEquals("相机 离线 1 · 光谱 离线 1", model.compactAttentionSummary());
        assertTrue(model.categories.get(0).attentionRequired);
        assertEquals("离线 1", model.categories.get(0).unavailableReasons);
        assertEquals("相机类，共 1 台，就绪 0，不可用 1，原因，离线 1",
                model.categories.get(0).accessibilityLabel());
        assertFalse(model.categories.get(2).attentionRequired);
        assertTrue(model.guidance.startsWith("优先检查 相机类、光谱类"));
        assertTrue(model.accessibilitySummary().contains("不可用原因，离线 2"));
        assertTrue(model.accessibilitySummary().contains("需关注类型，相机类、光谱类"));
        assertTrue(model.canTrackRecovery());
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
        assertEquals("", model.compactAttentionSummary());
        assertTrue(model.guidance.contains("不等同故障"));
    }

    @Test
    public void compactAttentionSummaryCapsDenseCategoryAndReasonLists() throws Exception {
        DeviceHealthPresentation.ViewModel model = DeviceHealthPresentation.from(
                new JSONObject("{"
                        + "\"available\":true,"
                        + "\"hasConfiguredDevices\":true,"
                        + "\"attentionCount\":3,"
                        + "\"offlineCount\":1,"
                        + "\"uninitializedCount\":1,"
                        + "\"unauthorizedCount\":1,"
                        + "\"categories\":["
                        + "{\"category\":\"camera\",\"unavailableCount\":1},"
                        + "{\"category\":\"spectrum\",\"unavailableCount\":1},"
                        + "{\"category\":\"motion\",\"unavailableCount\":1}"
                        + "]}"));

        assertEquals("相机、光谱等 3 类 · 离线 1、未初始化 1等",
                model.compactAttentionSummary());
    }

    @Test
    public void categoryReasonsUseExactServerAttributionWhenMultipleCausesExist() throws Exception {
        DeviceHealthPresentation.ViewModel model = DeviceHealthPresentation.from(
                new JSONObject("{"
                        + "\"available\":true,"
                        + "\"hasConfiguredDevices\":true,"
                        + "\"attentionCount\":2,"
                        + "\"unavailableCount\":2,"
                        + "\"offlineCount\":1,"
                        + "\"unauthorizedCount\":1,"
                        + "\"categories\":["
                        + "{\"category\":\"camera\",\"totalCount\":1,"
                        + "\"unavailableCount\":1,\"offlineCount\":1},"
                        + "{\"category\":\"spectrum\",\"totalCount\":1,"
                        + "\"unavailableCount\":1,\"unauthorizedCount\":1}"
                        + "]}"));

        assertEquals("离线 1", model.categories.get(0).unavailableReasons);
        assertEquals("未授权 1", model.categories.get(1).unavailableReasons);
        assertEquals("相机 离线 1 · 光谱 未授权 1", model.compactAttentionSummary());
    }

    @Test
    public void unavailableStateKeepsRecoveryBounded() {
        DeviceHealthPresentation.ViewModel model = DeviceHealthPresentation.from(null);

        assertFalse(model.available);
        assertTrue(model.attentionRequired);
        assertEquals("当前无法读取检测设备状态", model.headline);
        assertEquals("不会自动重连或重启设备", model.summary);
        assertTrue(model.categories.isEmpty());
        assertFalse(model.canTrackRecovery());
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

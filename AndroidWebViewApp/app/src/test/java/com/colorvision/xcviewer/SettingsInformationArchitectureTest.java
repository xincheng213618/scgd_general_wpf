package com.colorvision.xcviewer;

import org.junit.Test;

import java.util.Arrays;
import java.util.List;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;

public class SettingsInformationArchitectureTest {
    @Test
    public void sectionsFollowTheMaterialSettingsHierarchy() {
        assertEquals(Arrays.asList("电脑连接", "后台运行", "权限", "应用"),
                SettingsInformationArchitecture.sectionHeadings());
    }

    @Test
    public void pairedSettingsStayFocusedOnOperationsAndAppMaintenance() {
        assertEquals(Arrays.asList(
                        "现场运维",
                        "安全通道",
                        "添加电脑",
                        "持续守护",
                        "运维提醒",
                        "相机权限",
                        "主题模式",
                        "应用更新"),
                SettingsInformationArchitecture.visibleRows(true));
    }

    @Test
    public void unpairedSettingsUseTheConnectionAction() {
        List<String> rows = SettingsInformationArchitecture.visibleRows(false);

        assertEquals("连接电脑", rows.get(2));
        assertEquals("持续守护", rows.get(3));
        assertEquals("运维提醒", rows.get(4));
        assertFalse(rows.stream().anyMatch(label -> label.contains("音乐")));
        assertFalse(rows.stream().anyMatch(label -> label.contains("下载站")));
        assertFalse(rows.stream().anyMatch(label -> label.contains("网络权限")));
    }
}

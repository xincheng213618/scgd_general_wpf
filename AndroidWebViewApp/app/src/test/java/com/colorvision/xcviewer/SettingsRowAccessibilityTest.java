package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

public class SettingsRowAccessibilityTest {
    @Test
    public void clickableRowAnnouncesLabelAndCurrentValue() {
        assertEquals("主题模式，跟随系统",
                SettingsRowAccessibility.contentDescription("主题模式", "跟随系统"));
    }

    @Test
    public void emptyValueKeepsItsVisibleLabel() {
        assertEquals("相机权限",
                SettingsRowAccessibility.contentDescription("相机权限", ""));
    }

    @Test
    public void nullValuesDoNotLeakIntoAccessibilityText() {
        assertEquals("安全通道",
                SettingsRowAccessibility.contentDescription("安全通道", null));
    }
}

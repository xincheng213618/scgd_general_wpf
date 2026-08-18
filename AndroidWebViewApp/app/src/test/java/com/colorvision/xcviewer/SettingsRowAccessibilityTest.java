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
    public void actionWithoutValueKeepsItsVisibleLabel() {
        assertEquals("打开现场运维",
                SettingsRowAccessibility.contentDescription("打开现场运维", ""));
    }

    @Test
    public void nullValuesDoNotLeakIntoAccessibilityText() {
        assertEquals("相机权限",
                SettingsRowAccessibility.contentDescription("相机权限", null));
    }
}

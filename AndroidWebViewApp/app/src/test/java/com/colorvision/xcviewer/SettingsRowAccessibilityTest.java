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
        assertEquals("添加电脑",
                SettingsRowAccessibility.contentDescription("添加电脑", ""));
    }

    @Test
    public void nullValuesDoNotLeakIntoAccessibilityText() {
        assertEquals("电脑与连接",
                SettingsRowAccessibility.contentDescription("电脑与连接", null));
    }
}

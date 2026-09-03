package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class SettingsInformationArchitectureTest {
    @Test
    public void pairedConnectionTextUsesTheComputerSummaryWhenAvailable() {
        assertEquals("ColorVision-PC · 共 1 台 · 设备密钥 + TLS 证书固定",
                SettingsInformationArchitecture.connectionSupportingText(
                        true, "ColorVision-PC · 共 1 台"));
        assertEquals("设备密钥 + TLS 证书固定",
                SettingsInformationArchitecture.connectionSupportingText(true, null));
    }

    @Test
    public void unpairedConnectionTextExplainsSecurePairing() {
        assertEquals("扫描安全配对码 · 配对后启用设备密钥 + TLS 证书固定",
                SettingsInformationArchitecture.connectionSupportingText(false, ""));
    }
}

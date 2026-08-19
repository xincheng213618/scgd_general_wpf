package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class OperationsConnectionPresentationTest {
    @Test
    public void directConnectionCreatesFourScannableValues() {
        OperationsConnectionPresentation.ViewModel model =
                OperationsConnectionPresentation.from(
                        " 检测电脑 ",
                        " 现场直连 ",
                        new OperationsConnectionOverviewProbe.Result(
                                OperationsConnectionOverviewProbe.Channel.DIRECT,
                                true,
                                false,
                                null,
                                null),
                        2,
                        6);

        assertEquals("检测电脑", model.computerLabel);
        assertEquals("现场直连", model.activeChannelLabel);
        assertEquals("现场直连", model.preferredChannelLabel);
        assertEquals("2 / 6", model.pairedComputersLabel);
    }

    @Test
    public void relayWaitingStateRemainsExplicit() {
        OperationsConnectionPresentation.ViewModel model =
                OperationsConnectionPresentation.from(
                        "中继电脑",
                        "固定中继",
                        new OperationsConnectionOverviewProbe.Result(
                                OperationsConnectionOverviewProbe.Channel.RELAY,
                                false,
                                false,
                                null,
                                null),
                        1,
                        6);

        assertEquals("固定中继（电脑未上线）", model.activeChannelLabel);
    }

    @Test
    public void missingValuesUseSafeSettingsLabels() {
        OperationsConnectionPresentation.ViewModel model =
                OperationsConnectionPresentation.from(
                        null,
                        " ",
                        OperationsConnectionOverviewProbe.checking(),
                        -1,
                        -2);

        assertEquals("未命名电脑", model.computerLabel);
        assertEquals("正在确认", model.activeChannelLabel);
        assertEquals("正在确认", model.preferredChannelLabel);
        assertEquals("0 / 0", model.pairedComputersLabel);
    }

    @Test
    public void pairedCountNeverExceedsTheRegistryLimit() {
        OperationsConnectionPresentation.ViewModel model =
                OperationsConnectionPresentation.from(
                        "检测电脑",
                        "现场直连",
                        null,
                        9,
                        6);

        assertEquals("6 / 6", model.pairedComputersLabel);
    }
}

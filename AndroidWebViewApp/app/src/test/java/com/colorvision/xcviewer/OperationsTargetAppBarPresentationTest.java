package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsTargetAppBarPresentationTest {
    @Test
    public void pairedOperationsPagesShowTheComputerInTheTopAppBar() {
        OperationsTargetAppBarPresentation.ViewModel model =
                OperationsTargetAppBarPresentation.from(
                        true,
                        true,
                        OperationsDestinationState.TRIAGE,
                        " 检测电脑 ");

        assertTrue(model.visible);
        assertEquals("检测电脑", model.subtitle);
        assertEquals("当前操作电脑：检测电脑，点按管理或切换电脑", model.actionLabel);
    }

    @Test
    public void connectionManagementDoesNotRepeatItsOwnCurrentComputerSummary() {
        OperationsTargetAppBarPresentation.ViewModel model =
                OperationsTargetAppBarPresentation.from(
                        true,
                        true,
                        OperationsDestinationState.CONNECTIONS,
                        "检测电脑");

        assertFalse(model.visible);
        assertEquals("", model.subtitle);
    }

    @Test
    public void settingsKeepsTheComputerContextWhileUnpairedShellsHideIt() {
        OperationsTargetAppBarPresentation.ViewModel settings =
                OperationsTargetAppBarPresentation.from(
                        true,
                        true,
                        OperationsDestinationState.SETTINGS,
                        "检测电脑");

        assertTrue(settings.visible);
        assertEquals("检测电脑", settings.subtitle);
        assertEquals("当前操作电脑：检测电脑，点按管理或切换电脑", settings.actionLabel);
        assertFalse(OperationsTargetAppBarPresentation.from(
                false,
                true,
                OperationsDestinationState.OVERVIEW,
                "").visible);
    }

    @Test
    public void missingPairedLabelUsesAnExplicitFallback() {
        OperationsTargetAppBarPresentation.ViewModel model =
                OperationsTargetAppBarPresentation.from(
                        true,
                        true,
                        OperationsDestinationState.TOOLS,
                        " ");

        assertEquals("未命名电脑", model.subtitle);
    }
}

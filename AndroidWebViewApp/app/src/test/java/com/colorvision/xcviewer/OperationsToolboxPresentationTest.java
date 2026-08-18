package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsToolboxPresentationTest {
    @Test
    public void toolsAreGroupedInsteadOfPresentedAsOneFlatActionWall() {
        OperationsToolboxPresentation.ViewModel model =
                OperationsToolboxPresentation.create();

        assertEquals(4, model.sections.size());
        assertEquals("诊断", model.sections.get(0).title);
        assertEquals("恢复", model.sections.get(1).title);
        assertEquals("取证", model.sections.get(2).title);
        assertEquals("支持与记录", model.sections.get(3).title);
        assertEquals(12, model.actionCount());
        assertTrue(model.hasUniqueActionIds());
    }

    @Test
    public void actionDescriptionsKeepConfirmationAndConsentBoundariesVisible() {
        OperationsToolboxPresentation.ViewModel model =
                OperationsToolboxPresentation.create();

        assertTrue(find(model, OperationsToolboxPresentation.ACTION_RESTART_MQTT)
                .summary.contains("再次确认"));
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_CREATE_DIAGNOSTIC)
                .summary.contains("需手机确认"));
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_CREATE_SNAPSHOT)
                .summary.contains("仅截取 ColorVision 主窗口"));
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_SUPPORT)
                .summary.contains("电脑端同意"));
    }

    @Test
    public void accessibilityLabelsReadTitleBeforeSupportingText() {
        OperationsToolboxPresentation.Action action = find(
                OperationsToolboxPresentation.create(),
                OperationsToolboxPresentation.ACTION_RECENT_EVENTS);

        assertEquals("近期事件，查看已脱敏的近期异常事件", action.accessibilityLabel());
    }

    @Test
    public void unknownActionsAreNotAvailableToTheActivityDispatcher() {
        assertTrue(OperationsToolboxPresentation.isSupportedAction(
                OperationsToolboxPresentation.ACTION_SERVICES_HEALTH));
        assertTrue(OperationsToolboxPresentation.isSupportedAction(
                OperationsToolboxPresentation.ACTION_TIMELINE));
        assertFalse(OperationsToolboxPresentation.isSupportedAction("toolbox.unknown"));
    }

    private static OperationsToolboxPresentation.Action find(
            OperationsToolboxPresentation.ViewModel model,
            String actionId) {
        for (OperationsToolboxPresentation.Section section : model.sections) {
            for (OperationsToolboxPresentation.Action action : section.actions) {
                if (actionId.equals(action.actionId)) {
                    return action;
                }
            }
        }
        throw new AssertionError("Missing action: " + actionId);
    }
}

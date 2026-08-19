package com.colorvision.xcviewer;

import org.junit.Test;

import java.util.Arrays;
import java.util.HashSet;
import java.util.Set;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertSame;
import static org.junit.Assert.assertTrue;

public class OperationsToolboxPresentationTest {
    @Test
    public void toolsAreGroupedInsteadOfPresentedAsOneFlatActionWall() {
        OperationsToolboxPresentation.ViewModel model =
                OperationsToolboxPresentation.create();

        assertEquals(5, model.sections.size());
        assertEquals("控制", model.sections.get(0).title);
        assertEquals("诊断", model.sections.get(1).title);
        assertEquals("恢复", model.sections.get(2).title);
        assertEquals("取证", model.sections.get(3).title);
        assertEquals("支持与记录", model.sections.get(4).title);
        assertEquals("支持记录", model.sections.get(4).shortcutLabel());
        assertEquals("跳到支持与记录分组",
                model.sections.get(4).shortcutAccessibilityLabel());
        Set<String> shortcutLabels = new HashSet<>();
        for (OperationsToolboxPresentation.Section section : model.sections) {
            assertTrue(shortcutLabels.add(section.shortcutLabel()));
            assertEquals("跳到" + section.title + "分组",
                    section.shortcutAccessibilityLabel());
        }
        assertEquals(17, model.actionCount());
        assertEquals(17, model.enabledActionCount());
        assertEquals(4, model.quickActionCount());
        assertEquals(OperationsToolboxPresentation.ACTION_CONNECTION_CHECK,
                model.quickActions.get(0).actionId);
        assertEquals(OperationsToolboxPresentation.ACTION_LIVE_MONITOR,
                model.quickActions.get(1).actionId);
        assertEquals(OperationsToolboxPresentation.ACTION_DEVICE_HEALTH,
                model.quickActions.get(2).actionId);
        assertEquals(OperationsToolboxPresentation.ACTION_RECENT_EVENTS,
                model.quickActions.get(3).actionId);
        assertTrue(model.hasUniqueActionIds());
    }

    @Test
    public void actionDescriptionsKeepConfirmationAndConsentBoundariesVisible() {
        OperationsToolboxPresentation.ViewModel model =
                OperationsToolboxPresentation.create();

        assertTrue(find(model, OperationsToolboxPresentation.ACTION_RESTART_MQTT)
                .summary.contains("再次确认"));
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_MINIMIZE_WINDOW)
                .summary.contains("执行前确认"));
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_CANCEL_FLOW)
                .summary.contains("仅在主检测运行"));
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE)
                .summary.contains("电脑现有配置"));
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_RESTART_APPLICATION)
                .summary.contains("检测空闲"));
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_CREATE_DIAGNOSTIC)
                .summary.contains("需手机确认"));
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_CREATE_SNAPSHOT)
                .summary.contains("仅截取 ColorVision 主窗口"));
        assertTrue(find(model, OperationsToolboxPresentation.ACTION_SUPPORT)
                .summary.contains("电脑端同意"));
    }

    @Test
    public void recentDeepToolsReplaceDefaultsWithoutGrowingTheQuickGrid() {
        OperationsToolboxPresentation.ViewModel model =
                OperationsToolboxPresentation.withRecentQuickActions(
                        OperationsToolboxPresentation.create(),
                        Arrays.asList(
                                OperationsToolboxPresentation.ACTION_CREATE_SNAPSHOT,
                                OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE));

        assertEquals(4, model.quickActionCount());
        assertEquals(OperationsToolboxPresentation.ACTION_CREATE_SNAPSHOT,
                model.quickActions.get(0).actionId);
        assertEquals(OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE,
                model.quickActions.get(1).actionId);
        assertEquals(OperationsToolboxPresentation.ACTION_CONNECTION_CHECK,
                model.quickActions.get(2).actionId);
        assertEquals(OperationsToolboxPresentation.ACTION_LIVE_MONITOR,
                model.quickActions.get(3).actionId);
    }

    @Test
    public void recentQuickToolsIgnoreUnknownDuplicatesAndDisabledActions() {
        OperationsToolboxPresentation.Action enabled = new OperationsToolboxPresentation.Action(
                "toolbox.enabled", "可用", "可用工具", true);
        OperationsToolboxPresentation.Action disabled = new OperationsToolboxPresentation.Action(
                "toolbox.disabled", "不可用", "不可用工具", false);
        OperationsToolboxPresentation.ViewModel source = new OperationsToolboxPresentation.ViewModel(
                Arrays.asList(new OperationsToolboxPresentation.Section(
                        "测试", Arrays.asList(enabled, disabled))),
                Arrays.asList(enabled));

        OperationsToolboxPresentation.ViewModel model =
                OperationsToolboxPresentation.withRecentQuickActions(
                        source,
                        Arrays.asList(null, "toolbox.unknown", "toolbox.disabled",
                                "toolbox.enabled", "toolbox.enabled"));

        assertEquals(1, model.quickActionCount());
        assertEquals("toolbox.enabled", model.quickActions.get(0).actionId);
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
                OperationsToolboxPresentation.ACTION_CONNECTION_CHECK));
        assertTrue(OperationsToolboxPresentation.isSupportedAction(
                OperationsToolboxPresentation.ACTION_LIVE_MONITOR));
        assertTrue(OperationsToolboxPresentation.isSupportedAction(
                OperationsToolboxPresentation.ACTION_DEVICE_HEALTH));
        assertTrue(OperationsToolboxPresentation.isSupportedAction(
                OperationsToolboxPresentation.ACTION_SERVICES_HEALTH));
        assertTrue(OperationsToolboxPresentation.isSupportedAction(
                OperationsToolboxPresentation.ACTION_SHOW_WINDOW));
        assertTrue(OperationsToolboxPresentation.isSupportedAction(
                OperationsToolboxPresentation.ACTION_CANCEL_FLOW));
        assertTrue(OperationsToolboxPresentation.isSupportedAction(
                OperationsToolboxPresentation.ACTION_TIMELINE));
        assertFalse(OperationsToolboxPresentation.isSupportedAction("toolbox.unknown"));
    }

    @Test
    public void blankSearchKeepsTheFullToolboxAndQuickActions() {
        OperationsToolboxPresentation.ViewModel model =
                OperationsToolboxPresentation.create();

        assertSame(model, OperationsToolboxPresentation.filter(model, "  "));
    }

    @Test
    public void searchFindsQuickAndDeepActionsWithoutKnowingTheirGroups() {
        OperationsToolboxPresentation.ViewModel model =
                OperationsToolboxPresentation.create();

        OperationsToolboxPresentation.ViewModel connection =
                OperationsToolboxPresentation.filter(model, "连接自检");
        assertEquals(1, connection.actionCount());
        assertEquals(OperationsToolboxPresentation.QUICK_SECTION_TITLE,
                connection.sections.get(0).title);
        assertEquals(OperationsToolboxPresentation.ACTION_CONNECTION_CHECK,
                connection.sections.get(0).actions.get(0).actionId);

        OperationsToolboxPresentation.ViewModel snapshot =
                OperationsToolboxPresentation.filter(model, "主窗口快照");
        assertEquals(1, snapshot.actionCount());
        assertEquals("取证", snapshot.sections.get(0).title);
        assertEquals(OperationsToolboxPresentation.ACTION_CREATE_SNAPSHOT,
                snapshot.sections.get(0).actions.get(0).actionId);
    }

    @Test
    public void searchMatchesDescriptionsSectionsAndAsciiCaseInsensitively() {
        OperationsToolboxPresentation.ViewModel model =
                OperationsToolboxPresentation.create();

        OperationsToolboxPresentation.ViewModel consent =
                OperationsToolboxPresentation.filter(model, "电脑端同意");
        assertEquals(1, consent.actionCount());
        assertEquals(OperationsToolboxPresentation.ACTION_SUPPORT,
                consent.sections.get(0).actions.get(0).actionId);

        OperationsToolboxPresentation.ViewModel recovery =
                OperationsToolboxPresentation.filter(model, "恢复");
        assertEquals(3, recovery.actionCount());
        assertEquals("恢复", recovery.sections.get(0).title);

        OperationsToolboxPresentation.ViewModel mqtt =
                OperationsToolboxPresentation.filter(model, "mqtt");
        assertEquals(1, mqtt.actionCount());
        assertEquals(OperationsToolboxPresentation.ACTION_RESTART_MQTT,
                mqtt.sections.get(0).actions.get(0).actionId);
    }

    @Test
    public void searchReturnsAnEmptyModelForUnknownText() {
        OperationsToolboxPresentation.ViewModel filtered =
                OperationsToolboxPresentation.filter(
                        OperationsToolboxPresentation.create(), "不存在的功能");

        assertTrue(filtered.sections.isEmpty());
        assertEquals(0, filtered.actionCount());
        assertTrue(filtered.quickActions.isEmpty());
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

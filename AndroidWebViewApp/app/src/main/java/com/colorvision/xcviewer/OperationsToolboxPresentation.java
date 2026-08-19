package com.colorvision.xcviewer;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;

final class OperationsToolboxPresentation {
    static final String QUICK_SECTION_TITLE = "快捷工具";
    private static final int QUICK_ACTION_LIMIT = 4;
    static final String ACTION_CONNECTION_CHECK = "toolbox.connection.check";
    static final String ACTION_LIVE_MONITOR = "toolbox.live.monitor";
    static final String ACTION_DEVICE_HEALTH = "toolbox.devices.health";
    static final String ACTION_SERVICES_HEALTH = "toolbox.services.health";
    static final String ACTION_SHOW_WINDOW = "toolbox.window.show";
    static final String ACTION_MINIMIZE_WINDOW = "toolbox.window.minimize";
    static final String ACTION_CANCEL_FLOW = "toolbox.flow.cancel";
    static final String ACTION_RECOVER_MESSAGE = "toolbox.message.recover";
    static final String ACTION_RESTART_MQTT = "toolbox.mqtt.restart";
    static final String ACTION_RESTART_APPLICATION = "toolbox.application.restart";
    static final String ACTION_RECENT_EVENTS = "toolbox.events.recent";
    static final String ACTION_FAILURES = "toolbox.failures";
    static final String ACTION_JOBS = "toolbox.jobs";
    static final String ACTION_AUDIT = "toolbox.audit";
    static final String ACTION_CREATE_DIAGNOSTIC = "toolbox.diagnostic.create";
    static final String ACTION_CREATE_SNAPSHOT = "toolbox.snapshot.create";
    static final String ACTION_SHARE_SUMMARY = "toolbox.summary.share";
    static final String ACTION_SUPPORT = "toolbox.support";
    static final String ACTION_DEPLOYMENT = "toolbox.deployment";
    static final String ACTION_TIMELINE = "toolbox.timeline";

    private OperationsToolboxPresentation() {
    }

    static ViewModel create() {
        List<Section> sections = new ArrayList<>();
        sections.add(section("控制",
                action(ACTION_SHOW_WINDOW,
                        "显示主窗口", "显示或还原当前 ColorVision 主窗口"),
                action(ACTION_MINIMIZE_WINDOW,
                        "最小化主窗口", "最小化当前 ColorVision 主窗口 · 执行前确认"),
                action(ACTION_CANCEL_FLOW,
                        "取消当前检测", "仅在主检测运行且允许取消时开放 · 执行前确认")));
        sections.add(section("诊断",
                action(ACTION_CONNECTION_CHECK,
                        "连接自检", "只读检查网络、安全通道、证书固定与设备签名"),
                action(ACTION_LIVE_MONITOR,
                        "持续观察", "每 10 秒读取一次脱敏运行快照，本次最多保留 30 个样本"),
                action(ACTION_DEVICE_HEALTH,
                        "设备状态", "查看检测设备类型、可用性与异常原因汇总"),
                action(ACTION_SERVICES_HEALTH,
                        "服务健康", "查看服务、依赖与运行状态"),
                action(ACTION_RECENT_EVENTS,
                        "近期事件", "查看已脱敏的近期异常事件"),
                action(ACTION_FAILURES,
                        "崩溃与卡死", "查看崩溃、卡死与转储线索")));
        sections.add(section("恢复",
                action(ACTION_RECOVER_MESSAGE,
                        "恢复消息通道", "按电脑现有配置恢复连接与订阅 · 执行前确认"),
                action(ACTION_RESTART_MQTT,
                        "重启 MQTT", "重新启动消息服务 · 执行前再次确认"),
                action(ACTION_RESTART_APPLICATION,
                        "重启 ColorVision", "仅在检测空闲时重启应用 · 执行前确认")));
        sections.add(section("取证",
                action(ACTION_CREATE_DIAGNOSTIC,
                        "生成诊断包", "创建有界脱敏诊断包 · 需手机确认"),
                action(ACTION_CREATE_SNAPSHOT,
                        "主窗口快照", "仅截取 ColorVision 主窗口 · 需手机确认"),
                action(ACTION_SHARE_SUMMARY,
                        "分享诊断摘要", "分享不含设备身份与密钥的当前摘要")));
        sections.add(section("支持与记录",
                action(ACTION_JOBS,
                        "作业与审批", "查看远程请求、状态与审批边界"),
                action(ACTION_AUDIT,
                        "操作记录", "查看已脱敏的运维审计记录"),
                action(ACTION_SUPPORT,
                        "支持会话", "管理需电脑端同意的支持会话"),
                action(ACTION_DEPLOYMENT,
                        "提交部署确认", "向电脑端提交本次部署结果"),
                action(ACTION_TIMELINE,
                        "运维时间线", "查看连接与后台守护状态变化")));
        List<Action> quickActions = new ArrayList<>();
        quickActions.add(findAction(sections, ACTION_CONNECTION_CHECK));
        quickActions.add(findAction(sections, ACTION_LIVE_MONITOR));
        quickActions.add(findAction(sections, ACTION_DEVICE_HEALTH));
        quickActions.add(findAction(sections, ACTION_RECENT_EVENTS));
        return new ViewModel(
                Collections.unmodifiableList(sections),
                Collections.unmodifiableList(quickActions));
    }

    static boolean isSupportedAction(String actionId) {
        switch (actionId) {
            case ACTION_CONNECTION_CHECK:
            case ACTION_LIVE_MONITOR:
            case ACTION_DEVICE_HEALTH:
            case ACTION_SERVICES_HEALTH:
            case ACTION_SHOW_WINDOW:
            case ACTION_MINIMIZE_WINDOW:
            case ACTION_CANCEL_FLOW:
            case ACTION_RECOVER_MESSAGE:
            case ACTION_RESTART_MQTT:
            case ACTION_RESTART_APPLICATION:
            case ACTION_RECENT_EVENTS:
            case ACTION_FAILURES:
            case ACTION_JOBS:
            case ACTION_AUDIT:
            case ACTION_CREATE_DIAGNOSTIC:
            case ACTION_CREATE_SNAPSHOT:
            case ACTION_SHARE_SUMMARY:
            case ACTION_SUPPORT:
            case ACTION_DEPLOYMENT:
            case ACTION_TIMELINE:
                return true;
            default:
                return false;
        }
    }

    static ViewModel withRecentQuickActions(ViewModel source, List<String> recentActionIds) {
        if (recentActionIds == null || recentActionIds.isEmpty()) {
            return source;
        }
        List<Action> quickActions = new ArrayList<>();
        Set<String> includedActionIds = new HashSet<>();
        for (String actionId : recentActionIds) {
            addQuickAction(source, actionId, quickActions, includedActionIds, true);
        }
        for (Action fallback : source.quickActions) {
            addQuickAction(source, fallback.actionId, quickActions, includedActionIds, false);
        }
        return new ViewModel(
                source.sections,
                Collections.unmodifiableList(quickActions));
    }

    private static void addQuickAction(
            ViewModel source,
            String actionId,
            List<Action> quickActions,
            Set<String> includedActionIds,
            boolean recentAction) {
        if (actionId == null
                || quickActions.size() == QUICK_ACTION_LIMIT
                || includedActionIds.contains(actionId)
                || (recentAction && !isQuickActionEligible(actionId))) {
            return;
        }
        Action action = findAction(source, actionId);
        if (action != null && action.enabled) {
            quickActions.add(action);
            includedActionIds.add(actionId);
        }
    }

    static boolean isQuickActionEligible(String actionId) {
        switch (actionId) {
            case ACTION_CONNECTION_CHECK:
            case ACTION_LIVE_MONITOR:
            case ACTION_DEVICE_HEALTH:
            case ACTION_SERVICES_HEALTH:
            case ACTION_RECENT_EVENTS:
            case ACTION_FAILURES:
            case ACTION_JOBS:
            case ACTION_AUDIT:
            case ACTION_TIMELINE:
                return true;
            default:
                return false;
        }
    }

    private static Action findAction(ViewModel source, String actionId) {
        for (Action action : source.quickActions) {
            if (actionId.equals(action.actionId)) {
                return action;
            }
        }
        return findAction(source.sections, actionId);
    }

    static ViewModel filter(ViewModel source, String query) {
        String normalizedQuery = normalizeSearchText(query);
        if (normalizedQuery.isEmpty()) {
            return source;
        }

        List<Section> matchingSections = new ArrayList<>();
        Set<String> includedActionIds = new HashSet<>();
        List<Action> matchingQuickActions = new ArrayList<>();
        boolean quickSectionMatches = matches(QUICK_SECTION_TITLE, normalizedQuery);
        for (Action action : source.quickActions) {
            if (quickSectionMatches || matches(action, normalizedQuery)) {
                matchingQuickActions.add(action);
                includedActionIds.add(action.actionId);
            }
        }
        if (!matchingQuickActions.isEmpty()) {
            matchingSections.add(new Section(
                    QUICK_SECTION_TITLE,
                    Collections.unmodifiableList(matchingQuickActions)));
        }

        for (Section section : source.sections) {
            boolean sectionMatches = matches(section.title, normalizedQuery);
            List<Action> matchingActions = new ArrayList<>();
            for (Action action : section.actions) {
                if (!includedActionIds.contains(action.actionId)
                        && (sectionMatches || matches(action, normalizedQuery))) {
                    matchingActions.add(action);
                    includedActionIds.add(action.actionId);
                }
            }
            if (!matchingActions.isEmpty()) {
                matchingSections.add(new Section(
                        section.title,
                        Collections.unmodifiableList(matchingActions)));
            }
        }
        return new ViewModel(Collections.unmodifiableList(matchingSections));
    }

    private static boolean matches(Action action, String normalizedQuery) {
        return matches(action.title, normalizedQuery)
                || matches(action.summary, normalizedQuery);
    }

    private static boolean matches(String value, String normalizedQuery) {
        return normalizeSearchText(value).contains(normalizedQuery);
    }

    private static String normalizeSearchText(String value) {
        return value == null ? "" : value.trim().toLowerCase(Locale.ROOT);
    }

    private static Section section(String title, Action... actions) {
        List<Action> values = new ArrayList<>();
        Collections.addAll(values, actions);
        return new Section(title, Collections.unmodifiableList(values));
    }

    private static Action action(String actionId, String title, String summary) {
        return new Action(actionId, title, summary);
    }

    static List<Action> enabledQuickActions(
            List<Section> sections,
            String... actionIds) {
        List<Action> quickActions = new ArrayList<>();
        for (String actionId : actionIds) {
            Action action = findAction(sections, actionId);
            if (action != null && action.enabled) {
                quickActions.add(action);
            }
        }
        return Collections.unmodifiableList(quickActions);
    }

    private static Action findAction(List<Section> sections, String actionId) {
        for (Section section : sections) {
            for (Action action : section.actions) {
                if (actionId.equals(action.actionId)) {
                    return action;
                }
            }
        }
        return null;
    }

    static final class ViewModel {
        final List<Section> sections;
        final List<Action> quickActions;

        ViewModel(List<Section> sections) {
            this(sections, Collections.emptyList());
        }

        ViewModel(List<Section> sections, List<Action> quickActions) {
            this.sections = sections;
            this.quickActions = quickActions;
        }

        int quickActionCount() {
            return quickActions.size();
        }

        int actionCount() {
            int count = 0;
            for (Section section : sections) {
                count += section.actions.size();
            }
            return count;
        }

        int enabledActionCount() {
            int count = 0;
            for (Section section : sections) {
                for (Action action : section.actions) {
                    if (action.enabled) {
                        count++;
                    }
                }
            }
            return count;
        }

        boolean hasUniqueActionIds() {
            Set<String> actionIds = new HashSet<>();
            for (Section section : sections) {
                for (Action action : section.actions) {
                    if (!actionIds.add(action.actionId)) {
                        return false;
                    }
                }
            }
            return true;
        }
    }

    static final class Section {
        final String title;
        final List<Action> actions;

        Section(String title, List<Action> actions) {
            this.title = title;
            this.actions = actions;
        }

        String shortcutLabel() {
            return title.replace("与", "");
        }

        String shortcutAccessibilityLabel() {
            return "跳到" + title + "分组";
        }
    }

    static final class Action {
        final String actionId;
        final String title;
        final String summary;
        final boolean enabled;

        Action(String actionId, String title, String summary) {
            this(actionId, title, summary, true);
        }

        Action(String actionId, String title, String summary, boolean enabled) {
            this.actionId = actionId;
            this.title = title;
            this.summary = summary;
            this.enabled = enabled;
        }

        String accessibilityLabel() {
            String availability = enabled ? "" : "，当前不可用";
            return title + "，" + summary.replace(" · ", "，") + availability;
        }
    }
}

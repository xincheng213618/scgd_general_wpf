package com.colorvision.xcviewer;

import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

final class OperationsDirectToolboxPresentation {
    private OperationsDirectToolboxPresentation() {
    }

    static ViewModel from(JSONObject monitor, boolean loading) {
        JSONObject flow = child(monitor, "flow");
        JSONObject messageChannel = child(monitor, "messageChannel");
        JSONObject mqttService = child(monitor, "mqttService");
        boolean flowAvailable = flow != null && flow.optBoolean("available", false);
        boolean flowActive = flowAvailable && flow.optBoolean("isActive", false);
        boolean canCancelFlow = flowActive && flow.optBoolean("cancelAvailable", false);
        boolean messageAvailable = messageChannel != null
                && messageChannel.optBoolean("available", false);
        String messageState = messageChannel == null
                ? "unavailable" : messageChannel.optString("state", "unavailable");
        boolean canRecoverMessage = messageAvailable
                && messageChannel.optBoolean("attentionRequired", false)
                && ("disconnected".equals(messageState) || "degraded".equals(messageState));
        boolean canRestartMqtt = OperationsRelayPolicy.canRestartMqttService(
                true,
                true,
                flowAvailable,
                flowActive,
                mqttService != null && mqttService.optBoolean("available", false),
                mqttService == null ? "unknown" : mqttService.optString("status", "unknown"),
                mqttService != null && mqttService.optBoolean("maintenanceSupported", false));
        boolean canRestartApplication = flowAvailable && !flowActive;

        OperationsToolboxPresentation.ViewModel source = OperationsToolboxPresentation.create();
        List<OperationsToolboxPresentation.Section> sections = new ArrayList<>();
        for (OperationsToolboxPresentation.Section section : source.sections) {
            List<OperationsToolboxPresentation.Action> actions = new ArrayList<>();
            for (OperationsToolboxPresentation.Action action : section.actions) {
                actions.add(withLiveAvailability(
                        action,
                        flowAvailable,
                        flowActive,
                        canCancelFlow,
                        messageAvailable,
                        canRecoverMessage,
                        canRestartMqtt,
                        canRestartApplication));
            }
            sections.add(new OperationsToolboxPresentation.Section(
                    section.title, Collections.unmodifiableList(actions)));
        }

        List<OperationsToolboxPresentation.Section> immutableSections =
                Collections.unmodifiableList(sections);
        OperationsToolboxPresentation.ViewModel toolbox =
                new OperationsToolboxPresentation.ViewModel(
                        immutableSections,
                        OperationsToolboxPresentation.enabledQuickActions(
                                immutableSections,
                                OperationsToolboxPresentation.ACTION_CONNECTION_CHECK,
                                OperationsToolboxPresentation.ACTION_LIVE_MONITOR,
                                OperationsToolboxPresentation.ACTION_DEVICE_HEALTH,
                                OperationsToolboxPresentation.ACTION_RECENT_EVENTS));
        String stateLabel = toolbox.enabledActionCount() + " / " + toolbox.actionCount()
                + " 项可用 · " + (monitor != null
                        ? "运行状态已核对"
                        : loading ? "正在核对运行状态" : "仅显示安全可用项");
        String summary = monitor != null
                ? "可用性来自刚刚读取的主检测、消息通道和固定 MQTT 服务状态；"
                        + "改变运行状态的操作仍会再次确认。"
                : loading
                        ? "正在读取实时状态；取消检测、恢复消息和重启操作暂不开放。"
                        : "实时状态暂不可用；取消检测、恢复消息和重启操作保持关闭。";
        return new ViewModel(stateLabel, summary, toolbox);
    }

    private static OperationsToolboxPresentation.Action withLiveAvailability(
            OperationsToolboxPresentation.Action action,
            boolean flowAvailable,
            boolean flowActive,
            boolean canCancelFlow,
            boolean messageAvailable,
            boolean canRecoverMessage,
            boolean canRestartMqtt,
            boolean canRestartApplication) {
        switch (action.actionId) {
            case OperationsToolboxPresentation.ACTION_CANCEL_FLOW:
                return action(
                        action,
                        canCancelFlow
                                ? "主检测正在运行且允许取消 · 执行前确认"
                                : flowAvailable
                                        ? "当前没有可取消的主检测"
                                        : "需要先读取主检测运行状态",
                        canCancelFlow);
            case OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE:
                return action(
                        action,
                        canRecoverMessage
                                ? "消息通道需要关注，可按电脑现有配置恢复 · 执行前确认"
                                : messageAvailable
                                        ? "消息通道当前健康，无需恢复"
                                        : "需要先读取消息通道状态",
                        canRecoverMessage);
            case OperationsToolboxPresentation.ACTION_RESTART_MQTT:
                return action(
                        action,
                        canRestartMqtt
                                ? "主检测空闲且固定 MQTT 服务可维护 · 执行前再次确认"
                                : flowActive
                                        ? "主检测运行时不允许重启 MQTT"
                                        : "需要检测空闲且固定 MQTT 服务处于可维护状态",
                        canRestartMqtt);
            case OperationsToolboxPresentation.ACTION_RESTART_APPLICATION:
                return action(
                        action,
                        canRestartApplication
                                ? "主检测已确认空闲 · 执行前确认"
                                : flowActive
                                        ? "主检测运行时不允许重启 ColorVision"
                                        : "需要先确认主检测处于空闲状态",
                        canRestartApplication);
            default:
                return action;
        }
    }

    private static OperationsToolboxPresentation.Action action(
            OperationsToolboxPresentation.Action source,
            String summary,
            boolean enabled) {
        return new OperationsToolboxPresentation.Action(
                source.actionId, source.title, summary, enabled);
    }

    private static JSONObject child(JSONObject parent, String name) {
        return parent == null ? null : parent.optJSONObject(name);
    }

    static final class ViewModel {
        final String stateLabel;
        final String summary;
        final OperationsToolboxPresentation.ViewModel toolbox;

        ViewModel(
                String stateLabel,
                String summary,
                OperationsToolboxPresentation.ViewModel toolbox) {
            this.stateLabel = stateLabel;
            this.summary = summary;
            this.toolbox = toolbox;
        }
    }
}

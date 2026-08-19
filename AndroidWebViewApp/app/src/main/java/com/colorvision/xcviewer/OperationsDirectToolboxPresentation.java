package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

final class OperationsDirectToolboxPresentation {
    private OperationsDirectToolboxPresentation() {
    }

    static ViewModel from(JSONObject monitor, JSONObject capabilityCatalog, boolean loading) {
        Set<String> availableCapabilities = availableCapabilities(capabilityCatalog);
        boolean capabilityCatalogAvailable = availableCapabilities != null;
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
                String[] requirements = capabilityRequirements(action.actionId);
                if (requirements != null
                        && !hasAllCapabilities(availableCapabilities, requirements)) {
                    actions.add(action(
                            action,
                            capabilityCatalogAvailable
                                    ? "当前电脑未开放此工具所需能力"
                                    : loading
                                            ? "正在确认电脑是否支持此工具"
                                            : "未能从电脑确认此工具所需能力",
                            false));
                } else {
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
        String verification;
        if (!capabilityCatalogAvailable) {
            verification = loading ? "正在核对电脑能力" : "电脑能力目录不可用";
        } else if (monitor != null) {
            verification = "电脑能力与运行状态已核对";
        } else if (loading) {
            verification = "电脑能力已核对，正在读取运行状态";
        } else {
            verification = "电脑能力已核对，运行状态不可用";
        }
        String stateLabel = toolbox.enabledActionCount() + " / " + toolbox.actionCount()
                + " 项可用 · " + verification;
        String summary;
        if (!capabilityCatalogAvailable) {
            summary = loading
                    ? "正在读取电脑发布的固定能力目录与实时状态；尚未确认的电脑端工具暂不开放。"
                    : "未能读取电脑发布的固定能力目录；为避免出现必然失败的入口，"
                            + "仅保留本机连接自检与运维时间线。";
        } else if (monitor != null) {
            summary = "工具必须同时由电脑固定能力目录明确开放，并满足刚刚读取的主检测、"
                    + "消息通道和固定 MQTT 服务状态；改变运行状态的操作仍会再次确认。";
        } else if (loading) {
            summary = "电脑能力目录已核对；正在读取实时状态，取消检测、恢复消息和重启操作暂不开放。";
        } else {
            summary = "电脑能力目录已核对，但实时状态暂不可用；取消检测、恢复消息和重启操作保持关闭。";
        }
        return new ViewModel(stateLabel, summary, toolbox);
    }

    private static Set<String> availableCapabilities(JSONObject capabilityCatalog) {
        JSONArray capabilities = capabilityCatalog == null
                ? null : capabilityCatalog.optJSONArray("capabilities");
        if (capabilities == null) {
            return null;
        }
        Set<String> available = new HashSet<>();
        for (int index = 0; index < capabilities.length(); index++) {
            JSONObject capability = capabilities.optJSONObject(index);
            if (capability == null || !capability.optBoolean("available", false)
                    || !discoverableOnAndroid(capability.optJSONArray("discoverableOn"))) {
                continue;
            }
            String id = capability.optString("id", "").trim();
            if (!id.isEmpty()) {
                available.add(id);
            }
        }
        return available;
    }

    private static boolean discoverableOnAndroid(JSONArray clients) {
        if (clients == null) {
            return false;
        }
        for (int index = 0; index < clients.length(); index++) {
            if ("android".equals(clients.optString(index, ""))) {
                return true;
            }
        }
        return false;
    }

    private static boolean hasAllCapabilities(Set<String> available, String[] requirements) {
        if (available == null) {
            return false;
        }
        for (String requirement : requirements) {
            if (!available.contains(requirement)) {
                return false;
            }
        }
        return true;
    }

    private static String[] capabilityRequirements(String actionId) {
        switch (actionId) {
            case OperationsToolboxPresentation.ACTION_CONNECTION_CHECK:
            case OperationsToolboxPresentation.ACTION_TIMELINE:
                return null;
            case OperationsToolboxPresentation.ACTION_LIVE_MONITOR:
                return requirements("ops.monitor.read");
            case OperationsToolboxPresentation.ACTION_DEVICE_HEALTH:
                return requirements("ops.devices.health.read");
            case OperationsToolboxPresentation.ACTION_SERVICES_HEALTH:
                return requirements("ops.services.health.read");
            case OperationsToolboxPresentation.ACTION_SHOW_WINDOW:
                return requirements("ops.window.show");
            case OperationsToolboxPresentation.ACTION_MINIMIZE_WINDOW:
                return requirements("ops.window.minimize");
            case OperationsToolboxPresentation.ACTION_CANCEL_FLOW:
                return requirements("ops.flow.cancel");
            case OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE:
                return requirements("ops.messaging.reconnect");
            case OperationsToolboxPresentation.ACTION_RESTART_MQTT:
                return requirements("ops.service.restart");
            case OperationsToolboxPresentation.ACTION_RESTART_APPLICATION:
                return requirements("ops.application.restart");
            case OperationsToolboxPresentation.ACTION_RECENT_EVENTS:
                return requirements("ops.diagnostics.events.read");
            case OperationsToolboxPresentation.ACTION_FAILURES:
                return requirements("ops.diagnostics.failures.read");
            case OperationsToolboxPresentation.ACTION_JOBS:
                return requirements("ops.jobs.manage");
            case OperationsToolboxPresentation.ACTION_AUDIT:
                return requirements("ops.audit.read");
            case OperationsToolboxPresentation.ACTION_CREATE_DIAGNOSTIC:
                return requirements(
                        "ops.diagnostics.bundle.create",
                        "ops.diagnostics.bundle.download");
            case OperationsToolboxPresentation.ACTION_CREATE_SNAPSHOT:
                return requirements(
                        "ops.window.snapshot.capture",
                        "ops.window.snapshot.download");
            case OperationsToolboxPresentation.ACTION_SHARE_SUMMARY:
                return requirements(
                        "ops.status.read",
                        "ops.diagnostics.events.read",
                        "ops.diagnostics.performance.read",
                        "ops.flow.runtime.read",
                        "ops.services.health.read",
                        "ops.devices.health.read",
                        "ops.messaging.health.read");
            case OperationsToolboxPresentation.ACTION_SUPPORT:
                return requirements(
                        "ops.support.session.request",
                        "ops.support.message.exchange");
            case OperationsToolboxPresentation.ACTION_DEPLOYMENT:
                return requirements("ops.deployment.receipt.create");
            default:
                return requirements("unsupported.toolbox.action");
        }
    }

    private static String[] requirements(String... capabilityIds) {
        return capabilityIds;
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

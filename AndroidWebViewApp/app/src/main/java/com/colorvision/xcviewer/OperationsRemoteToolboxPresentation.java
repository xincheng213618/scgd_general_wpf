package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

final class OperationsRemoteToolboxPresentation {
    static final String ACTION_RECENT_REMOTE_TASK = "toolbox.remote.task.recent";

    private OperationsRemoteToolboxPresentation() {
    }

    static ViewModel from(
            JSONObject response,
            boolean hasRecentRemoteTask,
            int androidSdk,
            long nowMilliseconds) {
        JSONObject host = response == null ? null : response.optJSONObject("host");
        JSONObject snapshot = host == null ? null : host.optJSONObject("snapshot");
        JSONObject monitor = snapshot == null ? null : snapshot.optJSONObject("monitor");
        JSONObject flow = monitor == null ? null : monitor.optJSONObject("flow");
        JSONObject mqttService = monitor == null ? null : monitor.optJSONObject("mqttService");
        JSONArray capabilities = host == null ? null : host.optJSONArray("capabilities");
        boolean hostFresh = host != null && OperationsRelayPolicy.isHostFresh(
                host.optLong("signedAt", 0L), nowMilliseconds);
        boolean flowAvailable = flow != null && flow.optBoolean("available", false);
        boolean flowActive = flowAvailable && flow.optBoolean("isActive", false);

        boolean canShowWindow = OperationsRelayPolicy.canControlWindow(
                contains(capabilities, OperationsRelayPolicy.CAPABILITY_SHOW_WINDOW),
                hostFresh);
        boolean canMinimizeWindow = OperationsRelayPolicy.canControlWindow(
                contains(capabilities, OperationsRelayPolicy.CAPABILITY_MINIMIZE_WINDOW),
                hostFresh);
        boolean canCancelFlow = contains(
                capabilities, OperationsRelayPolicy.CAPABILITY_CANCEL_FLOW)
                && hostFresh
                && flowAvailable
                && flowActive
                && flow.optBoolean("cancelAvailable", false);
        boolean canRecoverMessage = contains(
                capabilities, OperationsRelayPolicy.CAPABILITY_RECOVER_MESSAGE_CHANNEL)
                && hostFresh;
        boolean canRestartMqtt = OperationsRelayPolicy.canRestartMqttService(
                contains(capabilities, OperationsRelayPolicy.CAPABILITY_RESTART_MQTT),
                hostFresh,
                flowAvailable,
                flowActive,
                mqttService != null && mqttService.optBoolean("available", false),
                mqttService == null ? "unknown" : mqttService.optString("status", "unknown"),
                mqttService != null && mqttService.optBoolean("maintenanceSupported", false));
        boolean canRestartApplication = contains(
                capabilities, OperationsRelayPolicy.CAPABILITY_RESTART_APPLICATION)
                && hostFresh
                && flowAvailable
                && !flowActive;
        boolean canRequestDiagnostics = contains(
                capabilities, OperationsRelayPolicy.CAPABILITY_REQUEST_DIAGNOSTICS);
        boolean canReadFailures = OperationsRelayPolicy.canReadFailureEvidence(
                contains(capabilities, OperationsRelayPolicy.CAPABILITY_READ_FAILURE_EVIDENCE),
                hostFresh);
        boolean canCaptureSnapshot = OperationsRelayPolicy.canCaptureWindowSnapshot(
                contains(capabilities, OperationsRelayPolicy.CAPABILITY_CAPTURE_WINDOW_SNAPSHOT),
                hostFresh,
                androidSdk);

        List<OperationsToolboxPresentation.Section> sections = new ArrayList<>();
        sections.add(section("窗口与检测",
                action(
                        OperationsToolboxPresentation.ACTION_SHOW_WINDOW,
                        "显示主窗口",
                        canShowWindow
                                ? "显示或还原当前电脑的 ColorVision 主窗口"
                                : "等待电脑最新签名状态与窗口控制能力",
                        canShowWindow),
                action(
                        OperationsToolboxPresentation.ACTION_MINIMIZE_WINDOW,
                        "最小化主窗口",
                        canMinimizeWindow
                                ? "最小化当前电脑的 ColorVision 主窗口 · 执行前确认"
                                : "等待电脑最新签名状态与窗口控制能力",
                        canMinimizeWindow),
                action(
                        OperationsToolboxPresentation.ACTION_CANCEL_FLOW,
                        "取消当前检测",
                        canCancelFlow
                                ? "电脑确认主检测正在运行且允许取消 · 执行前确认"
                                : "需要最新状态确认主检测正在运行且允许取消",
                        canCancelFlow)));
        sections.add(section("恢复",
                action(
                        OperationsToolboxPresentation.ACTION_RECOVER_MESSAGE,
                        "恢复消息通道",
                        canRecoverMessage
                                ? "按电脑现有配置恢复连接与订阅 · 执行前确认"
                                : "等待电脑最新签名状态与消息恢复能力",
                        canRecoverMessage),
                action(
                        OperationsToolboxPresentation.ACTION_RESTART_MQTT,
                        "重启 MQTT",
                        canRestartMqtt
                                ? "电脑确认检测空闲且固定服务可维护 · 执行前确认"
                                : "需要检测空闲、服务可维护与电脑最新签名状态",
                        canRestartMqtt),
                action(
                        OperationsToolboxPresentation.ACTION_RESTART_APPLICATION,
                        "重启 ColorVision",
                        canRestartApplication
                                ? "电脑确认主检测空闲 · 执行前确认"
                                : "需要电脑最新状态确认主检测空闲",
                        canRestartApplication)));
        sections.add(section("诊断与取证",
                action(
                        OperationsToolboxPresentation.ACTION_CREATE_DIAGNOSTIC,
                        hostFresh ? "请求远程诊断" : "排队请求诊断",
                        canRequestDiagnostics
                                ? hostFresh
                                        ? "提交只读诊断请求并核验电脑签名结果"
                                        : "电脑上线后处理；固定中继最多等待 15 分钟"
                                : "电脑尚未发布只读诊断能力",
                        canRequestDiagnostics),
                action(
                        OperationsToolboxPresentation.ACTION_FAILURES,
                        "崩溃与卡死线索",
                        canReadFailures
                                ? "读取七天内固定类别的电脑签名聚合线索"
                                : "等待电脑最新签名状态与只读线索能力",
                        canReadFailures),
                action(
                        OperationsToolboxPresentation.ACTION_CREATE_SNAPSHOT,
                        "主窗口快照",
                        snapshotSummary(canCaptureSnapshot, androidSdk),
                        canCaptureSnapshot)));
        sections.add(section("记录",
                action(
                        ACTION_RECENT_REMOTE_TASK,
                        "最近远程请求",
                        hasRecentRemoteTask
                                ? "核验最近一次请求的电脑签名状态与收据"
                                : "本机还没有可核验的远程请求",
                        hasRecentRemoteTask),
                action(
                        OperationsToolboxPresentation.ACTION_TIMELINE,
                        "运维时间线",
                        "查看本机保存的连接与后台守护状态变化",
                        true)));

        OperationsToolboxPresentation.ViewModel toolbox =
                new OperationsToolboxPresentation.ViewModel(
                        Collections.unmodifiableList(sections));
        String stateLabel = toolbox.enabledActionCount()
                + " / " + toolbox.actionCount() + " 项当前可用";
        String summary = hostFresh
                ? "能力来自当前电脑刚刚签名的状态；改变运行状态的操作仍会再次确认。"
                : "电脑尚未提供新鲜签名状态；窗口、恢复与取证操作已暂停，"
                        + "只保留本机记录和电脑已发布的可排队诊断。";
        return new ViewModel(hostFresh, stateLabel, summary, toolbox);
    }

    private static String snapshotSummary(boolean enabled, int androidSdk) {
        if (enabled) {
            return "端到端加密采集当前 ColorVision 主窗口 · 执行前确认";
        }
        if (androidSdk < OperationsRemoteWindowSnapshot.MINIMUM_ANDROID_SDK) {
            return "需要 Android 12 或更高版本；现场局域网快照不受影响";
        }
        return "等待电脑最新签名状态与端到端快照能力";
    }

    private static OperationsToolboxPresentation.Section section(
            String title, OperationsToolboxPresentation.Action... actions) {
        List<OperationsToolboxPresentation.Action> values = new ArrayList<>();
        Collections.addAll(values, actions);
        return new OperationsToolboxPresentation.Section(
                title, Collections.unmodifiableList(values));
    }

    private static OperationsToolboxPresentation.Action action(
            String actionId, String title, String summary, boolean enabled) {
        return new OperationsToolboxPresentation.Action(
                actionId, title, summary, enabled);
    }

    private static boolean contains(JSONArray values, String expected) {
        if (values == null) {
            return false;
        }
        for (int index = 0; index < values.length(); index++) {
            if (expected.equals(values.optString(index))) {
                return true;
            }
        }
        return false;
    }

    static final class ViewModel {
        final boolean hostFresh;
        final String stateLabel;
        final String summary;
        final OperationsToolboxPresentation.ViewModel toolbox;

        ViewModel(
                boolean hostFresh,
                String stateLabel,
                String summary,
                OperationsToolboxPresentation.ViewModel toolbox) {
            this.hostFresh = hostFresh;
            this.stateLabel = stateLabel;
            this.summary = summary;
            this.toolbox = toolbox;
        }
    }
}

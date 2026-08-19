package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

final class OperationsServiceHealthPresentation {
    static final int TONE_HEALTHY = 0;
    static final int TONE_ATTENTION = 1;
    static final int TONE_UNAVAILABLE = 2;
    private static final String SERVICE_HOST = "colorvision-service-host";
    private static final String MQTT_BROKER = "mqtt-broker";
    private static final int MAXIMUM_SERVICES = 2;

    private OperationsServiceHealthPresentation() {
    }

    static ViewModel from(JSONObject payload, TimeFormatter timeFormatter) {
        if (payload == null || !payload.optBoolean("available", false)) {
            return new ViewModel(
                    false,
                    "服务状态不可用",
                    "当前无法读取固定白名单服务状态。",
                    "恢复安全直连后可重新读取；不会仅凭日志建议服务维护。",
                    "仅报告固定白名单服务的规范化状态；不返回服务账户、可执行路径、启动参数或机器标识。",
                    TONE_UNAVAILABLE,
                    0,
                    false,
                    Collections.emptyList());
        }

        List<Service> services = new ArrayList<>();
        Set<String> serviceIds = new HashSet<>();
        boolean canRestartMqtt = false;
        JSONArray sourceServices = payload.optJSONArray("services");
        if (sourceServices != null) {
            for (int index = 0;
                    index < sourceServices.length() && services.size() < MAXIMUM_SERVICES;
                    index++) {
                JSONObject source = sourceServices.optJSONObject(index);
                if (source == null) {
                    continue;
                }
                String serviceId = source.optString("serviceId", "");
                String title = serviceTitle(serviceId);
                if (title.isEmpty() || !serviceIds.add(serviceId)) {
                    continue;
                }
                boolean healthy = source.optBoolean("healthy", false);
                String status = source.optString("status", "unknown");
                boolean maintenanceSupported = MQTT_BROKER.equals(serviceId)
                        && source.optBoolean("maintenanceSupported", false);
                canRestartMqtt |= maintenanceSupported
                        && !healthy
                        && ("stopped".equals(status) || "paused".equals(status));
                services.add(new Service(
                        title,
                        statusLabel(status),
                        healthy ? "正常" : "需关注",
                        sourceLabel(source.optString("statusSource", "")),
                        timeFormatter.format(source.optString("observedAt", "")),
                        maintenanceSupported
                                ? "手机维护 · 确认后可重启固定 MQTT 服务"
                                : "手机维护 · 不提供远程操作",
                        maintenanceSupported,
                        healthy ? TONE_HEALTHY : TONE_ATTENTION));
            }
        }

        int attentionCount = 0;
        for (Service service : services) {
            if (service.tone == TONE_ATTENTION) {
                attentionCount++;
            }
        }
        int healthyCount = services.size() - attentionCount;
        int tone = attentionCount > 0 ? TONE_ATTENTION : TONE_HEALTHY;
        String stateLabel;
        String summaryLabel;
        String countLabel;
        if (services.isEmpty()) {
            stateLabel = "未发现适用服务";
            summaryLabel = "当前没有适用的本机白名单服务。";
            countLabel = "没有可显示的服务状态。";
        } else if (attentionCount > 0) {
            stateLabel = services.size() + " 项服务 · " + attentionCount + " 项需关注";
            summaryLabel = "有白名单服务需要关注";
            countLabel = "需关注 " + attentionCount + " · 正常 " + healthyCount;
        } else {
            stateLabel = services.size() + " 项服务 · 均正常";
            summaryLabel = "白名单服务均正常";
            countLabel = "正常 " + healthyCount + " · 需关注 0";
        }

        return new ViewModel(
                true,
                stateLabel,
                summaryLabel,
                countLabel,
                payload.optString("privacyNotice",
                        "仅报告固定白名单服务的规范化状态；不返回服务账户、可执行路径、启动参数或机器标识。"),
                tone,
                attentionCount,
                canRestartMqtt,
                Collections.unmodifiableList(services));
    }

    private static String serviceTitle(String serviceId) {
        if (SERVICE_HOST.equals(serviceId)) {
            return "ColorVision 后台服务";
        }
        if (MQTT_BROKER.equals(serviceId)) {
            return "MQTT 消息服务";
        }
        return "";
    }

    static String statusLabel(String value) {
        switch (value) {
            case "running": return "运行中";
            case "stopped": return "已停止";
            case "paused": return "已暂停";
            case "start_pending": return "正在启动";
            case "stop_pending": return "正在停止";
            case "pause_pending": return "正在暂停";
            case "continue_pending": return "正在恢复";
            case "not_installed": return "未安装";
            case "not_applicable": return "使用远程端点，本机不适用";
            default: return "未知";
        }
    }

    private static String sourceLabel(String value) {
        if ("windows-service-control-manager".equals(value)) {
            return "Windows 服务控制管理器";
        }
        if ("application-config".equals(value)) {
            return "应用配置";
        }
        return "受限状态提供程序";
    }

    interface TimeFormatter {
        String format(String value);
    }

    static final class ViewModel {
        final boolean available;
        final String stateLabel;
        final String summaryLabel;
        final String countLabel;
        final String privacyNotice;
        final int tone;
        final int attentionCount;
        final boolean canRestartMqtt;
        final List<Service> services;

        ViewModel(
                boolean available,
                String stateLabel,
                String summaryLabel,
                String countLabel,
                String privacyNotice,
                int tone,
                int attentionCount,
                boolean canRestartMqtt,
                List<Service> services) {
            this.available = available;
            this.stateLabel = stateLabel;
            this.summaryLabel = summaryLabel;
            this.countLabel = countLabel;
            this.privacyNotice = privacyNotice;
            this.tone = tone;
            this.attentionCount = attentionCount;
            this.canRestartMqtt = canRestartMqtt;
            this.services = services;
        }

        String servicesSectionLabel() {
            return services.isEmpty() ? "白名单服务" : "白名单服务 · " + services.size();
        }

        String plainText() {
            StringBuilder text = new StringBuilder(summaryLabel);
            if (!countLabel.isEmpty()) {
                text.append("\n").append(countLabel);
            }
            for (Service service : services) {
                text.append("\n\n").append(service.title)
                        .append("\n状态：").append(service.statusLabel)
                        .append(" · ").append(service.healthLabel)
                        .append("\n来源：").append(service.sourceLabel);
                if (!service.observedAt.isEmpty()) {
                    text.append("\n观测时间：").append(service.observedAt);
                }
                text.append("\n").append(service.maintenanceLabel);
            }
            text.append("\n\n").append(privacyNotice);
            return text.toString();
        }
    }

    static final class Service {
        final String title;
        final String statusLabel;
        final String healthLabel;
        final String sourceLabel;
        final String observedAt;
        final String maintenanceLabel;
        final boolean maintenanceSupported;
        final int tone;

        Service(
                String title,
                String statusLabel,
                String healthLabel,
                String sourceLabel,
                String observedAt,
                String maintenanceLabel,
                boolean maintenanceSupported,
                int tone) {
            this.title = title;
            this.statusLabel = statusLabel;
            this.healthLabel = healthLabel;
            this.sourceLabel = sourceLabel;
            this.observedAt = observedAt;
            this.maintenanceLabel = maintenanceLabel;
            this.maintenanceSupported = maintenanceSupported;
            this.tone = tone;
        }

        String statusSummary() {
            return statusLabel + " · " + healthLabel;
        }

        String observationSummary() {
            return observedAt.isEmpty()
                    ? sourceLabel
                    : sourceLabel + " · " + observedAt;
        }

        String accessibilityLabel() {
            return title + "，" + statusLabel + "，" + healthLabel + "，"
                    + observationSummary().replace(" · ", "，") + "，"
                    + maintenanceLabel.replace(" · ", "，");
        }
    }
}

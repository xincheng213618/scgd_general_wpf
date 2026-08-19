package com.colorvision.xcviewer;

final class OperationsConnectionOverview {
    private OperationsConnectionOverview() {
    }

    static String pageStatus(OperationsConnectionOverviewProbe.Result result) {
        if (result == null
                || result.channel == OperationsConnectionOverviewProbe.Channel.CHECKING) {
            return "正在确认安全连接…";
        }
        if (result.channel == OperationsConnectionOverviewProbe.Channel.DIRECT) {
            return "安全连接可用 · 现场直连";
        }
        if (result.channel == OperationsConnectionOverviewProbe.Channel.RELAY) {
            return result.relayHostFresh
                    ? "安全连接可用 · 固定中继"
                    : "固定中继可用 · 电脑尚未上线";
        }
        return "需要处理 · 两种连接均不可达";
    }

    static String summary(
            String activeProfileLabel,
            String preferredChannel,
            OperationsConnectionOverviewProbe.Result result,
            int profileCount,
            int maximumProfiles) {
        String value = "当前电脑 " + safe(activeProfileLabel, "未命名电脑")
                + "\n当前通道 " + activeChannelLabel(result)
                + " · 首选 " + safe(preferredChannel, "正在确认")
                + "\n已配对电脑 " + Math.max(0, profileCount)
                + " / " + Math.max(0, maximumProfiles);
        if (result != null
                && result.channel == OperationsConnectionOverviewProbe.Channel.UNAVAILABLE) {
            return value + "\n配对资料已保留 · 请运行连接自检";
        }
        return value;
    }

    static boolean showsFleetTools(int profileCount) {
        return profileCount > 1;
    }

    static String connectionNote() {
        return "安全通道始终使用设备密钥和 TLS 证书固定。"
                + "首选通道不可用时自动安全回退，恢复后切回；"
                + "固定中继地址由应用内置，不能修改。";
    }

    static String removalNote() {
        return "仅当不再使用当前电脑时移除。此操作会删除手机中的独立密钥、"
                + "证书指纹、时间线和最近任务；其他电脑不受影响。";
    }

    static String activeChannelLabel(OperationsConnectionOverviewProbe.Result result) {
        if (result == null
                || result.channel == OperationsConnectionOverviewProbe.Channel.CHECKING) {
            return "正在确认";
        }
        if (result.channel == OperationsConnectionOverviewProbe.Channel.DIRECT) {
            return "现场直连";
        }
        if (result.channel == OperationsConnectionOverviewProbe.Channel.RELAY) {
            return result.relayHostFresh ? "固定中继" : "固定中继（电脑未上线）";
        }
        return "暂不可达";
    }

    private static String safe(String value, String fallback) {
        return value == null || value.trim().isEmpty() ? fallback : value.trim();
    }
}

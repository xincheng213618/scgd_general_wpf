package com.colorvision.xcviewer;

final class OperationsDashboardStatusFormatter {
    static final int TONE_DEFAULT = 0;
    static final int TONE_ACTIVE = 1;
    static final int TONE_ATTENTION = 2;
    static final int TONE_MUTED = 3;

    static final class Item {
        final String title;
        final String summary;
        final int tone;

        Item(String title, String summary, int tone) {
            this.title = title;
            this.summary = summary;
            this.tone = tone;
        }

        String accessibilityLabel() {
            return title + "，" + summary + "，查看详情";
        }
    }

    private OperationsDashboardStatusFormatter() {
    }

    static String sectionTitle(boolean remote, boolean fresh) {
        if (!remote) {
            return "实时状态";
        }
        return fresh ? "电脑签名状态" : "上次电脑签名状态";
    }

    static String sectionCaption(boolean remote, boolean fresh) {
        if (!remote) {
            return "来自当前电脑；点击任一项查看详细状态。";
        }
        return fresh
                ? "电脑刚刚签名返回；点击任一项查看签名详情。"
                : "快照已过期，仅供参考；点击任一项查看签名详情。";
    }

    static Item loading(String title) {
        return new Item(title, "读取中…", TONE_MUTED);
    }

    static Item unavailable(String title) {
        return new Item(title, "暂不可用", TONE_MUTED);
    }

    static Item application(
            boolean available,
            String version,
            boolean windowExists,
            boolean windowVisible,
            String windowState,
            double memoryMb) {
        if (!available) {
            return unavailable("应用");
        }
        String safeVersion = version == null || version.trim().isEmpty()
                ? "版本未知" : version.trim();
        String window = !windowExists ? "窗口不可用"
                : windowVisible ? "窗口可见"
                : "Minimized".equalsIgnoreCase(windowState) ? "已最小化" : "窗口未显示";
        String memory = memoryMb > 0 ? " · " + Math.round(memoryMb) + " MB" : "";
        return new Item("应用", safeVersion + " · " + window + memory,
                windowExists ? TONE_DEFAULT : TONE_ATTENTION);
    }

    static Item flow(boolean available, boolean active, String phase) {
        if (!available) {
            return unavailable("检测");
        }
        if (!active) {
            return new Item("检测", "空闲", TONE_DEFAULT);
        }
        if ("paused".equals(phase)) {
            return new Item("检测", "已暂停", TONE_ATTENTION);
        }
        if ("cancelling".equals(phase)) {
            return new Item("检测", "正在取消", TONE_ACTIVE);
        }
        return new Item("检测", "运行中", TONE_ACTIVE);
    }

    static String flowCancellation(boolean available, boolean active, boolean cancelAvailable,
            boolean inFlight) {
        if (inFlight) {
            return "正在取消检测…";
        }
        if (!available) {
            return "取消检测（暂不可用）";
        }
        if (!active) {
            return "当前无检测";
        }
        return cancelAvailable ? "取消当前检测" : "检测运行中（不可取消）";
    }

    static boolean flowCancellationEnabled(boolean available, boolean active, boolean cancelAvailable,
            boolean inFlight) {
        return available && active && cancelAvailable && !inFlight;
    }

    static Item devices(boolean available, boolean configured, int ready, int busy, int attention,
            int total, String attentionSummary) {
        if (!available) {
            return unavailable("设备");
        }
        if (!configured || total <= 0) {
            return new Item("设备", "未加载", TONE_MUTED);
        }
        if (attention > 0) {
            String summary = attentionSummary == null ? "" : attentionSummary.trim();
            return new Item("设备", summary.isEmpty() ? "需关注 " + attention : summary,
                    TONE_ATTENTION);
        }
        if (busy > 0) {
            return new Item("设备", "忙碌 " + busy + " / " + total, TONE_ACTIVE);
        }
        return new Item("设备", "就绪 " + ready + " / " + total, TONE_DEFAULT);
    }

    static Item messageChannel(boolean available, boolean connected, boolean subscriptionsReady,
            int activeSubscriptions, int registeredSubscriptions) {
        if (!available) {
            return unavailable("消息");
        }
        if (!connected) {
            return new Item("消息", "未连接", TONE_ATTENTION);
        }
        if (!subscriptionsReady) {
            return new Item("消息",
                    "订阅 " + activeSubscriptions + " / " + registeredSubscriptions,
                    TONE_ATTENTION);
        }
        return new Item("消息", "已连接", TONE_DEFAULT);
    }

    static Item alerts(boolean available, int warningCount, int errorCount, int criticalCount) {
        if (!available) {
            return unavailable("告警");
        }
        if (criticalCount > 0) {
            return new Item("告警", "严重 " + criticalCount, TONE_ATTENTION);
        }
        if (errorCount > 0) {
            return new Item("告警", "错误 " + errorCount, TONE_ATTENTION);
        }
        if (warningCount > 0) {
            return new Item("告警", "警告 " + warningCount, TONE_ATTENTION);
        }
        return new Item("告警", "暂无异常", TONE_DEFAULT);
    }

    static Item performance(boolean available, double cpuPercent, String uiState) {
        if (!available) {
            return unavailable("性能");
        }
        String cpu = "CPU " + Math.round(Math.max(0, cpuPercent)) + "%";
        if ("unavailable".equals(uiState)) {
            return new Item("性能", cpu + " · 界面未知", TONE_MUTED);
        }
        String responsiveness = "unresponsive".equals(uiState) ? "无响应"
                : "slow".equals(uiState) ? "偏慢" : "正常";
        int tone = "unresponsive".equals(uiState) || "slow".equals(uiState)
                ? TONE_ATTENTION : TONE_DEFAULT;
        return new Item("性能", cpu + " · " + responsiveness, tone);
    }

    static Item recovery(boolean available, boolean supported, boolean registered,
            boolean automaticWatchdogActive) {
        if (!available) {
            return unavailable("恢复");
        }
        if (!supported) {
            return new Item("恢复", "系统不支持", TONE_MUTED);
        }
        if (!registered) {
            return new Item("恢复", "未就绪", TONE_ATTENTION);
        }
        return new Item("恢复",
                automaticWatchdogActive ? "自动看门狗" : "Windows 后备",
                TONE_DEFAULT);
    }
}

package com.colorvision.xcviewer;

final class OperationsDashboardStatusFormatter {
    private OperationsDashboardStatusFormatter() {
    }

    static String flow(boolean available, boolean active, String phase) {
        if (!available) {
            return "检测\n暂不可用";
        }
        if (!active) {
            return "检测\n空闲";
        }
        if ("paused".equals(phase)) {
            return "检测\n已暂停";
        }
        if ("cancelling".equals(phase)) {
            return "检测\n正在取消";
        }
        return "检测\n运行中";
    }

    static String devices(boolean available, boolean configured, int ready, int busy, int attention, int total) {
        if (!available) {
            return "设备\n暂不可用";
        }
        if (!configured || total <= 0) {
            return "设备\n未加载";
        }
        if (attention > 0) {
            return "设备\n需关注 " + attention;
        }
        if (busy > 0) {
            return "设备\n忙碌 " + busy + " / " + total;
        }
        return "设备\n就绪 " + ready + " / " + total;
    }

    static String messageChannel(boolean available, boolean connected, boolean subscriptionsReady,
            int activeSubscriptions, int registeredSubscriptions) {
        if (!available) {
            return "消息\n暂不可用";
        }
        if (!connected) {
            return "消息\n未连接";
        }
        if (!subscriptionsReady) {
            return "消息\n订阅 " + activeSubscriptions + " / " + registeredSubscriptions;
        }
        return "消息\n已连接";
    }

    static String alerts(int warningCount, int errorCount, int criticalCount) {
        if (criticalCount > 0) {
            return "告警\n严重 " + criticalCount;
        }
        if (errorCount > 0) {
            return "告警\n错误 " + errorCount;
        }
        if (warningCount > 0) {
            return "告警\n警告 " + warningCount;
        }
        return "告警\n暂无异常";
    }

    static String performance(boolean available, double cpuPercent, String uiState) {
        if (!available) {
            return "性能\n暂不可用";
        }
        String responsiveness = "unresponsive".equals(uiState) ? "无响应"
                : "slow".equals(uiState) ? "偏慢" : "正常";
        return "性能\nCPU " + Math.round(Math.max(0, cpuPercent)) + "% · " + responsiveness;
    }

    static String recovery(boolean supported, boolean registered, boolean automaticWatchdogActive) {
        if (!supported) {
            return "恢复\n系统不支持";
        }
        if (!registered) {
            return "恢复\n未就绪";
        }
        return automaticWatchdogActive ? "恢复\n自动看门狗" : "恢复\nWindows 后备";
    }
}

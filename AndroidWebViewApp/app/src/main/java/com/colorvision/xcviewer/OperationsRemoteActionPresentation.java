package com.colorvision.xcviewer;

final class OperationsRemoteActionPresentation {
    private OperationsRemoteActionPresentation() {
    }

    static String scopeNote(boolean remote, boolean hostFresh) {
        if (!remote) {
            return "操作仅作用于当前电脑；恢复、中断和重启操作会在执行前再次确认。";
        }
        if (hostFresh) {
            return "远程请求由本机设备密钥签名，电脑核验后执行并返回签名结果。";
        }
        return "电脑当前离线。请求会在固定中继短时等待；请只提交一次，并在“最近远程请求”确认结果。";
    }

    static String windowDescription(boolean remote) {
        return remote
                ? "控制当前电脑上的 ColorVision 主窗口；不会影响其他应用。"
                : "显示或最小化当前 ColorVision 主窗口。";
    }

    static String diagnosticsDescription(boolean remote) {
        return remote
                ? "生成只读诊断，或检查消息通道的恢复选项。"
                : "先定位异常，再按需持续观察关键运行状态。";
    }

    static String recoveryDescription(boolean remote) {
        return remote
                ? "检测取消和应用重启仅在最新电脑状态允许时可用。"
                : "恢复消息通道，或在检测空闲时重启 ColorVision。";
    }
}

package com.colorvision.xcviewer;

final class OperationsConnectionCheckPresentation {
    private OperationsConnectionCheckPresentation() {
    }

    static String status(boolean success, String heading) {
        if (success) {
            return "检查通过 · 安全连接可用";
        }
        String normalized = heading == null ? "" : heading.trim();
        return "需要处理 · " + (normalized.isEmpty() ? "连接检查未通过" : normalized);
    }

    static String runningDescription() {
        return "会依次检查手机网络、目标地址、安全端口、TLS 证书和设备签名。";
    }

    static String diagnosticSummary(int completedCheckCount) {
        int safeCount = Math.max(0, completedCheckCount);
        if (safeCount == 0) {
            return "自检未能开始。配对资料保持不变；错误详情不包含设备密钥、证书指纹、设备 ID、用户名或机器名。";
        }
        return "已完成 " + safeCount + " 项只读检查。配对资料保持不变；诊断详情不包含设备密钥、证书指纹、设备 ID、用户名或机器名。";
    }

    static String detailsAction(int completedCheckCount, boolean expanded) {
        if (expanded) {
            return "收起检查详情";
        }
        int safeCount = Math.max(0, completedCheckCount);
        return safeCount == 0 ? "查看启动错误详情" : "查看 " + safeCount + " 项检查详情";
    }
}

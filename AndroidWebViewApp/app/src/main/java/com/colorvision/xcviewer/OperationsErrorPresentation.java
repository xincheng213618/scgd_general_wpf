package com.colorvision.xcviewer;

import java.net.ConnectException;
import java.net.SocketTimeoutException;
import java.net.UnknownHostException;

import javax.net.ssl.SSLHandshakeException;

final class OperationsErrorPresentation {
    private static final int MAX_CAUSE_DEPTH = 12;

    private OperationsErrorPresentation() {
    }

    static String readable(Exception exception) {
        if (exception == null) {
            return "连接暂不可用，请稍后重试。";
        }
        if (containsMessage(exception, "Certificate pin mismatch")) {
            return "服务器证书与二维码指纹不一致，已阻止连接。";
        }
        if (containsMessage(exception,
                "invalid_host_certificate_pin",
                "invalid_operations_endpoint",
                "invalid_operations_host")) {
            return "本机配对资料中的安全地址或证书指纹已损坏；请确认原电脑后重新配对。";
        }
        if (hasCause(exception, SocketTimeoutException.class)
                || containsMessage(exception, "after 7000ms")) {
            return "连接电脑超时。配对资料已保留，请运行连接自检。";
        }
        if (hasCause(exception, ConnectException.class)
                || containsMessage(exception, "failed to connect")) {
            return "电脑端安全通道当前不可达。配对资料已保留，请运行连接自检。";
        }
        if (hasCause(exception, UnknownHostException.class)) {
            return "无法解析电脑地址，请检查当前网络或重新获取配对地址。";
        }
        if (hasCause(exception, SSLHandshakeException.class)) {
            return "TLS 安全握手失败，已阻止连接。";
        }
        if (containsMessage(exception, "unknown_or_revoked_device")) {
            return "设备已被电脑端撤销，请重新配对。";
        }
        if (containsMessage(exception, "host_not_found", "unknown_host_identity")) {
            return "电脑端尚未接入固定远程中继；配对资料已保留，电脑更新并启动后会自动接入。";
        }
        if (containsMessage(exception, "device_scope_required")) {
            return "当前配对未获准执行这项远程操作。";
        }
        if (containsMessage(exception, "request_time_out_of_range")) {
            return "手机时间与中继时间偏差过大，请开启系统自动校时后重试。";
        }
        if (containsMessage(exception,
                "task_capability_not_allowed",
                "relay_request_origin_rejected")) {
            return "该远程操作不在应用的固定安全能力清单中，已阻止提交。";
        }
        if (containsMessage(exception,
                "relay_response_too_large",
                "invalid_relay_task_response")) {
            return "远程中继响应不符合应用的安全边界，已停止处理。";
        }
        if (containsMessage(exception, "invalid_failure_evidence_receipt")) {
            return "电脑返回的聚合线索未通过精确安全校验，已阻止显示。";
        }
        if (containsMessage(exception, "window_snapshot_e2e_requires_android_31")) {
            return "远程端到端快照需要 Android 12 或更高版本；现场局域网快照仍可使用。";
        }
        if (containsMessage(exception,
                "invalid_window_snapshot_receipt",
                "invalid_window_snapshot_public_key",
                "invalid_window_snapshot_context")) {
            return "电脑返回的远程快照收据未通过精确签名、时间或加密参数校验，已阻止下载。";
        }
        if (containsMessage(exception, "window_snapshot_payload_not_allowed")) {
            return "远程快照请求不符合固定端到端加密协议，已阻止提交。";
        }
        if (containsMessage(exception,
                "window_snapshot_consumed",
                "window_snapshot_already_consumed")) {
            return "这张远程快照已在手机成功读取并从固定站点销毁，请重新采集。";
        }
        if (containsMessage(exception, "application_restart_flow_active")) {
            return "当前检测仍在执行，为避免中断检测，电脑端已拒绝重启。";
        }
        if (containsMessage(exception, "message_channel:unconfigured")) {
            return "电脑端尚未配置有效消息服务地址，请先在电脑端完成配置。";
        }
        if (containsMessage(exception, "message_channel_recovery_job_missing")) {
            return "未找到本次消息通道恢复作业回执。";
        }
        if (containsMessage(exception,
                "message_channel_recovery_failed",
                "message_channel:recovery_failed",
                "message_channel:recovery_timeout")) {
            return "消息通道尚未恢复，请查看消息通道健康和作业时间线。";
        }
        if (containsMessage(exception, "application_restart_flow_status_unavailable")) {
            return "暂时无法确认检测是否正在执行，已阻止重启。";
        }
        if (containsMessage(exception,
                "application_restart_not_scheduled",
                "application_restart_failed")) {
            return "电脑端未能完成 ColorVision 重启，请查看作业时间线。";
        }
        if (containsMessage(exception, "application_restart_reconnect_timeout")) {
            return "90 秒内未确认 ColorVision 恢复；配对资料已保留。";
        }
        if (containsMessage(exception, "application_restart_job_missing")) {
            return "未找到本次 ColorVision 重启作业回执。";
        }
        if (containsMessage(exception, "window_snapshot_expired")) {
            return "主窗口快照的 5 分钟读取窗口已结束，请重新采集。";
        }
        if (containsMessage(exception, "window_snapshot_not_completed")) {
            return "主窗口快照采集未完成。请确保电脑主窗口已显示且未最小化，然后重试。";
        }
        if (containsMessage(exception, "window_snapshot_not_ready")) {
            return "主窗口快照尚未完成采集。";
        }
        if (containsMessage(exception, "window_snapshot_not_found")) {
            return "一次性主窗口快照已读取销毁、已失效，或不属于当前设备。";
        }
        if (containsMessage(exception, "window_snapshot_read_failed")) {
            return "电脑端暂时无法读取主窗口快照，请重新申请。";
        }
        if (containsMessage(exception,
                "window_snapshot_hash_mismatch",
                "window_snapshot_sealed_hash_mismatch",
                "window_snapshot_decryption_failed")) {
            return "主窗口快照完整性校验失败，已阻止预览。";
        }
        if (containsMessage(exception,
                "window_snapshot_size_rejected",
                "window_snapshot_too_large")) {
            return "主窗口快照超出 1.5 MiB 安全上限，已阻止下载。";
        }
        if (containsMessage(exception,
                "window_snapshot_type_rejected",
                "window_snapshot_encoding_rejected",
                "window_snapshot_format_rejected",
                "window_snapshot_dimensions_rejected")) {
            return "主窗口快照格式或尺寸不符合安全约束，已阻止预览。";
        }
        if (containsMessage(exception, "diagnostic_bundle_expired")) {
            return "诊断包的 24 小时下载窗口已结束，请重新生成。";
        }
        if (containsMessage(exception, "diagnostic_bundle_not_completed")) {
            return "脱敏诊断包生成未完成，请稍后重试并查看作业结果。";
        }
        if (containsMessage(exception, "diagnostic_bundle_not_ready")) {
            return "诊断包尚未完成生成。";
        }
        if (containsMessage(exception, "diagnostic_bundle_not_found")) {
            return "当前设备无权读取该诊断包，或文件已经不可用。";
        }
        if (containsMessage(exception, "diagnostic_bundle_regeneration_required")) {
            return "旧版诊断包不符合当前脱敏规则，请重新生成。";
        }
        if (containsMessage(exception, "diagnostic_bundle_read_failed")) {
            return "电脑端暂时无法读取诊断包，请稍后重试。";
        }
        if (containsMessage(exception, "diagnostic_bundle_hash_mismatch")) {
            return "诊断包完整性校验失败，已阻止分享。";
        }
        if (containsMessage(exception,
                "diagnostic_bundle_size_rejected",
                "diagnostic_bundle_too_large")) {
            return "诊断包超出移动端 2 MiB 安全上限，已阻止下载。";
        }

        return "操作暂未完成，请稍后重试。";
    }

    private static boolean hasCause(Throwable value, Class<? extends Throwable> type) {
        Throwable current = value;
        for (int depth = 0; current != null && depth < MAX_CAUSE_DEPTH; depth++) {
            if (type.isInstance(current)) {
                return true;
            }
            current = current.getCause();
        }
        return false;
    }

    private static boolean containsMessage(Throwable value, String... tokens) {
        Throwable current = value;
        for (int depth = 0; current != null && depth < MAX_CAUSE_DEPTH; depth++) {
            String message = current.getMessage();
            if (message != null) {
                for (String token : tokens) {
                    if (message.contains(token)) {
                        return true;
                    }
                }
            }
            current = current.getCause();
        }
        return false;
    }
}

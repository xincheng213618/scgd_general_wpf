package com.colorvision.xcviewer;

final class OperationsRemoteTaskPresentation {
    private static final String COMPLETED_DETAILS =
            "电脑已验证设备签名并完成请求，结果已写入运维审计。\n\n点击“刷新远程状态”可读取最新脱敏摘要。";
    private static final String GENERIC_FAILURE_DETAILS =
            "请求已安全送达，但电脑端拒绝或执行失败。可刷新远程状态后重试；不会回退为任意命令执行。";

    private OperationsRemoteTaskPresentation() {
    }

    static Presentation create(String capabilityId, String status, String formattedResult) {
        String safeStatus = status == null ? "" : status;
        if ("completed".equals(safeStatus)) {
            return completed(capabilityId, formattedResult);
        }
        if ("awaiting_local_consent".equals(safeStatus)) {
            return new Presentation(
                    "诊断请求已到达电脑",
                    "为避免远程静默取证，诊断包仍需电脑端本机同意后生成。请求身份、时间和状态已写入运维审计。",
                    false);
        }
        if ("failed".equals(safeStatus) || "rejected".equals(safeStatus)) {
            return failed(capabilityId);
        }
        if ("expired".equals(safeStatus)) {
            return new Presentation(
                    "远程请求已过期",
                    "电脑未在 15 分钟有效期内领取该请求。配对资料仍然保留，可在电脑上线后重新提交。",
                    false);
        }
        if ("accepted".equals(safeStatus)
                && OperationsRelayPolicy.CAPABILITY_RESTART_APPLICATION.equals(capabilityId)) {
            return new Presentation(
                    "重启已受理，等待电脑重新上线",
                    "电脑已复核当前检测为空闲并开始固定重启。配对资料已保留，可稍后通过“最近远程请求”继续查看最终签名结果。",
                    false);
        }
        if ("accepted".equals(safeStatus)
                && OperationsRelayPolicy.CAPABILITY_RESTART_MQTT.equals(capabilityId)) {
            return new Presentation(
                    "MQTT 重启已受理，等待服务恢复",
                    "电脑已复核固定服务与检测状态，并通过 ColorVisionServiceHost 开始执行。可稍后通过“最近远程请求”继续查看最终签名结果。",
                    false);
        }
        return new Presentation(
                "远程请求已安全排队",
                "电脑暂未返回最终结果。后台中继会在有效期内继续等待；稍后点击“最近远程请求”即可继续查看。",
                false);
    }

    private static Presentation completed(String capabilityId, String formattedResult) {
        boolean clearsCancel = OperationsRelayPolicy.CAPABILITY_CANCEL_FLOW.equals(capabilityId);
        String state;
        if (OperationsRelayPolicy.CAPABILITY_SHOW_WINDOW.equals(capabilityId)) {
            state = "电脑主窗口已显示";
        } else if (OperationsRelayPolicy.CAPABILITY_MINIMIZE_WINDOW.equals(capabilityId)) {
            state = "电脑主窗口已最小化";
        } else if (OperationsRelayPolicy.CAPABILITY_RECOVER_MESSAGE_CHANNEL.equals(capabilityId)) {
            state = "电脑消息通道已就绪";
        } else if (OperationsRelayPolicy.CAPABILITY_RESTART_MQTT.equals(capabilityId)) {
            state = "MQTT 消息服务已远程重启";
        } else if (clearsCancel) {
            state = "已向当前检测发送取消请求";
        } else if (OperationsRelayPolicy.CAPABILITY_RESTART_APPLICATION.equals(capabilityId)) {
            state = "ColorVision 已远程重启并重新上线";
        } else if (OperationsRelayPolicy.CAPABILITY_READ_FAILURE_EVIDENCE.equals(capabilityId)) {
            state = "崩溃与卡死线索已刷新";
        } else if (OperationsRelayPolicy.CAPABILITY_CAPTURE_WINDOW_SNAPSHOT.equals(capabilityId)) {
            state = "远程主窗口快照已就绪";
        } else {
            state = "远程诊断请求已完成";
        }

        String details;
        if (OperationsRelayPolicy.CAPABILITY_READ_FAILURE_EVIDENCE.equals(capabilityId)) {
            details = formattedResult == null ? "" : formattedResult;
        } else if (OperationsRelayPolicy.CAPABILITY_RESTART_MQTT.equals(capabilityId)) {
            details = "电脑已通过固定白名单完成服务重启，结果已写入运维审计。消息连接与检测设备可能仍在恢复，请刷新“消息”状态确认连接和订阅。";
        } else {
            details = COMPLETED_DETAILS;
        }
        return new Presentation(state, details, clearsCancel);
    }

    private static Presentation failed(String capabilityId) {
        boolean clearsCancel = OperationsRelayPolicy.CAPABILITY_CANCEL_FLOW.equals(capabilityId);
        if (OperationsRelayPolicy.CAPABILITY_RESTART_MQTT.equals(capabilityId)) {
            return new Presentation(
                    "电脑端未执行 MQTT 重启",
                    "固定服务不适用、检测状态发生变化，或 ColorVisionServiceHost 拒绝或执行失败。请求不会回退为任意命令执行。",
                    false);
        }
        if (OperationsRelayPolicy.CAPABILITY_READ_FAILURE_EVIDENCE.equals(capabilityId)) {
            return new Presentation(
                    "电脑暂无法读取聚合线索",
                    "电脑当前无法读取最近 7 天的有界崩溃、卡死与本机转储聚合线索。配对资料已保留，可刷新远程状态后重试。",
                    false);
        }
        if (OperationsRelayPolicy.CAPABILITY_CAPTURE_WINDOW_SNAPSHOT.equals(capabilityId)) {
            return new Presentation(
                    "电脑暂无法生成远程快照",
                    "电脑没有生成可端到端加密的 ColorVision 主窗口快照。固定站点不会接收未加密画面；可刷新远程状态后重新采集。",
                    false);
        }
        if (OperationsRelayPolicy.CAPABILITY_SHOW_WINDOW.equals(capabilityId)) {
            return new Presentation("电脑主窗口未显示", GENERIC_FAILURE_DETAILS, false);
        }
        if (OperationsRelayPolicy.CAPABILITY_MINIMIZE_WINDOW.equals(capabilityId)) {
            return new Presentation("电脑主窗口未最小化", GENERIC_FAILURE_DETAILS, false);
        }
        if (OperationsRelayPolicy.CAPABILITY_RECOVER_MESSAGE_CHANNEL.equals(capabilityId)) {
            return new Presentation(
                    "电脑消息通道未恢复",
                    "电脑未能恢复 ColorVision 当前消息连接与既有订阅。可刷新“消息”状态后重试；请求不会更改地址、Topic 或凭据。",
                    false);
        }
        if (clearsCancel) {
            return new Presentation(
                    "当前检测未取消",
                    "检测状态已变化、当前检测不可取消，或电脑拒绝了请求。手机不会选择、启动或取消其他检测。",
                    true);
        }
        if (OperationsRelayPolicy.CAPABILITY_RESTART_APPLICATION.equals(capabilityId)) {
            return new Presentation(
                    "ColorVision 未完成重启",
                    "检测状态发生变化，或电脑未能完成固定应用重启。配对资料已保留，可刷新连接状态后重试。",
                    false);
        }
        if (OperationsRelayPolicy.CAPABILITY_REQUEST_DIAGNOSTICS.equals(capabilityId)) {
            return new Presentation(
                    "电脑未生成诊断包",
                    "电脑端未同意本次诊断请求，或脱敏诊断包生成失败。不会回退为远程文件读取或静默取证。",
                    false);
        }
        return new Presentation("电脑端未执行远程请求", GENERIC_FAILURE_DETAILS, clearsCancel);
    }

    static final class Presentation {
        final String state;
        final String details;
        final boolean clearFlowCancelAvailability;

        Presentation(String state, String details, boolean clearFlowCancelAvailability) {
            this.state = state;
            this.details = details;
            this.clearFlowCancelAvailability = clearFlowCancelAvailability;
        }
    }
}

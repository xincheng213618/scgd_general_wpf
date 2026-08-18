package com.colorvision.xcviewer;

import java.net.ConnectException;
import java.net.SocketTimeoutException;
import java.net.UnknownHostException;

import javax.net.ssl.SSLHandshakeException;

final class PairingFailurePresentation {
    static final String INVALID_QR = "invalid_qr";
    static final String UNSUPPORTED_QR = "unsupported_qr";
    static final String EXPIRED_QR = "expired_qr";
    static final String SECURITY_REJECTED = "security_rejected";
    static final String COMPUTER_UNREACHABLE = "computer_unreachable";
    static final String APPROVAL_REJECTED = "approval_rejected";
    static final String UNKNOWN = "unknown";

    private PairingFailurePresentation() {
    }

    static String reasonFor(Exception exception) {
        if (exception instanceof SSLHandshakeException) {
            return SECURITY_REJECTED;
        }
        if (exception instanceof SocketTimeoutException
                || exception instanceof ConnectException
                || exception instanceof UnknownHostException) {
            return COMPUTER_UNREACHABLE;
        }
        String message = exception == null || exception.getMessage() == null
                ? "" : exception.getMessage();
        if (message.contains("pairing_qr_expired")
                || message.contains("pairing_challenge_invalid_or_expired")
                || message.contains("pairing_claim_not_found")) {
            return EXPIRED_QR;
        }
        if (message.contains("pairing_qr_unsupported")) {
            return UNSUPPORTED_QR;
        }
        if (message.contains("pairing_qr_security_invalid")
                || message.contains("Certificate pin mismatch")
                || message.contains("invalid_pairing_signature")
                || message.contains("invalid_pairing_encoding")
                || message.contains("invalid_pairing_key")) {
            return SECURITY_REJECTED;
        }
        if (message.contains("pairing_qr_invalid")) {
            return INVALID_QR;
        }
        if (message.contains("timeout") || message.contains("failed to connect")) {
            return COMPUTER_UNREACHABLE;
        }
        return UNKNOWN;
    }

    static String title(String reason) {
        if (EXPIRED_QR.equals(reason)) {
            return "配对码已过期";
        }
        if (UNSUPPORTED_QR.equals(reason)) {
            return "配对码版本不兼容";
        }
        if (SECURITY_REJECTED.equals(reason)) {
            return "已阻止不安全配对";
        }
        if (COMPUTER_UNREACHABLE.equals(reason)) {
            return "无法连接电脑";
        }
        if (APPROVAL_REJECTED.equals(reason)) {
            return "电脑端已拒绝配对";
        }
        if (INVALID_QR.equals(reason)) {
            return "无法识别配对码";
        }
        return "未能完成安全配对";
    }

    static String message(String reason) {
        if (EXPIRED_QR.equals(reason)) {
            return "电脑端配对码仅短时有效。请在电脑端刷新配对码后重新扫描。";
        }
        if (UNSUPPORTED_QR.equals(reason)) {
            return "手机与电脑端的配对协议版本不一致。请更新较旧的一端，再生成新的配对码。";
        }
        if (SECURITY_REJECTED.equals(reason)) {
            return "证书指纹或设备证明未通过安全校验，连接已停止。请确认扫描的是当前电脑新生成的配对码。";
        }
        if (COMPUTER_UNREACHABLE.equals(reason)) {
            return "暂时无法通过安全通道联系电脑。请确认手机与电脑位于同一可信网络，再刷新配对码重试。";
        }
        if (APPROVAL_REJECTED.equals(reason)) {
            return "电脑端拒绝了这台手机。本次设备证明不会保存；需要连接时请重新生成配对码。";
        }
        if (INVALID_QR.equals(reason)) {
            return "当前二维码不是有效的 ColorVision 安全配对码。请从电脑端“局域网控制”重新打开二维码。";
        }
        return "本次配对没有完成。请在电脑端刷新配对码后重新扫描。";
    }

    static String preservationNote(boolean hasExistingProfile) {
        return hasExistingProfile
                ? "现有已配对电脑、设备私钥和运维记录均已保留。"
                : "本次二维码和未完成的配对不会保存；设备私钥不会写入二维码或网址。";
    }

    static String secondaryAction(boolean hasExistingProfile) {
        return hasExistingProfile ? "返回当前电脑" : "返回设置";
    }
}

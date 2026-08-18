package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;

import org.junit.Test;

import java.net.ConnectException;
import java.net.SocketTimeoutException;
import java.net.UnknownHostException;

import javax.net.ssl.SSLHandshakeException;

public class OperationsErrorPresentationTest {
    @Test
    public void nestedNetworkFailuresKeepSpecificRecoveryGuidance() {
        assertEquals(
                "连接电脑超时。配对资料已保留，请运行连接自检。",
                OperationsErrorPresentation.readable(
                        new Exception("request_failed", new SocketTimeoutException())));
        assertEquals(
                "电脑端安全通道当前不可达。配对资料已保留，请运行连接自检。",
                OperationsErrorPresentation.readable(
                        new Exception("request_failed", new ConnectException())));
        assertEquals(
                "无法解析电脑地址，请检查当前网络或重新获取配对地址。",
                OperationsErrorPresentation.readable(
                        new Exception("request_failed", new UnknownHostException())));
    }

    @Test
    public void certificateMismatchOutranksGenericTlsFailure() {
        SSLHandshakeException handshake = new SSLHandshakeException("handshake_failed");
        handshake.initCause(new SecurityException("Certificate pin mismatch"));

        assertEquals(
                "服务器证书与二维码指纹不一致，已阻止连接。",
                OperationsErrorPresentation.readable(handshake));
        assertEquals(
                "TLS 安全握手失败，已阻止连接。",
                OperationsErrorPresentation.readable(
                        new SSLHandshakeException("protocol_version")));
    }

    @Test
    public void corruptedPairingConfigurationNeverLeaksInternalCodes() {
        assertEquals(
                "本机配对资料中的安全地址或证书指纹已损坏；请确认原电脑后重新配对。",
                OperationsErrorPresentation.readable(
                        new IllegalArgumentException("invalid_host_certificate_pin")));
        assertEquals(
                "本机配对资料中的安全地址或证书指纹已损坏；请确认原电脑后重新配对。",
                OperationsErrorPresentation.readable(
                        new IllegalArgumentException("invalid_operations_endpoint")));
    }

    @Test
    public void securityBoundaryFailuresRetainTheirActionableMeaning() {
        assertEquals(
                "主窗口快照完整性校验失败，已阻止预览。",
                OperationsErrorPresentation.readable(
                        new SecurityException("window_snapshot_decryption_failed")));
        assertEquals(
                "诊断包超出移动端 2 MiB 安全上限，已阻止下载。",
                OperationsErrorPresentation.readable(
                        new SecurityException("diagnostic_bundle_too_large")));
        assertEquals(
                "操作暂未完成，请稍后重试。",
                OperationsErrorPresentation.readable(
                        new IllegalStateException("remote_task_pending")));
    }

    @Test
    public void nullAndUntrustedMessagesUseBoundedFallbacks() {
        assertEquals(
                "连接暂不可用，请稍后重试。",
                OperationsErrorPresentation.readable(null));
        String result = OperationsErrorPresentation.readable(
                new IllegalStateException("C:\\Users\\operator\\secret.log contained credentials"));

        assertEquals("操作暂未完成，请稍后重试。", result);
        assertFalse(result.contains("operator"));
        assertFalse(result.contains("secret.log"));
    }
}

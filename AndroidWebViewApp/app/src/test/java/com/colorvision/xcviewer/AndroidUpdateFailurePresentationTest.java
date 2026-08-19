package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

import java.io.IOException;
import java.net.SocketTimeoutException;
import java.net.UnknownHostException;

public class AndroidUpdateFailurePresentationTest {
    @Test
    public void connectivityFailuresStaySpecificAndRecoverable() {
        assertEquals("安全更新服务当前不可达，请稍后重试。",
                AndroidUpdateFailurePresentation.message(new UnknownHostException()));
        assertEquals("安全更新服务响应超时，请稍后重试。",
                AndroidUpdateFailurePresentation.message(new SocketTimeoutException()));
    }

    @Test
    public void packageTrustFailuresExplainWhatWasBlocked() {
        assertEquals("安装包签名与当前应用不一致，已阻止安装。",
                AndroidUpdateFailurePresentation.message(
                        new IOException("android_update_package_signature_mismatch")));
        assertEquals("安装包完整性校验失败，已删除临时文件。",
                AndroidUpdateFailurePresentation.message(
                        new IOException("android_update_download_hash_mismatch")));
        assertEquals("下载的安装包不是更高版本，已阻止降级或重复安装。",
                AndroidUpdateFailurePresentation.message(
                        new IOException("android_update_package_not_newer")));
        assertEquals("安装包身份与更新清单不一致，已阻止安装。",
                AndroidUpdateFailurePresentation.message(
                        new IOException("android_update_package_name_mismatch")));
    }

    @Test
    public void malformedAndUnknownFailuresUseSafeMessages() {
        assertEquals("安全更新服务尚未提供移动端更新清单。",
                AndroidUpdateFailurePresentation.message(
                        new IOException("android_update_manifest_http_404")));
        assertEquals("更新数据不符合安全约束，已阻止安装。",
                AndroidUpdateFailurePresentation.message(
                        new IOException("android_update_manifest_schema_rejected")));
        assertEquals("无法完成安全更新校验，请稍后重试。",
                AndroidUpdateFailurePresentation.message(null));
    }
}

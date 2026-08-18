package com.colorvision.xcviewer;

import java.net.ConnectException;
import java.net.SocketTimeoutException;
import java.net.UnknownHostException;

final class AndroidUpdateFailurePresentation {
    private AndroidUpdateFailurePresentation() {
    }

    static String message(Exception exception) {
        String detail = exception == null || exception.getMessage() == null
                ? "" : exception.getMessage();
        if (exception instanceof UnknownHostException
                || exception instanceof ConnectException) {
            return "安全更新服务当前不可达，请稍后重试。";
        }
        if (exception instanceof SocketTimeoutException) {
            return "安全更新服务响应超时，请稍后重试。";
        }
        if (detail.contains("manifest_http_404")) {
            return "安全更新服务尚未提供移动端更新清单。";
        }
        if (detail.contains("signature_mismatch")) {
            return "安装包签名与当前应用不一致，已阻止安装。";
        }
        if (detail.contains("hash_mismatch")) {
            return "安装包完整性校验失败，已删除临时文件。";
        }
        if (detail.contains("not_newer")) {
            return "下载的安装包不是更高版本，已阻止降级或重复安装。";
        }
        if (detail.contains("package_name_mismatch")
                || detail.contains("package_version_mismatch")) {
            return "安装包身份与更新清单不一致，已阻止安装。";
        }
        if (detail.contains("rejected")
                || detail.contains("incomplete")
                || detail.contains("too_large")) {
            return "更新数据不符合安全约束，已阻止安装。";
        }
        return "无法完成安全更新校验，请稍后重试。";
    }
}

package com.colorvision.xcviewer;

import android.content.Context;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.NetworkCapabilities;

import org.json.JSONObject;

import java.net.ConnectException;
import java.net.InetAddress;
import java.net.SocketTimeoutException;
import java.net.URL;
import java.util.Locale;

import javax.net.ssl.SSLHandshakeException;

final class OperationsConnectionCheck {
    enum TransportFailure {
        TIMEOUT,
        REFUSED,
        TLS,
        OTHER
    }

    static final class Result {
        final boolean success;
        final String heading;
        final String recommendation;
        final String technicalDetails;
        final int completedCheckCount;

        Result(boolean success, String heading, String recommendation, String technicalDetails) {
            this.success = success;
            this.heading = heading;
            this.recommendation = recommendation;
            this.technicalDetails = technicalDetails;
            this.completedCheckCount = countCompletedChecks(technicalDetails);
        }
    }

    private OperationsConnectionCheck() {
    }

    static Result run(Context context, String endpoint, OperationsApiClient client) {
        StringBuilder report = new StringBuilder();
        NetworkStatus network = readNetworkStatus(context);
        report.append("1. 手机网络：").append(network.label).append('\n');
        if (!network.connected) {
            return failure("手机当前没有可用网络", report,
                    "请先连接与电脑相同的可信 Wi-Fi，再重新运行自检。");
        }

        URL target;
        try {
            target = new URL(endpoint);
        } catch (Exception ex) {
            report.append("2. 配对地址：格式无效\n");
            return failure("配对资料中的地址无效", report,
                    "请移除失效配对资料，再扫描电脑端新生成的安全配对码。");
        }

        int port = target.getPort() > 0 ? target.getPort() : target.getDefaultPort();
        report.append("2. 目标主机：").append(target.getHost()).append(':').append(port).append('\n');

        InetAddress[] addresses;
        long dnsStarted = System.nanoTime();
        try {
            addresses = InetAddress.getAllByName(target.getHost());
        } catch (Exception ex) {
            report.append("3. 主机解析：失败\n");
            return failure("无法解析电脑地址", report,
                    "检查配对地址和当前网络；若电脑地址已变化，请在电脑端刷新配对码。");
        }
        long dnsMilliseconds = elapsedMilliseconds(dnsStarted);
        report.append("3. 主机解析：通过（").append(dnsMilliseconds).append(" ms）\n");

        boolean localTarget = false;
        for (InetAddress address : addresses) {
            localTarget |= address.isSiteLocalAddress() || address.isLoopbackAddress();
        }
        if (localTarget && !network.localNetwork) {
            report.append("4. 局域网路径：当前网络不是 Wi-Fi/以太网\n");
            return failure("手机不在电脑所在局域网", report,
                    "关闭移动数据切换，连接与电脑相同的 Wi-Fi 后重试。");
        }

        JSONObject response;
        long apiStarted = System.nanoTime();
        try {
            response = client.get("/ops/v1/diagnostics/connection");
        } catch (IllegalStateException ex) {
            long apiMilliseconds = elapsedMilliseconds(apiStarted);
            String code = ex.getMessage() == null ? "request_rejected" : ex.getMessage();
            report.append("4. 安全端口：通过（").append(apiMilliseconds).append(" ms）\n");
            report.append("5. TLS 证书固定：通过\n");
            report.append("6. 设备签名请求：被拒绝（").append(safeCode(code)).append("）\n");
            if (code.contains("unknown_or_revoked_device")) {
                return failure("设备已被电脑端撤销", report,
                        "在确认电脑身份后，移除本机配对资料并重新扫码配对。");
            }
            if (code.contains("scope_required")) {
                return failure("当前设备缺少连接诊断权限", report,
                        "请在电脑端撤销旧配对，并使用当前版本重新配对。");
            }
            return failure("安全请求被电脑端拒绝", report,
                    "保留配对资料并重试；若持续出现，请在电脑端检查运维审计。");
        } catch (Exception ex) {
            long apiMilliseconds = elapsedMilliseconds(apiStarted);
            switch (classifyTransportFailure(ex)) {
                case TIMEOUT:
                    report.append("4. 安全端口：响应超时\n");
                    return failure("电脑端安全端口没有及时响应", report,
                            "确认 ColorVision 正在运行、局域网控制已启用，并检查 Windows 防火墙和当前网络。");
                case REFUSED:
                    report.append("4. 安全端口：拒绝连接\n");
                    return failure("电脑端安全通道未监听", report,
                            "启动 ColorVision，并在“选项 > 局域网控制”确认安全通道正在运行。");
                case TLS:
                    report.append("4. 安全端口：通过（").append(apiMilliseconds).append(" ms）\n");
                    report.append("5. TLS 证书固定：失败\n");
                    return failure("电脑证书与配对记录不一致", report,
                            "已阻止连接。请先确认连接的是原电脑，再决定是否重新配对。");
                default:
                    report.append("4. 安全端口：通信失败\n");
                    return failure("无法完成电脑端安全请求", report,
                            "确认手机和电脑处于同一可信局域网，并检查 ColorVision 安全通道状态。");
            }
        }

        long apiMilliseconds = elapsedMilliseconds(apiStarted);
        JSONObject data = response.optJSONObject("data");
        if (data == null || !"ready".equals(data.optString("channel"))) {
            report.append("4. 安全端口：通过（").append(apiMilliseconds).append(" ms）\n");
            report.append("5. TLS 证书固定：通过\n");
            report.append("6. 设备签名与安全 API：返回内容异常\n");
            return failure("电脑端诊断响应不完整", report,
                    "请升级电脑端 ColorVision 后重新运行自检。");
        }

        report.append("4. 安全端口：通过（").append(apiMilliseconds).append(" ms）\n");
        report.append("5. TLS 证书固定：通过\n");
        report.append("6. 设备签名认证：通过（").append(apiMilliseconds).append(" ms）\n");
        long serverTime = data.optLong("serverUnixTimeMilliseconds", 0L);
        if (serverTime > 0L) {
            long driftSeconds = Math.abs(System.currentTimeMillis() - serverTime) / 1000L;
            report.append("7. 时钟偏差：").append(driftSeconds <= 30 ? "正常" : driftSeconds + " 秒")
                    .append('\n');
        } else {
            report.append("7. 时钟偏差：电脑端未提供\n");
        }

        JSONObject desktop = data.optJSONObject("desktop");
        String windowState = desktop == null ? "未知" : desktop.optString("windowState", "未知");
        boolean visible = desktop != null && desktop.optBoolean("isVisible", false);
        report.append("8. 桌面主窗口：").append(windowState)
                .append(visible ? "（可见）" : "（不可见）").append('\n');
        report.append("9. 电脑版本：").append(data.optString("applicationVersion", "未知"))
                .append(" · ").append(data.optString("runtime", "未知运行时")).append('\n');
        report.append("10. 运维概况：可用能力 ").append(data.optInt("availableCapabilityCount", 0))
                .append("，告警 ").append(data.optInt("alertCount", 0))
                .append("，待处理作业 ").append(data.optInt("pendingJobCount", 0)).append('\n');
        return new Result(
                true,
                "连接自检通过",
                "电脑安全连接正常，可以进入现场运维。",
                report.toString().trim());
    }

    private static NetworkStatus readNetworkStatus(Context context) {
        ConnectivityManager manager = (ConnectivityManager) context.getSystemService(Context.CONNECTIVITY_SERVICE);
        if (manager == null) {
            return new NetworkStatus(false, false, "系统网络服务不可用");
        }
        Network active = manager.getActiveNetwork();
        NetworkCapabilities capabilities = active == null ? null : manager.getNetworkCapabilities(active);
        if (capabilities == null) {
            return new NetworkStatus(false, false, "未连接");
        }

        boolean local = capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)
                || capabilities.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET);
        String type = capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) ? "Wi-Fi"
                : capabilities.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) ? "以太网"
                : capabilities.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR) ? "移动网络"
                : capabilities.hasTransport(NetworkCapabilities.TRANSPORT_VPN) ? "VPN"
                : "其他网络";
        boolean validated = capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED);
        return new NetworkStatus(true, local, type + (validated ? "（已验证）" : "（局域网可用性待检测）"));
    }

    private static Result failure(String heading, StringBuilder report, String suggestion) {
        return new Result(false, heading, suggestion, report.toString().trim());
    }

    private static int countCompletedChecks(String technicalDetails) {
        if (technicalDetails == null || technicalDetails.isEmpty()) {
            return 0;
        }
        int count = 0;
        for (String line : technicalDetails.split("\\R")) {
            if (line.matches("\\d+\\.\\s.*")) {
                count++;
            }
        }
        return count;
    }

    private static long elapsedMilliseconds(long started) {
        return Math.max(0L, (System.nanoTime() - started) / 1_000_000L);
    }

    static TransportFailure classifyTransportFailure(Throwable value) {
        if (hasCause(value, SSLHandshakeException.class)) {
            return TransportFailure.TLS;
        }
        if (hasCause(value, ConnectException.class)) {
            return TransportFailure.REFUSED;
        }
        if (hasCause(value, SocketTimeoutException.class)) {
            return TransportFailure.TIMEOUT;
        }
        return TransportFailure.OTHER;
    }

    private static boolean hasCause(
            Throwable value,
            Class<? extends Throwable> expectedType) {
        Throwable current = value;
        for (int depth = 0; current != null && depth < 8; depth++) {
            if (expectedType.isInstance(current)) {
                return true;
            }
            current = current.getCause();
        }
        return false;
    }

    private static String safeCode(String value) {
        String normalized = value.toLowerCase(Locale.ROOT).replaceAll("[^a-z0-9_.-]", "_");
        return normalized.length() <= 64 ? normalized : normalized.substring(0, 64);
    }

    private static final class NetworkStatus {
        final boolean connected;
        final boolean localNetwork;
        final String label;

        NetworkStatus(boolean connected, boolean localNetwork, String label) {
            this.connected = connected;
            this.localNetwork = localNetwork;
            this.label = label;
        }
    }
}

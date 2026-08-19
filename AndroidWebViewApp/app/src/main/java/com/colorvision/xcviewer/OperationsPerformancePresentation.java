package com.colorvision.xcviewer;

import org.json.JSONObject;

import java.util.Arrays;
import java.util.Collections;
import java.util.List;

final class OperationsPerformancePresentation {
    static final int TONE_NORMAL = 0;
    static final int TONE_ATTENTION = 1;
    static final int TONE_ERROR = 2;
    static final String BOUNDARY_NOTICE =
            "单次短采样用于远程定位，不代表长期趋势；不包含进程标识、名称、路径、命令行、主机名、用户名、网络地址、窗口内容或业务数据。";
    static final String SIGNED_NOTICE =
            "该快照由电脑证书签名，手机按配对证书指纹核验；固定中继站无法修改或伪造。";
    private static final int MAXIMUM_SAMPLE_MILLISECONDS = 2_000;
    private static final int MAXIMUM_COUNT = 999_999_999;
    private static final double MAXIMUM_MEMORY_MB = 1_048_576d;

    private OperationsPerformancePresentation() {
    }

    static ViewModel from(
            JSONObject payload,
            TimeFormatter timeFormatter,
            boolean signedSnapshot) {
        if (payload == null) {
            return new ViewModel(
                    false,
                    "性能状态不可用",
                    "当前无法读取进程性能快照",
                    "CPU 0% · 短采样不可用",
                    "",
                    signedSnapshot ? "电脑签名短采样" : "现场短采样",
                    signedSnapshot ? SIGNED_NOTICE : "",
                    TONE_ATTENTION,
                    memoryMetrics(0, 0, 0),
                    resourceMetrics(0, 0, 0, 0, 0));
        }

        double cpuPercent = boundedDouble(payload.optDouble("cpuPercent", 0), 100d);
        int sampleMilliseconds = boundedCount(
                payload.optInt("sampleMilliseconds", 0), MAXIMUM_SAMPLE_MILLISECONDS);
        JSONObject mainUi = payload.optJSONObject("mainUi");
        boolean uiAvailable = mainUi != null && mainUi.optBoolean("available", false);
        String uiState = uiAvailable ? mainUi.optString("state", "unavailable") : "unavailable";
        Long latency = uiAvailable
                && mainUi.has("latencyMilliseconds")
                && !mainUi.isNull("latencyMilliseconds")
                ? Math.max(0L, Math.min(60_000L,
                        mainUi.optLong("latencyMilliseconds", 0L)))
                : null;

        int tone;
        String stateLabel;
        String summaryLabel;
        if ("unresponsive".equals(uiState)) {
            tone = TONE_ERROR;
            stateLabel = "主界面无响应 · CPU " + decimal(cpuPercent) + "%";
            summaryLabel = "主界面响应超时" + latencySuffix(latency);
        } else if ("slow".equals(uiState)) {
            tone = TONE_ATTENTION;
            stateLabel = "主界面响应偏慢 · CPU " + decimal(cpuPercent) + "%";
            summaryLabel = "主界面响应偏慢" + latencySuffix(latency);
        } else if ("responsive".equals(uiState)) {
            tone = TONE_NORMAL;
            stateLabel = "性能正常 · CPU " + decimal(cpuPercent) + "%";
            summaryLabel = "主界面响应正常" + latencySuffix(latency);
        } else {
            tone = TONE_ATTENTION;
            stateLabel = "主界面状态不可用 · CPU " + decimal(cpuPercent) + "%";
            summaryLabel = "主界面响应当前不可探测";
        }

        JSONObject garbageCollection = payload.optJSONObject("garbageCollection");
        int gen0 = garbageCollection == null ? 0
                : boundedCount(garbageCollection.optInt("gen0Collections", 0), MAXIMUM_COUNT);
        int gen1 = garbageCollection == null ? 0
                : boundedCount(garbageCollection.optInt("gen1Collections", 0), MAXIMUM_COUNT);
        int gen2 = garbageCollection == null ? 0
                : boundedCount(garbageCollection.optInt("gen2Collections", 0), MAXIMUM_COUNT);
        String capturedAt = timeFormatter.format(payload.optString("capturedAt", ""));
        return new ViewModel(
                true,
                stateLabel,
                summaryLabel,
                "CPU " + decimal(cpuPercent) + "% · 短采样 "
                        + (sampleMilliseconds > 0 ? sampleMilliseconds + " ms" : "时间不可用"),
                capturedAt.isEmpty() ? "" : "采集于 " + capturedAt,
                signedSnapshot ? "电脑签名短采样" : "现场短采样",
                signedSnapshot ? SIGNED_NOTICE : "",
                tone,
                memoryMetrics(
                        boundedDouble(payload.optDouble("workingSetMb", 0), MAXIMUM_MEMORY_MB),
                        boundedDouble(payload.optDouble("privateMemoryMb", 0), MAXIMUM_MEMORY_MB),
                        boundedDouble(payload.optDouble("managedHeapMb", 0), MAXIMUM_MEMORY_MB)),
                resourceMetrics(
                        boundedCount(payload.optInt("threadCount", 0), MAXIMUM_COUNT),
                        boundedCount(payload.optInt("handleCount", 0), MAXIMUM_COUNT),
                        gen0,
                        gen1,
                        gen2));
    }

    private static List<Metric> memoryMetrics(
            double workingSetMb,
            double privateMemoryMb,
            double managedHeapMb) {
        return Collections.unmodifiableList(Arrays.asList(
                new Metric("工作集", decimal(workingSetMb) + " MB"),
                new Metric("私有内存", decimal(privateMemoryMb) + " MB"),
                new Metric("托管堆", decimal(managedHeapMb) + " MB")));
    }

    private static List<Metric> resourceMetrics(
            int threadCount,
            int handleCount,
            int gen0,
            int gen1,
            int gen2) {
        return Collections.unmodifiableList(Arrays.asList(
                new Metric("线程", Integer.toString(threadCount)),
                new Metric("句柄", Integer.toString(handleCount)),
                new Metric("GC 次数", "Gen0 " + gen0 + " · Gen1 " + gen1 + " · Gen2 " + gen2)));
    }

    private static String latencySuffix(Long latency) {
        return latency == null ? "" : " · " + latency + " ms";
    }

    private static double boundedDouble(double value, double maximum) {
        if (!Double.isFinite(value)) {
            return 0;
        }
        return Math.max(0, Math.min(maximum, value));
    }

    private static int boundedCount(int value, int maximum) {
        return Math.max(0, Math.min(maximum, value));
    }

    private static String decimal(double value) {
        double rounded = Math.round(value * 10d) / 10d;
        return rounded == Math.rint(rounded)
                ? Long.toString(Math.round(rounded))
                : Double.toString(rounded);
    }

    interface TimeFormatter {
        String format(String value);
    }

    static final class ViewModel {
        final boolean available;
        final String stateLabel;
        final String summaryLabel;
        final String sampleLabel;
        final String capturedLabel;
        final String sourceLabel;
        final String integrityNotice;
        final int tone;
        final List<Metric> memoryMetrics;
        final List<Metric> resourceMetrics;

        ViewModel(
                boolean available,
                String stateLabel,
                String summaryLabel,
                String sampleLabel,
                String capturedLabel,
                String sourceLabel,
                String integrityNotice,
                int tone,
                List<Metric> memoryMetrics,
                List<Metric> resourceMetrics) {
            this.available = available;
            this.stateLabel = stateLabel;
            this.summaryLabel = summaryLabel;
            this.sampleLabel = sampleLabel;
            this.capturedLabel = capturedLabel;
            this.sourceLabel = sourceLabel;
            this.integrityNotice = integrityNotice;
            this.tone = tone;
            this.memoryMetrics = memoryMetrics;
            this.resourceMetrics = resourceMetrics;
        }
    }

    static final class Metric {
        final String label;
        final String value;

        Metric(String label, String value) {
            this.label = label;
            this.value = value;
        }

        String accessibilityLabel() {
            return label + "，" + value.replace(" · ", "，");
        }
    }
}

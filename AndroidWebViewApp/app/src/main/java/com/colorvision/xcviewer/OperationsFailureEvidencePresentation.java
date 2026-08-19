package com.colorvision.xcviewer;

import java.util.Arrays;
import java.util.Collections;
import java.util.List;

final class OperationsFailureEvidencePresentation {
    static final int TONE_MUTED = 0;
    static final int TONE_ATTENTION = 1;
    static final int TONE_ERROR = 2;
    static final String COUNT_NOTICE =
            "同一次故障可能留下多条事件和转储；这些数字是聚合线索，不等于故障次数。";
    static final String PRIVACY_NOTICE =
            "只显示固定类别计数和聚合时间；不返回事件正文、文件名、路径、转储内容、进程标识、用户/机器信息或堆栈。";
    private static final int WINDOW_DAYS = 7;
    private static final int MAXIMUM_COUNT = 999;

    private OperationsFailureEvidencePresentation() {
    }

    static ViewModel from(
            OperationsFailureEvidence.Snapshot snapshot,
            String latestEvidenceDisplay) {
        int crashCount = safeCount(snapshot.crashCount);
        int hangCount = safeCount(snapshot.hangCount);
        int managedRuntimeFailureCount = safeCount(snapshot.managedRuntimeFailureCount);
        int windowsErrorReportCount = safeCount(snapshot.windowsErrorReportCount);
        int categorizedEventCount = safeSum(
                crashCount,
                hangCount,
                managedRuntimeFailureCount,
                windowsErrorReportCount);
        int failureEventCount = Math.max(
                safeCount(snapshot.failureEventCount), categorizedEventCount);
        int dumpCount = safeCount(snapshot.dumpCount);
        int evidenceCount = failureEventCount + dumpCount;
        boolean hasEvidence = evidenceCount > 0;
        boolean anySourceAvailable = snapshot.eventLogAvailable
                || snapshot.dumpFolderAvailable;
        boolean anySourceUnavailable = !snapshot.eventLogAvailable
                || !snapshot.dumpFolderAvailable;
        boolean scanLimited = snapshot.eventScanLimited || snapshot.dumpScanLimited;

        String stateLabel;
        String summaryLabel;
        int tone;
        if (hasEvidence) {
            stateLabel = "最近 " + WINDOW_DAYS + " 天 · " + evidenceCount + " 条聚合线索";
            summaryLabel = "发现崩溃、卡死或本机转储线索";
            tone = TONE_ERROR;
        } else if (!anySourceAvailable) {
            stateLabel = "故障线索不可用";
            summaryLabel = "当前无法读取 Windows 应用事件和本机转储";
            tone = TONE_ERROR;
        } else if (scanLimited) {
            stateLabel = "最近 " + WINDOW_DAYS + " 天 · 未发现线索 · 扫描有界";
            summaryLabel = "安全上限内未发现崩溃、卡死或本机转储线索";
            tone = TONE_ATTENTION;
        } else if (anySourceUnavailable) {
            stateLabel = "最近 " + WINDOW_DAYS + " 天 · 未发现线索 · 部分来源不可用";
            summaryLabel = "当前可读取来源中未发现故障线索";
            tone = TONE_ATTENTION;
        } else {
            stateLabel = "最近 " + WINDOW_DAYS + " 天 · 未发现线索";
            summaryLabel = "未发现崩溃、卡死或本机转储线索";
            tone = TONE_MUTED;
        }

        List<Category> categories = Collections.unmodifiableList(Arrays.asList(
                new Category("应用崩溃", crashCount, "条"),
                new Category("应用卡死", hangCount, "条"),
                new Category(".NET 运行时失败", managedRuntimeFailureCount, "条"),
                new Category("Windows 错误报告", windowsErrorReportCount, "条"),
                new Category("本机转储", dumpCount, "个")));
        List<Source> sources = Collections.unmodifiableList(Arrays.asList(
                source(
                        "Windows 应用事件",
                        snapshot.eventLogAvailable,
                        snapshot.eventScanLimited,
                        "扫描最近 7 天固定 ColorVision 故障类别。"),
                source(
                        "本机转储目录",
                        snapshot.dumpFolderAvailable,
                        snapshot.dumpScanLimited,
                        "只聚合最近 7 天的本机转储数量。")));

        String latestLabel = hasEvidence
                && latestEvidenceDisplay != null
                && !latestEvidenceDisplay.isEmpty()
                ? "最近线索 · " + latestEvidenceDisplay
                : "";
        return new ViewModel(
                stateLabel,
                summaryLabel,
                "失败事件 " + failureEventCount + " · 本机转储 " + dumpCount,
                latestLabel,
                tone,
                categories,
                sources);
    }

    private static Source source(
            String title,
            boolean available,
            boolean limited,
            String completeDescription) {
        if (!available) {
            return new Source(
                    title,
                    "不可读取",
                    "本次结果不包含此来源。",
                    TONE_ERROR);
        }
        if (limited) {
            return new Source(
                    title,
                    "已读取 · 扫描有界",
                    "结果仅覆盖安全上限内的条目。",
                    TONE_ATTENTION);
        }
        return new Source(title, "已读取", completeDescription, TONE_MUTED);
    }

    private static int safeCount(int value) {
        return Math.max(0, Math.min(MAXIMUM_COUNT, value));
    }

    private static int safeSum(int... values) {
        int total = 0;
        for (int value : values) {
            total = Math.min(MAXIMUM_COUNT, total + value);
        }
        return total;
    }

    static final class ViewModel {
        final String stateLabel;
        final String summaryLabel;
        final String countSummary;
        final String latestLabel;
        final int tone;
        final List<Category> categories;
        final List<Source> sources;

        ViewModel(
                String stateLabel,
                String summaryLabel,
                String countSummary,
                String latestLabel,
                int tone,
                List<Category> categories,
                List<Source> sources) {
            this.stateLabel = stateLabel;
            this.summaryLabel = summaryLabel;
            this.countSummary = countSummary;
            this.latestLabel = latestLabel;
            this.tone = tone;
            this.categories = categories;
            this.sources = sources;
        }
    }

    static final class Category {
        final String label;
        final int count;
        final String unit;

        Category(String label, int count, String unit) {
            this.label = label;
            this.count = count;
            this.unit = unit;
        }

        String countLabel() {
            return count + " " + unit;
        }

        String accessibilityLabel() {
            return label + "，" + count + unit;
        }
    }

    static final class Source {
        final String title;
        final String statusLabel;
        final String supportingLabel;
        final int tone;

        Source(String title, String statusLabel, String supportingLabel, int tone) {
            this.title = title;
            this.statusLabel = statusLabel;
            this.supportingLabel = supportingLabel;
            this.tone = tone;
        }

        String accessibilityLabel() {
            return title + "，" + statusLabel.replace(" · ", "，")
                    + "。" + supportingLabel;
        }
    }
}

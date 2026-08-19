package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;

final class OperationsRecentEventsRefreshPresentation {
    private static final int MAXIMUM_EVIDENCE = 30;

    private OperationsRecentEventsRefreshPresentation() {
    }

    static Snapshot capture(JSONObject payload) {
        if (payload == null || !payload.optBoolean("available", false)) {
            return Snapshot.unavailable();
        }
        List<Evidence> evidence = new ArrayList<>();
        Set<String> seen = new HashSet<>();
        JSONArray events = payload.optJSONArray("recentEvents");
        if (events != null) {
            int count = Math.min(events.length(), MAXIMUM_EVIDENCE);
            for (int index = 0; index < count; index++) {
                JSONObject event = events.optJSONObject(index);
                if (event == null) {
                    continue;
                }
                String revision = event.optString("alertId", "").trim();
                if (revision.isEmpty() || revision.length() > 128) {
                    revision = fallbackRevision(event);
                }
                if (seen.add(revision)) {
                    evidence.add(new Evidence(revision,
                            severity(event.optString("severity", ""))));
                }
            }
        }
        return new Snapshot(
                true,
                boundedCount(payload, "warningCount"),
                boundedCount(payload, "errorCount"),
                boundedCount(payload, "criticalCount"),
                Collections.unmodifiableList(evidence));
    }

    static String feedback(Snapshot previous, Snapshot current) {
        if (previous == null || current == null) {
            return "";
        }
        if (!current.available) {
            return "刷新完成，但近期事件当前不可用";
        }
        if (!previous.available) {
            return "刷新完成 · 近期事件已恢复可用 · 当前异常 "
                    + current.attentionCount() + " 条";
        }

        List<Evidence> added = addedEvidence(previous.evidence, current.evidence);
        if (!added.isEmpty()) {
            return "刷新完成 · 发现 " + added.size() + " 条新增异常证据"
                    + severitySuffix(added);
        }

        int previousCount = previous.attentionCount();
        int currentCount = current.attentionCount();
        if (currentCount > previousCount) {
            return "刷新完成 · 异常计数增加"
                    + countIncreaseSuffix(previous, current);
        }
        if (currentCount < previousCount) {
            return "刷新完成 · 日志窗口已更新，当前异常 " + currentCount
                    + " 条；计数下降不代表已恢复";
        }
        if (current.criticalCount != previous.criticalCount
                || current.errorCount != previous.errorCount
                || current.warningCount != previous.warningCount) {
            return "刷新完成 · 异常分级已变化 · " + current.severitySummary();
        }
        return "刷新完成 · 未发现新增异常证据";
    }

    static boolean hasNewEvidence(Snapshot viewed, Snapshot latest) {
        if (viewed == null || latest == null || !latest.available) {
            return true;
        }
        if (!viewed.available) {
            return latest.attentionCount() > 0 || !latest.evidence.isEmpty();
        }
        if (!addedEvidence(viewed.evidence, latest.evidence).isEmpty()) {
            return true;
        }
        return latest.criticalCount > viewed.criticalCount
                || latest.errorCount > viewed.errorCount
                || latest.warningCount > viewed.warningCount;
    }

    private static List<Evidence> addedEvidence(
            List<Evidence> previous,
            List<Evidence> current) {
        Set<String> previousRevisions = new HashSet<>();
        for (Evidence item : previous) {
            previousRevisions.add(item.revision);
        }
        List<Evidence> added = new ArrayList<>();
        for (Evidence item : current) {
            if (!previousRevisions.contains(item.revision)) {
                added.add(item);
            }
        }
        return added;
    }

    private static String severitySuffix(List<Evidence> evidence) {
        int warnings = 0;
        int errors = 0;
        int critical = 0;
        for (Evidence item : evidence) {
            if ("critical".equals(item.severity)) {
                critical++;
            } else if ("error".equals(item.severity)) {
                errors++;
            } else if ("warning".equals(item.severity)) {
                warnings++;
            }
        }
        StringBuilder suffix = new StringBuilder();
        appendCount(suffix, "严重", critical, "");
        appendCount(suffix, "错误", errors, "");
        appendCount(suffix, "警告", warnings, "");
        return suffix.toString();
    }

    private static String countIncreaseSuffix(Snapshot previous, Snapshot current) {
        StringBuilder suffix = new StringBuilder();
        appendCount(suffix, "严重",
                Math.max(0, current.criticalCount - previous.criticalCount), "+");
        appendCount(suffix, "错误",
                Math.max(0, current.errorCount - previous.errorCount), "+");
        appendCount(suffix, "警告",
                Math.max(0, current.warningCount - previous.warningCount), "+");
        return suffix.toString();
    }

    private static void appendCount(
            StringBuilder value,
            String label,
            int count,
            String prefix) {
        if (count > 0) {
            value.append(" · ").append(label).append(' ').append(prefix).append(count);
        }
    }

    private static String fallbackRevision(JSONObject event) {
        String canonical = canonical(
                event.optString("severity", ""),
                event.optString("source", ""),
                event.optString("occurredAt", ""),
                event.optString("summary", ""));
        try {
            byte[] digest = MessageDigest.getInstance("SHA-256")
                    .digest(canonical.getBytes(StandardCharsets.UTF_8));
            StringBuilder encoded = new StringBuilder(digest.length * 2);
            for (byte item : digest) {
                encoded.append(String.format(Locale.ROOT, "%02x", item & 0xff));
            }
            return encoded.toString();
        } catch (Exception ignored) {
            return String.format(Locale.ROOT, "%064x", canonical.hashCode());
        }
    }

    private static String canonical(String... values) {
        StringBuilder canonical = new StringBuilder();
        for (String value : values) {
            String safe = value == null ? "" : value.trim();
            canonical.append(safe.length()).append(':').append(safe).append(';');
        }
        return canonical.toString();
    }

    private static int boundedCount(JSONObject payload, String name) {
        return Math.max(0, Math.min(999_999, payload.optInt(name, 0)));
    }

    private static String severity(String value) {
        String normalized = value == null ? "" : value.trim().toLowerCase(Locale.ROOT);
        if ("critical".equals(normalized)
                || "error".equals(normalized)
                || "warning".equals(normalized)) {
            return normalized;
        }
        return "";
    }

    static final class Snapshot {
        final boolean available;
        final int warningCount;
        final int errorCount;
        final int criticalCount;
        final List<Evidence> evidence;

        Snapshot(
                boolean available,
                int warningCount,
                int errorCount,
                int criticalCount,
                List<Evidence> evidence) {
            this.available = available;
            this.warningCount = warningCount;
            this.errorCount = errorCount;
            this.criticalCount = criticalCount;
            this.evidence = evidence;
        }

        static Snapshot unavailable() {
            return new Snapshot(false, 0, 0, 0, Collections.emptyList());
        }

        int attentionCount() {
            return warningCount + errorCount + criticalCount;
        }

        String severitySummary() {
            return "严重 " + criticalCount + " · 错误 " + errorCount
                    + " · 警告 " + warningCount;
        }
    }

    private static final class Evidence {
        final String revision;
        final String severity;

        Evidence(String revision, String severity) {
            this.revision = revision;
            this.severity = severity;
        }
    }
}

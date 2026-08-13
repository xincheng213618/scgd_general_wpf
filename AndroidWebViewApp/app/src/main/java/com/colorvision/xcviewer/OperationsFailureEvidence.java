package com.colorvision.xcviewer;

import org.json.JSONObject;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

final class OperationsFailureEvidence {
    private static final String KIND = "failure-evidence-v1";
    private static final String ERROR_KIND = "failure-evidence-error-v1";
    private static final String UNAVAILABLE_CODE = "failure_evidence_unavailable";
    private static final int EXACT_RECEIPT_FIELD_COUNT = 18;
    private static final Pattern ISO_TIMESTAMP = Pattern.compile(
            "^(\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2})(?:\\.(\\d{1,9}))?(Z|[+-]\\d{2}:\\d{2})$");
    private static final String[] EXACT_RECEIPT_FIELDS = {
            "kind",
            "eventLogAvailable",
            "dumpFolderAvailable",
            "eventScanLimited",
            "dumpScanLimited",
            "hasEvidence",
            "windowDays",
            "failureEventCount",
            "crashCount",
            "hangCount",
            "managedRuntimeFailureCount",
            "windowsErrorReportCount",
            "dumpCount",
            "latestEventAt",
            "latestDumpAt",
            "latestEvidenceAt",
            "windowStartedAt",
            "observedAt",
    };

    private OperationsFailureEvidence() {
    }

    static Snapshot parseStrictReceipt(JSONObject evidence) {
        try {
            requireExactFields(evidence);
            if (!KIND.equals(requireString(evidence, "kind"))) {
                throw invalid();
            }

            boolean eventLogAvailable = requireBoolean(evidence, "eventLogAvailable");
            boolean dumpFolderAvailable = requireBoolean(evidence, "dumpFolderAvailable");
            boolean eventScanLimited = requireBoolean(evidence, "eventScanLimited");
            boolean dumpScanLimited = requireBoolean(evidence, "dumpScanLimited");
            boolean hasEvidence = requireBoolean(evidence, "hasEvidence");
            int windowDays = requireCount(evidence, "windowDays");
            int failureEventCount = requireCount(evidence, "failureEventCount");
            int crashCount = requireCount(evidence, "crashCount");
            int hangCount = requireCount(evidence, "hangCount");
            int managedRuntimeFailureCount = requireCount(
                    evidence, "managedRuntimeFailureCount");
            int windowsErrorReportCount = requireCount(
                    evidence, "windowsErrorReportCount");
            int dumpCount = requireCount(evidence, "dumpCount");
            if (windowDays != 7) {
                throw invalid();
            }

            NullableTimestamp latestEvent = requireNullableTimestamp(evidence, "latestEventAt");
            NullableTimestamp latestDump = requireNullableTimestamp(evidence, "latestDumpAt");
            NullableTimestamp latestEvidence = requireNullableTimestamp(
                    evidence, "latestEvidenceAt");
            ParsedTimestamp windowStartedAt = requireTimestamp(evidence, "windowStartedAt");
            ParsedTimestamp observedAt = requireTimestamp(evidence, "observedAt");
            if (windowStartedAt.compareTo(observedAt) > 0
                    || !insideWindow(latestEvent.parsed, windowStartedAt, observedAt)
                    || !insideWindow(latestDump.parsed, windowStartedAt, observedAt)
                    || !insideWindow(latestEvidence.parsed, windowStartedAt, observedAt)) {
                throw invalid();
            }

            if ((failureEventCount == 0) != (latestEvent.parsed == null)
                    || (dumpCount == 0) != (latestDump.parsed == null)
                    || hasEvidence != (failureEventCount > 0 || dumpCount > 0)) {
                throw invalid();
            }
            if (!hasEvidence && (failureEventCount != 0
                    || crashCount != 0
                    || hangCount != 0
                    || managedRuntimeFailureCount != 0
                    || windowsErrorReportCount != 0
                    || dumpCount != 0
                    || latestEvent.parsed != null
                    || latestDump.parsed != null
                    || latestEvidence.parsed != null)) {
                throw invalid();
            }

            ParsedTimestamp expectedLatest = later(latestEvent.parsed, latestDump.parsed);
            if (!sameTimestamp(expectedLatest, latestEvidence.parsed)) {
                throw invalid();
            }

            return new Snapshot(
                    eventLogAvailable,
                    dumpFolderAvailable,
                    eventScanLimited,
                    dumpScanLimited,
                    hasEvidence,
                    windowDays,
                    failureEventCount,
                    crashCount,
                    hangCount,
                    managedRuntimeFailureCount,
                    windowsErrorReportCount,
                    dumpCount,
                    latestEvidence.value);
        } catch (SecurityException ex) {
            throw ex;
        } catch (Exception ex) {
            throw invalid();
        }
    }

    static void validateStrictErrorReceipt(JSONObject evidence) {
        try {
            if (evidence == null
                    || evidence.length() != 2
                    || !evidence.has("kind")
                    || !evidence.has("code")
                    || !ERROR_KIND.equals(requireString(evidence, "kind"))
                    || !UNAVAILABLE_CODE.equals(requireString(evidence, "code"))) {
                throw invalid();
            }
        } catch (SecurityException ex) {
            throw ex;
        } catch (Exception ex) {
            throw invalid();
        }
    }

    static Snapshot fromLocalPayload(JSONObject payload) {
        return new Snapshot(
                payload.optBoolean("eventLogAvailable", false),
                payload.optBoolean("dumpFolderAvailable", false),
                payload.optBoolean("eventScanLimited", false),
                payload.optBoolean("dumpScanLimited", false),
                payload.optBoolean("hasEvidence", false),
                payload.optInt("windowDays", 7),
                payload.optInt("failureEventCount", 0),
                payload.optInt("crashCount", 0),
                payload.optInt("hangCount", 0),
                payload.optInt("managedRuntimeFailureCount", 0),
                payload.optInt("windowsErrorReportCount", 0),
                payload.optInt("dumpCount", 0),
                payload.optString("latestEvidenceAt", ""));
    }

    static String format(Snapshot snapshot, String latestEvidenceDisplay) {
        StringBuilder text = new StringBuilder();
        if (!snapshot.hasEvidence) {
            text.append("最近 ").append(snapshot.windowDays)
                    .append(" 天未发现 ColorVision 崩溃、卡死或本机转储线索。");
        } else {
            text.append("最近 ").append(snapshot.windowDays).append(" 天聚合线索")
                    .append("\n失败事件：").append(snapshot.failureEventCount).append(" 条")
                    .append("\n应用崩溃：").append(snapshot.crashCount).append(" 条")
                    .append(" · 应用卡死：").append(snapshot.hangCount).append(" 条")
                    .append("\n.NET 运行时失败：")
                    .append(snapshot.managedRuntimeFailureCount).append(" 条")
                    .append(" · Windows 错误报告：")
                    .append(snapshot.windowsErrorReportCount).append(" 条")
                    .append("\n本机转储：").append(snapshot.dumpCount).append(" 个");
            if (latestEvidenceDisplay != null && !latestEvidenceDisplay.isEmpty()) {
                text.append("\n最近线索：").append(latestEvidenceDisplay);
            }
            text.append("\n\n同一次故障可能留下多条事件和转储，因此计数不能直接当作故障次数。");
        }
        if (!snapshot.eventLogAvailable) {
            text.append("\n\nWindows 应用事件当前不可读取。");
        }
        if (!snapshot.dumpFolderAvailable) {
            text.append("\n本机转储目录当前不可读取。");
        }
        if (snapshot.eventScanLimited || snapshot.dumpScanLimited) {
            text.append("\n扫描已达到安全上限，显示的是有界结果。");
        }
        text.append("\n\n只显示固定类别计数和聚合时间；不返回事件正文、文件名、路径、转储内容、进程标识、用户/机器信息或堆栈。");
        return text.toString();
    }

    private static void requireExactFields(JSONObject evidence) {
        if (evidence == null || evidence.length() != EXACT_RECEIPT_FIELD_COUNT) {
            throw invalid();
        }
        for (String field : EXACT_RECEIPT_FIELDS) {
            if (!evidence.has(field)) {
                throw invalid();
            }
        }
    }

    private static String requireString(JSONObject evidence, String field) throws Exception {
        Object value = evidence.get(field);
        if (!(value instanceof String)) {
            throw invalid();
        }
        return (String) value;
    }

    private static boolean requireBoolean(JSONObject evidence, String field) throws Exception {
        Object value = evidence.get(field);
        if (!(value instanceof Boolean)) {
            throw invalid();
        }
        return (Boolean) value;
    }

    private static int requireCount(JSONObject evidence, String field) throws Exception {
        Object value = evidence.get(field);
        if (!(value instanceof Integer) && !(value instanceof Long)) {
            throw invalid();
        }
        long count = ((Number) value).longValue();
        if (count < 0L || count > 999L) {
            throw invalid();
        }
        return (int) count;
    }

    private static NullableTimestamp requireNullableTimestamp(
            JSONObject evidence, String field) throws Exception {
        Object value = evidence.get(field);
        if (value == JSONObject.NULL) {
            return new NullableTimestamp(null, null);
        }
        if (!(value instanceof String)) {
            throw invalid();
        }
        String text = (String) value;
        return new NullableTimestamp(text, parseTimestamp(text));
    }

    private static ParsedTimestamp requireTimestamp(JSONObject evidence, String field)
            throws Exception {
        Object value = evidence.get(field);
        if (!(value instanceof String)) {
            throw invalid();
        }
        return parseTimestamp((String) value);
    }

    private static ParsedTimestamp parseTimestamp(String value) throws Exception {
        Matcher matcher = ISO_TIMESTAMP.matcher(value);
        if (!matcher.matches()) {
            throw invalid();
        }
        SimpleDateFormat parser = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.ROOT);
        parser.setLenient(false);
        parser.setTimeZone(TimeZone.getTimeZone("UTC"));
        Date parsed = parser.parse(matcher.group(1));
        if (parsed == null) {
            throw invalid();
        }

        String fraction = matcher.group(2);
        int nanoseconds = 0;
        if (fraction != null) {
            String padded = (fraction + "000000000").substring(0, 9);
            nanoseconds = Integer.parseInt(padded);
        }
        int offsetMinutes = parseOffsetMinutes(matcher.group(3));
        long epochSecond = parsed.getTime() / 1_000L - offsetMinutes * 60L;
        return new ParsedTimestamp(epochSecond, nanoseconds);
    }

    private static int parseOffsetMinutes(String offset) {
        if ("Z".equals(offset)) {
            return 0;
        }
        int hours = Integer.parseInt(offset.substring(1, 3));
        int minutes = Integer.parseInt(offset.substring(4, 6));
        if (hours > 18 || minutes > 59 || (hours == 18 && minutes != 0)) {
            throw invalid();
        }
        int total = hours * 60 + minutes;
        return offset.charAt(0) == '-' ? -total : total;
    }

    private static boolean insideWindow(
            ParsedTimestamp value, ParsedTimestamp start, ParsedTimestamp end) {
        return value == null || (value.compareTo(start) >= 0 && value.compareTo(end) <= 0);
    }

    private static ParsedTimestamp later(ParsedTimestamp first, ParsedTimestamp second) {
        if (first == null) {
            return second;
        }
        if (second == null) {
            return first;
        }
        return first.compareTo(second) >= 0 ? first : second;
    }

    private static boolean sameTimestamp(ParsedTimestamp first, ParsedTimestamp second) {
        return first == null ? second == null : first.equals(second);
    }

    private static SecurityException invalid() {
        return new SecurityException("invalid_failure_evidence_receipt");
    }

    static final class Snapshot {
        final boolean eventLogAvailable;
        final boolean dumpFolderAvailable;
        final boolean eventScanLimited;
        final boolean dumpScanLimited;
        final boolean hasEvidence;
        final int windowDays;
        final int failureEventCount;
        final int crashCount;
        final int hangCount;
        final int managedRuntimeFailureCount;
        final int windowsErrorReportCount;
        final int dumpCount;
        final String latestEvidenceAt;

        private Snapshot(
                boolean eventLogAvailable,
                boolean dumpFolderAvailable,
                boolean eventScanLimited,
                boolean dumpScanLimited,
                boolean hasEvidence,
                int windowDays,
                int failureEventCount,
                int crashCount,
                int hangCount,
                int managedRuntimeFailureCount,
                int windowsErrorReportCount,
                int dumpCount,
                String latestEvidenceAt) {
            this.eventLogAvailable = eventLogAvailable;
            this.dumpFolderAvailable = dumpFolderAvailable;
            this.eventScanLimited = eventScanLimited;
            this.dumpScanLimited = dumpScanLimited;
            this.hasEvidence = hasEvidence;
            this.windowDays = windowDays;
            this.failureEventCount = failureEventCount;
            this.crashCount = crashCount;
            this.hangCount = hangCount;
            this.managedRuntimeFailureCount = managedRuntimeFailureCount;
            this.windowsErrorReportCount = windowsErrorReportCount;
            this.dumpCount = dumpCount;
            this.latestEvidenceAt = latestEvidenceAt;
        }
    }

    private static final class NullableTimestamp {
        final String value;
        final ParsedTimestamp parsed;

        private NullableTimestamp(String value, ParsedTimestamp parsed) {
            this.value = value;
            this.parsed = parsed;
        }
    }

    private static final class ParsedTimestamp implements Comparable<ParsedTimestamp> {
        final long epochSecond;
        final int nanoseconds;

        private ParsedTimestamp(long epochSecond, int nanoseconds) {
            this.epochSecond = epochSecond;
            this.nanoseconds = nanoseconds;
        }

        @Override
        public int compareTo(ParsedTimestamp other) {
            int seconds = Long.compare(epochSecond, other.epochSecond);
            return seconds != 0 ? seconds : Integer.compare(nanoseconds, other.nanoseconds);
        }

        @Override
        public boolean equals(Object value) {
            if (!(value instanceof ParsedTimestamp)) {
                return false;
            }
            ParsedTimestamp other = (ParsedTimestamp) value;
            return epochSecond == other.epochSecond && nanoseconds == other.nanoseconds;
        }

        @Override
        public int hashCode() {
            return 31 * Long.hashCode(epochSecond) + nanoseconds;
        }
    }
}

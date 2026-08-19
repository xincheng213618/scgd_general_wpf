package com.colorvision.xcviewer;

import org.json.JSONObject;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.Locale;

final class OperationsMonitorEvidenceRevision {
    private OperationsMonitorEvidenceRevision() {
    }

    static Evidence capture(JSONObject monitor, String attentionKey) {
        if (monitor == null || attentionKey == null || attentionKey.isEmpty()) {
            return Evidence.EMPTY;
        }
        switch (attentionKey) {
            case OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE:
                JSONObject performance = object(monitor, "performance");
                JSONObject mainUi = object(performance, "mainUi");
                return evidence(
                        canonical(attentionKey, text(mainUi, "state")), 0L, 1L);
            case OperationsWatchPolicy.ATTENTION_CRITICAL:
            case OperationsWatchPolicy.ATTENTION_ERRORS:
                JSONObject alerts = object(monitor, "alerts");
                int warningCount = count(alerts, "warningCount");
                int errorCount = count(alerts, "errorCount");
                int criticalCount = count(alerts, "criticalCount");
                String latestOccurredAt = text(alerts, "latestOccurredAt");
                return evidence(canonical(
                        attentionKey,
                        Integer.toString(count(alerts, "count")),
                        Integer.toString(warningCount),
                        Integer.toString(errorCount),
                        Integer.toString(criticalCount),
                        text(alerts, "primarySource"),
                        latestOccurredAt),
                        timestamp(latestOccurredAt),
                        (long) criticalCount * 1_000_000L
                                + (long) errorCount * 1_000L
                                + warningCount);
            case OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL:
                JSONObject message = object(monitor, "messageChannel");
                int registered = count(message, "registeredSubscriptionCount");
                int active = count(message, "activeSubscriptionCount");
                String messageState = text(message, "state");
                return evidence(canonical(
                        attentionKey,
                        flag(message, "available"),
                        messageState,
                        flag(message, "connected"),
                        flag(message, "subscriptionReady"),
                        Integer.toString(registered),
                        Integer.toString(active),
                        flag(message, "attentionRequired")),
                        0L,
                        (long) messageRank(messageState) * 1_000_000L
                                + Math.max(0, registered - active));
            case OperationsWatchPolicy.ATTENTION_DEVICES:
                JSONObject devices = object(monitor, "devices");
                int attention = count(devices, "attentionCount");
                int unavailable = count(devices, "unavailableCount");
                int unknown = count(devices, "unknownCount");
                return evidence(canonical(
                        attentionKey,
                        flag(devices, "available"),
                        Integer.toString(count(devices, "totalCount")),
                        Integer.toString(count(devices, "readyCount")),
                        Integer.toString(count(devices, "busyCount")),
                        Integer.toString(count(devices, "closedCount")),
                        Integer.toString(unavailable),
                        Integer.toString(unknown),
                        Integer.toString(attention),
                        Integer.toString(count(devices, "offlineCount")),
                        Integer.toString(count(devices, "uninitializedCount")),
                        Integer.toString(count(devices, "unauthorizedCount")),
                        Integer.toString(count(devices, "unclassifiedUnavailableCount"))),
                        0L,
                        (long) attention * 1_000_000L
                                + (long) unavailable * 1_000L
                                + unknown);
            default:
                return Evidence.EMPTY;
        }
    }

    private static Evidence evidence(String canonical, long sequence, long burden) {
        return new Evidence(sha256(canonical), sequence, burden);
    }

    private static JSONObject object(JSONObject parent, String name) {
        JSONObject value = parent == null ? null : parent.optJSONObject(name);
        return value == null ? new JSONObject() : value;
    }

    private static int count(JSONObject value, String name) {
        return Math.max(0, Math.min(999_999, value.optInt(name, 0)));
    }

    private static String flag(JSONObject value, String name) {
        return value.optBoolean(name, false) ? "1" : "0";
    }

    private static String text(JSONObject value, String name) {
        String text = value.optString(name, "").trim();
        return text.length() > 128 ? text.substring(0, 128) : text;
    }

    private static long timestamp(String value) {
        try {
            return Math.max(0L, Rfc3339Timestamp.parseMilliseconds(value));
        } catch (Exception ignored) {
            return 0L;
        }
    }

    private static int messageRank(String state) {
        switch (state) {
            case "unconfigured":
                return 4;
            case "disconnected":
                return 3;
            case "degraded":
                return 2;
            default:
                return 1;
        }
    }

    private static String canonical(String... values) {
        StringBuilder canonical = new StringBuilder();
        for (String value : values) {
            String safe = value == null ? "" : value;
            canonical.append(safe.length()).append(':').append(safe).append(';');
        }
        return canonical.toString();
    }

    private static String sha256(String value) {
        try {
            byte[] digest = MessageDigest.getInstance("SHA-256")
                    .digest(value.getBytes(StandardCharsets.UTF_8));
            StringBuilder encoded = new StringBuilder(digest.length * 2);
            for (byte item : digest) {
                encoded.append(String.format(Locale.ROOT, "%02x", item & 0xff));
            }
            return encoded.toString();
        } catch (Exception ignored) {
            return String.format(Locale.ROOT, "%064x", value.hashCode());
        }
    }

    static final class Evidence {
        static final Evidence EMPTY = new Evidence("", 0L, 0L);

        final String revision;
        final long sequence;
        final long burden;

        Evidence(String revision, long sequence, long burden) {
            this.revision = revision == null ? "" : revision;
            this.sequence = Math.max(0L, sequence);
            this.burden = Math.max(0L, burden);
        }

        boolean available() {
            return revision.matches("[0-9a-f]{64}");
        }
    }
}

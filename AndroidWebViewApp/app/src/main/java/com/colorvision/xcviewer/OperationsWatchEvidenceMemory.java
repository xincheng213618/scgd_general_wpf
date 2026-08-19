package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

final class OperationsWatchEvidenceMemory {
    private static final int VERSION = 1;
    private static final int MAX_ENTRIES = OperationsProfileRegistry.MAX_PROFILES;
    private static final int MAX_SERIALIZED_LENGTH = 4_096;

    private OperationsWatchEvidenceMemory() {
    }

    static OperationsMonitorEvidenceRevision.Evidence evidence(
            String serialized, String hostId, String attentionKey) {
        if (!validHost(hostId) || !validAttention(attentionKey)) {
            return OperationsMonitorEvidenceRevision.Evidence.EMPTY;
        }
        for (Entry entry : parse(serialized)) {
            if (entry.hostId.equals(hostId) && entry.attentionKey.equals(attentionKey)) {
                return entry.evidence;
            }
        }
        return OperationsMonitorEvidenceRevision.Evidence.EMPTY;
    }

    static String update(
            String serialized,
            String hostId,
            String attentionKey,
            OperationsMonitorEvidenceRevision.Evidence evidence) {
        List<Entry> entries = parse(serialized);
        if (!validHost(hostId)) {
            return serialize(entries);
        }
        removeHostEntries(entries, hostId);
        if (validAttention(attentionKey) && evidence != null && evidence.available()) {
            entries.add(new Entry(hostId, attentionKey, evidence));
        }
        while (entries.size() > MAX_ENTRIES) {
            entries.remove(0);
        }
        return serialize(entries);
    }

    static String removeHost(String serialized, String hostId) {
        return update(
                serialized,
                hostId,
                "",
                OperationsMonitorEvidenceRevision.Evidence.EMPTY);
    }

    private static List<Entry> parse(String serialized) {
        List<Entry> entries = new ArrayList<>();
        if (serialized == null || serialized.isEmpty()
                || serialized.length() > MAX_SERIALIZED_LENGTH) {
            return entries;
        }
        try {
            JSONObject root = new JSONObject(serialized);
            if (root.optInt("version", 0) != VERSION) {
                return entries;
            }
            JSONArray values = root.optJSONArray("entries");
            if (values == null) {
                return entries;
            }
            for (int index = 0; index < values.length() && entries.size() < MAX_ENTRIES;
                    index++) {
                JSONObject value = values.optJSONObject(index);
                if (value == null) {
                    continue;
                }
                Entry entry = new Entry(
                        value.optString("hostId", ""),
                        value.optString("attentionKey", ""),
                        new OperationsMonitorEvidenceRevision.Evidence(
                                value.optString("revision", ""),
                                Math.max(0L, value.optLong("sequence", 0L)),
                                Math.max(0L, value.optLong("burden", 0L))));
                if (validHost(entry.hostId)
                        && validAttention(entry.attentionKey)
                        && entry.evidence.available()) {
                    removeHostEntries(entries, entry.hostId);
                    entries.add(entry);
                }
            }
        } catch (Exception ignored) {
            entries.clear();
        }
        return entries;
    }

    private static String serialize(List<Entry> entries) {
        if (entries.isEmpty()) {
            return "";
        }
        try {
            JSONArray values = new JSONArray();
            for (Entry entry : entries) {
                values.put(new JSONObject()
                        .put("hostId", entry.hostId)
                        .put("attentionKey", entry.attentionKey)
                        .put("revision", entry.evidence.revision)
                        .put("sequence", entry.evidence.sequence)
                        .put("burden", entry.evidence.burden));
            }
            return new JSONObject()
                    .put("version", VERSION)
                    .put("entries", values)
                    .toString();
        } catch (Exception ignored) {
            return "";
        }
    }

    private static boolean validHost(String hostId) {
        return OperationsRelayPolicy.isSafeIdentifier(hostId);
    }

    private static boolean validAttention(String attentionKey) {
        return !OperationsWatchHistory.attentionState(attentionKey).isEmpty();
    }

    private static void removeHostEntries(List<Entry> entries, String hostId) {
        for (int index = entries.size() - 1; index >= 0; index--) {
            if (entries.get(index).hostId.equals(hostId)) {
                entries.remove(index);
            }
        }
    }

    private static final class Entry {
        final String hostId;
        final String attentionKey;
        final OperationsMonitorEvidenceRevision.Evidence evidence;

        Entry(
                String hostId,
                String attentionKey,
                OperationsMonitorEvidenceRevision.Evidence evidence) {
            this.hostId = hostId;
            this.attentionKey = attentionKey;
            this.evidence = evidence;
        }
    }
}

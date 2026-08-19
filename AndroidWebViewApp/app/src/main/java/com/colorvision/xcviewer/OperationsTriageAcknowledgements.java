package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;

final class OperationsTriageAcknowledgements {
    static final int MAX_ENTRIES = 64;
    static final long RETENTION_MILLISECONDS = 30L * 24L * 60L * 60L * 1000L;
    private static final long MAXIMUM_FUTURE_SKEW_MILLISECONDS = 24L * 60L * 60L * 1000L;

    private OperationsTriageAcknowledgements() {
    }

    static boolean contains(
            String serialized,
            String hostId,
            String findingId,
            String revision,
            long nowMilliseconds) {
        for (Entry entry : parse(serialized, nowMilliseconds)) {
            if (entry.matches(hostId, findingId) && entry.revision.equals(revision)) {
                return true;
            }
        }
        return false;
    }

    static String acknowledge(
            String serialized,
            String hostId,
            String findingId,
            String revision,
            long nowMilliseconds) {
        List<Entry> entries = withoutFinding(
                parse(serialized, nowMilliseconds), hostId, findingId);
        if (validIdentity(hostId, findingId, revision) && nowMilliseconds > 0L) {
            entries.add(new Entry(hostId, findingId, revision, nowMilliseconds));
        }
        return serialize(entries);
    }

    static String remove(
            String serialized,
            String hostId,
            String findingId,
            long nowMilliseconds) {
        return serialize(withoutFinding(
                parse(serialized, nowMilliseconds), hostId, findingId));
    }

    static String reconcile(
            String serialized,
            String hostId,
            Map<String, String> currentRevisions,
            long nowMilliseconds) {
        List<Entry> reconciled = new ArrayList<>();
        Map<String, String> safeCurrent = currentRevisions == null
                ? java.util.Collections.emptyMap() : currentRevisions;
        for (Entry entry : parse(serialized, nowMilliseconds)) {
            if (!entry.hostId.equals(hostId)) {
                reconciled.add(entry);
                continue;
            }
            String currentRevision = safeCurrent.get(entry.findingId);
            if (entry.revision.equals(currentRevision)) {
                reconciled.add(entry);
            }
        }
        return serialize(reconciled);
    }

    static String removeHost(String serialized, String hostId, long nowMilliseconds) {
        List<Entry> remaining = new ArrayList<>();
        for (Entry entry : parse(serialized, nowMilliseconds)) {
            if (!entry.hostId.equals(hostId)) {
                remaining.add(entry);
            }
        }
        return serialize(remaining);
    }

    private static List<Entry> withoutFinding(
            List<Entry> entries, String hostId, String findingId) {
        List<Entry> remaining = new ArrayList<>();
        for (Entry entry : entries) {
            if (!entry.matches(hostId, findingId)) {
                remaining.add(entry);
            }
        }
        return remaining;
    }

    private static List<Entry> parse(String serialized, long nowMilliseconds) {
        List<Entry> entries = new ArrayList<>();
        if (serialized == null || serialized.trim().isEmpty()) {
            return entries;
        }
        long cutoff = nowMilliseconds - RETENTION_MILLISECONDS;
        long latestAllowed = nowMilliseconds + MAXIMUM_FUTURE_SKEW_MILLISECONDS;
        try {
            JSONArray values = new JSONArray(serialized);
            for (int index = 0; index < values.length(); index++) {
                JSONObject value = values.optJSONObject(index);
                if (value == null) {
                    continue;
                }
                String hostId = value.optString("h", "");
                String findingId = value.optString("i", "");
                String revision = value.optString("r", "");
                long acknowledgedAt = value.optLong("t", 0L);
                if (validIdentity(hostId, findingId, revision)
                        && acknowledgedAt >= cutoff
                        && acknowledgedAt <= latestAllowed) {
                    entries.add(new Entry(hostId, findingId, revision, acknowledgedAt));
                }
            }
        } catch (Exception ignored) {
            return new ArrayList<>();
        }
        sortByAcknowledgedAt(entries);
        if (entries.size() > MAX_ENTRIES) {
            return new ArrayList<>(entries.subList(entries.size() - MAX_ENTRIES, entries.size()));
        }
        return entries;
    }

    private static String serialize(List<Entry> entries) {
        if (entries == null || entries.isEmpty()) {
            return "";
        }
        sortByAcknowledgedAt(entries);
        int first = Math.max(0, entries.size() - MAX_ENTRIES);
        JSONArray values = new JSONArray();
        for (int index = first; index < entries.size(); index++) {
            Entry entry = entries.get(index);
            JSONObject value = new JSONObject();
            try {
                value.put("h", entry.hostId);
                value.put("i", entry.findingId);
                value.put("r", entry.revision);
                value.put("t", entry.acknowledgedAt);
                values.put(value);
            } catch (Exception ignored) {
            }
        }
        return values.length() == 0 ? "" : values.toString();
    }

    private static void sortByAcknowledgedAt(List<Entry> entries) {
        Collections.sort(entries, (left, right) -> {
            if (left.acknowledgedAt == right.acknowledgedAt) {
                return 0;
            }
            return left.acknowledgedAt < right.acknowledgedAt ? -1 : 1;
        });
    }

    private static boolean validIdentity(String hostId, String findingId, String revision) {
        return hostId != null && !hostId.isEmpty() && hostId.length() <= 256
                && findingId != null && !findingId.isEmpty() && findingId.length() <= 128
                && revision != null && revision.matches("[0-9a-f]{64}");
    }

    private static final class Entry {
        final String hostId;
        final String findingId;
        final String revision;
        final long acknowledgedAt;

        Entry(String hostId, String findingId, String revision, long acknowledgedAt) {
            this.hostId = hostId;
            this.findingId = findingId;
            this.revision = revision;
            this.acknowledgedAt = acknowledgedAt;
        }

        boolean matches(String candidateHostId, String candidateFindingId) {
            return hostId.equals(candidateHostId) && findingId.equals(candidateFindingId);
        }
    }
}

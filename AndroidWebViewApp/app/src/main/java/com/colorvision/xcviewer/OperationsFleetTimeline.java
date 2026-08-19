package com.colorvision.xcviewer;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

final class OperationsFleetTimeline {
    static final int MAX_VISIBLE_ENTRIES = 60;

    private static final long MAXIMUM_FUTURE_SKEW_MILLISECONDS = 60_000L;

    private OperationsFleetTimeline() {
    }

    static Timeline build(
            List<OperationsProfileRegistry.Profile> profiles,
            String activeHostId,
            long nowMilliseconds,
            boolean issuesOnly) {
        List<OperationsProfileRegistry.Profile> safeProfiles = profiles == null
                ? Collections.emptyList() : profiles;
        List<String> baseLabels = new ArrayList<>();
        Map<String, Integer> labelCounts = new HashMap<>();
        for (int index = 0; index < safeProfiles.size(); index++) {
            OperationsProfileRegistry.Profile profile = safeProfiles.get(index);
            String label = profile.label.isEmpty() ? "电脑 " + (index + 1) : profile.label;
            baseLabels.add(label);
            Integer existingCount = labelCounts.get(label);
            labelCounts.put(label, existingCount == null ? 1 : existingCount + 1);
        }

        List<Entry> allEntries = new ArrayList<>();
        for (int profileIndex = 0; profileIndex < safeProfiles.size(); profileIndex++) {
            OperationsProfileRegistry.Profile profile = safeProfiles.get(profileIndex);
            String label = baseLabels.get(profileIndex);
            Integer labelCount = labelCounts.get(label);
            if (labelCount != null && labelCount > 1) {
                label += "（电脑 " + (profileIndex + 1) + "）";
            }
            if (profile.hostId.equals(activeHostId)) {
                label += "（当前）";
            }
            for (OperationsWatchHistory.Entry historyEntry : OperationsWatchHistory.parse(
                    profile.watchHistory, nowMilliseconds)) {
                if (historyEntry.timestampMilliseconds
                        > nowMilliseconds + MAXIMUM_FUTURE_SKEW_MILLISECONDS) {
                    continue;
                }
                allEntries.add(new Entry(
                        historyEntry.timestampMilliseconds,
                        profileIndex,
                        profile.hostId,
                        label,
                        historyEntry.state,
                        isIssueState(historyEntry.state)));
            }
        }
        Collections.sort(allEntries, (left, right) -> {
            int timeComparison = Long.compare(
                    right.timestampMilliseconds, left.timestampMilliseconds);
            return timeComparison != 0
                    ? timeComparison : Integer.compare(left.profileIndex, right.profileIndex);
        });

        int issueEntryCount = 0;
        for (Entry entry : allEntries) {
            if (entry.issue) {
                issueEntryCount++;
            }
        }
        List<Entry> visibleEntries = new ArrayList<>();
        Set<String> matchingHostIds = new HashSet<>();
        int matchingEntryCount = 0;
        for (Entry entry : allEntries) {
            if (issuesOnly && !entry.issue) {
                continue;
            }
            matchingEntryCount++;
            matchingHostIds.add(entry.hostId);
            if (visibleEntries.size() < MAX_VISIBLE_ENTRIES) {
                visibleEntries.add(entry);
            }
        }
        String summary;
        if (matchingEntryCount == 0) {
            summary = issuesOnly
                    ? "近 7 天 · 暂无需关注变化"
                    : "近 7 天 · 暂无状态变化";
        } else {
            summary = "近 7 天 · " + matchingHostIds.size() + " 台电脑 · "
                    + matchingEntryCount + (issuesOnly ? " 条需关注变化" : " 条变化");
        }
        return new Timeline(
                visibleEntries,
                summary,
                allEntries.size(),
                issueEntryCount,
                matchingEntryCount,
                matchingHostIds.size(),
                issuesOnly);
    }

    private static boolean isIssueState(String state) {
        return !OperationsWatchHistory.attentionKey(state).isEmpty()
                || OperationsWatchHistory.STATE_OFFLINE.equals(state)
                || OperationsWatchHistory.STATE_REMOTE_WAITING.equals(state)
                || OperationsWatchHistory.STATE_REVOKED.equals(state);
    }

    static final class Timeline {
        final List<Entry> entries;
        final String summary;
        final int totalEntryCount;
        final int issueEntryCount;
        final int matchingEntryCount;
        final int matchingComputerCount;
        final boolean issuesOnly;

        Timeline(
                List<Entry> entries,
                String summary,
                int totalEntryCount,
                int issueEntryCount,
                int matchingEntryCount,
                int matchingComputerCount,
                boolean issuesOnly) {
            this.entries = Collections.unmodifiableList(new ArrayList<>(entries));
            this.summary = summary;
            this.totalEntryCount = totalEntryCount;
            this.issueEntryCount = issueEntryCount;
            this.matchingEntryCount = matchingEntryCount;
            this.matchingComputerCount = matchingComputerCount;
            this.issuesOnly = issuesOnly;
        }

        boolean truncated() {
            return entries.size() < matchingEntryCount;
        }
    }

    static final class Entry {
        final long timestampMilliseconds;
        final int profileIndex;
        final String hostId;
        final String profileLabel;
        final String state;
        final boolean issue;

        Entry(
                long timestampMilliseconds,
                int profileIndex,
                String hostId,
                String profileLabel,
                String state,
                boolean issue) {
            this.timestampMilliseconds = timestampMilliseconds;
            this.profileIndex = profileIndex;
            this.hostId = hostId;
            this.profileLabel = profileLabel;
            this.state = state;
            this.issue = issue;
        }
    }
}

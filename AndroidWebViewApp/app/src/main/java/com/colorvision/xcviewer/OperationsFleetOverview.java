package com.colorvision.xcviewer;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

final class OperationsFleetOverview {
    static final String ACTION_NONE = "none";
    static final String ACTION_OPEN = "open";
    static final String ACTION_REMOVE = "remove";
    static final long RECENT_CHECK_MILLISECONDS = 10L * 60L * 1000L;

    private static final long MAXIMUM_FUTURE_SKEW_MILLISECONDS = 60_000L;

    private OperationsFleetOverview() {
    }

    static Assessment assess(
            List<OperationsProfileRegistry.Profile> profiles,
            long nowMilliseconds) {
        List<OperationsProfileRegistry.Profile> safeProfiles = profiles == null
                ? Collections.emptyList() : profiles;
        int attention = 0;
        int unavailable = 0;
        int stable = 0;
        int unchecked = 0;
        int revoked = 0;
        int priorityRank = 0;
        String priorityHostId = "";
        String priorityLabel = "";
        String priorityAction = ACTION_NONE;
        String priorityButtonPrefix = "";
        List<Problem> problems = new ArrayList<>();

        for (int index = 0; index < safeProfiles.size(); index++) {
            OperationsProfileRegistry.Profile profile = safeProfiles.get(index);
            ProfileState profileState = classify(profile, nowMilliseconds);
            String profileLabel = profile.label.isEmpty()
                    ? "电脑 " + (index + 1) : profile.label;
            switch (profileState.category) {
                case CATEGORY_ATTENTION:
                    attention++;
                    break;
                case CATEGORY_UNAVAILABLE:
                    unavailable++;
                    break;
                case CATEGORY_STABLE:
                    stable++;
                    break;
                case CATEGORY_REVOKED:
                    revoked++;
                    break;
                default:
                    unchecked++;
                    break;
            }
            if (profileState.priorityRank > priorityRank) {
                priorityRank = profileState.priorityRank;
                priorityHostId = profile.hostId;
                priorityLabel = profileLabel;
                priorityAction = profileState.action;
                priorityButtonPrefix = profileState.buttonPrefix;
            }
            if (profileState.isProblem()) {
                problems.add(new Problem(
                        profile.hostId,
                        profileLabel,
                        profileState.state,
                        OperationsWatchHistory.label(profileState.state),
                        profileState.action,
                        profileState.priorityRank,
                        index));
            }
        }
        Collections.sort(problems, (left, right) -> {
            int priorityComparison = Integer.compare(
                    right.priorityRank, left.priorityRank);
            return priorityComparison != 0
                    ? priorityComparison : Integer.compare(left.profileIndex, right.profileIndex);
        });

        List<String> summaryParts = new ArrayList<>();
        addSummaryPart(summaryParts, "需关注", attention);
        addSummaryPart(summaryParts, "暂不可达", unavailable);
        addSummaryPart(summaryParts, "稳定", stable);
        addSummaryPart(summaryParts, "待巡检", unchecked);
        addSummaryPart(summaryParts, "授权失效", revoked);
        String summary = summaryParts.isEmpty()
                ? "没有配对电脑" : String.join(" · ", summaryParts);
        String priorityButtonLabel = priorityHostId.isEmpty()
                ? "" : priorityButtonPrefix + " · " + priorityLabel;
        return new Assessment(summary, priorityHostId, priorityLabel,
                priorityAction, priorityButtonLabel, problems);
    }

    private static ProfileState classify(
            OperationsProfileRegistry.Profile profile,
            long nowMilliseconds) {
        if (profile.revoked) {
            return new ProfileState(
                    CATEGORY_REVOKED,
                    OperationsWatchHistory.STATE_REVOKED,
                    10,
                    ACTION_REMOVE,
                    "移除失效配对");
        }
        long checkedAt = profile.watchCheckedAt;
        if (checkedAt <= 0L
                || checkedAt > nowMilliseconds + MAXIMUM_FUTURE_SKEW_MILLISECONDS
                || nowMilliseconds - checkedAt > RECENT_CHECK_MILLISECONDS) {
            return ProfileState.unchecked();
        }
        List<OperationsWatchHistory.Entry> entries = OperationsWatchHistory.parse(
                profile.watchHistory, nowMilliseconds);
        if (entries.isEmpty()) {
            return ProfileState.unchecked();
        }
        String state = entries.get(entries.size() - 1).state;
        String attentionKey = OperationsWatchHistory.attentionKey(state);
        int attentionRank = attentionRank(attentionKey);
        if (attentionRank > 0) {
            return new ProfileState(CATEGORY_ATTENTION, state, attentionRank,
                    ACTION_OPEN, "处理首要电脑");
        }
        if (OperationsWatchHistory.STATE_OFFLINE.equals(state)) {
            return new ProfileState(CATEGORY_UNAVAILABLE, state, 70,
                    ACTION_OPEN, "检查首要电脑");
        }
        if (OperationsWatchHistory.STATE_REMOTE_WAITING.equals(state)) {
            return new ProfileState(CATEGORY_UNAVAILABLE, state, 60,
                    ACTION_OPEN, "检查首要电脑");
        }
        if (OperationsWatchHistory.STATE_ONLINE.equals(state)
                || OperationsWatchHistory.STATE_REMOTE_ONLINE.equals(state)) {
            return new ProfileState(CATEGORY_STABLE, state, 0, ACTION_NONE, "");
        }
        return ProfileState.unchecked();
    }

    private static int attentionRank(String attentionKey) {
        if (OperationsWatchPolicy.ATTENTION_UI_UNRESPONSIVE.equals(attentionKey)) {
            return 120;
        }
        if (OperationsWatchPolicy.ATTENTION_CRITICAL.equals(attentionKey)) {
            return 110;
        }
        if (OperationsWatchPolicy.ATTENTION_MESSAGE_CHANNEL.equals(attentionKey)) {
            return 100;
        }
        if (OperationsWatchPolicy.ATTENTION_DEVICES.equals(attentionKey)) {
            return 90;
        }
        if (OperationsWatchPolicy.ATTENTION_ERRORS.equals(attentionKey)) {
            return 80;
        }
        return 0;
    }

    private static void addSummaryPart(List<String> parts, String label, int count) {
        if (count > 0) {
            parts.add(label + " " + count);
        }
    }

    private static final String CATEGORY_ATTENTION = "attention";
    private static final String CATEGORY_UNAVAILABLE = "unavailable";
    private static final String CATEGORY_STABLE = "stable";
    private static final String CATEGORY_UNCHECKED = "unchecked";
    private static final String CATEGORY_REVOKED = "revoked";

    private static final class ProfileState {
        final String category;
        final String state;
        final int priorityRank;
        final String action;
        final String buttonPrefix;

        ProfileState(
                String category,
                String state,
                int priorityRank,
                String action,
                String buttonPrefix) {
            this.category = category;
            this.state = state;
            this.priorityRank = priorityRank;
            this.action = action;
            this.buttonPrefix = buttonPrefix;
        }

        static ProfileState unchecked() {
            return new ProfileState(CATEGORY_UNCHECKED, "", 0, ACTION_NONE, "");
        }

        boolean isProblem() {
            return CATEGORY_ATTENTION.equals(category)
                    || CATEGORY_UNAVAILABLE.equals(category)
                    || CATEGORY_REVOKED.equals(category);
        }
    }

    static final class Problem {
        final String hostId;
        final String profileLabel;
        final String watchState;
        final String summary;
        final String action;
        private final int priorityRank;
        private final int profileIndex;

        Problem(
                String hostId,
                String profileLabel,
                String watchState,
                String summary,
                String action,
                int priorityRank,
                int profileIndex) {
            this.hostId = hostId;
            this.profileLabel = profileLabel;
            this.watchState = watchState;
            this.summary = summary;
            this.action = action;
            this.priorityRank = priorityRank;
            this.profileIndex = profileIndex;
        }

        String destination() {
            if (OperationsWatchHistory.STATE_OFFLINE.equals(watchState)) {
                return OperationsDestinationState.CONNECTION_CHECK;
            }
            if (OperationsWatchHistory.STATE_REMOTE_WAITING.equals(watchState)
                    || OperationsWatchHistory.STATE_REVOKED.equals(watchState)) {
                return OperationsDestinationState.CONNECTIONS;
            }
            String attentionDestination = OperationsWatchPolicy.attentionDestination(
                    OperationsWatchHistory.attentionKey(watchState));
            return attentionDestination.isEmpty()
                    ? OperationsDestinationState.OVERVIEW : attentionDestination;
        }

        String actionDescription() {
            switch (destination()) {
                case OperationsDestinationState.CONNECTION_CHECK:
                    return "切换并运行连接自检";
                case OperationsDestinationState.CONNECTIONS:
                    return "切换并查看电脑与连接";
                case OperationsDestinationState.TRIAGE:
                    return "切换并查看问题";
                default:
                    return "切换并查看电脑";
            }
        }
    }

    static final class Assessment {
        final String summary;
        final String priorityHostId;
        final String priorityLabel;
        final String priorityAction;
        final String priorityButtonLabel;
        final List<Problem> problems;

        Assessment(
                String summary,
                String priorityHostId,
                String priorityLabel,
                String priorityAction,
                String priorityButtonLabel,
                List<Problem> problems) {
            this.summary = summary;
            this.priorityHostId = priorityHostId;
            this.priorityLabel = priorityLabel;
            this.priorityAction = priorityAction;
            this.priorityButtonLabel = priorityButtonLabel;
            this.problems = Collections.unmodifiableList(new ArrayList<>(problems));
        }

        boolean hasPriorityAction() {
            return !ACTION_NONE.equals(priorityAction) && !priorityHostId.isEmpty();
        }

        List<Problem> problemsExcluding(String hostId) {
            List<Problem> result = new ArrayList<>();
            for (Problem problem : problems) {
                if (!problem.hostId.equals(hostId)) {
                    result.add(problem);
                }
            }
            return Collections.unmodifiableList(result);
        }

        Problem findProblem(String hostId) {
            for (Problem problem : problems) {
                if (problem.hostId.equals(hostId)) {
                    return problem;
                }
            }
            return null;
        }
    }
}

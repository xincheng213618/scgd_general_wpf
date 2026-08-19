package com.colorvision.xcviewer;

import org.json.JSONObject;

final class OperationsRemoteProblemRevision {
    private OperationsRemoteProblemRevision() {
    }

    static Identity capture(
            String section,
            JSONObject monitor,
            OperationsDashboardStatusFormatter.Item status) {
        String safeSection = section == null ? "" : section;
        JSONObject source;
        String category;
        String summary;
        int evidenceCount = 1;
        String latestAt = "";
        switch (safeSection) {
            case "flow":
                source = object(monitor, "flow");
                category = "flow";
                summary = canonical(
                        flag(source, "available"),
                        flag(source, "isActive"),
                        text(source, "phase"));
                break;
            case "devices":
                source = object(monitor, "devices");
                category = "devices";
                evidenceCount = count(source, "attentionCount");
                summary = canonical(
                        flag(source, "available"),
                        flag(source, "hasConfiguredDevices"),
                        number(source, "totalCount"),
                        number(source, "readyCount"),
                        number(source, "busyCount"),
                        number(source, "closedCount"),
                        number(source, "unavailableCount"),
                        number(source, "unknownCount"),
                        number(source, "attentionCount"),
                        number(source, "offlineCount"),
                        number(source, "uninitializedCount"),
                        number(source, "unauthorizedCount"),
                        number(source, "unclassifiedUnavailableCount"));
                break;
            case "message":
                source = object(monitor, "messageChannel");
                category = "message-channel";
                summary = canonical(
                        flag(source, "available"),
                        text(source, "state"),
                        flag(source, "connected"),
                        flag(source, "subscriptionReady"),
                        number(source, "registeredSubscriptionCount"),
                        number(source, "activeSubscriptionCount"),
                        flag(source, "attentionRequired"));
                break;
            case "alerts":
                source = object(monitor, "alerts");
                category = "diagnostics";
                int warnings = count(source, "warningCount");
                int errors = count(source, "errorCount");
                int critical = count(source, "criticalCount");
                evidenceCount = warnings + errors + critical;
                latestAt = text(source, "latestOccurredAt");
                summary = canonical(
                        number(source, "count"),
                        Integer.toString(warnings),
                        Integer.toString(errors),
                        Integer.toString(critical),
                        text(source, "primarySource"));
                break;
            case "performance":
                source = object(object(monitor, "performance"), "mainUi");
                category = "desktop";
                summary = canonical(text(source, "state"));
                break;
            case "recovery":
                source = object(monitor, "applicationRecovery");
                category = "recovery";
                summary = canonical(
                        flag(source, "supported"),
                        flag(source, "registered"),
                        flag(source, "automaticWatchdogActive"));
                break;
            default:
                return Identity.EMPTY;
        }
        String findingId = "relay-" + safeSection;
        String title = status == null ? safeSection : status.title;
        return new Identity(
                findingId,
                OperationsTriageFindingRevision.revision(
                        findingId,
                        "attention",
                        category,
                        title,
                        summary,
                        evidenceCount,
                        latestAt));
    }

    private static JSONObject object(JSONObject parent, String name) {
        JSONObject value = parent == null ? null : parent.optJSONObject(name);
        return value == null ? new JSONObject() : value;
    }

    private static int count(JSONObject source, String name) {
        return Math.max(0, Math.min(999, source.optInt(name, 0)));
    }

    private static String number(JSONObject source, String name) {
        return Integer.toString(count(source, name));
    }

    private static String flag(JSONObject source, String name) {
        return source.optBoolean(name, false) ? "1" : "0";
    }

    private static String text(JSONObject source, String name) {
        String value = source.optString(name, "").trim();
        return value.length() > 128 ? value.substring(0, 128) : value;
    }

    private static String canonical(String... values) {
        StringBuilder result = new StringBuilder();
        for (String value : values) {
            String safe = value == null ? "" : value;
            result.append(safe.length()).append(':').append(safe).append(';');
        }
        return result.toString();
    }

    static final class Identity {
        static final Identity EMPTY = new Identity("", "");

        final String findingId;
        final String revision;

        Identity(String findingId, String revision) {
            this.findingId = findingId == null ? "" : findingId;
            this.revision = revision == null ? "" : revision;
        }

        boolean available() {
            return !findingId.isEmpty() && revision.matches("[0-9a-f]{64}");
        }
    }
}

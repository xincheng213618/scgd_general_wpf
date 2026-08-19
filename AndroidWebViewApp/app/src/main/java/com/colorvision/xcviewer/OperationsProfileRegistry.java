package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.net.URI;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;

final class OperationsProfileRegistry {
    static final int MAX_PROFILES = 6;
    private static final int VERSION = 1;
    private static final int MAX_HISTORY_LENGTH = 8_192;

    private OperationsProfileRegistry() {
    }

    static State empty() {
        return new State(Collections.emptyList(), "");
    }

    static State fromLegacy(
            String endpoint,
            String certificatePin,
            String hostId,
            String connectionPreference,
            boolean revoked,
            String watchHistory,
            String relayTaskId,
            String relayTaskCapability,
            String relayTaskIdempotency) {
        Profile profile = createProfile(
                endpoint, certificatePin, hostId, connectionPreference, revoked,
                true, "", watchHistory, 0L,
                relayTaskId, relayTaskCapability, relayTaskIdempotency);
        if (profile == null) {
            return empty();
        }
        return new State(Collections.singletonList(profile), profile.hostId);
    }

    static State parse(String serialized) {
        if (serialized == null || serialized.isEmpty()) {
            return empty();
        }
        try {
            JSONObject root = new JSONObject(serialized);
            if (root.optInt("version", 0) != VERSION) {
                return empty();
            }
            JSONArray values = root.optJSONArray("profiles");
            if (values == null) {
                return empty();
            }
            List<Profile> profiles = new ArrayList<>();
            Set<String> hostIds = new HashSet<>();
            for (int index = 0; index < values.length() && profiles.size() < MAX_PROFILES; index++) {
                JSONObject value = values.optJSONObject(index);
                if (value == null) {
                    continue;
                }
                Profile profile = createProfile(
                        value.optString("endpoint", ""),
                        value.optString("certificatePin", ""),
                        value.optString("hostId", ""),
                        value.optString("connectionPreference", ""),
                        value.optBoolean("revoked", false),
                        value.optBoolean("attentionNotificationsEnabled", true),
                        value.optString("label", ""),
                        value.optString("watchHistory", ""),
                        value.optLong("watchCheckedAt", 0L),
                        value.optString("relayTaskId", ""),
                        value.optString("relayTaskCapability", ""),
                        value.optString("relayTaskIdempotency", ""));
                if (profile != null && hostIds.add(profile.hostId)) {
                    profiles.add(profile);
                }
            }
            if (profiles.isEmpty()) {
                return empty();
            }
            String activeHostId = root.optString("activeHostId", "");
            if (find(profiles, activeHostId) == null) {
                activeHostId = firstUsableHostId(profiles);
                if (activeHostId.isEmpty()) {
                    activeHostId = profiles.get(0).hostId;
                }
            }
            return new State(profiles, activeHostId);
        } catch (Exception ignored) {
            return empty();
        }
    }

    static String serialize(State state) {
        try {
            JSONObject root = new JSONObject()
                    .put("version", VERSION)
                    .put("activeHostId", state.activeHostId);
            JSONArray profiles = new JSONArray();
            for (Profile profile : state.profiles) {
                profiles.put(new JSONObject()
                        .put("hostId", profile.hostId)
                        .put("endpoint", profile.endpoint)
                        .put("certificatePin", profile.certificatePin)
                        .put("connectionPreference", profile.connectionPreference)
                        .put("revoked", profile.revoked)
                        .put("attentionNotificationsEnabled",
                                profile.attentionNotificationsEnabled)
                        .put("label", profile.label)
                        .put("watchHistory", profile.watchHistory)
                        .put("watchCheckedAt", profile.watchCheckedAt)
                        .put("relayTaskId", profile.relayTaskId)
                        .put("relayTaskCapability", profile.relayTaskCapability)
                        .put("relayTaskIdempotency", profile.relayTaskIdempotency));
            }
            root.put("profiles", profiles);
            return root.toString();
        } catch (Exception exception) {
            throw new IllegalStateException("operations_profile_registry_serialize_failed", exception);
        }
    }

    static final class State {
        final List<Profile> profiles;
        final String activeHostId;

        State(List<Profile> profiles, String activeHostId) {
            this.profiles = Collections.unmodifiableList(new ArrayList<>(profiles));
            this.activeHostId = activeHostId == null ? "" : activeHostId;
        }

        Profile active() {
            return find(profiles, activeHostId);
        }

        int usableCount() {
            int count = 0;
            for (Profile profile : profiles) {
                if (!profile.revoked) {
                    count++;
                }
            }
            return count;
        }

        int attentionNotificationsEnabledCount() {
            int count = 0;
            for (Profile profile : profiles) {
                if (!profile.revoked && profile.attentionNotificationsEnabled) {
                    count++;
                }
            }
            return count;
        }

        String activeDisplayLabel() {
            return displayLabel(activeHostId);
        }

        String displayLabel(String hostId) {
            for (int index = 0; index < profiles.size(); index++) {
                Profile profile = profiles.get(index);
                if (profile.hostId.equals(hostId)) {
                    return profile.label.isEmpty() ? "电脑 " + (index + 1) : profile.label;
                }
            }
            return "未选择";
        }

        State upsert(String endpoint, String certificatePin, String hostId) {
            Profile existing = find(profiles, hostId);
            Profile replacement = createProfile(
                    endpoint, certificatePin, hostId, OperationsConnectionPreference.DIRECT,
                    false, true, existing == null ? "" : existing.label,
                    "", 0L, "", "", "");
            if (replacement == null) {
                throw new IllegalArgumentException("invalid_operations_profile");
            }
            List<Profile> updated = new ArrayList<>(profiles);
            for (int index = 0; index < updated.size(); index++) {
                if (hostId.equals(updated.get(index).hostId)) {
                    updated.set(index, replacement);
                    return new State(updated, hostId);
                }
            }
            if (updated.size() >= MAX_PROFILES) {
                throw new IllegalStateException("operations_profile_limit_reached");
            }
            updated.add(replacement);
            return new State(updated, hostId);
        }

        State select(String hostId) {
            Profile profile = find(profiles, hostId);
            if (profile == null || profile.revoked) {
                return this;
            }
            return new State(profiles, hostId);
        }

        State remove(String hostId) {
            List<Profile> updated = new ArrayList<>();
            for (Profile profile : profiles) {
                if (!profile.hostId.equals(hostId)) {
                    updated.add(profile);
                }
            }
            if (updated.size() == profiles.size()) {
                return this;
            }
            if (updated.isEmpty()) {
                return empty();
            }
            String selected = activeHostId;
            if (hostId.equals(activeHostId) || find(updated, selected) == null) {
                selected = firstUsableHostId(updated);
                if (selected.isEmpty()) {
                    selected = updated.get(0).hostId;
                }
            }
            return new State(updated, selected);
        }

        State updateConnectionPreference(String value) {
            Profile active = active();
            return active == null ? this : replace(active.withConnectionPreference(value));
        }

        State rename(String hostId, String label) {
            Profile profile = find(profiles, hostId);
            return profile == null ? this : replace(profile.withLabel(label));
        }

        State updateWatchHistory(String hostId, String value, long checkedAt) {
            Profile profile = find(profiles, hostId);
            return profile == null ? this : replace(profile.withWatchHistory(value, checkedAt));
        }

        State updateAttentionNotificationsEnabled(String hostId, boolean enabled) {
            Profile profile = find(profiles, hostId);
            return profile == null
                    ? this : replace(profile.withAttentionNotificationsEnabled(enabled));
        }

        State updateRelayTask(String taskId, String capabilityId, String idempotencyKey) {
            Profile active = active();
            return active == null ? this
                    : replace(active.withRelayTask(taskId, capabilityId, idempotencyKey));
        }

        State revoke(String hostId) {
            Profile profile = find(profiles, hostId);
            if (profile == null) {
                return this;
            }
            State revoked = replace(profile.withRevoked(true));
            if (!hostId.equals(activeHostId)) {
                return revoked;
            }
            String fallback = firstUsableHostId(revoked.profiles);
            return fallback.isEmpty() ? revoked : new State(revoked.profiles, fallback);
        }

        private State replace(Profile replacement) {
            List<Profile> updated = new ArrayList<>(profiles);
            for (int index = 0; index < updated.size(); index++) {
                if (replacement.hostId.equals(updated.get(index).hostId)) {
                    updated.set(index, replacement);
                    break;
                }
            }
            return new State(updated, activeHostId);
        }
    }

    static final class Profile {
        final String endpoint;
        final String certificatePin;
        final String hostId;
        final String connectionPreference;
        final boolean revoked;
        final boolean attentionNotificationsEnabled;
        final String label;
        final String watchHistory;
        final long watchCheckedAt;
        final String relayTaskId;
        final String relayTaskCapability;
        final String relayTaskIdempotency;

        Profile(
                String endpoint,
                String certificatePin,
                String hostId,
                String connectionPreference,
                boolean revoked,
                boolean attentionNotificationsEnabled,
                String label,
                String watchHistory,
                long watchCheckedAt,
                String relayTaskId,
                String relayTaskCapability,
                String relayTaskIdempotency) {
            this.endpoint = endpoint;
            this.certificatePin = certificatePin;
            this.hostId = hostId;
            this.connectionPreference = OperationsConnectionPreference.normalize(connectionPreference);
            this.revoked = revoked;
            this.attentionNotificationsEnabled = attentionNotificationsEnabled;
            this.label = normalizedLabel(label);
            this.watchHistory = watchHistory;
            this.watchCheckedAt = Math.max(0L, watchCheckedAt);
            this.relayTaskId = relayTaskId;
            this.relayTaskCapability = relayTaskCapability;
            this.relayTaskIdempotency = relayTaskIdempotency;
        }

        private Profile withConnectionPreference(String value) {
            return new Profile(endpoint, certificatePin, hostId, value, revoked,
                    attentionNotificationsEnabled, label, watchHistory, watchCheckedAt,
                    relayTaskId, relayTaskCapability, relayTaskIdempotency);
        }

        private Profile withLabel(String value) {
            return new Profile(endpoint, certificatePin, hostId, connectionPreference, revoked,
                    attentionNotificationsEnabled, value, watchHistory, watchCheckedAt,
                    relayTaskId, relayTaskCapability, relayTaskIdempotency);
        }

        private Profile withWatchHistory(String value, long checkedAt) {
            return new Profile(endpoint, certificatePin, hostId, connectionPreference, revoked,
                    attentionNotificationsEnabled, label, boundedHistory(value), checkedAt,
                    relayTaskId, relayTaskCapability, relayTaskIdempotency);
        }

        private Profile withAttentionNotificationsEnabled(boolean enabled) {
            return new Profile(endpoint, certificatePin, hostId, connectionPreference, revoked,
                    enabled, label, watchHistory, watchCheckedAt,
                    relayTaskId, relayTaskCapability, relayTaskIdempotency);
        }

        private Profile withRelayTask(String taskId, String capabilityId, String idempotencyKey) {
            String safeTaskId = safeTask(taskId, capabilityId, idempotencyKey)
                    ? normalized(taskId) : "";
            String safeCapability = safeTaskId.isEmpty() ? "" : normalized(capabilityId);
            String safeIdempotency = safeTaskId.isEmpty() ? "" : normalized(idempotencyKey);
            return new Profile(endpoint, certificatePin, hostId, connectionPreference, revoked,
                    attentionNotificationsEnabled, label, watchHistory, watchCheckedAt,
                    safeTaskId, safeCapability, safeIdempotency);
        }

        private Profile withRevoked(boolean value) {
            return new Profile(endpoint, certificatePin, hostId, connectionPreference, value,
                    attentionNotificationsEnabled, label, watchHistory, watchCheckedAt,
                    relayTaskId, relayTaskCapability, relayTaskIdempotency);
        }
    }

    private static Profile createProfile(
            String endpoint,
            String certificatePin,
            String hostId,
            String connectionPreference,
            boolean revoked,
            boolean attentionNotificationsEnabled,
            String label,
            String watchHistory,
            long watchCheckedAt,
            String relayTaskId,
            String relayTaskCapability,
            String relayTaskIdempotency) {
        String normalizedEndpoint = endpoint == null ? "" : endpoint.trim();
        String normalizedPin = certificatePin == null ? "" : certificatePin.trim().toLowerCase(Locale.ROOT);
        String normalizedHostId = hostId == null ? "" : hostId.trim();
        if (!isSafeEndpoint(normalizedEndpoint)
                || !normalizedPin.matches("[0-9a-f]{64}")
                || !OperationsRelayPolicy.isSafeIdentifier(normalizedHostId)) {
            return null;
        }
        boolean safeTask = safeTask(relayTaskId, relayTaskCapability, relayTaskIdempotency);
        return new Profile(
                normalizedEndpoint,
                normalizedPin,
                normalizedHostId,
                connectionPreference,
                revoked,
                attentionNotificationsEnabled,
                label,
                boundedHistory(watchHistory),
                Math.max(0L, watchCheckedAt),
                safeTask ? normalized(relayTaskId) : "",
                safeTask ? normalized(relayTaskCapability) : "",
                safeTask ? normalized(relayTaskIdempotency) : "");
    }

    private static boolean isSafeEndpoint(String endpoint) {
        try {
            URI uri = new URI(endpoint);
            return "https".equalsIgnoreCase(uri.getScheme())
                    && uri.getHost() != null
                    && uri.getUserInfo() == null
                    && uri.getFragment() == null;
        } catch (Exception ignored) {
            return false;
        }
    }

    private static boolean safeTask(String taskId, String capabilityId, String idempotencyKey) {
        boolean allEmpty = isEmpty(taskId) && isEmpty(capabilityId) && isEmpty(idempotencyKey);
        return allEmpty || (OperationsRelayPolicy.isSafeIdentifier(taskId)
                && OperationsRelayPolicy.isAllowedTaskCapability(capabilityId)
                && OperationsRelayPolicy.isSafeIdentifier(idempotencyKey));
    }

    private static String boundedHistory(String value) {
        return value == null || value.length() > MAX_HISTORY_LENGTH ? "" : value;
    }

    private static String normalizedLabel(String value) {
        if (value == null) {
            return "";
        }
        StringBuilder result = new StringBuilder();
        String trimmed = value.trim();
        for (int index = 0; index < trimmed.length() && result.length() < 20; index++) {
            char character = trimmed.charAt(index);
            if (!Character.isISOControl(character)) {
                result.append(character);
            }
        }
        return result.toString().trim();
    }

    private static boolean isEmpty(String value) {
        return value == null || value.isEmpty();
    }

    private static String normalized(String value) {
        return value == null ? "" : value;
    }

    private static Profile find(List<Profile> profiles, String hostId) {
        if (hostId == null || hostId.isEmpty()) {
            return null;
        }
        for (Profile profile : profiles) {
            if (hostId.equals(profile.hostId)) {
                return profile;
            }
        }
        return null;
    }

    private static String firstUsableHostId(List<Profile> profiles) {
        for (Profile profile : profiles) {
            if (!profile.revoked) {
                return profile.hostId;
            }
        }
        return "";
    }
}

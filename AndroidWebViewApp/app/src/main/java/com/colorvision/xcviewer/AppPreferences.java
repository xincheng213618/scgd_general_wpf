package com.colorvision.xcviewer;

import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.net.Uri;

import java.util.List;
import java.util.Map;

final class AppPreferences {
    static final String THEME_SYSTEM = "system";
    static final String THEME_LIGHT = "light";
    static final String THEME_DARK = "dark";

    private static final String PREFS_NAME = "colorvision_mobile";
    private static final String KEY_LEGACY_LAN_URL = "lan_url";
    private static final String KEY_THEME_MODE = "theme_mode";
    private static final String KEY_START_TAB = "start_tab";
    private static final String KEY_LEGACY_AUDIO_URI = "audio_uri";
    private static final String KEY_LEGACY_AUDIO_TITLE = "audio_title";
    private static final String KEY_CAMERA_PERMISSION_BLOCKED = "camera_permission_blocked";
    private static final String KEY_NOTIFICATION_PERMISSION_BLOCKED =
            "notification_runtime_request_blocked";
    private static final String KEY_DEVICE_ID = "operations_device_id";
    private static final String KEY_OPERATIONS_PROFILES = "operations_profiles_v1";
    private static final String KEY_OPERATIONS_ENDPOINT = "operations_endpoint";
    private static final String KEY_OPERATIONS_PIN = "operations_certificate_pin";
    private static final String KEY_OPERATIONS_HOST_ID = "operations_host_id";
    private static final String KEY_OPERATIONS_CONNECTION_PREFERENCE =
            "operations_connection_preference";
    private static final String KEY_OPERATIONS_PROFILE_REVOKED = "operations_profile_revoked";
    private static final String KEY_OPERATIONS_WATCH_USER_ENABLED =
            "operations_watch_user_enabled_v2";
    private static final String KEY_LEGACY_OPERATIONS_WATCH_ENABLED = "operations_watch_enabled";
    private static final String KEY_LEGACY_OPERATIONS_WATCH_STATE = "operations_watch_state";
    private static final String KEY_OPERATIONS_WATCH_HISTORY = "operations_watch_history";
    private static final String KEY_OPERATIONS_RELAY_TASK_ID = "operations_relay_task_id";
    private static final String KEY_OPERATIONS_RELAY_TASK_CAPABILITY = "operations_relay_task_capability";
    private static final String KEY_OPERATIONS_RELAY_TASK_IDEMPOTENCY = "operations_relay_task_idempotency";
    private static final String KEY_OPERATIONS_TOOLBOX_RECENTS =
            "operations_toolbox_recents_v1";
    private static final String KEY_OPERATIONS_TRIAGE_ACKNOWLEDGEMENTS =
            "operations_triage_acknowledgements_v1";
    private static final String KEY_OPERATIONS_WATCH_EVIDENCE =
            "operations_watch_evidence_v1";
    private static final Object OPERATIONS_PROFILE_LOCK = new Object();

    private final SharedPreferences preferences;

    AppPreferences(Context context) {
        preferences = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        migrateOperationsProfiles();
        clearLegacyAudioAccess(context);
        if (preferences.contains(KEY_LEGACY_LAN_URL)
                || preferences.contains(KEY_LEGACY_OPERATIONS_WATCH_ENABLED)
                || preferences.contains(KEY_LEGACY_OPERATIONS_WATCH_STATE)) {
            preferences.edit()
                    .remove(KEY_LEGACY_LAN_URL)
                    .remove(KEY_LEGACY_OPERATIONS_WATCH_ENABLED)
                    .remove(KEY_LEGACY_OPERATIONS_WATCH_STATE)
                    .apply();
        }
    }

    String getThemeMode() {
        return preferences.getString(KEY_THEME_MODE, THEME_SYSTEM);
    }

    String getThemeModeLabel() {
        String mode = getThemeMode();
        if (THEME_LIGHT.equals(mode)) {
            return "浅色";
        }
        if (THEME_DARK.equals(mode)) {
            return "深色";
        }
        return "跟随系统";
    }

    void saveThemeMode(String mode, int startTab) {
        preferences.edit()
                .putString(KEY_THEME_MODE, mode)
                .putInt(KEY_START_TAB, startTab)
                .apply();
    }

    int consumeStartTab(int defaultTab) {
        int startTab = preferences.getInt(KEY_START_TAB, defaultTab);
        preferences.edit().remove(KEY_START_TAB).apply();
        return startTab;
    }

    boolean isCameraPermissionBlocked() {
        return preferences.getBoolean(KEY_CAMERA_PERMISSION_BLOCKED, false);
    }

    void saveCameraPermissionBlocked(boolean blocked) {
        preferences.edit().putBoolean(KEY_CAMERA_PERMISSION_BLOCKED, blocked).apply();
    }

    boolean isNotificationPermissionBlocked() {
        return preferences.getBoolean(KEY_NOTIFICATION_PERMISSION_BLOCKED, false);
    }

    void saveNotificationPermissionBlocked(boolean blocked) {
        preferences.edit().putBoolean(KEY_NOTIFICATION_PERMISSION_BLOCKED, blocked).apply();
    }

    boolean isOperationsWatchUserEnabled() {
        return preferences.getBoolean(KEY_OPERATIONS_WATCH_USER_ENABLED, true);
    }

    void saveOperationsWatchUserEnabled(boolean enabled) {
        preferences.edit().putBoolean(KEY_OPERATIONS_WATCH_USER_ENABLED, enabled).apply();
    }

    private void clearLegacyAudioAccess(Context context) {
        String value = preferences.getString(KEY_LEGACY_AUDIO_URI, "");
        if (value != null && !value.isEmpty()) {
            try {
                context.getContentResolver().releasePersistableUriPermission(
                        Uri.parse(value), Intent.FLAG_GRANT_READ_URI_PERMISSION);
            } catch (Exception ignored) {
            }
        }
        if (preferences.contains(KEY_LEGACY_AUDIO_URI)
                || preferences.contains(KEY_LEGACY_AUDIO_TITLE)) {
            preferences.edit()
                    .remove(KEY_LEGACY_AUDIO_URI)
                    .remove(KEY_LEGACY_AUDIO_TITLE)
                    .apply();
        }
    }

    String getOrCreateDeviceId() {
        String existing = preferences.getString(KEY_DEVICE_ID, "");
        if (existing != null && !existing.isEmpty()) {
            return existing;
        }
        String created = java.util.UUID.randomUUID().toString().replace("-", "");
        preferences.edit().putString(KEY_DEVICE_ID, created).apply();
        return created;
    }

    boolean saveOperationsProfile(String endpoint, String certificatePin, String hostId) {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            OperationsProfileRegistry.State state = readOperationsProfiles();
            try {
                writeOperationsProfiles(state.upsert(endpoint, certificatePin, hostId));
                saveOperationsWatchEvidence(
                        hostId, "", OperationsMonitorEvidenceRevision.Evidence.EMPTY);
                return true;
            } catch (IllegalStateException exception) {
                return false;
            }
        }
    }

    String getOperationsEndpoint() {
        OperationsProfileRegistry.Profile profile = activeOperationsProfile();
        return profile == null ? "" : profile.endpoint;
    }

    String getOperationsCertificatePin() {
        OperationsProfileRegistry.Profile profile = activeOperationsProfile();
        return profile == null ? "" : profile.certificatePin;
    }

    String getOperationsHostId() {
        OperationsProfileRegistry.Profile profile = activeOperationsProfile();
        return profile == null ? "" : profile.hostId;
    }

    String getOperationsConnectionPreference() {
        OperationsProfileRegistry.Profile profile = activeOperationsProfile();
        return profile == null ? OperationsConnectionPreference.DIRECT
                : profile.connectionPreference;
    }

    void saveOperationsConnectionPreference(String connectionPreference) {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            writeOperationsProfiles(readOperationsProfiles()
                    .updateConnectionPreference(connectionPreference));
        }
    }

    boolean hasOperationsProfile() {
        OperationsProfileRegistry.Profile profile = activeOperationsProfile();
        return profile != null && !profile.revoked;
    }

    int getOperationsProfileCount() {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            return readOperationsProfiles().profiles.size();
        }
    }

    int getUsableOperationsProfileCount() {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            return readOperationsProfiles().usableCount();
        }
    }

    List<OperationsProfileRegistry.Profile> getOperationsProfiles() {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            return readOperationsProfiles().profiles;
        }
    }

    String getActiveOperationsProfileLabel() {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            return readOperationsProfiles().activeDisplayLabel();
        }
    }

    long getActiveOperationsWatchCheckedAt() {
        OperationsProfileRegistry.Profile profile = activeOperationsProfile();
        return profile == null ? 0L : profile.watchCheckedAt;
    }

    String getOperationsProfileLabel(String hostId) {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            return readOperationsProfiles().displayLabel(hostId);
        }
    }

    boolean selectOperationsProfile(String hostId) {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            OperationsProfileRegistry.State before = readOperationsProfiles();
            OperationsProfileRegistry.State after = before.select(hostId);
            if (after.activeHostId.equals(before.activeHostId)) {
                return hostId != null && hostId.equals(before.activeHostId)
                        && before.active() != null && !before.active().revoked;
            }
            writeOperationsProfiles(after);
            return true;
        }
    }

    void renameOperationsProfile(String hostId, String label) {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            writeOperationsProfiles(readOperationsProfiles().rename(hostId, label));
        }
    }

    void markOperationsProfileRevoked(String hostId) {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            writeOperationsProfiles(readOperationsProfiles().revoke(hostId));
        }
    }

    String getOperationsWatchState() {
        List<OperationsWatchHistory.Entry> entries = getOperationsWatchHistory(
                System.currentTimeMillis());
        return entries.isEmpty() ? "" : entries.get(entries.size() - 1).state;
    }

    List<OperationsWatchHistory.Entry> getOperationsWatchHistory(long nowMilliseconds) {
        OperationsProfileRegistry.Profile profile = activeOperationsProfile();
        return OperationsWatchHistory.parse(
                profile == null ? "" : profile.watchHistory, nowMilliseconds);
    }

    boolean recordOperationsProfileWatchState(
            String hostId, String state, long nowMilliseconds) {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            OperationsProfileRegistry.State profiles = readOperationsProfiles();
            OperationsProfileRegistry.Profile profile = null;
            for (OperationsProfileRegistry.Profile candidate : profiles.profiles) {
                if (candidate.hostId.equals(hostId)) {
                    profile = candidate;
                    break;
                }
            }
            if (profile == null) {
                return false;
            }
            String previousHistory = profile.watchHistory;
            OperationsWatchHistory.Transition transition = OperationsWatchHistory.transition(
                    previousHistory, state, nowMilliseconds);
            if (!transition.currentState.isEmpty()
                    && (transition.changed
                    || !transition.serializedHistory.equals(previousHistory)
                    || profile.watchCheckedAt != nowMilliseconds)) {
                writeOperationsProfiles(profiles.updateWatchHistory(
                        hostId, transition.serializedHistory, nowMilliseconds));
            }
            return transition.changed;
        }
    }

    OperationsMonitorEvidenceRevision.Evidence getOperationsWatchEvidence(
            String hostId, String attentionKey) {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            return OperationsWatchEvidenceMemory.evidence(
                    preferences.getString(KEY_OPERATIONS_WATCH_EVIDENCE, ""),
                    hostId,
                    attentionKey);
        }
    }

    void saveOperationsWatchEvidence(
            String hostId,
            String attentionKey,
            OperationsMonitorEvidenceRevision.Evidence evidence) {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            String updated = OperationsWatchEvidenceMemory.update(
                    preferences.getString(KEY_OPERATIONS_WATCH_EVIDENCE, ""),
                    hostId,
                    attentionKey,
                    evidence);
            SharedPreferences.Editor editor = preferences.edit();
            if (updated.isEmpty()) {
                editor.remove(KEY_OPERATIONS_WATCH_EVIDENCE);
            } else {
                editor.putString(KEY_OPERATIONS_WATCH_EVIDENCE, updated);
            }
            editor.apply();
        }
    }

    void saveOperationsRelayTask(String taskId, String capabilityId, String idempotencyKey) {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            writeOperationsProfiles(readOperationsProfiles().updateRelayTask(
                    taskId, capabilityId, idempotencyKey));
        }
    }

    String getOperationsRelayTaskId() {
        OperationsProfileRegistry.Profile profile = activeOperationsProfile();
        return profile == null ? "" : profile.relayTaskId;
    }

    String getOperationsRelayTaskCapability() {
        OperationsProfileRegistry.Profile profile = activeOperationsProfile();
        return profile == null ? "" : profile.relayTaskCapability;
    }

    String getOperationsRelayTaskIdempotencyKey() {
        OperationsProfileRegistry.Profile profile = activeOperationsProfile();
        return profile == null ? "" : profile.relayTaskIdempotency;
    }

    List<String> getRecentOperationsToolboxActions() {
        return OperationsToolboxRecents.parse(
                preferences.getString(KEY_OPERATIONS_TOOLBOX_RECENTS, ""));
    }

    void recordOperationsToolboxAction(String actionId) {
        String current = preferences.getString(KEY_OPERATIONS_TOOLBOX_RECENTS, "");
        preferences.edit().putString(
                KEY_OPERATIONS_TOOLBOX_RECENTS,
                OperationsToolboxRecents.record(current, actionId)).apply();
    }

    void reconcileOperationsTriageAcknowledgements(
            String hostId, Map<String, String> currentRevisions, long nowMilliseconds) {
        saveOperationsTriageAcknowledgements(OperationsTriageAcknowledgements.reconcile(
                getOperationsTriageAcknowledgements(),
                hostId,
                currentRevisions,
                nowMilliseconds));
    }

    boolean isOperationsTriageFindingAcknowledged(
            String hostId, String findingId, String revision, long nowMilliseconds) {
        return OperationsTriageAcknowledgements.contains(
                getOperationsTriageAcknowledgements(),
                hostId,
                findingId,
                revision,
                nowMilliseconds);
    }

    void saveOperationsTriageFindingAcknowledged(
            String hostId,
            String findingId,
            String revision,
            boolean acknowledged,
            long nowMilliseconds) {
        String current = getOperationsTriageAcknowledgements();
        saveOperationsTriageAcknowledgements(acknowledged
                ? OperationsTriageAcknowledgements.acknowledge(
                        current, hostId, findingId, revision, nowMilliseconds)
                : OperationsTriageAcknowledgements.remove(
                        current, hostId, findingId, nowMilliseconds));
    }

    void removeOperationsProfile(String hostId) {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            writeOperationsProfiles(readOperationsProfiles().remove(hostId));
            saveOperationsTriageAcknowledgements(OperationsTriageAcknowledgements.removeHost(
                    getOperationsTriageAcknowledgements(),
                    hostId,
                    System.currentTimeMillis()));
            saveOperationsWatchEvidence(
                    hostId, "", OperationsMonitorEvidenceRevision.Evidence.EMPTY);
        }
    }

    private String getOperationsTriageAcknowledgements() {
        return preferences.getString(KEY_OPERATIONS_TRIAGE_ACKNOWLEDGEMENTS, "");
    }

    private void saveOperationsTriageAcknowledgements(String serialized) {
        SharedPreferences.Editor editor = preferences.edit();
        if (serialized == null || serialized.isEmpty()) {
            editor.remove(KEY_OPERATIONS_TRIAGE_ACKNOWLEDGEMENTS);
        } else {
            editor.putString(KEY_OPERATIONS_TRIAGE_ACKNOWLEDGEMENTS, serialized);
        }
        editor.apply();
    }

    private OperationsProfileRegistry.Profile activeOperationsProfile() {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            return readOperationsProfiles().active();
        }
    }

    private OperationsProfileRegistry.State readOperationsProfiles() {
        return OperationsProfileRegistry.parse(
                preferences.getString(KEY_OPERATIONS_PROFILES, ""));
    }

    private void writeOperationsProfiles(OperationsProfileRegistry.State state) {
        SharedPreferences.Editor editor = preferences.edit();
        if (state.profiles.isEmpty()) {
            editor.remove(KEY_OPERATIONS_PROFILES);
        } else {
            editor.putString(KEY_OPERATIONS_PROFILES, OperationsProfileRegistry.serialize(state));
        }
        removeLegacyOperationsProfile(editor).apply();
    }

    private void migrateOperationsProfiles() {
        synchronized (OPERATIONS_PROFILE_LOCK) {
            if (preferences.contains(KEY_OPERATIONS_PROFILES)) {
                return;
            }
            OperationsProfileRegistry.State migrated = OperationsProfileRegistry.fromLegacy(
                    preferences.getString(KEY_OPERATIONS_ENDPOINT, ""),
                    preferences.getString(KEY_OPERATIONS_PIN, ""),
                    preferences.getString(KEY_OPERATIONS_HOST_ID, ""),
                    preferences.getString(KEY_OPERATIONS_CONNECTION_PREFERENCE,
                            OperationsConnectionPreference.DIRECT),
                    preferences.getBoolean(KEY_OPERATIONS_PROFILE_REVOKED, false),
                    preferences.getString(KEY_OPERATIONS_WATCH_HISTORY, ""),
                    preferences.getString(KEY_OPERATIONS_RELAY_TASK_ID, ""),
                    preferences.getString(KEY_OPERATIONS_RELAY_TASK_CAPABILITY, ""),
                    preferences.getString(KEY_OPERATIONS_RELAY_TASK_IDEMPOTENCY, ""));
            if (!migrated.profiles.isEmpty()) {
                writeOperationsProfiles(migrated);
            }
        }
    }

    private static SharedPreferences.Editor removeLegacyOperationsProfile(
            SharedPreferences.Editor editor) {
        return editor
                .remove(KEY_OPERATIONS_ENDPOINT)
                .remove(KEY_OPERATIONS_PIN)
                .remove(KEY_OPERATIONS_HOST_ID)
                .remove(KEY_OPERATIONS_CONNECTION_PREFERENCE)
                .remove(KEY_OPERATIONS_PROFILE_REVOKED)
                .remove(KEY_LEGACY_OPERATIONS_WATCH_ENABLED)
                .remove(KEY_LEGACY_OPERATIONS_WATCH_STATE)
                .remove(KEY_OPERATIONS_WATCH_HISTORY)
                .remove(KEY_OPERATIONS_RELAY_TASK_ID)
                .remove(KEY_OPERATIONS_RELAY_TASK_CAPABILITY)
                .remove(KEY_OPERATIONS_RELAY_TASK_IDEMPOTENCY);
    }

}

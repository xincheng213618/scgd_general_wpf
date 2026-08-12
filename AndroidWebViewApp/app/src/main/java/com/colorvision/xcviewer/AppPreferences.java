package com.colorvision.xcviewer;

import android.content.Context;
import android.content.SharedPreferences;
import android.net.Uri;

import java.util.List;

final class AppPreferences {
    static final String THEME_SYSTEM = "system";
    static final String THEME_LIGHT = "light";
    static final String THEME_DARK = "dark";

    private static final String PREFS_NAME = "colorvision_mobile";
    private static final String KEY_LEGACY_LAN_URL = "lan_url";
    private static final String KEY_THEME_MODE = "theme_mode";
    private static final String KEY_START_TAB = "start_tab";
    private static final String KEY_AUDIO_URI = "audio_uri";
    private static final String KEY_AUDIO_TITLE = "audio_title";
    private static final String KEY_DEVICE_ID = "operations_device_id";
    private static final String KEY_OPERATIONS_ENDPOINT = "operations_endpoint";
    private static final String KEY_OPERATIONS_PIN = "operations_certificate_pin";
    private static final String KEY_OPERATIONS_HOST_ID = "operations_host_id";
    private static final String KEY_OPERATIONS_PROFILE_REVOKED = "operations_profile_revoked";
    private static final String KEY_LEGACY_OPERATIONS_WATCH_ENABLED = "operations_watch_enabled";
    private static final String KEY_LEGACY_OPERATIONS_WATCH_STATE = "operations_watch_state";
    private static final String KEY_OPERATIONS_WATCH_HISTORY = "operations_watch_history";
    private static final String KEY_OPERATIONS_RELAY_TASK_ID = "operations_relay_task_id";
    private static final String KEY_OPERATIONS_RELAY_TASK_CAPABILITY = "operations_relay_task_capability";
    private static final String KEY_OPERATIONS_RELAY_TASK_IDEMPOTENCY = "operations_relay_task_idempotency";

    private final SharedPreferences preferences;

    AppPreferences(Context context) {
        preferences = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
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

    void saveAudio(Uri uri, String title) {
        preferences.edit()
                .putString(KEY_AUDIO_URI, uri.toString())
                .putString(KEY_AUDIO_TITLE, title)
                .apply();
    }

    Uri getAudioUri() {
        String value = preferences.getString(KEY_AUDIO_URI, "");
        if (value == null || value.isEmpty()) {
            return null;
        }

        try {
            return Uri.parse(value);
        } catch (Exception ex) {
            return null;
        }
    }

    String getAudioTitle() {
        return getAudioUri() == null ? "未选择音乐" : preferences.getString(KEY_AUDIO_TITLE, "已选择音乐");
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

    void saveOperationsProfile(String endpoint, String certificatePin, String hostId) {
        preferences.edit()
                .putString(KEY_OPERATIONS_ENDPOINT, endpoint)
                .putString(KEY_OPERATIONS_PIN, certificatePin)
                .putString(KEY_OPERATIONS_HOST_ID, hostId)
                .putBoolean(KEY_OPERATIONS_PROFILE_REVOKED, false)
                .remove(KEY_OPERATIONS_WATCH_HISTORY)
                .remove(KEY_OPERATIONS_RELAY_TASK_ID)
                .remove(KEY_OPERATIONS_RELAY_TASK_CAPABILITY)
                .remove(KEY_OPERATIONS_RELAY_TASK_IDEMPOTENCY)
                .apply();
    }

    String getOperationsEndpoint() {
        return preferences.getString(KEY_OPERATIONS_ENDPOINT, "");
    }

    String getOperationsCertificatePin() {
        return preferences.getString(KEY_OPERATIONS_PIN, "");
    }

    String getOperationsHostId() {
        return preferences.getString(KEY_OPERATIONS_HOST_ID, "");
    }

    boolean hasOperationsProfile() {
        return !getOperationsEndpoint().isEmpty()
                && !getOperationsCertificatePin().isEmpty()
                && !getOperationsHostId().isEmpty()
                && !preferences.getBoolean(KEY_OPERATIONS_PROFILE_REVOKED, false);
    }

    void markOperationsProfileRevoked() {
        preferences.edit().putBoolean(KEY_OPERATIONS_PROFILE_REVOKED, true).apply();
    }

    String getOperationsWatchState() {
        List<OperationsWatchHistory.Entry> entries = getOperationsWatchHistory(
                System.currentTimeMillis());
        return entries.isEmpty() ? "" : entries.get(entries.size() - 1).state;
    }

    List<OperationsWatchHistory.Entry> getOperationsWatchHistory(long nowMilliseconds) {
        return OperationsWatchHistory.parse(
                preferences.getString(KEY_OPERATIONS_WATCH_HISTORY, ""), nowMilliseconds);
    }

    boolean recordOperationsWatchState(String state, long nowMilliseconds) {
        String previousHistory = preferences.getString(KEY_OPERATIONS_WATCH_HISTORY, "");
        OperationsWatchHistory.Transition transition = OperationsWatchHistory.transition(
                previousHistory, state, nowMilliseconds);
        if (transition.changed || !transition.serializedHistory.equals(previousHistory)) {
            preferences.edit()
                    .putString(KEY_OPERATIONS_WATCH_HISTORY, transition.serializedHistory)
                    .apply();
        }
        return transition.changed;
    }

    void saveOperationsRelayTask(String taskId, String capabilityId, String idempotencyKey) {
        preferences.edit()
                .putString(KEY_OPERATIONS_RELAY_TASK_ID, taskId)
                .putString(KEY_OPERATIONS_RELAY_TASK_CAPABILITY, capabilityId)
                .putString(KEY_OPERATIONS_RELAY_TASK_IDEMPOTENCY, idempotencyKey)
                .apply();
    }

    String getOperationsRelayTaskId() {
        return preferences.getString(KEY_OPERATIONS_RELAY_TASK_ID, "");
    }

    String getOperationsRelayTaskCapability() {
        return preferences.getString(KEY_OPERATIONS_RELAY_TASK_CAPABILITY, "");
    }

    String getOperationsRelayTaskIdempotencyKey() {
        return preferences.getString(KEY_OPERATIONS_RELAY_TASK_IDEMPOTENCY, "");
    }

    void clearOperationsProfile() {
        preferences.edit()
                .remove(KEY_OPERATIONS_ENDPOINT)
                .remove(KEY_OPERATIONS_PIN)
                .remove(KEY_OPERATIONS_HOST_ID)
                .remove(KEY_OPERATIONS_PROFILE_REVOKED)
                .remove(KEY_LEGACY_OPERATIONS_WATCH_ENABLED)
                .remove(KEY_LEGACY_OPERATIONS_WATCH_STATE)
                .remove(KEY_OPERATIONS_WATCH_HISTORY)
                .remove(KEY_OPERATIONS_RELAY_TASK_ID)
                .remove(KEY_OPERATIONS_RELAY_TASK_CAPABILITY)
                .remove(KEY_OPERATIONS_RELAY_TASK_IDEMPOTENCY)
                .apply();
    }

}

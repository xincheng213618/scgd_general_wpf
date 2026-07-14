package com.colorvision.xcviewer;

import android.content.Context;
import android.content.SharedPreferences;
import android.net.Uri;

final class AppPreferences {
    static final String THEME_SYSTEM = "system";
    static final String THEME_LIGHT = "light";
    static final String THEME_DARK = "dark";

    private static final String PREFS_NAME = "colorvision_mobile";
    private static final String KEY_LAN_URL = "lan_url";
    private static final String KEY_THEME_MODE = "theme_mode";
    private static final String KEY_START_TAB = "start_tab";
    private static final String KEY_AUDIO_URI = "audio_uri";
    private static final String KEY_AUDIO_TITLE = "audio_title";
    private static final String KEY_DEVICE_ID = "operations_device_id";
    private static final String KEY_OPERATIONS_ENDPOINT = "operations_endpoint";
    private static final String KEY_OPERATIONS_PIN = "operations_certificate_pin";
    private static final String KEY_OPERATIONS_HOST_ID = "operations_host_id";

    private final SharedPreferences preferences;

    AppPreferences(Context context) {
        preferences = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
    }

    String getLanUrl() {
        String value = preferences.getString(KEY_LAN_URL, "");
        if (containsUrlCredential(value)) {
            preferences.edit().remove(KEY_LAN_URL).apply();
            return "";
        }
        return value;
    }

    void saveLanUrl(String url) {
        if (containsUrlCredential(url)) {
            clearLanUrl();
            return;
        }
        preferences.edit().putString(KEY_LAN_URL, url).apply();
    }

    void clearLanUrl() {
        preferences.edit().remove(KEY_LAN_URL).apply();
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
                && !getOperationsHostId().isEmpty();
    }

    void clearOperationsProfile() {
        preferences.edit()
                .remove(KEY_OPERATIONS_ENDPOINT)
                .remove(KEY_OPERATIONS_PIN)
                .remove(KEY_OPERATIONS_HOST_ID)
                .apply();
    }

    static boolean containsUrlCredential(String value) {
        if (value == null || value.isEmpty()) {
            return false;
        }
        try {
            Uri uri = Uri.parse(value);
            for (String name : uri.getQueryParameterNames()) {
                if ("token".equalsIgnoreCase(name) || "access_token".equalsIgnoreCase(name)) {
                    return true;
                }
            }
            return false;
        } catch (Exception ignored) {
            return true;
        }
    }
}

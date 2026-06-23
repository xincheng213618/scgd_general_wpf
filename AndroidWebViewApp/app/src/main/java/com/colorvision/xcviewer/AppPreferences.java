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

    private final SharedPreferences preferences;

    AppPreferences(Context context) {
        preferences = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
    }

    String getLanUrl() {
        return preferences.getString(KEY_LAN_URL, "");
    }

    void saveLanUrl(String url) {
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
}

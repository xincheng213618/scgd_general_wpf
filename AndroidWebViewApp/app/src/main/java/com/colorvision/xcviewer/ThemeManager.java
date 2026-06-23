package com.colorvision.xcviewer;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Context;
import android.content.res.Configuration;
import android.graphics.Color;
import android.os.Build;
import android.view.View;

final class ThemeManager {
    private final Context context;
    private final AppPreferences preferences;

    ThemeManager(Context context, AppPreferences preferences) {
        this.context = context;
        this.preferences = preferences;
    }

    void applySystemBars(Activity activity) {
        activity.getWindow().setStatusBarColor(shellBackgroundColor());
        activity.getWindow().setNavigationBarColor(bottomNavBackgroundColor());

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            int flags = activity.getWindow().getDecorView().getSystemUiVisibility();
            if (isDarkTheme()) {
                flags &= ~View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                    flags &= ~View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
                }
            } else {
                flags |= View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                    flags |= View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
                }
            }
            activity.getWindow().getDecorView().setSystemUiVisibility(flags);
        }
    }

    void showThemeDialog(Activity activity, int startTab, Runnable onChanged) {
        String[] labels = {"跟随系统", "浅色", "深色"};
        String[] values = {AppPreferences.THEME_SYSTEM, AppPreferences.THEME_LIGHT, AppPreferences.THEME_DARK};
        String current = preferences.getThemeMode();
        int checked = 0;
        for (int i = 0; i < values.length; i++) {
            if (values[i].equals(current)) {
                checked = i;
                break;
            }
        }

        int dialogTheme = isDarkTheme()
                ? AlertDialog.THEME_DEVICE_DEFAULT_DARK
                : AlertDialog.THEME_DEVICE_DEFAULT_LIGHT;
        new AlertDialog.Builder(activity, dialogTheme)
                .setTitle("主题模式")
                .setSingleChoiceItems(labels, checked, (dialog, which) -> {
                    preferences.saveThemeMode(values[which], startTab);
                    dialog.dismiss();
                    onChanged.run();
                })
                .setNegativeButton("取消", null)
                .show();
    }

    String getThemeModeLabel() {
        return preferences.getThemeModeLabel();
    }

    int shellBackgroundColor() {
        return isDarkTheme() ? Color.rgb(17, 24, 39) : Color.rgb(242, 247, 255);
    }

    int pageBackgroundColor() {
        return isDarkTheme() ? Color.rgb(12, 18, 31) : Color.rgb(245, 247, 251);
    }

    int settingsBackgroundColor() {
        return isDarkTheme() ? Color.rgb(10, 15, 27) : Color.rgb(239, 242, 247);
    }

    int cardBackgroundColor() {
        return isDarkTheme() ? Color.rgb(24, 32, 48) : Color.WHITE;
    }

    int bottomNavBackgroundColor() {
        return isDarkTheme() ? Color.rgb(18, 25, 38) : Color.WHITE;
    }

    int primaryTextColor() {
        return isDarkTheme() ? Color.rgb(235, 241, 250) : Color.rgb(24, 32, 51);
    }

    int secondaryTextColor() {
        return isDarkTheme() ? Color.rgb(168, 181, 201) : Color.rgb(96, 112, 139);
    }

    int mutedTextColor() {
        return isDarkTheme() ? Color.rgb(128, 143, 166) : Color.rgb(112, 125, 145);
    }

    int inactiveTabColor() {
        return isDarkTheme() ? Color.rgb(108, 121, 143) : Color.rgb(143, 154, 171);
    }

    int dividerColor() {
        return isDarkTheme() ? Color.rgb(43, 54, 75) : Color.rgb(235, 239, 245);
    }

    int borderColor() {
        return isDarkTheme() ? Color.rgb(55, 68, 92) : Color.rgb(221, 228, 239);
    }

    int inputBackgroundColor() {
        return isDarkTheme() ? Color.rgb(17, 24, 39) : Color.rgb(248, 250, 253);
    }

    int secondaryButtonBackgroundColor() {
        return isDarkTheme() ? Color.rgb(38, 49, 68) : Color.rgb(238, 242, 247);
    }

    private boolean isDarkTheme() {
        String mode = preferences.getThemeMode();
        if (AppPreferences.THEME_DARK.equals(mode)) {
            return true;
        }
        if (AppPreferences.THEME_LIGHT.equals(mode)) {
            return false;
        }

        int nightMode = context.getResources().getConfiguration().uiMode & Configuration.UI_MODE_NIGHT_MASK;
        return nightMode == Configuration.UI_MODE_NIGHT_YES;
    }
}

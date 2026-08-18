package com.colorvision.xcviewer;

import android.app.Activity;
import android.content.Context;
import android.graphics.Color;
import android.os.Build;
import android.view.View;

import androidx.appcompat.app.AppCompatDelegate;
import androidx.appcompat.R;

import com.google.android.material.color.MaterialColors;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;

final class ThemeManager {
    private final Context context;
    private final AppPreferences preferences;

    ThemeManager(Context context, AppPreferences preferences) {
        this.context = context;
        this.preferences = preferences;
    }

    static void applySavedMode(AppPreferences preferences) {
        String mode = preferences.getThemeMode();
        if (AppPreferences.THEME_DARK.equals(mode)) {
            AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_YES);
        } else if (AppPreferences.THEME_LIGHT.equals(mode)) {
            AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        } else {
            AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_FOLLOW_SYSTEM);
        }
    }

    void applySystemBars(Activity activity) {
        int surface = cardBackgroundColor();
        activity.getWindow().setStatusBarColor(surface);
        activity.getWindow().setNavigationBarColor(surface);

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            int flags = activity.getWindow().getDecorView().getSystemUiVisibility();
            boolean lightSurface = MaterialColors.isColorLight(surface);
            if (lightSurface) {
                flags |= View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                    flags |= View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
                }
            } else {
                flags &= ~View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR;
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                    flags &= ~View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
                }
            }
            activity.getWindow().getDecorView().setSystemUiVisibility(flags);
        }
    }

    void showThemeDialog(Activity activity, int startTab) {
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

        new MaterialAlertDialogBuilder(activity)
                .setTitle("主题模式")
                .setSingleChoiceItems(labels, checked, (dialog, which) -> {
                    preferences.saveThemeMode(values[which], startTab);
                    dialog.dismiss();
                    applySavedMode(preferences);
                })
                .setNegativeButton("取消", null)
                .show();
    }

    String getThemeModeLabel() {
        return preferences.getThemeModeLabel();
    }

    int primaryColor() {
        return color(R.attr.colorPrimary, Color.BLACK);
    }

    int onPrimaryColor() {
        return color(com.google.android.material.R.attr.colorOnPrimary, Color.WHITE);
    }

    int primaryContainerColor() {
        return color(com.google.android.material.R.attr.colorPrimaryContainer, cardBackgroundColor());
    }

    int onPrimaryContainerColor() {
        return color(com.google.android.material.R.attr.colorOnPrimaryContainer, primaryTextColor());
    }

    int shellBackgroundColor() {
        return color(com.google.android.material.R.attr.colorSurfaceContainer, pageBackgroundColor());
    }

    int pageBackgroundColor() {
        return color(com.google.android.material.R.attr.colorSurface, Color.WHITE);
    }

    int settingsBackgroundColor() {
        return pageBackgroundColor();
    }

    int cardBackgroundColor() {
        return color(com.google.android.material.R.attr.colorSurfaceContainerLow, pageBackgroundColor());
    }

    int bottomNavBackgroundColor() {
        return color(com.google.android.material.R.attr.colorSurfaceContainer, cardBackgroundColor());
    }

    int primaryTextColor() {
        return color(com.google.android.material.R.attr.colorOnSurface, Color.BLACK);
    }

    int secondaryTextColor() {
        return color(com.google.android.material.R.attr.colorOnSurfaceVariant, primaryTextColor());
    }

    int mutedTextColor() {
        return secondaryTextColor();
    }

    int inactiveTabColor() {
        return secondaryTextColor();
    }

    int dividerColor() {
        return color(com.google.android.material.R.attr.colorOutlineVariant, borderColor());
    }

    int borderColor() {
        return color(com.google.android.material.R.attr.colorOutline, secondaryTextColor());
    }

    private int color(int attribute, int fallback) {
        return MaterialColors.getColor(context, attribute, fallback);
    }
}

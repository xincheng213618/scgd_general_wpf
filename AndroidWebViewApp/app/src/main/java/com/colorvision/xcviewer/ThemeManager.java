package com.colorvision.xcviewer;

import android.app.Activity;
import android.app.Application;
import android.content.Context;
import android.graphics.Color;
import android.view.Window;

import androidx.appcompat.app.AppCompatDelegate;
import androidx.appcompat.R;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsControllerCompat;

import com.google.android.material.color.DynamicColors;
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

    static void applyDynamicColors(Application application) {
        DynamicColors.applyToActivitiesIfAvailable(application);
    }

    void applySystemBars(Activity activity) {
        int surface = cardBackgroundColor();
        Window window = activity.getWindow();
        window.setStatusBarColor(surface);
        window.setNavigationBarColor(surface);

        boolean lightSurface = MaterialColors.isColorLight(surface);
        WindowInsetsControllerCompat controller = WindowCompat.getInsetsController(
                window, window.getDecorView());
        controller.setAppearanceLightStatusBars(lightSurface);
        controller.setAppearanceLightNavigationBars(lightSurface);
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

    int primaryContainerColor() {
        return color(com.google.android.material.R.attr.colorPrimaryContainer, cardBackgroundColor());
    }

    int onPrimaryContainerColor() {
        return color(com.google.android.material.R.attr.colorOnPrimaryContainer, primaryTextColor());
    }

    int secondaryContainerColor() {
        return color(com.google.android.material.R.attr.colorSecondaryContainer, cardBackgroundColor());
    }

    int onSecondaryContainerColor() {
        return color(com.google.android.material.R.attr.colorOnSecondaryContainer, primaryTextColor());
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

    int errorColor() {
        return color(R.attr.colorError, Color.RED);
    }

    int errorContainerColor() {
        return color(com.google.android.material.R.attr.colorErrorContainer, cardBackgroundColor());
    }

    int onErrorContainerColor() {
        return color(com.google.android.material.R.attr.colorOnErrorContainer, primaryTextColor());
    }

    private int color(int attribute, int fallback) {
        return MaterialColors.getColor(context, attribute, fallback);
    }
}

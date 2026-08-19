package com.colorvision.xcviewer;

import android.app.Application;

public final class ColorVisionApplication extends Application {
    @Override
    public void onCreate() {
        super.onCreate();
        ThemeManager.applySavedMode(new AppPreferences(this));
        ThemeManager.applyDynamicColors(this);
    }
}

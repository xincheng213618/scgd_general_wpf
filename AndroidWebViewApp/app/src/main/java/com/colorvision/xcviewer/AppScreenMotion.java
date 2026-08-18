package com.colorvision.xcviewer;

import android.app.Activity;
import android.app.ActivityOptions;
import android.content.Intent;
import android.view.ViewGroup;

import androidx.transition.TransitionManager;

import com.google.android.material.transition.MaterialSharedAxis;

final class AppScreenMotion {
    static final int DIRECTION_NONE = 0;
    static final int DIRECTION_FORWARD = 1;
    static final int DIRECTION_BACKWARD = -1;

    private AppScreenMotion() {
    }

    static int directionBetween(int fromTab, int toTab, int operationsTab, int settingsTab) {
        if (fromTab == operationsTab && toTab == settingsTab) {
            return DIRECTION_FORWARD;
        }
        if (fromTab == settingsTab && toTab == operationsTab) {
            return DIRECTION_BACKWARD;
        }
        return DIRECTION_NONE;
    }

    static boolean usesSharedAxis(int direction) {
        return direction == DIRECTION_FORWARD || direction == DIRECTION_BACKWARD;
    }

    static void configureOperationsActivity(Activity activity) {
        activity.getWindow().setExitTransition(sharedAxisPlatform(true));
        activity.getWindow().setReenterTransition(sharedAxisPlatform(false));
    }

    static void configureSettingsActivity(Activity activity) {
        activity.getWindow().setEnterTransition(sharedAxisPlatform(true));
        activity.getWindow().setReturnTransition(sharedAxisPlatform(false));
    }

    static void startForward(Activity activity, Intent intent) {
        activity.startActivity(intent, ActivityOptions.makeSceneTransitionAnimation(activity).toBundle());
    }

    static void beginContentTransition(ViewGroup container, int direction) {
        if (!usesSharedAxis(direction) || container.getChildCount() == 0) {
            return;
        }
        MaterialSharedAxis transition = new MaterialSharedAxis(
                MaterialSharedAxis.X,
                direction == DIRECTION_FORWARD);
        TransitionManager.beginDelayedTransition(container, transition);
    }

    private static com.google.android.material.transition.platform.MaterialSharedAxis sharedAxisPlatform(
            boolean forward) {
        return new com.google.android.material.transition.platform.MaterialSharedAxis(
                com.google.android.material.transition.platform.MaterialSharedAxis.X,
                forward);
    }
}

package com.colorvision.xcviewer;

import android.app.Activity;
import android.os.Build;

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

    static void configureSettingsActivity(Activity activity) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            activity.overrideActivityTransition(
                    Activity.OVERRIDE_TRANSITION_OPEN,
                    R.anim.screen_enter_from_right,
                    R.anim.screen_exit_to_left);
            activity.overrideActivityTransition(
                    Activity.OVERRIDE_TRANSITION_CLOSE,
                    R.anim.screen_enter_from_left,
                    R.anim.screen_exit_to_right);
        }
    }

    @SuppressWarnings("deprecation")
    static void applyForward(Activity activity) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            activity.overridePendingTransition(
                    R.anim.screen_enter_from_right,
                    R.anim.screen_exit_to_left);
        }
    }

    @SuppressWarnings("deprecation")
    static void applyBackward(Activity activity) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            activity.overridePendingTransition(
                    R.anim.screen_enter_from_left,
                    R.anim.screen_exit_to_right);
        }
    }
}

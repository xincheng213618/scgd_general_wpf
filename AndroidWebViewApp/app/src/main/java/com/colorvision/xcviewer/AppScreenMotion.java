package com.colorvision.xcviewer;

import android.app.Activity;
import android.app.ActivityOptions;
import android.content.Intent;
import android.os.Build;
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

    static void configureSettingsActivity(Activity activity) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            int[] animations = activityAnimations(activity, false);
            activity.overrideActivityTransition(
                    Activity.OVERRIDE_TRANSITION_CLOSE,
                    animations[0],
                    animations[1]);
        }
    }

    static void startForward(Activity activity, Intent intent) {
        int[] animations = activityAnimations(activity, true);
        activity.startActivity(intent, ActivityOptions.makeCustomAnimation(
                activity,
                animations[0],
                animations[1]).toBundle());
    }

    @SuppressWarnings("deprecation")
    static void finishBackward(Activity activity) {
        activity.finish();
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            int[] animations = activityAnimations(activity, false);
            activity.overridePendingTransition(animations[0], animations[1]);
        }
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

    private static int[] activityAnimations(Activity activity, boolean forward) {
        boolean rtl = activity.getResources().getConfiguration().getLayoutDirection()
                == android.view.View.LAYOUT_DIRECTION_RTL;
        boolean enterFromRight = entersFromRight(forward, rtl);
        return enterFromRight
                ? new int[]{R.anim.m3_screen_enter_from_right, R.anim.m3_screen_exit_to_left}
                : new int[]{R.anim.m3_screen_enter_from_left, R.anim.m3_screen_exit_to_right};
    }

    static boolean entersFromRight(boolean forward, boolean rtl) {
        return forward != rtl;
    }
}

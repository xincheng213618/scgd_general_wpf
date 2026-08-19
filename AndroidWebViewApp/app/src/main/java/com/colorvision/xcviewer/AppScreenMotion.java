package com.colorvision.xcviewer;

import android.app.Activity;
import android.app.ActivityOptions;
import android.content.Intent;
import android.os.Build;
import android.view.ViewGroup;

import androidx.transition.TransitionManager;

import com.google.android.material.transition.MaterialFadeThrough;
import com.google.android.material.transition.MaterialSharedAxis;

final class AppScreenMotion {
    static final int DIRECTION_NONE = 0;
    static final int DIRECTION_FORWARD = 1;
    static final int DIRECTION_BACKWARD = -1;

    private AppScreenMotion() {
    }

    static int directionBetween(
            int fromTab,
            int toTab,
            int operationsTab,
            int toolsTab,
            int settingsTab) {
        int fromIndex = tabIndex(fromTab, operationsTab, toolsTab, settingsTab);
        int toIndex = tabIndex(toTab, operationsTab, toolsTab, settingsTab);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex) {
            return DIRECTION_NONE;
        }
        return fromIndex < toIndex ? DIRECTION_FORWARD : DIRECTION_BACKWARD;
    }

    private static int tabIndex(int tab, int operationsTab, int toolsTab, int settingsTab) {
        if (tab == operationsTab) {
            return 0;
        }
        if (tab == toolsTab) {
            return 1;
        }
        return tab == settingsTab ? 2 : -1;
    }

    static boolean usesFadeThrough(int direction, boolean topLevelTransition) {
        return topLevelTransition && isDirectional(direction);
    }

    static boolean usesSharedAxis(int direction, boolean topLevelTransition) {
        return !topLevelTransition && isDirectional(direction);
    }

    private static boolean isDirectional(int direction) {
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

    static void startBackward(Activity activity, Intent intent) {
        int[] animations = activityAnimations(activity, false);
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
        beginContentTransition(container, direction, false);
    }

    static void beginContentTransition(
            ViewGroup container, int direction, boolean topLevelTransition) {
        if (!isDirectional(direction) || container.getChildCount() == 0) {
            return;
        }
        if (usesFadeThrough(direction, topLevelTransition)) {
            TransitionManager.beginDelayedTransition(container, new MaterialFadeThrough());
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

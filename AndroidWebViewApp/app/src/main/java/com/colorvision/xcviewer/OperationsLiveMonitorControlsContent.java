package com.colorvision.xcviewer;

import android.app.Activity;
import android.content.res.ColorStateList;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.widget.TextViewCompat;

import com.google.android.material.card.MaterialCardView;
import com.google.android.material.materialswitch.MaterialSwitch;

final class OperationsLiveMonitorControlsContent {
    private OperationsLiveMonitorControlsContent() {
    }

    static View create(
            Activity activity,
            ThemeManager themeManager,
            OperationsLiveMonitorControlsPresentation.ViewModel model,
            Handler handler) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        rows.addView(autoRefreshRow(activity, themeManager, model, handler), matchWidth());
        rows.addView(divider(activity, themeManager), dividerParams(activity));
        rows.addView(refreshRow(activity, themeManager, model, handler), matchWidth());

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(rows, cardWidth());
        return card;
    }

    private static View autoRefreshRow(
            Activity activity,
            ThemeManager themeManager,
            OperationsLiveMonitorControlsPresentation.ViewModel model,
            Handler handler) {
        LinearLayout row = baseRow(activity);
        LinearLayout copy = copy(
                activity,
                themeManager,
                "自动观察",
                model.autoRefreshSummary);
        row.addView(copy, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));

        MaterialSwitch toggle = new MaterialSwitch(activity);
        toggle.setChecked(model.autoRefresh);
        toggle.setEnabled(model.toggleEnabled);
        toggle.setContentDescription(model.autoRefreshAccessibilityLabel());
        toggle.setOnCheckedChangeListener((button, checked) ->
                handler.onAutoRefreshChanged(checked));
        row.addView(toggle, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));

        configureToggleRow(activity, row, model.toggleEnabled, toggle);
        hideChildrenFromAccessibility(copy);
        return row;
    }

    private static View refreshRow(
            Activity activity,
            ThemeManager themeManager,
            OperationsLiveMonitorControlsPresentation.ViewModel model,
            Handler handler) {
        LinearLayout row = baseRow(activity);
        LinearLayout copy = copy(
                activity,
                themeManager,
                model.refreshLabel,
                model.refreshSummary);
        row.addView(copy, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));

        ImageView refresh = new ImageView(activity);
        refresh.setImageResource(R.drawable.ic_refresh_24);
        refresh.setImageTintList(ColorStateList.valueOf(themeManager.primaryColor()));
        refresh.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        LinearLayout.LayoutParams iconParams = new LinearLayout.LayoutParams(
                dp(activity, 24), dp(activity, 24));
        iconParams.setMargins(dp(activity, 12), 0, 0, 0);
        row.addView(refresh, iconParams);

        configureActionRow(
                activity,
                row,
                model.refreshEnabled,
                model.refreshAccessibilityLabel(),
                view -> handler.onRefresh());
        hideChildrenFromAccessibility(copy);
        return row;
    }

    private static LinearLayout baseRow(Activity activity) {
        LinearLayout row = new LinearLayout(activity);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setMinimumHeight(dp(activity, 72));
        row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 12), dp(activity, 10));
        return row;
    }

    private static LinearLayout copy(
            Activity activity,
            ThemeManager themeManager,
            String title,
            String summary) {
        LinearLayout copy = new LinearLayout(activity);
        copy.setOrientation(LinearLayout.VERTICAL);
        copy.addView(text(
                activity,
                title,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                themeManager.primaryTextColor()), matchWidth());
        copy.addView(text(
                activity,
                summary,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.mutedTextColor()), topMargin(dp(activity, 2)));
        return copy;
    }

    private static void configureActionRow(
            Activity activity,
            View row,
            boolean enabled,
            String contentDescription,
            View.OnClickListener listener) {
        row.setEnabled(enabled);
        row.setAlpha(enabled ? 1f : 0.55f);
        row.setContentDescription(contentDescription);
        row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
        row.setClickable(enabled);
        row.setFocusable(enabled);
        if (enabled) {
            applySelectableBackground(activity, row);
            row.setOnClickListener(listener);
        }
    }

    private static void configureToggleRow(
            Activity activity,
            View row,
            boolean enabled,
            MaterialSwitch toggle) {
        row.setEnabled(enabled);
        row.setAlpha(enabled ? 1f : 0.55f);
        row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        row.setClickable(enabled);
        if (enabled) {
            applySelectableBackground(activity, row);
            row.setOnClickListener(view -> toggle.performClick());
        }
    }

    private static void hideChildrenFromAccessibility(LinearLayout parent) {
        for (int index = 0; index < parent.getChildCount(); index++) {
            parent.getChildAt(index).setImportantForAccessibility(
                    View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        }
    }

    private static TextView text(Activity activity, String value, int appearance, int color) {
        TextView view = new TextView(activity);
        view.setText(value);
        view.setTextColor(color);
        TextViewCompat.setTextAppearance(view, appearance);
        return view;
    }

    private static void applySelectableBackground(Activity activity, View view) {
        TypedValue selectable = new TypedValue();
        if (activity.getTheme().resolveAttribute(
                android.R.attr.selectableItemBackground, selectable, true)
                && selectable.resourceId != 0) {
            view.setBackgroundResource(selectable.resourceId);
        }
    }

    private static View divider(Activity activity, ThemeManager themeManager) {
        View divider = new View(activity);
        divider.setBackgroundColor(themeManager.dividerColor());
        return divider;
    }

    private static LinearLayout.LayoutParams dividerParams(Activity activity) {
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 1);
        params.setMargins(dp(activity, 16), 0, 0, 0);
        return params;
    }

    private static LinearLayout.LayoutParams matchWidth() {
        return new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
    }

    private static LinearLayout.LayoutParams topMargin(int margin) {
        LinearLayout.LayoutParams params = matchWidth();
        params.setMargins(0, margin, 0, 0);
        return params;
    }

    private static MaterialCardView.LayoutParams cardWidth() {
        return new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT);
    }

    private static int dp(Activity activity, int value) {
        return Math.round(value * activity.getResources().getDisplayMetrics().density);
    }

    interface Handler {
        void onAutoRefreshChanged(boolean enabled);

        void onRefresh();
    }
}

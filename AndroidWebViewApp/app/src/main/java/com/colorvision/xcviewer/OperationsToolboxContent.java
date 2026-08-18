package com.colorvision.xcviewer;

import android.app.Activity;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.HorizontalScrollView;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.view.ViewCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.card.MaterialCardView;
import com.google.android.material.chip.Chip;
import com.google.android.material.chip.ChipGroup;

import java.util.ArrayList;
import java.util.List;

final class OperationsToolboxContent {
    private OperationsToolboxContent() {
    }

    static void addTo(
            Activity activity,
            ThemeManager themeManager,
            LinearLayout target,
            OperationsToolboxPresentation.ViewModel model,
            ActionHandler actionHandler,
            SectionHandler sectionHandler) {
        List<Chip> shortcutChips = new ArrayList<>();
        ChipGroup shortcutGroup = new ChipGroup(activity);
        shortcutGroup.setSingleLine(true);
        shortcutGroup.setChipSpacingHorizontal(dp(activity, 8));
        for (OperationsToolboxPresentation.Section section : model.sections) {
            Chip shortcut = new Chip(activity);
            shortcut.setText(section.shortcutLabel());
            shortcut.setCheckable(false);
            shortcut.setEnsureMinTouchTargetSize(true);
            shortcut.setContentDescription(section.shortcutAccessibilityLabel());
            shortcutGroup.addView(shortcut);
            shortcutChips.add(shortcut);
        }
        HorizontalScrollView shortcuts = new HorizontalScrollView(activity);
        shortcuts.setHorizontalScrollBarEnabled(false);
        shortcuts.setFillViewport(false);
        shortcuts.addView(shortcutGroup, new HorizontalScrollView.LayoutParams(
                HorizontalScrollView.LayoutParams.WRAP_CONTENT,
                HorizontalScrollView.LayoutParams.WRAP_CONTENT));
        LinearLayout.LayoutParams shortcutParams = matchWidth();
        shortcutParams.setMargins(0, dp(activity, 2), 0, dp(activity, 2));
        target.addView(shortcuts, shortcutParams);

        List<TextView> sectionHeadings = new ArrayList<>();
        for (OperationsToolboxPresentation.Section section : model.sections) {
            TextView heading = sectionTitle(activity, themeManager, section.title);
            sectionHeadings.add(heading);
            target.addView(heading, matchWidth());
            target.addView(sectionCard(
                    activity, themeManager, section, actionHandler), cardParams(activity));
        }
        for (int index = 0; index < shortcutChips.size(); index++) {
            TextView heading = sectionHeadings.get(index);
            shortcutChips.get(index).setOnClickListener(
                    view -> sectionHandler.onSection(heading));
        }
    }

    private static TextView sectionTitle(
            Activity activity, ThemeManager themeManager, String value) {
        TextView view = text(activity, value,
                com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                themeManager.primaryTextColor());
        view.setPadding(dp(activity, 4), dp(activity, 12), 0, dp(activity, 8));
        ViewCompat.setAccessibilityHeading(view, true);
        return view;
    }

    private static MaterialCardView sectionCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsToolboxPresentation.Section section,
            ActionHandler actionHandler) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < section.actions.size(); index++) {
            OperationsToolboxPresentation.Action action = section.actions.get(index);
            rows.addView(actionRow(activity, themeManager, action,
                    view -> actionHandler.onAction(action.actionId)), matchWidth());
            if (index < section.actions.size() - 1) {
                rows.addView(divider(activity, themeManager), dividerParams(activity));
            }
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(rows, new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT));
        return card;
    }

    private static LinearLayout actionRow(
            Activity activity,
            ThemeManager themeManager,
            OperationsToolboxPresentation.Action action,
            View.OnClickListener listener) {
        LinearLayout row = new LinearLayout(activity);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setMinimumHeight(dp(activity, 72));
        row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 12), dp(activity, 10));
        row.setClickable(action.enabled);
        row.setFocusable(true);
        row.setEnabled(action.enabled);
        row.setAlpha(action.enabled ? 1f : 0.56f);
        if (action.enabled) {
            row.setOnClickListener(listener);
        }
        TypedValue selectableBackground = new TypedValue();
        if (activity.getTheme().resolveAttribute(
                android.R.attr.selectableItemBackground, selectableBackground, true)) {
            row.setBackgroundResource(selectableBackground.resourceId);
        }

        LinearLayout labels = new LinearLayout(activity);
        labels.setOrientation(LinearLayout.VERTICAL);
        TextView title = text(activity, action.title,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                themeManager.primaryTextColor());
        title.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        labels.addView(title, matchWidth());
        TextView summary = text(activity, action.summary,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.secondaryTextColor());
        summary.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        LinearLayout.LayoutParams summaryParams = matchWidth();
        summaryParams.setMargins(0, dp(activity, 2), 0, 0);
        labels.addView(summary, summaryParams);
        row.addView(labels, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        ImageView arrow = new ImageView(activity);
        arrow.setImageResource(R.drawable.ic_chevron_right_24);
        arrow.setColorFilter(themeManager.secondaryTextColor());
        arrow.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        row.addView(arrow, new LinearLayout.LayoutParams(
                dp(activity, 24), dp(activity, 24)));
        row.setContentDescription(action.accessibilityLabel());
        return row;
    }

    private static TextView text(Activity activity, String value, int appearance, int color) {
        TextView view = new TextView(activity);
        view.setText(value);
        view.setTextColor(color);
        TextViewCompat.setTextAppearance(view, appearance);
        return view;
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

    private static LinearLayout.LayoutParams cardParams(Activity activity) {
        LinearLayout.LayoutParams params = matchWidth();
        params.setMargins(0, 0, 0, dp(activity, 8));
        return params;
    }

    private static int dp(Activity activity, int value) {
        return Math.round(value * activity.getResources().getDisplayMetrics().density);
    }

    interface ActionHandler {
        void onAction(String actionId);
    }

    interface SectionHandler {
        void onSection(View sectionHeading);
    }
}

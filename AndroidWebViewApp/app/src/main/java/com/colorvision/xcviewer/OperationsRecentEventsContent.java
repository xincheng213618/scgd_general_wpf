package com.colorvision.xcviewer;

import android.app.Activity;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.view.ViewCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.card.MaterialCardView;

import java.util.List;

final class OperationsRecentEventsContent {
    private OperationsRecentEventsContent() {
    }

    static View create(
            Activity activity,
            ThemeManager themeManager,
            OperationsRecentEventsPresentation.ViewModel model,
            ActionHandler actionHandler) {
        LinearLayout root = new LinearLayout(activity);
        root.setOrientation(LinearLayout.VERTICAL);

        root.addView(summaryCard(activity, themeManager, model), matchWidth());
        root.addView(sectionTitle(
                activity, themeManager, model.eventsSectionLabel()),
                topMargin(dp(activity, 20)));
        root.addView(model.events.isEmpty()
                        ? infoCard(
                                activity,
                                themeManager,
                                model.available
                                        ? "当前有界日志样本中没有警告、错误或严重事件。"
                                        : "恢复安全直连后可重新读取近期脱敏事件。")
                        : eventListCard(activity, themeManager, model),
                topMargin(dp(activity, 8)));
        if (!model.recommendedActions.isEmpty()) {
            root.addView(sectionTitle(activity, themeManager, "建议操作"),
                    topMargin(dp(activity, 20)));
            root.addView(actionCard(
                            activity,
                            themeManager,
                            model.recommendedActions,
                            actionHandler),
                    topMargin(dp(activity, 8)));
        }
        root.addView(sectionTitle(activity, themeManager, "数据边界"),
                topMargin(dp(activity, 20)));
        root.addView(infoCard(activity, themeManager, model.privacyNotice),
                topMargin(dp(activity, 8)));
        return root;
    }

    private static MaterialCardView summaryCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsRecentEventsPresentation.ViewModel model) {
        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(activity, 16), dp(activity, 14), dp(activity, 16), dp(activity, 14));

        TextView title = text(
                activity,
                "有界日志样本",
                com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                themeManager.primaryTextColor());
        ViewCompat.setAccessibilityHeading(title, true);
        content.addView(title, matchWidth());
        content.addView(text(
                        activity,
                        model.sampleSummary,
                        com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                        themeManager.primaryTextColor()),
                topMargin(dp(activity, 6)));
        if (!model.severitySummary.isEmpty()) {
            content.addView(text(
                            activity,
                            model.severitySummary,
                            com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                            severityColor(themeManager, model)),
                    topMargin(dp(activity, 4)));
        }
        if (!model.categorySummary.isEmpty()) {
            content.addView(text(
                            activity,
                            "来源 · " + model.categorySummary,
                            com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                            themeManager.secondaryTextColor()),
                    topMargin(dp(activity, 8)));
        }
        if (!model.rangeSummary.isEmpty()) {
            content.addView(text(
                            activity,
                            "范围 · " + model.rangeSummary,
                            com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                            themeManager.secondaryTextColor()),
                    topMargin(dp(activity, 4)));
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(content, matchCardWidth());
        return card;
    }

    private static MaterialCardView actionCard(
            Activity activity,
            ThemeManager themeManager,
            List<OperationsRecentEventsPresentation.Action> actions,
            ActionHandler actionHandler) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < actions.size(); index++) {
            OperationsRecentEventsPresentation.Action action = actions.get(index);
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            row.setMinimumHeight(dp(activity, 72));
            row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 12), dp(activity, 10));
            TypedValue selectableBackground = new TypedValue();
            if (activity.getTheme().resolveAttribute(
                    android.R.attr.selectableItemBackground,
                    selectableBackground,
                    true)) {
                row.setBackgroundResource(selectableBackground.resourceId);
            }

            LinearLayout labels = new LinearLayout(activity);
            labels.setOrientation(LinearLayout.VERTICAL);
            TextView title = text(
                    activity,
                    action.title,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                    themeManager.primaryTextColor());
            TextView summary = text(
                    activity,
                    action.summary,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                    themeManager.secondaryTextColor());
            labels.addView(title, matchWidth());
            labels.addView(summary, topMargin(dp(activity, 2)));
            row.addView(labels, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

            ImageView arrow = new ImageView(activity);
            arrow.setImageResource(R.drawable.ic_chevron_right_24);
            arrow.setColorFilter(themeManager.secondaryTextColor());
            arrow.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            row.addView(arrow, new LinearLayout.LayoutParams(
                    dp(activity, 24), dp(activity, 24)));

            row.setContentDescription(action.accessibilityLabel());
            row.setFocusable(true);
            row.setOnClickListener(view -> actionHandler.onAction(action.actionId));
            title.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            summary.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            rows.addView(row, matchWidth());
            if (index < actions.size() - 1) {
                rows.addView(divider(activity, themeManager), dividerParams(activity));
            }
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(rows, matchCardWidth());
        return card;
    }

    private static MaterialCardView eventListCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsRecentEventsPresentation.ViewModel model) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < model.events.size(); index++) {
            OperationsRecentEventsPresentation.Event event = model.events.get(index);
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.VERTICAL);
            row.setMinimumHeight(dp(activity, 64));
            row.setPadding(dp(activity, 16), dp(activity, 12), dp(activity, 16), dp(activity, 12));

            TextView metadata = text(
                    activity,
                    event.metadataLabel(),
                    com.google.android.material.R.style.TextAppearance_Material3_LabelLarge,
                    eventColor(themeManager, event.tone));
            TextView summary = text(
                    activity,
                    event.summary,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                    themeManager.primaryTextColor());
            summary.setLineSpacing(0, 1.06f);
            row.addView(metadata, matchWidth());
            row.addView(summary, topMargin(dp(activity, 5)));
            row.setContentDescription(event.accessibilityLabel());
            row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
            metadata.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            summary.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            rows.addView(row, matchWidth());
            if (index < model.events.size() - 1) {
                rows.addView(divider(activity, themeManager), dividerParams(activity));
            }
        }
        if (model.hiddenEventCount > 0) {
            TextView hidden = text(
                    activity,
                    "另有 " + model.hiddenEventCount + " 条，仅显示最近 12 条。",
                    com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                    themeManager.secondaryTextColor());
            hidden.setPadding(dp(activity, 16), dp(activity, 10),
                    dp(activity, 16), dp(activity, 12));
            rows.addView(hidden, matchWidth());
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(rows, matchCardWidth());
        return card;
    }

    private static MaterialCardView infoCard(
            Activity activity, ThemeManager themeManager, String value) {
        TextView body = text(
                activity,
                value,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.secondaryTextColor());
        body.setLineSpacing(0, 1.06f);
        body.setPadding(dp(activity, 16), dp(activity, 14), dp(activity, 16), dp(activity, 14));
        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(body, matchCardWidth());
        return card;
    }

    private static TextView sectionTitle(
            Activity activity, ThemeManager themeManager, String value) {
        TextView title = text(
                activity,
                value,
                com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                themeManager.primaryTextColor());
        ViewCompat.setAccessibilityHeading(title, true);
        return title;
    }

    private static TextView text(Activity activity, String value, int appearance, int color) {
        TextView view = new TextView(activity);
        view.setText(value);
        view.setTextColor(color);
        TextViewCompat.setTextAppearance(view, appearance);
        return view;
    }

    private static int severityColor(
            ThemeManager themeManager,
            OperationsRecentEventsPresentation.ViewModel model) {
        if (model.tone == OperationsRecentEventsPresentation.TONE_ERROR) {
            return themeManager.errorColor();
        }
        return model.tone == OperationsRecentEventsPresentation.TONE_ATTENTION
                ? themeManager.primaryColor()
                : themeManager.secondaryTextColor();
    }

    private static int eventColor(ThemeManager themeManager, int tone) {
        return tone == OperationsRecentEventsPresentation.TONE_ERROR
                ? themeManager.errorColor()
                : tone == OperationsRecentEventsPresentation.TONE_ATTENTION
                        ? themeManager.primaryColor()
                        : themeManager.secondaryTextColor();
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

    private static MaterialCardView.LayoutParams matchCardWidth() {
        return new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT);
    }

    private static int dp(Activity activity, int value) {
        return Math.round(value * activity.getResources().getDisplayMetrics().density);
    }

    interface ActionHandler {
        void onAction(String actionId);
    }
}

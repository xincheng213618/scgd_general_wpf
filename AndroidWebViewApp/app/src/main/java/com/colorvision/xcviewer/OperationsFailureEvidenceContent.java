package com.colorvision.xcviewer;

import android.app.Activity;
import android.view.Gravity;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.view.ViewCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.card.MaterialCardView;

final class OperationsFailureEvidenceContent {
    private OperationsFailureEvidenceContent() {
    }

    static View create(
            Activity activity,
            ThemeManager themeManager,
            OperationsFailureEvidencePresentation.ViewModel model) {
        LinearLayout root = new LinearLayout(activity);
        root.setOrientation(LinearLayout.VERTICAL);
        root.addView(summaryCard(activity, themeManager, model), matchWidth());
        root.addView(sectionTitle(activity, themeManager, "线索分类"),
                topMargin(dp(activity, 20)));
        root.addView(categoryListCard(activity, themeManager, model),
                topMargin(dp(activity, 8)));
        root.addView(sectionTitle(activity, themeManager, "扫描范围"),
                topMargin(dp(activity, 20)));
        root.addView(sourceListCard(activity, themeManager, model),
                topMargin(dp(activity, 8)));
        root.addView(sectionTitle(activity, themeManager, "计数说明"),
                topMargin(dp(activity, 20)));
        root.addView(infoCard(
                        activity,
                        themeManager,
                        OperationsFailureEvidencePresentation.COUNT_NOTICE),
                topMargin(dp(activity, 8)));
        root.addView(sectionTitle(activity, themeManager, "数据边界"),
                topMargin(dp(activity, 20)));
        root.addView(infoCard(
                        activity,
                        themeManager,
                        OperationsFailureEvidencePresentation.PRIVACY_NOTICE),
                topMargin(dp(activity, 8)));
        return root;
    }

    private static MaterialCardView summaryCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsFailureEvidencePresentation.ViewModel model) {
        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(activity, 16), dp(activity, 14), dp(activity, 16), dp(activity, 14));

        TextView title = text(
                activity,
                "线索概览",
                com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                themeManager.primaryTextColor());
        ViewCompat.setAccessibilityHeading(title, true);
        content.addView(title, matchWidth());
        content.addView(text(
                        activity,
                        model.summaryLabel,
                        com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                        toneColor(themeManager, model.tone)),
                topMargin(dp(activity, 6)));
        content.addView(text(
                        activity,
                        model.countSummary,
                        com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                        themeManager.secondaryTextColor()),
                topMargin(dp(activity, 4)));
        if (!model.latestLabel.isEmpty()) {
            content.addView(text(
                            activity,
                            model.latestLabel,
                            com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                            themeManager.secondaryTextColor()),
                    topMargin(dp(activity, 6)));
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(content, matchCardWidth());
        return card;
    }

    private static MaterialCardView categoryListCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsFailureEvidencePresentation.ViewModel model) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < model.categories.size(); index++) {
            OperationsFailureEvidencePresentation.Category category =
                    model.categories.get(index);
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            row.setMinimumHeight(dp(activity, 56));
            row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 16), dp(activity, 10));

            TextView label = text(
                    activity,
                    category.label,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                    themeManager.primaryTextColor());
            TextView count = text(
                    activity,
                    category.countLabel(),
                    com.google.android.material.R.style.TextAppearance_Material3_LabelLarge,
                    category.count > 0
                            ? themeManager.errorColor()
                            : themeManager.secondaryTextColor());
            count.setGravity(Gravity.END);
            row.addView(label, weightedWidth());
            row.addView(count, wrapWidth());
            row.setContentDescription(category.accessibilityLabel());
            row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
            label.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            count.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            rows.addView(row, matchWidth());
            if (index < model.categories.size() - 1) {
                rows.addView(divider(activity, themeManager), dividerParams(activity));
            }
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(rows, matchCardWidth());
        return card;
    }

    private static MaterialCardView sourceListCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsFailureEvidencePresentation.ViewModel model) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < model.sources.size(); index++) {
            OperationsFailureEvidencePresentation.Source source = model.sources.get(index);
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.VERTICAL);
            row.setMinimumHeight(dp(activity, 72));
            row.setPadding(dp(activity, 16), dp(activity, 12), dp(activity, 16), dp(activity, 12));

            TextView title = text(
                    activity,
                    source.title,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                    themeManager.primaryTextColor());
            TextView status = text(
                    activity,
                    source.statusLabel,
                    com.google.android.material.R.style.TextAppearance_Material3_LabelLarge,
                    toneColor(themeManager, source.tone));
            TextView supporting = text(
                    activity,
                    source.supportingLabel,
                    com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                    themeManager.secondaryTextColor());
            row.addView(title, matchWidth());
            row.addView(status, topMargin(dp(activity, 4)));
            row.addView(supporting, topMargin(dp(activity, 4)));
            row.setContentDescription(source.accessibilityLabel());
            row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
            title.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            status.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            supporting.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            rows.addView(row, matchWidth());
            if (index < model.sources.size() - 1) {
                rows.addView(divider(activity, themeManager), dividerParams(activity));
            }
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

    private static int toneColor(ThemeManager themeManager, int tone) {
        if (tone == OperationsFailureEvidencePresentation.TONE_ERROR) {
            return themeManager.errorColor();
        }
        return tone == OperationsFailureEvidencePresentation.TONE_ATTENTION
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

    private static LinearLayout.LayoutParams weightedWidth() {
        return new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f);
    }

    private static LinearLayout.LayoutParams wrapWidth() {
        return new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
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
}

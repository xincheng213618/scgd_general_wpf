package com.colorvision.xcviewer;

import android.app.Activity;
import android.view.Gravity;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.widget.TextViewCompat;

import com.google.android.material.card.MaterialCardView;

final class OperationsLiveMonitorContent {
    private OperationsLiveMonitorContent() {
    }

    static View create(
            Activity activity,
            ThemeManager themeManager,
            OperationsLiveMonitorPresentation.ViewModel model) {
        LinearLayout root = new LinearLayout(activity);
        root.setOrientation(LinearLayout.VERTICAL);
        root.addView(statusCard(activity, themeManager, model), matchWidth());

        TextView trendHeading = text(
                activity,
                "本次趋势",
                com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                themeManager.primaryTextColor());
        trendHeading.setPadding(dp(activity, 4), dp(activity, 12), 0, dp(activity, 8));
        root.addView(trendHeading, matchWidth());

        TextView trend = text(
                activity,
                model.trendSummary,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                toneColor(themeManager, model.trendTone));
        trend.setPadding(dp(activity, 16), dp(activity, 14), dp(activity, 16), dp(activity, 14));
        trend.setLineSpacing(0, 1.08f);
        trend.setContentDescription("本次趋势，" + model.trendSummary.replace("\n", "，"));
        MaterialCardView trendCard = new MaterialCardView(activity);
        trendCard.setCardBackgroundColor(themeManager.cardBackgroundColor());
        trendCard.addView(trend, cardWidth());
        root.addView(trendCard, matchWidth());

        TextView privacy = text(
                activity,
                model.privacyNote,
                com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                themeManager.secondaryTextColor());
        privacy.setPadding(dp(activity, 4), dp(activity, 12), dp(activity, 4), dp(activity, 4));
        root.addView(privacy, matchWidth());
        return root;
    }

    private static MaterialCardView statusCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsLiveMonitorPresentation.ViewModel model) {
        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);

        TextView caption = text(
                activity,
                model.statusCaption,
                com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                themeManager.secondaryTextColor());
        caption.setPadding(dp(activity, 16), dp(activity, 12), dp(activity, 16), dp(activity, 10));
        content.addView(caption, matchWidth());

        for (int index = 0; index < model.statuses.size(); index++) {
            OperationsDashboardStatusFormatter.Item item = model.statuses.get(index);
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.VERTICAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            row.setMinimumHeight(dp(activity, 58));
            row.setPadding(dp(activity, 16), dp(activity, 8), dp(activity, 16), dp(activity, 8));
            row.setFocusable(true);
            row.setContentDescription(item.title + "，" + item.summary);

            TextView title = text(
                    activity,
                    item.title,
                    com.google.android.material.R.style.TextAppearance_Material3_LabelMedium,
                    themeManager.secondaryTextColor());
            title.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            row.addView(title, matchWidth());

            TextView summary = text(
                    activity,
                    item.summary,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                    toneColor(themeManager, item.tone));
            summary.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            row.addView(summary, matchWidth());
            content.addView(row, matchWidth());

            if (index < model.statuses.size() - 1) {
                View divider = new View(activity);
                divider.setBackgroundColor(themeManager.dividerColor());
                LinearLayout.LayoutParams dividerParams = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MATCH_PARENT, 1);
                dividerParams.setMargins(dp(activity, 16), 0, 0, 0);
                content.addView(divider, dividerParams);
            }
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(content, cardWidth());
        return card;
    }

    private static int toneColor(ThemeManager themeManager, int tone) {
        if (tone == OperationsDashboardStatusFormatter.TONE_ACTIVE) {
            return themeManager.primaryColor();
        }
        if (tone == OperationsDashboardStatusFormatter.TONE_ATTENTION) {
            return themeManager.errorColor();
        }
        if (tone == OperationsDashboardStatusFormatter.TONE_MUTED) {
            return themeManager.secondaryTextColor();
        }
        return themeManager.primaryTextColor();
    }

    private static TextView text(Activity activity, String value, int appearance, int color) {
        TextView view = new TextView(activity);
        view.setText(value);
        view.setTextColor(color);
        TextViewCompat.setTextAppearance(view, appearance);
        return view;
    }

    private static LinearLayout.LayoutParams matchWidth() {
        return new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
    }

    private static MaterialCardView.LayoutParams cardWidth() {
        return new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT);
    }

    private static int dp(Activity activity, int value) {
        return Math.round(value * activity.getResources().getDisplayMetrics().density);
    }
}

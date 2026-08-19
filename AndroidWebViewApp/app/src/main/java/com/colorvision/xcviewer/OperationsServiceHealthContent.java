package com.colorvision.xcviewer;

import android.app.Activity;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.view.ViewCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.card.MaterialCardView;

final class OperationsServiceHealthContent {
    private OperationsServiceHealthContent() {
    }

    static View create(
            Activity activity,
            ThemeManager themeManager,
            OperationsServiceHealthPresentation.ViewModel model) {
        LinearLayout root = new LinearLayout(activity);
        root.setOrientation(LinearLayout.VERTICAL);
        root.addView(summaryCard(activity, themeManager, model), matchWidth());
        root.addView(sectionTitle(
                        activity, themeManager, model.servicesSectionLabel()),
                topMargin(dp(activity, 20)));
        root.addView(model.services.isEmpty()
                        ? infoCard(activity, themeManager,
                                model.available
                                        ? "当前没有适用的本机白名单服务。"
                                        : "恢复安全直连后可重新读取固定服务状态。")
                        : serviceListCard(activity, themeManager, model),
                topMargin(dp(activity, 8)));
        root.addView(sectionTitle(activity, themeManager, "数据边界"),
                topMargin(dp(activity, 20)));
        root.addView(infoCard(activity, themeManager, model.privacyNotice),
                topMargin(dp(activity, 8)));
        return root;
    }

    private static MaterialCardView summaryCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsServiceHealthPresentation.ViewModel model) {
        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(activity, 16), dp(activity, 14), dp(activity, 16), dp(activity, 14));

        TextView title = text(
                activity,
                "运行概览",
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
                        model.countLabel,
                        com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                        themeManager.secondaryTextColor()),
                topMargin(dp(activity, 4)));

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(content, matchCardWidth());
        return card;
    }

    private static MaterialCardView serviceListCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsServiceHealthPresentation.ViewModel model) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < model.services.size(); index++) {
            OperationsServiceHealthPresentation.Service service = model.services.get(index);
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.VERTICAL);
            row.setMinimumHeight(dp(activity, 88));
            row.setPadding(dp(activity, 16), dp(activity, 12), dp(activity, 16), dp(activity, 12));

            TextView title = text(
                    activity,
                    service.title,
                    com.google.android.material.R.style.TextAppearance_Material3_TitleSmall,
                    themeManager.primaryTextColor());
            TextView status = text(
                    activity,
                    service.statusSummary(),
                    com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                    toneColor(themeManager, service.tone));
            TextView observation = text(
                    activity,
                    service.observationSummary(),
                    com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                    themeManager.secondaryTextColor());
            TextView maintenance = text(
                    activity,
                    service.maintenanceLabel,
                    com.google.android.material.R.style.TextAppearance_Material3_LabelLarge,
                    service.maintenanceSupported
                            ? themeManager.primaryColor()
                            : themeManager.secondaryTextColor());
            row.addView(title, matchWidth());
            row.addView(status, topMargin(dp(activity, 4)));
            row.addView(observation, topMargin(dp(activity, 5)));
            row.addView(maintenance, topMargin(dp(activity, 8)));
            row.setContentDescription(service.accessibilityLabel());
            row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
            title.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            status.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            observation.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            maintenance.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            rows.addView(row, matchWidth());
            if (index < model.services.size() - 1) {
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
        if (tone == OperationsServiceHealthPresentation.TONE_ATTENTION) {
            return themeManager.errorColor();
        }
        return tone == OperationsServiceHealthPresentation.TONE_HEALTHY
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
}

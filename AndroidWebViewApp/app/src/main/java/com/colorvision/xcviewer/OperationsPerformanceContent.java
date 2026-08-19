package com.colorvision.xcviewer;

import android.app.Activity;
import android.view.Gravity;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.view.ViewCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.button.MaterialButton;
import com.google.android.material.card.MaterialCardView;

import java.util.List;

final class OperationsPerformanceContent {
    private OperationsPerformanceContent() {
    }

    static View create(
            Activity activity,
            ThemeManager themeManager,
            OperationsPerformancePresentation.ViewModel model,
            Runnable openLiveMonitor) {
        LinearLayout root = new LinearLayout(activity);
        root.setOrientation(LinearLayout.VERTICAL);
        root.addView(summaryCard(activity, themeManager, model), matchWidth());
        root.addView(sectionTitle(activity, themeManager, "内存"),
                topMargin(dp(activity, 20)));
        root.addView(metricListCard(activity, themeManager, model.memoryMetrics),
                topMargin(dp(activity, 8)));
        root.addView(sectionTitle(activity, themeManager, "进程与托管运行时"),
                topMargin(dp(activity, 20)));
        root.addView(metricListCard(activity, themeManager, model.resourceMetrics),
                topMargin(dp(activity, 8)));
        if (openLiveMonitor != null) {
            root.addView(sectionTitle(activity, themeManager, "进一步分析"),
                    topMargin(dp(activity, 20)));
            root.addView(trendCard(activity, themeManager, openLiveMonitor),
                    topMargin(dp(activity, 8)));
        }
        root.addView(sectionTitle(activity, themeManager, "数据边界"),
                topMargin(dp(activity, 20)));
        String boundary = model.integrityNotice.isEmpty()
                ? OperationsPerformancePresentation.BOUNDARY_NOTICE
                : OperationsPerformancePresentation.BOUNDARY_NOTICE
                        + "\n\n" + model.integrityNotice;
        root.addView(infoCard(activity, themeManager, boundary),
                topMargin(dp(activity, 8)));
        return root;
    }

    private static MaterialCardView summaryCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsPerformancePresentation.ViewModel model) {
        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(activity, 16), dp(activity, 14), dp(activity, 16), dp(activity, 14));

        TextView title = text(
                activity,
                model.sourceLabel,
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
                        model.sampleLabel,
                        com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                        themeManager.secondaryTextColor()),
                topMargin(dp(activity, 4)));
        if (!model.capturedLabel.isEmpty()) {
            content.addView(text(
                            activity,
                            model.capturedLabel,
                            com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                            themeManager.secondaryTextColor()),
                    topMargin(dp(activity, 6)));
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(content, matchCardWidth());
        return card;
    }

    private static MaterialCardView metricListCard(
            Activity activity,
            ThemeManager themeManager,
            List<OperationsPerformancePresentation.Metric> metrics) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < metrics.size(); index++) {
            OperationsPerformancePresentation.Metric metric = metrics.get(index);
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            row.setMinimumHeight(dp(activity, 56));
            row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 16), dp(activity, 10));
            TextView label = text(
                    activity,
                    metric.label,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                    themeManager.primaryTextColor());
            TextView value = text(
                    activity,
                    metric.value,
                    com.google.android.material.R.style.TextAppearance_Material3_LabelLarge,
                    themeManager.secondaryTextColor());
            value.setGravity(Gravity.END);
            row.addView(label, weightedWidth());
            row.addView(value, wrapWidth());
            row.setContentDescription(metric.accessibilityLabel());
            row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
            label.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            value.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            rows.addView(row, matchWidth());
            if (index < metrics.size() - 1) {
                rows.addView(divider(activity, themeManager), dividerParams(activity));
            }
        }
        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(rows, matchCardWidth());
        return card;
    }

    private static MaterialCardView trendCard(
            Activity activity,
            ThemeManager themeManager,
            Runnable openLiveMonitor) {
        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(activity, 16), dp(activity, 14), dp(activity, 16), dp(activity, 14));
        content.addView(text(
                        activity,
                        "前台每 10 秒更新并在内存中保留本次最多 30 个脱敏样本；离开观察页即清空。",
                        com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                        themeManager.secondaryTextColor()),
                matchWidth());
        MaterialButton button = new MaterialButton(activity);
        button.setText("在问题中心持续观察");
        button.setIconResource(R.drawable.ic_visibility_24);
        button.setIconGravity(MaterialButton.ICON_GRAVITY_TEXT_START);
        button.setOnClickListener(view -> openLiveMonitor.run());
        content.addView(button, topMargin(dp(activity, 12)));

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(content, matchCardWidth());
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
        if (tone == OperationsPerformancePresentation.TONE_ERROR) {
            return themeManager.errorColor();
        }
        return themeManager.primaryColor();
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

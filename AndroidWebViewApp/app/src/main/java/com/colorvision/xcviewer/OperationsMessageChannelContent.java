package com.colorvision.xcviewer;

import android.app.Activity;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.view.ViewCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.button.MaterialButton;
import com.google.android.material.card.MaterialCardView;

final class OperationsMessageChannelContent {
    private OperationsMessageChannelContent() {
    }

    static View create(
            Activity activity,
            ThemeManager themeManager,
            OperationsMessageChannelPresentation.ViewModel model,
            boolean recoveryInFlight,
            Runnable recover) {
        LinearLayout root = new LinearLayout(activity);
        root.setOrientation(LinearLayout.VERTICAL);
        root.addView(summaryCard(activity, themeManager, model), matchWidth());

        root.addView(sectionTitle(activity, themeManager, "连接与订阅"),
                topMargin(dp(activity, 20)));
        root.addView(statusCard(activity, themeManager, model),
                topMargin(dp(activity, 8)));

        root.addView(sectionTitle(activity, themeManager, "聚合活动"),
                topMargin(dp(activity, 20)));
        root.addView(model.activityItems.isEmpty()
                        ? infoCard(activity, themeManager, "当前没有可显示的聚合活动时间。")
                        : activityCard(activity, themeManager, model),
                topMargin(dp(activity, 8)));

        root.addView(sectionTitle(activity, themeManager, "安全恢复"),
                topMargin(dp(activity, 20)));
        root.addView(recoveryCard(
                        activity, themeManager, model, recoveryInFlight, recover),
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
            OperationsMessageChannelPresentation.ViewModel model) {
        LinearLayout content = verticalContent(activity);
        TextView heading = text(
                activity,
                "运行概览",
                com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                themeManager.primaryTextColor());
        ViewCompat.setAccessibilityHeading(heading, true);
        content.addView(heading, matchWidth());
        content.addView(text(
                        activity,
                        model.stateLabel,
                        com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                        toneColor(themeManager, model.tone)),
                topMargin(dp(activity, 6)));
        content.addView(text(
                        activity,
                        model.summary,
                        com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                        themeManager.secondaryTextColor()),
                topMargin(dp(activity, 4)));
        return card(activity, themeManager, content);
    }

    private static MaterialCardView statusCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsMessageChannelPresentation.ViewModel model) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        rows.addView(statusRow(
                activity, themeManager, "连接", model.connectionLabel, model.tone),
                matchWidth());
        rows.addView(divider(activity, themeManager), dividerParams(activity));
        rows.addView(statusRow(
                activity, themeManager, "已登记订阅", model.subscriptionLabel, model.tone),
                matchWidth());
        return card(activity, themeManager, rows);
    }

    private static View statusRow(
            Activity activity,
            ThemeManager themeManager,
            String label,
            String value,
            int tone) {
        LinearLayout row = new LinearLayout(activity);
        row.setOrientation(LinearLayout.VERTICAL);
        row.setMinimumHeight(dp(activity, 72));
        row.setPadding(dp(activity, 16), dp(activity, 12), dp(activity, 16), dp(activity, 12));
        TextView title = text(
                activity,
                label,
                com.google.android.material.R.style.TextAppearance_Material3_LabelLarge,
                themeManager.secondaryTextColor());
        TextView status = text(
                activity,
                value,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                toneColor(themeManager, tone));
        row.addView(title, matchWidth());
        row.addView(status, topMargin(dp(activity, 3)));
        row.setContentDescription(label + "，" + value.replace(" · ", "，"));
        row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
        title.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        status.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        return row;
    }

    private static MaterialCardView activityCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsMessageChannelPresentation.ViewModel model) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < model.activityItems.size(); index++) {
            OperationsMessageChannelPresentation.ActivityItem item =
                    model.activityItems.get(index);
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setMinimumHeight(dp(activity, 56));
            row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 16), dp(activity, 10));
            TextView label = text(
                    activity,
                    item.label,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                    themeManager.primaryTextColor());
            TextView value = text(
                    activity,
                    item.value,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                    themeManager.secondaryTextColor());
            value.setTextAlignment(View.TEXT_ALIGNMENT_VIEW_END);
            row.addView(label, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
            row.addView(value, new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.WRAP_CONTENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT));
            row.setContentDescription(item.accessibilityLabel());
            row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
            label.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            value.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            rows.addView(row, matchWidth());
            if (index < model.activityItems.size() - 1) {
                rows.addView(divider(activity, themeManager), dividerParams(activity));
            }
        }
        return card(activity, themeManager, rows);
    }

    private static MaterialCardView recoveryCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsMessageChannelPresentation.ViewModel model,
            boolean recoveryInFlight,
            Runnable recover) {
        LinearLayout content = verticalContent(activity);
        TextView summary = text(
                activity,
                model.recoverySummary,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.secondaryTextColor());
        summary.setLineSpacing(0, 1.06f);
        content.addView(summary, matchWidth());
        if (model.canRecover || recoveryInFlight) {
            MaterialButton button = new MaterialButton(activity);
            button.setText(recoveryInFlight ? "正在恢复消息通道…" : "恢复消息通道");
            button.setMinHeight(dp(activity, 48));
            button.setEnabled(model.canRecover && !recoveryInFlight);
            button.setContentDescription(recoveryInFlight
                    ? "正在恢复消息通道，完成前不能重复提交"
                    : "恢复消息通道，需要确认；只使用电脑当前配置重建连接与已登记订阅");
            button.setOnClickListener(view -> recover.run());
            content.addView(button, topMargin(dp(activity, 12)));
        }
        return card(activity, themeManager, content);
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

    private static LinearLayout verticalContent(Activity activity) {
        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(activity, 16), dp(activity, 14), dp(activity, 16), dp(activity, 14));
        return content;
    }

    private static MaterialCardView card(
            Activity activity, ThemeManager themeManager, View content) {
        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(content, matchCardWidth());
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
        if (tone == OperationsMessageChannelPresentation.TONE_ATTENTION) {
            return themeManager.errorColor();
        }
        return tone == OperationsMessageChannelPresentation.TONE_HEALTHY
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

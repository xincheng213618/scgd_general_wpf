package com.colorvision.xcviewer;

import android.app.Activity;
import android.view.Gravity;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.widget.TextViewCompat;

import com.google.android.material.button.MaterialButton;
import com.google.android.material.card.MaterialCardView;

import java.util.HashSet;
import java.util.Set;

final class OperationsTriageContent {
    private OperationsTriageContent() {
    }

    static View create(
            Activity activity,
            ThemeManager themeManager,
            OperationsTriagePresentation.ViewModel model,
            ActionHandler actionHandler,
            Runnable refresh,
            Runnable back) {
        LinearLayout root = new LinearLayout(activity);
        root.setOrientation(LinearLayout.VERTICAL);

        root.addView(sectionTitle(activity, themeManager, "运行概览"), matchWidth());
        root.addView(metricsCard(activity, themeManager, model), topMargin(dp(activity, 8)));

        String findingsTitle = model.findings.isEmpty()
                ? "发现项" : "发现项 · " + model.findings.size();
        root.addView(sectionTitle(activity, themeManager, findingsTitle), topMargin(dp(activity, 20)));
        if (model.findings.isEmpty()) {
            root.addView(emptyCard(activity, themeManager), topMargin(dp(activity, 8)));
        } else {
            Set<String> renderedActions = new HashSet<>();
            for (OperationsTriagePresentation.Finding finding : model.findings) {
                root.addView(findingCard(
                        activity,
                        themeManager,
                        finding,
                        renderedActions,
                        actionHandler),
                        topMargin(dp(activity, 8)));
            }
        }

        root.addView(sectionTitle(activity, themeManager, "操作边界"), topMargin(dp(activity, 20)));
        root.addView(safetyCard(activity, themeManager, model.safetyNotice),
                topMargin(dp(activity, 8)));

        boolean singleColumn = OperationsResponsiveLayout.usesSingleColumn(
                activity.getResources().getConfiguration().fontScale);
        LinearLayout navigation = new LinearLayout(activity);
        navigation.setOrientation(singleColumn
                ? LinearLayout.VERTICAL : LinearLayout.HORIZONTAL);
        MaterialButton backButton = new MaterialButton(
                activity, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        backButton.setText("返回运维概览");
        backButton.setMinHeight(dp(activity, 48));
        backButton.setOnClickListener(view -> back.run());
        navigation.addView(backButton, singleColumn ? matchWidth() : weightedButton());

        MaterialButton refreshButton = new MaterialButton(activity);
        refreshButton.setText("刷新排障建议");
        refreshButton.setMinHeight(dp(activity, 48));
        refreshButton.setOnClickListener(view -> refresh.run());
        LinearLayout.LayoutParams refreshParams = singleColumn
                ? topMargin(dp(activity, 8)) : weightedButton();
        if (!singleColumn) {
            refreshParams.setMargins(dp(activity, 8), 0, 0, 0);
        }
        navigation.addView(refreshButton, refreshParams);
        root.addView(navigation, topMargin(dp(activity, 16)));
        return root;
    }

    private static MaterialCardView metricsCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsTriagePresentation.ViewModel model) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < model.metrics.size(); index++) {
            OperationsTriagePresentation.Metric metric = model.metrics.get(index);
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.VERTICAL);
            row.setMinimumHeight(dp(activity, 64));
            row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 16), dp(activity, 10));
            TextView label = text(activity, metric.label,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                    themeManager.primaryTextColor());
            TextView summary = text(activity, metric.summary,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                    metricTextColor(themeManager, metric.tone));
            row.addView(label, matchWidth());
            row.addView(summary, topMargin(dp(activity, 2)));
            row.setContentDescription(metric.accessibilityLabel());
            row.setFocusable(true);
            row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
            label.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            summary.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            rows.addView(row, matchWidth());
            if (index < model.metrics.size() - 1) {
                rows.addView(divider(activity, themeManager), dividerParams(activity));
            }
        }
        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(rows, matchCardWidth());
        return card;
    }

    private static MaterialCardView findingCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsTriagePresentation.Finding finding,
            Set<String> renderedActions,
            ActionHandler actionHandler) {
        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(activity, 16), dp(activity, 16), dp(activity, 16), dp(activity, 16));
        int contentColor = findingContentColor(themeManager, finding.tone());

        TextView evidence = text(activity, finding.evidenceLabel(),
                com.google.android.material.R.style.TextAppearance_Material3_LabelLarge,
                contentColor);
        content.addView(evidence, matchWidth());
        TextView title = text(activity, finding.title,
                com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                contentColor);
        content.addView(title, topMargin(dp(activity, 6)));
        if (!finding.summary.isEmpty()) {
            TextView summary = text(activity, finding.summary,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                    contentColor);
            summary.setLineSpacing(0, 1.06f);
            content.addView(summary, topMargin(dp(activity, 8)));
        }
        if (!finding.latestAt.isEmpty()) {
            TextView latest = text(activity, "最近证据 · " + finding.latestAt,
                    com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                    contentColor);
            content.addView(latest, topMargin(dp(activity, 8)));
        }

        for (OperationsTriagePresentation.Action action : finding.actions) {
            if (!OperationsTriagePresentation.isSupportedAction(action.actionId)
                    || !renderedActions.add(action.actionId)) {
                continue;
            }
            MaterialButton button = actionButton(activity, action);
            button.setText(action.buttonLabel());
            button.setMinHeight(dp(activity, 48));
            button.setContentDescription(action.description.isEmpty()
                    ? action.buttonLabel() : action.buttonLabel() + "。" + action.description);
            button.setOnClickListener(view -> actionHandler.onAction(action.actionId));
            content.addView(button, topMargin(dp(activity, 12)));
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(findingContainerColor(themeManager, finding.tone()));
        card.addView(content, matchCardWidth());
        return card;
    }

    private static MaterialButton actionButton(
            Activity activity,
            OperationsTriagePresentation.Action action) {
        if (action.readOnly()) {
            return new MaterialButton(
                    activity, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        }
        if ("low-risk".equals(action.riskLevel)) {
            return new MaterialButton(
                    activity, null, com.google.android.material.R.attr.materialButtonTonalStyle);
        }
        return new MaterialButton(activity);
    }

    private static MaterialCardView emptyCard(Activity activity, ThemeManager themeManager) {
        TextView message = text(activity, "当前有界证据未发现需要处理的项目。",
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                themeManager.onPrimaryContainerColor());
        message.setPadding(dp(activity, 16), dp(activity, 16), dp(activity, 16), dp(activity, 16));
        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.primaryContainerColor());
        card.addView(message, matchCardWidth());
        return card;
    }

    private static MaterialCardView safetyCard(
            Activity activity,
            ThemeManager themeManager,
            String safetyNotice) {
        TextView notice = text(activity, safetyNotice,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.secondaryTextColor());
        notice.setLineSpacing(0, 1.06f);
        notice.setPadding(dp(activity, 16), dp(activity, 14), dp(activity, 16), dp(activity, 14));
        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(notice, matchCardWidth());
        return card;
    }

    private static TextView sectionTitle(
            Activity activity,
            ThemeManager themeManager,
            String value) {
        return text(activity, value,
                com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                themeManager.primaryTextColor());
    }

    private static TextView text(Activity activity, String value, int appearance, int color) {
        TextView view = new TextView(activity);
        view.setText(value);
        view.setTextColor(color);
        TextViewCompat.setTextAppearance(view, appearance);
        return view;
    }

    private static int metricTextColor(ThemeManager themeManager, int tone) {
        if (tone == OperationsTriagePresentation.TONE_ERROR) {
            return themeManager.errorColor();
        }
        if (tone == OperationsTriagePresentation.TONE_ATTENTION) {
            return themeManager.primaryColor();
        }
        if (tone == OperationsTriagePresentation.TONE_MUTED) {
            return themeManager.secondaryTextColor();
        }
        return themeManager.primaryTextColor();
    }

    private static int findingContainerColor(ThemeManager themeManager, int tone) {
        if (tone == OperationsTriagePresentation.TONE_ERROR) {
            return themeManager.errorContainerColor();
        }
        if (tone == OperationsTriagePresentation.TONE_ATTENTION) {
            return themeManager.tertiaryContainerColor();
        }
        return themeManager.secondaryContainerColor();
    }

    private static int findingContentColor(ThemeManager themeManager, int tone) {
        if (tone == OperationsTriagePresentation.TONE_ERROR) {
            return themeManager.onErrorContainerColor();
        }
        if (tone == OperationsTriagePresentation.TONE_ATTENTION) {
            return themeManager.onTertiaryContainerColor();
        }
        return themeManager.onSecondaryContainerColor();
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

    private static LinearLayout.LayoutParams weightedButton() {
        return new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1);
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

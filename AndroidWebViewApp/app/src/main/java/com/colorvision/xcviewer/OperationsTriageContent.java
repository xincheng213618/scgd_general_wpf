package com.colorvision.xcviewer;

import android.app.Activity;
import android.content.res.ColorStateList;
import android.text.TextUtils;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.view.ViewCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.button.MaterialButton;
import com.google.android.material.card.MaterialCardView;
import com.google.android.material.chip.Chip;
import com.google.android.material.chip.ChipGroup;

import java.util.HashSet;
import java.util.List;
import java.util.Set;

final class OperationsTriageContent {
    private OperationsTriageContent() {
    }

    static View create(
            Activity activity,
            ThemeManager themeManager,
            OperationsTriagePresentation.ViewModel model,
            ActionHandler actionHandler,
            ReviewHandler reviewHandler,
            Runnable connectionCheck,
            Runnable observe) {
        LinearLayout root = new LinearLayout(activity);
        root.setOrientation(LinearLayout.VERTICAL);

        ChipGroup quickActions = new ChipGroup(activity);
        quickActions.setSingleLine(false);
        quickActions.setChipSpacingHorizontal(dp(activity, 8));
        quickActions.setChipSpacingVertical(dp(activity, 4));
        quickActions.addView(actionChip(
                activity,
                themeManager,
                activity.getString(R.string.operations_triage_connection_check_action),
                activity.getString(
                        R.string.operations_triage_connection_check_content_description),
                R.drawable.ic_devices_24,
                connectionCheck));
        quickActions.addView(actionChip(
                activity,
                themeManager,
                activity.getString(R.string.operations_triage_observe_action),
                activity.getString(R.string.operations_triage_observe_content_description),
                R.drawable.ic_visibility_24,
                observe));
        root.addView(quickActions, matchWidth());

        if (!model.pendingFindings.isEmpty()) {
            root.addView(sectionTitle(
                    activity, themeManager, model.prioritySectionLabel()),
                    topMargin(dp(activity, 20)));
            root.addView(findingsContent(
                            activity,
                            themeManager,
                            model.pendingFindings,
                            actionHandler,
                            reviewHandler),
                    topMargin(dp(activity, 8)));
        }
        if (!model.reviewedFindings.isEmpty()) {
            root.addView(sectionTitle(
                    activity, themeManager, model.reviewedSectionLabel()),
                    topMargin(dp(activity, 20)));
            root.addView(findingsContent(
                            activity,
                            themeManager,
                            model.reviewedFindings,
                            actionHandler,
                            reviewHandler),
                    topMargin(dp(activity, 8)));
        }

        root.addView(sectionTitle(activity, themeManager, "运行概览"),
                topMargin(dp(activity, 20)));
        root.addView(metricsCard(activity, themeManager, model, actionHandler),
                topMargin(dp(activity, 8)));

        root.addView(sectionTitle(activity, themeManager, "操作边界"), topMargin(dp(activity, 20)));
        root.addView(safetyCard(activity, themeManager, model.safetyNotice),
                topMargin(dp(activity, 8)));

        return root;
    }

    private static Chip actionChip(
            Activity activity,
            ThemeManager themeManager,
            String label,
            String contentDescription,
            int iconResource,
            Runnable action) {
        Chip chip = new Chip(activity);
        chip.setText(label);
        chip.setCheckable(false);
        chip.setEnsureMinTouchTargetSize(true);
        chip.setChipIconResource(iconResource);
        chip.setChipIconTint(ColorStateList.valueOf(themeManager.primaryTextColor()));
        chip.setChipIconVisible(true);
        chip.setContentDescription(contentDescription);
        chip.setOnClickListener(view -> action.run());
        return chip;
    }

    private static MaterialCardView metricsCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsTriagePresentation.ViewModel model,
            ActionHandler actionHandler) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < model.metrics.size(); index++) {
            OperationsTriagePresentation.Metric metric = model.metrics.get(index);
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            row.setMinimumHeight(dp(activity, 64));
            row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 16), dp(activity, 10));

            LinearLayout copy = new LinearLayout(activity);
            copy.setOrientation(LinearLayout.VERTICAL);
            TextView label = text(activity, metric.label,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                    themeManager.primaryTextColor());
            TextView summary = text(activity, metric.summary,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                    metricTextColor(themeManager, metric.tone));
            copy.addView(label, matchWidth());
            copy.addView(summary, topMargin(dp(activity, 2)));
            row.addView(copy, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

            ImageView chevron = new ImageView(activity);
            chevron.setImageResource(R.drawable.ic_chevron_right_24);
            chevron.setImageTintList(ColorStateList.valueOf(themeManager.secondaryTextColor()));
            chevron.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            LinearLayout.LayoutParams iconParams = new LinearLayout.LayoutParams(
                    dp(activity, 24), dp(activity, 24));
            iconParams.setMargins(dp(activity, 12), 0, 0, 0);
            row.addView(chevron, iconParams);

            row.setContentDescription(metric.accessibilityLabel());
            row.setClickable(true);
            row.setFocusable(true);
            row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
            applySelectableBackground(activity, row);
            row.setOnClickListener(view -> actionHandler.onAction(metric.actionId));
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

    private static void applySelectableBackground(Activity activity, View view) {
        TypedValue selectable = new TypedValue();
        if (activity.getTheme().resolveAttribute(
                android.R.attr.selectableItemBackground, selectable, true)
                && selectable.resourceId != 0) {
            view.setBackgroundResource(selectable.resourceId);
        }
    }

    private static View findingsContent(
            Activity activity,
            ThemeManager themeManager,
            List<OperationsTriagePresentation.Finding> findings,
            ActionHandler actionHandler,
            ReviewHandler reviewHandler) {
        boolean twoColumns = AppResponsiveLayout.usesTwoColumnGrid(
                activity.getResources().getConfiguration().screenWidthDp,
                activity.getResources().getConfiguration().fontScale,
                findings.size());
        return twoColumns
                ? findingsGrid(activity, themeManager, findings, actionHandler, reviewHandler)
                : findingsCard(activity, themeManager, findings, actionHandler, reviewHandler);
    }

    private static MaterialCardView findingsCard(
            Activity activity,
            ThemeManager themeManager,
            List<OperationsTriagePresentation.Finding> findings,
            ActionHandler actionHandler,
            ReviewHandler reviewHandler) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        Set<String> renderedActions = new HashSet<>();
        for (int index = 0; index < findings.size(); index++) {
            rows.addView(findingRow(
                    activity,
                    themeManager,
                    findings.get(index),
                    renderedActions,
                    actionHandler,
                    reviewHandler), matchWidth());
            if (index < findings.size() - 1) {
                rows.addView(divider(activity, themeManager), dividerParams(activity));
            }
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(rows, matchCardWidth());
        return card;
    }

    private static View findingsGrid(
            Activity activity,
            ThemeManager themeManager,
            List<OperationsTriagePresentation.Finding> findings,
            ActionHandler actionHandler,
            ReviewHandler reviewHandler) {
        LinearLayout grid = new LinearLayout(activity);
        grid.setOrientation(LinearLayout.VERTICAL);
        Set<String> renderedActions = new HashSet<>();
        int spacing = dp(activity, 8);
        for (int index = 0; index < findings.size(); index += 2) {
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.HORIZONTAL);
            int itemsInRow = Math.min(2, findings.size() - index);
            for (int column = 0; column < itemsInRow; column++) {
                OperationsTriagePresentation.Finding finding =
                        findings.get(index + column);
                MaterialCardView card = new MaterialCardView(activity);
                card.setCardBackgroundColor(themeManager.cardBackgroundColor());
                card.addView(findingRow(
                        activity,
                        themeManager,
                        finding,
                        renderedActions,
                        actionHandler,
                        reviewHandler),
                        matchCardWidth());
                LinearLayout.LayoutParams cardParams = new LinearLayout.LayoutParams(
                        0, LinearLayout.LayoutParams.MATCH_PARENT, 1);
                if (column > 0) {
                    cardParams.setMargins(spacing, 0, 0, 0);
                }
                row.addView(card, cardParams);
            }
            LinearLayout.LayoutParams rowParams = matchWidth();
            if (index > 0) {
                rowParams.setMargins(0, spacing, 0, 0);
            }
            grid.addView(row, rowParams);
        }
        return grid;
    }

    private static View findingRow(
            Activity activity,
            ThemeManager themeManager,
            OperationsTriagePresentation.Finding finding,
            Set<String> renderedActions,
            ActionHandler actionHandler,
            ReviewHandler reviewHandler) {
        OperationsTriagePresentation.Action primaryAction = finding.primaryCardAction();
        if (primaryAction != null && !renderedActions.add(primaryAction.actionId)) {
            primaryAction = null;
        }

        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);
        int accentColor = findingAccentColor(themeManager, finding.tone());

        LinearLayout summaryContent = new LinearLayout(activity);
        summaryContent.setOrientation(LinearLayout.VERTICAL);

        TextView evidence = text(activity, finding.listMetaLabel(),
                com.google.android.material.R.style.TextAppearance_Material3_LabelMedium,
                accentColor);
        summaryContent.addView(evidence, matchWidth());
        TextView title = text(activity, finding.title,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                themeManager.primaryTextColor());
        summaryContent.addView(title, topMargin(dp(activity, 4)));
        TextView summary = null;
        if (!finding.summary.isEmpty()) {
            summary = text(activity, finding.summary,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                    themeManager.secondaryTextColor());
            summary.setLineSpacing(0, 1.06f);
            summary.setMaxLines(2);
            summary.setEllipsize(TextUtils.TruncateAt.END);
            summaryContent.addView(summary, topMargin(dp(activity, 4)));
        }

        LinearLayout clickableSummary = new LinearLayout(activity);
        clickableSummary.setOrientation(LinearLayout.HORIZONTAL);
        clickableSummary.setGravity(Gravity.CENTER_VERTICAL);
        clickableSummary.setPadding(
                dp(activity, 16), dp(activity, 12), dp(activity, 12), dp(activity, 12));
        clickableSummary.addView(summaryContent, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
        if (primaryAction != null) {
            ImageView chevron = new ImageView(activity);
            chevron.setImageResource(R.drawable.ic_chevron_right_24);
            chevron.setImageTintList(ColorStateList.valueOf(themeManager.secondaryTextColor()));
            chevron.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            LinearLayout.LayoutParams iconParams = new LinearLayout.LayoutParams(
                    dp(activity, 24), dp(activity, 24));
            iconParams.setMargins(dp(activity, 12), 0, 0, 0);
            clickableSummary.addView(chevron, iconParams);

            OperationsTriagePresentation.Action cardAction = primaryAction;
            clickableSummary.setClickable(true);
            clickableSummary.setFocusable(true);
            clickableSummary.setContentDescription(finding.cardAccessibilityLabel(cardAction));
            clickableSummary.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
            applySelectableBackground(activity, clickableSummary);
            clickableSummary.setOnClickListener(
                    view -> actionHandler.onAction(cardAction.actionId));
            evidence.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            title.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            if (summary != null) {
                summary.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            }
        }
        content.addView(clickableSummary, matchWidth());

        for (OperationsTriagePresentation.Action action : finding.actions) {
            if (action == primaryAction
                    || !OperationsTriagePresentation.isSupportedAction(action.actionId)
                    || !renderedActions.add(action.actionId)) {
                continue;
            }
            MaterialButton button = actionButton(activity, action);
            button.setText(action.buttonLabel());
            button.setMinHeight(dp(activity, 48));
            button.setContentDescription(action.description.isEmpty()
                    ? action.buttonLabel() : action.buttonLabel() + "。" + action.description);
            button.setOnClickListener(view -> actionHandler.onAction(action.actionId));
            LinearLayout.LayoutParams buttonParams = topMargin(dp(activity, 4));
            buttonParams.setMargins(
                    dp(activity, 16), dp(activity, 4), dp(activity, 16), dp(activity, 12));
            content.addView(button, buttonParams);
        }
        MaterialButton reviewButton = new MaterialButton(
                activity,
                null,
                finding.acknowledged
                        ? com.google.android.material.R.attr.materialButtonOutlinedStyle
                        : com.google.android.material.R.attr.materialButtonTonalStyle);
        reviewButton.setText(finding.acknowledged ? "撤销复核" : "标记已复核");
        reviewButton.setMinHeight(dp(activity, 48));
        reviewButton.setContentDescription(finding.acknowledged
                ? "撤销本机复核，恢复为待复核"
                : "标记为已在此手机复核；电脑状态不会改变，新证据会自动恢复为待复核");
        reviewButton.setOnClickListener(view -> reviewHandler.onReview(
                finding, !finding.acknowledged));
        LinearLayout.LayoutParams reviewParams = topMargin(dp(activity, 4));
        reviewParams.setMargins(
                dp(activity, 16), dp(activity, 4), dp(activity, 16), dp(activity, 12));
        content.addView(reviewButton, reviewParams);
        return content;
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
        TextView title = text(activity, value,
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

    private static int findingAccentColor(ThemeManager themeManager, int tone) {
        if (tone == OperationsTriagePresentation.TONE_ERROR) {
            return themeManager.errorColor();
        }
        if (tone == OperationsTriagePresentation.TONE_ATTENTION) {
            return themeManager.primaryColor();
        }
        return themeManager.secondaryTextColor();
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

    interface ReviewHandler {
        void onReview(OperationsTriagePresentation.Finding finding, boolean acknowledged);
    }
}

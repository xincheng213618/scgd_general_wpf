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
import java.util.Set;

final class OperationsTriageContent {
    private OperationsTriageContent() {
    }

    static View create(
            Activity activity,
            ThemeManager themeManager,
            OperationsTriagePresentation.ViewModel model,
            ActionHandler actionHandler,
            Runnable observe,
            Runnable refresh) {
        LinearLayout root = new LinearLayout(activity);
        root.setOrientation(LinearLayout.VERTICAL);

        ChipGroup quickActions = new ChipGroup(activity);
        quickActions.setSingleLine(false);
        quickActions.setChipSpacingHorizontal(dp(activity, 8));
        quickActions.setChipSpacingVertical(dp(activity, 4));
        quickActions.addView(actionChip(
                activity,
                activity.getString(R.string.operations_triage_observe_action),
                activity.getString(R.string.operations_triage_observe_content_description),
                R.drawable.ic_visibility_24,
                observe));
        quickActions.addView(actionChip(
                activity,
                "刷新摘要",
                "重新读取问题证据与可用处置动作",
                R.drawable.ic_refresh_24,
                refresh));
        root.addView(quickActions, matchWidth());

        if (!model.findings.isEmpty()) {
            root.addView(sectionTitle(
                    activity, themeManager, model.prioritySectionLabel()),
                    topMargin(dp(activity, 20)));
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
            String label,
            String contentDescription,
            int iconResource,
            Runnable action) {
        Chip chip = new Chip(activity);
        chip.setText(label);
        chip.setCheckable(false);
        chip.setEnsureMinTouchTargetSize(true);
        chip.setChipIconResource(iconResource);
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

    private static MaterialCardView findingCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsTriagePresentation.Finding finding,
            Set<String> renderedActions,
            ActionHandler actionHandler) {
        OperationsTriagePresentation.Action primaryAction = finding.primaryCardAction();
        if (primaryAction != null && !renderedActions.add(primaryAction.actionId)) {
            primaryAction = null;
        }

        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(activity, 16), dp(activity, 16), dp(activity, 16), dp(activity, 16));
        int contentColor = findingContentColor(themeManager, finding.tone());

        LinearLayout summaryContent = new LinearLayout(activity);
        summaryContent.setOrientation(LinearLayout.VERTICAL);

        TextView evidence = text(activity, finding.evidenceLabel(),
                com.google.android.material.R.style.TextAppearance_Material3_LabelLarge,
                contentColor);
        summaryContent.addView(evidence, matchWidth());
        TextView title = text(activity, finding.title,
                com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                contentColor);
        summaryContent.addView(title, topMargin(dp(activity, 6)));
        TextView summary = null;
        if (!finding.summary.isEmpty()) {
            summary = text(activity, finding.summary,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                    contentColor);
            summary.setLineSpacing(0, 1.06f);
            summary.setMaxLines(3);
            summary.setEllipsize(TextUtils.TruncateAt.END);
            summaryContent.addView(summary, topMargin(dp(activity, 8)));
        }
        TextView latest = null;
        if (!finding.latestAt.isEmpty()) {
            latest = text(activity, "最近证据 · " + finding.latestAt,
                    com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                    contentColor);
            summaryContent.addView(latest, topMargin(dp(activity, 8)));
        }

        TextView actionHint = null;
        if (primaryAction != null) {
            actionHint = text(activity, primaryAction.buttonLabel(),
                    com.google.android.material.R.style.TextAppearance_Material3_LabelLarge,
                    themeManager.primaryColor());
            summaryContent.addView(actionHint, topMargin(dp(activity, 10)));

            LinearLayout clickableSummary = new LinearLayout(activity);
            clickableSummary.setOrientation(LinearLayout.HORIZONTAL);
            clickableSummary.setGravity(Gravity.CENTER_VERTICAL);
            clickableSummary.addView(summaryContent, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
            ImageView chevron = new ImageView(activity);
            chevron.setImageResource(R.drawable.ic_chevron_right_24);
            chevron.setImageTintList(ColorStateList.valueOf(contentColor));
            chevron.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            LinearLayout.LayoutParams iconParams = new LinearLayout.LayoutParams(
                    dp(activity, 24), dp(activity, 24));
            iconParams.setMargins(dp(activity, 12), 0, 0, 0);
            clickableSummary.addView(chevron, iconParams);
            content.addView(clickableSummary, matchWidth());
        } else {
            content.addView(summaryContent, matchWidth());
        }

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
            content.addView(button, topMargin(dp(activity, 12)));
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(findingContainerColor(themeManager, finding.tone()));
        card.addView(content, matchCardWidth());
        if (primaryAction != null) {
            OperationsTriagePresentation.Action cardAction = primaryAction;
            card.setClickable(true);
            card.setFocusable(true);
            card.setContentDescription(finding.cardAccessibilityLabel(cardAction));
            card.setOnClickListener(view -> actionHandler.onAction(cardAction.actionId));
            evidence.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            title.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            if (summary != null) {
                summary.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            }
            if (latest != null) {
                latest.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            }
            actionHint.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        }
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

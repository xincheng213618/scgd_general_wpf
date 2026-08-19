package com.colorvision.xcviewer;

import android.app.Activity;
import android.content.res.ColorStateList;
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

import java.util.List;

final class OperationsRemoteProblemsContent {
    private OperationsRemoteProblemsContent() {
    }

    static View create(
            Activity activity,
            ThemeManager themeManager,
            OperationsRemoteProblemsPresentation.ViewModel model,
            String attentionContext,
            SectionHandler sectionHandler,
            ReviewHandler reviewHandler,
            Runnable reviewAll) {
        LinearLayout root = new LinearLayout(activity);
        root.setOrientation(LinearLayout.VERTICAL);

        boolean hasAttentionContext = attentionContext != null
                && !attentionContext.isEmpty();
        if (hasAttentionContext) {
            root.addView(attentionContextCard(
                    activity, themeManager, attentionContext), matchWidth());
        }
        root.addView(infoCard(activity, themeManager, model.summary),
                hasAttentionContext ? topMargin(dp(activity, 8)) : matchWidth());
        if (model.pendingIssues.size() > 1) {
            MaterialButton reviewAllButton = new MaterialButton(
                    activity,
                    null,
                    com.google.android.material.R.attr.materialButtonTonalStyle);
            reviewAllButton.setText(activity.getString(
                    R.string.operations_review_all_action, model.pendingIssues.size()));
            reviewAllButton.setMinHeight(dp(activity, 48));
            reviewAllButton.setContentDescription(activity.getString(
                    R.string.operations_review_all_remote_content_description,
                    model.pendingIssues.size()));
            reviewAllButton.setOnClickListener(view -> reviewAll.run());
            root.addView(reviewAllButton, topMargin(dp(activity, 12)));
        }
        if (!model.pendingIssues.isEmpty()) {
            root.addView(sectionTitle(
                            activity,
                            themeManager,
                            "优先处理 · " + model.pendingIssues.size()),
                    topMargin(dp(activity, 20)));
            root.addView(issueContent(
                    activity,
                    themeManager,
                    model.pendingIssues,
                    sectionHandler,
                    reviewHandler),
                    topMargin(dp(activity, 8)));
        }
        if (!model.reviewedIssues.isEmpty()) {
            root.addView(sectionTitle(activity, themeManager, "已复核 · 状态仍存在"),
                    topMargin(dp(activity, 20)));
            root.addView(issueContent(
                    activity,
                    themeManager,
                    model.reviewedIssues,
                    sectionHandler,
                    reviewHandler),
                    topMargin(dp(activity, 8)));
        }

        root.addView(sectionTitle(activity, themeManager, "操作边界"),
                topMargin(dp(activity, 20)));
        root.addView(infoCard(
                activity,
                themeManager,
                OperationsRemoteProblemsPresentation.SAFETY_NOTICE),
                topMargin(dp(activity, 8)));

        return root;
    }

    private static MaterialCardView attentionContextCard(
            Activity activity, ThemeManager themeManager, String message) {
        TextView text = text(
                activity,
                message,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.onSecondaryContainerColor());
        text.setLineSpacing(0, 1.06f);
        text.setPadding(dp(activity, 16), dp(activity, 14), dp(activity, 16), dp(activity, 14));
        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.secondaryContainerColor());
        card.addView(text, matchCardWidth());
        return card;
    }

    private static View issueContent(
            Activity activity,
            ThemeManager themeManager,
            List<OperationsRemoteProblemsPresentation.Issue> issues,
            SectionHandler sectionHandler,
            ReviewHandler reviewHandler) {
        boolean twoColumns = AppResponsiveLayout.usesTwoColumnGrid(
                activity.getResources().getConfiguration().screenWidthDp,
                activity.getResources().getConfiguration().fontScale,
                issues.size());
        return twoColumns
                ? issueGrid(activity, themeManager, issues, sectionHandler, reviewHandler)
                : issueListCard(activity, themeManager, issues, sectionHandler, reviewHandler);
    }

    private static MaterialCardView issueListCard(
            Activity activity,
            ThemeManager themeManager,
            List<OperationsRemoteProblemsPresentation.Issue> issues,
            SectionHandler sectionHandler,
            ReviewHandler reviewHandler) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < issues.size(); index++) {
            rows.addView(issueRow(
                    activity,
                    themeManager,
                    issues.get(index),
                    sectionHandler,
                    reviewHandler), matchWidth());
            if (index < issues.size() - 1) {
                rows.addView(divider(activity, themeManager), dividerParams(activity));
            }
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(rows, matchCardWidth());
        return card;
    }

    private static View issueGrid(
            Activity activity,
            ThemeManager themeManager,
            List<OperationsRemoteProblemsPresentation.Issue> issues,
            SectionHandler sectionHandler,
            ReviewHandler reviewHandler) {
        LinearLayout grid = new LinearLayout(activity);
        grid.setOrientation(LinearLayout.VERTICAL);
        int spacing = dp(activity, 8);
        for (int index = 0; index < issues.size(); index += 2) {
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.HORIZONTAL);
            int itemsInRow = Math.min(2, issues.size() - index);
            for (int column = 0; column < itemsInRow; column++) {
                MaterialCardView card = new MaterialCardView(activity);
                card.setCardBackgroundColor(themeManager.cardBackgroundColor());
                card.addView(issueRow(
                        activity,
                        themeManager,
                        issues.get(index + column),
                        sectionHandler,
                        reviewHandler), matchCardWidth());
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

    private static View issueRow(
            Activity activity,
            ThemeManager themeManager,
            OperationsRemoteProblemsPresentation.Issue issue,
            SectionHandler sectionHandler,
            ReviewHandler reviewHandler) {
        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);

        LinearLayout clickableSummary = new LinearLayout(activity);
        clickableSummary.setOrientation(LinearLayout.HORIZONTAL);
        clickableSummary.setGravity(Gravity.CENTER_VERTICAL);
        clickableSummary.setMinimumHeight(dp(activity, 68));
        clickableSummary.setPadding(
                dp(activity, 16), dp(activity, 10), dp(activity, 12), dp(activity, 10));

        LinearLayout copy = new LinearLayout(activity);
        copy.setOrientation(LinearLayout.VERTICAL);
        TextView title = text(
                activity,
                issue.status.title,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                themeManager.primaryTextColor());
        TextView summary = text(
                activity,
                issue.status.summary,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.errorColor());
        copy.addView(title, matchWidth());
        copy.addView(summary, topMargin(dp(activity, 2)));
        clickableSummary.addView(copy, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        ImageView chevron = new ImageView(activity);
        chevron.setImageResource(R.drawable.ic_chevron_right_24);
        chevron.setImageTintList(ColorStateList.valueOf(themeManager.secondaryTextColor()));
        chevron.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        LinearLayout.LayoutParams iconParams = new LinearLayout.LayoutParams(
                dp(activity, 24), dp(activity, 24));
        iconParams.setMargins(dp(activity, 12), 0, 0, 0);
        clickableSummary.addView(chevron, iconParams);

        clickableSummary.setContentDescription(issue.accessibilityLabel());
        clickableSummary.setClickable(true);
        clickableSummary.setFocusable(true);
        clickableSummary.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
        applySelectableBackground(activity, clickableSummary);
        clickableSummary.setOnClickListener(view -> sectionHandler.onSection(issue.section));
        title.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        summary.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        content.addView(clickableSummary, matchWidth());

        MaterialButton reviewButton = new MaterialButton(
                activity,
                null,
                issue.acknowledged
                        ? com.google.android.material.R.attr.materialButtonOutlinedStyle
                        : com.google.android.material.R.attr.materialButtonTonalStyle);
        reviewButton.setText(issue.acknowledged ? "撤销复核" : "标记已复核");
        reviewButton.setMinHeight(dp(activity, 48));
        reviewButton.setContentDescription(issue.acknowledged
                ? "撤销本机复核，恢复为待复核"
                : "标记为已在此手机复核；电脑签名状态不会改变，新证据会自动恢复为待复核");
        reviewButton.setOnClickListener(view -> reviewHandler.onReview(
                issue, !issue.acknowledged));
        LinearLayout.LayoutParams reviewParams = topMargin(dp(activity, 4));
        reviewParams.setMargins(
                dp(activity, 16), dp(activity, 4), dp(activity, 16), dp(activity, 12));
        content.addView(reviewButton, reviewParams);
        return content;
    }

    private static MaterialCardView infoCard(
            Activity activity, ThemeManager themeManager, String value) {
        TextView text = text(
                activity,
                value,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.secondaryTextColor());
        text.setLineSpacing(0, 1.06f);
        text.setPadding(dp(activity, 16), dp(activity, 14), dp(activity, 16), dp(activity, 14));
        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(text, matchCardWidth());
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

    private static void applySelectableBackground(Activity activity, View view) {
        TypedValue selectable = new TypedValue();
        if (activity.getTheme().resolveAttribute(
                android.R.attr.selectableItemBackground, selectable, true)
                && selectable.resourceId != 0) {
            view.setBackgroundResource(selectable.resourceId);
        }
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

    interface SectionHandler {
        void onSection(String section);
    }

    interface ReviewHandler {
        void onReview(
                OperationsRemoteProblemsPresentation.Issue issue,
                boolean acknowledged);
    }
}

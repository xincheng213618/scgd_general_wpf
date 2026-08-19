package com.colorvision.xcviewer;

import android.app.Activity;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.view.ViewCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.card.MaterialCardView;
import com.google.android.material.chip.Chip;
import com.google.android.material.chip.ChipGroup;

import java.util.List;

final class OperationsAuditContent {
    private OperationsAuditContent() {
    }

    static View create(
            Activity activity,
            ThemeManager themeManager,
            OperationsAuditPresentation.ViewModel model) {
        LinearLayout root = new LinearLayout(activity);
        root.setOrientation(LinearLayout.VERTICAL);
        root.addView(summaryCard(activity, themeManager, model), matchWidth());

        TextView sectionTitle = sectionTitle(activity, themeManager, "操作记录");
        LinearLayout listHost = new LinearLayout(activity);
        listHost.setOrientation(LinearLayout.VERTICAL);

        if (!model.entries.isEmpty() && model.routineCount > 0) {
            ChipGroup filters = new ChipGroup(activity);
            filters.setSingleSelection(true);
            filters.setSelectionRequired(true);
            filters.setSingleLine(false);
            filters.setChipSpacingHorizontal(dp(activity, 8));
            filters.setChipSpacingVertical(dp(activity, 4));

            Chip focused = filterChip(
                    activity, "关键操作 " + model.focusedEntries.size());
            Chip all = filterChip(activity, "全部 " + model.entries.size());
            filters.addView(focused);
            filters.addView(all);
            root.addView(filters, topMargin(dp(activity, 16)));
            root.addView(sectionTitle, topMargin(dp(activity, 16)));
            root.addView(listHost, topMargin(dp(activity, 8)));

            focused.setOnClickListener(view -> renderEntries(
                    activity,
                    themeManager,
                    sectionTitle,
                    listHost,
                    "关键操作",
                    model.focusedEntries,
                    model.hiddenEntryCount));
            all.setOnClickListener(view -> renderEntries(
                    activity,
                    themeManager,
                    sectionTitle,
                    listHost,
                    "全部记录",
                    model.entries,
                    model.hiddenEntryCount));
            if (model.defaultsToFocusedEntries()) {
                focused.setChecked(true);
                renderEntries(activity, themeManager, sectionTitle, listHost,
                        "关键操作", model.focusedEntries, model.hiddenEntryCount);
            } else {
                all.setChecked(true);
                renderEntries(activity, themeManager, sectionTitle, listHost,
                        "全部记录", model.entries, model.hiddenEntryCount);
            }
        } else {
            root.addView(sectionTitle, topMargin(dp(activity, 20)));
            root.addView(listHost, topMargin(dp(activity, 8)));
            renderEntries(activity, themeManager, sectionTitle, listHost,
                    "操作记录", model.entries, model.hiddenEntryCount);
        }

        root.addView(sectionTitle(activity, themeManager, "数据边界"),
                topMargin(dp(activity, 20)));
        root.addView(infoCard(
                        activity, themeManager, OperationsAuditPresentation.PRIVACY_NOTICE),
                topMargin(dp(activity, 8)));
        return root;
    }

    private static void renderEntries(
            Activity activity,
            ThemeManager themeManager,
            TextView sectionTitle,
            LinearLayout listHost,
            String label,
            List<OperationsAuditPresentation.Entry> entries,
            int hiddenEntryCount) {
        sectionTitle.setText(entries.isEmpty()
                ? label
                : activity.getString(
                        R.string.operations_audit_section_count,
                        label,
                        entries.size()));
        listHost.removeAllViews();
        if (entries.isEmpty()) {
            listHost.addView(infoCard(
                    activity,
                    themeManager,
                    "当前没有符合此筛选条件的操作记录。"),
                    matchWidth());
            return;
        }
        listHost.addView(entryListCard(
                activity, themeManager, entries, hiddenEntryCount), matchWidth());
    }

    private static MaterialCardView summaryCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsAuditPresentation.ViewModel model) {
        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(activity, 16), dp(activity, 14), dp(activity, 16), dp(activity, 14));

        TextView title = text(
                activity,
                "操作概览",
                com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                themeManager.primaryTextColor());
        ViewCompat.setAccessibilityHeading(title, true);
        content.addView(title, matchWidth());
        if (!model.entries.isEmpty()) {
            content.addView(text(
                            activity,
                            model.summaryLabel(),
                            com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                            summaryColor(themeManager, model)),
                    topMargin(dp(activity, 6)));
        }
        if (model.routineCount > 0) {
            content.addView(text(
                            activity,
                            "默认折叠 " + model.routineCount
                                    + " 条重复的持续观察读取，可切换“全部”查看。",
                            com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                            themeManager.secondaryTextColor()),
                    topMargin(dp(activity, 8)));
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(content, matchCardWidth());
        return card;
    }

    private static MaterialCardView entryListCard(
            Activity activity,
            ThemeManager themeManager,
            List<OperationsAuditPresentation.Entry> entries,
            int hiddenEntryCount) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < entries.size(); index++) {
            OperationsAuditPresentation.Entry entry = entries.get(index);
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.VERTICAL);
            row.setMinimumHeight(dp(activity, 64));
            row.setPadding(dp(activity, 16), dp(activity, 12), dp(activity, 16), dp(activity, 12));

            TextView action = text(
                    activity,
                    entry.actionLabel,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                    themeManager.primaryTextColor());
            TextView metadata = text(
                    activity,
                    entry.metadataLabel(),
                    com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                    entryColor(themeManager, entry.tone));
            row.addView(action, matchWidth());
            row.addView(metadata, topMargin(dp(activity, 3)));
            row.setContentDescription(entry.accessibilityLabel());
            row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
            action.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            metadata.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            rows.addView(row, matchWidth());
            if (index < entries.size() - 1) {
                rows.addView(divider(activity, themeManager), dividerParams(activity));
            }
        }
        if (hiddenEntryCount > 0) {
            TextView hidden = text(
                    activity,
                    "另有 " + hiddenEntryCount + " 条记录未返回。",
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

    private static Chip filterChip(Activity activity, String label) {
        Chip chip = new Chip(activity);
        chip.setId(View.generateViewId());
        chip.setText(label);
        chip.setCheckable(true);
        chip.setCheckedIconVisible(true);
        chip.setEnsureMinTouchTargetSize(true);
        return chip;
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

    private static int summaryColor(
            ThemeManager themeManager,
            OperationsAuditPresentation.ViewModel model) {
        if (model.errorCount > 0) {
            return themeManager.errorColor();
        }
        return model.attentionCount > 0
                ? themeManager.primaryColor()
                : themeManager.secondaryTextColor();
    }

    private static int entryColor(ThemeManager themeManager, int tone) {
        if (tone == OperationsAuditPresentation.TONE_ERROR) {
            return themeManager.errorColor();
        }
        return tone == OperationsAuditPresentation.TONE_ATTENTION
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

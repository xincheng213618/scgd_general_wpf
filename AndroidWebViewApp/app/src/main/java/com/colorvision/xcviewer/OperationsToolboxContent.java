package com.colorvision.xcviewer;

import android.app.Activity;
import android.text.Editable;
import android.text.TextWatcher;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.HorizontalScrollView;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.view.ViewCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.button.MaterialButton;
import com.google.android.material.card.MaterialCardView;
import com.google.android.material.chip.Chip;
import com.google.android.material.chip.ChipGroup;
import com.google.android.material.textfield.TextInputEditText;
import com.google.android.material.textfield.TextInputLayout;

import java.util.ArrayList;
import java.util.List;

final class OperationsToolboxContent {
    private OperationsToolboxContent() {
    }

    static void addTo(
            Activity activity,
            ThemeManager themeManager,
            LinearLayout target,
            OperationsToolboxPresentation.ViewModel model,
            ActionHandler actionHandler,
            SectionHandler sectionHandler) {
        TextInputLayout searchField = (TextInputLayout) activity.getLayoutInflater().inflate(
                R.layout.operations_toolbox_search, target, false);
        TextInputEditText searchInput = searchField.findViewById(
                R.id.operations_toolbox_search_input);
        target.addView(searchField);

        LinearLayout results = new LinearLayout(activity);
        results.setOrientation(LinearLayout.VERTICAL);
        target.addView(results, matchWidth());
        renderResults(activity, themeManager, results, model, "",
                actionHandler, sectionHandler);
        searchInput.addTextChangedListener(new TextWatcher() {
            @Override
            public void beforeTextChanged(CharSequence text, int start, int count, int after) {
            }

            @Override
            public void onTextChanged(CharSequence text, int start, int before, int count) {
                renderResults(activity, themeManager, results, model, text.toString(),
                        actionHandler, sectionHandler);
            }

            @Override
            public void afterTextChanged(Editable editable) {
            }
        });
    }

    private static void renderResults(
            Activity activity,
            ThemeManager themeManager,
            LinearLayout target,
            OperationsToolboxPresentation.ViewModel source,
            String query,
            ActionHandler actionHandler,
            SectionHandler sectionHandler) {
        target.removeAllViews();
        if (query.trim().isEmpty()) {
            renderAllTools(activity, themeManager, target, source,
                    actionHandler, sectionHandler);
            return;
        }

        OperationsToolboxPresentation.ViewModel filtered =
                OperationsToolboxPresentation.filter(source, query);
        TextView resultHeading = sectionTitle(
                activity,
                themeManager,
                "搜索结果 · " + filtered.actionCount() + " 项");
        resultHeading.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        target.addView(resultHeading, matchWidth());
        if (filtered.sections.isEmpty()) {
            target.addView(emptySearchCard(activity, themeManager), cardParams(activity));
            return;
        }
        addSectionCards(activity, themeManager, target, filtered.sections, actionHandler);
    }

    private static void renderAllTools(
            Activity activity,
            ThemeManager themeManager,
            LinearLayout target,
            OperationsToolboxPresentation.ViewModel model,
            ActionHandler actionHandler,
            SectionHandler sectionHandler) {
        if (!model.quickActions.isEmpty()) {
            target.addView(sectionTitle(
                    activity,
                    themeManager,
                    OperationsToolboxPresentation.QUICK_SECTION_TITLE), matchWidth());
            target.addView(quickActionGrid(
                    activity, model.quickActions, actionHandler), cardParams(activity));
        }

        target.addView(sectionTitle(activity, themeManager, "全部工具"), matchWidth());
        List<Chip> shortcutChips = new ArrayList<>();
        ChipGroup shortcutGroup = new ChipGroup(activity);
        shortcutGroup.setSingleLine(true);
        shortcutGroup.setChipSpacingHorizontal(dp(activity, 8));
        for (OperationsToolboxPresentation.Section section : model.sections) {
            Chip shortcut = new Chip(activity);
            shortcut.setText(section.shortcutLabel());
            shortcut.setCheckable(false);
            shortcut.setEnsureMinTouchTargetSize(true);
            shortcut.setContentDescription(section.shortcutAccessibilityLabel());
            shortcutGroup.addView(shortcut);
            shortcutChips.add(shortcut);
        }
        HorizontalScrollView shortcutScroll = new HorizontalScrollView(activity);
        shortcutScroll.setHorizontalScrollBarEnabled(false);
        shortcutScroll.setFillViewport(false);
        shortcutScroll.setContentDescription("工具分组快捷导航");
        shortcutScroll.addView(shortcutGroup, new HorizontalScrollView.LayoutParams(
                HorizontalScrollView.LayoutParams.WRAP_CONTENT,
                HorizontalScrollView.LayoutParams.WRAP_CONTENT));
        LinearLayout.LayoutParams shortcutParams = matchWidth();
        shortcutParams.setMargins(0, dp(activity, 2), 0, dp(activity, 2));
        target.addView(shortcutScroll, shortcutParams);

        List<TextView> sectionHeadings = new ArrayList<>();
        for (OperationsToolboxPresentation.Section section : model.sections) {
            TextView heading = sectionTitle(activity, themeManager, section.title);
            sectionHeadings.add(heading);
            target.addView(heading, matchWidth());
            target.addView(sectionCard(
                    activity, themeManager, section, actionHandler), cardParams(activity));
        }
        for (int index = 0; index < shortcutChips.size(); index++) {
            TextView heading = sectionHeadings.get(index);
            shortcutChips.get(index).setOnClickListener(
                    view -> sectionHandler.onSection(target.getTop() + heading.getTop()));
        }
    }

    private static void addSectionCards(
            Activity activity,
            ThemeManager themeManager,
            LinearLayout target,
            List<OperationsToolboxPresentation.Section> sections,
            ActionHandler actionHandler) {
        for (OperationsToolboxPresentation.Section section : sections) {
            target.addView(sectionTitle(activity, themeManager, section.title), matchWidth());
            target.addView(sectionCard(
                    activity, themeManager, section, actionHandler), cardParams(activity));
        }
    }

    private static MaterialCardView emptySearchCard(
            Activity activity,
            ThemeManager themeManager) {
        LinearLayout copy = new LinearLayout(activity);
        copy.setOrientation(LinearLayout.VERTICAL);
        copy.setPadding(dp(activity, 20), dp(activity, 18), dp(activity, 20), dp(activity, 18));
        copy.addView(text(
                activity,
                "没有找到工具",
                com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                themeManager.primaryTextColor()), matchWidth());
        TextView supportingText = text(
                activity,
                "可搜索工具名称、说明或分组，例如“诊断”、“恢复”或“支持”。",
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.secondaryTextColor());
        LinearLayout.LayoutParams supportingParams = matchWidth();
        supportingParams.setMargins(0, dp(activity, 4), 0, 0);
        copy.addView(supportingText, supportingParams);

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(copy, new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT));
        return card;
    }

    private static View quickActionGrid(
            Activity activity,
            List<OperationsToolboxPresentation.Action> actions,
            ActionHandler actionHandler) {
        LinearLayout grid = new LinearLayout(activity);
        grid.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < actions.size(); index += 2) {
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.addView(quickActionButton(
                    activity, actions.get(index), actionHandler), weightedButtonParams());
            if (index + 1 < actions.size()) {
                LinearLayout.LayoutParams secondParams = weightedButtonParams();
                secondParams.setMargins(dp(activity, 8), 0, 0, 0);
                row.addView(quickActionButton(
                        activity, actions.get(index + 1), actionHandler), secondParams);
            } else {
                View spacer = new View(activity);
                LinearLayout.LayoutParams spacerParams = weightedButtonParams();
                spacerParams.setMargins(dp(activity, 8), 0, 0, 0);
                row.addView(spacer, spacerParams);
            }
            LinearLayout.LayoutParams rowParams = matchWidth();
            if (index > 0) {
                rowParams.setMargins(0, dp(activity, 8), 0, 0);
            }
            grid.addView(row, rowParams);
        }
        return grid;
    }

    private static MaterialButton quickActionButton(
            Activity activity,
            OperationsToolboxPresentation.Action action,
            ActionHandler actionHandler) {
        MaterialButton button = new MaterialButton(
                activity, null,
                com.google.android.material.R.attr.materialButtonTonalStyle);
        button.setText(action.title);
        button.setMinHeight(dp(activity, 56));
        button.setEnabled(action.enabled);
        button.setContentDescription(action.accessibilityLabel());
        button.setOnClickListener(view -> actionHandler.onAction(action.actionId));
        return button;
    }

    private static TextView sectionTitle(
            Activity activity, ThemeManager themeManager, String value) {
        TextView view = text(activity, value,
                com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                themeManager.primaryTextColor());
        view.setPadding(dp(activity, 4), dp(activity, 12), 0, dp(activity, 8));
        ViewCompat.setAccessibilityHeading(view, true);
        return view;
    }

    private static MaterialCardView sectionCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsToolboxPresentation.Section section,
            ActionHandler actionHandler) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < section.actions.size(); index++) {
            OperationsToolboxPresentation.Action action = section.actions.get(index);
            rows.addView(actionRow(activity, themeManager, action,
                    view -> actionHandler.onAction(action.actionId)), matchWidth());
            if (index < section.actions.size() - 1) {
                rows.addView(divider(activity, themeManager), dividerParams(activity));
            }
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(rows, new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT));
        return card;
    }

    private static LinearLayout actionRow(
            Activity activity,
            ThemeManager themeManager,
            OperationsToolboxPresentation.Action action,
            View.OnClickListener listener) {
        LinearLayout row = new LinearLayout(activity);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setMinimumHeight(dp(activity, 72));
        row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 12), dp(activity, 10));
        row.setClickable(action.enabled);
        row.setFocusable(true);
        row.setEnabled(action.enabled);
        row.setAlpha(action.enabled ? 1f : 0.56f);
        if (action.enabled) {
            row.setOnClickListener(listener);
        }
        TypedValue selectableBackground = new TypedValue();
        if (activity.getTheme().resolveAttribute(
                android.R.attr.selectableItemBackground, selectableBackground, true)) {
            row.setBackgroundResource(selectableBackground.resourceId);
        }

        LinearLayout labels = new LinearLayout(activity);
        labels.setOrientation(LinearLayout.VERTICAL);
        TextView title = text(activity, action.title,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                themeManager.primaryTextColor());
        title.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        labels.addView(title, matchWidth());
        TextView summary = text(activity, action.summary,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.secondaryTextColor());
        summary.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        LinearLayout.LayoutParams summaryParams = matchWidth();
        summaryParams.setMargins(0, dp(activity, 2), 0, 0);
        labels.addView(summary, summaryParams);
        row.addView(labels, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        ImageView arrow = new ImageView(activity);
        arrow.setImageResource(R.drawable.ic_chevron_right_24);
        arrow.setColorFilter(themeManager.secondaryTextColor());
        arrow.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        row.addView(arrow, new LinearLayout.LayoutParams(
                dp(activity, 24), dp(activity, 24)));
        row.setContentDescription(action.accessibilityLabel());
        return row;
    }

    private static TextView text(Activity activity, String value, int appearance, int color) {
        TextView view = new TextView(activity);
        view.setText(value);
        view.setTextColor(color);
        TextViewCompat.setTextAppearance(view, appearance);
        return view;
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

    private static LinearLayout.LayoutParams cardParams(Activity activity) {
        LinearLayout.LayoutParams params = matchWidth();
        params.setMargins(0, 0, 0, dp(activity, 8));
        return params;
    }

    private static LinearLayout.LayoutParams weightedButtonParams() {
        return new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1);
    }

    private static int dp(Activity activity, int value) {
        return Math.round(value * activity.getResources().getDisplayMetrics().density);
    }

    interface ActionHandler {
        void onAction(String actionId);
    }

    interface SectionHandler {
        void onSection(int sectionOffset);
    }
}

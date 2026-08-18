package com.colorvision.xcviewer;

import android.app.Activity;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.bottomsheet.BottomSheetBehavior;
import com.google.android.material.bottomsheet.BottomSheetDialog;
import com.google.android.material.button.MaterialButton;
import com.google.android.material.card.MaterialCardView;

final class OperationsToolboxBottomSheet {
    private OperationsToolboxBottomSheet() {
    }

    static MaterialCardView createDashboardEntry(
            Activity activity,
            ThemeManager themeManager,
            View.OnClickListener listener) {
        LinearLayout row = actionRow(
                activity,
                themeManager,
                "运维工具箱",
                "服务诊断、恢复、取证、审批与支持",
                listener);
        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(row, matchCardWidth());
        return card;
    }

    static void show(
            Activity activity,
            ThemeManager themeManager,
            OperationsToolboxPresentation.ViewModel model,
            ActionHandler actionHandler) {
        BottomSheetDialog dialog = new BottomSheetDialog(activity);
        LinearLayout sheetRoot = new LinearLayout(activity);
        sheetRoot.setOrientation(LinearLayout.VERTICAL);

        int horizontalPadding = dp(activity, 24);
        LinearLayout heading = new LinearLayout(activity);
        boolean singleColumn = AppResponsiveLayout.usesSingleColumn(
                activity.getResources().getConfiguration().screenWidthDp,
                activity.getResources().getConfiguration().fontScale);
        heading.setOrientation(singleColumn
                ? LinearLayout.VERTICAL : LinearLayout.HORIZONTAL);
        heading.setGravity(singleColumn ? Gravity.START : Gravity.CENTER_VERTICAL);
        TextView title = text(activity, "运维工具箱",
                com.google.android.material.R.style.TextAppearance_Material3_HeadlineSmall,
                themeManager.primaryTextColor());
        heading.addView(title, singleColumn
                ? matchWidth()
                : new LinearLayout.LayoutParams(
                        0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
        MaterialButton close = new MaterialButton(
                activity, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        close.setText("关闭");
        close.setMinHeight(dp(activity, 48));
        close.setOnClickListener(view -> dialog.dismiss());
        heading.addView(close, singleColumn
                ? topMargin(dp(activity, 8))
                : new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.WRAP_CONTENT,
                        LinearLayout.LayoutParams.WRAP_CONTENT));

        LinearLayout header = new LinearLayout(activity);
        header.setPadding(horizontalPadding, dp(activity, 20), horizontalPadding, dp(activity, 8));
        header.addView(heading, matchWidth());
        sheetRoot.addView(header, matchWidth());

        ScrollView scroll = new ScrollView(activity);
        scroll.setFillViewport(true);
        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);
        int bottomPadding = dp(activity, 24) + navigationBarInset(activity);
        content.setPadding(horizontalPadding, dp(activity, 4), horizontalPadding, bottomPadding);

        TextView introduction = text(activity,
                "高级工具按任务分组。只读项目可直接打开；恢复、取证和支持动作仍会在执行前确认。",
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.secondaryTextColor());
        introduction.setLineSpacing(0, 1.06f);
        content.addView(introduction, matchWidth());

        for (OperationsToolboxPresentation.Section section : model.sections) {
            TextView sectionTitle = text(activity, section.title,
                    com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                    themeManager.primaryTextColor());
            content.addView(sectionTitle, topMargin(dp(activity, 20)));
            content.addView(sectionCard(
                            activity,
                            themeManager,
                            section,
                            dialog,
                            actionHandler),
                    topMargin(dp(activity, 8)));
        }

        scroll.addView(content, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT,
                ScrollView.LayoutParams.WRAP_CONTENT));
        sheetRoot.addView(scroll, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));

        dialog.setContentView(sheetRoot, new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT));
        dialog.setOnShowListener(ignored -> {
            View sheet = dialog.findViewById(com.google.android.material.R.id.design_bottom_sheet);
            if (sheet != null) {
                ViewCompat.setAccessibilityPaneTitle(sheet, "运维工具箱");
                BottomSheetBehavior<View> behavior = BottomSheetBehavior.from(sheet);
                behavior.setSkipCollapsed(true);
                behavior.setState(BottomSheetBehavior.STATE_EXPANDED);
                ViewCompat.setOnApplyWindowInsetsListener(content, (view, windowInsets) -> {
                    Insets navigationBars = windowInsets.getInsets(
                            WindowInsetsCompat.Type.navigationBars());
                    Insets displayCutout = windowInsets.getInsets(
                            WindowInsetsCompat.Type.displayCutout());
                    int safeBottom = Math.max(
                            navigationBarInset(activity),
                            Math.max(navigationBars.bottom, displayCutout.bottom));
                    view.setPadding(horizontalPadding, dp(activity, 4), horizontalPadding,
                            dp(activity, 24) + safeBottom);
                    return windowInsets;
                });
                ViewCompat.requestApplyInsets(content);
            }
        });
        dialog.show();
    }

    private static MaterialCardView sectionCard(
            Activity activity,
            ThemeManager themeManager,
            OperationsToolboxPresentation.Section section,
            BottomSheetDialog dialog,
            ActionHandler actionHandler) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < section.actions.size(); index++) {
            OperationsToolboxPresentation.Action action = section.actions.get(index);
            rows.addView(actionRow(
                    activity,
                    themeManager,
                    action.title,
                    action.summary,
                    view -> {
                        dialog.dismiss();
                        actionHandler.onAction(action.actionId);
                    }), matchWidth());
            if (index < section.actions.size() - 1) {
                rows.addView(divider(activity, themeManager), dividerParams(activity));
            }
        }
        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(rows, matchCardWidth());
        return card;
    }

    private static LinearLayout actionRow(
            Activity activity,
            ThemeManager themeManager,
            String title,
            String summary,
            View.OnClickListener listener) {
        LinearLayout row = new LinearLayout(activity);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setMinimumHeight(dp(activity, 72));
        row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 12), dp(activity, 10));
        row.setClickable(true);
        row.setFocusable(true);
        row.setOnClickListener(listener);
        android.util.TypedValue selectableBackground = new android.util.TypedValue();
        if (activity.getTheme().resolveAttribute(
                android.R.attr.selectableItemBackground, selectableBackground, true)) {
            row.setBackgroundResource(selectableBackground.resourceId);
        }

        LinearLayout labels = new LinearLayout(activity);
        labels.setOrientation(LinearLayout.VERTICAL);
        TextView titleView = text(activity, title,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                themeManager.primaryTextColor());
        titleView.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        labels.addView(titleView, matchWidth());
        TextView summaryView = text(activity, summary,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.secondaryTextColor());
        summaryView.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        labels.addView(summaryView, topMargin(dp(activity, 2)));
        row.addView(labels, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        ImageView arrow = new ImageView(activity);
        arrow.setImageResource(R.drawable.ic_chevron_right_24);
        arrow.setColorFilter(themeManager.secondaryTextColor());
        arrow.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        row.addView(arrow, new LinearLayout.LayoutParams(dp(activity, 24), dp(activity, 24)));
        row.setContentDescription(title + "，" + summary.replace(" · ", "，"));
        return row;
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

    private static int navigationBarInset(Activity activity) {
        WindowInsetsCompat windowInsets = ViewCompat.getRootWindowInsets(
                activity.getWindow().getDecorView());
        if (windowInsets == null) {
            return dp(activity, 24);
        }
        Insets navigationBars = windowInsets.getInsets(WindowInsetsCompat.Type.navigationBars());
        Insets displayCutout = windowInsets.getInsets(WindowInsetsCompat.Type.displayCutout());
        return Math.max(dp(activity, 24), Math.max(navigationBars.bottom, displayCutout.bottom));
    }

    interface ActionHandler {
        void onAction(String actionId);
    }
}

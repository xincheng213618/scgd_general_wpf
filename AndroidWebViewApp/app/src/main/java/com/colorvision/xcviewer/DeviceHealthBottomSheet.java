package com.colorvision.xcviewer;

import android.app.Activity;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
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
import com.google.android.material.dialog.MaterialAlertDialogBuilder;

final class DeviceHealthBottomSheet {
    private DeviceHealthBottomSheet() {
    }

    static void show(
            Activity activity,
            ThemeManager themeManager,
            DeviceHealthPresentation.ViewModel model,
            String observedAt,
            Runnable refresh,
            Runnable openTriage) {
        BottomSheetDialog dialog = new BottomSheetDialog(activity);
        LinearLayout sheetRoot = new LinearLayout(activity);
        sheetRoot.setOrientation(LinearLayout.VERTICAL);
        ScrollView scroll = new ScrollView(activity);
        scroll.setFillViewport(true);
        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);
        int horizontalPadding = dp(activity, 24);
        int topPadding = dp(activity, 8);
        int bottomPadding = dp(activity, 12);
        content.setPadding(horizontalPadding, topPadding, horizontalPadding, bottomPadding);
        scroll.addView(content, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT,
                ScrollView.LayoutParams.WRAP_CONTENT));

        LinearLayout heading = new LinearLayout(activity);
        heading.setGravity(Gravity.CENTER_VERTICAL);
        TextView title = text(activity, "检测设备状态",
                com.google.android.material.R.style.TextAppearance_Material3_HeadlineSmall,
                themeManager.primaryTextColor());
        heading.addView(title, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
        MaterialButton close = new MaterialButton(
                activity, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        close.setText("关闭");
        close.setMinHeight(dp(activity, 48));
        close.setOnClickListener(view -> dialog.dismiss());
        heading.addView(close, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));
        LinearLayout header = new LinearLayout(activity);
        header.setPadding(
                horizontalPadding,
                dp(activity, 20),
                horizontalPadding,
                dp(activity, 8));
        header.addView(heading, matchWidth());

        LinearLayout overview = new LinearLayout(activity);
        overview.setOrientation(LinearLayout.VERTICAL);
        overview.setPadding(dp(activity, 16), dp(activity, 16), dp(activity, 16), dp(activity, 16));
        int overviewContentColor = model.attentionRequired
                ? themeManager.onErrorContainerColor() : themeManager.onPrimaryContainerColor();
        TextView headline = text(activity, model.headline,
                com.google.android.material.R.style.TextAppearance_Material3_TitleLarge,
                overviewContentColor);
        overview.addView(headline, matchWidth());
        TextView summary = text(activity, model.summary,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                overviewContentColor);
        overview.addView(summary, topMargin(dp(activity, 6)));
        if (!model.unavailableReasons.isEmpty()) {
            TextView reasons = text(activity, "不可用原因 · " + model.unavailableReasons,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                    overviewContentColor);
            reasons.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            overview.addView(reasons, topMargin(dp(activity, 6)));
        }
        TextView guidance = text(activity, model.guidance,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                overviewContentColor);
        overview.addView(guidance, topMargin(dp(activity, 10)));
        overview.setContentDescription(model.accessibilitySummary());
        overview.setFocusable(true);
        overview.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
        headline.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        summary.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        guidance.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);

        MaterialCardView overviewCard = new MaterialCardView(activity);
        overviewCard.setCardBackgroundColor(model.attentionRequired
                ? themeManager.errorContainerColor() : themeManager.primaryContainerColor());
        overviewCard.addView(overview, matchCardWidth());
        LinearLayout.LayoutParams overviewParams = matchWidth();
        overviewParams.setMargins(0, dp(activity, 16), 0, 0);
        content.addView(overviewCard, overviewParams);

        if (!model.categories.isEmpty()) {
            TextView categoryTitle = text(activity, "按设备类型",
                    com.google.android.material.R.style.TextAppearance_Material3_TitleMedium,
                    themeManager.primaryTextColor());
            content.addView(categoryTitle, topMargin(dp(activity, 20)));
            content.addView(categoryCard(activity, themeManager, model),
                    topMargin(dp(activity, 8)));
        }

        if (!observedAt.isEmpty()) {
            TextView observed = text(activity, "观测时间 · " + observedAt,
                    com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                    themeManager.secondaryTextColor());
            content.addView(observed, topMargin(dp(activity, 16)));
        }
        TextView privacy = text(activity, "仅显示脱敏聚合状态，不会执行设备操作。",
                com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                themeManager.secondaryTextColor());
        content.addView(privacy, topMargin(dp(activity, 6)));

        MaterialButton scope = new MaterialButton(
                activity, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        scope.setText("查看数据范围");
        scope.setMinHeight(dp(activity, 48));
        scope.setOnClickListener(view -> new MaterialAlertDialogBuilder(activity)
                .setTitle("设备状态数据范围")
                .setMessage(DeviceHealthPresentation.DATA_SCOPE)
                .setPositiveButton("知道了", null)
                .show());
        content.addView(scope, topMargin(dp(activity, 12)));

        boolean singleColumnActions = AppResponsiveLayout.usesSingleColumn(
                activity.getResources().getConfiguration().screenWidthDp,
                activity.getResources().getConfiguration().fontScale);
        LinearLayout actions = new LinearLayout(activity);
        actions.setOrientation(singleColumnActions
                ? LinearLayout.VERTICAL : LinearLayout.HORIZONTAL);
        MaterialButton refreshButton = new MaterialButton(
                activity, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        refreshButton.setText("刷新状态");
        refreshButton.setMinHeight(dp(activity, 48));
        refreshButton.setOnClickListener(view -> {
            dialog.dismiss();
            refresh.run();
        });
        actions.addView(refreshButton, singleColumnActions ? matchWidth() : weightedButton());

        MaterialButton triageButton = new MaterialButton(activity);
        triageButton.setText("打开远程排障");
        triageButton.setMinHeight(dp(activity, 48));
        triageButton.setOnClickListener(view -> {
            dialog.dismiss();
            openTriage.run();
        });
        LinearLayout.LayoutParams triageParams = singleColumnActions
                ? topMargin(dp(activity, 8)) : weightedButton();
        if (!singleColumnActions) {
            triageParams.setMargins(dp(activity, 8), 0, 0, 0);
        }
        actions.addView(triageButton, triageParams);

        int footerBaseBottom = dp(activity, 16);
        int navigationBarFallback = navigationBarInset(activity);
        LinearLayout footer = new LinearLayout(activity);
        footer.setPadding(
                horizontalPadding,
                dp(activity, 12),
                horizontalPadding,
                footerBaseBottom + navigationBarFallback);
        footer.addView(actions, matchWidth());
        ViewCompat.setOnApplyWindowInsetsListener(footer, (view, windowInsets) -> {
            Insets navigationBars = windowInsets.getInsets(WindowInsetsCompat.Type.navigationBars());
            Insets displayCutout = windowInsets.getInsets(WindowInsetsCompat.Type.displayCutout());
            int safeBottom = Math.max(
                    navigationBarFallback,
                    Math.max(navigationBars.bottom, displayCutout.bottom));
            view.setPadding(
                    horizontalPadding,
                    dp(activity, 12),
                    horizontalPadding,
                    footerBaseBottom + safeBottom);
            return windowInsets;
        });

        sheetRoot.addView(header, matchWidth());
        sheetRoot.addView(scroll, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));
        sheetRoot.addView(footer, matchWidth());
        dialog.setContentView(sheetRoot, new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT));
        dialog.setOnShowListener(ignored -> {
            View sheet = dialog.findViewById(com.google.android.material.R.id.design_bottom_sheet);
            if (sheet != null) {
                ViewCompat.setAccessibilityPaneTitle(sheet, "检测设备状态详情");
                BottomSheetBehavior<View> behavior = BottomSheetBehavior.from(sheet);
                behavior.setSkipCollapsed(true);
                behavior.setState(BottomSheetBehavior.STATE_EXPANDED);
                ViewCompat.requestApplyInsets(footer);
            }
        });
        dialog.show();
    }

    private static MaterialCardView categoryCard(
            Activity activity,
            ThemeManager themeManager,
            DeviceHealthPresentation.ViewModel model) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (int index = 0; index < model.categories.size(); index++) {
            DeviceHealthPresentation.Category category = model.categories.get(index);
            LinearLayout row = new LinearLayout(activity);
            row.setOrientation(LinearLayout.VERTICAL);
            row.setGravity(Gravity.START);
            row.setMinimumHeight(dp(activity, 64));
            row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 16), dp(activity, 10));
            TextView label = text(activity, category.label,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                    themeManager.primaryTextColor());
            row.addView(label, matchWidth());
            TextView status = text(activity, category.summary,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                    category.attentionRequired
                            ? themeManager.errorColor() : themeManager.secondaryTextColor());
            status.setGravity(Gravity.START);
            row.addView(status, topMargin(dp(activity, 2)));
            row.setContentDescription(category.accessibilityLabel());
            row.setFocusable(true);
            row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
            label.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            status.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            rows.addView(row, matchWidth());
            if (index < model.categories.size() - 1) {
                View divider = new View(activity);
                divider.setBackgroundColor(themeManager.dividerColor());
                LinearLayout.LayoutParams dividerParams = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MATCH_PARENT, 1);
                dividerParams.setMargins(dp(activity, 16), 0, 0, 0);
                rows.addView(divider, dividerParams);
            }
        }

        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(rows, matchCardWidth());
        return card;
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
}

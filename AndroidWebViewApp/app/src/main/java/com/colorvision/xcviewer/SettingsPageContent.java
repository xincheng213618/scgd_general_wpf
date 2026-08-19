package com.colorvision.xcviewer;

import android.app.Activity;
import android.view.Gravity;
import android.view.View;
import android.view.accessibility.AccessibilityNodeInfo;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.Switch;
import android.widget.TextView;

import androidx.core.view.ViewCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.materialswitch.MaterialSwitch;

final class SettingsPageContent {
    private SettingsPageContent() {
    }

    static ScrollView create(
            Activity activity,
            ThemeManager themeManager,
            ViewModel model,
            Handler handler) {
        ScrollView scrollView = new ScrollView(activity);
        scrollView.setFillViewport(false);
        scrollView.setBackgroundColor(themeManager.settingsBackgroundColor());

        LinearLayout content = new LinearLayout(activity);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(0, dp(activity, 4), 0, dp(activity, 28));
        scrollView.addView(content, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT,
                ScrollView.LayoutParams.WRAP_CONTENT));

        LinearLayout connectionSection = section(activity, themeManager);
        addSection(activity, themeManager, content,
                SettingsInformationArchitecture.CONNECTION_SECTION, connectionSection);
        if (model.paired) {
            addRow(activity, themeManager, connectionSection,
                    SettingsInformationArchitecture.COMPUTER_CONNECTIONS,
                    SettingsInformationArchitecture.connectionSupportingText(
                            true, model.computerSummary),
                    view -> handler.onComputerConnections(),
                    true);
            addRow(activity, themeManager, connectionSection,
                    SettingsInformationArchitecture.ADD_COMPUTER,
                    "扫描二维码",
                    view -> handler.onAddComputer(),
                    false);
        } else {
            addRow(activity, themeManager, connectionSection,
                    SettingsInformationArchitecture.CONNECT_COMPUTER,
                    SettingsInformationArchitecture.connectionSupportingText(false, ""),
                    view -> handler.onAddComputer(),
                    false);
        }

        LinearLayout backgroundSection = section(activity, themeManager);
        addSection(activity, themeManager, content,
                SettingsInformationArchitecture.BACKGROUND_SECTION, backgroundSection);
        addWatchRow(activity, themeManager, backgroundSection, model, handler, true);
        if (model.paired) {
            addRow(activity, themeManager, backgroundSection,
                    SettingsInformationArchitecture.OPERATIONS_WATCH_STATUS,
                    model.watchRuntimeStatus,
                    view -> handler.onWatchStatus(),
                    true);
        }
        addRow(activity, themeManager, backgroundSection,
                SettingsInformationArchitecture.NOTIFICATION_PERMISSION,
                model.notificationStatus,
                view -> handler.onNotificationPermission(),
                false);

        LinearLayout appSection = section(activity, themeManager);
        addSection(activity, themeManager, content,
                SettingsInformationArchitecture.APPLICATION_SECTION, appSection);
        addRow(activity, themeManager, appSection,
                SettingsInformationArchitecture.THEME_MODE,
                model.themeMode,
                view -> handler.onThemeMode(),
                true);
        addRow(activity, themeManager, appSection,
                SettingsInformationArchitecture.APP_UPDATE,
                model.appUpdateStatus,
                view -> handler.onAppUpdate(),
                false);

        return scrollView;
    }

    private static LinearLayout section(Activity activity, ThemeManager themeManager) {
        LinearLayout section = new LinearLayout(activity);
        section.setOrientation(LinearLayout.VERTICAL);
        section.setBackgroundColor(themeManager.cardBackgroundColor());
        return section;
    }

    private static void addSection(
            Activity activity,
            ThemeManager themeManager,
            LinearLayout content,
            String heading,
            LinearLayout section) {
        TextView headingView = new TextView(activity);
        headingView.setText(heading);
        TextViewCompat.setTextAppearance(
                headingView,
                com.google.android.material.R.style.TextAppearance_Material3_LabelLarge);
        headingView.setTextColor(themeManager.primaryColor());
        headingView.setPadding(dp(activity, 22), dp(activity, 18), dp(activity, 22), dp(activity, 8));
        ViewCompat.setAccessibilityHeading(headingView, true);
        content.addView(headingView, matchWidth());
        content.addView(section, matchWidth());
    }

    private static void addRow(
            Activity activity,
            ThemeManager themeManager,
            LinearLayout parent,
            String label,
            String value,
            View.OnClickListener listener,
            boolean showDivider) {
        boolean supportingTextLayout = AppResponsiveLayout.usesSingleColumn(
                activity.getResources().getConfiguration().screenWidthDp,
                activity.getResources().getConfiguration().fontScale);
        LinearLayout row = new LinearLayout(activity);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(dp(activity, 22), supportingTextLayout ? dp(activity, 10) : 0,
                dp(activity, 18), supportingTextLayout ? dp(activity, 10) : 0);
        row.setMinimumHeight(dp(activity, supportingTextLayout ? 72 : 58));
        row.setBackgroundColor(themeManager.cardBackgroundColor());
        if (listener != null) {
            row.setOnClickListener(listener);
            row.setFocusable(true);
            row.setContentDescription(SettingsRowAccessibility.contentDescription(label, value));
        }

        TextView labelView = text(
                activity,
                label,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                themeManager.primaryTextColor());
        TextView valueView = text(
                activity,
                value == null ? "" : value,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.mutedTextColor());
        valueView.setGravity(supportingTextLayout ? Gravity.START : Gravity.END);
        valueView.setSingleLine(false);
        if (listener != null) {
            labelView.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            valueView.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        }

        if (supportingTextLayout) {
            LinearLayout labels = new LinearLayout(activity);
            labels.setOrientation(LinearLayout.VERTICAL);
            labels.addView(labelView, matchWidth());
            LinearLayout.LayoutParams valueParams = matchWidth();
            valueParams.setMargins(0, dp(activity, 2), 0, 0);
            labels.addView(valueView, valueParams);
            row.addView(labels, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
        } else {
            row.addView(labelView, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
            row.addView(valueView, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1.35f));
        }

        if (listener != null) {
            ImageView arrow = new ImageView(activity);
            arrow.setImageResource(R.drawable.ic_chevron_right_24);
            arrow.setColorFilter(themeManager.inactiveTabColor());
            arrow.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            row.addView(arrow, new LinearLayout.LayoutParams(dp(activity, 24), dp(activity, 24)));
        }
        parent.addView(row, matchWidth());
        if (showDivider) {
            addDivider(activity, themeManager, parent);
        }
    }

    private static void addWatchRow(
            Activity activity,
            ThemeManager themeManager,
            LinearLayout parent,
            ViewModel model,
            Handler handler,
            boolean showDivider) {
        boolean stackSupportingText = AppResponsiveLayout.usesStackedControlRow(
                activity.getResources().getConfiguration().screenWidthDp,
                activity.getResources().getConfiguration().fontScale);
        LinearLayout row = new LinearLayout(activity);
        row.setOrientation(stackSupportingText
                ? LinearLayout.VERTICAL : LinearLayout.HORIZONTAL);
        row.setGravity(stackSupportingText ? Gravity.START : Gravity.CENTER_VERTICAL);
        row.setPadding(dp(activity, 22), stackSupportingText ? dp(activity, 10) : 0,
                dp(activity, 18), stackSupportingText ? dp(activity, 10) : 0);
        row.setMinimumHeight(dp(activity, 64));
        row.setBackgroundColor(themeManager.cardBackgroundColor());

        TextView title = text(
                activity,
                SettingsInformationArchitecture.OPERATIONS_WATCH,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                themeManager.primaryTextColor());
        TextView status = text(
                activity,
                model.watchStatus,
                com.google.android.material.R.style.TextAppearance_Material3_BodySmall,
                themeManager.mutedTextColor());
        title.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        status.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);

        MaterialSwitch toggle = new MaterialSwitch(activity);
        toggle.setChecked(model.watchEnabled);
        toggle.setClickable(false);
        toggle.setFocusable(false);
        toggle.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        row.setFocusable(true);
        row.setContentDescription(SettingsRowAccessibility.contentDescription(
                SettingsInformationArchitecture.OPERATIONS_WATCH, model.watchStatus));
        row.setAccessibilityDelegate(new View.AccessibilityDelegate() {
            @Override
            public void onInitializeAccessibilityNodeInfo(
                    View host, AccessibilityNodeInfo info) {
                super.onInitializeAccessibilityNodeInfo(host, info);
                info.setClassName(Switch.class.getName());
                info.setCheckable(true);
                info.setChecked(toggle.isChecked());
            }
        });
        toggle.setOnCheckedChangeListener((button, checked) -> {
            String updatedStatus = handler.onWatchChanged(checked);
            status.setText(updatedStatus);
            row.setContentDescription(SettingsRowAccessibility.contentDescription(
                    SettingsInformationArchitecture.OPERATIONS_WATCH, updatedStatus));
        });
        if (stackSupportingText) {
            LinearLayout headline = new LinearLayout(activity);
            headline.setOrientation(LinearLayout.HORIZONTAL);
            headline.setGravity(Gravity.CENTER_VERTICAL);
            headline.addView(title, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
            headline.addView(toggle, new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.WRAP_CONTENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT));
            row.addView(headline, matchWidth());
            LinearLayout.LayoutParams statusParams = matchWidth();
            statusParams.setMargins(0, dp(activity, 2), 0, 0);
            row.addView(status, statusParams);
        } else {
            LinearLayout labels = new LinearLayout(activity);
            labels.setOrientation(LinearLayout.VERTICAL);
            labels.setGravity(Gravity.CENTER_VERTICAL);
            labels.addView(title, matchWidth());
            labels.addView(status, matchWidth());
            row.addView(labels, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
            row.addView(toggle, new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.WRAP_CONTENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT));
        }
        row.setOnClickListener(view -> toggle.setChecked(!toggle.isChecked()));
        parent.addView(row, matchWidth());
        if (showDivider) {
            addDivider(activity, themeManager, parent);
        }
    }

    private static TextView text(
            Activity activity,
            String value,
            int appearance,
            int color) {
        TextView view = new TextView(activity);
        view.setText(value);
        TextViewCompat.setTextAppearance(view, appearance);
        view.setTextColor(color);
        return view;
    }

    private static void addDivider(
            Activity activity,
            ThemeManager themeManager,
            LinearLayout parent) {
        View divider = new View(activity);
        divider.setBackgroundColor(themeManager.dividerColor());
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 1);
        params.setMargins(dp(activity, 22), 0, 0, 0);
        parent.addView(divider, params);
    }

    private static LinearLayout.LayoutParams matchWidth() {
        return new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
    }

    private static int dp(Activity activity, int value) {
        return Math.round(value * activity.getResources().getDisplayMetrics().density);
    }

    static final class ViewModel {
        final boolean paired;
        final String computerSummary;
        final boolean watchEnabled;
        final String watchStatus;
        final String watchRuntimeStatus;
        final String notificationStatus;
        final String themeMode;
        final String appUpdateStatus;

        ViewModel(
                boolean paired,
                String computerSummary,
                boolean watchEnabled,
                String watchStatus,
                String watchRuntimeStatus,
                String notificationStatus,
                String themeMode,
                String appUpdateStatus) {
            this.paired = paired;
            this.computerSummary = computerSummary;
            this.watchEnabled = watchEnabled;
            this.watchStatus = watchStatus;
            this.watchRuntimeStatus = watchRuntimeStatus;
            this.notificationStatus = notificationStatus;
            this.themeMode = themeMode;
            this.appUpdateStatus = appUpdateStatus;
        }
    }

    interface Handler {
        void onComputerConnections();

        void onAddComputer();

        String onWatchChanged(boolean enabled);

        void onWatchStatus();

        void onNotificationPermission();

        void onThemeMode();

        void onAppUpdate();
    }
}

package com.colorvision.xcviewer;

import android.app.Activity;
import android.content.res.ColorStateList;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.accessibility.AccessibilityNodeInfo;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.Switch;
import android.widget.TextView;

import androidx.core.widget.TextViewCompat;

import com.google.android.material.card.MaterialCardView;
import com.google.android.material.materialswitch.MaterialSwitch;

import java.util.List;

final class OperationsConnectionContent {
    private OperationsConnectionContent() {
    }

    static SummaryView createSummary(
            Activity activity,
            ThemeManager themeManager,
            OperationsConnectionPresentation.ViewModel model) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        TextView computer = addValueRow(
                activity, themeManager, rows, "当前电脑", model.computerLabel, true);
        TextView activeChannel = addValueRow(
                activity, themeManager, rows, "当前通道", model.activeChannelLabel, true);
        TextView preferredChannel = addValueRow(
                activity, themeManager, rows, "首选通道", model.preferredChannelLabel, true);
        TextView pairedComputers = addValueRow(
                activity, themeManager, rows, "已配对电脑", model.pairedComputersLabel, false);

        MaterialCardView card = card(activity, themeManager, rows);
        return new SummaryView(
                card, computer, activeChannel, preferredChannel, pairedComputers);
    }

    static MaterialCardView createManagement(
            Activity activity,
            ThemeManager themeManager,
            int maximumProfiles,
            Handler handler) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        addActionRow(
                activity,
                themeManager,
                rows,
                "命名当前电脑",
                "修改手机上的显示名称",
                handler::onRenameComputer,
                true);
        addActionRow(
                activity,
                themeManager,
                rows,
                "扫描并添加电脑",
                "最多可安全配对 " + Math.max(0, maximumProfiles) + " 台电脑",
                handler::onAddComputer,
                true);
        addActionRow(
                activity,
                themeManager,
                rows,
                "配对码在哪里？",
                "查看电脑端生成配对码的位置",
                handler::onPairingHelp,
                false);
        return card(activity, themeManager, rows);
    }

    static MaterialCardView createAttentionNotifications(
            Activity activity,
            ThemeManager themeManager,
            List<OperationsProfileRegistry.Profile> profiles,
            String activeHostId,
            AttentionNotificationHandler handler) {
        LinearLayout rows = new LinearLayout(activity);
        rows.setOrientation(LinearLayout.VERTICAL);
        int usableCount = 0;
        for (OperationsProfileRegistry.Profile profile : profiles) {
            usableCount += profile.revoked ? 0 : 1;
        }
        int usableIndex = 0;
        for (int index = 0; index < profiles.size(); index++) {
            OperationsProfileRegistry.Profile profile = profiles.get(index);
            if (profile.revoked) {
                continue;
            }
            usableIndex++;
            String label = profile.label.isEmpty() ? "电脑 " + (index + 1) : profile.label;
            if (profile.hostId.equals(activeHostId)) {
                label += "（当前）";
            }
            addAttentionNotificationRow(
                    activity,
                    themeManager,
                    rows,
                    label,
                    profile,
                    handler,
                    usableIndex < usableCount);
        }
        return card(activity, themeManager, rows);
    }

    private static void addAttentionNotificationRow(
            Activity activity,
            ThemeManager themeManager,
            LinearLayout parent,
            String label,
            OperationsProfileRegistry.Profile profile,
            AttentionNotificationHandler handler,
            boolean showDivider) {
        boolean stackSupportingText = AppResponsiveLayout.usesStackedControlRow(
                activity.getResources().getConfiguration().screenWidthDp,
                activity.getResources().getConfiguration().fontScale);
        LinearLayout row = new LinearLayout(activity);
        row.setOrientation(stackSupportingText
                ? LinearLayout.VERTICAL : LinearLayout.HORIZONTAL);
        row.setGravity(stackSupportingText ? Gravity.START : Gravity.CENTER_VERTICAL);
        row.setMinimumHeight(dp(activity, 72));
        row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 12), dp(activity, 10));
        applySelectableBackground(activity, row);

        TextView title = text(
                activity,
                label,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                themeManager.primaryTextColor());
        TextView supporting = text(
                activity,
                attentionNotificationSummary(profile.attentionNotificationsEnabled),
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.mutedTextColor());
        title.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        supporting.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);

        MaterialSwitch toggle = new MaterialSwitch(activity);
        toggle.setChecked(profile.attentionNotificationsEnabled);
        toggle.setClickable(false);
        toggle.setFocusable(false);
        toggle.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        row.setClickable(true);
        row.setFocusable(true);
        row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
        updateAttentionNotificationAccessibility(
                row, label, profile.attentionNotificationsEnabled);
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

        if (stackSupportingText) {
            LinearLayout headline = new LinearLayout(activity);
            headline.setOrientation(LinearLayout.HORIZONTAL);
            headline.setGravity(Gravity.CENTER_VERTICAL);
            headline.addView(title, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
            headline.addView(toggle, new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.WRAP_CONTENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT));
            row.addView(headline, matchWidth());
            row.addView(supporting, topMargin(dp(activity, 2)));
        } else {
            LinearLayout labels = new LinearLayout(activity);
            labels.setOrientation(LinearLayout.VERTICAL);
            labels.addView(title, matchWidth());
            labels.addView(supporting, topMargin(dp(activity, 2)));
            row.addView(labels, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
            row.addView(toggle, new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.WRAP_CONTENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT));
        }

        boolean[] binding = {false};
        toggle.setOnCheckedChangeListener((button, requested) -> {
            if (binding[0]) {
                return;
            }
            boolean actual = handler.onAttentionNotificationChanged(
                    profile.hostId, requested);
            if (actual != requested) {
                binding[0] = true;
                toggle.setChecked(actual);
                binding[0] = false;
            }
            supporting.setText(attentionNotificationSummary(actual));
            updateAttentionNotificationAccessibility(row, label, actual);
        });
        row.setOnClickListener(view -> toggle.setChecked(!toggle.isChecked()));
        parent.addView(row, matchWidth());
        if (showDivider) {
            addDivider(activity, themeManager, parent);
        }
    }

    private static void updateAttentionNotificationAccessibility(
            View row, String label, boolean enabled) {
        row.setContentDescription(SettingsRowAccessibility.contentDescription(
                label + "异常提醒", attentionNotificationSummary(enabled)));
    }

    private static String attentionNotificationSummary(boolean enabled) {
        return enabled
                ? "允许提醒 · 同类新证据出现时生效"
                : "已暂停 · 状态记录与手动检查保留";
    }

    private static TextView addValueRow(
            Activity activity,
            ThemeManager themeManager,
            LinearLayout parent,
            String label,
            String value,
            boolean showDivider) {
        LinearLayout row = new LinearLayout(activity);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setMinimumHeight(dp(activity, 56));
        row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 16), dp(activity, 10));

        TextView labelView = text(
                activity,
                label,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                themeManager.primaryTextColor());
        TextView valueView = text(
                activity,
                value,
                com.google.android.material.R.style.TextAppearance_Material3_LabelLarge,
                themeManager.secondaryTextColor());
        valueView.setGravity(Gravity.END);
        valueView.setMaxLines(2);
        row.addView(labelView, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
        row.addView(valueView, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1.35f));
        updateValueRowAccessibility(row, label, value);
        labelView.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        valueView.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        parent.addView(row, matchWidth());
        if (showDivider) {
            addDivider(activity, themeManager, parent);
        }
        return valueView;
    }

    private static void addActionRow(
            Activity activity,
            ThemeManager themeManager,
            LinearLayout parent,
            String title,
            String supportingText,
            Runnable action,
            boolean showDivider) {
        LinearLayout row = new LinearLayout(activity);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setMinimumHeight(dp(activity, 72));
        row.setPadding(dp(activity, 16), dp(activity, 10), dp(activity, 12), dp(activity, 10));

        LinearLayout copy = new LinearLayout(activity);
        copy.setOrientation(LinearLayout.VERTICAL);
        TextView titleView = text(
                activity,
                title,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge,
                themeManager.primaryTextColor());
        TextView supportingView = text(
                activity,
                supportingText,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium,
                themeManager.mutedTextColor());
        copy.addView(titleView, matchWidth());
        copy.addView(supportingView, topMargin(dp(activity, 2)));
        row.addView(copy, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));

        ImageView chevron = new ImageView(activity);
        chevron.setImageResource(R.drawable.ic_chevron_right_24);
        chevron.setImageTintList(ColorStateList.valueOf(themeManager.secondaryTextColor()));
        chevron.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        LinearLayout.LayoutParams chevronParams = new LinearLayout.LayoutParams(
                dp(activity, 24), dp(activity, 24));
        chevronParams.setMargins(dp(activity, 12), 0, 0, 0);
        row.addView(chevron, chevronParams);

        row.setContentDescription(SettingsRowAccessibility.contentDescription(
                title, supportingText));
        row.setClickable(true);
        row.setFocusable(true);
        row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
        applySelectableBackground(activity, row);
        row.setOnClickListener(view -> action.run());
        titleView.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        supportingView.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        parent.addView(row, matchWidth());
        if (showDivider) {
            addDivider(activity, themeManager, parent);
        }
    }

    private static MaterialCardView card(
            Activity activity, ThemeManager themeManager, View content) {
        MaterialCardView card = new MaterialCardView(activity);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(content, new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT));
        return card;
    }

    private static void updateValueRowAccessibility(View row, String label, String value) {
        row.setContentDescription(SettingsRowAccessibility.contentDescription(label, value));
        row.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
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

    private static void addDivider(
            Activity activity,
            ThemeManager themeManager,
            LinearLayout parent) {
        View divider = new View(activity);
        divider.setBackgroundColor(themeManager.dividerColor());
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 1);
        params.setMargins(dp(activity, 16), 0, 0, 0);
        parent.addView(divider, params);
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

    private static int dp(Activity activity, int value) {
        return Math.round(value * activity.getResources().getDisplayMetrics().density);
    }

    static final class SummaryView {
        final MaterialCardView view;
        private final TextView computer;
        private final TextView activeChannel;
        private final TextView preferredChannel;
        private final TextView pairedComputers;

        SummaryView(
                MaterialCardView view,
                TextView computer,
                TextView activeChannel,
                TextView preferredChannel,
                TextView pairedComputers) {
            this.view = view;
            this.computer = computer;
            this.activeChannel = activeChannel;
            this.preferredChannel = preferredChannel;
            this.pairedComputers = pairedComputers;
        }

        void render(OperationsConnectionPresentation.ViewModel model) {
            updateValue(computer, "当前电脑", model.computerLabel);
            updateValue(activeChannel, "当前通道", model.activeChannelLabel);
            updateValue(preferredChannel, "首选通道", model.preferredChannelLabel);
            updateValue(pairedComputers, "已配对电脑", model.pairedComputersLabel);
        }

        private static void updateValue(TextView valueView, String label, String value) {
            valueView.setText(value);
            View row = (View) valueView.getParent();
            updateValueRowAccessibility(row, label, value);
        }
    }

    interface Handler {
        void onRenameComputer();

        void onAddComputer();

        void onPairingHelp();
    }

    interface AttentionNotificationHandler {
        boolean onAttentionNotificationChanged(String hostId, boolean enabled);
    }
}

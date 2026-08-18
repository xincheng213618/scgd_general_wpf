package com.colorvision.xcviewer;

import android.Manifest;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.content.ClipData;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.Settings;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.core.content.FileProvider;
import androidx.core.app.NotificationManagerCompat;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.bottomnavigation.BottomNavigationView;
import com.google.android.material.button.MaterialButton;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.progressindicator.LinearProgressIndicator;
import com.google.android.material.materialswitch.MaterialSwitch;

import java.io.File;
import java.net.ConnectException;
import java.net.SocketTimeoutException;
import java.net.UnknownHostException;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class MainActivity extends AppCompatActivity {
    static final String EXTRA_START_TAB = "start_tab";
    static final String EXTRA_FROM_OPERATIONS = "from_operations";
    private static final String STATE_CURRENT_TAB = "current_tab";
    private static final String STATE_OPENED_FROM_OPERATIONS = "opened_from_operations";
    private static final int REQUEST_QR_SCAN = 1001;
    private static final int REQUEST_INSTALL_PERMISSION = 1004;
    private static final int REQUEST_NOTIFICATION_PERMISSION = 1005;
    private static final int NAV_OPERATIONS = 2001;
    private static final int NAV_SETTINGS = 2002;
    static final int TAB_OPERATIONS = 0;
    static final int TAB_SETTINGS = 2;

    private FrameLayout root;
    private LinearLayout appShell;
    private FrameLayout setupContainer;
    private ProgressBar progressBar;
    private AppPreferences appPreferences;
    private ThemeManager themeManager;
    private TextView headerTitle;
    private TextView headerSubtitle;
    private BottomNavigationView bottomNavigation;
    private boolean updatingBottomNavigation;
    private boolean openedFromOperations;
    private int currentTab = TAB_OPERATIONS;
    private boolean cameraPermissionGranted;
    private String lastNotificationPermissionStatus = "";
    private final RuntimePermissionDialogState notificationPermissionDialogState =
            new RuntimePermissionDialogState();
    private final ExecutorService appUpdateExecutor = Executors.newSingleThreadExecutor();
    private boolean appUpdateInFlight;
    private File pendingInstallFile;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        WindowCompat.setDecorFitsSystemWindows(getWindow(), false);

        appPreferences = new AppPreferences(this);
        OperationsWatchService.start(this);
        cameraPermissionGranted = hasCameraPermission();
        lastNotificationPermissionStatus = notificationPermissionStatus();
        int requestedTab = consumeStartTab(getIntent());
        boolean restoring = savedInstanceState != null;
        int restoredTab = restoring
                ? savedInstanceState.getInt(STATE_CURRENT_TAB, -1) : -1;
        int startTab = AppNavigationPolicy.resolveCreationTab(
                restoring, restoredTab, requestedTab, TAB_OPERATIONS, TAB_SETTINGS);
        openedFromOperations = restoring
                ? savedInstanceState.getBoolean(
                        STATE_OPENED_FROM_OPERATIONS,
                        getIntent().getBooleanExtra(EXTRA_FROM_OPERATIONS, false))
                : getIntent().getBooleanExtra(EXTRA_FROM_OPERATIONS, false);
        if (openedFromOperations && startTab == TAB_SETTINGS) {
            AppScreenMotion.configureSettingsActivity(this);
        }
        if (AppNavigationPolicy.shouldOpenOperationsDirectly(
                appPreferences.hasOperationsProfile(), startTab == TAB_OPERATIONS)) {
            openOperationsDirectly();
            return;
        }
        themeManager = new ThemeManager(this, appPreferences);
        themeManager.applySystemBars(this);

        root = new FrameLayout(this);
        root.setBackgroundColor(shellBackgroundColor());
        setupContainer = new FrameLayout(this);
        appShell = createAppShell();
        progressBar = new LinearProgressIndicator(this);

        root.addView(appShell, matchParentParams());
        root.addView(progressBar, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.WRAP_CONTENT));

        applyTopSystemBarInset(root);
        setContentView(root);
        ViewCompat.requestApplyInsets(root);

        showInitialTab(startTab);
    }

    private boolean hasCameraPermission() {
        return checkSelfPermission(Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED;
    }

    private void showThemeDialog() {
        themeManager.showThemeDialog(this, TAB_SETTINGS);
    }

    private String getThemeModeLabel() {
        return themeManager.getThemeModeLabel();
    }

    private int shellBackgroundColor() {
        return themeManager.shellBackgroundColor();
    }

    private int pageBackgroundColor() {
        return themeManager.pageBackgroundColor();
    }

    private int settingsBackgroundColor() {
        return themeManager.settingsBackgroundColor();
    }

    private int cardBackgroundColor() {
        return themeManager.cardBackgroundColor();
    }

    private int bottomNavBackgroundColor() {
        return themeManager.bottomNavBackgroundColor();
    }

    private int primaryTextColor() {
        return themeManager.primaryTextColor();
    }

    private int secondaryTextColor() {
        return themeManager.secondaryTextColor();
    }

    private int mutedTextColor() {
        return themeManager.mutedTextColor();
    }

    private int inactiveTabColor() {
        return themeManager.inactiveTabColor();
    }

    private int dividerColor() {
        return themeManager.dividerColor();
    }

    private LinearLayout createAppShell() {
        LinearLayout shell = new LinearLayout(this);
        shell.setOrientation(LinearLayout.VERTICAL);
        shell.setBackgroundColor(shellBackgroundColor());
        shell.addView(createTopBar(), new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));

        shell.addView(setupContainer, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                0,
                1));

        bottomNavigation = createBottomNav();
        shell.addView(bottomNavigation, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));
        return shell;
    }

    private LinearLayout createTopBar() {
        LinearLayout bar = new LinearLayout(this);
        bar.setOrientation(LinearLayout.HORIZONTAL);
        bar.setGravity(Gravity.CENTER_VERTICAL);
        bar.setMinimumHeight(dp(48));
        bar.setPadding(dp(18), dp(2), dp(14), dp(2));
        bar.setBackgroundColor(shellBackgroundColor());

        LinearLayout titleBlock = new LinearLayout(this);
        titleBlock.setOrientation(LinearLayout.VERTICAL);
        titleBlock.setGravity(Gravity.CENTER_VERTICAL);
        bar.addView(titleBlock, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));

        headerTitle = new TextView(this);
        headerTitle.setText("ColorVision");
        TextViewCompat.setTextAppearance(headerTitle, com.google.android.material.R.style.TextAppearance_Material3_TitleLarge);
        headerTitle.setTextColor(primaryTextColor());
        titleBlock.addView(headerTitle, matchWidthWrapParams());

        headerSubtitle = new TextView(this);
        headerSubtitle.setText("安全运维伴侣");
        TextViewCompat.setTextAppearance(headerSubtitle, com.google.android.material.R.style.TextAppearance_Material3_BodySmall);
        headerSubtitle.setTextColor(secondaryTextColor());
        titleBlock.addView(headerSubtitle, matchWidthWrapParams());

        return bar;
    }

    private BottomNavigationView createBottomNav() {
        BottomNavigationView nav = new BottomNavigationView(this);
        nav.setBackgroundColor(bottomNavBackgroundColor());
        nav.setLabelVisibilityMode(BottomNavigationView.LABEL_VISIBILITY_LABELED);
        nav.getMenu().add(0, NAV_OPERATIONS, 0, "运维").setIcon(R.drawable.ic_devices_24);
        nav.getMenu().add(0, NAV_SETTINGS, 1, "设置").setIcon(R.drawable.ic_settings_24);
        nav.setOnItemSelectedListener(item -> {
            if (updatingBottomNavigation) {
                return true;
            }
            if (item.getItemId() == NAV_OPERATIONS) {
                showOperationsLanding();
                return true;
            }
            if (item.getItemId() == NAV_SETTINGS) {
                showProfileView();
                return true;
            }
            return false;
        });
        return nav;
    }

    private void selectTab(int tab) {
        currentTab = tab;
        if (bottomNavigation == null) {
            return;
        }
        int itemId = tab == TAB_SETTINGS ? NAV_SETTINGS : NAV_OPERATIONS;
        if (bottomNavigation.getSelectedItemId() != itemId) {
            updatingBottomNavigation = true;
            bottomNavigation.setSelectedItemId(itemId);
            updatingBottomNavigation = false;
        }
    }

    private int consumeStartTab(Intent intent) {
        int requestedTab = intent.getIntExtra(EXTRA_START_TAB, -1);
        intent.removeExtra(EXTRA_START_TAB);
        return AppNavigationPolicy.normalizeStartTab(
                requestedTab,
                appPreferences.consumeStartTab(TAB_OPERATIONS),
                TAB_OPERATIONS,
                TAB_SETTINGS);
    }

    private void showInitialTab(int startTab) {
        if (startTab == TAB_SETTINGS) {
            showProfileView();
            return;
        }
        showOperationsLanding();
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        int startTab = consumeStartTab(intent);
        openedFromOperations = intent.getBooleanExtra(EXTRA_FROM_OPERATIONS, false);
        if (openedFromOperations && startTab == TAB_SETTINGS) {
            AppScreenMotion.configureSettingsActivity(this);
        }
        if (AppNavigationPolicy.shouldOpenOperationsDirectly(
                appPreferences.hasOperationsProfile(), startTab == TAB_OPERATIONS)) {
            openOperationsDirectly();
            return;
        }
        showInitialTab(startTab);
    }

    private void showOperationsLanding() {
        int direction = AppScreenMotion.directionBetween(
                currentTab,
                TAB_OPERATIONS,
                TAB_OPERATIONS,
                TAB_SETTINGS);
        if (appPreferences.hasOperationsProfile()) {
            if (openedFromOperations
                    && direction == AppScreenMotion.DIRECTION_BACKWARD) {
                AppScreenMotion.finishBackward(this);
                return;
            }
            openOperations();
            return;
        }
        AppScreenMotion.beginContentTransition(setupContainer, direction);
        selectTab(TAB_OPERATIONS);
        headerTitle.setText("现场运维");
        headerSubtitle.setText("扫描电脑端安全配对码");
        setupContainer.removeAllViews();
        setupContainer.addView(createOperationsLandingContent(), matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        progressBar.setVisibility(View.GONE);
    }

    private ScrollView createOperationsLandingContent() {
        ScrollView scrollView = new ScrollView(this);
        scrollView.setFillViewport(true);
        scrollView.setBackgroundColor(pageBackgroundColor());

        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(18), dp(18), dp(18), dp(24));
        scrollView.addView(content, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT,
                ScrollView.LayoutParams.WRAP_CONTENT));

        LinearLayout operationsCard = makeCard();
        content.addView(operationsCard, fullWidthCardParams());
        operationsCard.addView(makeTitle("连接运维电脑", 22), matchWidthWrapParams());

        TextView status = makeBodyText("扫描电脑端“设置 > 局域网控制”中的短时安全配对码。完成一次配对后，运维伴侣会成为首屏并持续守护安全连接。");
        status.setPadding(0, dp(8), 0, dp(4));
        operationsCard.addView(status, matchWidthWrapParams());

        Button operationsButton = makePrimaryButton("扫描并连接电脑");
        operationsButton.setOnClickListener(v -> openOperations());
        operationsCard.addView(operationsButton, fullWidthButtonParams());

        Button helpButton = makeSecondaryButton("配对码在哪里？");
        helpButton.setOnClickListener(v -> PairingHelpDialog.show(this, this::startQrScan));
        operationsCard.addView(helpButton, fullWidthButtonParams());

        return scrollView;
    }

    private void showProfileView() {
        int direction = AppScreenMotion.directionBetween(
                currentTab,
                TAB_SETTINGS,
                TAB_OPERATIONS,
                TAB_SETTINGS);
        AppScreenMotion.beginContentTransition(setupContainer, direction);
        selectTab(TAB_SETTINGS);
        headerTitle.setText("设置");
        headerSubtitle.setText("安全配对与应用信息");
        setupContainer.removeAllViews();
        setupContainer.addView(createProfileContent(), matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        progressBar.setVisibility(View.GONE);
    }

    private ScrollView createProfileContent() {
        ScrollView scrollView = new ScrollView(this);
        scrollView.setFillViewport(false);
        scrollView.setBackgroundColor(settingsBackgroundColor());

        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(0, dp(4), 0, dp(28));
        scrollView.addView(content, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT,
                ScrollView.LayoutParams.WRAP_CONTENT));

        LinearLayout connectionSection = makeSettingsSection();
        addSettingsSection(content, SettingsInformationArchitecture.CONNECTION_SECTION,
                connectionSection);
        boolean paired = appPreferences.hasOperationsProfile();
        int pairedComputerCount = appPreferences.getOperationsProfileCount();
        addSettingsRow(connectionSection, SettingsInformationArchitecture.OPERATIONS,
                paired ? "当前 " + appPreferences.getActiveOperationsProfileLabel()
                        + " · 共 " + pairedComputerCount + " 台" : "尚未配对",
                v -> openOperations(), true);
        addSettingsRow(connectionSection, SettingsInformationArchitecture.SECURE_CHANNEL,
                paired ? "设备密钥 + TLS 证书固定" : "等待安全配对",
                null, true);
        addSettingsRow(connectionSection,
                paired ? SettingsInformationArchitecture.ADD_COMPUTER
                        : SettingsInformationArchitecture.CONNECT_COMPUTER,
                "扫描二维码", v -> startQrScan(), false);

        LinearLayout backgroundSection = makeSettingsSection();
        addSettingsSection(content, SettingsInformationArchitecture.BACKGROUND_SECTION,
                backgroundSection);
        addOperationsWatchSwitchRow(backgroundSection, paired, true);
        addSettingsRow(backgroundSection,
                SettingsInformationArchitecture.NOTIFICATION_PERMISSION,
                notificationPermissionStatus(), v -> manageNotificationPermission(), false);

        LinearLayout permissionSection = makeSettingsSection();
        addSettingsSection(content, SettingsInformationArchitecture.PERMISSION_SECTION,
                permissionSection);
        addSettingsRow(permissionSection, SettingsInformationArchitecture.CAMERA_PERMISSION,
                cameraPermissionStatus(), v -> startQrScan(), false);

        LinearLayout appSection = makeSettingsSection();
        addSettingsSection(content, SettingsInformationArchitecture.APPLICATION_SECTION,
                appSection);
        addSettingsRow(appSection, SettingsInformationArchitecture.THEME_MODE,
                getThemeModeLabel(), v -> showThemeDialog(), true);
        addSettingsRow(appSection, SettingsInformationArchitecture.APP_UPDATE,
                "当前 " + getAppVersionName() + " · 签名校验", v -> checkForAppUpdate(), false);

        return scrollView;
    }

    private String getAppVersionName() {
        try {
            return getPackageManager().getPackageInfo(getPackageName(), 0).versionName;
        } catch (Exception ex) {
            return "--";
        }
    }

    private LinearLayout makeSettingsSection() {
        LinearLayout section = new LinearLayout(this);
        section.setOrientation(LinearLayout.VERTICAL);
        section.setBackgroundColor(cardBackgroundColor());
        return section;
    }

    private void addSettingsSection(LinearLayout content, String heading, LinearLayout section) {
        TextView headingView = new TextView(this);
        headingView.setText(heading);
        TextViewCompat.setTextAppearance(
                headingView,
                com.google.android.material.R.style.TextAppearance_Material3_LabelLarge);
        headingView.setTextColor(themeManager.primaryColor());
        headingView.setPadding(dp(22), dp(18), dp(22), dp(8));
        ViewCompat.setAccessibilityHeading(headingView, true);
        content.addView(headingView, matchWidthWrapParams());
        content.addView(section, matchWidthWrapParams());
    }

    private void addSettingsRow(
            LinearLayout parent,
            String label,
            String value,
            View.OnClickListener listener,
            boolean showDivider) {
        boolean supportingTextLayout = AppResponsiveLayout.usesSingleColumn(
                getResources().getConfiguration().fontScale);
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(dp(22), supportingTextLayout ? dp(10) : 0,
                dp(18), supportingTextLayout ? dp(10) : 0);
        row.setMinimumHeight(dp(supportingTextLayout ? 72 : 58));
        row.setBackgroundColor(cardBackgroundColor());
        if (listener != null) {
            row.setOnClickListener(listener);
            row.setFocusable(true);
            row.setContentDescription(SettingsRowAccessibility.contentDescription(label, value));
        }

        TextView labelView = new TextView(this);
        labelView.setText(label);
        TextViewCompat.setTextAppearance(labelView, com.google.android.material.R.style.TextAppearance_Material3_BodyLarge);
        labelView.setTextColor(primaryTextColor());

        TextView valueView = new TextView(this);
        valueView.setText(value == null ? "" : value);
        TextViewCompat.setTextAppearance(valueView, com.google.android.material.R.style.TextAppearance_Material3_BodyMedium);
        valueView.setTextColor(mutedTextColor());
        valueView.setGravity(supportingTextLayout ? Gravity.START : Gravity.END);
        valueView.setSingleLine(false);
        if (listener != null) {
            labelView.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            valueView.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        }

        if (supportingTextLayout) {
            LinearLayout text = new LinearLayout(this);
            text.setOrientation(LinearLayout.VERTICAL);
            text.addView(labelView, matchWidthWrapParams());
            LinearLayout.LayoutParams valueParams = matchWidthWrapParams();
            valueParams.setMargins(0, dp(2), 0, 0);
            text.addView(valueView, valueParams);
            row.addView(text, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
        } else {
            row.addView(labelView, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
            row.addView(valueView, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1.35f));
        }

        if (listener != null) {
            ImageView arrow = new ImageView(this);
            arrow.setImageResource(R.drawable.ic_chevron_right_24);
            arrow.setColorFilter(inactiveTabColor());
            arrow.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            row.addView(arrow, new LinearLayout.LayoutParams(dp(24), dp(24)));
        }

        parent.addView(row, matchWidthWrapParams());

        if (showDivider) {
            View divider = new View(this);
            divider.setBackgroundColor(dividerColor());
            LinearLayout.LayoutParams dividerParams = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT,
                    1);
            dividerParams.setMargins(dp(22), 0, 0, 0);
            parent.addView(divider, dividerParams);
        }
    }

    private void startQrScan() {
        if (hasCameraPermission()
                || shouldShowRequestPermissionRationale(Manifest.permission.CAMERA)) {
            appPreferences.saveCameraPermissionBlocked(false);
        } else if (cameraPermissionNeedsSystemSettings()) {
            showQrScanFailure(QrScanFailurePresentation.CAMERA_PERMISSION_BLOCKED);
            return;
        }
        startActivityForResult(new Intent(this, QrScanActivity.class), REQUEST_QR_SCAN);
    }

    private void addOperationsWatchSwitchRow(
            LinearLayout parent, boolean paired, boolean showDivider) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(dp(22), 0, dp(18), 0);
        row.setMinimumHeight(dp(64));
        row.setBackgroundColor(cardBackgroundColor());

        LinearLayout text = new LinearLayout(this);
        text.setOrientation(LinearLayout.VERTICAL);
        text.setGravity(Gravity.CENTER_VERTICAL);
        TextView label = new TextView(this);
        label.setText(SettingsInformationArchitecture.OPERATIONS_WATCH);
        TextViewCompat.setTextAppearance(
                label, com.google.android.material.R.style.TextAppearance_Material3_BodyLarge);
        label.setTextColor(primaryTextColor());
        text.addView(label, matchWidthWrapParams());

        boolean enabled = appPreferences.isOperationsWatchUserEnabled();
        TextView value = new TextView(this);
        value.setText(OperationsWatchPreferencePolicy.status(paired, enabled));
        TextViewCompat.setTextAppearance(
                value, com.google.android.material.R.style.TextAppearance_Material3_BodySmall);
        value.setTextColor(mutedTextColor());
        text.addView(value, matchWidthWrapParams());
        row.addView(text, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        MaterialSwitch toggle = new MaterialSwitch(this);
        toggle.setChecked(enabled);
        toggle.setContentDescription(SettingsRowAccessibility.contentDescription(
                SettingsInformationArchitecture.OPERATIONS_WATCH,
                OperationsWatchPreferencePolicy.status(paired, enabled)));
        toggle.setOnCheckedChangeListener((button, checked) -> {
            String status = OperationsWatchPreferencePolicy.status(paired, checked);
            value.setText(status);
            button.setContentDescription(SettingsRowAccessibility.contentDescription(
                    SettingsInformationArchitecture.OPERATIONS_WATCH, status));
            setOperationsWatchEnabled(checked);
        });
        row.addView(toggle, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));
        row.setOnClickListener(view -> toggle.performClick());
        parent.addView(row, matchWidthWrapParams());

        if (showDivider) {
            View divider = new View(this);
            divider.setBackgroundColor(dividerColor());
            LinearLayout.LayoutParams dividerParams = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT, 1);
            dividerParams.setMargins(dp(22), 0, 0, 0);
            parent.addView(divider, dividerParams);
        }
    }

    private void setOperationsWatchEnabled(boolean enabled) {
        appPreferences.saveOperationsWatchUserEnabled(enabled);
        if (enabled) {
            OperationsWatchService.start(this);
            Toast.makeText(this, appPreferences.hasOperationsProfile()
                    ? "持续守护已开启" : "配对电脑后将自动开启持续守护",
                    Toast.LENGTH_SHORT).show();
        } else {
            OperationsWatchService.stopForUserPreference(this);
            Toast.makeText(this, "持续守护已关闭；前台运维仍可使用",
                    Toast.LENGTH_LONG).show();
        }
    }

    private String cameraPermissionStatus() {
        if (hasCameraPermission()) {
            return "已授权";
        }
        return cameraPermissionNeedsSystemSettings() ? "需在系统设置开启" : "需要时申请";
    }

    private boolean cameraPermissionNeedsSystemSettings() {
        return !hasCameraPermission()
                && appPreferences.isCameraPermissionBlocked()
                && !shouldShowRequestPermissionRationale(Manifest.permission.CAMERA);
    }

    private String notificationPermissionStatus() {
        return NotificationPermissionPolicy.status(
                Build.VERSION.SDK_INT,
                hasPostNotificationPermission(),
                NotificationManagerCompat.from(this).areNotificationsEnabled(),
                attentionNotificationChannelEnabled(),
                appPreferences.isNotificationPermissionBlocked(),
                shouldShowNotificationPermissionRationale());
    }

    private boolean hasPostNotificationPermission() {
        return Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU
                || checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)
                == PackageManager.PERMISSION_GRANTED;
    }

    private boolean shouldShowNotificationPermissionRationale() {
        return Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU
                && shouldShowRequestPermissionRationale(Manifest.permission.POST_NOTIFICATIONS);
    }

    private boolean attentionNotificationChannelEnabled() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return true;
        }
        NotificationManager manager = getSystemService(NotificationManager.class);
        NotificationChannel channel = manager == null ? null
                : manager.getNotificationChannel(OperationsWatchService.ATTENTION_CHANNEL_ID);
        return channel == null || channel.getImportance() != NotificationManager.IMPORTANCE_NONE;
    }

    private void manageNotificationPermission() {
        int action = NotificationPermissionPolicy.action(
                Build.VERSION.SDK_INT,
                hasPostNotificationPermission(),
                NotificationManagerCompat.from(this).areNotificationsEnabled(),
                attentionNotificationChannelEnabled(),
                appPreferences.isNotificationPermissionBlocked(),
                shouldShowNotificationPermissionRationale());
        if (action == NotificationPermissionPolicy.ACTION_REQUEST) {
            showNotificationPermissionExplanation();
            return;
        }
        openNotificationSettings();
    }

    private void showNotificationPermissionExplanation() {
        new MaterialAlertDialogBuilder(this)
                .setTitle("开启运维提醒")
                .setMessage("仅在电脑离线、严重告警、错误事件、消息通道异常或设备需关注等新状态出现时提醒。通知不显示端点、设备身份、日志或检测数据。")
                .setNegativeButton("暂不开启", null)
                .setPositiveButton("继续", (dialog, which) -> requestNotificationPermission())
                .show();
    }

    private void requestNotificationPermission() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU) {
            openNotificationSettings();
            return;
        }
        int requestGeneration = notificationPermissionDialogState.begin();
        requestPermissions(
                new String[]{Manifest.permission.POST_NOTIFICATIONS},
                REQUEST_NOTIFICATION_PERMISSION);
        root.postDelayed(() -> observeNotificationPermissionDialog(requestGeneration),
                RuntimePermissionDialogState.OBSERVE_DELAY_MILLISECONDS);
        root.postDelayed(() -> recoverBlockedNotificationPermissionRequest(requestGeneration),
                RuntimePermissionDialogState.NO_DIALOG_RECOVERY_DELAY_MILLISECONDS);
    }

    private void observeNotificationPermissionDialog(int requestGeneration) {
        notificationPermissionDialogState.observe(
                requestGeneration,
                hasPostNotificationPermission(),
                hasWindowFocus());
    }

    private void recoverBlockedNotificationPermissionRequest(int requestGeneration) {
        if (!notificationPermissionDialogState.shouldRecoverAsBlocked(
                requestGeneration,
                hasPostNotificationPermission(),
                hasWindowFocus())) {
            return;
        }
        appPreferences.saveNotificationPermissionBlocked();
        lastNotificationPermissionStatus = notificationPermissionStatus();
        if (root != null && currentTab == TAB_SETTINGS) {
            showProfileView();
        }
        Toast.makeText(this, "请在系统通知设置中开启运维提醒", Toast.LENGTH_LONG).show();
        openNotificationSettings();
    }

    private void openNotificationSettings() {
        try {
            Intent settings = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                    ? new Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS)
                            .putExtra(Settings.EXTRA_APP_PACKAGE, getPackageName())
                    : new Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                            Uri.parse("package:" + getPackageName()));
            startActivity(settings);
        } catch (Exception ex) {
            try {
                startActivity(new Intent(
                        Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                        Uri.parse("package:" + getPackageName())));
            } catch (Exception ignored) {
                Toast.makeText(this, "无法打开系统通知设置", Toast.LENGTH_LONG).show();
            }
        }
    }

    private void showQrScanFailure(String reason) {
        boolean blocked = QrScanFailurePresentation.CAMERA_PERMISSION_BLOCKED.equals(reason);
        appPreferences.saveCameraPermissionBlocked(blocked);
        if (currentTab == TAB_SETTINGS) {
            showProfileView();
        }
        QrScanRecoveryDialog.show(this, reason, this::startQrScan);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == REQUEST_INSTALL_PERMISSION) {
            File verifiedApk = pendingInstallFile;
            if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O
                    || getPackageManager().canRequestPackageInstalls()) {
                launchPackageInstaller(verifiedApk);
            } else {
                Toast.makeText(this, "系统尚未允许安装更新；已保留校验后的安装包", Toast.LENGTH_LONG).show();
            }
            return;
        }

        if (requestCode == REQUEST_QR_SCAN) {
            if (resultCode == RESULT_OK && data != null) {
                appPreferences.saveCameraPermissionBlocked(false);
                String result = data.getStringExtra(QrScanActivity.EXTRA_QR_RESULT);
                saveAndOpen(result);
                return;
            }
            String failureReason = data == null
                    ? "" : data.getStringExtra(QrScanActivity.EXTRA_SCAN_FAILURE);
            if (failureReason != null && !failureReason.isEmpty()) {
                showQrScanFailure(failureReason);
                return;
            }
            Toast.makeText(this, "已取消运维配对扫码", Toast.LENGTH_SHORT).show();
            return;
        }

        super.onActivityResult(requestCode, resultCode, data);
    }

    @Override
    public void onRequestPermissionsResult(
            int requestCode, String[] permissions, int[] grantResults) {
        if (requestCode == REQUEST_NOTIFICATION_PERMISSION) {
            boolean granted = hasPostNotificationPermission();
            notificationPermissionDialogState.completeFromSystemResult(granted);
            lastNotificationPermissionStatus = notificationPermissionStatus();
            if (root != null && currentTab == TAB_SETTINGS) {
                showProfileView();
            }
            if (granted) {
                OperationsWatchService.start(this);
                Toast.makeText(this, "运维提醒已开启", Toast.LENGTH_SHORT).show();
            }
            return;
        }
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
    }

    @Override
    protected void onResume() {
        super.onResume();
        boolean granted = hasCameraPermission();
        String notificationStatus = appPreferences == null
                ? "" : notificationPermissionStatus();
        boolean notificationStatusChanged = !notificationStatus.equals(
                lastNotificationPermissionStatus);
        if (appPreferences != null && (granted
                || shouldShowRequestPermissionRationale(Manifest.permission.CAMERA))) {
            appPreferences.saveCameraPermissionBlocked(false);
        }
        if (root != null && currentTab == TAB_SETTINGS
                && (granted != cameraPermissionGranted || notificationStatusChanged)) {
            showProfileView();
        }
        cameraPermissionGranted = granted;
        lastNotificationPermissionStatus = notificationStatus;
    }

    private void openOperations() {
        if (appPreferences.hasOperationsProfile()) {
            openOperationsDirectly();
        } else {
            startQrScan();
        }
    }

    private void checkForAppUpdate() {
        if (appUpdateInFlight) {
            return;
        }
        appUpdateInFlight = true;
        progressBar.setIndeterminate(true);
        progressBar.setVisibility(View.VISIBLE);
        headerSubtitle.setText("正在检查安全更新…");
        appUpdateExecutor.execute(() -> {
            try {
                AndroidUpdateClient.Release release = new AndroidUpdateClient(this).check();
                String currentVersion = getAppVersionName();
                runOnUiThread(() -> {
                    finishAppUpdateWork();
                    if (release == null || !AndroidUpdatePolicy.isNewerVersion(release.version, currentVersion)) {
                        showAppUpdateMessage(
                                "已经是最新版本",
                                "当前版本 " + currentVersion + "，暂无更高版本。");
                        return;
                    }
                    showAvailableUpdate(release, currentVersion);
                });
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    finishAppUpdateWork();
                    showAppUpdateMessage("检查更新失败", readableAppUpdateError(ex));
                });
            }
        });
    }

    private void showAvailableUpdate(AndroidUpdateClient.Release release, String currentVersion) {
        String size = String.format(Locale.CHINA, "%.1f MB", release.size / 1024d / 1024d);
        new MaterialAlertDialogBuilder(this)
                .setTitle("发现 ColorVision Android " + release.version)
                .setMessage("当前 " + currentVersion + " · 安装包 " + size
                        + "\n\n下载完成后会在交给系统安装前校验文件长度、SHA-256、应用包名、版本和签名。")
                .setNegativeButton("稍后", null)
                .setPositiveButton("下载并安装", (dialog, which) -> downloadAndInstallUpdate(release))
                .show();
    }

    private void downloadAndInstallUpdate(AndroidUpdateClient.Release release) {
        if (appUpdateInFlight) {
            return;
        }
        appUpdateInFlight = true;
        progressBar.setIndeterminate(false);
        progressBar.setMax(100);
        progressBar.setProgress(0);
        progressBar.setVisibility(View.VISIBLE);
        headerSubtitle.setText("正在下载并校验 " + release.version + "…");
        appUpdateExecutor.execute(() -> {
            try {
                File verified = new AndroidUpdateClient(this).downloadAndVerify(
                        release,
                        percent -> runOnUiThread(() -> progressBar.setProgress(percent)));
                runOnUiThread(() -> {
                    finishAppUpdateWork();
                    requestInstallUpdate(verified);
                });
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    finishAppUpdateWork();
                    showAppUpdateMessage("更新包已阻止", readableAppUpdateError(ex));
                });
            }
        });
    }

    private void requestInstallUpdate(File verifiedApk) {
        pendingInstallFile = verifiedApk;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                && !getPackageManager().canRequestPackageInstalls()) {
            try {
                Intent settings = new Intent(
                        Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                        Uri.parse("package:" + getPackageName()));
                Toast.makeText(this, "请在系统页允许 ColorVision 安装本次更新", Toast.LENGTH_LONG).show();
                startActivityForResult(settings, REQUEST_INSTALL_PERMISSION);
            } catch (Exception ex) {
                showAppUpdateMessage("无法打开系统安装授权", "更新包已经安全校验，可稍后从应用更新入口重试。");
            }
            return;
        }
        launchPackageInstaller(verifiedApk);
    }

    private void launchPackageInstaller(File verifiedApk) {
        if (verifiedApk == null || !verifiedApk.isFile()) {
            showAppUpdateMessage("更新包不可用", "请重新检查安全更新。");
            return;
        }
        try {
            Uri uri = FileProvider.getUriForFile(
                    this,
                    getPackageName() + ".fileprovider",
                    verifiedApk);
            Intent install = new Intent(Intent.ACTION_INSTALL_PACKAGE);
            install.setDataAndType(uri, "application/vnd.android.package-archive");
            install.setClipData(ClipData.newRawUri("ColorVision Android 更新", uri));
            install.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            startActivity(install);
            pendingInstallFile = null;
        } catch (Exception ex) {
            showAppUpdateMessage("无法启动系统安装器", "更新包已经安全校验，请稍后重试。");
        }
    }

    private void finishAppUpdateWork() {
        appUpdateInFlight = false;
        progressBar.setVisibility(View.GONE);
        if (currentTab == TAB_SETTINGS) {
            headerSubtitle.setText("安全配对与应用信息");
        }
    }

    private void showAppUpdateMessage(String title, String message) {
        if (isFinishing()) {
            return;
        }
        new MaterialAlertDialogBuilder(this)
                .setTitle(title)
                .setMessage(message)
                .setPositiveButton("知道了", null)
                .show();
    }

    private String readableAppUpdateError(Exception ex) {
        String message = ex.getMessage() == null ? "" : ex.getMessage();
        if (ex instanceof UnknownHostException || ex instanceof ConnectException) {
            return "安全更新服务当前不可达，请稍后重试。";
        }
        if (ex instanceof SocketTimeoutException) {
            return "安全更新服务响应超时，请稍后重试。";
        }
        if (message.contains("manifest_http_404")) {
            return "安全更新服务尚未提供移动端更新清单。";
        }
        if (message.contains("signature_mismatch")) {
            return "安装包签名与当前应用不一致，已阻止安装。";
        }
        if (message.contains("hash_mismatch")) {
            return "安装包完整性校验失败，已删除临时文件。";
        }
        if (message.contains("not_newer")) {
            return "下载的安装包不是更高版本，已阻止降级或重复安装。";
        }
        if (message.contains("package_name_mismatch") || message.contains("package_version_mismatch")) {
            return "安装包身份与更新清单不一致，已阻止安装。";
        }
        if (message.contains("rejected") || message.contains("incomplete") || message.contains("too_large")) {
            return "更新数据不符合安全约束，已阻止安装。";
        }
        return "无法完成安全更新校验，请稍后重试。";
    }

    private void openOperationsDirectly() {
        OperationsWatchService.start(this);
        startActivity(new Intent(this, OperationsActivity.class)
                .addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP));
        finish();
    }

    private void saveAndOpen(String rawContent) {
        String text = rawContent == null ? "" : rawContent.trim();
        if (!OperationsPairingPayload.isPairingInput(text)) {
            showPairingScanFailure(PairingFailurePresentation.INVALID_QR);
            return;
        }
        try {
            OperationsPairingPayload.parse(text);
            Intent operations = new Intent(this, OperationsActivity.class);
            operations.putExtra(OperationsActivity.EXTRA_PAIRING_PAYLOAD, text);
            startActivity(operations);
            finish();
        } catch (Exception ex) {
            showPairingScanFailure(PairingFailurePresentation.reasonFor(ex));
        }
    }

    private void showPairingScanFailure(String reason) {
        PairingScanRecoveryDialog.show(this, reason, this::startQrScan);
    }

    private LinearLayout makeCard() {
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setPadding(dp(18), dp(18), dp(18), dp(18));
        card.setBackground(rounded(cardBackgroundColor(), dp(12), Color.TRANSPARENT, 0));
        return card;
    }

    private TextView makeTitle(String text, int size) {
        TextView title = new TextView(this);
        title.setText(text);
        TextViewCompat.setTextAppearance(title, size >= 22
                ? com.google.android.material.R.style.TextAppearance_Material3_HeadlineSmall
                : com.google.android.material.R.style.TextAppearance_Material3_TitleMedium);
        title.setTextColor(primaryTextColor());
        title.setGravity(Gravity.START);
        return title;
    }

    private TextView makeBodyText(String text) {
        TextView body = new TextView(this);
        body.setText(text);
        TextViewCompat.setTextAppearance(body, com.google.android.material.R.style.TextAppearance_Material3_BodyMedium);
        body.setTextColor(secondaryTextColor());
        body.setLineSpacing(0, 1.12f);
        return body;
    }

    private Button makePrimaryButton(String text) {
        return makeBaseButton(text, com.google.android.material.R.attr.materialButtonStyle);
    }

    private Button makeSecondaryButton(String text) {
        return makeBaseButton(text, com.google.android.material.R.attr.materialButtonOutlinedStyle);
    }

    private Button makeBaseButton(String text, int styleAttribute) {
        MaterialButton button = new MaterialButton(this, null, styleAttribute);
        button.setText(text);
        button.setTextSize(15);
        button.setAllCaps(false);
        button.setGravity(Gravity.CENTER);
        button.setMinHeight(dp(46));
        return button;
    }

    private GradientDrawable rounded(int fillColor, int radius, int strokeColor, int strokeWidth) {
        GradientDrawable drawable = new GradientDrawable();
        drawable.setColor(fillColor);
        drawable.setCornerRadius(radius);
        if (strokeWidth > 0) {
            drawable.setStroke(strokeWidth, strokeColor);
        }
        return drawable;
    }

    private FrameLayout.LayoutParams matchParentParams() {
        return new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT);
    }

    private LinearLayout.LayoutParams matchWidthWrapParams() {
        return new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
    }

    private LinearLayout.LayoutParams fullWidthButtonParams() {
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        params.setMargins(0, dp(10), 0, 0);
        return params;
    }

    private LinearLayout.LayoutParams fullWidthCardParams() {
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        params.setMargins(0, dp(12), 0, 0);
        return params;
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    private void applyTopSystemBarInset(View insetHost) {
        ViewCompat.setOnApplyWindowInsetsListener(insetHost, (view, windowInsets) -> {
            Insets statusBars = windowInsets.getInsets(WindowInsetsCompat.Type.statusBars());
            Insets displayCutout = windowInsets.getInsets(WindowInsetsCompat.Type.displayCutout());
            int topInset = AppWindowInsetsPolicy.topContentInset(
                    statusBars.top, displayCutout.top);
            view.setPadding(view.getPaddingLeft(), topInset,
                    view.getPaddingRight(), view.getPaddingBottom());
            return windowInsets;
        });
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        outState.putInt(STATE_CURRENT_TAB, currentTab);
        outState.putBoolean(STATE_OPENED_FROM_OPERATIONS, openedFromOperations);
        super.onSaveInstanceState(outState);
    }

    @Override
    protected void onDestroy() {
        appUpdateExecutor.shutdownNow();
        super.onDestroy();
    }
}

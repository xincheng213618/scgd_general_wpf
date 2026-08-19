package com.colorvision.xcviewer;

import android.Manifest;
import android.content.ClipData;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.Settings;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.coordinatorlayout.widget.CoordinatorLayout;
import androidx.core.content.FileProvider;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.appbar.MaterialToolbar;
import com.google.android.material.bottomnavigation.BottomNavigationView;
import com.google.android.material.button.MaterialButton;
import com.google.android.material.card.MaterialCardView;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.navigation.NavigationBarView;
import com.google.android.material.navigationrail.NavigationRailView;
import com.google.android.material.progressindicator.LinearProgressIndicator;
import com.google.android.material.snackbar.Snackbar;

import java.io.File;
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
    private static final int NAV_PROBLEMS = 2002;
    private static final int NAV_TOOLS = 2003;
    private static final int NAV_SETTINGS = 2004;
    static final int TAB_OPERATIONS = 0;
    static final int TAB_TOOLS = 1;
    static final int TAB_SETTINGS = 2;
    static final int TAB_PROBLEMS = 3;

    private FrameLayout root;
    private LinearLayout appShell;
    private FrameLayout setupContainer;
    private CoordinatorLayout snackbarHost;
    private ProgressBar progressBar;
    private AppPreferences appPreferences;
    private ThemeManager themeManager;
    private MaterialToolbar topAppBar;
    private NavigationBarView topLevelNavigation;
    private boolean navigationRail;
    private boolean updatingTopLevelNavigation;
    private boolean openedFromOperations;
    private int currentTab = TAB_OPERATIONS;
    private String lastNotificationPermissionStatus = "";
    private Snackbar watchPreferenceSnackbar;
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
        lastNotificationPermissionStatus = notificationPermissionStatus();
        int requestedTab = consumeStartTab(getIntent());
        boolean restoring = savedInstanceState != null;
        int restoredTab = restoring
                ? savedInstanceState.getInt(STATE_CURRENT_TAB, -1) : -1;
        int startTab = AppNavigationPolicy.resolveCreationTab(
                restoring,
                restoredTab,
                requestedTab,
                TAB_OPERATIONS,
                TAB_PROBLEMS,
                TAB_TOOLS,
                TAB_SETTINGS);
        openedFromOperations = restoring
                ? savedInstanceState.getBoolean(
                        STATE_OPENED_FROM_OPERATIONS,
                        getIntent().getBooleanExtra(EXTRA_FROM_OPERATIONS, false))
                : getIntent().getBooleanExtra(EXTRA_FROM_OPERATIONS, false);
        if (openedFromOperations && startTab == TAB_SETTINGS) {
            AppScreenMotion.configureSettingsActivity(this);
        }
        if (AppNavigationPolicy.shouldOpenPairedWorkspace(
                appPreferences.hasOperationsProfile(),
                AppNavigationPolicy.isTopLevelTab(
                        startTab,
                        TAB_OPERATIONS,
                        TAB_PROBLEMS,
                        TAB_TOOLS,
                        TAB_SETTINGS))) {
            openOperationsDirectly(AppNavigationPolicy.pairedDestinationForTab(
                    startTab,
                    TAB_OPERATIONS,
                    TAB_PROBLEMS,
                    TAB_TOOLS,
                    TAB_SETTINGS));
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

        applySystemBarInsets(root);
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
        navigationRail = AppResponsiveLayout.usesNavigationRail(
                getResources().getConfiguration().screenWidthDp);
        LinearLayout shell = new LinearLayout(this);
        shell.setOrientation(navigationRail
                ? LinearLayout.HORIZONTAL : LinearLayout.VERTICAL);
        shell.setBackgroundColor(shellBackgroundColor());
        LinearLayout workspace = new LinearLayout(this);
        workspace.setOrientation(LinearLayout.VERTICAL);
        workspace.setBackgroundColor(shellBackgroundColor());
        workspace.addView(createTopBar(), new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));

        workspace.addView(setupContainer, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                0,
                1));
        snackbarHost = new CoordinatorLayout(this);
        snackbarHost.addView(workspace, new CoordinatorLayout.LayoutParams(
                CoordinatorLayout.LayoutParams.MATCH_PARENT,
                CoordinatorLayout.LayoutParams.MATCH_PARENT));

        topLevelNavigation = createTopLevelNavigation();
        if (navigationRail) {
            shell.addView(topLevelNavigation, new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.WRAP_CONTENT,
                    LinearLayout.LayoutParams.MATCH_PARENT));
            shell.addView(snackbarHost, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.MATCH_PARENT, 1));
        } else {
            shell.addView(snackbarHost, new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));
            shell.addView(topLevelNavigation, new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT));
        }
        return shell;
    }

    private MaterialToolbar createTopBar() {
        topAppBar = new MaterialToolbar(this);
        topAppBar.setMinimumHeight(dp(64));
        topAppBar.setBackgroundColor(shellBackgroundColor());
        topAppBar.setTitleTextColor(primaryTextColor());
        topAppBar.setSubtitleTextColor(secondaryTextColor());
        setTopBar(getString(R.string.brand_name), "安全运维伴侣");
        return topAppBar;
    }

    private void setTopBar(String title, String subtitle) {
        topAppBar.setTitle(title);
        topAppBar.setSubtitle(subtitle);
    }

    private NavigationBarView createTopLevelNavigation() {
        NavigationBarView nav = navigationRail
                ? new NavigationRailView(this) : new BottomNavigationView(this);
        nav.setBackgroundColor(bottomNavBackgroundColor());
        nav.setLabelVisibilityMode(NavigationBarView.LABEL_VISIBILITY_LABELED);
        if (nav instanceof NavigationRailView) {
            ((NavigationRailView) nav).setMenuGravity(Gravity.CENTER);
        }
        nav.getMenu().add(0, NAV_OPERATIONS, 0, "概览").setIcon(R.drawable.ic_devices_24);
        nav.getMenu().add(0, NAV_PROBLEMS, 1, "问题")
                .setIcon(R.drawable.ic_report_problem_24);
        nav.getMenu().add(0, NAV_TOOLS, 2, "工具").setIcon(R.drawable.ic_build_24);
        nav.getMenu().add(0, NAV_SETTINGS, 3, "设置").setIcon(R.drawable.ic_settings_24);
        renderProblemNavigationBadge(nav);
        nav.setOnItemSelectedListener(item -> {
            if (updatingTopLevelNavigation) {
                return true;
            }
            if (item.getItemId() == NAV_OPERATIONS) {
                showOperationsLanding();
                return true;
            }
            if (item.getItemId() == NAV_PROBLEMS) {
                if (appPreferences.hasOperationsProfile()) {
                    openOperationsDirectly(OperationsDestinationState.TRIAGE);
                } else {
                    showProblemsLanding();
                }
                return true;
            }
            if (item.getItemId() == NAV_TOOLS) {
                showToolsLanding();
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

    private void renderProblemNavigationBadge(NavigationBarView navigation) {
        OperationsProblemBadgeRenderer.render(
                navigation,
                NAV_PROBLEMS,
                OperationsProblemBadgePresentation.create(
                        appPreferences.getOperationsProfileCount() > 0,
                        appPreferences.getOperationsWatchState(),
                        0));
    }

    private void selectTab(int tab) {
        currentTab = tab;
        if (topLevelNavigation == null) {
            return;
        }
        int itemId = tab == TAB_SETTINGS
                ? NAV_SETTINGS
                : tab == TAB_TOOLS
                        ? NAV_TOOLS
                        : tab == TAB_PROBLEMS ? NAV_PROBLEMS : NAV_OPERATIONS;
        if (topLevelNavigation.getSelectedItemId() != itemId) {
            updatingTopLevelNavigation = true;
            topLevelNavigation.setSelectedItemId(itemId);
            updatingTopLevelNavigation = false;
        }
    }

    private int consumeStartTab(Intent intent) {
        int requestedTab = intent.getIntExtra(EXTRA_START_TAB, -1);
        intent.removeExtra(EXTRA_START_TAB);
        return AppNavigationPolicy.normalizeStartTab(
                requestedTab,
                appPreferences.consumeStartTab(TAB_OPERATIONS),
                TAB_OPERATIONS,
                TAB_PROBLEMS,
                TAB_TOOLS,
                TAB_SETTINGS);
    }

    private void showInitialTab(int startTab) {
        if (startTab == TAB_SETTINGS) {
            showProfileView();
            return;
        }
        if (startTab == TAB_PROBLEMS) {
            showProblemsLanding();
            return;
        }
        if (startTab == TAB_TOOLS) {
            showToolsLanding();
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
        if (AppNavigationPolicy.shouldOpenPairedWorkspace(
                appPreferences.hasOperationsProfile(),
                AppNavigationPolicy.isTopLevelTab(
                        startTab,
                        TAB_OPERATIONS,
                        TAB_PROBLEMS,
                        TAB_TOOLS,
                        TAB_SETTINGS))) {
            openOperationsDirectly(AppNavigationPolicy.pairedDestinationForTab(
                    startTab,
                    TAB_OPERATIONS,
                    TAB_PROBLEMS,
                    TAB_TOOLS,
                    TAB_SETTINGS));
            return;
        }
        showInitialTab(startTab);
    }

    private void showOperationsLanding() {
        int direction = AppScreenMotion.directionBetween(
                currentTab,
                TAB_OPERATIONS,
                TAB_OPERATIONS,
                TAB_PROBLEMS,
                TAB_TOOLS,
                TAB_SETTINGS);
        if (appPreferences.hasOperationsProfile()) {
            if (openedFromOperations
                    && direction == AppScreenMotion.DIRECTION_BACKWARD) {
                openOperationsDirectly(OperationsDestinationState.OVERVIEW);
                return;
            }
            openOperations();
            return;
        }
        AppScreenMotion.beginContentTransition(setupContainer, direction);
        selectTab(TAB_OPERATIONS);
        setTopBar("现场运维", "扫描电脑端安全配对码");
        setupContainer.removeAllViews();
        setupContainer.addView(createOperationsLandingContent(), matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        progressBar.setVisibility(View.GONE);
    }

    private ScrollView createOperationsLandingContent() {
        return createPairingLandingContent(
                "连接运维电脑",
                "扫描电脑端“设置 > 局域网控制”中的短时安全配对码。完成一次配对后，运维伴侣会成为首屏并持续守护安全连接。");
    }

    private void showProblemsLanding() {
        int direction = AppScreenMotion.directionBetween(
                currentTab,
                TAB_PROBLEMS,
                TAB_OPERATIONS,
                TAB_PROBLEMS,
                TAB_TOOLS,
                TAB_SETTINGS);
        if (appPreferences.hasOperationsProfile()) {
            openOperationsDirectly(OperationsDestinationState.TRIAGE);
            return;
        }
        AppScreenMotion.beginContentTransition(setupContainer, direction);
        selectTab(TAB_PROBLEMS);
        setTopBar("问题中心", "连接电脑后开始复核");
        setupContainer.removeAllViews();
        setupContainer.addView(createPairingLandingContent(
                "先连接运维电脑",
                "完成安全配对后，问题中心会按优先级汇总设备离线、警告事件与待处理作业，并保留诊断证据。"),
                matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        progressBar.setVisibility(View.GONE);
    }

    private void showToolsLanding() {
        int direction = AppScreenMotion.directionBetween(
                currentTab,
                TAB_TOOLS,
                TAB_OPERATIONS,
                TAB_PROBLEMS,
                TAB_TOOLS,
                TAB_SETTINGS);
        if (appPreferences.hasOperationsProfile()) {
            openOperationsDirectly(OperationsDestinationState.TOOLS);
            return;
        }
        AppScreenMotion.beginContentTransition(setupContainer, direction);
        selectTab(TAB_TOOLS);
        setTopBar("运维工具", "连接电脑后使用");
        setupContainer.removeAllViews();
        setupContainer.addView(createPairingLandingContent(
                "先连接运维电脑",
                "完成安全配对后，可使用诊断、恢复、取证、审批与支持工具。所有高风险动作仍需明确确认。"),
                matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        progressBar.setVisibility(View.GONE);
    }

    private ScrollView createPairingLandingContent(String title, String description) {
        ScrollView scrollView = new ScrollView(this);
        scrollView.setFillViewport(true);
        scrollView.setBackgroundColor(pageBackgroundColor());

        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(16), dp(16), dp(16), dp(24));
        scrollView.addView(content, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT,
                ScrollView.LayoutParams.WRAP_CONTENT));

        LinearLayout cardContent = new LinearLayout(this);
        cardContent.setOrientation(LinearLayout.VERTICAL);
        cardContent.setPadding(dp(16), dp(16), dp(16), dp(16));
        cardContent.addView(makeTitle(title, 22), matchWidthWrapParams());

        TextView status = makeBodyText(description);
        status.setPadding(0, dp(8), 0, dp(4));
        cardContent.addView(status, matchWidthWrapParams());

        Button operationsButton = makePrimaryButton("扫描并连接电脑");
        operationsButton.setOnClickListener(v -> openOperations());
        cardContent.addView(operationsButton, fullWidthButtonParams());

        Button helpButton = makeSecondaryButton("配对码在哪里？");
        helpButton.setOnClickListener(v -> PairingHelpDialog.show(this, this::startQrScan));
        cardContent.addView(helpButton, fullWidthButtonParams());

        MaterialCardView operationsCard = new MaterialCardView(this);
        operationsCard.setCardBackgroundColor(cardBackgroundColor());
        operationsCard.addView(cardContent, new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT));
        content.addView(operationsCard, matchWidthWrapParams());

        return scrollView;
    }

    private void showProfileView() {
        int direction = AppScreenMotion.directionBetween(
                currentTab,
                TAB_SETTINGS,
                TAB_OPERATIONS,
                TAB_PROBLEMS,
                TAB_TOOLS,
                TAB_SETTINGS);
        AppScreenMotion.beginContentTransition(setupContainer, direction);
        selectTab(TAB_SETTINGS);
        setTopBar("设置", "连接、后台与应用");
        setupContainer.removeAllViews();
        setupContainer.addView(createProfileContent(), matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        progressBar.setVisibility(View.GONE);
    }

    private ScrollView createProfileContent() {
        boolean paired = appPreferences.hasOperationsProfile();
        int pairedComputerCount = appPreferences.getOperationsProfileCount();
        SettingsPageContent.ViewModel model = new SettingsPageContent.ViewModel(
                paired,
                paired
                        ? appPreferences.getActiveOperationsProfileLabel()
                                + " · 共 " + pairedComputerCount + " 台"
                        : "",
                appPreferences.isOperationsWatchUserEnabled(),
                OperationsWatchPreferencePolicy.status(
                        paired,
                        appPreferences.isOperationsWatchUserEnabled(),
                        operationsRemindersAvailable()),
                "",
                notificationPermissionStatus(),
                getThemeModeLabel(),
                "当前 " + getAppVersionName() + " · 签名校验");
        return SettingsPageContent.create(
                this,
                themeManager,
                model,
                new SettingsPageContent.Handler() {
                    @Override
                    public void onComputerConnections() {
                        openOperationsConnectionsFromSettings();
                    }

                    @Override
                    public void onAddComputer() {
                        startQrScan();
                    }

                    @Override
                    public String onWatchChanged(boolean enabled) {
                        setOperationsWatchEnabled(enabled);
                        return OperationsWatchPreferencePolicy.status(
                                appPreferences.hasOperationsProfile(),
                                enabled,
                                operationsRemindersAvailable());
                    }

                    @Override
                    public void onWatchStatus() {
                        openOperationsDirectly(OperationsDestinationState.OVERVIEW);
                    }

                    @Override
                    public void onNotificationPermission() {
                        manageNotificationPermission();
                    }

                    @Override
                    public void onThemeMode() {
                        showThemeDialog();
                    }

                    @Override
                    public void onAppUpdate() {
                        checkForAppUpdate();
                    }

                });
    }

    private String getAppVersionName() {
        try {
            return getPackageManager().getPackageInfo(getPackageName(), 0).versionName;
        } catch (Exception ex) {
            return "--";
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

    private void setOperationsWatchEnabled(boolean enabled) {
        appPreferences.saveOperationsWatchUserEnabled(enabled);
        if (enabled) {
            OperationsWatchService.start(this);
            boolean hasOperationsProfile = appPreferences.hasOperationsProfile();
            boolean remindersAvailable = operationsRemindersAvailable();
            showOperationsWatchFeedback(
                    OperationsWatchPreferencePolicy.enabledFeedback(
                            hasOperationsProfile, remindersAvailable),
                    OperationsWatchPreferencePolicy.shouldOfferReminderAction(
                            hasOperationsProfile, remindersAvailable));
        } else {
            OperationsWatchService.stopForUserPreference(this);
            showOperationsWatchFeedback("持续守护已关闭；前台运维仍可使用", false);
        }
    }

    private void showOperationsWatchFeedback(String message, boolean offerReminderAction) {
        if (watchPreferenceSnackbar != null) {
            watchPreferenceSnackbar.dismiss();
        }
        watchPreferenceSnackbar = Snackbar.make(snackbarHost, message, Snackbar.LENGTH_LONG);
        if (offerReminderAction) {
            watchPreferenceSnackbar.setAction("开启提醒", view -> manageNotificationPermission());
        }
        watchPreferenceSnackbar.show();
    }

    private boolean cameraPermissionNeedsSystemSettings() {
        return !hasCameraPermission()
                && appPreferences.isCameraPermissionBlocked()
                && !shouldShowRequestPermissionRationale(Manifest.permission.CAMERA);
    }

    private String notificationPermissionStatus() {
        return NotificationPermissionPolicy.status(
                Build.VERSION.SDK_INT,
                NotificationPermissionState.hasRuntimePermission(this),
                NotificationPermissionState.appNotificationsEnabled(this),
                NotificationPermissionState.attentionChannelEnabled(this),
                appPreferences.isNotificationPermissionBlocked(),
                shouldShowNotificationPermissionRationale());
    }

    private boolean operationsRemindersAvailable() {
        return NotificationPermissionPolicy.canPostAttention(
                Build.VERSION.SDK_INT,
                NotificationPermissionState.hasRuntimePermission(this),
                NotificationPermissionState.appNotificationsEnabled(this),
                NotificationPermissionState.attentionChannelEnabled(this));
    }

    private boolean shouldShowNotificationPermissionRationale() {
        return Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU
                && shouldShowRequestPermissionRationale(Manifest.permission.POST_NOTIFICATIONS);
    }

    private void manageNotificationPermission() {
        int action = NotificationPermissionPolicy.action(
                Build.VERSION.SDK_INT,
                NotificationPermissionState.hasRuntimePermission(this),
                NotificationPermissionState.appNotificationsEnabled(this),
                NotificationPermissionState.attentionChannelEnabled(this),
                appPreferences.isNotificationPermissionBlocked(),
                shouldShowNotificationPermissionRationale());
        if (action == NotificationPermissionPolicy.ACTION_REQUEST) {
            showNotificationPermissionExplanation();
            return;
        }
        if (action == NotificationPermissionPolicy.ACTION_MANAGE) {
            showNotificationManagementDialog();
            return;
        }
        openNotificationSettings();
    }

    private void showNotificationManagementDialog() {
        new MaterialAlertDialogBuilder(this)
                .setTitle(R.string.operations_reminder_manage_title)
                .setMessage(R.string.operations_reminder_manage_message)
                .setNegativeButton(R.string.operations_reminder_system_settings_action,
                        (dialog, which) -> openNotificationSettings())
                .setPositiveButton(R.string.operations_reminder_test_action,
                        (dialog, which) -> sendReminderTest())
                .show();
    }

    private void sendReminderTest() {
        boolean posted = OperationsWatchService.postReminderTest(this);
        if (!posted) {
            lastNotificationPermissionStatus = notificationPermissionStatus();
            if (root != null && currentTab == TAB_SETTINGS) {
                showProfileView();
            }
        }
        showOperationsWatchFeedback(
                getString(posted
                        ? R.string.operations_reminder_test_sent
                        : R.string.operations_reminder_test_unavailable),
                !posted);
    }

    private void showNotificationPermissionExplanation() {
        new MaterialAlertDialogBuilder(this)
                .setTitle(R.string.operations_reminder_permission_title)
                .setMessage(R.string.operations_reminder_permission_message)
                .setNegativeButton(R.string.operations_reminder_permission_later, null)
                .setPositiveButton(R.string.operations_reminder_permission_action,
                        (dialog, which) -> requestNotificationPermission())
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
                NotificationPermissionState.hasRuntimePermission(this),
                hasWindowFocus());
    }

    private void recoverBlockedNotificationPermissionRequest(int requestGeneration) {
        if (!notificationPermissionDialogState.shouldRecoverAsBlocked(
                requestGeneration,
                NotificationPermissionState.hasRuntimePermission(this),
                hasWindowFocus())) {
            return;
        }
        appPreferences.saveNotificationPermissionBlocked(true);
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
            boolean granted = NotificationPermissionState.hasRuntimePermission(this);
            notificationPermissionDialogState.completeFromSystemResult(granted);
            if (NotificationPermissionPolicy.shouldRecordDeniedRequest(
                    granted, permissions.length, grantResults.length)) {
                appPreferences.saveNotificationPermissionBlocked(true);
            } else if (granted) {
                appPreferences.saveNotificationPermissionBlocked(false);
            }
            lastNotificationPermissionStatus = notificationPermissionStatus();
            if (root != null && currentTab == TAB_SETTINGS) {
                showProfileView();
            }
            if (granted) {
                OperationsWatchService.start(this);
                Toast.makeText(this, "运维提醒已开启", Toast.LENGTH_SHORT).show();
            } else if (root != null && currentTab == TAB_SETTINGS) {
                showOperationsWatchFeedback(
                        getString(R.string.operations_reminder_permission_denied), false);
            }
            return;
        }
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
    }

    @Override
    protected void onResume() {
        super.onResume();
        renderProblemNavigationBadge(topLevelNavigation);
        boolean granted = hasCameraPermission();
        String notificationStatus = appPreferences == null
                ? "" : notificationPermissionStatus();
        boolean notificationStatusChanged = !notificationStatus.equals(
                lastNotificationPermissionStatus);
        if (appPreferences != null && (granted
                || shouldShowRequestPermissionRationale(Manifest.permission.CAMERA))) {
            appPreferences.saveCameraPermissionBlocked(false);
        }
        if (appPreferences != null
                && (NotificationPermissionState.hasRuntimePermission(this)
                || shouldShowNotificationPermissionRationale())) {
            appPreferences.saveNotificationPermissionBlocked(false);
        }
        if (root != null && currentTab == TAB_SETTINGS && notificationStatusChanged) {
            showProfileView();
        }
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
        topAppBar.setSubtitle("正在检查安全更新…");
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
                    showAppUpdateFailure(
                            "检查更新失败",
                            AndroidUpdateFailurePresentation.message(ex));
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
        topAppBar.setSubtitle(getString(R.string.app_update_downloading, release.version));
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
                    showAppUpdateFailure(
                            "更新包已阻止",
                            AndroidUpdateFailurePresentation.message(ex));
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
            topAppBar.setSubtitle("连接、后台与应用");
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

    private void showAppUpdateFailure(String title, String message) {
        if (isFinishing()) {
            return;
        }
        new MaterialAlertDialogBuilder(this)
                .setTitle(title)
                .setMessage(message)
                .setNegativeButton("稍后", null)
                .setPositiveButton("重新检查", (dialog, which) -> checkForAppUpdate())
                .show();
    }

    private void openOperationsDirectly() {
        openOperationsDirectly(OperationsDestinationState.OVERVIEW);
    }

    private void openOperationsDirectly(String destination) {
        openOperationsDirectly(destination, false);
    }

    private void openOperationsConnectionsFromSettings() {
        openOperationsDirectly(OperationsDestinationState.CONNECTIONS, true);
    }

    private void openOperationsDirectly(String destination, boolean returnToSettings) {
        OperationsWatchService.start(this);
        Intent intent = new Intent(this, OperationsActivity.class)
                .addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        intent.putExtra(OperationsActivity.EXTRA_OPEN_DESTINATION, destination);
        intent.putExtra(OperationsActivity.EXTRA_RETURN_TO_SETTINGS, returnToSettings);
        startActivity(intent);
        if (openedFromOperations) {
            AppScreenMotion.finishBackward(this);
        } else {
            finish();
        }
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
            if (currentTab == TAB_PROBLEMS) {
                operations.putExtra(
                        OperationsActivity.EXTRA_OPEN_DESTINATION,
                        OperationsDestinationState.TRIAGE);
            } else if (currentTab == TAB_TOOLS) {
                operations.putExtra(
                        OperationsActivity.EXTRA_OPEN_DESTINATION,
                        OperationsDestinationState.TOOLS);
            }
            startActivity(operations);
            finish();
        } catch (Exception ex) {
            showPairingScanFailure(PairingFailurePresentation.reasonFor(ex));
        }
    }

    private void showPairingScanFailure(String reason) {
        PairingScanRecoveryDialog.show(this, reason, this::startQrScan);
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
        return button;
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
        params.setMargins(0, dp(8), 0, 0);
        return params;
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    private void applySystemBarInsets(View insetHost) {
        ViewCompat.setOnApplyWindowInsetsListener(insetHost, (view, windowInsets) -> {
            Insets statusBars = windowInsets.getInsets(WindowInsetsCompat.Type.statusBars());
            Insets displayCutout = windowInsets.getInsets(WindowInsetsCompat.Type.displayCutout());
            Insets navigationBars = windowInsets.getInsets(
                    WindowInsetsCompat.Type.navigationBars());
            int topInset = AppWindowInsetsPolicy.topContentInset(
                    statusBars.top, displayCutout.top);
            int bottomInset = AppWindowInsetsPolicy.bottomContentInset(
                    navigationRail, navigationBars.bottom);
            view.setPadding(view.getPaddingLeft(), topInset,
                    view.getPaddingRight(), bottomInset);
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

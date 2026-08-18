package com.colorvision.xcviewer;

import android.Manifest;
import android.content.ClipData;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.Settings;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.ImageButton;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.core.content.FileProvider;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.widget.TextViewCompat;

import com.google.android.material.bottomnavigation.BottomNavigationView;
import com.google.android.material.button.MaterialButton;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.progressindicator.LinearProgressIndicator;

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
    private static final int REQUEST_QR_SCAN = 1001;
    private static final int REQUEST_AUDIO_PICK = 1003;
    private static final int REQUEST_INSTALL_PERMISSION = 1004;
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
    private MusicPlayerController musicController;
    private TextView headerTitle;
    private TextView headerSubtitle;
    private BottomNavigationView bottomNavigation;
    private boolean updatingBottomNavigation;
    private boolean openedFromOperations;
    private int currentTab = TAB_OPERATIONS;
    private boolean cameraPermissionGranted;
    private final ExecutorService appUpdateExecutor = Executors.newSingleThreadExecutor();
    private boolean appUpdateInFlight;
    private File pendingInstallFile;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        WindowCompat.setDecorFitsSystemWindows(getWindow(), false);

        appPreferences = new AppPreferences(this);
        cameraPermissionGranted = hasCameraPermission();
        int startTab = consumeStartTab(getIntent());
        openedFromOperations = getIntent().getBooleanExtra(EXTRA_FROM_OPERATIONS, false);
        if (openedFromOperations && startTab == TAB_SETTINGS) {
            AppScreenMotion.configureSettingsActivity(this);
        }
        if (AppNavigationPolicy.shouldOpenOperationsDirectly(
                appPreferences.hasOperationsProfile(), startTab == TAB_OPERATIONS)) {
            openOperationsDirectly();
            return;
        }
        themeManager = new ThemeManager(this, appPreferences);
        musicController = new MusicPlayerController(this, appPreferences, this::chooseAudioFile);
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

    private int borderColor() {
        return themeManager.borderColor();
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
        nav.getMenu().add(0, NAV_SETTINGS, 1, "设置").setIcon(R.drawable.ic_person_24);
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

    private ImageButton makeTopIconButton(int iconRes) {
        ImageButton button = new ImageButton(this);
        button.setImageResource(iconRes);
        button.setColorFilter(primaryTextColor());
        button.setBackground(oval(cardBackgroundColor(), borderColor(), 1));
        button.setPadding(dp(10), dp(10), dp(10), dp(10));
        button.setScaleType(ImageView.ScaleType.CENTER);
        return button;
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
                finishAfterTransition();
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

        TextView status = makeBodyText("扫描电脑端“选项 > 局域网控制”中的短时安全配对码。完成一次配对后，运维伴侣会成为首屏并持续守护安全连接。 ");
        status.setPadding(0, dp(8), 0, dp(4));
        operationsCard.addView(status, matchWidthWrapParams());

        Button operationsButton = makePrimaryButton("扫描并连接电脑");
        operationsButton.setOnClickListener(v -> openOperations());
        operationsCard.addView(operationsButton, fullWidthButtonParams());

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
        content.setPadding(0, dp(10), 0, dp(28));
        scrollView.addView(content, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT,
                ScrollView.LayoutParams.WRAP_CONTENT));

        content.addView(createProfileHeader(), matchWidthWrapParams());

        LinearLayout connectionSection = makeSettingsSection();
        content.addView(connectionSection, settingsSectionParams());
        boolean paired = appPreferences.hasOperationsProfile();
        int pairedComputerCount = appPreferences.getOperationsProfileCount();
        addSettingsRow(connectionSection, "现场运维",
                paired ? "当前 " + appPreferences.getActiveOperationsProfileLabel()
                        + " · 共 " + pairedComputerCount + " 台" : "尚未配对",
                v -> openOperations());
        addSettingsRow(connectionSection, "安全通道",
                paired ? "设备密钥 + TLS 证书固定" : "等待安全配对",
                null);
        addSettingsRow(connectionSection, paired ? "添加电脑" : "连接电脑", "扫描二维码", v -> startQrScan());

        LinearLayout permissionSection = makeSettingsSection();
        content.addView(permissionSection, settingsSectionParams());
        addSettingsRow(permissionSection, "相机权限", cameraPermissionStatus(), v -> startQrScan());
        addSettingsRow(permissionSection, "网络权限", "已配置", null);
        addSettingsRow(permissionSection, "音乐权限", "选择单曲授权", v -> chooseAudioFile());

        LinearLayout appSection = makeSettingsSection();
        content.addView(appSection, settingsSectionParams());
        addSettingsRow(appSection, "音乐播放", musicController.getSavedAudioTitle(), v -> chooseAudioFile());
        addSettingsRow(appSection, "主题模式", getThemeModeLabel(), v -> showThemeDialog());
        addSettingsRow(appSection, "应用更新", "当前 " + getAppVersionName() + " · 签名校验", v -> checkForAppUpdate());

        LinearLayout actionSection = makeSettingsSection();
        content.addView(actionSection, settingsSectionParams());
        addSettingsRow(actionSection, "打开现场运维", "", v -> openOperations());

        return scrollView;
    }

    private LinearLayout createProfileHeader() {
        LinearLayout header = new LinearLayout(this);
        header.setOrientation(LinearLayout.HORIZONTAL);
        header.setGravity(Gravity.CENTER_VERTICAL);
        header.setPadding(dp(22), dp(22), dp(22), dp(22));
        header.setBackgroundColor(cardBackgroundColor());

        TextView avatar = new TextView(this);
        avatar.setText("CV");
        avatar.setTextColor(themeManager.onPrimaryColor());
        avatar.setTextSize(18);
        avatar.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        avatar.setGravity(Gravity.CENTER);
        avatar.setBackground(oval(themeManager.primaryColor(), Color.TRANSPARENT, 0));
        header.addView(avatar, new LinearLayout.LayoutParams(dp(56), dp(56)));

        LinearLayout textBlock = new LinearLayout(this);
        textBlock.setOrientation(LinearLayout.VERTICAL);
        textBlock.setPadding(dp(14), 0, dp(8), 0);
        header.addView(textBlock, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        TextView title = makeTitle("ColorVision 移动端", 22);
        textBlock.addView(title, matchWidthWrapParams());

        TextView subtitle = makeBodyText(appPreferences.hasOperationsProfile()
                ? "现场运维已配对，启动时自动连接"
                : "尚未连接现场运维电脑");
        subtitle.setPadding(0, dp(4), 0, 0);
        textBlock.addView(subtitle, matchWidthWrapParams());

        ImageButton scanButton = makeTopIconButton(R.drawable.ic_qr_code_scanner_24);
        scanButton.setContentDescription("扫描二维码");
        scanButton.setOnClickListener(v -> startQrScan());
        header.addView(scanButton, new LinearLayout.LayoutParams(dp(44), dp(44)));
        return header;
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

    private void addSettingsRow(LinearLayout parent, String label, String value, View.OnClickListener listener) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(dp(22), 0, dp(18), 0);
        row.setMinimumHeight(dp(58));
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
        row.addView(labelView, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        TextView valueView = new TextView(this);
        valueView.setText(value == null ? "" : value);
        TextViewCompat.setTextAppearance(valueView, com.google.android.material.R.style.TextAppearance_Material3_BodyMedium);
        valueView.setTextColor(mutedTextColor());
        valueView.setGravity(Gravity.END);
        valueView.setSingleLine(false);
        row.addView(valueView, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1.35f));

        if (listener != null) {
            ImageView arrow = new ImageView(this);
            arrow.setImageResource(R.drawable.ic_chevron_right_24);
            arrow.setColorFilter(inactiveTabColor());
            arrow.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            row.addView(arrow, new LinearLayout.LayoutParams(dp(24), dp(24)));
        }

        parent.addView(row, matchWidthWrapParams());

        View divider = new View(this);
        divider.setBackgroundColor(dividerColor());
        LinearLayout.LayoutParams dividerParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                1);
        dividerParams.setMargins(dp(22), 0, 0, 0);
        parent.addView(divider, dividerParams);
    }

    private LinearLayout.LayoutParams settingsSectionParams() {
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        params.setMargins(0, dp(10), 0, 0);
        return params;
    }

    private void chooseAudioFile() {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("audio/*");
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
        try {
            startActivityForResult(intent, REQUEST_AUDIO_PICK);
        } catch (Exception ex) {
            Toast.makeText(this, "没有可用的音乐选择器", Toast.LENGTH_LONG).show();
        }
    }

    private void handlePickedAudio(Intent data) {
        Uri uri = data.getData();
        if (uri == null) {
            Toast.makeText(this, "没有读取到音乐文件", Toast.LENGTH_SHORT).show();
            return;
        }

        int readFlags = data.getFlags() & Intent.FLAG_GRANT_READ_URI_PERMISSION;
        if (readFlags != 0) {
            try {
                getContentResolver().takePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION);
            } catch (SecurityException ignored) {
            }
        }

        String title = AudioFiles.getDisplayName(this, uri);
        musicController.setAudio(uri, title, true);
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

        if (requestCode == REQUEST_AUDIO_PICK) {
            if (resultCode == RESULT_OK && data != null && data.getData() != null) {
                handlePickedAudio(data);
                return;
            }
            Toast.makeText(this, "已取消选择音乐", Toast.LENGTH_SHORT).show();
            return;
        }

        super.onActivityResult(requestCode, resultCode, data);
    }

    @Override
    protected void onResume() {
        super.onResume();
        boolean granted = hasCameraPermission();
        if (appPreferences != null && (granted
                || shouldShowRequestPermissionRationale(Manifest.permission.CAMERA))) {
            appPreferences.saveCameraPermissionBlocked(false);
        }
        if (root != null && currentTab == TAB_SETTINGS && granted != cameraPermissionGranted) {
            showProfileView();
        }
        cameraPermissionGranted = granted;
    }

    private void openOperations() {
        if (appPreferences.hasOperationsProfile()) {
            openOperationsDirectly();
        } else {
            Toast.makeText(this, "请扫描电脑端现场运维配对码", Toast.LENGTH_SHORT).show();
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
        if (OperationsPairingPayload.isPairingInput(text)) {
            Intent operations = new Intent(this, OperationsActivity.class);
            operations.putExtra(OperationsActivity.EXTRA_PAIRING_PAYLOAD, text);
            startActivity(operations);
            finish();
            return;
        }

        Toast.makeText(this, "请扫描电脑端的安全运维配对码", Toast.LENGTH_LONG).show();
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

    private GradientDrawable oval(int fillColor, int strokeColor, int strokeWidth) {
        GradientDrawable drawable = new GradientDrawable();
        drawable.setShape(GradientDrawable.OVAL);
        drawable.setColor(fillColor);
        if (strokeWidth > 0) {
            drawable.setStroke(strokeWidth, strokeColor);
        }
        return drawable;
    }

    private LinearLayout.LayoutParams topIconParams() {
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(dp(46), dp(46));
        params.setMargins(dp(8), 0, 0, 0);
        return params;
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
    protected void onDestroy() {
        appUpdateExecutor.shutdownNow();
        if (musicController != null) {
            musicController.release();
        }
        super.onDestroy();
    }
}

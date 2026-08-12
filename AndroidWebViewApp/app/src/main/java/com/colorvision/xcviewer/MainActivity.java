package com.colorvision.xcviewer;

import android.annotation.SuppressLint;
import android.Manifest;
import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.webkit.PermissionRequest;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.ImageButton;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

public class MainActivity extends Activity {
    static final String EXTRA_START_TAB = "start_tab";
    private static final int REQUEST_QR_SCAN = 1001;
    private static final int REQUEST_WEB_CAMERA_PERMISSION = 1002;
    private static final int REQUEST_AUDIO_PICK = 1003;
    static final int TAB_OPERATIONS = 0;
    static final int TAB_DOWNLOADS = 1;
    static final int TAB_SETTINGS = 2;

    private FrameLayout root;
    private LinearLayout appShell;
    private FrameLayout setupContainer;
    private WebView homeWebView;
    private ProgressBar progressBar;
    private AppPreferences appPreferences;
    private ThemeManager themeManager;
    private MusicPlayerController musicController;
    private PermissionRequest pendingWebCameraRequest;
    private TextView headerTitle;
    private TextView headerSubtitle;
    private ImageView deviceTabIcon;
    private ImageView homeTabIcon;
    private ImageView profileTabIcon;
    private TextView deviceTabLabel;
    private TextView homeTabLabel;
    private TextView profileTabLabel;
    private int currentTab = TAB_OPERATIONS;
    private String currentHomeUrl = "";

    @SuppressLint("SetJavaScriptEnabled")
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        appPreferences = new AppPreferences(this);
        int startTab = consumeStartTab(getIntent());
        if (AppNavigationPolicy.shouldOpenOperationsDirectly(
                appPreferences.hasOperationsProfile(), startTab == TAB_OPERATIONS)) {
            openOperationsDirectly();
            return;
        }
        themeManager = new ThemeManager(this, appPreferences);
        musicController = new MusicPlayerController(this, appPreferences, this::chooseAudioFile);
        themeManager.applySystemBars(this);

        root = new FrameLayout(this);
        homeWebView = new WebView(this);
        setupContainer = new FrameLayout(this);
        appShell = createAppShell();
        progressBar = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);

        root.addView(appShell, matchParentParams());
        root.addView(progressBar, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.WRAP_CONTENT));

        setContentView(root);
        configureHomeWebView();

        showInitialTab(startTab);
    }

    private void configureHomeWebView() {
        configureWebSettings(homeWebView);

        homeWebView.setWebChromeClient(new WebChromeClient() {
            @Override
            public void onProgressChanged(WebView view, int newProgress) {
                progressBar.setProgress(newProgress);
                progressBar.setVisibility(newProgress >= 100 ? View.GONE : View.VISIBLE);
            }

            @Override
            public void onPermissionRequest(PermissionRequest request) {
                runOnUiThread(() -> handleWebPermissionRequest(request));
            }

            @Override
            public void onPermissionRequestCanceled(PermissionRequest request) {
                runOnUiThread(() -> {
                    if (pendingWebCameraRequest == request) {
                        pendingWebCameraRequest = null;
                    }
                });
            }
        });

        homeWebView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageStarted(WebView view, String url, Bitmap favicon) {
                if (!handleSpecialUrl(url)) {
                    progressBar.setVisibility(View.VISIBLE);
                }
            }

            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                String url = request.getUrl().toString();
                if (handleSpecialUrl(url)) {
                    return true;
                }
                view.loadUrl(url);
                return true;
            }

            @Override
            public boolean shouldOverrideUrlLoading(WebView view, String url) {
                if (handleSpecialUrl(url)) {
                    return true;
                }
                view.loadUrl(url);
                return true;
            }

            @Override
            public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
                if (request.isForMainFrame()) {
                    showHomeErrorView();
                }
            }
        });
    }

    private void configureWebSettings(WebView targetWebView) {
        WebSettings settings = targetWebView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
    }

    private void handleWebPermissionRequest(PermissionRequest request) {
        if (!isVideoCaptureOnly(request) || !isTrustedCameraOrigin(request.getOrigin())) {
            request.deny();
            return;
        }

        if (hasCameraPermission()) {
            request.grant(new String[]{PermissionRequest.RESOURCE_VIDEO_CAPTURE});
            return;
        }

        if (pendingWebCameraRequest != null && pendingWebCameraRequest != request) {
            pendingWebCameraRequest.deny();
        }
        pendingWebCameraRequest = request;
        requestPermissions(new String[]{Manifest.permission.CAMERA}, REQUEST_WEB_CAMERA_PERMISSION);
    }

    private boolean isTrustedCameraOrigin(Uri origin) {
        return origin != null
                && ("https".equalsIgnoreCase(origin.getScheme()) || "http".equalsIgnoreCase(origin.getScheme()))
                && "xc213618.ddns.me".equalsIgnoreCase(origin.getHost());
    }

    private boolean isVideoCaptureOnly(PermissionRequest request) {
        String[] resources = request.getResources();
        if (resources == null || resources.length == 0) {
            return false;
        }

        for (String resource : resources) {
            if (!PermissionRequest.RESOURCE_VIDEO_CAPTURE.equals(resource)) {
                return false;
            }
        }
        return true;
    }

    private boolean hasCameraPermission() {
        return checkSelfPermission(Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED;
    }

    private void showThemeDialog() {
        themeManager.showThemeDialog(this, TAB_SETTINGS, this::recreate);
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

    private int secondaryButtonBackgroundColor() {
        return themeManager.secondaryButtonBackgroundColor();
    }

    private LinearLayout createAppShell() {
        LinearLayout shell = new LinearLayout(this);
        shell.setOrientation(LinearLayout.VERTICAL);
        shell.setBackgroundColor(shellBackgroundColor());
        shell.addView(createTopBar(), new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                getStatusBarHeight() + dp(48)));

        shell.addView(setupContainer, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                0,
                1));

        shell.addView(createBottomNav(), new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                dp(56)));
        return shell;
    }

    private LinearLayout createTopBar() {
        LinearLayout bar = new LinearLayout(this);
        bar.setOrientation(LinearLayout.HORIZONTAL);
        bar.setGravity(Gravity.CENTER_VERTICAL);
        bar.setPadding(dp(18), getStatusBarHeight() + dp(2), dp(14), dp(2));
        bar.setBackgroundColor(shellBackgroundColor());

        LinearLayout titleBlock = new LinearLayout(this);
        titleBlock.setOrientation(LinearLayout.VERTICAL);
        titleBlock.setGravity(Gravity.CENTER_VERTICAL);
        bar.addView(titleBlock, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));

        headerTitle = new TextView(this);
        headerTitle.setText("ColorVision");
        headerTitle.setTextColor(Color.rgb(21, 152, 204));
        headerTitle.setTextSize(20);
        headerTitle.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        titleBlock.addView(headerTitle, matchWidthWrapParams());

        headerSubtitle = new TextView(this);
        headerSubtitle.setText("现场运维与固定下载站");
        headerSubtitle.setTextColor(secondaryTextColor());
        headerSubtitle.setTextSize(11);
        titleBlock.addView(headerSubtitle, matchWidthWrapParams());

        return bar;
    }

    private LinearLayout createBottomNav() {
        LinearLayout nav = new LinearLayout(this);
        nav.setOrientation(LinearLayout.HORIZONTAL);
        nav.setGravity(Gravity.CENTER);
        nav.setPadding(dp(16), dp(3), dp(16), dp(4));
        nav.setBackgroundColor(bottomNavBackgroundColor());
        nav.setElevation(dp(10));

        nav.addView(createBottomNavItem(R.drawable.ic_devices_24, "运维", TAB_OPERATIONS), new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));
        nav.addView(createBottomNavItem(R.drawable.ic_home_24, "下载站", TAB_DOWNLOADS), new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));
        nav.addView(createBottomNavItem(R.drawable.ic_person_24, "设置", TAB_SETTINGS), new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));
        return nav;
    }

    private LinearLayout createBottomNavItem(int iconRes, String label, int tab) {
        LinearLayout item = new LinearLayout(this);
        item.setOrientation(LinearLayout.VERTICAL);
        item.setGravity(Gravity.CENTER);
        item.setOnClickListener(v -> {
            if (tab == TAB_OPERATIONS) {
                showOperationsLanding();
            } else if (tab == TAB_DOWNLOADS) {
                showHomePage();
            } else {
                showProfileView();
            }
        });

        ImageView icon = new ImageView(this);
        icon.setImageResource(iconRes);
        item.addView(icon, new LinearLayout.LayoutParams(dp(22), dp(22)));

        TextView text = new TextView(this);
        text.setText(label);
        text.setTextSize(11);
        text.setGravity(Gravity.CENTER);
        text.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        item.addView(text, wrapParams());

        if (tab == TAB_OPERATIONS) {
            deviceTabIcon = icon;
            deviceTabLabel = text;
        } else if (tab == TAB_DOWNLOADS) {
            homeTabIcon = icon;
            homeTabLabel = text;
        } else {
            profileTabIcon = icon;
            profileTabLabel = text;
        }
        return item;
    }

    private void selectTab(int tab) {
        currentTab = tab;
        setTabSelected(deviceTabIcon, deviceTabLabel, tab == TAB_OPERATIONS);
        setTabSelected(homeTabIcon, homeTabLabel, tab == TAB_DOWNLOADS);
        setTabSelected(profileTabIcon, profileTabLabel, tab == TAB_SETTINGS);
    }

    private void setTabSelected(ImageView icon, TextView label, boolean selected) {
        if (icon == null || label == null) {
            return;
        }

        int color = selected ? primaryTextColor() : inactiveTabColor();
        icon.setColorFilter(color);
        label.setTextColor(color);
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
        return requestedTab >= TAB_OPERATIONS && requestedTab <= TAB_SETTINGS
                ? requestedTab : appPreferences.consumeStartTab(TAB_OPERATIONS);
    }

    private void showInitialTab(int startTab) {
        if (startTab == TAB_SETTINGS) {
            showProfileView();
            return;
        }
        if (startTab == TAB_DOWNLOADS) {
            showHomePage();
            return;
        }

        showOperationsLanding();
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        int startTab = consumeStartTab(intent);
        if (AppNavigationPolicy.shouldOpenOperationsDirectly(
                appPreferences.hasOperationsProfile(), startTab == TAB_OPERATIONS)) {
            openOperationsDirectly();
            return;
        }
        showInitialTab(startTab);
    }

    private void showOperationsLanding() {
        if (appPreferences.hasOperationsProfile()) {
            openOperations();
            return;
        }
        selectTab(TAB_OPERATIONS);
        headerTitle.setText("现场运维");
        headerSubtitle.setText("扫描电脑端安全配对码");
        setupContainer.removeAllViews();
        setupContainer.addView(createOperationsLandingContent(), matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        progressBar.setVisibility(View.GONE);
    }

    private void showHomePage() {
        selectTab(TAB_DOWNLOADS);
        headerTitle.setText("固定下载站");
        headerSubtitle.setText("应用内置地址 · 自动加载");
        setupContainer.removeAllViews();
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        progressBar.setVisibility(View.GONE);

        setupContainer.addView(homeWebView, matchParentParams());
        if (!AppNavigationPolicy.FIXED_DOWNLOAD_URL.equals(currentHomeUrl) || homeWebView.getUrl() == null) {
            currentHomeUrl = AppNavigationPolicy.FIXED_DOWNLOAD_URL;
            homeWebView.loadUrl(AppNavigationPolicy.FIXED_DOWNLOAD_URL);
        }
    }

    private void showHomeErrorView() {
        if (currentTab != TAB_DOWNLOADS) {
            return;
        }

        setupContainer.removeAllViews();
        setupContainer.addView(createHomeErrorContent(), matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        progressBar.setVisibility(View.GONE);
    }

    private ScrollView createHomeErrorContent() {
        ScrollView scrollView = new ScrollView(this);
        scrollView.setFillViewport(true);
        scrollView.setBackgroundColor(pageBackgroundColor());

        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setGravity(Gravity.CENTER_VERTICAL);
        content.setPadding(dp(22), dp(24), dp(22), dp(24));
        scrollView.addView(content, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT,
                ScrollView.LayoutParams.MATCH_PARENT));

        LinearLayout card = makeCard();
        content.addView(card, matchWidthWrapParams());
        card.addView(makeTitle("下载站加载失败", 22), matchWidthWrapParams());
        TextView body = makeBodyText("请确认手机网络正常，或稍后再试。");
        body.setPadding(0, dp(10), 0, dp(6));
        card.addView(body, matchWidthWrapParams());

        Button retryButton = makePrimaryButton("重新加载固定下载站");
        retryButton.setOnClickListener(v -> showHomePage());
        card.addView(retryButton, fullWidthButtonParams());

        Button deviceButton = makeSecondaryButton("返回现场运维");
        deviceButton.setOnClickListener(v -> showOperationsLanding());
        card.addView(deviceButton, fullWidthButtonParams());
        return scrollView;
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
        addSettingsRow(connectionSection, "现场运维",
                paired ? "已配对 · 自动连接" : "尚未配对",
                v -> openOperations());
        addSettingsRow(connectionSection, "安全通道",
                paired ? "设备密钥 + TLS 证书固定" : "等待安全配对",
                null);
        addSettingsRow(connectionSection, paired ? "重新配对" : "连接电脑", "扫描二维码", v -> startQrScan());

        LinearLayout permissionSection = makeSettingsSection();
        content.addView(permissionSection, settingsSectionParams());
        addSettingsRow(permissionSection, "相机权限", hasCameraPermission() ? "已授权" : "需要时申请", v -> startQrScan());
        addSettingsRow(permissionSection, "网络权限", "已配置", null);
        addSettingsRow(permissionSection, "音乐权限", "选择单曲授权", v -> chooseAudioFile());

        LinearLayout appSection = makeSettingsSection();
        content.addView(appSection, settingsSectionParams());
        addSettingsRow(appSection, "固定下载站", "应用内置 · 无网址选项", v -> showHomePage());
        addSettingsRow(appSection, "音乐播放", musicController.getSavedAudioTitle(), v -> chooseAudioFile());
        addSettingsRow(appSection, "主题模式", getThemeModeLabel(), v -> showThemeDialog());
        addSettingsRow(appSection, "应用版本", getAppVersionName(), null);

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
        avatar.setTextColor(Color.WHITE);
        avatar.setTextSize(18);
        avatar.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        avatar.setGravity(Gravity.CENTER);
        avatar.setBackground(oval(Color.rgb(21, 152, 204), Color.TRANSPARENT, 0));
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
        }

        TextView labelView = new TextView(this);
        labelView.setText(label);
        labelView.setTextColor(primaryTextColor());
        labelView.setTextSize(16);
        row.addView(labelView, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        TextView valueView = new TextView(this);
        valueView.setText(value == null ? "" : value);
        valueView.setTextColor(mutedTextColor());
        valueView.setTextSize(14);
        valueView.setGravity(Gravity.RIGHT);
        valueView.setSingleLine(false);
        row.addView(valueView, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1.35f));

        TextView arrow = new TextView(this);
        arrow.setText(listener == null ? "" : "›");
        arrow.setTextColor(inactiveTabColor());
        arrow.setTextSize(28);
        arrow.setGravity(Gravity.CENTER);
        row.addView(arrow, new LinearLayout.LayoutParams(dp(28), LinearLayout.LayoutParams.WRAP_CONTENT));

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
        startActivityForResult(new Intent(this, QrScanActivity.class), REQUEST_QR_SCAN);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == REQUEST_QR_SCAN) {
            if (resultCode == RESULT_OK && data != null) {
                String result = data.getStringExtra(QrScanActivity.EXTRA_QR_RESULT);
                saveAndOpen(result);
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

    private void openOperations() {
        if (appPreferences.hasOperationsProfile()) {
            openOperationsDirectly();
        } else {
            Toast.makeText(this, "请扫描电脑端现场运维配对码", Toast.LENGTH_SHORT).show();
            startQrScan();
        }
    }

    private void openOperationsDirectly() {
        OperationsWatchService.start(this);
        startActivity(new Intent(this, OperationsActivity.class)
                .addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP));
        finish();
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != REQUEST_WEB_CAMERA_PERMISSION) {
            return;
        }

        PermissionRequest request = pendingWebCameraRequest;
        pendingWebCameraRequest = null;
        if (request == null) {
            return;
        }

        if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
            request.grant(new String[]{PermissionRequest.RESOURCE_VIDEO_CAPTURE});
            return;
        }

        request.deny();
        Toast.makeText(this, "没有相机权限，网页无法使用摄像头", Toast.LENGTH_LONG).show();
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

        Toast.makeText(this, "请扫描电脑端的安全运维配对码；下载站地址由应用固定管理", Toast.LENGTH_LONG).show();
    }

    private boolean handleSpecialUrl(String url) {
        if (url == null) {
            return false;
        }

        Uri uri;
        try {
            uri = Uri.parse(url);
        } catch (Exception ex) {
            return false;
        }

        if (!"cvapp".equalsIgnoreCase(uri.getScheme())) {
            return false;
        }

        String host = uri.getHost();
        if ("connections".equalsIgnoreCase(host)) {
            openOperations();
            return true;
        }

        if ("disconnect".equalsIgnoreCase(host)) {
            Toast.makeText(this, "请在运维伴侣中管理安全配对", Toast.LENGTH_SHORT).show();
            openOperations();
            return true;
        }

        return true;
    }

    private LinearLayout makeCard() {
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setPadding(dp(18), dp(18), dp(18), dp(18));
        card.setBackground(rounded(cardBackgroundColor(), dp(16), borderColor(), 1));
        return card;
    }

    private TextView makeTitle(String text, int size) {
        TextView title = new TextView(this);
        title.setText(text);
        title.setTextColor(primaryTextColor());
        title.setTextSize(size);
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        title.setGravity(Gravity.LEFT);
        return title;
    }

    private TextView makeBodyText(String text) {
        TextView body = new TextView(this);
        body.setText(text);
        body.setTextColor(secondaryTextColor());
        body.setTextSize(14);
        body.setLineSpacing(0, 1.12f);
        return body;
    }

    private Button makePrimaryButton(String text) {
        Button button = makeBaseButton(text);
        button.setTextColor(Color.WHITE);
        button.setBackground(rounded(Color.rgb(31, 111, 235), dp(10), Color.TRANSPARENT, 0));
        return button;
    }

    private Button makeSecondaryButton(String text) {
        Button button = makeBaseButton(text);
        button.setTextColor(primaryTextColor());
        button.setBackground(rounded(secondaryButtonBackgroundColor(), dp(10), Color.TRANSPARENT, 0));
        return button;
    }

    private Button makeBaseButton(String text) {
        Button button = new Button(this);
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

    private LinearLayout.LayoutParams wrapParams() {
        return new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
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

    private int getStatusBarHeight() {
        int resourceId = getResources().getIdentifier("status_bar_height", "dimen", "android");
        return resourceId > 0 ? getResources().getDimensionPixelSize(resourceId) : dp(24);
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        super.onSaveInstanceState(outState);
        if (homeWebView != null) {
            homeWebView.saveState(outState);
        }
    }

    @Override
    public void onBackPressed() {
        if (currentTab == TAB_DOWNLOADS && homeWebView != null && homeWebView.canGoBack()) {
            homeWebView.goBack();
            return;
        }

        super.onBackPressed();
    }

    @Override
    protected void onDestroy() {
        if (musicController != null) {
            musicController.release();
        }
        if (homeWebView != null) {
            homeWebView.destroy();
        }
        super.onDestroy();
    }
}

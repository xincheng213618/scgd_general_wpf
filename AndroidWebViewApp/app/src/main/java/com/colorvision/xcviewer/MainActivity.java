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
import android.os.Handler;
import android.os.Looper;
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
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.ImageButton;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;

public class MainActivity extends Activity {
    private static final String HOME_URL = "http://xc213618.ddns.me:9998/";
    private static final int REQUEST_QR_SCAN = 1001;
    private static final int REQUEST_WEB_CAMERA_PERMISSION = 1002;
    private static final int REQUEST_AUDIO_PICK = 1003;
    private static final int TAB_DEVICES = 0;
    private static final int TAB_HOME = 1;
    private static final int TAB_PROFILE = 2;

    private FrameLayout root;
    private LinearLayout appShell;
    private FrameLayout setupContainer;
    private LinearLayout errorView;
    private WebView webView;
    private WebView homeWebView;
    private ProgressBar progressBar;
    private Button manageFloatingButton;
    private AppPreferences appPreferences;
    private ThemeManager themeManager;
    private MusicPlayerController musicController;
    private Handler mainHandler;
    private int pageLoadGeneration;
    private String currentLanUrl = "";
    private PermissionRequest pendingWebCameraRequest;
    private TextView headerTitle;
    private TextView headerSubtitle;
    private ImageView deviceTabIcon;
    private ImageView homeTabIcon;
    private ImageView profileTabIcon;
    private TextView deviceTabLabel;
    private TextView homeTabLabel;
    private TextView profileTabLabel;
    private int currentTab = TAB_DEVICES;
    private String currentHomeUrl = "";

    private TextView dashboardStatusText;
    private TextView dashboardMachineText;
    private TextView dashboardEndpointText;
    private TextView dashboardVersionText;
    private TextView dashboardRuntimeText;
    private TextView dashboardMemoryText;
    private TextView dashboardWindowText;
    private TextView dashboardAddressText;
    private TextView dashboardLogText;

    @SuppressLint("SetJavaScriptEnabled")
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        appPreferences = new AppPreferences(this);
        themeManager = new ThemeManager(this, appPreferences);
        musicController = new MusicPlayerController(this, appPreferences, this::chooseAudioFile);
        mainHandler = new Handler(Looper.getMainLooper());
        themeManager.applySystemBars(this);

        root = new FrameLayout(this);
        webView = new WebView(this);
        homeWebView = new WebView(this);
        setupContainer = new FrameLayout(this);
        appShell = createAppShell();
        errorView = createErrorView();
        progressBar = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
        manageFloatingButton = createManageFloatingButton();

        root.addView(appShell, matchParentParams());
        root.addView(webView, matchParentParams());
        root.addView(errorView, matchParentParams());
        root.addView(progressBar, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.WRAP_CONTENT));
        FrameLayout.LayoutParams manageParams = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.WRAP_CONTENT,
                dp(42),
                Gravity.TOP | Gravity.RIGHT);
        manageParams.setMargins(0, dp(18), dp(14), 0);
        root.addView(manageFloatingButton, manageParams);

        setContentView(root);
        configureWebView();
        configureHomeWebView();
        webView.setVisibility(View.GONE);

        showInitialTab();
    }

    private void configureWebView() {
        configureWebSettings(webView);

        webView.setWebChromeClient(new WebChromeClient() {
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

        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageStarted(WebView view, String url, Bitmap favicon) {
                if (!handleSpecialUrl(url)) {
                    errorView.setVisibility(View.GONE);
                    appShell.setVisibility(View.GONE);
                    webView.setVisibility(View.VISIBLE);
                    startPageLoadTimeout(url);
                }
            }

            @Override
            public void onPageFinished(WebView view, String url) {
                pageLoadGeneration++;
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
                    pageLoadGeneration++;
                    showErrorView();
                }
            }
        });
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
                    errorView.setVisibility(View.GONE);
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
        themeManager.showThemeDialog(this, TAB_PROFILE, this::recreate);
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

    private int inputBackgroundColor() {
        return themeManager.inputBackgroundColor();
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
                getStatusBarHeight() + dp(98)));

        shell.addView(setupContainer, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                0,
                1));

        shell.addView(createBottomNav(), new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                dp(78)));
        return shell;
    }

    private LinearLayout createTopBar() {
        LinearLayout bar = new LinearLayout(this);
        bar.setOrientation(LinearLayout.HORIZONTAL);
        bar.setGravity(Gravity.CENTER_VERTICAL);
        bar.setPadding(dp(22), getStatusBarHeight() + dp(8), dp(18), dp(8));
        bar.setBackgroundColor(shellBackgroundColor());

        LinearLayout titleBlock = new LinearLayout(this);
        titleBlock.setOrientation(LinearLayout.VERTICAL);
        titleBlock.setGravity(Gravity.CENTER_VERTICAL);
        bar.addView(titleBlock, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));

        headerTitle = new TextView(this);
        headerTitle.setText("ColorVision");
        headerTitle.setTextColor(Color.rgb(21, 152, 204));
        headerTitle.setTextSize(28);
        headerTitle.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        titleBlock.addView(headerTitle, matchWidthWrapParams());

        headerSubtitle = new TextView(this);
        headerSubtitle.setText("移动检测控制台");
        headerSubtitle.setTextColor(secondaryTextColor());
        headerSubtitle.setTextSize(13);
        titleBlock.addView(headerSubtitle, matchWidthWrapParams());

        ImageButton scanButton = makeTopIconButton(R.drawable.ic_qr_code_scanner_24);
        scanButton.setContentDescription("扫描二维码");
        scanButton.setOnClickListener(v -> startQrScan());
        bar.addView(scanButton, topIconParams());

        ImageButton addButton = makeTopIconButton(R.drawable.ic_add_circle_outline_24);
        addButton.setContentDescription("添加连接");
        addButton.setOnClickListener(v -> showSetupView());
        bar.addView(addButton, topIconParams());
        return bar;
    }

    private LinearLayout createBottomNav() {
        LinearLayout nav = new LinearLayout(this);
        nav.setOrientation(LinearLayout.HORIZONTAL);
        nav.setGravity(Gravity.CENTER);
        nav.setPadding(dp(16), dp(6), dp(16), dp(8));
        nav.setBackgroundColor(bottomNavBackgroundColor());
        nav.setElevation(dp(10));

        nav.addView(createBottomNavItem(R.drawable.ic_devices_24, "设备", TAB_DEVICES), new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));
        nav.addView(createBottomNavItem(R.drawable.ic_home_24, "主页", TAB_HOME), new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));
        nav.addView(createBottomNavItem(R.drawable.ic_person_24, "我的", TAB_PROFILE), new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));
        return nav;
    }

    private LinearLayout createBottomNavItem(int iconRes, String label, int tab) {
        LinearLayout item = new LinearLayout(this);
        item.setOrientation(LinearLayout.VERTICAL);
        item.setGravity(Gravity.CENTER);
        item.setOnClickListener(v -> {
            if (tab == TAB_DEVICES) {
                String savedUrl = getSavedLanUrl();
                if (savedUrl.isEmpty()) {
                    showSetupView();
                } else {
                    showDashboard(savedUrl);
                }
            } else if (tab == TAB_HOME) {
                showHomePage();
            } else {
                showProfileView();
            }
        });

        ImageView icon = new ImageView(this);
        icon.setImageResource(iconRes);
        item.addView(icon, new LinearLayout.LayoutParams(dp(28), dp(28)));

        TextView text = new TextView(this);
        text.setText(label);
        text.setTextSize(13);
        text.setGravity(Gravity.CENTER);
        text.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        item.addView(text, wrapParams());

        if (tab == TAB_DEVICES) {
            deviceTabIcon = icon;
            deviceTabLabel = text;
        } else if (tab == TAB_HOME) {
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
        setTabSelected(deviceTabIcon, deviceTabLabel, tab == TAB_DEVICES);
        setTabSelected(homeTabIcon, homeTabLabel, tab == TAB_HOME);
        setTabSelected(profileTabIcon, profileTabLabel, tab == TAB_PROFILE);
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

    private LinearLayout createErrorView() {
        LinearLayout layout = new LinearLayout(this);
        layout.setOrientation(LinearLayout.VERTICAL);
        layout.setGravity(Gravity.CENTER);
        layout.setPadding(dp(24), dp(24), dp(24), dp(24));
        layout.setBackgroundColor(pageBackgroundColor());
        layout.setVisibility(View.GONE);

        TextView title = new TextView(this);
        title.setText("连接失败");
        title.setTextColor(primaryTextColor());
        title.setTextSize(24);
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        title.setGravity(Gravity.CENTER);
        layout.addView(title, wrapParams());

        TextView subtitle = new TextView(this);
        subtitle.setText("请确认手机和电脑在同一个局域网，并且电脑端已启用局域网控制。");
        subtitle.setTextColor(secondaryTextColor());
        subtitle.setTextSize(15);
        subtitle.setGravity(Gravity.CENTER);
        subtitle.setPadding(0, dp(10), 0, dp(18));
        layout.addView(subtitle, wrapParams());

        Button retryButton = makePrimaryButton("重试连接");
        retryButton.setOnClickListener(v -> {
            String url = getSavedLanUrl();
            if (url.isEmpty()) {
                showSetupView();
            } else {
                showDashboard(url);
            }
        });
        layout.addView(retryButton, fullWidthButtonParams());

        Button manageButton = makeSecondaryButton("管理连接");
        manageButton.setOnClickListener(v -> showSetupView());
        layout.addView(manageButton, fullWidthButtonParams());

        return layout;
    }

    private void showInitialTab() {
        int startTab = appPreferences.consumeStartTab(TAB_DEVICES);
        if (startTab == TAB_PROFILE) {
            showProfileView();
            return;
        }

        String savedUrl = getSavedLanUrl();
        if (savedUrl.isEmpty()) {
            showSetupView();
        } else {
            showDashboard(savedUrl);
        }
    }

    private void showSetupView() {
        currentLanUrl = "";
        selectTab(TAB_DEVICES);
        headerTitle.setText("ColorVision");
        headerSubtitle.setText("扫码或手动添加电脑端");
        setupContainer.removeAllViews();
        setupContainer.addView(createSetupContent(), matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        errorView.setVisibility(View.GONE);
        webView.setVisibility(View.GONE);
        manageFloatingButton.setVisibility(View.GONE);
        progressBar.setVisibility(View.GONE);
    }

    private void showHomePage() {
        selectTab(TAB_HOME);
        headerTitle.setText("主页");
        headerSubtitle.setText("ColorVision 网页");
        setupContainer.removeAllViews();
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        errorView.setVisibility(View.GONE);
        webView.setVisibility(View.GONE);
        manageFloatingButton.setVisibility(View.GONE);
        progressBar.setVisibility(View.GONE);

        setupContainer.addView(homeWebView, matchParentParams());
        if (!HOME_URL.equals(currentHomeUrl) || homeWebView.getUrl() == null) {
            currentHomeUrl = HOME_URL;
            homeWebView.loadUrl(HOME_URL);
        }
    }

    private void showHomeErrorView() {
        if (currentTab != TAB_HOME) {
            return;
        }

        setupContainer.removeAllViews();
        setupContainer.addView(createHomeErrorContent(), matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        errorView.setVisibility(View.GONE);
        webView.setVisibility(View.GONE);
        manageFloatingButton.setVisibility(View.GONE);
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
        card.addView(makeTitle("主页加载失败", 22), matchWidthWrapParams());
        TextView body = makeBodyText("请确认手机网络正常，或稍后再试。");
        body.setPadding(0, dp(10), 0, dp(6));
        card.addView(body, matchWidthWrapParams());

        Button retryButton = makePrimaryButton("重新加载主页");
        retryButton.setOnClickListener(v -> showHomePage());
        card.addView(retryButton, fullWidthButtonParams());

        Button deviceButton = makeSecondaryButton("查看设备状态");
        deviceButton.setOnClickListener(v -> {
            String savedUrl = getSavedLanUrl();
            if (savedUrl.isEmpty()) {
                showSetupView();
            } else {
                showDashboard(savedUrl);
            }
        });
        card.addView(deviceButton, fullWidthButtonParams());
        return scrollView;
    }

    private ScrollView createSetupContent() {
        ScrollView scrollView = new ScrollView(this);
        scrollView.setFillViewport(true);
        scrollView.setBackgroundColor(pageBackgroundColor());

        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setGravity(Gravity.CENTER_HORIZONTAL);
        content.setPadding(dp(22), dp(36), dp(22), dp(30));
        scrollView.addView(content, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT,
                ScrollView.LayoutParams.WRAP_CONTENT));

        TextView title = new TextView(this);
        title.setText("添加设备");
        title.setTextColor(primaryTextColor());
        title.setTextSize(25);
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        title.setGravity(Gravity.LEFT);
        content.addView(title, matchWidthWrapParams());

        TextView subtitle = new TextView(this);
        subtitle.setText("扫描电脑端二维码后，手机会保存这台电脑的局域网控制地址。");
        subtitle.setTextColor(secondaryTextColor());
        subtitle.setTextSize(15);
        subtitle.setGravity(Gravity.LEFT);
        subtitle.setPadding(0, dp(10), 0, dp(24));
        content.addView(subtitle, matchWidthWrapParams());

        LinearLayout card = makeCard();
        content.addView(card, fullWidthCardParams());

        String savedUrl = getSavedLanUrl();
        TextView cardTitle = makeTitle(savedUrl.isEmpty() ? "连接电脑端" : "当前连接", 19);
        card.addView(cardTitle, wrapParams());

        TextView cardText = makeBodyText(savedUrl.isEmpty()
                ? "在电脑端打开“选项 > 局域网控制”，启用后扫描二维码。"
                : savedUrl);
        cardText.setPadding(0, dp(8), 0, dp(14));
        card.addView(cardText, matchWidthWrapParams());

        Button scanButton = makePrimaryButton(savedUrl.isEmpty() ? "扫描二维码" : "重新扫描二维码");
        scanButton.setOnClickListener(v -> startQrScan());
        card.addView(scanButton, fullWidthButtonParams());

        if (!savedUrl.isEmpty()) {
            Button openButton = makeSecondaryButton("打开控制台");
            openButton.setOnClickListener(v -> showDashboard(savedUrl));
            card.addView(openButton, fullWidthButtonParams());

            Button disconnectButton = makeSecondaryButton("断开此电脑");
            disconnectButton.setOnClickListener(v -> {
                clearSavedLanUrl();
                Toast.makeText(this, "已断开当前电脑", Toast.LENGTH_SHORT).show();
                showSetupView();
            });
            card.addView(disconnectButton, fullWidthButtonParams());
        }

        EditText manualInput = new EditText(this);
        manualInput.setHint("请优先扫描电脑端短时安全配对码");
        manualInput.setSingleLine(false);
        manualInput.setMinLines(2);
        manualInput.setTextColor(primaryTextColor());
        manualInput.setHintTextColor(mutedTextColor());
        manualInput.setTextSize(14);
        manualInput.setBackground(rounded(inputBackgroundColor(), dp(10), borderColor(), 1));
        manualInput.setPadding(dp(12), dp(10), dp(12), dp(10));
        LinearLayout.LayoutParams inputParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        inputParams.setMargins(0, dp(14), 0, 0);
        card.addView(manualInput, inputParams);

        Button manualButton = makeSecondaryButton("连接手动地址");
        manualButton.setOnClickListener(v -> saveAndOpen(manualInput.getText().toString()));
        card.addView(manualButton, fullWidthButtonParams());

        Button webButton = makeSecondaryButton("打开下载站");
        webButton.setOnClickListener(v -> openUrl(HOME_URL));
        content.addView(webButton, fullWidthCardParams());

        return scrollView;
    }

    private void showDashboard(String url) {
        currentLanUrl = url;
        selectTab(TAB_DEVICES);
        headerTitle.setText("ColorVision");
        headerSubtitle.setText("设备在线状态与快捷控制");
        setupContainer.removeAllViews();
        setupContainer.addView(createDashboardContent(url), matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        errorView.setVisibility(View.GONE);
        webView.setVisibility(View.GONE);
        manageFloatingButton.setVisibility(View.GONE);
        progressBar.setVisibility(View.GONE);
        refreshDashboard();
    }

    private void showProfileView() {
        selectTab(TAB_PROFILE);
        headerTitle.setText("我的");
        headerSubtitle.setText("连接设置与应用信息");
        setupContainer.removeAllViews();
        setupContainer.addView(createProfileContent(), matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        appShell.setVisibility(View.VISIBLE);
        errorView.setVisibility(View.GONE);
        webView.setVisibility(View.GONE);
        manageFloatingButton.setVisibility(View.GONE);
        progressBar.setVisibility(View.GONE);
    }

    private ScrollView createDashboardContent(String url) {
        ScrollView scrollView = new ScrollView(this);
        scrollView.setFillViewport(false);
        scrollView.setBackgroundColor(pageBackgroundColor());

        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(18), dp(26), dp(18), dp(28));
        scrollView.addView(content, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT,
                ScrollView.LayoutParams.WRAP_CONTENT));

        TextView title = makeTitle("ColorVision 控制台", 26);
        content.addView(title, matchWidthWrapParams());

        TextView subtitle = makeBodyText(getBaseFromUrl(url));
        subtitle.setPadding(0, dp(6), 0, dp(14));
        content.addView(subtitle, matchWidthWrapParams());

        LinearLayout statusCard = makeCard();
        content.addView(statusCard, fullWidthCardParams());
        TextView statusTitle = makeTitle("当前连接", 19);
        statusCard.addView(statusTitle, matchWidthWrapParams());
        dashboardStatusText = makeBodyText("正在连接电脑端...");
        dashboardStatusText.setPadding(0, dp(8), 0, dp(10));
        statusCard.addView(dashboardStatusText, matchWidthWrapParams());
        dashboardMachineText = addMetricRow(statusCard, "电脑", "--");
        dashboardEndpointText = addMetricRow(statusCard, "地址", "--");
        dashboardVersionText = addMetricRow(statusCard, "版本", "--");

        LinearLayout metricsCard = makeCard();
        content.addView(metricsCard, fullWidthCardParams());
        metricsCard.addView(makeTitle("运行状态", 19), matchWidthWrapParams());
        dashboardRuntimeText = addMetricRow(metricsCard, "在线时间", "--");
        dashboardMemoryText = addMetricRow(metricsCard, "进程内存", "--");
        dashboardWindowText = addMetricRow(metricsCard, "主窗口", "--");
        dashboardAddressText = addMetricRow(metricsCard, "可用地址", "--");

        LinearLayout actionsCard = makeCard();
        content.addView(actionsCard, fullWidthCardParams());
        actionsCard.addView(makeTitle("快捷操作", 19), matchWidthWrapParams());
        Button refreshButton = makePrimaryButton("刷新状态");
        refreshButton.setOnClickListener(v -> refreshDashboard());
        actionsCard.addView(refreshButton, fullWidthButtonParams());
        Button showButton = makeSecondaryButton("显示电脑端主窗口");
        showButton.setOnClickListener(v -> sendCommand("showMainWindow"));
        actionsCard.addView(showButton, fullWidthButtonParams());
        Button minimizeButton = makeSecondaryButton("最小化电脑端主窗口");
        minimizeButton.setOnClickListener(v -> sendCommand("minimizeMainWindow"));
        actionsCard.addView(minimizeButton, fullWidthButtonParams());
        Button pageButton = makeSecondaryButton("打开电脑端页面");
        pageButton.setOnClickListener(v -> openUrl(currentLanUrl));
        actionsCard.addView(pageButton, fullWidthButtonParams());

        content.addView(createMusicCard(), fullWidthCardParams());

        LinearLayout logCard = makeCard();
        content.addView(logCard, fullWidthCardParams());
        logCard.addView(makeTitle("最近日志", 19), matchWidthWrapParams());
        dashboardLogText = makeBodyText("正在读取日志...");
        dashboardLogText.setTypeface(Typeface.MONOSPACE);
        dashboardLogText.setTextSize(12);
        dashboardLogText.setPadding(0, dp(10), 0, 0);
        logCard.addView(dashboardLogText, matchWidthWrapParams());

        LinearLayout settingsCard = makeCard();
        content.addView(settingsCard, fullWidthCardParams());
        settingsCard.addView(makeTitle("连接设置", 19), matchWidthWrapParams());
        Button scanButton = makeSecondaryButton("重新扫描二维码");
        scanButton.setOnClickListener(v -> startQrScan());
        settingsCard.addView(scanButton, fullWidthButtonParams());
        Button disconnectButton = makeSecondaryButton("断开此电脑");
        disconnectButton.setOnClickListener(v -> {
            clearSavedLanUrl();
            Toast.makeText(this, "已断开当前电脑", Toast.LENGTH_SHORT).show();
            showSetupView();
        });
        settingsCard.addView(disconnectButton, fullWidthButtonParams());
        Button downloadButton = makeSecondaryButton("打开下载站");
        downloadButton.setOnClickListener(v -> openUrl(HOME_URL));
        settingsCard.addView(downloadButton, fullWidthButtonParams());

        return scrollView;
    }

    private LinearLayout createMusicCard() {
        LinearLayout card = makeCard();
        card.addView(makeTitle("音乐播放", 19), matchWidthWrapParams());

        TextView musicTitleText = makeTitle(musicController.getSavedAudioTitle(), 17);
        musicTitleText.setPadding(0, dp(12), 0, 0);
        card.addView(musicTitleText, matchWidthWrapParams());

        TextView musicStatusText = makeBodyText(musicController.hasSavedAudio() ? "已选择，点击播放。" : "从手机选择一首音乐后播放。");
        musicStatusText.setPadding(0, dp(6), 0, dp(4));
        card.addView(musicStatusText, matchWidthWrapParams());

        Button chooseButton = makeSecondaryButton("选择音乐");
        chooseButton.setOnClickListener(v -> chooseAudioFile());
        card.addView(chooseButton, fullWidthButtonParams());

        LinearLayout controls = new LinearLayout(this);
        controls.setOrientation(LinearLayout.HORIZONTAL);
        controls.setPadding(0, dp(10), 0, 0);
        card.addView(controls, matchWidthWrapParams());

        Button musicPlayPauseButton = makePrimaryButton("播放");
        musicPlayPauseButton.setOnClickListener(v -> musicController.togglePlayback());
        LinearLayout.LayoutParams playParams = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1);
        controls.addView(musicPlayPauseButton, playParams);

        Button musicStopButton = makeSecondaryButton("停止");
        musicStopButton.setOnClickListener(v -> musicController.stop());
        LinearLayout.LayoutParams stopParams = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1);
        stopParams.setMargins(dp(10), 0, 0, 0);
        controls.addView(musicStopButton, stopParams);

        musicController.bindViews(musicTitleText, musicStatusText, musicPlayPauseButton, musicStopButton);
        return card;
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

        String savedUrl = getSavedLanUrl();
        content.addView(createProfileHeader(savedUrl), matchWidthWrapParams());

        LinearLayout connectionSection = makeSettingsSection();
        content.addView(connectionSection, settingsSectionParams());
        addSettingsRow(connectionSection, "当前电脑", savedUrl.isEmpty() ? "未连接" : getBaseFromUrl(savedUrl), v -> openCurrentDeviceOrSetup());
        addSettingsRow(connectionSection, "服务地址", savedUrl.isEmpty() ? "未配置" : getBaseFromUrl(savedUrl), v -> openCurrentDeviceOrSetup());
        addSettingsRow(connectionSection, "控制端口", getPortFromUrl(savedUrl), null);
        addSettingsRow(connectionSection, "配对状态", savedUrl.isEmpty() ? "未配对" : "已配对", null);
        addSettingsRow(connectionSection, "安全现场运维",
                appPreferences.hasOperationsProfile() ? "设备密钥已配对" : "尚未配对",
                v -> openOperations());

        LinearLayout permissionSection = makeSettingsSection();
        content.addView(permissionSection, settingsSectionParams());
        addSettingsRow(permissionSection, "相机权限", hasCameraPermission() ? "已授权" : "需要时申请", v -> startQrScan());
        addSettingsRow(permissionSection, "网络权限", "已配置", null);
        addSettingsRow(permissionSection, "网页访问", "允许局域网 HTTP", null);
        addSettingsRow(permissionSection, "音乐权限", "选择单曲授权", v -> chooseAudioFile());

        LinearLayout appSection = makeSettingsSection();
        content.addView(appSection, settingsSectionParams());
        addSettingsRow(appSection, "网页列表", savedUrl.isEmpty() ? "未连接" : "打开", v -> {
            if (getSavedLanUrl().isEmpty()) {
                showSetupView();
            } else {
                openUrl(getSavedLanUrl());
            }
        });
        addSettingsRow(appSection, "下载站", "xc213618.ddns.me", v -> openUrl(HOME_URL));
        addSettingsRow(appSection, "音乐播放", musicController.getSavedAudioTitle(), v -> chooseAudioFile());
        addSettingsRow(appSection, "主题模式", getThemeModeLabel(), v -> showThemeDialog());
        addSettingsRow(appSection, "应用版本", getAppVersionName(), null);

        LinearLayout actionSection = makeSettingsSection();
        content.addView(actionSection, settingsSectionParams());
        addSettingsRow(actionSection, savedUrl.isEmpty() ? "添加电脑" : "重新扫描二维码", "", v -> startQrScan());
        addSettingsRow(actionSection, "打开现场运维伴侣", "", v -> openOperations());
        addSettingsRow(actionSection, "回到设备页", "", v -> openCurrentDeviceOrSetup());
        if (!savedUrl.isEmpty()) {
            addSettingsRow(actionSection, "断开当前电脑", "", v -> {
                clearSavedLanUrl();
                Toast.makeText(this, "已断开当前电脑", Toast.LENGTH_SHORT).show();
                showSetupView();
            });
        }

        return scrollView;
    }

    private LinearLayout createProfileHeader(String savedUrl) {
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

        TextView title = makeTitle("配置参数", 22);
        textBlock.addView(title, matchWidthWrapParams());

        TextView subtitle = makeBodyText(savedUrl.isEmpty() ? "未连接电脑端" : getBaseFromUrl(savedUrl));
        subtitle.setPadding(0, dp(4), 0, 0);
        textBlock.addView(subtitle, matchWidthWrapParams());

        ImageButton scanButton = makeTopIconButton(R.drawable.ic_qr_code_scanner_24);
        scanButton.setContentDescription("扫描二维码");
        scanButton.setOnClickListener(v -> startQrScan());
        header.addView(scanButton, new LinearLayout.LayoutParams(dp(44), dp(44)));
        return header;
    }

    private void openCurrentDeviceOrSetup() {
        String savedUrl = getSavedLanUrl();
        int startTab = appPreferences.consumeStartTab(TAB_DEVICES);
        if (startTab == TAB_PROFILE) {
            showProfileView();
        } else if (savedUrl.isEmpty()) {
            showSetupView();
        } else {
            showDashboard(savedUrl);
        }
    }

    private String getPortFromUrl(String url) {
        if (url == null || url.trim().isEmpty()) {
            return "未配置";
        }

        try {
            int port = Uri.parse(url).getPort();
            return port > 0 ? String.valueOf(port) : "默认";
        } catch (Exception ex) {
            return "未知";
        }
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

    private void refreshDashboard() {
        String statusUrl = buildApiUrl("/api/status", "");
        String logsUrl = buildApiUrl("/api/logs", "count=28");
        if (statusUrl.isEmpty() || logsUrl.isEmpty()) {
            dashboardStatusText.setText("连接地址无效，请重新扫码。");
            return;
        }

        dashboardStatusText.setText("正在刷新状态...");
        new Thread(() -> {
            try {
                JSONObject status = getJson(statusUrl);
                JSONObject logs = getJson(logsUrl);
                runOnUiThread(() -> {
                    renderDashboardStatus(status);
                    renderDashboardLogs(logs);
                });
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    dashboardStatusText.setText("连接失败：" + ex.getMessage());
                    dashboardLogText.setText("无法读取日志。");
                });
            }
        }, "ColorVisionDashboardRefresh").start();
    }

    private void renderDashboardStatus(JSONObject status) {
        dashboardStatusText.setText(status.optBoolean("ok")
                ? "电脑端在线 · " + status.optString("serverTime", "")
                : "电脑端未授权或离线");
        dashboardMachineText.setText(status.optString("machine", "--"));
        dashboardEndpointText.setText(status.optString("endpoint", "--"));
        dashboardVersionText.setText(status.optString("version", "--"));
        dashboardRuntimeText.setText(formatDuration(status.optInt("uptimeSeconds", 0)));

        JSONObject process = status.optJSONObject("process");
        if (process != null) {
            dashboardMemoryText.setText(process.optString("memoryMb", "--") + " MB");
        }

        JSONObject window = status.optJSONObject("mainWindow");
        if (window != null) {
            String state = window.optString("state", "--");
            boolean visible = window.optBoolean("isVisible", false);
            dashboardWindowText.setText(state + (visible ? " · 可见" : " · 不可见"));
        }

        JSONArray addresses = status.optJSONArray("addresses");
        dashboardAddressText.setText(joinJsonArray(addresses, ", "));
    }

    private void renderDashboardLogs(JSONObject logs) {
        JSONArray lines = logs.optJSONArray("lines");
        String text = joinJsonArray(lines, "\n");
        dashboardLogText.setText(text.isEmpty() ? "暂无日志。" : text);
    }

    private void sendCommand(String action) {
        String commandUrl = buildApiUrl("/api/command", "");
        if (commandUrl.isEmpty()) {
            Toast.makeText(this, "连接地址无效，请重新扫码", Toast.LENGTH_SHORT).show();
            return;
        }

        Toast.makeText(this, "正在发送命令...", Toast.LENGTH_SHORT).show();
        new Thread(() -> {
            try {
                JSONObject body = new JSONObject();
                body.put("action", action);
                JSONObject result = postJson(commandUrl, body);
                JSONObject status = result.optJSONObject("status");
                runOnUiThread(() -> {
                    Toast.makeText(this, result.optString("message", "命令已发送"), Toast.LENGTH_SHORT).show();
                    if (status != null) {
                        renderDashboardStatus(status);
                    } else {
                        refreshDashboard();
                    }
                });
            } catch (Exception ex) {
                runOnUiThread(() -> Toast.makeText(this, "命令失败：" + ex.getMessage(), Toast.LENGTH_LONG).show());
            }
        }, "ColorVisionDashboardCommand").start();
    }

    private JSONObject getJson(String url) throws Exception {
        HttpURLConnection connection = openJsonConnection(url, "GET");
        try {
            return new JSONObject(readResponse(connection));
        } finally {
            connection.disconnect();
        }
    }

    private JSONObject postJson(String url, JSONObject body) throws Exception {
        HttpURLConnection connection = openJsonConnection(url, "POST");
        connection.setDoOutput(true);
        byte[] payload = body.toString().getBytes(StandardCharsets.UTF_8);
        connection.setFixedLengthStreamingMode(payload.length);
        try (OutputStream output = connection.getOutputStream()) {
            output.write(payload);
        }

        try {
            return new JSONObject(readResponse(connection));
        } finally {
            connection.disconnect();
        }
    }

    private HttpURLConnection openJsonConnection(String url, String method) throws Exception {
        HttpURLConnection connection = (HttpURLConnection) new URL(url).openConnection();
        connection.setRequestMethod(method);
        connection.setConnectTimeout(3500);
        connection.setReadTimeout(5000);
        connection.setRequestProperty("Accept", "application/json");
        connection.setRequestProperty("Content-Type", "application/json; charset=utf-8");
        return connection;
    }

    private String readResponse(HttpURLConnection connection) throws Exception {
        int code = connection.getResponseCode();
        InputStream stream = code >= 400 ? connection.getErrorStream() : connection.getInputStream();
        if (stream == null) {
            throw new IllegalStateException("HTTP " + code);
        }

        StringBuilder builder = new StringBuilder();
        try (BufferedReader reader = new BufferedReader(new InputStreamReader(stream, StandardCharsets.UTF_8))) {
            String line;
            while ((line = reader.readLine()) != null) {
                builder.append(line);
            }
        }

        if (code >= 400) {
            throw new IllegalStateException("HTTP " + code + " " + builder);
        }

        return builder.toString();
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
                if (result != null && result.startsWith("colorvision://pair")) {
                    Intent operations = new Intent(this, OperationsActivity.class);
                    operations.putExtra(OperationsActivity.EXTRA_PAIRING_PAYLOAD, result);
                    startActivity(operations);
                } else {
                    saveAndOpen(result);
                }
                return;
            }
            Toast.makeText(this, "已取消扫码，可手动输入连接地址", Toast.LENGTH_SHORT).show();
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
            startActivity(new Intent(this, OperationsActivity.class));
        } else {
            Toast.makeText(this, "请扫描电脑端现场运维配对码", Toast.LENGTH_SHORT).show();
            startQrScan();
        }
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
        String url = parseConnectionUrl(rawContent);
        if (url.isEmpty()) {
            Toast.makeText(this, "没有识别到有效的连接地址", Toast.LENGTH_SHORT).show();
            return;
        }
        if (AppPreferences.containsUrlCredential(url)) {
            Toast.makeText(this, "旧版 URL token 配对码已停用，请在电脑端刷新安全运维配对码", Toast.LENGTH_LONG).show();
            return;
        }

        appPreferences.saveLanUrl(url);
        showDashboard(url);
    }

    private String parseConnectionUrl(String rawContent) {
        if (rawContent == null) {
            return "";
        }

        String text = rawContent.trim();
        if (text.isEmpty()) {
            return "";
        }

        if (text.startsWith("{")) {
            try {
                JSONObject object = new JSONObject(text);
                String url = object.optString("url", object.optString("connectionUrl", ""));
                return normalizeUrl(url);
            } catch (Exception ignored) {
            }
        }

        try {
            Uri uri = Uri.parse(text);
            if ("colorvision".equalsIgnoreCase(uri.getScheme())) {
                return normalizeUrl(uri.getQueryParameter("url"));
            }
        } catch (Exception ignored) {
        }

        return normalizeUrl(text);
    }

    private String normalizeUrl(String url) {
        if (url == null) {
            return "";
        }

        String text = url.trim();
        if (text.isEmpty()) {
            return "";
        }

        if (!text.startsWith("http://") && !text.startsWith("https://")) {
            text = "http://" + text;
        }

        try {
            Uri uri = Uri.parse(text);
            if (uri.getScheme() == null || uri.getHost() == null) {
                return "";
            }
            return text;
        } catch (Exception ex) {
            return "";
        }
    }

    private String buildApiUrl(String path, String extraQuery) {
        Uri uri;
        try {
            uri = Uri.parse(currentLanUrl.isEmpty() ? getSavedLanUrl() : currentLanUrl);
        } catch (Exception ex) {
            return "";
        }

        String scheme = uri.getScheme();
        String authority = uri.getEncodedAuthority();
        String token = uri.getQueryParameter("token");
        if (scheme == null || authority == null || token == null || token.isEmpty()) {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        builder.append(scheme).append("://").append(authority).append(path)
                .append("?token=").append(urlEncode(token));
        if (extraQuery != null && !extraQuery.isEmpty()) {
            builder.append("&").append(extraQuery);
        }
        return builder.toString();
    }

    private String getBaseFromUrl(String url) {
        try {
            Uri uri = Uri.parse(url);
            return uri.getScheme() + "://" + uri.getEncodedAuthority();
        } catch (Exception ex) {
            return url;
        }
    }

    private String urlEncode(String value) {
        try {
            return URLEncoder.encode(value, StandardCharsets.UTF_8.name());
        } catch (Exception ex) {
            return "";
        }
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
            showSetupView();
            return true;
        }

        if ("disconnect".equalsIgnoreCase(host)) {
            clearSavedLanUrl();
            Toast.makeText(this, "已断开当前电脑", Toast.LENGTH_SHORT).show();
            showSetupView();
            return true;
        }

        return true;
    }

    private void openUrl(String url) {
        appShell.setVisibility(View.GONE);
        errorView.setVisibility(View.GONE);
        webView.setVisibility(View.VISIBLE);
        manageFloatingButton.setVisibility(View.VISIBLE);
        webView.loadUrl(url);
    }

    private void startPageLoadTimeout(String url) {
        int generation = ++pageLoadGeneration;
        mainHandler.postDelayed(() -> {
            String currentUrl = webView.getUrl();
            if (generation == pageLoadGeneration
                    && webView.getVisibility() == View.VISIBLE
                    && currentUrl != null
                    && currentUrl.equals(url)) {
                webView.stopLoading();
                showErrorView();
            }
        }, 7000);
    }

    private void showErrorView() {
        webView.setVisibility(View.GONE);
        appShell.setVisibility(View.GONE);
        errorView.setVisibility(View.VISIBLE);
        manageFloatingButton.setVisibility(View.GONE);
        progressBar.setVisibility(View.GONE);
    }

    private String getSavedLanUrl() {
        return appPreferences.getLanUrl();
    }

    private void clearSavedLanUrl() {
        appPreferences.clearLanUrl();
    }

    private String formatDuration(int seconds) {
        int hours = seconds / 3600;
        int minutes = (seconds % 3600) / 60;
        int rest = seconds % 60;
        if (hours > 0) {
            return hours + "小时 " + minutes + "分钟";
        }
        if (minutes > 0) {
            return minutes + "分钟 " + rest + "秒";
        }
        return rest + "秒";
    }

    private String joinJsonArray(JSONArray array, String separator) {
        if (array == null || array.length() == 0) {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < array.length(); i++) {
            if (i > 0) {
                builder.append(separator);
            }
            builder.append(array.optString(i, ""));
        }
        return builder.toString();
    }

    private TextView addMetricRow(LinearLayout parent, String label, String value) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(0, dp(10), 0, 0);
        parent.addView(row, matchWidthWrapParams());

        TextView labelView = new TextView(this);
        labelView.setText(label);
        labelView.setTextColor(secondaryTextColor());
        labelView.setTextSize(14);
        row.addView(labelView, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        TextView valueView = new TextView(this);
        valueView.setText(value);
        valueView.setTextColor(primaryTextColor());
        valueView.setTextSize(14);
        valueView.setGravity(Gravity.RIGHT);
        valueView.setSingleLine(false);
        row.addView(valueView, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1.35f));
        return valueView;
    }

    private LinearLayout addProfileRow(String label, String value) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(0, dp(10), 0, 0);

        TextView labelView = new TextView(this);
        labelView.setText(label);
        labelView.setTextColor(secondaryTextColor());
        labelView.setTextSize(14);
        row.addView(labelView, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        TextView valueView = new TextView(this);
        valueView.setText(value);
        valueView.setTextColor(primaryTextColor());
        valueView.setTextSize(14);
        valueView.setGravity(Gravity.RIGHT);
        valueView.setSingleLine(false);
        row.addView(valueView, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1.55f));
        return row;
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

    private Button createManageFloatingButton() {
        Button button = new Button(this);
        button.setText("控制");
        button.setTextSize(14);
        button.setAllCaps(false);
        button.setTextColor(Color.WHITE);
        button.setPadding(dp(14), 0, dp(14), 0);
        button.setMinHeight(dp(38));
        button.setBackground(rounded(Color.rgb(31, 111, 235), dp(21), Color.TRANSPARENT, 0));
        button.setVisibility(View.GONE);
        button.setOnClickListener(v -> {
            String savedUrl = getSavedLanUrl();
            if (savedUrl.isEmpty()) {
                showSetupView();
            } else {
                showDashboard(savedUrl);
            }
        });
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
        webView.saveState(outState);
        homeWebView.saveState(outState);
    }

    @Override
    public void onBackPressed() {
        if (currentTab == TAB_HOME && homeWebView.canGoBack()) {
            homeWebView.goBack();
            return;
        }

        if (webView.getVisibility() == View.VISIBLE && webView.canGoBack()) {
            webView.goBack();
            return;
        }

        if (webView.getVisibility() == View.VISIBLE) {
            String savedUrl = getSavedLanUrl();
            if (savedUrl.isEmpty()) {
                showSetupView();
            } else {
                showDashboard(savedUrl);
            }
            return;
        }

        super.onBackPressed();
    }

    @Override
    protected void onDestroy() {
        musicController.release();
        if (webView != null) {
            webView.destroy();
        }
        if (homeWebView != null) {
            homeWebView.destroy();
        }
        super.onDestroy();
    }
}

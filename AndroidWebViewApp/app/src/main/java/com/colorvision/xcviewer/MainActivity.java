package com.colorvision.xcviewer;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.Intent;
import android.content.SharedPreferences;
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
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
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
    private static final String PREFS_NAME = "colorvision_mobile";
    private static final String KEY_LAN_URL = "lan_url";
    private static final int REQUEST_QR_SCAN = 1001;

    private FrameLayout root;
    private FrameLayout setupContainer;
    private LinearLayout errorView;
    private WebView webView;
    private ProgressBar progressBar;
    private Button manageFloatingButton;
    private SharedPreferences preferences;
    private Handler mainHandler;
    private int pageLoadGeneration;
    private String currentLanUrl = "";

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

        preferences = getSharedPreferences(PREFS_NAME, MODE_PRIVATE);
        mainHandler = new Handler(Looper.getMainLooper());

        root = new FrameLayout(this);
        webView = new WebView(this);
        setupContainer = new FrameLayout(this);
        errorView = createErrorView();
        progressBar = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
        manageFloatingButton = createManageFloatingButton();

        root.addView(webView, matchParentParams());
        root.addView(setupContainer, matchParentParams());
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

        String savedUrl = getSavedLanUrl();
        if (savedUrl.isEmpty()) {
            showSetupView();
        } else {
            showDashboard(savedUrl);
        }
    }

    private void configureWebView() {
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);

        webView.setWebChromeClient(new WebChromeClient() {
            @Override
            public void onProgressChanged(WebView view, int newProgress) {
                progressBar.setProgress(newProgress);
                progressBar.setVisibility(newProgress >= 100 ? View.GONE : View.VISIBLE);
            }
        });

        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageStarted(WebView view, String url, Bitmap favicon) {
                if (!handleSpecialUrl(url)) {
                    errorView.setVisibility(View.GONE);
                    setupContainer.setVisibility(View.GONE);
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

    private LinearLayout createErrorView() {
        LinearLayout layout = new LinearLayout(this);
        layout.setOrientation(LinearLayout.VERTICAL);
        layout.setGravity(Gravity.CENTER);
        layout.setPadding(dp(24), dp(24), dp(24), dp(24));
        layout.setBackgroundColor(Color.rgb(245, 247, 251));
        layout.setVisibility(View.GONE);

        TextView title = new TextView(this);
        title.setText("连接失败");
        title.setTextColor(Color.rgb(24, 32, 51));
        title.setTextSize(24);
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        title.setGravity(Gravity.CENTER);
        layout.addView(title, wrapParams());

        TextView subtitle = new TextView(this);
        subtitle.setText("请确认手机和电脑在同一个局域网，并且电脑端已启用局域网控制。");
        subtitle.setTextColor(Color.rgb(96, 112, 139));
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

    private void showSetupView() {
        currentLanUrl = "";
        setupContainer.removeAllViews();
        setupContainer.addView(createSetupContent(), matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        errorView.setVisibility(View.GONE);
        webView.setVisibility(View.GONE);
        manageFloatingButton.setVisibility(View.GONE);
        progressBar.setVisibility(View.GONE);
    }

    private ScrollView createSetupContent() {
        ScrollView scrollView = new ScrollView(this);
        scrollView.setFillViewport(true);
        scrollView.setBackgroundColor(Color.rgb(245, 247, 251));

        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setGravity(Gravity.CENTER_HORIZONTAL);
        content.setPadding(dp(22), dp(36), dp(22), dp(30));
        scrollView.addView(content, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT,
                ScrollView.LayoutParams.WRAP_CONTENT));

        TextView title = new TextView(this);
        title.setText("ColorVision 移动版");
        title.setTextColor(Color.rgb(24, 32, 51));
        title.setTextSize(28);
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        title.setGravity(Gravity.CENTER);
        content.addView(title, wrapParams());

        TextView subtitle = new TextView(this);
        subtitle.setText("扫描二维码以设置新手机或管理现有连接");
        subtitle.setTextColor(Color.rgb(96, 112, 139));
        subtitle.setTextSize(15);
        subtitle.setGravity(Gravity.CENTER);
        subtitle.setPadding(0, dp(10), 0, dp(24));
        content.addView(subtitle, wrapParams());

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
        manualInput.setHint("手动输入地址，例如 http://192.168.1.10:8787/mobile?token=...");
        manualInput.setSingleLine(false);
        manualInput.setMinLines(2);
        manualInput.setTextColor(Color.rgb(24, 32, 51));
        manualInput.setHintTextColor(Color.rgb(130, 142, 160));
        manualInput.setTextSize(14);
        manualInput.setBackground(rounded(Color.rgb(248, 250, 253), dp(10), Color.rgb(221, 228, 239), 1));
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
        setupContainer.removeAllViews();
        setupContainer.addView(createDashboardContent(url), matchParentParams());
        setupContainer.setVisibility(View.VISIBLE);
        errorView.setVisibility(View.GONE);
        webView.setVisibility(View.GONE);
        manageFloatingButton.setVisibility(View.GONE);
        progressBar.setVisibility(View.GONE);
        refreshDashboard();
    }

    private ScrollView createDashboardContent(String url) {
        ScrollView scrollView = new ScrollView(this);
        scrollView.setFillViewport(false);
        scrollView.setBackgroundColor(Color.rgb(245, 247, 251));

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

    private void startQrScan() {
        startActivityForResult(new Intent(this, QrScanActivity.class), REQUEST_QR_SCAN);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == REQUEST_QR_SCAN) {
            if (resultCode == RESULT_OK && data != null) {
                saveAndOpen(data.getStringExtra(QrScanActivity.EXTRA_QR_RESULT));
                return;
            }
            Toast.makeText(this, "已取消扫码，可手动输入连接地址", Toast.LENGTH_SHORT).show();
            return;
        }

        super.onActivityResult(requestCode, resultCode, data);
    }

    private void saveAndOpen(String rawContent) {
        String url = parseConnectionUrl(rawContent);
        if (url.isEmpty()) {
            Toast.makeText(this, "没有识别到有效的连接地址", Toast.LENGTH_SHORT).show();
            return;
        }

        preferences.edit().putString(KEY_LAN_URL, url).apply();
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
        setupContainer.setVisibility(View.GONE);
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
        setupContainer.setVisibility(View.GONE);
        errorView.setVisibility(View.VISIBLE);
        manageFloatingButton.setVisibility(View.GONE);
        progressBar.setVisibility(View.GONE);
    }

    private String getSavedLanUrl() {
        return preferences.getString(KEY_LAN_URL, "");
    }

    private void clearSavedLanUrl() {
        preferences.edit().remove(KEY_LAN_URL).apply();
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
        labelView.setTextColor(Color.rgb(96, 112, 139));
        labelView.setTextSize(14);
        row.addView(labelView, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        TextView valueView = new TextView(this);
        valueView.setText(value);
        valueView.setTextColor(Color.rgb(24, 32, 51));
        valueView.setTextSize(14);
        valueView.setGravity(Gravity.RIGHT);
        valueView.setSingleLine(false);
        row.addView(valueView, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1.35f));
        return valueView;
    }

    private LinearLayout makeCard() {
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setPadding(dp(18), dp(18), dp(18), dp(18));
        card.setBackground(rounded(Color.WHITE, dp(16), Color.rgb(221, 228, 239), 1));
        return card;
    }

    private TextView makeTitle(String text, int size) {
        TextView title = new TextView(this);
        title.setText(text);
        title.setTextColor(Color.rgb(24, 32, 51));
        title.setTextSize(size);
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        title.setGravity(Gravity.LEFT);
        return title;
    }

    private TextView makeBodyText(String text) {
        TextView body = new TextView(this);
        body.setText(text);
        body.setTextColor(Color.rgb(96, 112, 139));
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
        button.setTextColor(Color.rgb(36, 48, 68));
        button.setBackground(rounded(Color.rgb(238, 242, 247), dp(10), Color.TRANSPARENT, 0));
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

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        super.onSaveInstanceState(outState);
        webView.saveState(outState);
    }

    @Override
    public void onBackPressed() {
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
        if (webView != null) {
            webView.destroy();
        }
        super.onDestroy();
    }
}

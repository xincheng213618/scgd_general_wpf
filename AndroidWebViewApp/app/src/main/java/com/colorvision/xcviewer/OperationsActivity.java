package com.colorvision.xcviewer;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ClipData;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Color;
import android.graphics.Typeface;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import androidx.core.content.FileProvider;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.File;
import java.io.FileOutputStream;
import java.net.ConnectException;
import java.net.SocketTimeoutException;
import java.net.UnknownHostException;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import java.util.TimeZone;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

import javax.net.ssl.SSLHandshakeException;

public class OperationsActivity extends Activity {
    public static final String EXTRA_PAIRING_PAYLOAD = "operations_pairing_payload";
    private static final long LIVE_MONITOR_REFRESH_MILLISECONDS = 10_000L;
    private static final long CONNECTION_HEARTBEAT_MILLISECONDS = 30_000L;

    private boolean supportCenterVisible;
    private boolean supportAutoRefresh;
    private boolean liveMonitorVisible;
    private boolean liveMonitorAutoRefresh;
    private boolean liveMonitorRefreshInFlight;
    private boolean liveMonitorCancelAvailable;
    private boolean liveMonitorCancelInFlight;
    private JSONObject liveMonitorLatestSnapshot;
    private boolean activityResumed;
    private int liveMonitorGeneration;
    private final OperationsLiveMonitorTrend liveMonitorTrend = new OperationsLiveMonitorTrend();
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final Handler supportRefreshHandler = new Handler(Looper.getMainLooper());
    private final Runnable supportRefresh = () -> {
        if (activityResumed && supportCenterVisible && supportAutoRefresh) {
            loadSupportCenter(false);
        }
    };
    private final Handler liveMonitorRefreshHandler = new Handler(Looper.getMainLooper());
    private final Runnable liveMonitorRefresh = () -> {
        if (activityResumed && liveMonitorVisible && liveMonitorAutoRefresh) {
            loadLiveMonitor(false);
        }
    };
    private final Handler connectionHeartbeatHandler = new Handler(Looper.getMainLooper());
    private final Runnable connectionHeartbeat = this::runConnectionHeartbeat;
    private AppPreferences preferences;
    private OperationsApiClient client;
    private TextView title;
    private TextView state;
    private TextView details;
    private ProgressBar progress;
    private LinearLayout actions;
    private boolean dashboardVisible;
    private boolean showingDashboardSummary;
    private boolean connectionHeartbeatInFlight;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        preferences = new AppPreferences(this);
        createView();

        String rawPairing = getIntent().getStringExtra(EXTRA_PAIRING_PAYLOAD);
        if (rawPairing != null && !rawPairing.isEmpty()) {
            beginPairing(rawPairing);
        } else if (preferences.hasOperationsProfile()) {
            openExistingProfile();
        } else {
            showError("尚未安全配对", "请返回并扫描电脑端的现场运维配对码。", null);
        }
    }

    private void createView() {
        LinearLayout shell = new LinearLayout(this);
        shell.setOrientation(LinearLayout.VERTICAL);
        shell.setBackgroundColor(Color.rgb(245, 247, 250));

        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(dp(16), getStatusBarHeight() + dp(6), dp(16), dp(24));
        root.setBackgroundColor(Color.rgb(245, 247, 250));
        scroll.addView(root, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT, ScrollView.LayoutParams.WRAP_CONTENT));

        LinearLayout header = new LinearLayout(this);
        header.setOrientation(LinearLayout.HORIZONTAL);
        header.setGravity(Gravity.CENTER_VERTICAL);
        root.addView(header, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(46)));

        Button back = new Button(this);
        back.setText("返回");
        back.setTextSize(13);
        back.setAllCaps(false);
        back.setMinHeight(0);
        back.setMinimumHeight(0);
        back.setPadding(dp(8), 0, dp(8), 0);
        back.setOnClickListener(v -> finish());
        header.addView(back, new LinearLayout.LayoutParams(dp(72), dp(40)));

        title = new TextView(this);
        title.setText("ColorVision 运维伴侣");
        title.setTextSize(22);
        title.setTextColor(Color.rgb(24, 35, 49));
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        title.setSingleLine(true);
        title.setPadding(dp(12), 0, 0, 0);
        header.addView(title, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        state = new TextView(this);
        state.setTextSize(14);
        state.setTextColor(Color.rgb(58, 75, 92));
        state.setPadding(dp(12), dp(8), dp(12), dp(8));
        state.setBackgroundColor(Color.WHITE);
        LinearLayout.LayoutParams stateParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        stateParams.setMargins(0, dp(8), 0, 0);
        root.addView(state, stateParams);

        progress = new ProgressBar(this);
        LinearLayout.LayoutParams progressParams = new LinearLayout.LayoutParams(dp(32), dp(32));
        progressParams.gravity = Gravity.CENTER_HORIZONTAL;
        progressParams.setMargins(0, dp(8), 0, dp(4));
        root.addView(progress, progressParams);

        details = new TextView(this);
        details.setTextSize(13);
        details.setTextColor(Color.rgb(41, 53, 66));
        details.setLineSpacing(0, 1.08f);
        details.setPadding(dp(12), dp(10), dp(12), dp(10));
        details.setBackgroundColor(Color.WHITE);
        details.setTextIsSelectable(true);
        LinearLayout.LayoutParams detailsParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        detailsParams.setMargins(0, dp(8), 0, 0);
        root.addView(details, detailsParams);

        actions = new LinearLayout(this);
        actions.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams actionsParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        actionsParams.setMargins(0, dp(10), 0, 0);
        root.addView(actions, actionsParams);

        shell.addView(scroll, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));
        shell.addView(createBottomNavigation(), new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(60)));
        setContentView(shell);
    }

    private LinearLayout createBottomNavigation() {
        LinearLayout navigation = new LinearLayout(this);
        navigation.setOrientation(LinearLayout.HORIZONTAL);
        navigation.setGravity(Gravity.CENTER);
        navigation.setPadding(dp(12), dp(3), dp(12), dp(3));
        navigation.setBackgroundColor(Color.WHITE);
        navigation.setElevation(dp(8));
        navigation.addView(createBottomNavigationItem("运维", true, null),
                new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));
        navigation.addView(createBottomNavigationItem("下载站", false,
                        v -> openMainTab(MainActivity.TAB_DOWNLOADS)),
                new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));
        navigation.addView(createBottomNavigationItem("设置", false,
                        v -> openMainTab(MainActivity.TAB_SETTINGS)),
                new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));
        return navigation;
    }

    private TextView createBottomNavigationItem(String label, boolean selected, View.OnClickListener listener) {
        TextView item = new TextView(this);
        item.setText(label);
        item.setTextSize(13);
        item.setGravity(Gravity.CENTER);
        item.setTextColor(selected ? Color.rgb(31, 111, 235) : Color.rgb(91, 105, 119));
        item.setTypeface(Typeface.DEFAULT, selected ? Typeface.BOLD : Typeface.NORMAL);
        item.setClickable(listener != null);
        item.setFocusable(listener != null);
        item.setOnClickListener(listener);
        return item;
    }

    private void openMainTab(int tab) {
        Intent intent = new Intent(this, MainActivity.class);
        intent.putExtra(MainActivity.EXTRA_START_TAB, tab);
        intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        startActivity(intent);
        finish();
    }

    private void beginPairing(String rawPairing) {
        setBusy("正在验证配对码并创建设备身份…");
        executor.execute(() -> {
            try {
                OperationsPairingPayload payload = OperationsPairingPayload.parse(rawPairing);
                String deviceId = preferences.getOrCreateDeviceId();
                String deviceName = Build.MANUFACTURER + " " + Build.MODEL;
                OperationsDeviceIdentity identity = new OperationsDeviceIdentity(payload.hostId);
                OperationsApiClient pairingClient = new OperationsApiClient(
                        payload.endpoint, payload.certificateSha256, deviceId, identity);
                pairingClient.submitClaim(payload, deviceName.trim());
                runOnUiThread(() -> {
                    state.setText("已提交安全证明，请在电脑端批准这台设备");
                    details.setText("设备：" + deviceName + "\n权限：状态、告警、消息通道与设备运行状态汇总、诊断摘要、主窗口控制、受控窗口取证与当前检测取消\n配对码：一次性，短时有效");
                });
                pollPairingApproval(payload, pairingClient);
            } catch (Exception ex) {
                runOnUiThread(() -> showError("安全配对失败", readableError(ex), null));
            }
        });
    }

    private void pollPairingApproval(OperationsPairingPayload payload, OperationsApiClient pairingClient) throws Exception {
        for (int attempt = 0; attempt < 60; attempt++) {
            if (isFinishing()) {
                return;
            }
            JSONObject response = pairingClient.pairingStatus(payload.pairingId);
            JSONObject data = response.optJSONObject("data");
            String status = data == null ? "" : data.optString("status", "");
            if ("approved".equals(status)) {
                preferences.saveOperationsProfile(payload.endpoint, payload.certificateSha256, payload.hostId);
                client = pairingClient;
                runOnUiThread(this::showDashboard);
                return;
            }
            if ("rejected".equals(status)) {
                runOnUiThread(() -> showError("配对被拒绝", "电脑端拒绝了这台设备。", null));
                return;
            }
            Thread.sleep(2000);
        }
        runOnUiThread(() -> showPairingTimeout(payload, pairingClient));
    }

    private void showPairingTimeout(OperationsPairingPayload payload, OperationsApiClient pairingClient) {
        showError("等待批准超时", "电脑端可能尚未批准，也可能刚刚完成批准。", null);
        details.setText("本次桌面会话仍保留已提交的设备证明。批准完成后，可直接重新检查，无需刷新二维码或重新创建设备密钥。");
        Button retry = new Button(this);
        retry.setText("重新检查批准状态");
        retry.setOnClickListener(v -> {
            setBusy("正在重新检查电脑端批准状态…");
            executor.execute(() -> {
                try {
                    pollPairingApproval(payload, pairingClient);
                } catch (Exception ex) {
                    runOnUiThread(() -> showError("检查批准状态失败", readableError(ex), null));
                }
            });
        });
        actions.addView(retry, actionParams());
    }

    private void openExistingProfile() {
        setBusy("正在连接已配对的 ColorVision 主机…");
        executor.execute(() -> {
            try {
                OperationsDeviceIdentity identity = new OperationsDeviceIdentity(preferences.getOperationsHostId());
                client = new OperationsApiClient(
                        preferences.getOperationsEndpoint(),
                        preferences.getOperationsCertificatePin(),
                        preferences.getOrCreateDeviceId(),
                        identity);
                client.get("/ops/v1/snapshot");
                runOnUiThread(this::showDashboard);
            } catch (Exception ex) {
                runOnUiThread(() -> showExistingProfileFailure(ex));
            }
        });
    }

    private void showDashboard() {
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = true;
        showingDashboardSummary = true;
        progress.setVisibility(View.GONE);
        title.setText("ColorVision 运维伴侣");
        state.setText("● 在线 · 安全通道已验证");
        details.setText("正在读取 ColorVision 运行摘要…");
        actions.removeAllViews();

        addDashboardSection("常用操作");
        addDashboardActionRow(
                dashboardButton("远程排障", v -> showTriageCenter()),
                dashboardButton("持续观察", v -> showLiveMonitor()));
        addDashboardActionRow(
                dashboardButton("显示主窗口", v -> runWindowAction("show", "主窗口已显示")),
                dashboardButton("最小化窗口", v -> confirmMinimizeWindow()));
        addDashboardActionRow(
                dashboardButton("重启 MQTT", v -> confirmRestartMqtt()),
                dashboardButton("重启 ColorVision", v -> confirmRestartApplication()));
        addDashboardActionRow(
                dashboardButton("连接自检", v -> runConnectionSelfCheck()),
                capabilityButton("刷新摘要", "/ops/v1/snapshot"));

        addDashboardSection("状态与排障");
        addDashboardActionRow(
                capabilityButton("当前检测", "/ops/v1/flow/runtime"),
                capabilityButton("设备概览", "/ops/v1/devices/health"));
        addDashboardActionRow(
                capabilityButton("服务健康", "/ops/v1/services/health"),
                capabilityButton("消息通道", "/ops/v1/messaging/health"));
        addDashboardActionRow(
                capabilityButton("进程性能", "/ops/v1/diagnostics/performance"),
                capabilityButton("当前告警", "/ops/v1/alerts"));
        addDashboardActionRow(
                capabilityButton("近期事件", "/ops/v1/diagnostics/recent-events"),
                capabilityButton("诊断摘要", "/ops/v1/diagnostics/summary"));

        addDashboardSection("取证与支持");
        addDashboardActionRow(
                dashboardButton("作业与审批", v -> showJobs()),
                capabilityButton("操作记录", "/ops/v1/audit"));
        addDashboardActionRow(
                dashboardButton("生成诊断包", v -> confirmCreateDiagnosticJob()),
                dashboardButton("主窗口快照", v -> confirmCreateWindowSnapshotJob()));
        addDashboardActionRow(
                dashboardButton("分享诊断摘要", v -> loadAndShareSafeDiagnostics()),
                dashboardButton("支持会话", v -> showSupportCenter()));
        addDashboardActionRow(
                dashboardButton("提交部署确认", v -> confirmDeploymentReceipt()),
                capabilityButton("能力目录", "/ops/v1/capabilities"));
        loadCapability("/ops/v1/snapshot");
        scheduleConnectionHeartbeat();
    }

    private void scheduleConnectionHeartbeat() {
        connectionHeartbeatHandler.removeCallbacks(connectionHeartbeat);
        if (activityResumed && dashboardVisible && client != null) {
            connectionHeartbeatHandler.postDelayed(connectionHeartbeat, CONNECTION_HEARTBEAT_MILLISECONDS);
        }
    }

    private void runConnectionHeartbeat() {
        if (!activityResumed || !dashboardVisible || client == null || connectionHeartbeatInFlight) {
            return;
        }
        connectionHeartbeatInFlight = true;
        executor.execute(() -> {
            try {
                client.get("/ops/v1/snapshot");
                runOnUiThread(() -> {
                    connectionHeartbeatInFlight = false;
                    if (showingDashboardSummary) {
                        state.setText("● 在线 · 安全通道已验证");
                    }
                    scheduleConnectionHeartbeat();
                });
            } catch (Exception ignored) {
                runOnUiThread(() -> {
                    connectionHeartbeatInFlight = false;
                    if (showingDashboardSummary) {
                        state.setText("● 连接暂断 · 正在自动重试");
                    }
                    scheduleConnectionHeartbeat();
                });
            }
        });
    }

    private void addDashboardSection(String label) {
        TextView heading = new TextView(this);
        heading.setText(label);
        heading.setTextSize(15);
        heading.setTextColor(Color.rgb(24, 35, 49));
        heading.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        heading.setPadding(dp(2), dp(8), 0, dp(5));
        actions.addView(heading, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));
    }

    private Button dashboardButton(String label, View.OnClickListener listener) {
        Button button = new Button(this);
        button.setText(label);
        button.setTextSize(13);
        button.setAllCaps(false);
        button.setOnClickListener(listener);
        return button;
    }

    private Button capabilityButton(String label, String path) {
        return dashboardButton(label, v -> loadCapability(path));
    }

    private void addDashboardActionRow(Button left, Button right) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);

        LinearLayout.LayoutParams leftParams = new LinearLayout.LayoutParams(0, dp(48), 1);
        leftParams.setMargins(0, 0, dp(4), dp(4));
        row.addView(left, leftParams);

        LinearLayout.LayoutParams rightParams = new LinearLayout.LayoutParams(0, dp(48), 1);
        rightParams.setMargins(dp(4), 0, 0, dp(4));
        row.addView(right, rightParams);
        actions.addView(row, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));
    }

    private void showExistingProfileFailure(Exception ex) {
        leaveSupportCenter();
        dashboardVisible = false;
        progress.setVisibility(View.GONE);
        title.setText("安全通道暂不可用");
        state.setText(readableError(ex));
        details.setText("已保留本机设备密钥和配对资料。临时断线、电脑未启动或防火墙阻断都不需要重新配对；请先运行分层连接自检。");
        showConnectionRecoveryActions(false);
    }

    private void runConnectionSelfCheck() {
        showingDashboardSummary = false;
        progress.setVisibility(View.VISIBLE);
        state.setText("正在检查网络、端口、证书和设备签名…");
        executor.execute(() -> {
            OperationsConnectionCheck.Result result;
            try {
                if (client == null) {
                    OperationsDeviceIdentity identity = new OperationsDeviceIdentity(preferences.getOperationsHostId());
                    client = new OperationsApiClient(
                            preferences.getOperationsEndpoint(),
                            preferences.getOperationsCertificatePin(),
                            preferences.getOrCreateDeviceId(),
                            identity);
                }
                result = OperationsConnectionCheck.run(
                        getApplicationContext(), preferences.getOperationsEndpoint(), client);
            } catch (Exception ex) {
                result = new OperationsConnectionCheck.Result(false, "无法启动连接自检",
                        readableError(ex) + "\n\n配对资料已保留；不要仅因临时断线重新配对。");
            }
            OperationsConnectionCheck.Result finalResult = result;
            runOnUiThread(() -> {
                progress.setVisibility(View.GONE);
                if (!dashboardVisible) {
                    title.setText("连接自检");
                }
                state.setText(finalResult.heading);
                details.setText(finalResult.details);
                if (!dashboardVisible) {
                    showConnectionRecoveryActions(finalResult.success);
                }
            });
        });
    }

    private void showConnectionRecoveryActions(boolean channelReady) {
        actions.removeAllViews();

        Button check = new Button(this);
        check.setText("重新运行连接自检");
        check.setOnClickListener(v -> runConnectionSelfCheck());
        actions.addView(check, actionParams());

        Button reconnect = new Button(this);
        reconnect.setText(channelReady ? "进入现场运维" : "重新连接电脑");
        reconnect.setOnClickListener(v -> openExistingProfile());
        actions.addView(reconnect, actionParams());

        Button remove = new Button(this);
        remove.setText("移除本机配对资料");
        remove.setOnClickListener(v -> confirmClearProfile());
        actions.addView(remove, actionParams());
    }

    private void confirmClearProfile() {
        new AlertDialog.Builder(this)
                .setTitle("移除本机配对资料")
                .setMessage("仅删除手机中的设备密钥、证书指纹和端点记录。电脑端的已配对设备仍需单独撤销。")
                .setNegativeButton("取消", null)
                .setPositiveButton("确认移除", (dialog, which) -> clearProfile())
                .show();
    }

    private void confirmMinimizeWindow() {
        new AlertDialog.Builder(this)
                .setTitle("最小化电脑主窗口")
                .setMessage("该操作会立即最小化已连接电脑上的 ColorVision 主窗口，并写入运维审计。")
                .setNegativeButton("取消", null)
                .setPositiveButton("最小化", (dialog, which) -> runWindowAction("minimize", "主窗口已最小化"))
                .show();
    }

    private void runWindowAction(String action, String successText) {
        showingDashboardSummary = false;
        progress.setVisibility(View.VISIBLE);
        state.setText("正在执行安全桌面操作…");
        executor.execute(() -> {
            try {
                JSONObject response = client.post("/ops/v1/actions/window/" + action, new JSONObject());
                JSONObject data = response.optJSONObject("data");
                String message = data == null ? successText : data.optString("message", successText);
                runOnUiThread(() -> {
                    progress.setVisibility(View.GONE);
                    state.setText(successText);
                    details.setText(message + "\n该操作已记录设备身份、时间和结果。");
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void showTriageCenter() {
        showingDashboardSummary = false;
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = true;
        progress.setVisibility(View.VISIBLE);
        state.setText("正在汇总有界证据与可用处置动作…");
        executor.execute(() -> {
            try {
                JSONObject response = client.get("/ops/v1/triage");
                JSONObject report = response.optJSONObject("data");
                if (report == null) {
                    throw new IllegalStateException("incomplete_triage_response");
                }
                runOnUiThread(() -> renderTriageCenter(report));
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void renderTriageCenter(JSONObject report) {
        progress.setVisibility(View.GONE);
        title.setText("远程排障中心");
        state.setText(triageStateLabel(report.optString("state", "attention")));
        details.setText(formatTriageReport(report));
        actions.removeAllViews();

        Button refresh = new Button(this);
        refresh.setText("刷新排障建议");
        refresh.setOnClickListener(v -> showTriageCenter());
        actions.addView(refresh, actionParams());

        addTriageActions(report.optJSONArray("findings"));

        Button back = new Button(this);
        back.setText("返回现场运维概览");
        back.setOnClickListener(v -> showDashboard());
        actions.addView(back, actionParams());
    }

    private void addTriageActions(JSONArray findings) {
        if (findings == null) {
            return;
        }
        Set<String> added = new HashSet<>();
        for (int findingIndex = 0; findingIndex < findings.length(); findingIndex++) {
            JSONObject finding = findings.optJSONObject(findingIndex);
            JSONArray recommendations = finding == null ? null : finding.optJSONArray("actions");
            if (recommendations == null) {
                continue;
            }
            for (int actionIndex = 0; actionIndex < recommendations.length(); actionIndex++) {
                JSONObject recommendation = recommendations.optJSONObject(actionIndex);
                String actionId = recommendation == null ? "" : recommendation.optString("actionId", "");
                if (!added.add(actionId)) {
                    continue;
                }
                Button button = createTriageActionButton(actionId);
                if (button != null) {
                    actions.addView(button, actionParams());
                }
            }
        }
    }

    private Button createTriageActionButton(String actionId) {
        Button button = new Button(this);
        switch (actionId) {
            case "triage.events.view":
                button.setText("查看近期脱敏事件");
                button.setOnClickListener(v -> loadCapability("/ops/v1/diagnostics/recent-events"));
                return button;
            case "triage.window.show":
                button.setText("显示电脑主窗口（低风险）");
                button.setOnClickListener(v -> runWindowAction("show", "主窗口已显示"));
                return button;
            case "triage.jobs.review":
                button.setText("查看作业与审批");
                button.setOnClickListener(v -> showJobs());
                return button;
            case "triage.mqtt.restart.request":
                button.setText("重启 MQTT（需手机确认）");
                button.setOnClickListener(v -> confirmRestartMqtt());
                return button;
            case "triage.devices.view":
                button.setText("查看检测设备状态概览");
                button.setOnClickListener(v -> loadCapability("/ops/v1/devices/health"));
                return button;
            case "triage.messaging.view":
                button.setText("查看消息通道健康");
                button.setOnClickListener(v -> loadCapability("/ops/v1/messaging/health"));
                return button;
            default:
                return null;
        }
    }

    private String formatTriageReport(JSONObject report) {
        JSONArray findings = report.optJSONArray("findings");
        StringBuilder text = new StringBuilder();
        text.append(report.optString("summary", "排障建议已生成"))
                .append("\n事件：严重 ").append(report.optInt("criticalCount", 0))
                .append(" · 错误 ").append(report.optInt("errorCount", 0))
                .append(" · 警告 ").append(report.optInt("warningCount", 0))
                .append("\n待处理作业：").append(report.optInt("pendingJobCount", 0))
                .append("\n消息通道：").append(messageChannelStateLabel(
                        report.optString("messageChannelState", "unavailable")))
                .append(" · 订阅 ").append(report.optInt("messageChannelActiveSubscriptionCount", 0))
                .append('/').append(report.optInt("messageChannelRegisteredSubscriptionCount", 0))
                .append("\n检测设备：就绪 ").append(report.optInt("deviceReadyCount", 0))
                .append(" · 忙碌 ").append(report.optInt("deviceBusyCount", 0))
                .append(" · 已关闭 ").append(report.optInt("deviceClosedCount", 0))
                .append(" · 需关注 ").append(report.optInt("deviceAttentionCount", 0))
                .append(" / 共 ").append(report.optInt("deviceTotalCount", 0));
        appendDeviceUnavailableReasonValues(text,
                report.optInt("deviceOfflineCount", 0),
                report.optInt("deviceUninitializedCount", 0),
                report.optInt("deviceUnauthorizedCount", 0),
                report.optInt("deviceUnclassifiedUnavailableCount", 0));
        if (findings == null || findings.length() == 0) {
            text.append("\n\n当前有界证据未发现需要处理的项目。");
        } else {
            for (int index = 0; index < findings.length(); index++) {
                JSONObject finding = findings.optJSONObject(index);
                if (finding == null) {
                    continue;
                }
                text.append("\n\n").append(index + 1).append(". ")
                        .append(severityLabel(finding.optString("severity", "info")))
                        .append(" · ").append(finding.optString("title", "需要关注"))
                        .append("\n").append(finding.optString("summary", ""));
                String latestAt = shortTime(finding.optString("latestAt", ""));
                if (!latestAt.isEmpty()) {
                    text.append("\n最近证据：").append(latestAt);
                }
                appendRecommendationSummary(text, finding.optJSONArray("actions"));
            }
        }
        text.append("\n\n").append(report.optString("safetyNotice",
                "固定 MQTT 恢复、脱敏诊断包和单次主窗口快照需手机确认；支持会话仍需电脑端本机同意。"));
        return text.toString();
    }

    private void appendRecommendationSummary(StringBuilder text, JSONArray recommendations) {
        if (recommendations == null || recommendations.length() == 0) {
            text.append("\n建议：请在电脑端复核");
            return;
        }
        text.append("\n建议：");
        for (int index = 0; index < recommendations.length(); index++) {
            JSONObject recommendation = recommendations.optJSONObject(index);
            if (recommendation == null) {
                continue;
            }
            if (index > 0) {
                text.append("；");
            }
            text.append(recommendation.optString("title", "查看详情"));
            if (recommendation.optBoolean("requiresLocalCoSign", false)) {
                text.append("（需电脑共签）");
            } else if (recommendation.optBoolean("requiresConfirmation", false)) {
                text.append("（需确认）");
            }
        }
    }

    private String triageStateLabel(String value) {
        if ("critical".equalsIgnoreCase(value)) {
            return "发现严重事件 · 请优先复核";
        }
        if ("attention".equalsIgnoreCase(value)) {
            return "发现需要关注的状态";
        }
        return "当前有界证据正常";
    }

    private void showJobs() {
        showingDashboardSummary = false;
        leaveSupportCenter();
        leaveLiveMonitor();
        progress.setVisibility(View.VISIBLE);
        state.setText("正在读取安全作业摘要…");
        executor.execute(() -> {
            try {
                JSONObject response = client.get("/ops/v1/jobs");
                JSONObject data = response.optJSONObject("data");
                org.json.JSONArray jobs = data == null ? null : data.optJSONArray("jobs");
                JSONObject waiting = null;
                JSONObject downloadableDiagnostic = null;
                JSONObject downloadableWindowSnapshot = null;
                if (jobs != null) {
                    for (int index = 0; index < jobs.length(); index++) {
                        JSONObject job = jobs.optJSONObject(index);
                        if (job != null && ("awaiting_mobile_approval".equals(job.optString("status"))
                                || "approved_mobile".equals(job.optString("status")))) {
                            if (waiting == null) {
                                waiting = job;
                            }
                        }
                        JSONObject evidence = job == null ? null : job.optJSONObject("evidence");
                        if (downloadableDiagnostic == null && job != null
                                && "completed".equals(job.optString("status"))
                                && "ops.diagnostics.bundle.create".equals(job.optString("capabilityId"))
                                && evidence != null && evidence.optBoolean("available", false)
                                && "diagnostic-bundle-receipt".equals(evidence.optString("kind"))) {
                            downloadableDiagnostic = job;
                        }
                        if (downloadableWindowSnapshot == null && job != null
                                && "completed".equals(job.optString("status"))
                                && "ops.window.snapshot.capture".equals(job.optString("capabilityId"))
                                && evidence != null && evidence.optBoolean("available", false)
                                && "window-snapshot-receipt".equals(evidence.optString("kind"))) {
                            downloadableWindowSnapshot = job;
                        }
                    }
                }
                JSONObject finalWaiting = waiting;
                JSONObject finalDownloadableDiagnostic = downloadableDiagnostic;
                JSONObject finalDownloadableWindowSnapshot = downloadableWindowSnapshot;
                runOnUiThread(() -> {
                    renderJobs(data == null ? response : data, finalWaiting,
                            finalDownloadableDiagnostic, finalDownloadableWindowSnapshot);
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void renderJobs(JSONObject data, JSONObject waiting,
                            JSONObject downloadableDiagnostic, JSONObject downloadableWindowSnapshot) {
        progress.setVisibility(View.GONE);
        title.setText("作业与审批");
        state.setText(waiting != null ? "发现待移动审批作业"
                : downloadableWindowSnapshot != null ? "主窗口安全快照可读取一次"
                : downloadableDiagnostic != null ? "安全诊断包可下载" : "当前没有待移动审批作业");
        details.setText(formatJobs(data));
        actions.removeAllViews();

        if (waiting != null) {
            addApprovalActions(waiting);
        }
        if (downloadableDiagnostic != null) {
            Button download = new Button(this);
            download.setText("下载并分享安全诊断包");
            String jobId = downloadableDiagnostic.optString("jobId", "");
            download.setOnClickListener(v -> confirmDiagnosticBundleDownload(jobId));
            actions.addView(download, actionParams());
        }
        if (downloadableWindowSnapshot != null) {
            Button preview = new Button(this);
            preview.setText("下载并预览主窗口快照（单次）");
            String jobId = downloadableWindowSnapshot.optString("jobId", "");
            preview.setOnClickListener(v -> confirmWindowSnapshotDownload(jobId));
            actions.addView(preview, actionParams());
        }
        Button refresh = new Button(this);
        refresh.setText("刷新作业状态");
        refresh.setOnClickListener(v -> showJobs());
        actions.addView(refresh, actionParams());

        Button back = new Button(this);
        back.setText("返回现场运维概览");
        back.setOnClickListener(v -> showDashboard());
        actions.addView(back, actionParams());
    }

    private void addApprovalActions(JSONObject job) {
        Button approve = new Button(this);
        approve.setText("approved_mobile".equals(job.optString("status"))
                ? "继续执行已批准作业" : "确认并批准此作业");
        approve.setOnClickListener(v -> confirmJobApproval(job));
        actions.addView(approve, actionParams());
        if (!"approved_mobile".equals(job.optString("status"))) {
            Button reject = new Button(this);
            reject.setText("拒绝此作业");
            reject.setOnClickListener(v -> decideJob(job.optString("jobId", ""), false));
            actions.addView(reject, actionParams());
        }
    }

    private void confirmJobApproval(JSONObject job) {
        String jobId = job.optString("jobId", "");
        if (jobId.isEmpty()) {
            Toast.makeText(this, "作业标识无效", Toast.LENGTH_LONG).show();
            return;
        }
        String title = job.optString("title", "现场运维作业");
        String target = job.optString("target", "固定运维能力");
        boolean requiresLocalCoSign = job.optBoolean("requiresLocalCoSign", true);
        new AlertDialog.Builder(this)
                .setTitle("确认批准：" + title)
                .setMessage("目标：" + target
                        + (requiresLocalCoSign
                        ? "\n\n批准只记录这台已配对手机的明确意图，不会立即执行。电脑端仍需本机人员再次确认；未共签前作业保持阻塞。"
                        : "\n\n这是固定、无参数的远程动作。确认后会立即执行并写入审计，不需要电脑端再次共签。"))
                .setNegativeButton("取消", null)
                .setPositiveButton("确认批准", (dialog, which) -> decideJob(jobId, true))
                .show();
    }

    private void decideJob(String jobId, boolean approved) {
        progress.setVisibility(View.VISIBLE);
        executor.execute(() -> {
            try {
                JSONObject body = new JSONObject();
                body.put("approved", approved);
                body.put("reason", approved ? "已配对手机明确确认" : "现场运维人员拒绝");
                JSONObject response = client.post("/ops/v1/jobs/" + jobId + "/decision", body);
                JSONObject data = response.optJSONObject("data");
                JSONObject job = data == null ? null : data.optJSONObject("job");
                boolean requiresLocalCoSign = job == null || job.optBoolean("requiresLocalCoSign", true);
                String status = job == null ? "" : job.optString("status", "");
                runOnUiThread(() -> {
                    Toast.makeText(this,
                            !approved ? "作业已拒绝"
                                    : requiresLocalCoSign ? "移动审批已记录，仍需电脑端本机共签"
                                    : "completed".equals(status) ? "远程动作已执行"
                                    : "远程动作未执行，请查看作业结果",
                            Toast.LENGTH_LONG).show();
                    showJobs();
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void confirmCreateDiagnosticJob() {
        new AlertDialog.Builder(this)
                .setTitle("生成并分享诊断包")
                .setMessage("确认后电脑端立即生成脱敏 ZIP，手机会校验 SHA-256 并打开系统分享面板，不再等待电脑端共签。包内只含有界运行状态、脱敏事件、白名单服务健康和去标识审计，不含凭据、用户名、机器名、设备 ID、用户文档、数据库或图像；仅本申请设备可在 24 小时内下载。")
                .setNegativeButton("取消", null)
                .setPositiveButton("确认生成", (dialog, which) -> createDiagnosticJob())
                .show();
    }

    private void confirmDiagnosticBundleDownload(String jobId) {
        if (jobId.isEmpty()) {
            Toast.makeText(this, "诊断作业标识无效", Toast.LENGTH_LONG).show();
            return;
        }
        new AlertDialog.Builder(this)
                .setTitle("下载安全诊断包")
                .setMessage("仅下载当前设备已明确确认生成的脱敏 ZIP。下载内容会先校验 SHA-256，再交给你选择的应用；不要转发到不受信任的位置。")
                .setNegativeButton("取消", null)
                .setPositiveButton("下载并分享", (dialog, which) -> downloadAndShareDiagnosticBundle(jobId))
                .show();
    }

    private void downloadAndShareDiagnosticBundle(String jobId) {
        progress.setVisibility(View.VISIBLE);
        state.setText("正在下载并校验安全诊断包…");
        executor.execute(() -> {
            try {
                byte[] data = client.getBytes(
                        "/ops/v1/jobs/" + jobId + "/diagnostic-bundle", 2 * 1024 * 1024);
                File directory = new File(getCacheDir(), "diagnostic-share");
                if ((!directory.exists() && !directory.mkdirs()) || !directory.isDirectory()) {
                    throw new IllegalStateException("diagnostic_share_cache_unavailable");
                }
                File file = new File(directory, "ColorVision-diagnostics.zip");
                try (FileOutputStream output = new FileOutputStream(file, false)) {
                    output.write(data);
                    output.flush();
                }
                Uri uri = FileProvider.getUriForFile(
                        this, getPackageName() + ".fileprovider", file);
                runOnUiThread(() -> shareDiagnosticBundle(uri, data.length));
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void shareDiagnosticBundle(Uri uri, int sizeBytes) {
        progress.setVisibility(View.GONE);
        state.setText("安全诊断包已校验，可选择接收应用");
        details.setText("已下载 " + Math.max(1, Math.round(sizeBytes / 1024f))
                + " KiB 的脱敏 ZIP。临时副本位于应用缓存，由 Android 控制访问；接收应用仅获得本次文件的只读权限。");
        Intent share = new Intent(Intent.ACTION_SEND);
        share.setType("application/zip");
        share.putExtra(Intent.EXTRA_SUBJECT, "ColorVision 安全诊断包");
        share.putExtra(Intent.EXTRA_STREAM, uri);
        share.setClipData(ClipData.newRawUri("ColorVision 安全诊断包", uri));
        share.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        startActivity(Intent.createChooser(share, "分享安全诊断包"));
    }

    private void createDiagnosticJob() {
        progress.setVisibility(View.VISIBLE);
        state.setText("正在生成并校验脱敏诊断包…");
        executor.execute(() -> {
            try {
                JSONObject job = createAndApproveJob(
                        "ops.diagnostics.bundle.create", "现场支持诊断", new JSONObject(),
                        "diagnostic_bundle_job_missing", "已配对手机明确确认生成脱敏诊断包");
                String jobId = job.optString("jobId", "");
                if (!"completed".equals(job.optString("status", "")) || jobId.isEmpty()) {
                    throw new IllegalStateException("diagnostic_bundle_not_completed");
                }
                runOnUiThread(() -> downloadAndShareDiagnosticBundle(jobId));
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void confirmCreateWindowSnapshotJob() {
        new AlertDialog.Builder(this)
                .setTitle("采集并预览主窗口快照")
                .setMessage("确认后会先显示或还原 ColorVision 主窗口，再立即采集一张 JPEG；手机会校验后预览，不再等待电脑端共签。不会捕获整个桌面，也不会连续录屏；画面可能包含当前可见的检测数据。仅本申请设备可在 5 分钟内读取一次，读取后电脑端立即销毁。")
                .setNegativeButton("取消", null)
                .setPositiveButton("确认采集", (dialog, which) -> createWindowSnapshotJob())
                .show();
    }

    private void createWindowSnapshotJob() {
        progress.setVisibility(View.VISIBLE);
        state.setText("正在采集主窗口安全快照…");
        executor.execute(() -> {
            try {
                client.post("/ops/v1/actions/window/show", new JSONObject());
                JSONObject job = createAndApproveJob(
                        "ops.window.snapshot.capture", "现场远程调试主窗口取证", new JSONObject(),
                        "window_snapshot_job_missing", "已配对手机明确确认采集单次主窗口快照");
                String jobId = job.optString("jobId", "");
                if (!"completed".equals(job.optString("status", "")) || jobId.isEmpty()) {
                    throw new IllegalStateException("window_snapshot_not_completed");
                }
                runOnUiThread(() -> downloadWindowSnapshot(jobId));
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void confirmWindowSnapshotDownload(String jobId) {
        if (jobId.isEmpty()) {
            Toast.makeText(this, "快照作业标识无效", Toast.LENGTH_LONG).show();
            return;
        }
        new AlertDialog.Builder(this)
                .setTitle("读取一次主窗口快照")
                .setMessage("将下载当前设备已明确确认采集的 ColorVision 主窗口 JPEG。SHA-256 校验通过后，电脑端证据立即销毁；应用先在本机预览，只有你再次点击分享才会交给其他应用。")
                .setNegativeButton("取消", null)
                .setPositiveButton("下载并预览", (dialog, which) -> downloadWindowSnapshot(jobId))
                .show();
    }

    private void downloadWindowSnapshot(String jobId) {
        progress.setVisibility(View.VISIBLE);
        state.setText("正在读取并校验一次性主窗口快照…");
        executor.execute(() -> {
            try {
                byte[] data = client.getBytes(
                        "/ops/v1/jobs/" + jobId + "/window-snapshot",
                        1536 * 1024, "image/jpeg", "window_snapshot");
                BitmapFactory.Options bounds = new BitmapFactory.Options();
                bounds.inJustDecodeBounds = true;
                BitmapFactory.decodeByteArray(data, 0, data.length, bounds);
                if (bounds.outWidth <= 0 || bounds.outHeight <= 0
                        || Math.max(bounds.outWidth, bounds.outHeight) > 1280) {
                    throw new IllegalStateException("window_snapshot_dimensions_rejected");
                }
                Bitmap bitmap = BitmapFactory.decodeByteArray(data, 0, data.length);
                if (bitmap == null) {
                    throw new IllegalStateException("window_snapshot_format_rejected");
                }

                File directory = new File(getCacheDir(), "diagnostic-share");
                if ((!directory.exists() && !directory.mkdirs()) || !directory.isDirectory()) {
                    throw new IllegalStateException("window_snapshot_cache_unavailable");
                }
                File file = new File(directory, "ColorVision-window-snapshot.jpg");
                try (FileOutputStream output = new FileOutputStream(file, false)) {
                    output.write(data);
                    output.flush();
                }
                Uri uri = FileProvider.getUriForFile(
                        this, getPackageName() + ".fileprovider", file);
                runOnUiThread(() -> showWindowSnapshotPreview(bitmap, uri, data.length));
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void showWindowSnapshotPreview(Bitmap bitmap, Uri uri, int sizeBytes) {
        progress.setVisibility(View.GONE);
        title.setText("主窗口安全快照");
        state.setText("一次性证据已校验并从电脑端销毁");
        details.setText("已读取 " + Math.max(1, Math.round(sizeBytes / 1024f))
                + " KiB JPEG，仅包含采集时的 ColorVision 主窗口。当前预览副本位于 Android 应用缓存；请确认画面后再决定是否分享。");
        actions.removeAllViews();

        ImageView preview = new ImageView(this);
        preview.setContentDescription("ColorVision 主窗口快照预览");
        preview.setAdjustViewBounds(true);
        preview.setMaxHeight(dp(520));
        preview.setScaleType(ImageView.ScaleType.FIT_CENTER);
        preview.setBackgroundColor(Color.rgb(28, 34, 42));
        preview.setImageBitmap(bitmap);
        LinearLayout.LayoutParams previewParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        previewParams.setMargins(0, 0, 0, dp(14));
        actions.addView(preview, previewParams);

        Button share = new Button(this);
        share.setText("分享这张主窗口快照");
        share.setOnClickListener(v -> shareWindowSnapshot(uri));
        actions.addView(share, actionParams());

        Button jobs = new Button(this);
        jobs.setText("返回作业与审批");
        jobs.setOnClickListener(v -> showJobs());
        actions.addView(jobs, actionParams());
    }

    private void shareWindowSnapshot(Uri uri) {
        Intent share = new Intent(Intent.ACTION_SEND);
        share.setType("image/jpeg");
        share.putExtra(Intent.EXTRA_SUBJECT, "ColorVision 主窗口安全快照");
        share.putExtra(Intent.EXTRA_STREAM, uri);
        share.setClipData(ClipData.newRawUri("ColorVision 主窗口安全快照", uri));
        share.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        startActivity(Intent.createChooser(share, "分享主窗口安全快照"));
    }

    private JSONObject createAndApproveJob(String capabilityId, String reason, JSONObject input,
                                           String missingJobCode, String approvalReason) throws Exception {
        JSONObject body = new JSONObject();
        body.put("capabilityId", capabilityId);
        body.put("reason", reason);
        body.put("input", input);
        JSONObject created = client.post("/ops/v1/jobs", body);
        JSONObject createdData = created.optJSONObject("data");
        JSONObject createdJob = createdData == null ? null : createdData.optJSONObject("job");
        String jobId = createdJob == null ? "" : createdJob.optString("jobId", "");
        if (jobId.isEmpty()) {
            throw new IllegalStateException(missingJobCode);
        }
        JSONObject decision = new JSONObject();
        decision.put("approved", true);
        decision.put("reason", approvalReason);
        JSONObject response = client.post("/ops/v1/jobs/" + jobId + "/decision", decision);
        JSONObject responseData = response.optJSONObject("data");
        JSONObject completedJob = responseData == null ? null : responseData.optJSONObject("job");
        if (completedJob == null) {
            throw new IllegalStateException(missingJobCode);
        }
        return completedJob;
    }

    private void confirmRestartMqtt() {
        new AlertDialog.Builder(this)
                .setTitle("确认重启 MQTT 服务")
                .setMessage("确认后将立即通过 ColorVisionServiceHost 重启固定白名单中的 Mosquitto 服务，消息与设备通信可能短暂中断后自动恢复。手机不能选择其他服务、命令、路径或参数。")
                .setNegativeButton("取消", null)
                .setPositiveButton("确认重启", (dialog, which) -> restartMqtt())
                .show();
    }

    private void restartMqtt() {
        progress.setVisibility(View.VISIBLE);
        state.setText("正在通过固定白名单重启 MQTT…");
        executor.execute(() -> {
            try {
                JSONObject input = new JSONObject();
                input.put("serviceId", "mosquitto");
                JSONObject job = createAndApproveJob(
                        "ops.service.restart", "现场 MQTT 通信恢复", input,
                        "mqtt_restart_job_missing", "已配对手机明确确认固定 MQTT 恢复");
                String status = job.optString("status", "");
                runOnUiThread(() -> {
                    Toast.makeText(this, "completed".equals(status)
                            ? "MQTT 消息服务已重启" : "MQTT 重启未完成，请查看作业结果", Toast.LENGTH_LONG).show();
                    showJobs();
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void confirmRestartApplication() {
        new AlertDialog.Builder(this)
                .setTitle("确认重启 ColorVision")
                .setMessage("确认后只会干净重启当前 ColorVision 应用，不会选择程序、路径、命令或启动参数。正在执行检测时电脑端会拒绝；重启期间会短暂断线，应用将保留配对资料并自动等待恢复。")
                .setNegativeButton("取消", null)
                .setPositiveButton("确认重启", (dialog, which) -> restartApplication())
                .show();
    }

    private void restartApplication() {
        progress.setVisibility(View.VISIBLE);
        title.setText("正在重启 ColorVision");
        state.setText("正在检查检测状态并提交固定重启作业…");
        details.setText("安全通道会短暂断开；请保持此页打开，配对资料不会被移除。");
        executor.execute(() -> {
            try {
                JSONObject snapshotResponse = client.get("/ops/v1/snapshot");
                JSONObject snapshot = snapshotResponse.optJSONObject("data");
                long previousUptimeSeconds = snapshot == null
                        ? 0L : snapshot.optLong("uptimeSeconds", 0L);

                JSONObject flowResponse = client.get("/ops/v1/flow/runtime");
                JSONObject flow = flowResponse.optJSONObject("data");
                if (flow == null || !flow.optBoolean("available", false)) {
                    throw new IllegalStateException("application_restart_flow_status_unavailable");
                }
                if (flow.optBoolean("isActive", false)) {
                    throw new IllegalStateException("application_restart_flow_active");
                }

                JSONObject job = createAndApproveJob(
                        "ops.application.restart", "现场 ColorVision 应用恢复", new JSONObject(),
                        "application_restart_job_missing", "已配对手机明确确认重启当前 ColorVision 应用");
                String jobId = job.optString("jobId", "");
                String status = job.optString("status", "");
                if (jobId.isEmpty()) {
                    throw new IllegalStateException("application_restart_job_missing");
                }
                if (!"executing".equals(status) && !"completed".equals(status)) {
                    throw new IllegalStateException("application_restart_not_scheduled");
                }

                waitForApplicationRestart(jobId, previousUptimeSeconds);
                runOnUiThread(() -> {
                    progress.setVisibility(View.GONE);
                    Toast.makeText(this, "ColorVision 已完成重启并自动重连", Toast.LENGTH_LONG).show();
                    showDashboard();
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showApplicationRestartFailure(ex));
            }
        });
    }

    private void waitForApplicationRestart(String jobId, long previousUptimeSeconds) throws Exception {
        long deadline = System.currentTimeMillis() + 90_000L;
        boolean sawDisconnect = false;
        while (System.currentTimeMillis() < deadline) {
            Thread.sleep(2_000L);
            try {
                JSONObject snapshotResponse = client.get("/ops/v1/snapshot");
                JSONObject snapshot = snapshotResponse.optJSONObject("data");
                JSONObject jobsResponse = client.get("/ops/v1/jobs");
                JSONObject job = findJobById(jobsResponse, jobId);
                if (job == null) {
                    throw new IllegalStateException("application_restart_job_missing");
                }

                String status = job.optString("status", "");
                if ("failed".equals(status) || "rejected".equals(status)
                        || "rejected_local".equals(status)) {
                    throw new IllegalStateException("application_restart_failed");
                }
                long currentUptimeSeconds = snapshot == null
                        ? Long.MAX_VALUE : snapshot.optLong("uptimeSeconds", Long.MAX_VALUE);
                boolean restarted = sawDisconnect
                        || currentUptimeSeconds < previousUptimeSeconds
                        || currentUptimeSeconds < 90L;
                if ("completed".equals(status) && restarted) {
                    return;
                }
            } catch (IllegalStateException ex) {
                String message = ex.getMessage();
                if (message != null && message.startsWith("application_restart_")) {
                    throw ex;
                }
                sawDisconnect = true;
            } catch (Exception ex) {
                sawDisconnect = true;
            }
        }
        throw new IllegalStateException("application_restart_reconnect_timeout");
    }

    private JSONObject findJobById(JSONObject response, String jobId) {
        JSONObject data = response.optJSONObject("data");
        JSONArray jobs = data == null ? null : data.optJSONArray("jobs");
        if (jobs == null) {
            return null;
        }
        for (int index = 0; index < jobs.length(); index++) {
            JSONObject job = jobs.optJSONObject(index);
            if (job != null && jobId.equals(job.optString("jobId", ""))) {
                return job;
            }
        }
        return null;
    }

    private void showApplicationRestartFailure(Exception ex) {
        progress.setVisibility(View.GONE);
        dashboardVisible = false;
        title.setText("ColorVision 重启未完成");
        state.setText(readableError(ex));
        details.setText("本机设备密钥、证书指纹和配对资料均已保留。可以返回运维主页重试连接，或查看作业时间线确认电脑端结果。");
        actions.removeAllViews();

        Button reconnect = new Button(this);
        reconnect.setText("重新连接运维通道");
        reconnect.setOnClickListener(v -> openExistingProfile());
        actions.addView(reconnect, actionParams());

        Button jobs = new Button(this);
        jobs.setText("查看作业时间线");
        jobs.setOnClickListener(v -> showJobs());
        actions.addView(jobs, actionParams());

        Button selfCheck = new Button(this);
        selfCheck.setText("运行连接自检");
        selfCheck.setOnClickListener(v -> runConnectionSelfCheck());
        actions.addView(selfCheck, actionParams());
    }

    private void confirmDeploymentReceipt() {
        new AlertDialog.Builder(this)
                .setTitle("提交部署确认")
                .setMessage("仅提交本移动伴侣当前版本的验证收据，不会触发远程部署。")
                .setNegativeButton("取消", null)
                .setPositiveButton("确认", (dialog, which) -> submitDeploymentReceipt())
                .show();
    }

    private void submitDeploymentReceipt() {
        executor.execute(() -> {
            try {
                String version = getPackageManager().getPackageInfo(getPackageName(), 0).versionName;
                JSONObject body = new JSONObject();
                body.put("releaseId", "android-companion-" + version);
                body.put("version", version);
                body.put("status", "verified");
                body.put("evidenceSha256", "");
                JSONObject response = client.post("/ops/v1/deployment-receipts", body);
                runOnUiThread(() -> {
                    state.setText("部署确认已记录");
                    details.setText(pretty(response));
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void showSupportCenter() {
        showingDashboardSummary = false;
        leaveLiveMonitor();
        supportCenterVisible = true;
        supportAutoRefresh = false;
        loadSupportCenter(true);
    }

    private void loadSupportCenter(boolean showBusy) {
        if (!supportCenterVisible) {
            return;
        }
        supportRefreshHandler.removeCallbacks(supportRefresh);
        if (showBusy) {
            progress.setVisibility(View.VISIBLE);
            state.setText("正在刷新受控支持会话…");
        }
        executor.execute(() -> {
            try {
                JSONObject sessionsResponse = client.get("/ops/v1/support-sessions");
                JSONObject sessionsData = sessionsResponse.optJSONObject("data");
                if (sessionsData == null) {
                    throw new IllegalStateException("incomplete_support_response");
                }
                JSONObject selected = selectSupportSession(sessionsData.optJSONArray("sessions"));
                JSONObject messagesData = null;
                if (selected != null) {
                    String sessionId = selected.optString("sessionId", "");
                    if (!sessionId.isEmpty()) {
                        messagesData = client.get("/ops/v1/support-sessions/" + sessionId + "/messages")
                                .optJSONObject("data");
                    }
                }
                JSONObject finalSelected = selected;
                JSONObject finalMessagesData = messagesData;
                runOnUiThread(() -> {
                    if (supportCenterVisible) {
                        renderSupportCenter(sessionsData, finalSelected, finalMessagesData);
                    }
                });
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    if (supportCenterVisible) {
                        showTransientError(ex);
                    }
                });
            }
        });
    }

    private JSONObject selectSupportSession(JSONArray sessions) {
        if (sessions == null || sessions.length() == 0) {
            return null;
        }
        JSONObject awaiting = null;
        JSONObject fallback = null;
        for (int index = 0; index < sessions.length(); index++) {
            JSONObject session = sessions.optJSONObject(index);
            if (session == null) {
                continue;
            }
            if (fallback == null) {
                fallback = session;
            }
            String status = session.optString("status", "expired");
            if ("active".equals(status)) {
                return session;
            }
            if (awaiting == null && "awaiting_local_consent".equals(status)) {
                awaiting = session;
            }
        }
        return awaiting == null ? fallback : awaiting;
    }

    private void renderSupportCenter(JSONObject sessionsData, JSONObject selected, JSONObject messagesData) {
        progress.setVisibility(View.GONE);
        title.setText("引导支持会话");
        actions.removeAllViews();

        JSONObject session = messagesData == null ? selected : messagesData.optJSONObject("session");
        String status = session == null ? "" : session.optString("status", "expired");
        supportAutoRefresh = "awaiting_local_consent".equals(status);
        if (session == null) {
            state.setText("当前没有支持会话");
            details.setText("可申请 15 分钟引导支持。电脑端必须本机同意后，双方才能交换最多 500 字的有限文本；不开放远程桌面、命令、文件或凭据。\n\n"
                    + sessionsData.optString("privacyNotice", "请勿发送密码、密钥或客户数据。"));
            addSupportRequestButton();
        } else {
            state.setText(supportStatusLabel(status));
            details.setText(formatSupportSession(session, messagesData, sessionsData));
            if ("active".equals(status) && session.optBoolean("canSendMessages", false)) {
                addSupportComposer(session.optString("sessionId", ""));
            } else if ("expired".equals(status) || "rejected_local".equals(status)) {
                addSupportRequestButton();
            }
        }

        Button refresh = new Button(this);
        refresh.setText("立即刷新会话");
        refresh.setOnClickListener(v -> loadSupportCenter(true));
        actions.addView(refresh, actionParams());

        Button back = new Button(this);
        back.setText("返回现场运维概览");
        back.setOnClickListener(v -> showDashboard());
        actions.addView(back, actionParams());
        scheduleSupportRefresh();
    }

    private void addSupportRequestButton() {
        Button request = new Button(this);
        request.setText("申请 15 分钟引导支持");
        request.setOnClickListener(v -> confirmSupportRequest());
        actions.addView(request, actionParams());
    }

    private void addSupportComposer(String sessionId) {
        EditText input = new EditText(this);
        input.setHint("输入有限现场说明（最多 500 字，请勿发送密码或客户数据）");
        input.setMinLines(2);
        input.setMaxLines(4);
        LinearLayout.LayoutParams inputParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        inputParams.setMargins(0, 0, 0, dp(10));
        actions.addView(input, inputParams);

        Button send = new Button(this);
        send.setText("发送到已同意的支持会话");
        send.setOnClickListener(v -> sendSupportMessage(sessionId, input.getText().toString()));
        actions.addView(send, actionParams());
    }

    private String formatSupportSession(JSONObject session, JSONObject messagesData, JSONObject sessionsData) {
        String status = session.optString("status", "expired");
        StringBuilder text = new StringBuilder();
        text.append("模式：").append(supportModeLabel(session.optString("mode", "guided")))
                .append("\n状态：").append(supportStatusLabel(status))
                .append("\n创建：").append(shortTime(session.optString("createdAt", "")))
                .append("\n到期：").append(shortTime(session.optString("expiresAt", "")));
        if ("active".equals(status)) {
            text.append("\n剩余：").append(formatDuration(session.optInt("remainingSeconds", 0)));
        } else if ("awaiting_local_consent".equals(status)) {
            text.append("\n\n请在电脑端“局域网控制”中本机同意。未同意前，手机和 Web 中继都不能写入消息。");
        }

        JSONArray messages = messagesData == null ? null : messagesData.optJSONArray("messages");
        if (messages != null && messages.length() > 0) {
            text.append("\n\n有限消息");
            for (int index = 0; index < messages.length(); index++) {
                JSONObject message = messages.optJSONObject(index);
                if (message == null) {
                    continue;
                }
                String direction = message.optString("direction", "from_support");
                text.append("\n\n")
                        .append("from_device".equals(direction) ? "现场设备" : "远程支持")
                        .append(" · ").append(shortTime(message.optString("createdAt", "")))
                        .append("\n").append(message.optString("text", ""));
            }
        } else if ("active".equals(status)) {
            text.append("\n\n当前还没有消息。");
        }
        String privacy = messagesData == null
                ? sessionsData.optString("privacyNotice", "")
                : messagesData.optString("privacyNotice", sessionsData.optString("privacyNotice", ""));
        if (!privacy.isEmpty()) {
            text.append("\n\n").append(privacy);
        }
        return text.toString();
    }

    private String supportStatusLabel(String status) {
        switch (status) {
            case "awaiting_local_consent": return "等待电脑端本机同意";
            case "active": return "支持会话已激活";
            case "rejected_local": return "电脑端已拒绝";
            case "expired": return "支持会话已到期";
            default: return "支持会话状态未知";
        }
    }

    private String supportModeLabel(String mode) {
        return "diagnostics".equals(mode) ? "诊断" : "引导";
    }

    private void scheduleSupportRefresh() {
        supportRefreshHandler.removeCallbacks(supportRefresh);
        if (activityResumed && supportCenterVisible && supportAutoRefresh) {
            supportRefreshHandler.postDelayed(supportRefresh, 5000);
        }
    }

    private void leaveSupportCenter() {
        supportCenterVisible = false;
        supportAutoRefresh = false;
        supportRefreshHandler.removeCallbacks(supportRefresh);
    }

    private void showLiveMonitor() {
        showingDashboardSummary = false;
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = true;
        liveMonitorVisible = true;
        liveMonitorAutoRefresh = true;
        liveMonitorCancelAvailable = false;
        liveMonitorCancelInFlight = false;
        liveMonitorLatestSnapshot = null;
        liveMonitorTrend.reset();
        title.setText("远程持续观察");
        state.setText("正在采集第一份有界运行快照…");
        details.setText("仅在此页面位于前台时每 10 秒刷新；切到后台会自动停止网络请求。服务器不保存采样历史。只有主检测活动时才会提供有界取消动作。 ");
        renderLiveMonitorActions();
        loadLiveMonitor(true);
    }

    private void loadLiveMonitor(boolean showBusy) {
        if (!liveMonitorVisible || liveMonitorRefreshInFlight) {
            return;
        }
        liveMonitorRefreshInFlight = true;
        int requestGeneration = liveMonitorGeneration;
        liveMonitorRefreshHandler.removeCallbacks(liveMonitorRefresh);
        renderLiveMonitorActions();
        if (showBusy) {
            progress.setVisibility(View.VISIBLE);
            state.setText(liveMonitorTrend.size() == 0
                    ? "正在采集第一份有界运行快照…"
                    : "正在立即刷新持续观察…");
        }
        executor.execute(() -> {
            try {
                JSONObject response = client.get("/ops/v1/monitor");
                JSONObject snapshot = response.optJSONObject("data");
                if (snapshot == null) {
                    throw new IllegalStateException("incomplete_live_monitor_response");
                }
                runOnUiThread(() -> {
                    if (requestGeneration != liveMonitorGeneration) {
                        return;
                    }
                    liveMonitorRefreshInFlight = false;
                    if (!liveMonitorVisible) {
                        return;
                    }
                    progress.setVisibility(View.GONE);
                    liveMonitorLatestSnapshot = snapshot;
                    liveMonitorTrend.add(createLiveMonitorSample(snapshot));
                    JSONObject flow = snapshot.optJSONObject("flow");
                    liveMonitorCancelAvailable = flow != null
                            && flow.optBoolean("isActive", false)
                            && flow.optBoolean("cancelAvailable", false);
                    state.setText(liveMonitorState(snapshot));
                    details.setText(formatLiveMonitorSnapshot(snapshot));
                    renderLiveMonitorActions();
                    scheduleLiveMonitorRefresh();
                });
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    if (requestGeneration != liveMonitorGeneration) {
                        return;
                    }
                    liveMonitorRefreshInFlight = false;
                    if (!liveMonitorVisible) {
                        return;
                    }
                    progress.setVisibility(View.GONE);
                    state.setText(liveMonitorAutoRefresh
                            ? "本轮观察失败 · 10 秒后自动重试"
                            : "本轮观察失败");
                    details.setText(readableError(ex)
                            + "\n\n持续观察本身不会删除配对资料或修改检测流程；只有你明确确认取消动作后才会介入当前检测。 ");
                    renderLiveMonitorActions();
                    scheduleLiveMonitorRefresh();
                });
            }
        });
    }

    private void renderLiveMonitorActions() {
        actions.removeAllViews();

        Button refresh = new Button(this);
        refresh.setText("立即刷新");
        refresh.setEnabled(!liveMonitorRefreshInFlight && !liveMonitorCancelInFlight);
        refresh.setOnClickListener(v -> loadLiveMonitor(true));
        actions.addView(refresh, actionParams());

        Button toggle = new Button(this);
        toggle.setText(liveMonitorAutoRefresh ? "暂停自动观察" : "恢复每 10 秒观察");
        toggle.setEnabled(!liveMonitorCancelInFlight);
        toggle.setOnClickListener(v -> {
            liveMonitorAutoRefresh = !liveMonitorAutoRefresh;
            liveMonitorRefreshHandler.removeCallbacks(liveMonitorRefresh);
            if (liveMonitorAutoRefresh) {
                state.setText("自动观察已恢复 · 正在刷新");
                renderLiveMonitorActions();
                loadLiveMonitor(true);
            } else {
                state.setText("自动观察已暂停 · 当前快照保留");
                renderLiveMonitorActions();
            }
        });
        actions.addView(toggle, actionParams());

        Button cancelFlow = new Button(this);
        cancelFlow.setText(liveMonitorCancelAvailable
                ? "取消当前检测"
                : "当前没有可取消的主检测");
        cancelFlow.setEnabled(liveMonitorCancelAvailable
                && !liveMonitorRefreshInFlight
                && !liveMonitorCancelInFlight);
        cancelFlow.setOnClickListener(v -> confirmCancelCurrentFlow());
        actions.addView(cancelFlow, actionParams());

        Button share = new Button(this);
        share.setText(liveMonitorTrend.size() < 2
                ? "分享本次趋势（至少需要 2 个样本）"
                : "分享本次脱敏趋势");
        share.setEnabled(liveMonitorTrend.size() >= 2);
        share.setOnClickListener(v -> shareLiveMonitorTrend());
        actions.addView(share, actionParams());

        Button back = new Button(this);
        back.setText("返回现场运维概览");
        back.setOnClickListener(v -> showDashboard());
        actions.addView(back, actionParams());
    }

    private void scheduleLiveMonitorRefresh() {
        liveMonitorRefreshHandler.removeCallbacks(liveMonitorRefresh);
        if (activityResumed && liveMonitorVisible && liveMonitorAutoRefresh && !isFinishing()) {
            liveMonitorRefreshHandler.postDelayed(
                    liveMonitorRefresh, LIVE_MONITOR_REFRESH_MILLISECONDS);
        }
    }

    private void leaveLiveMonitor() {
        liveMonitorGeneration++;
        liveMonitorVisible = false;
        liveMonitorAutoRefresh = false;
        liveMonitorRefreshInFlight = false;
        liveMonitorCancelAvailable = false;
        liveMonitorCancelInFlight = false;
        liveMonitorLatestSnapshot = null;
        liveMonitorTrend.reset();
        liveMonitorRefreshHandler.removeCallbacks(liveMonitorRefresh);
    }

    private OperationsLiveMonitorTrend.Sample createLiveMonitorSample(JSONObject snapshot) {
        JSONObject flow = snapshot.optJSONObject("flow");
        JSONObject performance = snapshot.optJSONObject("performance");
        JSONObject mainUi = performance == null ? null : performance.optJSONObject("mainUi");
        JSONObject alerts = snapshot.optJSONObject("alerts");
        Long uiLatency = mainUi != null
                && mainUi.has("latencyMilliseconds")
                && !mainUi.isNull("latencyMilliseconds")
                ? mainUi.optLong("latencyMilliseconds", 0)
                : null;
        return new OperationsLiveMonitorTrend.Sample(
                System.currentTimeMillis(),
                performance == null ? 0 : performance.optDouble("cpuPercent", 0),
                performance == null ? 0 : performance.optDouble("workingSetMb", 0),
                mainUi == null ? "unavailable" : mainUi.optString("state", "unavailable"),
                uiLatency,
                flow == null ? "unavailable" : flow.optString("phase", "unavailable"),
                alerts == null ? 0 : alerts.optInt("count", 0));
    }

    private void confirmCancelCurrentFlow() {
        if (!liveMonitorCancelAvailable) {
            Toast.makeText(this, "当前没有可取消的主检测", Toast.LENGTH_LONG).show();
            return;
        }
        new AlertDialog.Builder(this)
                .setTitle("取消当前检测？")
                .setMessage("只会向当前主工作区正在执行的检测发送取消请求，不会选择、启动或修改其他流程，也不接受远程参数。确认后立即执行并记录审计。")
                .setNegativeButton("继续观察", null)
                .setPositiveButton("确认取消检测", (dialog, which) -> requestCancelCurrentFlow())
                .show();
    }

    private void requestCancelCurrentFlow() {
        liveMonitorRefreshHandler.removeCallbacks(liveMonitorRefresh);
        liveMonitorCancelInFlight = true;
        progress.setVisibility(View.VISIBLE);
        state.setText("正在提交并确认取消请求…");
        renderLiveMonitorActions();
        executor.execute(() -> {
            try {
                JSONObject createBody = new JSONObject();
                createBody.put("capabilityId", "ops.flow.cancel");
                createBody.put("reason", "已配对手机明确取消当前主检测");
                createBody.put("input", new JSONObject());
                JSONObject createResponse = client.post("/ops/v1/jobs", createBody);
                JSONObject createData = createResponse.optJSONObject("data");
                JSONObject createdJob = createData == null ? null : createData.optJSONObject("job");
                String jobId = createdJob == null ? "" : createdJob.optString("jobId", "");
                if (jobId.isEmpty()) {
                    throw new IllegalStateException("invalid_flow_cancel_job");
                }

                JSONObject decisionBody = new JSONObject();
                decisionBody.put("approved", true);
                decisionBody.put("reason", "已配对手机明确确认取消当前主检测");
                JSONObject decisionResponse = client.post("/ops/v1/jobs/" + jobId + "/decision", decisionBody);
                JSONObject decisionData = decisionResponse.optJSONObject("data");
                JSONObject decidedJob = decisionData == null ? null : decisionData.optJSONObject("job");
                String status = decidedJob == null ? "" : decidedJob.optString("status", "");
                runOnUiThread(() -> {
                    progress.setVisibility(View.GONE);
                    liveMonitorCancelAvailable = false;
                    liveMonitorCancelInFlight = false;
                    Toast.makeText(this,
                            "completed".equals(status)
                                    ? "已向当前检测发送取消请求"
                                    : "当前检测未取消，已保留审计结果",
                            Toast.LENGTH_LONG).show();
                    if (liveMonitorVisible) {
                        loadLiveMonitor(true);
                    }
                });
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    liveMonitorCancelAvailable = false;
                    liveMonitorCancelInFlight = false;
                    showTransientError(ex);
                    if (liveMonitorVisible) {
                        scheduleLiveMonitorRefresh();
                    }
                });
            }
        });
    }

    private String liveMonitorState(JSONObject snapshot) {
        JSONObject flow = snapshot.optJSONObject("flow");
        JSONObject performance = snapshot.optJSONObject("performance");
        JSONObject mainUi = performance == null ? null : performance.optJSONObject("mainUi");
        JSONObject alerts = snapshot.optJSONObject("alerts");
        JSONObject devices = snapshot.optJSONObject("devices");
        JSONObject messageChannel = snapshot.optJSONObject("messageChannel");
        String uiState = mainUi == null ? "unavailable" : mainUi.optString("state", "unavailable");
        int criticalCount = alerts == null ? 0 : alerts.optInt("criticalCount", 0);
        int errorCount = alerts == null ? 0 : alerts.optInt("errorCount", 0);
        boolean flowActive = flow != null && flow.optBoolean("isActive", false);

        String prefix;
        int deviceAttentionCount = devices == null || !devices.optBoolean("available", false)
                ? 0 : devices.optInt("attentionCount", 0);
        boolean messageChannelAttention = messageChannel != null
                && messageChannel.optBoolean("available", false)
                && messageChannel.optBoolean("attentionRequired", false);
        if ("unresponsive".equals(uiState)) {
            prefix = "主界面响应超时";
        } else if (criticalCount > 0) {
            prefix = "发现严重告警";
        } else if (messageChannelAttention) {
            prefix = "消息通道状态需要关注";
        } else if (deviceAttentionCount > 0) {
            prefix = "检测设备状态需要关注";
        } else if (errorCount > 0) {
            prefix = "发现错误事件";
        } else if ("slow".equals(uiState)) {
            prefix = "主界面响应偏慢";
        } else if (flowActive) {
            prefix = "检测活动正在进行";
        } else {
            prefix = "当前聚合状态稳定";
        }
        return prefix + " · 本次内存样本 " + liveMonitorTrend.size()
                + "/" + OperationsLiveMonitorTrend.MAX_SAMPLES;
    }

    private String formatLiveMonitorSnapshot(JSONObject snapshot) {
        JSONObject flow = snapshot.optJSONObject("flow");
        JSONObject performance = snapshot.optJSONObject("performance");
        JSONObject devices = snapshot.optJSONObject("devices");
        JSONObject messageChannel = snapshot.optJSONObject("messageChannel");
        JSONObject alerts = snapshot.optJSONObject("alerts");
        StringBuilder text = new StringBuilder();
        if (flow == null || !flow.optBoolean("available", false)) {
            text.append("检测：流程运行时暂不可用");
        } else {
            text.append("检测：").append(flowPhaseLabel(flow.optString("phase", "idle")));
            if (flow.optBoolean("progressAvailable", false)
                    && flow.has("progressPercent") && !flow.isNull("progressPercent")) {
                text.append(" · ").append(roundOne(flow.optDouble("progressPercent", 0))).append('%');
            }
            if (flow.has("elapsedMilliseconds") && !flow.isNull("elapsedMilliseconds")) {
                text.append(" · 已用时 ")
                        .append(formatElapsedMilliseconds(flow.optLong("elapsedMilliseconds", 0)));
            }
            String lastStatus = flow.optString("lastRunStatus", "none");
            if (!"none".equals(lastStatus)) {
                text.append("\n最近结果：").append(flowOutcomeLabel(lastStatus));
            }
        }

        text.append("\n\n消息通道：");
        if (messageChannel == null) {
            text.append("状态暂不可用");
        } else {
            text.append(formatMessageChannelHealth(messageChannel, false));
        }

        if (devices == null || !devices.optBoolean("available", false)) {
            text.append("\n\n检测设备：运行状态汇总暂不可用");
        } else if (!devices.optBoolean("hasConfiguredDevices", false)) {
            text.append("\n\n检测设备：当前未发现已加载设备");
        } else {
            text.append("\n\n检测设备：共 ").append(devices.optInt("totalCount", 0)).append(" 台 · ");
            appendDeviceStateCounts(text, devices);
            appendDeviceUnavailableReasons(text, devices);
        }

        if (performance == null) {
            text.append("\n\n性能：暂不可用");
        } else {
            text.append("\n\n性能：CPU ")
                    .append(roundOne(performance.optDouble("cpuPercent", 0))).append('%')
                    .append(" · 工作集 ")
                    .append(roundOne(performance.optDouble("workingSetMb", 0))).append(" MB");
            JSONObject mainUi = performance.optJSONObject("mainUi");
            text.append("\n主界面：")
                    .append(mainUi == null ? "不可用"
                            : uiResponsivenessLabel(mainUi.optString("state", "unavailable")));
            if (mainUi != null && mainUi.has("latencyMilliseconds")
                    && !mainUi.isNull("latencyMilliseconds")) {
                text.append(" · ").append(mainUi.optLong("latencyMilliseconds", 0)).append(" ms");
            }
        }

        int alertCount = alerts == null ? 0 : alerts.optInt("count", 0);
        text.append("\n\n近期告警：").append(alertCount).append(" 条");
        if (alerts != null && alertCount > 0) {
            text.append(" · 警告 ").append(alerts.optInt("warningCount", 0))
                    .append(" · 错误 ").append(alerts.optInt("errorCount", 0))
                    .append(" · 严重 ").append(alerts.optInt("criticalCount", 0));
            String latestAt = shortTime(alerts.optString("latestOccurredAt", ""));
            if (!latestAt.isEmpty()) {
                text.append("\n最近告警：").append(latestAt);
            }
        }

        text.append("\n\n采集时间：").append(shortTime(snapshot.optString("capturedAt", "")))
                .append("\n刷新策略：仅当前台可见时每 ")
                .append(snapshot.optInt("suggestedRefreshSeconds", 10)).append(" 秒")
                .append(formatLiveMonitorTrend(liveMonitorTrend.summarize()))
                .append("\n\n服务器不保存采样历史；手机仅在内存保留最近 30 个样本，离开本页即清空。快照不含流程、模板、批次、节点、参数、结果、进程身份、主机、用户、端点、设备身份、Topic、消息载荷、配置、凭据、原始设备状态、日志正文或检测数据。 ");
        return text.toString();
    }

    private String formatLiveMonitorTrend(OperationsLiveMonitorTrend.Summary summary) {
        if (summary.sampleCount < 2) {
            return "\n\n本次趋势：再采集 1 个样本后显示。";
        }

        StringBuilder text = new StringBuilder();
        text.append("\n\n本次内存趋势：").append(summary.sampleCount)
                .append(" / ").append(OperationsLiveMonitorTrend.MAX_SAMPLES).append(" 个样本")
                .append(" · ").append(formatClock(summary.startedAtMilliseconds))
                .append(" 至 ").append(formatClock(summary.endedAtMilliseconds))
                .append(" · ").append(formatElapsedMilliseconds(
                        summary.endedAtMilliseconds - summary.startedAtMilliseconds))
                .append("\nCPU：均值 ").append(roundOne(summary.averageCpuPercent))
                .append("% · 峰值 ").append(roundOne(summary.maximumCpuPercent)).append('%')
                .append("\n工作集：").append(roundOne(summary.minimumWorkingSetMb))
                .append(" 至 ").append(roundOne(summary.maximumWorkingSetMb)).append(" MB")
                .append("\n主界面：最大延迟 ")
                .append(summary.maximumUiLatencyMilliseconds == null
                        ? "不可用" : summary.maximumUiLatencyMilliseconds + " ms")
                .append(" · 慢 ").append(summary.slowUiSampleCount)
                .append(" 次 · 超时 ").append(summary.unresponsiveUiSampleCount).append(" 次")
                .append("\n检测阶段：").append(flowPhaseLabel(summary.latestFlowPhase))
                .append(" · 切换 ").append(summary.flowPhaseTransitionCount).append(" 次")
                .append("\n告警计数：当前 ").append(summary.latestAlertCount)
                .append(" · 本次最高 ").append(summary.maximumAlertCount);
        return text.toString();
    }

    private String formatClock(long milliseconds) {
        if (milliseconds <= 0) {
            return "未知";
        }
        return new SimpleDateFormat("HH:mm:ss", Locale.CHINA).format(new Date(milliseconds));
    }

    private void shareLiveMonitorTrend() {
        OperationsLiveMonitorTrend.Summary summary = liveMonitorTrend.summarize();
        if (summary.sampleCount < 2) {
            Toast.makeText(this, "至少需要两个观察样本", Toast.LENGTH_SHORT).show();
            return;
        }
        String report = "ColorVision 远程观察趋势"
                + formatLiveMonitorTrend(summary)
                + formatLiveMonitorShareContext(liveMonitorLatestSnapshot)
                + "\n\n该文本只包含本次手机内存中的聚合趋势和当前脱敏运行汇总，不含流程、模板、批次、节点、参数、结果、进程身份、主机、用户、端点、设备身份、Topic、消息载荷、配置、凭据、原始设备状态、日志正文或检测数据。";
        shareSafeText("ColorVision 远程观察趋势", report);
    }

    private String formatLiveMonitorShareContext(JSONObject snapshot) {
        if (snapshot == null) {
            return "";
        }

        StringBuilder text = new StringBuilder();
        JSONObject messageChannel = snapshot.optJSONObject("messageChannel");
        if (messageChannel != null && messageChannel.optBoolean("available", false)) {
            text.append("\n\n消息通道：")
                    .append(messageChannelStateLabel(messageChannel.optString("state", "unavailable")))
                    .append(" · 订阅 ")
                    .append(messageChannel.optInt("activeSubscriptionCount", 0))
                    .append('/')
                    .append(messageChannel.optInt("registeredSubscriptionCount", 0));
        }

        JSONObject devices = snapshot.optJSONObject("devices");
        if (devices != null && devices.optBoolean("available", false)
                && devices.optBoolean("hasConfiguredDevices", false)) {
            text.append("\n检测设备：共 ").append(devices.optInt("totalCount", 0)).append(" 台 · ");
            appendDeviceStateCounts(text, devices);
            appendDeviceUnavailableReasons(text, devices);
        }
        return text.toString();
    }

    private void confirmSupportRequest() {
        new AlertDialog.Builder(this)
                .setTitle("申请引导支持")
                .setMessage("申请 15 分钟有限文本会话。电脑端必须本机同意；不开放远程桌面、命令或文件。")
                .setNegativeButton("取消", null)
                .setPositiveButton("提交申请", (dialog, which) -> submitSupportRequest())
                .show();
    }

    private void submitSupportRequest() {
        executor.execute(() -> {
            try {
                JSONObject body = new JSONObject();
                body.put("mode", "guided");
                body.put("reason", "现场设备请求引导支持");
                body.put("durationMinutes", 15);
                client.post("/ops/v1/support-sessions", body);
                runOnUiThread(() -> {
                    Toast.makeText(this, "支持请求已提交，等待电脑端本机同意", Toast.LENGTH_LONG).show();
                    showSupportCenter();
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void sendSupportMessage(String sessionId, String value) {
        String text = value == null ? "" : value.trim();
        if (text.isEmpty()) {
            Toast.makeText(this, "请输入现场说明", Toast.LENGTH_SHORT).show();
            return;
        }
        if (text.length() > 500) {
            Toast.makeText(this, "现场说明不能超过 500 字", Toast.LENGTH_LONG).show();
            return;
        }
        progress.setVisibility(View.VISIBLE);
        executor.execute(() -> {
            try {
                JSONObject body = new JSONObject();
                body.put("text", text);
                client.post("/ops/v1/support-sessions/" + sessionId + "/messages", body);
                runOnUiThread(() -> {
                    Toast.makeText(this, "有限消息已发送", Toast.LENGTH_SHORT).show();
                    loadSupportCenter(false);
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void showTransientError(Exception ex) {
        progress.setVisibility(View.GONE);
        state.setText("操作失败");
        details.setText(readableError(ex));
    }

    private void addAction(String label, String path) {
        Button button = new Button(this);
        button.setText(label);
        button.setOnClickListener(v -> loadCapability(path));
        actions.addView(button, actionParams());
    }

    private void loadCapability(String path) {
        showingDashboardSummary = "/ops/v1/snapshot".equals(path);
        leaveSupportCenter();
        leaveLiveMonitor();
        progress.setVisibility(View.VISIBLE);
        state.setText("正在读取…");
        executor.execute(() -> {
            try {
                JSONObject response = client.get(path);
                runOnUiThread(() -> {
                    progress.setVisibility(View.GONE);
                    state.setText(capabilityHeading(path));
                    details.setText(formatCapability(path, response));
                });
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    progress.setVisibility(View.GONE);
                    state.setText("读取失败");
                    details.setText(readableError(ex));
                });
            }
        });
    }

    private String formatCapability(String path, JSONObject response) {
        JSONObject data = response.optJSONObject("data");
        JSONObject payload = data == null ? response : data;
        if ("/ops/v1/alerts".equals(path)) {
            return formatAlerts(payload);
        }
        if ("/ops/v1/diagnostics/recent-events".equals(path)) {
            return formatRecentEvents(payload);
        }
        if ("/ops/v1/diagnostics/summary".equals(path)) {
            return formatDiagnosticSummary(payload);
        }
        if ("/ops/v1/diagnostics/performance".equals(path)) {
            return formatPerformanceSnapshot(payload);
        }
        if ("/ops/v1/flow/runtime".equals(path)) {
            return formatFlowRuntimeStatus(payload);
        }
        if ("/ops/v1/services/health".equals(path)) {
            return formatServiceHealth(payload);
        }
        if ("/ops/v1/devices/health".equals(path)) {
            return formatDeviceHealth(payload);
        }
        if ("/ops/v1/messaging/health".equals(path)) {
            return formatMessageChannelHealth(payload, true);
        }
        if ("/ops/v1/audit".equals(path)) {
            return formatAuditTimeline(payload);
        }
        if (!"/ops/v1/snapshot".equals(path)) {
            return pretty(payload);
        }

        JSONObject process = payload.optJSONObject("process");
        JSONObject window = payload.optJSONObject("mainWindow");
        JSONObject secureOperations = payload.optJSONObject("secureOperations");

        String version = payload.optString("version", "未知");
        long uptimeSeconds = payload.optLong("uptimeSeconds", 0);
        double memoryMb = process == null ? 0 : process.optDouble("memoryMb", 0);
        boolean windowExists = window != null && window.optBoolean("exists");
        boolean windowVisible = windowExists && window.optBoolean("isVisible");
        String windowState = window == null ? "未知" : localizeWindowState(window.optString("state", "未知"));
        boolean secureRunning = secureOperations != null && secureOperations.optBoolean("isRunning");
        int pairedDevices = secureOperations == null ? 0 : secureOperations.optInt("pairedDeviceCount", 0);
        boolean relayRunning = secureOperations != null && secureOperations.optBoolean("relayRunning");
        boolean relayConfigured = secureOperations != null && secureOperations.optBoolean("relayConfigured");

        StringBuilder summary = new StringBuilder();
        summary.append("ColorVision ").append(version)
                .append(" · 已运行 ").append(formatDuration(uptimeSeconds))
                .append("\n主窗口 ").append(windowVisible ? "可见" : windowExists ? "未显示" : "不可用")
                .append(" · ").append(windowState);
        if (memoryMb > 0) {
            summary.append(" · 内存 ").append(Math.round(memoryMb * 10) / 10.0).append(" MB");
        }
        summary.append("\n运维通道 ").append(secureRunning ? "已连接" : "未运行")
                .append(" · 已配对 ").append(pairedDevices).append(" 台")
                .append(" · 中继 ")
                .append(relayRunning ? "运行中" : relayConfigured ? "未启动" : "未配置");
        return summary.toString();
    }

    private String capabilityHeading(String path) {
        if ("/ops/v1/snapshot".equals(path)) {
            return "电脑端在线 · 安全通道已验证";
        }
        if ("/ops/v1/alerts".equals(path)) {
            return "当前告警已刷新";
        }
        if ("/ops/v1/diagnostics/recent-events".equals(path)) {
            return "近期日志摘要已刷新";
        }
        if ("/ops/v1/diagnostics/summary".equals(path)) {
            return "安全诊断摘要已刷新";
        }
        if ("/ops/v1/diagnostics/performance".equals(path)) {
            return "进程性能快照已刷新";
        }
        if ("/ops/v1/flow/runtime".equals(path)) {
            return "当前检测状态已刷新";
        }
        if ("/ops/v1/services/health".equals(path)) {
            return "白名单服务状态已刷新";
        }
        if ("/ops/v1/devices/health".equals(path)) {
            return "检测设备状态概览已刷新";
        }
        if ("/ops/v1/messaging/health".equals(path)) {
            return "消息通道健康已刷新";
        }
        if ("/ops/v1/audit".equals(path)) {
            return "近期远程操作记录已刷新";
        }
        return "读取成功 · " + path;
    }

    private String formatAuditTimeline(JSONObject payload) {
        JSONArray entries = payload.optJSONArray("entries");
        if (entries == null || entries.length() == 0) {
            return "当前没有远程操作记录。\n\n记录只包含时间、角色类型、固定动作和结果，不包含设备、人员、目标或关联标识。";
        }

        StringBuilder text = new StringBuilder();
        text.append("近期远程操作：").append(payload.optInt("count", entries.length())).append(" 条");
        int maximum = Math.min(entries.length(), 30);
        for (int index = 0; index < maximum; index++) {
            JSONObject entry = entries.optJSONObject(index);
            if (entry == null) {
                continue;
            }
            text.append("\n\n").append(index + 1).append(". ")
                    .append(auditActionLabel(entry.optString("action", "")))
                    .append("\n结果：").append(auditOutcomeLabel(entry.optString("outcome", "")))
                    .append(" · 发起方：").append(auditActorLabel(entry.optString("actorType", "")));
            String timestamp = shortTime(entry.optString("timestamp", ""));
            if (!timestamp.isEmpty()) {
                text.append("\n时间：").append(timestamp);
            }
        }
        text.append("\n\n只显示最近 30 条去标识记录；不返回设备 ID、人员名称、操作目标或内部关联 ID。内容不能用于识别具体人员。");
        return text.toString();
    }

    private String auditActorLabel(String value) {
        switch (value) {
            case "device": return "已配对手机";
            case "local-user": return "电脑本机人员";
            case "system": return "运维系统";
            case "support-relay": return "支持中继";
            default: return "受控运维通道";
        }
    }

    private String auditActionLabel(String value) {
        switch (value) {
            case "job.create": return "创建运维作业";
            case "job.approve": return "手机批准作业";
            case "job.reject": return "手机拒绝作业";
            case "job.local_cosign": return "电脑端共签作业";
            case "job.local_reject": return "电脑端拒绝作业";
            case "job.execution.start": return "开始执行受控作业";
            case "job.complete": return "作业执行完成";
            case "job.evidence.consume": return "读取一次性作业证据";
            case "desktop.action.execute": return "执行主窗口控制";
            case "diagnostics.performance.read": return "读取进程性能快照";
            case "flow.runtime.read": return "读取当前检测状态";
            case "monitor.read": return "持续观察运行状态";
            case "messaging.health.read": return "读取消息通道健康";
            case "diagnostic.bundle.download": return "下载安全诊断包";
            case "window.snapshot.download": return "读取主窗口安全快照";
            case "deployment.receipt.create": return "提交部署确认";
            case "support.request": return "申请引导支持会话";
            case "support.local_consent": return "电脑端同意支持会话";
            case "support.local_reject": return "电脑端拒绝支持会话";
            case "support.message.send": return "手机发送支持消息";
            case "support.message.receive": return "接收支持中继消息";
            default: return "受控运维活动";
        }
    }

    private String auditOutcomeLabel(String value) {
        switch (value) {
            case "success":
            case "completed":
            case "accepted":
            case "approved_local":
            case "active":
            case "consumed": return "成功";
            case "rejected":
            case "rejected_local": return "已拒绝";
            case "failed": return "失败";
            case "awaiting_mobile_approval": return "等待手机批准";
            case "executing": return "执行中";
            case "awaiting_local_cosign":
            case "awaiting_local_consent": return "等待电脑确认";
            default: return "已记录";
        }
    }

    private String formatPerformanceSnapshot(JSONObject payload) {
        JSONObject garbageCollection = payload.optJSONObject("garbageCollection");
        JSONObject mainUi = payload.optJSONObject("mainUi");
        StringBuilder text = new StringBuilder();
        text.append("CPU：").append(roundOne(payload.optDouble("cpuPercent", 0))).append("%")
                .append(" · 短采样 ").append(payload.optInt("sampleMilliseconds", 0)).append(" ms")
                .append("\n工作集：").append(roundOne(payload.optDouble("workingSetMb", 0))).append(" MB")
                .append("\n私有内存：").append(roundOne(payload.optDouble("privateMemoryMb", 0))).append(" MB")
                .append("\n托管堆：").append(roundOne(payload.optDouble("managedHeapMb", 0))).append(" MB")
                .append("\n线程 / 句柄：").append(payload.optInt("threadCount", 0))
                .append(" / ").append(payload.optInt("handleCount", 0));
        if (garbageCollection != null) {
            text.append("\nGC 次数（Gen0 / Gen1 / Gen2）：")
                    .append(garbageCollection.optInt("gen0Collections", 0)).append(" / ")
                    .append(garbageCollection.optInt("gen1Collections", 0)).append(" / ")
                    .append(garbageCollection.optInt("gen2Collections", 0));
        }
        if (mainUi == null || !mainUi.optBoolean("available", false)) {
            text.append("\n主界面响应：当前不可探测");
        } else {
            String uiState = mainUi.optString("state", "unavailable");
            text.append("\n主界面响应：").append(uiResponsivenessLabel(uiState));
            if (mainUi.has("latencyMilliseconds") && !mainUi.isNull("latencyMilliseconds")) {
                text.append(" · ").append(mainUi.optLong("latencyMilliseconds", 0)).append(" ms");
            }
        }
        text.append("\n采集时间：").append(shortTime(payload.optString("capturedAt", "")))
                .append("\n\n单次短采样用于远程定位，不代表长期趋势。摘要不含进程标识、名称、路径、命令行、主机名、用户名、网络地址、窗口内容或业务数据。");
        return text.toString();
    }

    private String formatFlowRuntimeStatus(JSONObject payload) {
        if (!payload.optBoolean("available", false)) {
            return "流程运行时尚未就绪。\n\n这通常表示流程界面尚未初始化或电脑主界面暂时无响应；不会因此自动执行或停止任何检测。";
        }

        String phase = payload.optString("phase", "idle");
        StringBuilder text = new StringBuilder();
        text.append("当前阶段：").append(flowPhaseLabel(phase))
                .append("\n流程配置：")
                .append(payload.optBoolean("hasConfiguredFlow", false) ? "电脑端已有可用流程" : "电脑端尚未加载可用流程")
                .append("\n远程取消：")
                .append(payload.optBoolean("cancelAvailable", false)
                        ? "当前主检测可在持续观察页确认取消"
                        : "当前不可用");
        if (payload.optBoolean("progressAvailable", false)
                && payload.has("progressPercent")
                && !payload.isNull("progressPercent")) {
            text.append("\n进度：").append(roundOne(payload.optDouble("progressPercent", 0))).append('%');
            if (payload.optBoolean("progressIsHistoricalEstimate", false)) {
                text.append("（按历史耗时估算）");
            }
        }
        if (payload.has("elapsedMilliseconds") && !payload.isNull("elapsedMilliseconds")) {
            text.append("\n本次已用时：")
                    .append(formatElapsedMilliseconds(payload.optLong("elapsedMilliseconds", 0)));
        }

        String lastStatus = payload.optString("lastRunStatus", "none");
        if (!"none".equals(lastStatus)) {
            text.append("\n最近一次结果：").append(flowOutcomeLabel(lastStatus));
            if (payload.has("lastRunDurationMilliseconds")
                    && !payload.isNull("lastRunDurationMilliseconds")) {
                text.append(" · ")
                        .append(formatElapsedMilliseconds(payload.optLong("lastRunDurationMilliseconds", 0)));
            }
        }
        text.append("\n观测时间：").append(shortTime(payload.optString("observedAt", "")))
                .append("\n\n状态内容只读。进度来自历史耗时估算，长时间不变化不能单独证明流程卡住；可结合进程性能、主界面响应和近期日志判断。取消动作只在持续观察页经明确确认后执行。")
                .append("\n\n摘要不含流程名、模板 ID、批次号、节点名、参数、结果文本或检测数据。");
        return text.toString();
    }

    private String flowPhaseLabel(String value) {
        switch (value) {
            case "preparing": return "正在准备检测";
            case "running": return "检测执行中";
            case "finalizing": return "检测已结束，正在收尾";
            case "idle": return "当前未在检测";
            default: return "暂不可用";
        }
    }

    private String flowOutcomeLabel(String value) {
        switch (value) {
            case "completed": return "已完成";
            case "failed": return "失败";
            case "canceled": return "已取消";
            case "timed_out": return "超时";
            default: return "暂无";
        }
    }

    private String formatElapsedMilliseconds(long milliseconds) {
        long totalSeconds = Math.max(0, milliseconds) / 1000;
        long hours = totalSeconds / 3600;
        long minutes = (totalSeconds % 3600) / 60;
        long seconds = totalSeconds % 60;
        if (hours > 0) {
            return hours + " 小时 " + minutes + " 分 " + seconds + " 秒";
        }
        if (minutes > 0) {
            return minutes + " 分 " + seconds + " 秒";
        }
        return seconds + " 秒";
    }

    private String uiResponsivenessLabel(String value) {
        switch (value) {
            case "responsive": return "正常";
            case "slow": return "偏慢";
            case "unresponsive": return "超时";
            default: return "不可用";
        }
    }

    private double roundOne(double value) {
        return Math.round(Math.max(0, value) * 10.0) / 10.0;
    }

    private String formatServiceHealth(JSONObject payload) {
        if (!payload.optBoolean("available", false)) {
            return "当前无法读取白名单服务状态。\n\n不会仅凭日志建议服务维护；请在电脑端检查 Windows 服务状态。";
        }

        JSONArray services = payload.optJSONArray("services");
        StringBuilder text = new StringBuilder();
        text.append(payload.optBoolean("allHealthy", false) ? "白名单服务均正常" : "有白名单服务需要关注");
        if (services == null || services.length() == 0) {
            text.append("\n\n当前没有适用的本机服务状态。");
        } else {
            for (int index = 0; index < services.length(); index++) {
                JSONObject service = services.optJSONObject(index);
                if (service == null) {
                    continue;
                }
                text.append("\n\n").append(index + 1).append(". ")
                        .append(service.optString("title", "白名单服务"))
                        .append("\n状态：").append(serviceStatusLabel(service.optString("status", "unknown")))
                        .append(service.optBoolean("healthy", false) ? " · 正常" : " · 需关注")
                        .append("\n来源：").append(serviceSourceLabel(service.optString("statusSource", "")));
                String observedAt = shortTime(service.optString("observedAt", ""));
                if (!observedAt.isEmpty()) {
                    text.append("\n观测时间：").append(observedAt);
                }
                if (service.optBoolean("maintenanceSupported", false)) {
                    text.append("\n维护边界：仅固定 Mosquitto 可由已配对手机确认后重启");
                }
            }
        }
        text.append("\n\n").append(payload.optString("privacyNotice",
                "仅报告固定白名单服务的规范化状态；不返回服务账户、程序路径或启动参数。"));
        return text.toString();
    }

    private String formatDeviceHealth(JSONObject payload) {
        if (!payload.optBoolean("available", false)) {
            return "当前无法读取检测设备运行状态汇总。\n\n不会据此执行设备重连或重启；请在电脑端检查设备注册表。";
        }

        int total = payload.optInt("totalCount", 0);
        int attention = payload.optInt("attentionCount", 0);
        int busy = payload.optInt("busyCount", 0);
        int closed = payload.optInt("closedCount", 0);
        StringBuilder text = new StringBuilder();
        if (!payload.optBoolean("hasConfiguredDevices", false)) {
            text.append("当前未发现已加载的检测设备。");
        } else {
            String headline = attention > 0 ? "有检测设备状态需要关注"
                    : busy > 0 ? "有检测设备正在工作"
                    : closed > 0 ? "部分检测设备当前关闭"
                    : "已加载检测设备状态正常";
            text.append(headline).append("\n总数：").append(total).append(" · ");
            appendDeviceStateCounts(text, payload);
            appendDeviceUnavailableReasons(text, payload);
        }

        JSONArray categories = payload.optJSONArray("categories");
        if (categories != null) {
            for (int index = 0; index < categories.length(); index++) {
                JSONObject category = categories.optJSONObject(index);
                if (category == null) {
                    continue;
                }
                text.append("\n\n").append(deviceCategoryLabel(category.optString("category", "other")))
                        .append("（").append(category.optInt("totalCount", 0)).append(" 台）：");
                appendDeviceStateCounts(text, category);
            }
        }
        String observedAt = shortTime(payload.optString("observedAt", ""));
        if (!observedAt.isEmpty()) {
            text.append("\n\n观测时间：").append(observedAt);
        }
        text.append("\n\n状态来自设备实际 MQTT 运行状态的固定归类；不返回设备名称、编号、标识、地址、Topic、配置、原始状态载荷、时间戳或测量数据，也不会执行设备操作。");
        return text.toString();
    }

    private String formatMessageChannelHealth(JSONObject payload, boolean includePrivacyNotice) {
        if (!payload.optBoolean("available", false)) {
            return "当前无法读取 ColorVision 消息通道状态。\n\n不会据此自动重连或重启；请在电脑端复核。";
        }

        String channelState = payload.optString("state", "unavailable");
        int registered = payload.optInt("registeredSubscriptionCount", 0);
        int active = payload.optInt("activeSubscriptionCount", 0);
        StringBuilder text = new StringBuilder();
        text.append(messageChannelStateLabel(channelState))
                .append("\n连接：").append(payload.optBoolean("connected", false) ? "已建立" : "未建立")
                .append("\n订阅：").append(active).append('/').append(registered)
                .append(payload.optBoolean("subscriptionReady", false) ? " · 已就绪" : " · 未就绪");
        appendOptionalActivityTime(text, "最近连接", payload.optString("lastConnectedAt", ""));
        appendOptionalActivityTime(text, "最近断开", payload.optString("lastDisconnectedAt", ""));
        appendOptionalActivityTime(text, "最近接收活动", payload.optString("lastInboundActivityAt", ""));
        appendOptionalActivityTime(text, "最近发送活动", payload.optString("lastOutboundActivityAt", ""));
        String observedAt = shortTime(payload.optString("observedAt", ""));
        if (!observedAt.isEmpty()) {
            text.append("\n观测时间：").append(observedAt);
        }
        if (includePrivacyNotice) {
            text.append("\n\n只显示 ColorVision 客户端的规范化连接状态、订阅计数和聚合活动时间；不返回地址、端口、端点、Topic、消息载荷、客户端或设备标识、配置、凭据、证书或原始日志，也不会执行重连或重启。");
        }
        return text.toString();
    }

    private void appendOptionalActivityTime(StringBuilder text, String label, String value) {
        String timestamp = shortTime(value);
        if (!timestamp.isEmpty()) {
            text.append("\n").append(label).append("：").append(timestamp);
        }
    }

    private String messageChannelStateLabel(String value) {
        switch (value) {
            case "connected": return "已连接 · 订阅就绪";
            case "degraded": return "已连接 · 订阅未完全恢复";
            case "disconnected": return "ColorVision 未连接消息服务";
            case "unconfigured": return "消息通道未配置";
            default: return "状态暂不可用";
        }
    }

    private void appendDeviceStateCounts(StringBuilder text, JSONObject source) {
        text.append("就绪 ").append(source.optInt("readyCount", 0));
        int busy = source.optInt("busyCount", 0);
        int transitioning = source.optInt("transitioningCount", 0);
        int closed = source.optInt("closedCount", 0);
        int unavailable = source.optInt("unavailableCount", 0);
        int unknown = source.optInt("unknownCount", 0);
        if (busy > 0) text.append(" · 忙碌 ").append(busy);
        if (transitioning > 0) text.append(" · 切换中 ").append(transitioning);
        if (closed > 0) text.append(" · 已关闭 ").append(closed);
        if (unavailable > 0) text.append(" · 不可用 ").append(unavailable);
        if (unknown > 0) text.append(" · 未知 ").append(unknown);
    }

    private void appendDeviceUnavailableReasons(StringBuilder text, JSONObject source) {
        appendDeviceUnavailableReasonValues(text,
                source.optInt("offlineCount", 0),
                source.optInt("uninitializedCount", 0),
                source.optInt("unauthorizedCount", 0),
                source.optInt("unclassifiedUnavailableCount", 0));
    }

    private void appendDeviceUnavailableReasonValues(
            StringBuilder text,
            int offline,
            int uninitialized,
            int unauthorized,
            int unclassified) {
        List<String> reasons = new ArrayList<>();
        addDeviceUnavailableReason(reasons, "离线", offline);
        addDeviceUnavailableReason(reasons, "未初始化", uninitialized);
        addDeviceUnavailableReason(reasons, "未授权", unauthorized);
        addDeviceUnavailableReason(reasons, "未归类", unclassified);
        if (!reasons.isEmpty()) {
            text.append("\n不可用原因：").append(TextUtils.join(" · ", reasons));
        }
    }

    private void addDeviceUnavailableReason(List<String> reasons, String label, int count) {
        if (count > 0) {
            reasons.add(label + " " + count);
        }
    }

    private String deviceCategoryLabel(String value) {
        switch (value) {
            case "camera": return "相机类";
            case "algorithm": return "算法类";
            case "spectrum": return "光谱类";
            case "instrument": return "仪表与供电类";
            case "motion": return "运动控制类";
            case "calibration": return "校准类";
            default: return "其他设备";
        }
    }

    private String formatJobs(JSONObject payload) {
        JSONArray jobs = payload.optJSONArray("jobs");
        if (jobs == null || jobs.length() == 0) {
            return "当前没有运维作业。\n\n手机端只显示固定目标、审批时间线与证据类型，不显示内部输入或收据 ID。";
        }

        StringBuilder text = new StringBuilder();
        text.append("安全作业摘要：").append(payload.optInt("count", jobs.length())).append(" 个");
        int maximum = Math.min(jobs.length(), 12);
        for (int index = 0; index < maximum; index++) {
            JSONObject job = jobs.optJSONObject(index);
            if (job == null) {
                continue;
            }
            text.append("\n\n").append(index + 1).append(". ")
                    .append(job.optString("title", "运维作业"))
                    .append("\n目标：").append(job.optString("target", "固定运维能力"))
                    .append("\n状态：").append(jobStatusLabel(job.optString("status", "unknown")))
                    .append(" · 风险：").append(riskLabel(job.optString("riskLevel", "read-only")));
            String createdAt = shortTime(job.optString("createdAt", ""));
            if (!createdAt.isEmpty()) {
                text.append("\n创建：").append(createdAt);
            }

            JSONArray timeline = job.optJSONArray("timeline");
            if (timeline != null && timeline.length() > 0) {
                text.append("\n时间线：");
                for (int timelineIndex = 0; timelineIndex < timeline.length(); timelineIndex++) {
                    JSONObject item = timeline.optJSONObject(timelineIndex);
                    if (item == null) {
                        continue;
                    }
                    text.append("\n  ")
                            .append(timelineStageLabel(item.optString("stage", "")))
                            .append("：")
                            .append(timelineStateLabel(item.optString("state", "")));
                    String at = shortTime(item.optString("at", ""));
                    if (!at.isEmpty()) {
                        text.append(" · ").append(at);
                    }
                }
            }

            JSONObject evidence = job.optJSONObject("evidence");
            if (evidence != null && evidence.optBoolean("available", false)) {
                text.append("\n证据：")
                        .append(evidenceKindLabel(evidence.optString("kind", "bounded-operation-receipt")))
                        .append(" · ")
                        .append("success".equals(evidence.optString("outcome")) ? "成功" : "失败");
            }
        }
        if (jobs.length() > maximum) {
            text.append("\n\n仅显示最近 12 个作业。");
        }
        text.append("\n\n摘要不含申请设备、理由、输入参数或电脑端内部收据 ID。");
        return text.toString();
    }

    private String jobStatusLabel(String value) {
        switch (value) {
            case "awaiting_mobile_approval": return "等待手机审批";
            case "awaiting_local_cosign": return "等待电脑端共签";
            case "approved_local": return "等待执行";
            case "approved_mobile": return "手机已批准，等待执行";
            case "executing": return "正在执行";
            case "completed": return "已完成";
            case "failed": return "执行失败";
            case "rejected": return "手机已拒绝";
            case "rejected_local": return "电脑端已拒绝";
            default: return "未知";
        }
    }

    private String timelineStageLabel(String value) {
        switch (value) {
            case "requested": return "已申请";
            case "mobile_approval": return "手机审批";
            case "local_cosign": return "电脑共签";
            case "execution": return "执行与证据";
            default: return "阶段";
        }
    }

    private String timelineStateLabel(String value) {
        switch (value) {
            case "completed": return "完成";
            case "approved": return "已批准";
            case "pending": return "等待中";
            case "in_progress": return "执行中";
            case "rejected": return "已拒绝";
            case "failed": return "失败";
            case "not_started": return "未开始";
            case "not_required": return "无需此步骤";
            default: return "未知";
        }
    }

    private String riskLabel(String value) {
        if ("privileged".equals(value)) {
            return "特权";
        }
        if ("approval-required".equals(value)) {
            return "需审批";
        }
        if ("low-risk".equals(value)) {
            return "低风险";
        }
        return "只读";
    }

    private String evidenceKindLabel(String value) {
        switch (value) {
            case "service-host-receipt": return "后台服务执行回执";
            case "service-host-error": return "后台服务失败回执";
            case "policy-rejection": return "白名单策略拒绝";
            case "diagnostic-bundle-receipt": return "安全诊断包回执";
            case "window-snapshot-receipt": return "一次性主窗口快照回执";
            case "flow-cancel-request-receipt": return "检测取消请求回执";
            default: return "有界运维回执";
        }
    }

    private String serviceStatusLabel(String value) {
        switch (value) {
            case "running": return "运行中";
            case "stopped": return "已停止";
            case "paused": return "已暂停";
            case "start_pending": return "正在启动";
            case "stop_pending": return "正在停止";
            case "pause_pending": return "正在暂停";
            case "continue_pending": return "正在恢复";
            case "not_installed": return "未安装";
            case "not_applicable": return "使用远程端点，本机不适用";
            default: return "未知";
        }
    }

    private String serviceSourceLabel(String value) {
        if ("windows-service-control-manager".equals(value)) {
            return "Windows 服务控制管理器";
        }
        if ("application-config".equals(value)) {
            return "应用配置";
        }
        return "受限状态提供程序";
    }

    private String formatAlerts(JSONObject payload) {
        JSONArray alerts = payload.optJSONArray("alerts");
        int count = payload.optInt("count", alerts == null ? 0 : alerts.length());
        if (alerts == null || alerts.length() == 0) {
            return "当前没有从近期应用日志中提取到警告、错误或严重事件。\n\n告警摘要不会返回原始日志、文件路径或凭据。";
        }

        StringBuilder text = new StringBuilder();
        text.append("近期告警：").append(count).append(" 条");
        appendEvents(text, alerts, 12);
        if (alerts.length() > 12) {
            text.append("\n\n仅显示最近 12 条；其余事件仍保留在电脑端。 ");
        }
        text.append("\n\n内容已在电脑端脱敏，不包含完整日志或日志文件位置。");
        return text.toString();
    }

    private String formatRecentEvents(JSONObject payload) {
        if (!payload.optBoolean("available", false)) {
            return "当前没有可读取的应用日志摘要。\n\n电脑端不会为此接口创建或搜索其他文件，也不会返回目录信息。";
        }

        StringBuilder text = new StringBuilder();
        text.append("有界日志样本：扫描 ").append(payload.optInt("scannedLineCount", 0))
                .append(" 行，识别 ").append(payload.optInt("parsedEventCount", 0)).append(" 个事件")
                .append("\n级别：信息 ").append(payload.optInt("infoCount", 0))
                .append(" · 警告 ").append(payload.optInt("warningCount", 0))
                .append(" · 错误 ").append(payload.optInt("errorCount", 0))
                .append(" · 严重 ").append(payload.optInt("criticalCount", 0));

        JSONArray categories = payload.optJSONArray("categories");
        if (categories != null && categories.length() > 0) {
            text.append("\n来源：");
            for (int index = 0; index < categories.length(); index++) {
                JSONObject category = categories.optJSONObject(index);
                if (category == null) {
                    continue;
                }
                if (index > 0) {
                    text.append("，");
                }
                text.append(category.optString("category", "应用"))
                        .append(' ').append(category.optInt("count", 0));
            }
        }
        if (payload.optBoolean("tailWasBounded", false)) {
            text.append("\n范围：日志较大，仅分析固定大小的最近尾部");
        } else {
            text.append("\n范围：最近日志尾部（最多 500 行 / 256 KiB）");
        }

        JSONArray events = payload.optJSONArray("recentEvents");
        if (events != null && events.length() > 0) {
            text.append("\n\n近期异常事件");
            appendEvents(text, events, 12);
        } else {
            text.append("\n\n近期没有警告、错误或严重事件。");
        }
        text.append("\n\n").append(payload.optString("privacyNotice",
                "仅返回有界聚合与脱敏事件；不返回完整日志或凭据。"));
        return text.toString();
    }

    private void appendEvents(StringBuilder text, JSONArray events, int maximum) {
        int count = Math.min(events.length(), maximum);
        for (int index = 0; index < count; index++) {
            JSONObject event = events.optJSONObject(index);
            if (event == null) {
                continue;
            }
            text.append("\n\n").append(index + 1).append(". ")
                    .append(severityLabel(event.optString("severity", "warning")))
                    .append(" · ").append(event.optString("source", "应用"));
            String occurredAt = shortTime(event.optString("occurredAt", ""));
            if (!occurredAt.isEmpty()) {
                text.append(" · ").append(occurredAt);
            }
            text.append("\n").append(event.optString("summary", "无摘要"));
        }
    }

    private String severityLabel(String severity) {
        if ("critical".equalsIgnoreCase(severity)) {
            return "严重";
        }
        if ("error".equalsIgnoreCase(severity)) {
            return "错误";
        }
        if ("warning".equalsIgnoreCase(severity)) {
            return "警告";
        }
        return "信息";
    }

    private String shortTime(String value) {
        if (value == null || value.isEmpty() || "null".equalsIgnoreCase(value)) {
            return "";
        }
        if (value.length() >= 19 && value.charAt(10) == 'T') {
            try {
                SimpleDateFormat parser = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.ROOT);
                parser.setLenient(false);
                parser.setTimeZone(TimeZone.getTimeZone("UTC"));
                Date parsed = parser.parse(value.substring(0, 19));
                if (parsed != null) {
                    long utcMilliseconds = parsed.getTime() - resolveOffsetMinutes(value.substring(19)) * 60_000L;
                    SimpleDateFormat display = new SimpleDateFormat("MM-dd HH:mm", Locale.CHINA);
                    return display.format(new Date(utcMilliseconds));
                }
            } catch (Exception ignored) {
            }
            return value.substring(5, 16).replace('T', ' ');
        }
        return value.length() <= 24 ? value : value.substring(0, 24);
    }

    private int resolveOffsetMinutes(String suffix) {
        if (suffix == null || suffix.isEmpty() || suffix.endsWith("Z")) {
            return 0;
        }
        int plus = suffix.lastIndexOf('+');
        int minus = suffix.lastIndexOf('-');
        int marker = Math.max(plus, minus);
        if (marker < 0 || suffix.length() < marker + 6) {
            return 0;
        }
        try {
            int hours = Integer.parseInt(suffix.substring(marker + 1, marker + 3));
            int minutes = Integer.parseInt(suffix.substring(marker + 4, marker + 6));
            int total = hours * 60 + minutes;
            return suffix.charAt(marker) == '-' ? -total : total;
        } catch (NumberFormatException ignored) {
            return 0;
        }
    }

    private String formatDiagnosticSummary(JSONObject payload) {
        long workingSetBytes = payload.optLong("processWorkingSetBytes", 0L);
        StringBuilder text = new StringBuilder();
        text.append("应用：").append(payload.optString("application", "ColorVision"))
                .append(' ').append(payload.optString("applicationVersion", "未知"))
                .append("\n操作系统：").append(payload.optString("os", "未知"))
                .append("\n进程架构：").append(payload.optString("processArchitecture", "未知"))
                .append("\n运行时：").append(payload.optString("runtime", "未知"));
        if (workingSetBytes > 0) {
            text.append("\n进程内存：").append(Math.round(workingSetBytes / 1024d / 1024d * 10) / 10.0).append(" MB");
        }
        text.append("\n生成时间：").append(shortTime(payload.optString("generatedAt", "")))
                .append("\n\n摘要不包含机器名、用户名、设备 ID、证书指纹或网络地址。");
        return text.toString();
    }

    private void loadAndShareSafeDiagnostics() {
        progress.setVisibility(View.VISIBLE);
        state.setText("正在生成安全诊断摘要…");
        executor.execute(() -> {
            try {
                JSONObject connection = client.get("/ops/v1/diagnostics/connection").optJSONObject("data");
                JSONObject events = client.get("/ops/v1/diagnostics/recent-events").optJSONObject("data");
                JSONObject services = client.get("/ops/v1/services/health").optJSONObject("data");
                JSONObject performance = client.get("/ops/v1/diagnostics/performance").optJSONObject("data");
                JSONObject flowRuntime = client.get("/ops/v1/flow/runtime").optJSONObject("data");
                JSONObject devices = client.get("/ops/v1/devices/health").optJSONObject("data");
                JSONObject messageChannel = client.get("/ops/v1/messaging/health").optJSONObject("data");
                if (connection == null || events == null || services == null || performance == null
                        || flowRuntime == null || devices == null || messageChannel == null) {
                    throw new IllegalStateException("incomplete_diagnostic_response");
                }
                String report = buildShareableDiagnostic(
                        connection, events, services, performance, flowRuntime, devices, messageChannel);
                runOnUiThread(() -> {
                    progress.setVisibility(View.GONE);
                    state.setText("安全诊断摘要已生成");
                    details.setText(report);
                    shareSafeText("ColorVision 安全诊断摘要", report);
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private String buildShareableDiagnostic(
            JSONObject connection,
            JSONObject events,
            JSONObject services,
            JSONObject performance,
            JSONObject flowRuntime,
            JSONObject devices,
            JSONObject messageChannel) {
        JSONObject desktop = connection.optJSONObject("desktop");
        String window = desktop == null ? "未知" : desktop.optString("windowState", "未知")
                + (desktop.optBoolean("isVisible", false) ? "（可见）" : "（不可见）");
        return "ColorVision 安全诊断摘要"
                + "\n电脑版本：" + connection.optString("applicationVersion", "未知")
                + "\n运行时：" + connection.optString("runtime", "未知")
                + "\n桌面主窗口：" + window
                + "\n可用能力：" + connection.optInt("availableCapabilityCount", 0)
                + "\n待处理作业：" + connection.optInt("pendingJobCount", 0)
                + "\n\n当前检测状态"
                + "\n" + formatFlowRuntimeStatus(flowRuntime)
                + "\n\n进程性能快照"
                + "\n" + formatPerformanceSnapshot(performance)
                + "\n\n消息通道健康"
                + "\n" + formatMessageChannelHealth(messageChannel, true)
                + "\n\n检测设备状态"
                + "\n" + formatDeviceHealth(devices)
                + "\n\n" + formatServiceHealth(services)
                + "\n\n" + formatRecentEvents(events)
                + "\n\n该文本不包含设备密钥、证书指纹、设备 ID、检测设备身份、用户名、机器名、地址、端口、端点、Topic、消息载荷、配置、凭据、原始设备状态或完整日志。";
    }

    private void shareSafeText(String subject, String report) {
        Intent share = new Intent(Intent.ACTION_SEND);
        share.setType("text/plain");
        share.putExtra(Intent.EXTRA_SUBJECT, subject);
        share.putExtra(Intent.EXTRA_TEXT, report);
        startActivity(Intent.createChooser(share, "分享" + subject));
    }

    private String formatDuration(long seconds) {
        long hours = seconds / 3600;
        long minutes = (seconds % 3600) / 60;
        if (hours > 0) {
            return hours + " 小时 " + minutes + " 分钟";
        }
        return Math.max(1, minutes) + " 分钟";
    }

    private String localizeWindowState(String value) {
        if ("Normal".equalsIgnoreCase(value)) {
            return "正常";
        }
        if ("Minimized".equalsIgnoreCase(value)) {
            return "已最小化";
        }
        if ("Maximized".equalsIgnoreCase(value)) {
            return "已最大化";
        }
        return value;
    }

    private void clearProfile() {
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = false;
        try {
            String hostId = preferences.getOperationsHostId();
            if (!hostId.isEmpty()) {
                new OperationsDeviceIdentity(hostId).delete();
            }
        } catch (Exception ignored) {
        }
        preferences.clearOperationsProfile();
        Toast.makeText(this, "本机配对资料已移除；电脑端仍可单独撤销设备", Toast.LENGTH_LONG).show();
        finish();
    }

    private void setBusy(String message) {
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = false;
        state.setText(message);
        details.setText("设备私钥只保存在 Android Keystore，不会写入二维码、网址或应用配置。 ");
        progress.setVisibility(View.VISIBLE);
        actions.removeAllViews();
    }

    private void showError(String heading, String message, Runnable recovery) {
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = false;
        progress.setVisibility(View.GONE);
        title.setText(heading);
        state.setText(message);
        details.setText("请确认手机与电脑位于同一可信局域网，并重新扫描电脑端短时配对码。\n不会回退到 URL token。 ");
        actions.removeAllViews();
        if (recovery != null) {
            Button button = new Button(this);
            button.setText("移除失效配对资料");
            button.setOnClickListener(v -> recovery.run());
            actions.addView(button, actionParams());
        }
    }

    private LinearLayout.LayoutParams actionParams() {
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(50));
        params.setMargins(0, 0, 0, dp(10));
        return params;
    }

    private String readableError(Exception ex) {
        String message = ex.getMessage();
        if (ex instanceof SocketTimeoutException || (message != null && message.contains("after 7000ms"))) {
            return "连接电脑超时。配对资料已保留，请运行连接自检。";
        }
        if (ex instanceof ConnectException || (message != null && message.contains("failed to connect"))) {
            return "电脑端安全通道当前不可达。配对资料已保留，请运行连接自检。";
        }
        if (ex instanceof UnknownHostException) {
            return "无法解析电脑地址，请检查当前网络或重新获取配对地址。";
        }
        if (ex instanceof SSLHandshakeException) {
            return "TLS 安全握手失败，已阻止连接。";
        }
        if (message == null || message.trim().isEmpty()) {
            return ex.getClass().getSimpleName();
        }
        if (message.contains("Certificate pin mismatch")) {
            return "服务器证书与二维码指纹不一致，已阻止连接。";
        }
        if (message.contains("unknown_or_revoked_device")) {
            return "设备已被电脑端撤销，请重新配对。";
        }
        if (message.contains("application_restart_flow_active")) {
            return "当前检测仍在执行，为避免中断检测，电脑端已拒绝重启。";
        }
        if (message.contains("application_restart_flow_status_unavailable")) {
            return "暂时无法确认检测是否正在执行，已阻止重启。";
        }
        if (message.contains("application_restart_not_scheduled")
                || message.contains("application_restart_failed")) {
            return "电脑端未能完成 ColorVision 重启，请查看作业时间线。";
        }
        if (message.contains("application_restart_reconnect_timeout")) {
            return "90 秒内未确认 ColorVision 恢复；配对资料已保留。";
        }
        if (message.contains("application_restart_job_missing")) {
            return "未找到本次 ColorVision 重启作业回执。";
        }
        if (message.contains("window_snapshot_expired")) {
            return "主窗口快照的 5 分钟读取窗口已结束，请重新采集。";
        }
        if (message.contains("window_snapshot_not_completed")) {
            return "主窗口快照采集未完成。请确保电脑主窗口已显示且未最小化，然后重试。";
        }
        if (message.contains("window_snapshot_not_ready")) {
            return "主窗口快照尚未完成采集。";
        }
        if (message.contains("window_snapshot_not_found")) {
            return "一次性主窗口快照已读取销毁、已失效，或不属于当前设备。";
        }
        if (message.contains("window_snapshot_read_failed")) {
            return "电脑端暂时无法读取主窗口快照，请重新申请。";
        }
        if (message.contains("window_snapshot_hash_mismatch")) {
            return "主窗口快照完整性校验失败，已阻止预览。";
        }
        if (message.contains("window_snapshot_size_rejected")
                || message.contains("window_snapshot_too_large")) {
            return "主窗口快照超出 1.5 MiB 安全上限，已阻止下载。";
        }
        if (message.contains("window_snapshot_type_rejected")
                || message.contains("window_snapshot_format_rejected")
                || message.contains("window_snapshot_dimensions_rejected")) {
            return "主窗口快照格式或尺寸不符合安全约束，已阻止预览。";
        }
        if (message.contains("diagnostic_bundle_expired")) {
            return "诊断包的 24 小时下载窗口已结束，请重新生成。";
        }
        if (message.contains("diagnostic_bundle_not_completed")) {
            return "脱敏诊断包生成未完成，请稍后重试并查看作业结果。";
        }
        if (message.contains("diagnostic_bundle_not_ready")) {
            return "诊断包尚未完成生成。";
        }
        if (message.contains("diagnostic_bundle_not_found")) {
            return "当前设备无权读取该诊断包，或文件已经不可用。";
        }
        if (message.contains("diagnostic_bundle_regeneration_required")) {
            return "旧版诊断包不符合当前脱敏规则，请重新生成。";
        }
        if (message.contains("diagnostic_bundle_read_failed")) {
            return "电脑端暂时无法读取诊断包，请稍后重试。";
        }
        if (message.contains("diagnostic_bundle_hash_mismatch")) {
            return "诊断包完整性校验失败，已阻止分享。";
        }
        if (message.contains("diagnostic_bundle_size_rejected")
                || message.contains("diagnostic_bundle_too_large")) {
            return "诊断包超出移动端 2 MiB 安全上限，已阻止下载。";
        }
        if (message.matches("[a-zA-Z0-9_.-]{1,64}")) {
            return message;
        }
        return "连接失败：" + ex.getClass().getSimpleName();
    }

    private String pretty(JSONObject value) {
        try {
            return value.toString(2);
        } catch (Exception ignored) {
            return value.toString();
        }
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    private int getStatusBarHeight() {
        int resourceId = getResources().getIdentifier("status_bar_height", "dimen", "android");
        return resourceId > 0 ? getResources().getDimensionPixelSize(resourceId) : dp(24);
    }

    @Override
    protected void onPause() {
        activityResumed = false;
        connectionHeartbeatHandler.removeCallbacks(connectionHeartbeat);
        supportRefreshHandler.removeCallbacks(supportRefresh);
        liveMonitorRefreshHandler.removeCallbacks(liveMonitorRefresh);
        super.onPause();
    }

    @Override
    protected void onResume() {
        super.onResume();
        activityResumed = true;
        if (supportCenterVisible) {
            scheduleSupportRefresh();
        }
        if (liveMonitorVisible && liveMonitorAutoRefresh) {
            liveMonitorRefreshHandler.post(liveMonitorRefresh);
        }
        scheduleConnectionHeartbeat();
    }

    @Override
    protected void onDestroy() {
        connectionHeartbeatHandler.removeCallbacks(connectionHeartbeat);
        leaveSupportCenter();
        leaveLiveMonitor();
        executor.shutdownNow();
        super.onDestroy();
    }
}

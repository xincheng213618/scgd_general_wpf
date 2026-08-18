package com.colorvision.xcviewer;

import android.Manifest;
import android.content.ClipData;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.content.res.ColorStateList;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Color;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.text.TextUtils;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import androidx.activity.OnBackPressedCallback;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.content.FileProvider;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.widget.TextViewCompat;
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout;

import com.google.android.material.bottomnavigation.BottomNavigationView;
import com.google.android.material.button.MaterialButton;
import com.google.android.material.button.MaterialButtonToggleGroup;
import com.google.android.material.card.MaterialCardView;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.progressindicator.LinearProgressIndicator;
import com.google.android.material.snackbar.Snackbar;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.File;
import java.io.FileOutputStream;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Date;
import java.util.List;
import java.util.Locale;
import java.util.TimeZone;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicInteger;

public class OperationsActivity extends AppCompatActivity {
    public static final String EXTRA_PAIRING_PAYLOAD = "operations_pairing_payload";
    static final String EXTRA_OPEN_DESTINATION = "operations_open_destination";
    private static final String STATE_DESTINATION = "operations_destination";
    private static final int REQUEST_QR_SCAN = 2406;
    private static final long LIVE_MONITOR_REFRESH_MILLISECONDS = 10_000L;
    private static final long CONNECTION_HEARTBEAT_MILLISECONDS = 30_000L;
    private static final int FLEET_CONNECT_TIMEOUT_MILLISECONDS = 3_500;
    private static final int FLEET_READ_TIMEOUT_MILLISECONDS = 5_000;
    private static final int NAV_OPERATIONS = 2001;
    private static final int NAV_SETTINGS = 2002;

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
    private final ExecutorService fleetExecutor = Executors.newFixedThreadPool(3);
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
    private final Handler pairingApprovalHandler = new Handler(Looper.getMainLooper());
    private final Runnable pairingApprovalTick = this::refreshPairingApprovalCountdown;
    private AppPreferences preferences;
    private ThemeManager themeManager;
    private OperationsApiClient client;
    private OperationsRelayApiClient relayClient;
    private String operationsClientHostId = "";
    private JSONObject lastRelaySnapshotResponse;
    private TextView title;
    private TextView profileTarget;
    private TextView state;
    private TextView details;
    private LinearProgressIndicator progress;
    private SwipeRefreshLayout dashboardRefresh;
    private ScrollView dashboardScroll;
    private LinearLayout actions;
    private DashboardStatusRow dashboardFlowStatus;
    private DashboardStatusRow dashboardDeviceStatus;
    private DashboardStatusRow dashboardMessageStatus;
    private DashboardStatusRow dashboardAlertStatus;
    private DashboardStatusRow dashboardPerformanceStatus;
    private DashboardStatusRow dashboardRecoveryStatus;
    private Button dashboardPriorityAction;
    private Button dashboardCancelFlowButton;
    private Button dashboardRestartApplicationButton;
    private Button remoteRestartMqttButton;
    private TextView dashboardStatusHeading;
    private TextView dashboardStatusCaption;
    private boolean dashboardFlowAvailable;
    private boolean dashboardFlowActive;
    private boolean dashboardFlowCancelAvailable;
    private boolean dashboardFlowCancelCapabilityAvailable;
    private boolean dashboardRestartCapabilityAvailable;
    private boolean dashboardRemoteHostFresh;
    private boolean dashboardVisible;
    private volatile boolean remoteDashboard;
    private boolean showingDashboardSummary;
    private boolean connectionRecoveryVisible;
    private boolean connectionHeartbeatInFlight;
    private boolean manualDashboardRefresh;
    private int connectionRequestGeneration;
    private int connectionCheckGeneration;
    private int remoteTaskGeneration;
    private int fleetCheckGeneration;
    private BottomNavigationView bottomNavigation;
    private boolean fleetCheckInFlight;
    private String pendingOperationsDestination = "";
    private String currentDestination = OperationsDestinationState.OVERVIEW;
    private String pendingRestoredDestination = "";
    private boolean pairingApprovalWaiting;
    private long pairingApprovalDeadlineMilliseconds;
    private volatile int pairingRequestGeneration;

    private static final class DashboardStatusRow {
        final LinearLayout container;
        final TextView title;
        final TextView summary;

        DashboardStatusRow(LinearLayout container, TextView title, TextView summary) {
            this.container = container;
            this.title = title;
            this.summary = summary;
        }
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        WindowCompat.setDecorFitsSystemWindows(getWindow(), false);
        preferences = new AppPreferences(this);
        themeManager = new ThemeManager(this, preferences);
        themeManager.applySystemBars(this);
        acceptOperationsDestination(getIntent());
        boolean restoring = savedInstanceState != null;
        String restoredDestination = OperationsDestinationState.normalize(
                restoring ? savedInstanceState.getString(STATE_DESTINATION) : null);
        currentDestination = restoredDestination;
        if (OperationsDestinationState.shouldRestore(restoredDestination)) {
            pendingRestoredDestination = restoredDestination;
        }
        createView();
        installInPageBackNavigation();

        String rawPairing = getIntent().getStringExtra(EXTRA_PAIRING_PAYLOAD);
        boolean hasPairingPayload = rawPairing != null && !rawPairing.isEmpty();
        if (OperationsDestinationState.shouldSubmitPairingAutomatically(
                restoring, hasPairingPayload)) {
            beginPairing(rawPairing);
        } else if (hasPairingPayload
                && OperationsDestinationState.PAIRING.equals(restoredDestination)) {
            showInterruptedPairing(rawPairing);
        } else if (preferences.hasOperationsProfile()) {
            openExistingProfile();
        } else if (hasPairingPayload) {
            showInterruptedPairing(rawPairing);
        } else {
            showError("尚未安全配对", "请返回并扫描电脑端的现场运维配对码。", null);
        }
    }

    private void createView() {
        LinearLayout shell = new LinearLayout(this);
        shell.setOrientation(LinearLayout.VERTICAL);
        shell.setBackgroundColor(themeManager.pageBackgroundColor());

        ScrollView scroll = new ScrollView(this);
        dashboardScroll = scroll;
        scroll.setFillViewport(true);
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(dp(16), dp(8), dp(16), dp(24));
        root.setBackgroundColor(themeManager.pageBackgroundColor());
        scroll.addView(root, new ScrollView.LayoutParams(
                ScrollView.LayoutParams.MATCH_PARENT, ScrollView.LayoutParams.WRAP_CONTENT));

        LinearLayout header = new LinearLayout(this);
        boolean singleColumn = AppResponsiveLayout.usesSingleColumn(
                getResources().getConfiguration().fontScale);
        header.setOrientation(singleColumn ? LinearLayout.VERTICAL : LinearLayout.HORIZONTAL);
        header.setGravity(singleColumn ? Gravity.START : Gravity.CENTER_VERTICAL);
        header.setMinimumHeight(dp(64));
        root.addView(header, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));

        title = new TextView(this);
        title.setText("运维伴侣");
        TextViewCompat.setTextAppearance(title, com.google.android.material.R.style.TextAppearance_Material3_TitleLarge);
        title.setTextColor(themeManager.primaryTextColor());
        title.setMaxLines(2);
        title.setEllipsize(TextUtils.TruncateAt.END);
        LinearLayout.LayoutParams titleParams = singleColumn
                ? new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MATCH_PARENT,
                        LinearLayout.LayoutParams.WRAP_CONTENT)
                : new LinearLayout.LayoutParams(
                        0, LinearLayout.LayoutParams.WRAP_CONTENT, 1);
        titleParams.setMargins(0, 0, singleColumn ? 0 : dp(8), singleColumn ? dp(4) : 0);
        header.addView(title, titleParams);

        profileTarget = new MaterialButton(this, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        profileTarget.setTextSize(12);
        profileTarget.setSingleLine(true);
        profileTarget.setEllipsize(TextUtils.TruncateAt.END);
        profileTarget.setGravity(Gravity.CENTER);
        profileTarget.setPadding(dp(10), dp(6), dp(10), dp(6));
        profileTarget.setMinHeight(dp(48));
        profileTarget.setMaxWidth(singleColumn ? Integer.MAX_VALUE : dp(180));
        profileTarget.setOnClickListener(v -> showConnectionPreference());
        header.addView(profileTarget, new LinearLayout.LayoutParams(
                singleColumn
                        ? LinearLayout.LayoutParams.MATCH_PARENT
                        : LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));

        state = new TextView(this);
        TextViewCompat.setTextAppearance(state, com.google.android.material.R.style.TextAppearance_Material3_BodyMedium);
        state.setTextColor(themeManager.onPrimaryContainerColor());
        state.setPadding(dp(16), dp(12), dp(16), dp(12));
        MaterialCardView stateCard = new MaterialCardView(this);
        stateCard.setCardBackgroundColor(themeManager.primaryContainerColor());
        stateCard.addView(state, new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT));
        LinearLayout.LayoutParams stateParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        stateParams.setMargins(0, dp(8), 0, 0);
        root.addView(stateCard, stateParams);

        progress = new LinearProgressIndicator(this);
        progress.setIndeterminate(true);
        LinearLayout.LayoutParams progressParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(4));
        progressParams.setMargins(0, dp(4), 0, 0);
        root.addView(progress, progressParams);

        details = new TextView(this);
        TextViewCompat.setTextAppearance(details, com.google.android.material.R.style.TextAppearance_Material3_BodyMedium);
        details.setTextColor(themeManager.primaryTextColor());
        details.setLineSpacing(0, 1.08f);
        details.setPadding(dp(16), dp(12), dp(16), dp(12));
        details.setTextIsSelectable(true);
        MaterialCardView detailsCard = new MaterialCardView(this);
        detailsCard.addView(details, new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT));
        LinearLayout.LayoutParams detailsParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        detailsParams.setMargins(0, dp(8), 0, 0);
        root.addView(detailsCard, detailsParams);

        actions = new LinearLayout(this);
        actions.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams actionsParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        actionsParams.setMargins(0, dp(7), 0, 0);
        root.addView(actions, actionsParams);

        dashboardRefresh = new SwipeRefreshLayout(this);
        dashboardRefresh.setColorSchemeColors(themeManager.primaryColor());
        dashboardRefresh.setProgressBackgroundColorSchemeColor(themeManager.cardBackgroundColor());
        dashboardRefresh.setOnRefreshListener(() -> requestDashboardRefresh());
        dashboardRefresh.setOnChildScrollUpCallback((parent, child) ->
                !showingDashboardSummary || dashboardScroll.canScrollVertically(-1));
        ViewCompat.addAccessibilityAction(scroll, "刷新运维状态", (view, arguments) ->
                requestDashboardRefresh());
        dashboardRefresh.addView(scroll, new SwipeRefreshLayout.LayoutParams(
                SwipeRefreshLayout.LayoutParams.MATCH_PARENT,
                SwipeRefreshLayout.LayoutParams.MATCH_PARENT));
        shell.addView(dashboardRefresh, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));
        bottomNavigation = createBottomNavigation();
        shell.addView(bottomNavigation, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));
        applyTopSystemBarInset(shell);
        setContentView(shell);
        ViewCompat.requestApplyInsets(shell);
        refreshOperationsTargetPresentation();
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

    private BottomNavigationView createBottomNavigation() {
        BottomNavigationView navigation = new BottomNavigationView(this);
        navigation.setBackgroundColor(themeManager.bottomNavBackgroundColor());
        navigation.setLabelVisibilityMode(BottomNavigationView.LABEL_VISIBILITY_LABELED);
        navigation.getMenu().add(0, NAV_OPERATIONS, 0, "运维").setIcon(R.drawable.ic_devices_24);
        navigation.getMenu().add(0, NAV_SETTINGS, 1, "设置").setIcon(R.drawable.ic_settings_24);
        navigation.setOnItemSelectedListener(item -> {
            if (item.getItemId() == NAV_OPERATIONS) {
                return true;
            }
            if (item.getItemId() == NAV_SETTINGS) {
                openMainTab(MainActivity.TAB_SETTINGS);
                return true;
            }
            return false;
        });
        navigation.setOnItemReselectedListener(item -> {
            if (item.getItemId() == NAV_OPERATIONS) {
                returnToOperationsOverview();
            }
        });
        navigation.setSelectedItemId(NAV_OPERATIONS);
        return navigation;
    }

    private void installInPageBackNavigation() {
        getOnBackPressedDispatcher().addCallback(this, new OnBackPressedCallback(true) {
            @Override
            public void handleOnBackPressed() {
                if (returnToOperationsOverview()) {
                    return;
                }
                setEnabled(false);
                getOnBackPressedDispatcher().onBackPressed();
            }
        });
    }

    private boolean returnToOperationsOverview() {
        if (!OperationsInPageNavigationPolicy.shouldReturnToOverview(
                preferences != null && preferences.hasOperationsProfile(),
                dashboardVisible,
                showingDashboardSummary,
                connectionRecoveryVisible)) {
            return false;
        }
        connectionCheckGeneration++;
        showCurrentDashboard();
        return true;
    }

    private void openMainTab(int tab) {
        connectionCheckGeneration++;
        Intent intent = new Intent(this, MainActivity.class);
        intent.putExtra(MainActivity.EXTRA_START_TAB, tab);
        intent.putExtra(MainActivity.EXTRA_FROM_OPERATIONS, true);
        intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        AppScreenMotion.startForward(this, intent);
    }

    private void beginPairing(String rawPairing) {
        currentDestination = OperationsDestinationState.PAIRING;
        pendingRestoredDestination = "";
        int claimGeneration = ++pairingRequestGeneration;
        setBusy("第 1 步（共 2 步） · 正在验证配对码并创建设备身份…");
        title.setText("安全配对");
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
                    if (claimGeneration == pairingRequestGeneration && !isFinishing()) {
                        startPairingApprovalChecks(payload, pairingClient, deviceName.trim());
                    }
                });
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    if (claimGeneration == pairingRequestGeneration && !isFinishing()) {
                        showPairingFailure(PairingFailurePresentation.reasonFor(ex));
                    }
                });
            }
        });
    }

    private void showInterruptedPairing(String rawPairing) {
        currentDestination = OperationsDestinationState.PAIRING;
        pendingRestoredDestination = "";
        pairingApprovalWaiting = false;
        pairingApprovalHandler.removeCallbacks(pairingApprovalTick);
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = false;
        showingDashboardSummary = false;
        connectionRecoveryVisible = false;
        progress.setVisibility(View.GONE);
        title.setText("安全配对");
        state.setText("页面已由系统重新创建");
        details.setText("为避免重复提交同一份配对申请，应用没有自动重放扫码结果。你可以明确继续验证；若配对码已经超过两分钟，请返回电脑端刷新二维码。");
        actions.removeAllViews();
        addDashboardSection("下一步");
        addDashboardWideAction(dashboardPrimaryButton(
                "继续验证配对码", v -> beginPairing(rawPairing)));
        addDashboardWideAction(dashboardButton(
                preferences.hasOperationsProfile() ? "返回当前电脑" : "返回设置",
                v -> {
                    if (preferences.hasOperationsProfile()) {
                        openExistingProfile();
                    } else {
                        openMainTab(MainActivity.TAB_SETTINGS);
                    }
                }));
    }

    private void startPairingApprovalChecks(
            OperationsPairingPayload payload,
            OperationsApiClient pairingClient,
            String deviceName) {
        int requestGeneration = ++pairingRequestGeneration;
        long deadlineMilliseconds = PairingApprovalWaitPolicy.deadlineFrom(
                SystemClock.elapsedRealtime());
        showPairingApprovalWait(deviceName, deadlineMilliseconds);
        executor.execute(() -> {
            try {
                pollPairingApproval(
                        payload, pairingClient, deviceName, deadlineMilliseconds, requestGeneration);
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    if (requestGeneration == pairingRequestGeneration && !isFinishing()) {
                        showPairingFailure(PairingFailurePresentation.reasonFor(ex));
                    }
                });
            }
        });
    }

    private void pollPairingApproval(
            OperationsPairingPayload payload,
            OperationsApiClient pairingClient,
            String deviceName,
            long deadlineMilliseconds,
            int requestGeneration) throws Exception {
        boolean checkedAtLeastOnce = false;
        while (requestGeneration == pairingRequestGeneration && !isFinishing() && !isDestroyed()) {
            long nowMilliseconds = SystemClock.elapsedRealtime();
            if (checkedAtLeastOnce && !PairingApprovalWaitPolicy.shouldContinue(
                    deadlineMilliseconds, nowMilliseconds)) {
                break;
            }
            JSONObject response = pairingClient.pairingStatus(payload.pairingId);
            if (requestGeneration != pairingRequestGeneration || isFinishing() || isDestroyed()) {
                return;
            }
            checkedAtLeastOnce = true;
            JSONObject data = response.optJSONObject("data");
            String status = data == null ? "" : data.optString("status", "");
            if ("approved".equals(status)) {
                if (!preferences.saveOperationsProfile(
                        payload.endpoint, payload.certificateSha256, payload.hostId)) {
                    try {
                        new OperationsDeviceIdentity(payload.hostId).delete();
                    } catch (Exception ignored) {
                    }
                    runOnUiThread(() -> {
                        if (requestGeneration == pairingRequestGeneration) {
                            showError(
                                    "已配对电脑数量已满",
                                    "手机最多保留 " + OperationsProfileRegistry.MAX_PROFILES
                                            + " 台电脑。请先在连接方式中移除不再使用的电脑。",
                                    null);
                        }
                    });
                    return;
                }
                OperationsWatchService.restartForProfileChange(this);
                client = pairingClient;
                relayClient = new OperationsRelayApiClient(
                        payload.hostId,
                        preferences.getOrCreateDeviceId(),
                        payload.certificateSha256,
                        new OperationsDeviceIdentity(payload.hostId));
                operationsClientHostId = payload.hostId;
                runOnUiThread(() -> {
                    if (requestGeneration == pairingRequestGeneration) {
                        stopPairingApprovalWait();
                        showDashboard();
                    }
                });
                return;
            }
            if ("rejected".equals(status)) {
                runOnUiThread(() -> {
                    if (requestGeneration == pairingRequestGeneration) {
                        showPairingFailure(PairingFailurePresentation.APPROVAL_REJECTED);
                    }
                });
                return;
            }
            long remainingMilliseconds = PairingApprovalWaitPolicy.remainingMilliseconds(
                    deadlineMilliseconds, SystemClock.elapsedRealtime());
            if (remainingMilliseconds <= 0L) {
                break;
            }
            Thread.sleep(Math.min(
                    PairingApprovalWaitPolicy.POLL_INTERVAL_MILLISECONDS,
                    remainingMilliseconds));
        }
        if (requestGeneration == pairingRequestGeneration && !isFinishing() && !isDestroyed()) {
            runOnUiThread(() -> {
                if (requestGeneration == pairingRequestGeneration) {
                    showPairingTimeout(payload, pairingClient, deviceName);
                }
            });
        }
    }

    private void showPairingApprovalWait(String deviceName, long deadlineMilliseconds) {
        stopPairingApprovalWait();
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = false;
        pairingApprovalWaiting = true;
        pairingApprovalDeadlineMilliseconds = deadlineMilliseconds;
        title.setText("批准这台手机");
        details.setText(PairingApprovalPresentation.waitingDetails(deviceName));
        actions.removeAllViews();
        progress.setVisibility(View.GONE);
        progress.setIndeterminate(false);
        progress.setMax(PairingApprovalWaitPolicy.PROGRESS_MAXIMUM);
        progress.setProgress(0);
        progress.setVisibility(View.VISIBLE);
        refreshPairingApprovalCountdown();
    }

    private void refreshPairingApprovalCountdown() {
        pairingApprovalHandler.removeCallbacks(pairingApprovalTick);
        if (!pairingApprovalWaiting) {
            return;
        }
        long nowMilliseconds = SystemClock.elapsedRealtime();
        int remainingSeconds = PairingApprovalWaitPolicy.remainingSeconds(
                pairingApprovalDeadlineMilliseconds, nowMilliseconds);
        state.setText(PairingApprovalPresentation.waitingState(remainingSeconds));
        progress.setProgress(PairingApprovalWaitPolicy.elapsedProgress(
                pairingApprovalDeadlineMilliseconds, nowMilliseconds));
        if (activityResumed && remainingSeconds > 0) {
            pairingApprovalHandler.postDelayed(pairingApprovalTick, 1_000L);
        }
    }

    private void stopPairingApprovalWait() {
        pairingApprovalWaiting = false;
        pairingApprovalHandler.removeCallbacks(pairingApprovalTick);
        if (progress != null) {
            progress.setVisibility(View.GONE);
            progress.setIndeterminate(true);
            progress.setProgress(0);
        }
    }

    private void showPairingTimeout(
            OperationsPairingPayload payload,
            OperationsApiClient pairingClient,
            String deviceName) {
        stopPairingApprovalWait();
        title.setText("电脑端尚未确认");
        state.setText("已暂停自动检查，待批准记录仍保留");
        details.setText(PairingApprovalPresentation.timeoutDetails(deviceName));
        actions.removeAllViews();

        Button retry = new MaterialButton(this);
        retry.setText("继续自动检查");
        retry.setOnClickListener(v -> startPairingApprovalChecks(payload, pairingClient, deviceName));
        actions.addView(retry, actionParams());

        boolean hasExistingProfile = preferences.hasOperationsProfile();
        Button secondary = new MaterialButton(
                this, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        secondary.setText(PairingFailurePresentation.secondaryAction(hasExistingProfile));
        secondary.setOnClickListener(v -> {
            if (hasExistingProfile) {
                openExistingProfile();
            } else {
                openMainTab(MainActivity.TAB_SETTINGS);
            }
        });
        actions.addView(secondary, actionParams());
    }

    private void openExistingProfile() {
        connectionCheckGeneration++;
        scrollDashboardToTop();
        refreshOperationsTargetPresentation();
        String requestHostId = preferences.getOperationsHostId();
        int requestGeneration = ++connectionRequestGeneration;
        setBusy("正在连接已配对的 ColorVision 主机…");
        executor.execute(() -> {
            Exception localFailure = null;
            Exception relayFailure = null;
            JSONObject relayResponse = null;
            boolean localConnected = false;
            try {
                ensureOperationsClients();
            } catch (Exception ex) {
                localFailure = ex;
                relayFailure = ex;
            }

            boolean relayFirst = OperationsConnectionPreference.prefersRelay(
                    preferences.getOperationsConnectionPreference());
            if (localFailure == null && relayFirst) {
                try {
                    relayResponse = relayClient.getSnapshot();
                } catch (Exception ex) {
                    relayFailure = ex;
                    if (!canFallbackAfter(ex)) {
                        localFailure = ex;
                    } else {
                        try {
                            client.get("/ops/v1/snapshot");
                            localConnected = true;
                        } catch (Exception localException) {
                            localFailure = localException;
                        }
                    }
                }
            } else if (localFailure == null) {
                try {
                    client.get("/ops/v1/snapshot");
                    localConnected = true;
                } catch (Exception ex) {
                    localFailure = ex;
                    if (!canFallbackAfter(ex)) {
                        relayFailure = ex;
                    } else {
                        try {
                            relayResponse = relayClient.getSnapshot();
                        } catch (Exception relayException) {
                            relayFailure = relayException;
                        }
                    }
                }
            }

            JSONObject finalRelayResponse = relayResponse;
            boolean finalLocalConnected = localConnected;
            Exception finalLocalFailure = localFailure;
            Exception finalRelayFailure = relayFailure;
            runOnUiThread(() -> {
                if (requestGeneration != connectionRequestGeneration
                        || isFinishing() || isDestroyed()) {
                    return;
                }
                if (!OperationsTargetPolicy.isSameTarget(
                        requestHostId, preferences.getOperationsHostId())) {
                    reconnectAfterOperationsTargetChange();
                    return;
                }
                connectionHeartbeatInFlight = false;
                if (isRevokedException(finalLocalFailure)
                        || isRevokedException(finalRelayFailure)) {
                    showRevokedProfile();
                } else if (finalRelayResponse != null) {
                    showRemoteDashboard(finalRelayResponse);
                } else if (finalLocalConnected) {
                    showDashboard();
                } else {
                    showExistingProfileFailure(finalLocalFailure, finalRelayFailure);
                }
            });
        });
    }

    private void ensureOperationsClients() throws Exception {
        String hostId = preferences.getOperationsHostId();
        if (!hostId.equals(operationsClientHostId)) {
            client = null;
            relayClient = null;
            lastRelaySnapshotResponse = null;
            operationsClientHostId = hostId;
        }
        OperationsDeviceIdentity identity = new OperationsDeviceIdentity(hostId);
        if (relayClient == null) {
            relayClient = new OperationsRelayApiClient(
                    hostId,
                    preferences.getOrCreateDeviceId(),
                    preferences.getOperationsCertificatePin(),
                    identity);
        }
        if (client == null) {
            client = new OperationsApiClient(
                    preferences.getOperationsEndpoint(),
                    preferences.getOperationsCertificatePin(),
                    preferences.getOrCreateDeviceId(),
                    identity);
        }
    }

    private void selectConnectionPreference(String connectionPreference) {
        String normalized = OperationsConnectionPreference.normalize(connectionPreference);
        preferences.saveOperationsConnectionPreference(normalized);
        OperationsWatchService.refreshConnectionPreference(this);
        Toast.makeText(this,
                OperationsConnectionPreference.prefersRelay(normalized)
                        ? "已设为固定中继优先"
                        : "已设为现场直连优先",
                Toast.LENGTH_SHORT).show();
        openExistingProfile();
    }

    private void showConnectionPreference() {
        currentDestination = OperationsDestinationState.CONNECTIONS;
        connectionCheckGeneration++;
        scrollDashboardToTop();
        refreshOperationsTargetPresentation();
        showingDashboardSummary = false;
        connectionRecoveryVisible = false;
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = true;
        progress.setVisibility(View.GONE);
        title.setText("电脑与连接");
        boolean relayPreferred = OperationsConnectionPreference.prefersRelay(
                preferences.getOperationsConnectionPreference());
        List<OperationsProfileRegistry.Profile> profiles = preferences.getOperationsProfiles();
        long nowMilliseconds = System.currentTimeMillis();
        OperationsFleetOverview.Assessment fleet = OperationsFleetOverview.assess(
                profiles, nowMilliseconds);
        int profileCount = profiles.size();
        OperationsProfileRegistry.Profile activeProfile = null;
        for (OperationsProfileRegistry.Profile profile : profiles) {
            if (profile.hostId.equals(preferences.getOperationsHostId())) {
                activeProfile = profile;
                break;
            }
        }
        String activeProfileState = activeProfile == null
                ? "尚未检查"
                : OperationsProfileOverview.summary(
                activeProfile.watchHistory,
                activeProfile.watchCheckedAt,
                activeProfile.revoked,
                nowMilliseconds);
        state.setText(OperationsConnectionOverview.pageStatus(
                profileCount, activeProfileState, fleet.summary));
        details.setText(OperationsConnectionOverview.summary(
                relayPreferred ? "固定中继" : "现场直连",
                remoteDashboard ? "固定中继" : "现场直连",
                profileCount,
                OperationsProfileRegistry.MAX_PROFILES));
        actions.removeAllViews();

        if (OperationsConnectionOverview.showsFleetTools(profileCount)) {
            addDashboardSection("电脑");
            if (fleet.hasPriorityAction()) {
                addDashboardWideAction(dashboardButton(
                        fleet.priorityButtonLabel, v -> openFleetPriority(fleet)));
            }
            for (OperationsProfileRegistry.Profile profile : profiles) {
                addOperationsProfileWideAction(operationsProfileButton(
                        profile, profiles, nowMilliseconds));
            }
            addDashboardWideAction(dashboardButton(
                    "查看全部电脑动态", v -> showFleetTimeline(false)));
        }

        addDashboardSection("连接偏好");
        addDashboardSegmentedChoices(
                "现场直连优先",
                "固定中继优先",
                relayPreferred,
                v -> selectConnectionPreference(OperationsConnectionPreference.DIRECT),
                v -> selectConnectionPreference(OperationsConnectionPreference.RELAY));

        addDashboardSection(OperationsConnectionOverview.showsFleetTools(profileCount)
                ? "检查与巡检" : "检查连接");
        if (OperationsConnectionOverview.showsFleetTools(profileCount)) {
            addDashboardActionRow(
                    dashboardButton("运行现场连接自检", v -> runConnectionSelfCheck()),
                    dashboardButton("只读巡检全部电脑", v -> refreshAllOperationsProfiles()));
        } else {
            addDashboardWideAction(dashboardButton(
                    "运行现场连接自检", v -> runConnectionSelfCheck()));
        }

        addDashboardSection("电脑管理");
        addDashboardActionRow(
                dashboardButton("命名当前电脑", v -> promptRenameCurrentOperationsProfile()),
                dashboardButton("扫描并添加电脑", v -> startOperationsPairingScan()));
        addDashboardWideAction(dashboardButton(
                "配对码在哪里？",
                v -> PairingHelpDialog.show(this, this::startOperationsPairingScan)));

        addDashboardSection("安全说明");
        addDashboardInfoCard(OperationsConnectionOverview.connectionNote());
        addDashboardSection("移除电脑");
        addDashboardInfoCard(OperationsConnectionOverview.removalNote());
        addDashboardWideAction(dashboardDestructiveButton(
                "移除当前电脑配对", v -> confirmClearProfile()));
        scheduleConnectionHeartbeat();
    }

    private View operationsProfileButton(
            OperationsProfileRegistry.Profile profile,
            List<OperationsProfileRegistry.Profile> profiles,
            long nowMilliseconds) {
        boolean current = profile.hostId.equals(preferences.getOperationsHostId());
        String profileLabel = operationsProfileLabel(profiles, profile.hostId);
        String summary = OperationsProfileOverview.summary(
                profile.watchHistory, profile.watchCheckedAt, profile.revoked, nowMilliseconds);
        String heading = profileLabel + (current ? "（当前）" : "");
        if (current && !profile.revoked) {
            TextView currentProfile = new TextView(this);
            currentProfile.setText(getString(
                    R.string.operations_profile_current_summary, heading, summary));
            TextViewCompat.setTextAppearance(currentProfile,
                    com.google.android.material.R.style.TextAppearance_Material3_BodyMedium);
            currentProfile.setTextColor(themeManager.onPrimaryContainerColor());
            currentProfile.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
            currentProfile.setPadding(dp(16), dp(6), dp(12), dp(6));
            currentProfile.setMaxLines(2);
            currentProfile.setEllipsize(TextUtils.TruncateAt.END);
            MaterialCardView currentCard = new MaterialCardView(this);
            currentCard.setCardBackgroundColor(themeManager.primaryContainerColor());
            currentCard.setContentDescription(profileLabel + "，当前电脑，" + summary);
            currentCard.addView(currentProfile, new MaterialCardView.LayoutParams(
                    MaterialCardView.LayoutParams.MATCH_PARENT,
                    MaterialCardView.LayoutParams.MATCH_PARENT));
            return currentCard;
        }
        Button button = dashboardButton(heading + "\n" + summary, v -> {
            if (profile.revoked) {
                confirmRemoveOperationsProfile(profile.hostId, profileLabel);
            } else {
                switchOperationsProfile(profile.hostId);
            }
        });
        button.setTextSize(12);
        button.setMaxLines(2);
        button.setEllipsize(TextUtils.TruncateAt.END);
        button.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
        button.setPadding(dp(12), dp(4), dp(8), dp(4));
        button.setContentDescription(profileLabel
                + (current ? "，当前电脑，" : profile.revoked ? "，点按移除，" : "，点按切换，")
                + summary);
        return button;
    }

    private void openFleetPriority(OperationsFleetOverview.Assessment fleet) {
        OperationsFleetOverview.Assessment current = OperationsFleetOverview.assess(
                preferences.getOperationsProfiles(), System.currentTimeMillis());
        if (!fleet.priorityHostId.equals(current.priorityHostId)
                || !fleet.priorityAction.equals(current.priorityAction)) {
            showConnectionPreference();
            Toast.makeText(this, "电脑队列已更新，请确认新的首要电脑",
                    Toast.LENGTH_LONG).show();
            return;
        }
        if (OperationsFleetOverview.ACTION_REMOVE.equals(current.priorityAction)) {
            confirmRemoveOperationsProfile(current.priorityHostId, current.priorityLabel);
            return;
        }
        if (!OperationsFleetOverview.ACTION_OPEN.equals(current.priorityAction)) {
            return;
        }
        if (current.priorityHostId.equals(preferences.getOperationsHostId())) {
            openExistingProfile();
        } else {
            switchOperationsProfile(current.priorityHostId);
        }
    }

    private void refreshAllOperationsProfiles() {
        if (fleetCheckInFlight) {
            Toast.makeText(this, "电脑巡检正在进行", Toast.LENGTH_SHORT).show();
            return;
        }
        List<OperationsProfileRegistry.Profile> targets = new ArrayList<>();
        for (OperationsProfileRegistry.Profile profile : preferences.getOperationsProfiles()) {
            if (!profile.revoked) {
                targets.add(profile);
            }
        }
        if (targets.isEmpty()) {
            Toast.makeText(this, "没有可巡检的配对电脑", Toast.LENGTH_LONG).show();
            return;
        }

        fleetCheckInFlight = true;
        int generation = ++fleetCheckGeneration;
        String activeHostBefore = preferences.getOperationsHostId();
        int total = targets.size();
        AtomicInteger completed = new AtomicInteger();
        setBusy("正在巡检 0 / " + total + " 台电脑…");
        title.setText("巡检电脑总览");
        details.setText("只读取每台电脑的脱敏聚合状态；最多并行检查 3 台，使用各自的设备密钥、证书固定和连接偏好。巡检不会切换当前操作目标，也不会执行远程动作。");

        for (OperationsProfileRegistry.Profile profile : targets) {
            fleetExecutor.execute(() -> {
                FleetCheckResult result = checkOperationsProfile(profile);
                long checkedAt = System.currentTimeMillis();
                preferences.recordOperationsProfileWatchState(
                        profile.hostId, result.state, checkedAt);
                if (result.revoked) {
                    clearRemoteWindowSnapshotSecrets(profile.hostId);
                    preferences.markOperationsProfileRevoked(profile.hostId);
                }
                completed.incrementAndGet();
                runOnUiThread(() -> completeFleetCheckProgress(
                        generation, activeHostBefore, completed.get(), total));
            });
        }
    }

    private FleetCheckResult checkOperationsProfile(
            OperationsProfileRegistry.Profile profile) {
        boolean relayFirst = OperationsConnectionPreference.prefersRelay(
                profile.connectionPreference);
        try {
            return relayFirst ? checkRelayOperationsProfile(profile)
                    : checkLocalOperationsProfile(profile);
        } catch (Exception firstException) {
            if (isRevokedException(firstException)) {
                return FleetCheckResult.revoked();
            }
        }
        try {
            return relayFirst ? checkLocalOperationsProfile(profile)
                    : checkRelayOperationsProfile(profile);
        } catch (Exception secondException) {
            return isRevokedException(secondException)
                    ? FleetCheckResult.revoked() : FleetCheckResult.offline();
        }
    }

    private FleetCheckResult checkLocalOperationsProfile(
            OperationsProfileRegistry.Profile profile) throws Exception {
        OperationsApiClient profileClient = new OperationsApiClient(
                profile.endpoint,
                profile.certificatePin,
                preferences.getOrCreateDeviceId(),
                new OperationsDeviceIdentity(profile.hostId),
                FLEET_CONNECT_TIMEOUT_MILLISECONDS,
                FLEET_READ_TIMEOUT_MILLISECONDS);
        JSONObject response = profileClient.get("/ops/v1/monitor");
        JSONObject monitor = response.optJSONObject("data");
        if (monitor == null) {
            throw new IllegalStateException("incomplete_live_monitor_response");
        }
        return FleetCheckResult.reachable(OperationsMonitorClassifier.watchState(
                monitor, OperationsWatchHistory.STATE_ONLINE));
    }

    private FleetCheckResult checkRelayOperationsProfile(
            OperationsProfileRegistry.Profile profile) throws Exception {
        OperationsRelayApiClient profileClient = new OperationsRelayApiClient(
                profile.hostId,
                preferences.getOrCreateDeviceId(),
                profile.certificatePin,
                new OperationsDeviceIdentity(profile.hostId),
                FLEET_CONNECT_TIMEOUT_MILLISECONDS,
                FLEET_READ_TIMEOUT_MILLISECONDS);
        JSONObject response = profileClient.getSnapshot();
        JSONObject host = response.optJSONObject("host");
        if (host == null) {
            throw new IllegalStateException("incomplete_relay_snapshot");
        }
        boolean fresh = OperationsRelayPolicy.isHostFresh(
                host.optLong("signedAt", 0L), System.currentTimeMillis());
        if (!fresh) {
            return FleetCheckResult.reachable(
                    OperationsWatchHistory.STATE_REMOTE_WAITING);
        }
        JSONObject snapshot = host.optJSONObject("snapshot");
        JSONObject monitor = snapshot == null ? null : snapshot.optJSONObject("monitor");
        return FleetCheckResult.reachable(monitor == null
                ? OperationsWatchHistory.STATE_REMOTE_ONLINE
                : OperationsMonitorClassifier.watchState(
                        monitor, OperationsWatchHistory.STATE_REMOTE_ONLINE));
    }

    private void completeFleetCheckProgress(
            int generation,
            String activeHostBefore,
            int completed,
            int total) {
        if (generation != fleetCheckGeneration || isFinishing() || isDestroyed()) {
            return;
        }
        if (!fleetCheckInFlight) {
            return;
        }
        state.setText(getString(R.string.operations_fleet_progress, completed, total));
        if (completed < total) {
            return;
        }
        fleetCheckInFlight = false;
        boolean activeChanged = !activeHostBefore.equals(preferences.getOperationsHostId());
        if (activeChanged) {
            resetOperationsClientsForProfileChange();
            OperationsWatchService.restartForProfileChange(this);
        }
        showConnectionPreference();
        OperationsFleetOverview.Assessment fleet = OperationsFleetOverview.assess(
                preferences.getOperationsProfiles(), System.currentTimeMillis());
        Toast.makeText(this,
                "巡检完成：" + fleet.summary,
                Toast.LENGTH_LONG).show();
    }

    private static final class FleetCheckResult {
        final String state;
        final boolean revoked;

        FleetCheckResult(String state, boolean revoked) {
            this.state = state;
            this.revoked = revoked;
        }

        static FleetCheckResult reachable(String state) {
            return new FleetCheckResult(state, false);
        }

        static FleetCheckResult offline() {
            return new FleetCheckResult(OperationsWatchHistory.STATE_OFFLINE, false);
        }

        static FleetCheckResult revoked() {
            return new FleetCheckResult(OperationsWatchHistory.STATE_REVOKED, true);
        }
    }

    private String operationsProfileLabel(
            List<OperationsProfileRegistry.Profile> profiles, String hostId) {
        for (int index = 0; index < profiles.size(); index++) {
            OperationsProfileRegistry.Profile profile = profiles.get(index);
            if (profile.hostId.equals(hostId)) {
                return profile.label.isEmpty() ? "电脑 " + (index + 1) : profile.label;
            }
        }
        return "未选择";
    }

    private void startOperationsPairingScan() {
        if (hasCameraPermission()
                || shouldShowRequestPermissionRationale(Manifest.permission.CAMERA)) {
            preferences.saveCameraPermissionBlocked(false);
        } else if (cameraPermissionNeedsSystemSettings()) {
            showQrScanFailure(QrScanFailurePresentation.CAMERA_PERMISSION_BLOCKED);
            return;
        }
        startActivityForResult(new Intent(this, QrScanActivity.class), REQUEST_QR_SCAN);
    }

    private boolean hasCameraPermission() {
        return checkSelfPermission(Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED;
    }

    private boolean cameraPermissionNeedsSystemSettings() {
        return !hasCameraPermission()
                && preferences.isCameraPermissionBlocked()
                && !shouldShowRequestPermissionRationale(Manifest.permission.CAMERA);
    }

    private void showQrScanFailure(String reason) {
        preferences.saveCameraPermissionBlocked(
                QrScanFailurePresentation.CAMERA_PERMISSION_BLOCKED.equals(reason));
        QrScanRecoveryDialog.show(this, reason, this::startOperationsPairingScan);
    }

    private void promptRenameCurrentOperationsProfile() {
        String hostId = preferences.getOperationsHostId();
        List<OperationsProfileRegistry.Profile> profiles = preferences.getOperationsProfiles();
        String currentLabel = operationsProfileLabel(profiles, hostId);
        EditText input = new EditText(this);
        input.setSingleLine(true);
        input.setMaxLines(1);
        input.setText(currentLabel.startsWith("电脑 ") ? "" : currentLabel);
        input.setHint("例如：一号线 AOI");
        new MaterialAlertDialogBuilder(this)
                .setTitle("命名当前电脑")
                .setMessage("名称只保存在这台手机，不会发送给电脑或固定中继。最多 20 个字符。")
                .setView(input)
                .setNegativeButton("取消", null)
                .setPositiveButton("保存", (dialog, which) -> {
                    preferences.renameOperationsProfile(hostId, input.getText().toString());
                    showConnectionPreference();
                })
                .show();
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == REQUEST_QR_SCAN) {
            if (resultCode == RESULT_OK && data != null) {
                preferences.saveCameraPermissionBlocked(false);
                String result = data.getStringExtra(QrScanActivity.EXTRA_QR_RESULT);
                handleScannedPairing(result);
                return;
            }
            String failureReason = data == null
                    ? "" : data.getStringExtra(QrScanActivity.EXTRA_SCAN_FAILURE);
            if (failureReason != null && !failureReason.isEmpty()) {
                showQrScanFailure(failureReason);
            } else {
                Toast.makeText(this, "已取消添加电脑", Toast.LENGTH_SHORT).show();
            }
            return;
        }
        super.onActivityResult(requestCode, resultCode, data);
    }

    private void handleScannedPairing(String rawPairing) {
        if (!OperationsPairingPayload.isPairingInput(rawPairing)) {
            showPairingScanFailure(PairingFailurePresentation.INVALID_QR);
            return;
        }
        try {
            OperationsPairingPayload.parse(rawPairing);
            beginPairing(rawPairing);
        } catch (Exception ex) {
            showPairingScanFailure(PairingFailurePresentation.reasonFor(ex));
        }
    }

    private void showPairingScanFailure(String reason) {
        PairingScanRecoveryDialog.show(this, reason, this::startOperationsPairingScan);
    }

    private void switchOperationsProfile(String hostId) {
        if (hostId.equals(preferences.getOperationsHostId())) {
            return;
        }
        if (!preferences.selectOperationsProfile(hostId)) {
            Toast.makeText(this, "这台电脑的配对资料不可用", Toast.LENGTH_LONG).show();
            return;
        }
        resetOperationsClientsForProfileChange();
        OperationsWatchService.restartForProfileChange(this);
        Toast.makeText(this, "已切换当前运维电脑", Toast.LENGTH_SHORT).show();
        openExistingProfile();
    }

    private void resetOperationsClientsForProfileChange() {
        connectionRequestGeneration++;
        remoteTaskGeneration++;
        client = null;
        relayClient = null;
        operationsClientHostId = "";
        lastRelaySnapshotResponse = null;
        remoteDashboard = false;
        connectionHeartbeatInFlight = false;
        connectionHeartbeatHandler.removeCallbacks(connectionHeartbeat);
        clearDashboardLiveStatusReferences();
    }

    private void refreshOperationsTargetPresentation() {
        if (profileTarget == null || preferences == null) {
            return;
        }
        boolean paired = preferences.hasOperationsProfile();
        String label = paired ? preferences.getActiveOperationsProfileLabel() : "未配对";
        profileTarget.setText(getString(R.string.operations_target_label, label));
        profileTarget.setContentDescription(paired
                ? getString(R.string.operations_target_content_description, label)
                : getString(R.string.operations_target_unpaired_content_description));
        profileTarget.setEnabled(paired);
        profileTarget.setAlpha(paired ? 1f : 0.55f);
    }

    private void showTargetedConfirmation(
            String dialogTitle,
            String body,
            String negativeLabel,
            String positiveLabel,
            Runnable confirmedAction) {
        String expectedHostId = operationsClientHostId.isEmpty()
                ? preferences.getOperationsHostId() : operationsClientHostId;
        if (!OperationsTargetPolicy.isSameTarget(
                expectedHostId, preferences.getOperationsHostId())) {
            cancelActionAfterOperationsTargetChange();
            return;
        }
        String targetLabel = preferences.getOperationsProfileLabel(expectedHostId);
        new MaterialAlertDialogBuilder(this)
                .setTitle(dialogTitle)
                .setMessage(OperationsTargetPolicy.confirmationMessage(targetLabel, body))
                .setNegativeButton(negativeLabel, null)
                .setPositiveButton(positiveLabel,
                        (dialog, which) -> runIfOperationsTargetUnchanged(
                                expectedHostId, confirmedAction))
                .show();
    }

    private void runIfOperationsTargetUnchanged(String expectedHostId, Runnable action) {
        String activeHostId = preferences.getOperationsHostId();
        if (!OperationsTargetPolicy.isSameTarget(expectedHostId, activeHostId)) {
            cancelActionAfterOperationsTargetChange();
            return;
        }
        action.run();
    }

    private boolean ensureOperationsClientTargetIsCurrent() {
        String activeHostId = preferences.getOperationsHostId();
        String expectedHostId = operationsClientHostId.isEmpty()
                ? activeHostId : operationsClientHostId;
        if (OperationsTargetPolicy.isSameTarget(expectedHostId, activeHostId)) {
            return true;
        }
        cancelActionAfterOperationsTargetChange();
        return false;
    }

    private void cancelActionAfterOperationsTargetChange() {
        Toast.makeText(this,
                "当前电脑已切换到 " + preferences.getActiveOperationsProfileLabel()
                        + "，本次操作已取消",
                Toast.LENGTH_LONG).show();
        reconnectAfterOperationsTargetChange();
    }

    private void reconnectAfterOperationsTargetChange() {
        resetOperationsClientsForProfileChange();
        refreshOperationsTargetPresentation();
        if (preferences.hasOperationsProfile()) {
            openExistingProfile();
        }
    }

    private void showDashboard() {
        scrollDashboardToTop();
        refreshOperationsTargetPresentation();
        leaveSupportCenter();
        leaveLiveMonitor();
        remoteDashboard = false;
        connectionRecoveryVisible = false;
        lastRelaySnapshotResponse = null;
        if (restorePendingDestination(true)) {
            return;
        }
        currentDestination = OperationsDestinationState.OVERVIEW;
        dashboardVisible = true;
        showingDashboardSummary = true;
        progress.setVisibility(View.GONE);
        title.setText("运维伴侣");
        state.setText(directConnectionState());
        details.setText(R.string.operations_dashboard_loading_summary);
        actions.removeAllViews();
        dashboardFlowAvailable = false;
        dashboardFlowActive = false;
        dashboardFlowCancelAvailable = false;
        dashboardFlowCancelCapabilityAvailable = true;
        dashboardRestartCapabilityAvailable = true;
        dashboardRemoteHostFresh = true;

        addDashboardSection("建议操作");
        dashboardPriorityAction = dashboardPrimaryButton("正在分析运行状态…", null);
        dashboardPriorityAction.setEnabled(false);
        addDashboardWideAction(dashboardPriorityAction);

        addDashboardSection("远程操作");
        addDashboardInfoCard(OperationsRemoteActionPresentation.scopeNote(false, true));
        addDashboardTaskGroup(
                "分析与监控",
                OperationsRemoteActionPresentation.diagnosticsDescription(false),
                dashboardTonalButton("远程排障", v -> showTriageCenter()),
                dashboardButton("持续监控", v -> showLiveMonitor()));
        addDashboardTaskGroup(
                "窗口控制",
                OperationsRemoteActionPresentation.windowDescription(false),
                dashboardTonalButton("显示主窗口", v -> runWindowAction("show", "主窗口已显示")),
                dashboardButton("最小化窗口", v -> confirmMinimizeWindow()));
        addDashboardTaskGroup(
                "恢复与控制",
                OperationsRemoteActionPresentation.recoveryDescription(false),
                dashboardTonalButton("恢复消息通道", v -> confirmRecoverMessageChannel()),
                dashboardRestartApplicationButton = dashboardDestructiveButton(
                        "重启 ColorVision", v -> confirmRestartApplication()));
        dashboardCancelFlowButton = dashboardButton("取消检测（读取中）", v -> confirmCancelCurrentFlow());
        dashboardCancelFlowButton.setEnabled(false);
        addDashboardTaskGroup(
                "检测与连接",
                "中断当前检测，或调整这台电脑的首选连接方式。",
                dashboardCancelFlowButton,
                dashboardButton("连接方式", v -> showConnectionPreference()));

        dashboardStatusHeading = addDashboardSection(
                OperationsDashboardStatusFormatter.sectionTitle(false, true));
        dashboardFlowStatus = dashboardStatusRow("检测",
                v -> loadCapability("/ops/v1/flow/runtime"));
        dashboardDeviceStatus = dashboardStatusRow("设备",
                v -> showDeviceHealthOverview());
        dashboardMessageStatus = dashboardStatusRow("消息",
                v -> loadCapability("/ops/v1/messaging/health"));
        dashboardAlertStatus = dashboardStatusRow("告警",
                v -> loadCapability("/ops/v1/alerts"));
        dashboardPerformanceStatus = dashboardStatusRow("性能",
                v -> loadCapability("/ops/v1/diagnostics/performance"));
        dashboardRecoveryStatus = dashboardStatusRow("恢复", v -> showLiveMonitor());
        dashboardStatusCaption = addDashboardStatusCard(
                OperationsDashboardStatusFormatter.sectionCaption(false, true),
                dashboardFlowStatus,
                dashboardDeviceStatus,
                dashboardMessageStatus,
                dashboardAlertStatus,
                dashboardPerformanceStatus,
                dashboardRecoveryStatus);

        addDashboardSection("更多工具");
        MaterialCardView toolboxEntry = OperationsToolboxBottomSheet.createDashboardEntry(
                this, themeManager, v -> showOperationsToolbox());
        LinearLayout.LayoutParams toolboxParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        toolboxParams.setMargins(0, 0, 0, dp(8));
        actions.addView(toolboxEntry, toolboxParams);
        scheduleConnectionHeartbeat();
        ensureOperationsWatchRunning();
        if (openPendingOperationsDestination()) {
            return;
        }
        loadCapability("/ops/v1/snapshot");
        loadDashboardLiveStatus();
    }

    private void showOperationsToolbox() {
        OperationsToolboxBottomSheet.show(
                this,
                themeManager,
                OperationsToolboxPresentation.create(),
                this::runOperationsToolboxAction);
    }

    private void runOperationsToolboxAction(String actionId) {
        switch (actionId) {
            case OperationsToolboxPresentation.ACTION_SERVICES_HEALTH:
                loadCapability("/ops/v1/services/health");
                return;
            case OperationsToolboxPresentation.ACTION_RESTART_MQTT:
                confirmRestartMqtt();
                return;
            case OperationsToolboxPresentation.ACTION_RECENT_EVENTS:
                loadCapability("/ops/v1/diagnostics/recent-events");
                return;
            case OperationsToolboxPresentation.ACTION_FAILURES:
                loadCapability("/ops/v1/diagnostics/failures");
                return;
            case OperationsToolboxPresentation.ACTION_JOBS:
                showJobs();
                return;
            case OperationsToolboxPresentation.ACTION_AUDIT:
                loadCapability("/ops/v1/audit");
                return;
            case OperationsToolboxPresentation.ACTION_CREATE_DIAGNOSTIC:
                confirmCreateDiagnosticJob();
                return;
            case OperationsToolboxPresentation.ACTION_CREATE_SNAPSHOT:
                confirmCreateWindowSnapshotJob();
                return;
            case OperationsToolboxPresentation.ACTION_SHARE_SUMMARY:
                loadAndShareSafeDiagnostics();
                return;
            case OperationsToolboxPresentation.ACTION_SUPPORT:
                showSupportCenter();
                return;
            case OperationsToolboxPresentation.ACTION_DEPLOYMENT:
                confirmDeploymentReceipt();
                return;
            case OperationsToolboxPresentation.ACTION_TIMELINE:
                showOperationsWatchHistory();
                return;
            default:
                return;
        }
    }

    private void showRemoteDashboard(JSONObject response) {
        scrollDashboardToTop();
        refreshOperationsTargetPresentation();
        leaveSupportCenter();
        leaveLiveMonitor();
        remoteDashboard = true;
        connectionRecoveryVisible = false;
        lastRelaySnapshotResponse = response;
        if (restorePendingDestination(false)) {
            return;
        }
        currentDestination = OperationsDestinationState.OVERVIEW;
        dashboardVisible = true;
        showingDashboardSummary = true;
        progress.setVisibility(View.GONE);
        title.setText("运维伴侣");
        actions.removeAllViews();
        clearDashboardLiveStatusReferences();
        updateRemoteDashboardStatus(response);

        JSONObject host = response.optJSONObject("host");
        JSONObject snapshot = host == null ? null : host.optJSONObject("snapshot");
        JSONObject monitor = snapshot == null ? null : snapshot.optJSONObject("monitor");
        JSONArray capabilities = host == null ? null : host.optJSONArray("capabilities");
        boolean canShowWindow = contains(capabilities, OperationsRelayPolicy.CAPABILITY_SHOW_WINDOW);
        boolean canMinimizeWindow = contains(
                capabilities, OperationsRelayPolicy.CAPABILITY_MINIMIZE_WINDOW);
        boolean canRecoverMessageChannel = contains(
                capabilities, OperationsRelayPolicy.CAPABILITY_RECOVER_MESSAGE_CHANNEL);
        boolean canRestartMqtt = contains(
                capabilities, OperationsRelayPolicy.CAPABILITY_RESTART_MQTT);
        boolean canCancelFlow = contains(capabilities, OperationsRelayPolicy.CAPABILITY_CANCEL_FLOW);
        boolean canRestartApplication = contains(
                capabilities, OperationsRelayPolicy.CAPABILITY_RESTART_APPLICATION);
        boolean canRequestDiagnostics = contains(
                capabilities, OperationsRelayPolicy.CAPABILITY_REQUEST_DIAGNOSTICS);
        dashboardFlowCancelCapabilityAvailable = canCancelFlow;
        dashboardRestartCapabilityAvailable = canRestartApplication;
        dashboardRemoteHostFresh = host != null && OperationsRelayPolicy.isHostFresh(
                host.optLong("signedAt", 0L), System.currentTimeMillis());

        addDashboardSection("建议操作");
        dashboardPriorityAction = dashboardPrimaryButton("正在分析运行状态…", null);
        addDashboardWideAction(dashboardPriorityAction);
        updateDashboardPriority(dashboardRemoteHostFresh
                ? OperationsDashboardAdvisor.fromMonitor(
                        monitor, attentionRemindersAvailable())
                : OperationsDashboardAdvisor.staleRemoteSnapshot());

        addDashboardSection("远程操作");
        addDashboardInfoCard(OperationsRemoteActionPresentation.scopeNote(
                true, dashboardRemoteHostFresh));
        Button showWindow = dashboardTonalButton("显示主窗口", v -> runRemoteTask(
                OperationsRelayPolicy.CAPABILITY_SHOW_WINDOW, new JSONObject()));
        showWindow.setEnabled(OperationsRelayPolicy.canControlWindow(
                canShowWindow, dashboardRemoteHostFresh));
        Button minimizeWindow = dashboardButton("最小化主窗口",
                v -> confirmRemoteMinimizeWindow());
        minimizeWindow.setEnabled(OperationsRelayPolicy.canControlWindow(
                canMinimizeWindow, dashboardRemoteHostFresh));
        addDashboardTaskGroup(
                "窗口控制",
                OperationsRemoteActionPresentation.windowDescription(
                        true, dashboardRemoteHostFresh),
                showWindow,
                minimizeWindow);
        Button diagnostics = dashboardTonalButton(
                dashboardRemoteHostFresh ? "请求远程诊断" : "排队请求诊断", v -> {
            JSONObject payload = new JSONObject();
            try {
                payload.put("reason", "Android 运维伴侣远程调试请求");
            } catch (Exception ignored) {
            }
            runRemoteTask(OperationsRelayPolicy.CAPABILITY_REQUEST_DIAGNOSTICS, payload);
        });
        diagnostics.setEnabled(canRequestDiagnostics);
        Button messageActions = dashboardButton(
                dashboardRemoteHostFresh ? "检查消息通道" : "上次消息状态",
                v -> showLatestRemoteMonitorDetail("message"));
        messageActions.setEnabled(monitor != null
                && (canRecoverMessageChannel || canRestartMqtt));
        addDashboardTaskGroup(
                "诊断与恢复",
                OperationsRemoteActionPresentation.diagnosticsDescription(
                        true, dashboardRemoteHostFresh),
                diagnostics,
                messageActions);
        if (dashboardRemoteHostFresh) {
            dashboardCancelFlowButton = dashboardButton(
                    "取消检测（读取中）", v -> confirmCancelCurrentFlow());
            dashboardCancelFlowButton.setEnabled(false);
            dashboardRestartApplicationButton = dashboardDestructiveButton(
                    "重启 ColorVision", v -> confirmRestartApplication());
            dashboardRestartApplicationButton.setEnabled(false);
            addDashboardTaskGroup(
                    "受控恢复",
                    OperationsRemoteActionPresentation.recoveryDescription(true),
                    dashboardCancelFlowButton,
                    dashboardRestartApplicationButton);
        }

        if (monitor != null) {
            dashboardStatusHeading = addDashboardSection(
                    OperationsDashboardStatusFormatter.sectionTitle(
                            true, dashboardRemoteHostFresh));
            dashboardFlowStatus = dashboardStatusRow("检测",
                    v -> showLatestRemoteMonitorDetail("flow"));
            dashboardDeviceStatus = dashboardStatusRow("设备",
                    v -> showLatestRemoteMonitorDetail("devices"));
            dashboardMessageStatus = dashboardStatusRow("消息",
                    v -> showLatestRemoteMonitorDetail("message"));
            dashboardAlertStatus = dashboardStatusRow("告警",
                    v -> showLatestRemoteMonitorDetail("alerts"));
            dashboardPerformanceStatus = dashboardStatusRow("性能",
                    v -> showLatestRemoteMonitorDetail("performance"));
            dashboardRecoveryStatus = dashboardStatusRow("恢复",
                    v -> showLatestRemoteMonitorDetail("recovery"));
            dashboardStatusCaption = addDashboardStatusCard(
                    OperationsDashboardStatusFormatter.sectionCaption(
                            true, dashboardRemoteHostFresh),
                    dashboardFlowStatus,
                    dashboardDeviceStatus,
                    dashboardMessageStatus,
                    dashboardAlertStatus,
                    dashboardPerformanceStatus,
                    dashboardRecoveryStatus);
            updateDashboardLiveStatus(monitor);
        }

        addDashboardSection("连接与记录");
        addDashboardActionRow(
                dashboardButton("刷新远程状态", v -> refreshRemoteDashboard()),
                dashboardButton("连接方式", v -> showConnectionPreference()));
        Button recentTask = dashboardButton("最近远程请求", v -> refreshRecentRemoteTask());
        recentTask.setEnabled(OperationsRelayPolicy.isSafeIdentifier(
                preferences.getOperationsRelayTaskId()));
        addDashboardActionRow(
                recentTask,
                dashboardButton("运维时间线", v -> showOperationsWatchHistory()));

        scheduleConnectionHeartbeat();
        ensureOperationsWatchRunning();
    }

    private void updateRemoteDashboardStatus(JSONObject response) {
        JSONObject host = response.optJSONObject("host");
        if (host == null) {
            state.setText("远程中继响应不完整");
            details.setText("配对资料已保留，后台会继续重试。");
            return;
        }
        boolean fresh = OperationsRelayPolicy.isHostFresh(
                host.optLong("signedAt", 0L), System.currentTimeMillis());
        dashboardRemoteHostFresh = fresh;
        JSONObject snapshot = host.optJSONObject("snapshot");
        JSONObject monitor = snapshot == null ? null : snapshot.optJSONObject("monitor");
        JSONObject window = snapshot == null ? null : snapshot.optJSONObject("mainWindow");
        boolean running = snapshot != null && snapshot.optBoolean("isRunning", false);
        boolean windowExists = window != null && window.optBoolean("exists", false);
        boolean windowVisible = window != null && window.optBoolean("isVisible", false);

        state.setText(remoteConnectionState(fresh));
        if (dashboardStatusHeading != null) {
            dashboardStatusHeading.setText(
                    OperationsDashboardStatusFormatter.sectionTitle(true, fresh));
        }
        if (dashboardStatusCaption != null) {
            dashboardStatusCaption.setText(
                    OperationsDashboardStatusFormatter.sectionCaption(true, fresh));
        }
        long signedAt = host.optLong("signedAt", 0L);
        if (monitor == null) {
            markDashboardLiveStatusUnavailable();
        } else if (dashboardFlowStatus != null) {
            updateDashboardLiveStatus(monitor);
        }
        updateDashboardPriority(fresh
                ? OperationsDashboardAdvisor.fromMonitor(
                        monitor, attentionRemindersAvailable())
                : OperationsDashboardAdvisor.staleRemoteSnapshot());
        updateDashboardRestartApplicationAction();
        details.setText(OperationsDashboardOverview.remoteSummary(
                fresh,
                running,
                windowExists,
                windowVisible,
                monitor != null,
                signedAt,
                System.currentTimeMillis()));
    }

    private void showRemoteMonitorDetail(String section, JSONObject monitor) {
        showingDashboardSummary = false;
        remoteRestartMqttButton = null;
        progress.setVisibility(View.GONE);
        title.setText(remoteMonitorTitle(section));
        state.setText("电脑签名远程状态");
        details.setText(getString(
                R.string.operations_remote_monitor_signed_summary,
                formatRemoteMonitorSection(section, monitor)));
        actions.removeAllViews();
        if ("message".equals(section)) {
            Button recoverMessageChannel = dashboardButton("恢复消息通道",
                    v -> confirmRemoteMessageChannelRecovery());
            recoverMessageChannel.setEnabled(isRemoteCapabilityAvailable(
                    OperationsRelayPolicy.CAPABILITY_RECOVER_MESSAGE_CHANNEL));
            remoteRestartMqttButton = dashboardButton("重启 MQTT",
                    v -> confirmRemoteMqttRestart());
            remoteRestartMqttButton.setEnabled(canRestartRemoteMqttService(
                    lastRelaySnapshotResponse));
            addDashboardActionRow(recoverMessageChannel, remoteRestartMqttButton);
        } else if ("alerts".equals(section)) {
            Button failureEvidence = dashboardButton("崩溃线索",
                    v -> readRemoteFailureEvidence());
            failureEvidence.setEnabled(canReadRemoteFailureEvidence(
                    lastRelaySnapshotResponse));
            Button windowSnapshot = dashboardButton("主窗口快照",
                    v -> confirmRemoteWindowSnapshot());
            windowSnapshot.setEnabled(canCaptureRemoteWindowSnapshot(
                    lastRelaySnapshotResponse));
            addDashboardActionRow(failureEvidence, windowSnapshot);
        }
        addDashboardActionRow(
                dashboardButton("刷新远程状态", v -> refreshRemoteMonitorDetail(section)),
                dashboardButton("返回运维概览", v -> showCurrentDashboard()));
        scheduleConnectionHeartbeat();
    }

    private void showLatestRemoteMonitorDetail(String section) {
        JSONObject monitor = remoteMonitor(lastRelaySnapshotResponse);
        if (monitor == null) {
            Toast.makeText(this, "远程状态暂不可用，请刷新后重试", Toast.LENGTH_LONG).show();
            return;
        }
        showRemoteMonitorDetail(section, monitor);
    }

    private void refreshRemoteMonitorDetail(String section) {
        progress.setVisibility(View.VISIBLE);
        state.setText("正在读取电脑签名状态…");
        executor.execute(() -> {
            try {
                JSONObject response = relayClient.getSnapshot();
                JSONObject monitor = remoteMonitor(response);
                if (monitor == null) {
                    throw new IllegalStateException("remote_monitor_unavailable");
                }
                runOnUiThread(() -> {
                    lastRelaySnapshotResponse = response;
                    showRemoteMonitorDetail(section, monitor);
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private JSONObject remoteMonitor(JSONObject response) {
        JSONObject host = response == null ? null : response.optJSONObject("host");
        JSONObject snapshot = host == null ? null : host.optJSONObject("snapshot");
        return snapshot == null ? null : snapshot.optJSONObject("monitor");
    }

    private boolean isRemoteCapabilityAvailable(String capabilityId) {
        JSONObject host = lastRelaySnapshotResponse == null
                ? null : lastRelaySnapshotResponse.optJSONObject("host");
        return host != null && contains(host.optJSONArray("capabilities"), capabilityId);
    }

    private boolean canRestartRemoteMqttService(JSONObject response) {
        JSONObject host = response == null ? null : response.optJSONObject("host");
        JSONObject monitor = remoteMonitor(response);
        JSONObject flow = monitor == null ? null : monitor.optJSONObject("flow");
        JSONObject mqttService = monitor == null ? null : monitor.optJSONObject("mqttService");
        return OperationsRelayPolicy.canRestartMqttService(
                host != null && contains(host.optJSONArray("capabilities"),
                        OperationsRelayPolicy.CAPABILITY_RESTART_MQTT),
                host != null && OperationsRelayPolicy.isHostFresh(
                        host.optLong("signedAt", 0L), System.currentTimeMillis()),
                flow != null && flow.optBoolean("available", false),
                flow != null && flow.optBoolean("isActive", false),
                mqttService != null && mqttService.optBoolean("available", false),
                mqttService == null ? "unknown" : mqttService.optString("status", "unknown"),
                mqttService != null && mqttService.optBoolean("maintenanceSupported", false));
    }

    private boolean canReadRemoteFailureEvidence(JSONObject response) {
        JSONObject host = response == null ? null : response.optJSONObject("host");
        return OperationsRelayPolicy.canReadFailureEvidence(
                host != null && contains(host.optJSONArray("capabilities"),
                        OperationsRelayPolicy.CAPABILITY_READ_FAILURE_EVIDENCE),
                host != null && OperationsRelayPolicy.isHostFresh(
                        host.optLong("signedAt", 0L), System.currentTimeMillis()));
    }

    private void readRemoteFailureEvidence() {
        if (!canReadRemoteFailureEvidence(lastRelaySnapshotResponse)) {
            Toast.makeText(this, "电脑签名状态已过期或未声明该能力，请先刷新远程状态",
                    Toast.LENGTH_LONG).show();
            return;
        }
        runRemoteTask(OperationsRelayPolicy.CAPABILITY_READ_FAILURE_EVIDENCE,
                new JSONObject());
    }

    private boolean canCaptureRemoteWindowSnapshot(JSONObject response) {
        JSONObject host = response == null ? null : response.optJSONObject("host");
        return OperationsRelayPolicy.canCaptureWindowSnapshot(
                host != null && contains(host.optJSONArray("capabilities"),
                        OperationsRelayPolicy.CAPABILITY_CAPTURE_WINDOW_SNAPSHOT),
                host != null && OperationsRelayPolicy.isHostFresh(
                        host.optLong("signedAt", 0L), System.currentTimeMillis()),
                Build.VERSION.SDK_INT);
    }

    private void confirmRemoteWindowSnapshot() {
        if (!canCaptureRemoteWindowSnapshot(lastRelaySnapshotResponse)) {
            String message = OperationsE2eIdentity.isSupported()
                    ? "电脑签名状态已过期或未声明端到端快照能力，请先刷新远程状态"
                    : "远程端到端快照需要 Android 12 或更高版本；现场局域网快照仍可使用";
            Toast.makeText(this, message, Toast.LENGTH_LONG).show();
            return;
        }
        showTargetedConfirmation(
                "采集远程主窗口快照？",
                "只会捕获当前 ColorVision 主窗口的一张 JPEG，不会捕获整个桌面或连续录屏；画面可能包含当前可见的业务图像。\n\n确认后，电脑会为本次快照端到端加密。固定站点只能短时保存最多 5 分钟的密文，无法查看画面；手机校验电脑签名、密文完整性、加密标签、JPEG 格式与尺寸后才会在应用内预览。",
                "取消", "确认采集", this::runRemoteWindowSnapshotTask);
    }

    private String remoteMonitorTitle(String section) {
        switch (section) {
            case "flow": return "远程检测状态";
            case "devices": return "远程设备状态";
            case "message": return "远程消息状态";
            case "alerts": return "远程告警摘要";
            case "performance": return "远程性能快照";
            case "recovery": return "远程恢复状态";
            default: return "远程运行状态";
        }
    }

    private String formatRemoteMonitorSection(String section, JSONObject monitor) {
        JSONObject payload;
        switch (section) {
            case "flow":
                payload = monitor.optJSONObject("flow");
                return payload == null ? "当前无法读取检测状态。" : formatFlowRuntimeStatus(payload);
            case "devices":
                payload = monitor.optJSONObject("devices");
                return payload == null ? "当前无法读取检测设备汇总。" : formatDeviceHealth(payload);
            case "message":
                payload = monitor.optJSONObject("messageChannel");
                return (payload == null
                        ? "当前无法读取消息通道状态。"
                        : formatMessageChannelHealth(payload, true))
                        + "\n\n" + formatRemoteMqttService(monitor.optJSONObject("mqttService"));
            case "alerts":
                return formatRemoteAlertSummary(monitor.optJSONObject("alerts"));
            case "performance":
                payload = monitor.optJSONObject("performance");
                return payload == null ? "当前无法读取进程性能快照。" : formatPerformanceSnapshot(payload);
            case "recovery":
                return formatRemoteRecoveryStatus(monitor.optJSONObject("applicationRecovery"));
            default:
                return "当前远程状态类别不可用。";
        }
    }

    private String formatRemoteAlertSummary(JSONObject alerts) {
        if (alerts == null) {
            return "当前无法读取近期告警计数。";
        }
        int count = alerts.optInt("count", 0);
        StringBuilder text = new StringBuilder("近期告警：").append(count).append(" 条")
                .append("\n警告：").append(alerts.optInt("warningCount", 0))
                .append(" · 错误：").append(alerts.optInt("errorCount", 0))
                .append(" · 严重：").append(alerts.optInt("criticalCount", 0));
        String latestAt = shortTime(alerts.optString("latestOccurredAt", ""));
        if (!latestAt.isEmpty()) {
            text.append("\n最近发生：").append(latestAt);
        }
        return text.append("\n\n只返回聚合计数和最近发生时间，不包含告警正文、日志、路径或身份信息。")
                .toString();
    }

    private String formatRemoteRecoveryStatus(JSONObject recovery) {
        if (recovery == null || !recovery.optBoolean("supported", false)) {
            return "当前系统不支持 ColorVision 异常恢复。";
        }
        StringBuilder text = new StringBuilder();
        if (!recovery.optBoolean("registered", false)) {
            text.append("异常恢复尚未就绪。 ");
        } else if (recovery.optBoolean("restartedAfterFailure", false)) {
            text.append("本次启动已由固定目标看门狗或 Windows 异常恢复接管。 ");
        } else if (recovery.optBoolean("automaticWatchdogActive", false)) {
            text.append("本机异常看门狗已就绪，只会恢复同目录 ColorVision。 ");
        } else {
            text.append("Windows 异常恢复已登记。 ");
        }
        return text.append("手机不能指定程序、路径、命令或启动参数。 ").toString();
    }

    private String formatRemoteMqttService(JSONObject mqttService) {
        if (mqttService == null || !mqttService.optBoolean("available", false)) {
            return "MQTT 固定服务：签名状态暂不可用，远程重启已禁用。";
        }
        String status = mqttService.optString("status", "unknown");
        StringBuilder text = new StringBuilder("MQTT 固定服务：")
                .append(serviceStatusLabel(status));
        if (mqttService.optBoolean("maintenanceSupported", false)
                && ("running".equals(status) || "stopped".equals(status)
                || "paused".equals(status))) {
            text.append(" · 可受控重启");
        } else {
            text.append(" · 当前不提供远程重启");
        }
        return text.append("\n该状态独立于 ColorVision 消息连接和订阅状态。 ").toString();
    }

    private void refreshRemoteDashboard() {
        progress.setVisibility(View.VISIBLE);
        state.setText("正在刷新远程中继状态…");
        executor.execute(() -> {
            try {
                JSONObject response = relayClient.getSnapshot();
                runOnUiThread(() -> showRemoteDashboard(response));
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void confirmRemoteMinimizeWindow() {
        showTargetedConfirmation(
                "远程最小化电脑主窗口",
                "只会最小化已配对电脑上的 ColorVision 主窗口。请求由本机设备密钥签名，电脑核验后执行并返回签名收据。",
                "取消", "最小化", () -> runRemoteTask(
                        OperationsRelayPolicy.CAPABILITY_MINIMIZE_WINDOW, new JSONObject()));
    }

    private void confirmRemoteMessageChannelRecovery() {
        showTargetedConfirmation(
                "远程恢复电脑消息通道",
                "只会检查并恢复已配对电脑当前 ColorVision 的既有消息连接和订阅。不会修改地址、Topic、凭据或重启 Windows 服务；通道已健康时不会主动断开。",
                "取消", "确认恢复", () -> runRemoteTask(
                        OperationsRelayPolicy.CAPABILITY_RECOVER_MESSAGE_CHANNEL,
                        new JSONObject()));
    }

    private void confirmRemoteMqttRestart() {
        if (!canRestartRemoteMqttService(lastRelaySnapshotResponse)) {
            Toast.makeText(this,
                    "电脑签名状态尚未确认固定 MQTT 服务可安全重启",
                    Toast.LENGTH_LONG).show();
            return;
        }
        showTargetedConfirmation(
                "远程重启 MQTT 服务？",
                "只会通过已配对电脑的 ColorVisionServiceHost 重启固定的本机 Mosquitto 服务。消息与检测设备通信会短暂中断并自动恢复；不会选择服务、地址、Topic、命令、路径或参数。",
                "取消", "确认重启", () -> {
                    if (remoteRestartMqttButton != null) {
                        remoteRestartMqttButton.setEnabled(false);
                    }
                    runRemoteTask(OperationsRelayPolicy.CAPABILITY_RESTART_MQTT,
                            new JSONObject());
                });
    }

    private void runRemoteTask(String capabilityId, JSONObject payload) {
        if (!ensureOperationsClientTargetIsCurrent()) {
            return;
        }
        showingDashboardSummary = false;
        progress.setVisibility(View.VISIBLE);
        state.setText("正在签名并提交远程请求…");
        int generation = ++remoteTaskGeneration;
        executor.execute(() -> {
            try {
                submitAndPollRemoteTask(capabilityId, payload, generation);
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    if (isRemoteTaskGenerationActive(generation)) {
                        showTransientError(ex);
                    }
                });
            }
        });
    }

    private void runRemoteWindowSnapshotTask() {
        if (!canCaptureRemoteWindowSnapshot(lastRelaySnapshotResponse)) {
            Toast.makeText(this, "电脑签名状态已变化，请刷新远程状态后重试",
                    Toast.LENGTH_LONG).show();
            return;
        }
        showingDashboardSummary = false;
        progress.setVisibility(View.VISIBLE);
        state.setText("正在生成端到端加密身份并提交快照请求…");
        int generation = ++remoteTaskGeneration;
        executor.execute(() -> {
            try {
                OperationsE2eIdentity e2eIdentity = new OperationsE2eIdentity(
                        preferences.getOperationsHostId());
                JSONObject payload = OperationsRemoteWindowSnapshot.createRequestPayload(
                        e2eIdentity.getPublicKeySpki());
                submitAndPollRemoteTask(
                        OperationsRelayPolicy.CAPABILITY_CAPTURE_WINDOW_SNAPSHOT,
                        payload,
                        generation);
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    if (isRemoteTaskGenerationActive(generation)) {
                        showTransientError(ex);
                    }
                });
            }
        });
    }

    private void submitAndPollRemoteTask(
            String capabilityId, JSONObject payload, int generation) throws Exception {
        JSONObject created = relayClient.createTask(capabilityId, payload);
        String taskId = created.optString("taskId", "");
        String idempotencyKey = created.optString("requestIdempotencyKey", "");
        if (!OperationsRelayPolicy.isSafeIdentifier(taskId)
                || !OperationsRelayPolicy.isSafeIdentifier(idempotencyKey)) {
            throw new IllegalStateException("invalid_relay_task_response");
        }
        preferences.saveOperationsRelayTask(taskId, capabilityId, idempotencyKey);
        pollRemoteTask(taskId, capabilityId, idempotencyKey, generation);
    }

    private void refreshRecentRemoteTask() {
        String taskId = preferences.getOperationsRelayTaskId();
        String capabilityId = preferences.getOperationsRelayTaskCapability();
        String idempotencyKey = preferences.getOperationsRelayTaskIdempotencyKey();
        if (!OperationsRelayPolicy.isSafeIdentifier(taskId)
                || !OperationsRelayPolicy.isSafeIdentifier(idempotencyKey)) {
            Toast.makeText(this, "还没有远程请求记录", Toast.LENGTH_SHORT).show();
            return;
        }
        showingDashboardSummary = false;
        progress.setVisibility(View.VISIBLE);
        state.setText("正在读取最近远程请求…");
        int generation = ++remoteTaskGeneration;
        executor.execute(() -> {
            try {
                pollRemoteTask(taskId, capabilityId, idempotencyKey, generation);
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    if (isRemoteTaskGenerationActive(generation)) {
                        showTransientError(ex);
                    }
                });
            }
        });
    }

    private void pollRemoteTask(
            String taskId,
            String capabilityId,
            String idempotencyKey,
            int generation) throws Exception {
        JSONObject latest = null;
        JSONObject latestTask = null;
        String status = "queued";
        int maximumAttempts = OperationsRelayPolicy.remoteTaskPollingAttempts(capabilityId);
        for (int attempt = 0; attempt < maximumAttempts
                && isRemoteTaskGenerationActive(generation); attempt++) {
            latest = relayClient.getTask(taskId, idempotencyKey);
            JSONObject task = latest.optJSONObject("task");
            if (task == null) {
                throw new IllegalStateException("invalid_relay_task_response");
            }
            latestTask = task;
            status = effectiveRemoteTaskStatus(task);
            if (isRemoteTaskResultReady(status)) {
                break;
            }
            if (attempt < maximumAttempts - 1) {
                Thread.sleep(2_000L);
            }
        }
        String formattedResult = "";
        if (OperationsRelayPolicy.CAPABILITY_READ_FAILURE_EVIDENCE.equals(capabilityId)
                && ("completed".equals(status) || "failed".equals(status))) {
            JSONObject receipt = latestRemoteTaskReceipt(latestTask);
            JSONObject evidence = receipt == null ? null : receipt.optJSONObject("evidence");
            if ("completed".equals(status)) {
                OperationsFailureEvidence.Snapshot snapshot =
                        OperationsFailureEvidence.parseStrictReceipt(evidence);
                formattedResult = OperationsFailureEvidence.format(
                        snapshot, shortTime(snapshot.latestEvidenceAt));
            } else {
                OperationsFailureEvidence.validateStrictErrorReceipt(evidence);
            }
        }
        if (OperationsRelayPolicy.CAPABILITY_CAPTURE_WINDOW_SNAPSHOT.equals(capabilityId)
                && ("completed".equals(status) || "failed".equals(status))) {
            JSONObject receipt = latestRemoteTaskReceipt(latestTask);
            JSONObject evidence = receipt == null ? null : receipt.optJSONObject("evidence");
            if ("completed".equals(status)) {
                OperationsRemoteWindowSnapshot.Receipt snapshotReceipt =
                        OperationsRemoteWindowSnapshot.parseCompletedReceipt(
                                evidence, System.currentTimeMillis());
                downloadAndPreviewRemoteWindowSnapshot(
                        taskId, idempotencyKey, snapshotReceipt, generation);
                return;
            }
            OperationsRemoteWindowSnapshot.validateFailedReceipt(evidence);
        }
        String finalStatus = status;
        String finalFormattedResult = formattedResult;
        runOnUiThread(() -> {
            if (isRemoteTaskGenerationActive(generation)) {
                renderRemoteTaskStatus(capabilityId, finalStatus, finalFormattedResult);
            }
        });
    }

    private boolean isRemoteTaskGenerationActive(int generation) {
        return generation == remoteTaskGeneration && !isFinishing() && !isDestroyed();
    }

    private void downloadAndPreviewRemoteWindowSnapshot(
            String taskId,
            String idempotencyKey,
            OperationsRemoteWindowSnapshot.Receipt receipt,
            int generation) throws Exception {
        if (!OperationsE2eIdentity.isSupported()) {
            throw new UnsupportedOperationException("window_snapshot_e2e_requires_android_31");
        }
        String hostId = preferences.getOperationsHostId();
        String deviceId = preferences.getOrCreateDeviceId();
        OperationsE2eIdentity e2eIdentity = new OperationsE2eIdentity(hostId);
        String recipientPublicKeySpki = e2eIdentity.getPublicKeySpki();
        byte[] sealed = null;
        byte[] sharedSecret = null;
        byte[] plaintext = null;
        Bitmap bitmap = null;
        File file = null;
        boolean handedToUi = false;
        try {
            sealed = relayClient.downloadWindowSnapshot(
                    taskId, receipt.sealedBytes, receipt.sealedSha256);
            sharedSecret = e2eIdentity.deriveSharedSecret(
                    receipt.hostEphemeralPublicKeySpki);
            plaintext = OperationsRemoteWindowSnapshot.decrypt(
                    sealed,
                    sharedSecret,
                    receipt,
                    hostId,
                    deviceId,
                    taskId,
                    idempotencyKey,
                    recipientPublicKeySpki);

            BitmapFactory.Options bounds = new BitmapFactory.Options();
            bounds.inJustDecodeBounds = true;
            BitmapFactory.decodeByteArray(plaintext, 0, plaintext.length, bounds);
            if (!"image/jpeg".equalsIgnoreCase(bounds.outMimeType)
                    || bounds.outWidth < 1 || bounds.outHeight < 1
                    || bounds.outWidth > 1280 || bounds.outHeight > 1280) {
                throw new SecurityException("window_snapshot_dimensions_rejected");
            }
            bitmap = BitmapFactory.decodeByteArray(plaintext, 0, plaintext.length);
            if (bitmap == null
                    || bitmap.getWidth() != bounds.outWidth
                    || bitmap.getHeight() != bounds.outHeight) {
                throw new SecurityException("window_snapshot_format_rejected");
            }

            clearRemoteWindowSnapshotCache();
            File directory = new File(getCacheDir(), "diagnostic-share");
            if ((!directory.exists() && !directory.mkdirs()) || !directory.isDirectory()) {
                throw new IllegalStateException("window_snapshot_cache_unavailable");
            }
            file = new File(directory,
                    "ColorVision-remote-window-snapshot-" + taskId + ".jpg");
            try (FileOutputStream output = new FileOutputStream(file, false)) {
                output.write(plaintext);
                output.flush();
            }
            Uri uri = FileProvider.getUriForFile(
                    this, getPackageName() + ".fileprovider", file);

            boolean consumed = true;
            try {
                relayClient.consumeWindowSnapshot(taskId, receipt.sealedSha256);
            } catch (Exception ignored) {
                consumed = false;
            }
            Bitmap previewBitmap = bitmap;
            File previewFile = file;
            Uri previewUri = uri;
            boolean consumeConfirmed = consumed;
            int plaintextBytes = plaintext.length;
            runOnUiThread(() -> {
                if (!isRemoteTaskGenerationActive(generation)) {
                    previewBitmap.recycle();
                    previewFile.delete();
                    return;
                }
                showWindowSnapshotPreview(
                        previewBitmap, previewUri, plaintextBytes, true, consumeConfirmed);
            });
            handedToUi = true;
        } finally {
            if (sealed != null) {
                Arrays.fill(sealed, (byte) 0);
            }
            if (sharedSecret != null) {
                Arrays.fill(sharedSecret, (byte) 0);
            }
            if (plaintext != null) {
                Arrays.fill(plaintext, (byte) 0);
            }
            if (!handedToUi) {
                if (bitmap != null) {
                    bitmap.recycle();
                }
                if (file != null) {
                    file.delete();
                }
            }
        }
    }

    private void clearRemoteWindowSnapshotCache() {
        File directory = new File(getCacheDir(), "diagnostic-share");
        File[] files = directory.listFiles((parent, name) ->
                name.startsWith("ColorVision-remote-window-snapshot-")
                        && name.endsWith(".jpg"));
        if (files == null) {
            return;
        }
        for (File file : files) {
            file.delete();
        }
    }

    private void clearRemoteWindowSnapshotSecrets(String hostId) {
        if (OperationsRelayPolicy.isSafeIdentifier(hostId)) {
            try {
                new OperationsE2eIdentity(hostId).delete();
            } catch (Exception ignored) {
            }
        }
        clearRemoteWindowSnapshotCache();
    }

    private String effectiveRemoteTaskStatus(JSONObject task) {
        JSONObject latest = latestRemoteTaskReceipt(task);
        if (latest != null) {
            String receiptStatus = latest.optString("status", "");
            if (!receiptStatus.isEmpty()) {
                return receiptStatus;
            }
        }
        return "queued";
    }

    private JSONObject latestRemoteTaskReceipt(JSONObject task) {
        JSONArray receipts = task == null ? null : task.optJSONArray("receipts");
        return receipts == null || receipts.length() == 0
                ? null : receipts.optJSONObject(receipts.length() - 1);
    }

    private boolean isRemoteTaskResultReady(String status) {
        return "completed".equals(status)
                || "failed".equals(status)
                || "rejected".equals(status)
                || "expired".equals(status)
                || "awaiting_local_consent".equals(status);
    }

    private void renderRemoteTaskStatus(
            String capabilityId, String status, String formattedResult) {
        progress.setVisibility(View.GONE);
        OperationsRemoteTaskPresentation.Presentation presentation =
                OperationsRemoteTaskPresentation.create(capabilityId, status, formattedResult);
        if (presentation.clearFlowCancelAvailability) {
            dashboardFlowCancelAvailable = false;
            updateDashboardCancelFlowAction();
        }
        state.setText(presentation.state);
        details.setText(presentation.details);
        scheduleConnectionHeartbeat();
    }

    private void showCurrentDashboard() {
        scrollDashboardToTop();
        if (remoteDashboard && lastRelaySnapshotResponse != null) {
            showRemoteDashboard(lastRelaySnapshotResponse);
        } else {
            showDashboard();
        }
    }

    private boolean restorePendingDestination(boolean directConnectionAvailable) {
        String destination = pendingRestoredDestination;
        pendingRestoredDestination = "";
        if (!OperationsDestinationState.shouldRestore(destination)) {
            return false;
        }
        if (OperationsDestinationState.requiresDirectConnection(destination)
                && !directConnectionAvailable) {
            return false;
        }
        dashboardVisible = true;
        switch (destination) {
            case OperationsDestinationState.CONNECTIONS:
                showConnectionPreference();
                return true;
            case OperationsDestinationState.CONNECTION_CHECK:
                runConnectionSelfCheck();
                return true;
            case OperationsDestinationState.HISTORY:
                showOperationsWatchHistory();
                return true;
            case OperationsDestinationState.FLEET_ALL:
                showFleetTimeline(false);
                return true;
            case OperationsDestinationState.FLEET_ISSUES:
                showFleetTimeline(true);
                return true;
            case OperationsDestinationState.TRIAGE:
                showTriageCenter();
                return true;
            case OperationsDestinationState.JOBS:
                showJobs();
                return true;
            case OperationsDestinationState.SUPPORT:
                showSupportCenter();
                return true;
            case OperationsDestinationState.LIVE_MONITOR:
                showLiveMonitor();
                return true;
            default:
                return false;
        }
    }

    private boolean contains(JSONArray values, String expected) {
        if (values == null) {
            return false;
        }
        for (int index = 0; index < values.length(); index++) {
            if (expected.equals(values.optString(index))) {
                return true;
            }
        }
        return false;
    }

    private boolean openPendingOperationsDestination() {
        String destination = pendingOperationsDestination;
        if (destination.isEmpty() || client == null) {
            return false;
        }
        pendingOperationsDestination = "";
        if (OperationsWatchPolicy.DESTINATION_TRIAGE.equals(destination)) {
            showTriageCenter();
            return true;
        }
        if (OperationsWatchPolicy.DESTINATION_CONNECTION_CHECK.equals(destination)) {
            runConnectionSelfCheck();
            return true;
        }
        return false;
    }

    private void showOperationsWatchHistory() {
        currentDestination = OperationsDestinationState.HISTORY;
        scrollDashboardToTop();
        showingDashboardSummary = false;
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = true;
        progress.setVisibility(View.GONE);
        title.setText("运维时间线");
        List<OperationsWatchHistory.Entry> entries = preferences.getOperationsWatchHistory(
                System.currentTimeMillis());
        state.setText(entries.isEmpty()
                ? "还没有状态变更"
                : OperationsWatchHistory.label(entries.get(entries.size() - 1).state));
        String timeline = formatOperationsWatchHistory(entries);
        details.setText(timeline);
        actions.removeAllViews();

        Button refresh = new MaterialButton(this);
        refresh.setText("刷新本机时间线");
        refresh.setOnClickListener(v -> showOperationsWatchHistory());
        actions.addView(refresh, actionParams());

        Button share = new MaterialButton(this);
        share.setText("分享脱敏时间线");
        share.setEnabled(!entries.isEmpty());
        share.setOnClickListener(v -> shareSafeText(
                "ColorVision 运维时间线",
                "ColorVision 运维时间线\n\n" + timeline));
        actions.addView(share, actionParams());

        Button back = new MaterialButton(this);
        back.setText("返回现场运维概览");
        back.setOnClickListener(v -> showCurrentDashboard());
        actions.addView(back, actionParams());
    }

    private void showFleetTimeline(boolean issuesOnly) {
        currentDestination = issuesOnly
                ? OperationsDestinationState.FLEET_ISSUES
                : OperationsDestinationState.FLEET_ALL;
        scrollDashboardToTop();
        showingDashboardSummary = false;
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = true;
        progress.setVisibility(View.GONE);
        title.setText("全部电脑动态");
        OperationsFleetTimeline.Timeline timeline = OperationsFleetTimeline.build(
                preferences.getOperationsProfiles(),
                preferences.getOperationsHostId(),
                System.currentTimeMillis(),
                issuesOnly);
        state.setText(timeline.summary);
        details.setText("合并手机已有的近七天固定状态变化，不发起网络刷新。两分钟内恢复的短时连接波动会自动折叠，持续故障仍会保留。电脑名称只由本机档案补充，不写入状态记录，也不会发送给电脑或固定中继；本页不提供批量分享。");
        actions.removeAllViews();

        String issueFilterLabel = "只看需关注"
                + (timeline.issueEntryCount > 0 ? "（" + timeline.issueEntryCount + "）" : "");
        addDashboardSegmentedChoices(
                "全部变化",
                issueFilterLabel,
                issuesOnly,
                v -> showFleetTimeline(false),
                v -> showFleetTimeline(true));
        addDashboardActionRow(
                dashboardButton("重新读取本机动态", v -> showFleetTimeline(issuesOnly)),
                dashboardButton("返回电脑总览", v -> showConnectionPreference()));
        addDashboardSection("状态变化");
        TextView timelineBody = new TextView(this);
        timelineBody.setText(formatFleetTimeline(timeline));
        TextViewCompat.setTextAppearance(timelineBody, com.google.android.material.R.style.TextAppearance_Material3_BodyMedium);
        timelineBody.setTextColor(themeManager.primaryTextColor());
        timelineBody.setLineSpacing(0, 1.08f);
        timelineBody.setPadding(dp(16), dp(12), dp(16), dp(12));
        timelineBody.setTextIsSelectable(true);
        MaterialCardView timelineCard = new MaterialCardView(this);
        timelineCard.addView(timelineBody, new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT));
        LinearLayout.LayoutParams timelineParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        timelineParams.setMargins(0, 0, 0, dp(8));
        actions.addView(timelineCard, timelineParams);
    }

    private String formatFleetTimeline(OperationsFleetTimeline.Timeline timeline) {
        StringBuilder text = new StringBuilder();
        if (timeline.entries.isEmpty()) {
            text.append(timeline.issuesOnly
                    ? "最近七天没有需关注的电脑状态变化。"
                    : "后台守护或只读巡检尚未记录电脑状态变化。");
        } else {
            text.append(timeline.issuesOnly
                    ? "按时间倒序显示需关注变化"
                    : "按时间倒序显示全部状态变化");
            if (timeline.truncated()) {
                text.append(" · 最近 ").append(timeline.entries.size())
                        .append(" / ").append(timeline.matchingEntryCount).append(" 条");
            }
            SimpleDateFormat formatter = new SimpleDateFormat(
                    "MM-dd HH:mm:ss", Locale.getDefault());
            for (OperationsFleetTimeline.Entry entry : timeline.entries) {
                text.append("\n\n")
                        .append(formatter.format(new Date(entry.timestampMilliseconds)))
                        .append(" · ").append(entry.profileLabel)
                        .append("\n").append(OperationsWatchHistory.label(entry.state));
            }
        }
        return text.toString();
    }

    private String formatOperationsWatchHistory(List<OperationsWatchHistory.Entry> entries) {
        if (entries.isEmpty()) {
            return "后台守护只会在连接、恢复或需要关注的聚合状态确实变化时记录一条。";
        }
        SimpleDateFormat formatter = new SimpleDateFormat("MM-dd HH:mm:ss", Locale.getDefault());
        StringBuilder text = new StringBuilder();
        text.append("近 7 天状态变更 · 短时连接波动已合并 · 本机最多 40 条");
        for (int index = entries.size() - 1; index >= 0; index--) {
            OperationsWatchHistory.Entry entry = entries.get(index);
            text.append("\n")
                    .append(formatter.format(new Date(entry.timestampMilliseconds)))
                    .append(" · ")
                    .append(OperationsWatchHistory.label(entry.state));
        }
        text.append("\n\n仅保存时间与固定状态类别；不保存主机、端点、设备身份、告警正文、日志或检测数据。移除配对资料时一并清除。");
        return text.toString();
    }

    private void ensureOperationsWatchRunning() {
        OperationsWatchService.start(this);
    }

    private boolean requestDashboardRefresh() {
        OperationsDashboardRefreshPolicy.Decision decision = OperationsDashboardRefreshPolicy.decide(
                activityResumed,
                dashboardVisible,
                showingDashboardSummary,
                preferences != null && preferences.hasOperationsProfile(),
                client != null || relayClient != null,
                connectionHeartbeatInFlight);
        if (decision == OperationsDashboardRefreshPolicy.Decision.REJECT) {
            dashboardRefresh.setRefreshing(false);
            return false;
        }

        manualDashboardRefresh = true;
        dashboardRefresh.setRefreshing(true);
        dashboardRefresh.announceForAccessibility("正在刷新运维状态");
        if (decision == OperationsDashboardRefreshPolicy.Decision.START) {
            connectionHeartbeatHandler.removeCallbacks(connectionHeartbeat);
            runConnectionHeartbeat();
        }
        return true;
    }

    private void finishDashboardRefresh(String message) {
        boolean showResult = manualDashboardRefresh;
        manualDashboardRefresh = false;
        if (dashboardRefresh != null) {
            dashboardRefresh.setRefreshing(false);
        }
        if (showResult && activityResumed && dashboardRefresh != null
                && message != null && !message.isEmpty()) {
            Snackbar snackbar = Snackbar.make(dashboardRefresh, message, Snackbar.LENGTH_SHORT);
            if (bottomNavigation != null) {
                snackbar.setAnchorView(bottomNavigation);
            }
            snackbar.show();
        }
    }

    private void cancelDashboardRefresh() {
        manualDashboardRefresh = false;
        if (dashboardRefresh != null) {
            dashboardRefresh.setRefreshing(false);
        }
    }

    private void scheduleConnectionHeartbeat() {
        connectionHeartbeatHandler.removeCallbacks(connectionHeartbeat);
        if (OperationsConnectionRecoveryPolicy.shouldSchedule(
                activityResumed,
                dashboardVisible,
                showingDashboardSummary,
                connectionRecoveryVisible,
                client != null || relayClient != null)) {
            connectionHeartbeatHandler.postDelayed(connectionHeartbeat, CONNECTION_HEARTBEAT_MILLISECONDS);
        }
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        acceptOperationsDestination(intent);
        if (!remoteDashboard && client != null && preferences.hasOperationsProfile()) {
            openPendingOperationsDestination();
        }
    }

    private void acceptOperationsDestination(Intent intent) {
        if (intent == null) {
            return;
        }
        String destination = OperationsWatchPolicy.normalizeDestination(
                intent.getStringExtra(EXTRA_OPEN_DESTINATION));
        intent.removeExtra(EXTRA_OPEN_DESTINATION);
        if (!destination.isEmpty()) {
            pendingOperationsDestination = destination;
        }
    }

    private void runConnectionHeartbeat() {
        if (!OperationsConnectionRecoveryPolicy.shouldStart(
                activityResumed,
                dashboardVisible,
                showingDashboardSummary,
                connectionRecoveryVisible,
                client != null || relayClient != null,
                connectionHeartbeatInFlight)) {
            return;
        }
        connectionHeartbeatInFlight = true;
        if (connectionRecoveryVisible) {
            state.setText(OperationsRecoveryOverview.checkingStatus());
        }
        boolean relayFirst = OperationsConnectionPreference.prefersRelay(
                preferences.getOperationsConnectionPreference());
        int requestGeneration = connectionRequestGeneration;
        String requestHostId = operationsClientHostId;
        executor.execute(() -> {
            if (relayFirst) {
                try {
                    if (relayClient == null) {
                        throw new IllegalStateException("relay_client_unavailable");
                    }
                    JSONObject response = relayClient.getSnapshot();
                    postHeartbeatResult(requestGeneration, requestHostId,
                            () -> applyRelayHeartbeat(response));
                } catch (Exception relayException) {
                    if (isRevokedException(relayException)) {
                        postHeartbeatResult(requestGeneration, requestHostId,
                                this::completeHeartbeatRevoked);
                        return;
                    }
                    try {
                        if (client == null) {
                            throw new IllegalStateException("local_operations_client_unavailable");
                        }
                        JSONObject response = client.get("/ops/v1/monitor");
                        JSONObject snapshot = response.optJSONObject("data");
                        postHeartbeatResult(requestGeneration, requestHostId,
                                () -> applyLocalHeartbeat(snapshot));
                    } catch (Exception localException) {
                        if (isRevokedException(localException)) {
                            postHeartbeatResult(requestGeneration, requestHostId,
                                    this::completeHeartbeatRevoked);
                        } else {
                            postHeartbeatResult(requestGeneration, requestHostId,
                                    () -> completeHeartbeatFailure(true));
                        }
                    }
                }
                return;
            }

            try {
                if (client == null) {
                    throw new IllegalStateException("local_operations_client_unavailable");
                }
                JSONObject response = client.get("/ops/v1/monitor");
                JSONObject snapshot = response.optJSONObject("data");
                postHeartbeatResult(requestGeneration, requestHostId,
                        () -> applyLocalHeartbeat(snapshot));
            } catch (Exception localException) {
                if (isRevokedException(localException)) {
                    postHeartbeatResult(requestGeneration, requestHostId,
                            this::completeHeartbeatRevoked);
                    return;
                }
                try {
                    if (relayClient == null) {
                        throw new IllegalStateException("relay_client_unavailable");
                    }
                    JSONObject response = relayClient.getSnapshot();
                    postHeartbeatResult(requestGeneration, requestHostId,
                            () -> applyRelayHeartbeat(response));
                } catch (Exception relayException) {
                    if (isRevokedException(relayException)) {
                        postHeartbeatResult(requestGeneration, requestHostId,
                                this::completeHeartbeatRevoked);
                    } else {
                        postHeartbeatResult(requestGeneration, requestHostId,
                                () -> completeHeartbeatFailure(false));
                    }
                }
            }
        });
    }

    private void postHeartbeatResult(
            int requestGeneration, String requestHostId, Runnable result) {
        runOnUiThread(() -> {
            if (requestGeneration != connectionRequestGeneration
                    || isFinishing() || isDestroyed()) {
                return;
            }
            if (!OperationsTargetPolicy.isSameTarget(
                    requestHostId, preferences.getOperationsHostId())) {
                reconnectAfterOperationsTargetChange();
                return;
            }
            if (!activityResumed
                    || (!showingDashboardSummary && !connectionRecoveryVisible)) {
                connectionHeartbeatInFlight = false;
                return;
            }
            result.run();
        });
    }

    private void applyLocalHeartbeat(JSONObject snapshot) {
        connectionHeartbeatInFlight = false;
        if (connectionRecoveryVisible) {
            showDashboard();
            finishDashboardRefresh(OperationsDashboardRefreshPolicy.completionMessage(
                    true, false, true));
            return;
        }
        if (remoteDashboard) {
            showDashboard();
            finishDashboardRefresh(OperationsDashboardRefreshPolicy.completionMessage(
                    true, false, true));
            return;
        }
        if (showingDashboardSummary) {
            state.setText(directConnectionState());
        }
        if (snapshot != null) {
            updateDashboardLiveStatus(snapshot);
        }
        finishDashboardRefresh(OperationsDashboardRefreshPolicy.completionMessage(
                true, false, true));
        scheduleConnectionHeartbeat();
    }

    private void applyRelayHeartbeat(JSONObject response) {
        connectionHeartbeatInFlight = false;
        JSONObject host = response.optJSONObject("host");
        boolean hostFresh = host != null && OperationsRelayPolicy.isHostFresh(
                host.optLong("signedAt", 0L), System.currentTimeMillis());
        if (!remoteDashboard) {
            showRemoteDashboard(response);
            finishDashboardRefresh(OperationsDashboardRefreshPolicy.completionMessage(
                    true, true, hostFresh));
            return;
        }
        boolean rebuildForFreshness = OperationsRelayPolicy.shouldRebuildDashboardForFreshness(
                showingDashboardSummary, dashboardRemoteHostFresh, hostFresh);
        lastRelaySnapshotResponse = response;
        if (showingDashboardSummary) {
            if (rebuildForFreshness) {
                showRemoteDashboard(response);
                finishDashboardRefresh(OperationsDashboardRefreshPolicy.completionMessage(
                        true, true, hostFresh));
                return;
            }
            updateRemoteDashboardStatus(response);
            progress.setVisibility(View.GONE);
        }
        finishDashboardRefresh(OperationsDashboardRefreshPolicy.completionMessage(
                true, true, hostFresh));
        scheduleConnectionHeartbeat();
    }

    private void completeHeartbeatFailure(boolean relayPreferred) {
        connectionHeartbeatInFlight = false;
        if (connectionRecoveryVisible) {
            state.setText(OperationsRecoveryOverview.waitingStatus());
        } else if (showingDashboardSummary) {
            state.setText(relayPreferred
                    ? "○ 固定中继暂断 · 现场直连也不可达"
                    : "○ 现场直连暂断 · 固定中继也不可达");
        }
        finishDashboardRefresh(OperationsDashboardRefreshPolicy.completionMessage(
                false, false, false));
        scheduleConnectionHeartbeat();
    }

    private void completeHeartbeatRevoked() {
        connectionHeartbeatInFlight = false;
        cancelDashboardRefresh();
        showRevokedProfile();
    }

    private void showRevokedProfile() {
        connectionRequestGeneration++;
        String revokedHostId = preferences.getOperationsHostId();
        clearRemoteWindowSnapshotSecrets(revokedHostId);
        preferences.markOperationsProfileRevoked(revokedHostId);
        refreshOperationsTargetPresentation();
        OperationsWatchService.stopForProfileRemoval(this);
        showError("配对授权已失效", "这台电脑已撤销设备授权；其他已配对电脑不会受影响。",
                () -> removeOperationsProfile(revokedHostId));
    }

    private static boolean isRevokedException(Exception exception) {
        return !canFallbackAfter(exception);
    }

    private static boolean canFallbackAfter(Exception exception) {
        return OperationsConnectionPreference.canFallbackAfter(
                exception == null ? null : exception.getMessage());
    }

    private String directConnectionState() {
        return OperationsDashboardOverview.directConnectionState(
                OperationsConnectionPreference.prefersRelay(
                        preferences.getOperationsConnectionPreference()))
                + "\n" + monitoringSummary();
    }

    private String remoteConnectionState(boolean hostFresh) {
        return OperationsDashboardOverview.remoteConnectionState(
                hostFresh,
                OperationsConnectionPreference.prefersRelay(
                        preferences.getOperationsConnectionPreference()))
                + "\n" + monitoringSummary();
    }

    private String monitoringSummary() {
        return OperationsDashboardOverview.monitoringSummary(
                preferences.isOperationsWatchUserEnabled(), attentionRemindersAvailable());
    }

    private boolean attentionRemindersAvailable() {
        return NotificationPermissionPolicy.canPostAttention(
                Build.VERSION.SDK_INT,
                NotificationPermissionState.hasRuntimePermission(this),
                NotificationPermissionState.appNotificationsEnabled(this),
                NotificationPermissionState.attentionChannelEnabled(this));
    }

    private TextView addDashboardSection(String label) {
        TextView heading = new TextView(this);
        heading.setText(label);
        TextViewCompat.setTextAppearance(heading, com.google.android.material.R.style.TextAppearance_Material3_TitleMedium);
        heading.setTextColor(themeManager.primaryTextColor());
        heading.setPadding(dp(4), dp(12), 0, dp(8));
        actions.addView(heading, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));
        return heading;
    }

    private Button dashboardButton(String label, View.OnClickListener listener) {
        Button button = new MaterialButton(this, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        button.setText(label);
        button.setTextSize(13);
        button.setAllCaps(false);
        button.setOnClickListener(listener);
        return button;
    }

    private Button dashboardPrimaryButton(String label, View.OnClickListener listener) {
        Button button = new MaterialButton(this);
        button.setText(label);
        button.setTextSize(13);
        button.setAllCaps(false);
        button.setOnClickListener(listener);
        return button;
    }

    private Button dashboardTonalButton(String label, View.OnClickListener listener) {
        Button button = new MaterialButton(
                this, null, com.google.android.material.R.attr.materialButtonTonalStyle);
        button.setText(label);
        button.setTextSize(13);
        button.setAllCaps(false);
        button.setOnClickListener(listener);
        return button;
    }

    private Button dashboardDestructiveButton(String label, View.OnClickListener listener) {
        MaterialButton button = (MaterialButton) dashboardButton(label, listener);
        int error = themeManager.errorColor();
        button.setTextColor(error);
        button.setStrokeColor(ColorStateList.valueOf(error));
        return button;
    }

    private DashboardStatusRow dashboardStatusRow(
            String title, View.OnClickListener listener) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setMinimumHeight(dp(64));
        row.setPadding(dp(16), dp(8), dp(12), dp(8));
        row.setOnClickListener(listener);
        row.setClickable(true);
        row.setFocusable(true);
        TypedValue selectableBackground = new TypedValue();
        if (getTheme().resolveAttribute(
                android.R.attr.selectableItemBackground, selectableBackground, true)) {
            row.setBackgroundResource(selectableBackground.resourceId);
        }

        LinearLayout text = new LinearLayout(this);
        text.setOrientation(LinearLayout.VERTICAL);
        TextView titleView = new TextView(this);
        titleView.setText(title);
        TextViewCompat.setTextAppearance(titleView,
                com.google.android.material.R.style.TextAppearance_Material3_LabelMedium);
        titleView.setTextColor(themeManager.secondaryTextColor());
        titleView.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        text.addView(titleView, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));

        TextView summaryView = new TextView(this);
        TextViewCompat.setTextAppearance(summaryView,
                com.google.android.material.R.style.TextAppearance_Material3_BodyLarge);
        summaryView.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        text.addView(summaryView, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));
        row.addView(text, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));

        ImageView arrow = new ImageView(this);
        arrow.setImageResource(R.drawable.ic_chevron_right_24);
        arrow.setColorFilter(themeManager.secondaryTextColor());
        arrow.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        row.addView(arrow, new LinearLayout.LayoutParams(dp(24), dp(24)));

        DashboardStatusRow result = new DashboardStatusRow(row, titleView, summaryView);
        updateDashboardStatus(result, OperationsDashboardStatusFormatter.loading(title));
        return result;
    }

    private TextView addDashboardStatusCard(
            String caption, DashboardStatusRow... statusRows) {
        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);

        TextView captionView = new TextView(this);
        captionView.setText(caption);
        TextViewCompat.setTextAppearance(captionView,
                com.google.android.material.R.style.TextAppearance_Material3_BodySmall);
        captionView.setTextColor(themeManager.secondaryTextColor());
        captionView.setPadding(dp(16), dp(12), dp(16), dp(10));
        content.addView(captionView, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));

        for (int index = 0; index < statusRows.length; index++) {
            content.addView(statusRows[index].container, new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT));
            if (index < statusRows.length - 1) {
                View divider = new View(this);
                divider.setBackgroundColor(themeManager.dividerColor());
                LinearLayout.LayoutParams dividerParams = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MATCH_PARENT, 1);
                dividerParams.setMargins(dp(16), 0, 0, 0);
                content.addView(divider, dividerParams);
            }
        }

        MaterialCardView card = new MaterialCardView(this);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(content, new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT));
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        params.setMargins(0, 0, 0, dp(8));
        actions.addView(card, params);
        return captionView;
    }

    private void updateDashboardStatus(
            DashboardStatusRow row, OperationsDashboardStatusFormatter.Item item) {
        if (row == null || item == null) {
            return;
        }
        row.title.setText(item.title);
        row.summary.setText(item.summary);
        row.summary.setTextColor(dashboardStatusColor(item.tone));
        row.container.setContentDescription(item.accessibilityLabel());
    }

    private int dashboardStatusColor(int tone) {
        if (tone == OperationsDashboardStatusFormatter.TONE_ACTIVE) {
            return themeManager.primaryColor();
        }
        if (tone == OperationsDashboardStatusFormatter.TONE_ATTENTION) {
            return themeManager.errorColor();
        }
        if (tone == OperationsDashboardStatusFormatter.TONE_MUTED) {
            return themeManager.secondaryTextColor();
        }
        return themeManager.primaryTextColor();
    }

    private void updateDashboardPriority(OperationsDashboardAdvisor.Recommendation recommendation) {
        if (dashboardPriorityAction == null || recommendation == null) {
            return;
        }
        dashboardPriorityAction.setText(recommendation.label);
        dashboardPriorityAction.setEnabled(
                !OperationsDashboardAdvisor.ACTION_NONE.equals(recommendation.action));
        dashboardPriorityAction.setOnClickListener(v -> runDashboardPriorityAction(
                recommendation.action));
    }

    private void runDashboardPriorityAction(String action) {
        switch (action) {
            case OperationsDashboardAdvisor.ACTION_CONNECTION_CHECK:
                runConnectionSelfCheck();
                return;
            case OperationsDashboardAdvisor.ACTION_FLOW:
                if (remoteDashboard) {
                    showLatestRemoteMonitorDetail("flow");
                } else {
                    showLiveMonitor();
                }
                return;
            case OperationsDashboardAdvisor.ACTION_DEVICES:
                openDashboardMonitorDetail("devices", "/ops/v1/devices/health");
                return;
            case OperationsDashboardAdvisor.ACTION_MESSAGE:
                openDashboardMonitorDetail("message", "/ops/v1/messaging/health");
                return;
            case OperationsDashboardAdvisor.ACTION_ALERTS:
                openDashboardMonitorDetail("alerts", "/ops/v1/alerts");
                return;
            case OperationsDashboardAdvisor.ACTION_PERFORMANCE:
                openDashboardMonitorDetail("performance", "/ops/v1/diagnostics/performance");
                return;
            case OperationsDashboardAdvisor.ACTION_MONITOR:
                if (remoteDashboard) {
                    showLatestRemoteMonitorDetail("flow");
                } else {
                    showLiveMonitor();
                }
                return;
            case OperationsDashboardAdvisor.ACTION_NOTIFICATION_SETTINGS:
                openMainTab(MainActivity.TAB_SETTINGS);
                return;
            default:
                return;
        }
    }

    private void openDashboardMonitorDetail(String remoteSection, String directPath) {
        if (remoteDashboard) {
            showLatestRemoteMonitorDetail(remoteSection);
        } else if ("/ops/v1/devices/health".equals(directPath)) {
            showDeviceHealthOverview();
        } else {
            loadCapability(directPath);
        }
    }

    private void loadDashboardLiveStatus() {
        executor.execute(() -> {
            try {
                JSONObject response = client.get("/ops/v1/monitor");
                JSONObject snapshot = response.optJSONObject("data");
                if (snapshot != null) {
                    runOnUiThread(() -> updateDashboardLiveStatus(snapshot));
                }
            } catch (Exception ignored) {
                runOnUiThread(this::markDashboardLiveStatusUnavailable);
            }
        });
    }

    private void updateDashboardLiveStatus(JSONObject snapshot) {
        if (dashboardFlowStatus == null) {
            return;
        }
        JSONObject flow = snapshot.optJSONObject("flow");
        JSONObject devices = snapshot.optJSONObject("devices");
        JSONObject messageChannel = snapshot.optJSONObject("messageChannel");
        JSONObject alerts = snapshot.optJSONObject("alerts");
        JSONObject performance = snapshot.optJSONObject("performance");
        JSONObject mainUi = performance == null ? null : performance.optJSONObject("mainUi");
        JSONObject recovery = snapshot.optJSONObject("applicationRecovery");

        dashboardFlowAvailable = flow != null && flow.optBoolean("available", false);
        dashboardFlowActive = flow != null && flow.optBoolean("isActive", false);
        dashboardFlowCancelAvailable = dashboardFlowAvailable
                && dashboardFlowActive
                && flow.optBoolean("cancelAvailable", false)
                && dashboardFlowCancelCapabilityAvailable;

        updateDashboardStatus(dashboardFlowStatus, OperationsDashboardStatusFormatter.flow(
                dashboardFlowAvailable,
                dashboardFlowActive,
                flow == null ? "idle" : flow.optString("phase", "idle")));
        updateDashboardStatus(dashboardDeviceStatus, OperationsDashboardStatusFormatter.devices(
                devices != null && devices.optBoolean("available", false),
                devices != null && devices.optBoolean("hasConfiguredDevices", false),
                devices == null ? 0 : devices.optInt("readyCount", 0),
                devices == null ? 0 : devices.optInt("busyCount", 0),
                devices == null ? 0 : devices.optInt("attentionCount", 0),
                devices == null ? 0 : devices.optInt("totalCount", 0)));
        updateDashboardStatus(dashboardMessageStatus, OperationsDashboardStatusFormatter.messageChannel(
                messageChannel != null && messageChannel.optBoolean("available", false),
                messageChannel != null && messageChannel.optBoolean("connected", false),
                messageChannel != null && messageChannel.optBoolean("subscriptionReady", false),
                messageChannel == null ? 0 : messageChannel.optInt("activeSubscriptionCount", 0),
                messageChannel == null ? 0 : messageChannel.optInt("registeredSubscriptionCount", 0)));
        updateDashboardStatus(dashboardAlertStatus, OperationsDashboardStatusFormatter.alerts(
                alerts != null,
                alerts == null ? 0 : alerts.optInt("warningCount", 0),
                alerts == null ? 0 : alerts.optInt("errorCount", 0),
                alerts == null ? 0 : alerts.optInt("criticalCount", 0)));
        updateDashboardStatus(dashboardPerformanceStatus, OperationsDashboardStatusFormatter.performance(
                performance != null,
                performance == null ? 0 : performance.optDouble("cpuPercent", 0),
                mainUi == null ? "unavailable" : mainUi.optString("state", "unavailable")));
        updateDashboardStatus(dashboardRecoveryStatus, OperationsDashboardStatusFormatter.recovery(
                recovery != null,
                recovery != null && recovery.optBoolean("supported", false),
                recovery != null && recovery.optBoolean("registered", false),
                recovery != null && recovery.optBoolean("automaticWatchdogActive", false)));
        updateDashboardPriority(remoteDashboard && !dashboardRemoteHostFresh
                ? OperationsDashboardAdvisor.staleRemoteSnapshot()
                : OperationsDashboardAdvisor.fromMonitor(
                        snapshot, attentionRemindersAvailable()));
        updateDashboardCancelFlowAction();
        updateDashboardRestartApplicationAction();
    }

    private void markDashboardLiveStatusUnavailable() {
        if (dashboardFlowStatus == null) {
            return;
        }
        updateDashboardStatus(dashboardFlowStatus,
                OperationsDashboardStatusFormatter.flow(false, false, "idle"));
        updateDashboardStatus(dashboardDeviceStatus,
                OperationsDashboardStatusFormatter.devices(false, false, 0, 0, 0, 0));
        updateDashboardStatus(dashboardMessageStatus,
                OperationsDashboardStatusFormatter.messageChannel(false, false, false, 0, 0));
        updateDashboardStatus(dashboardAlertStatus,
                OperationsDashboardStatusFormatter.unavailable("告警"));
        updateDashboardStatus(dashboardPerformanceStatus,
                OperationsDashboardStatusFormatter.performance(false, 0, "unavailable"));
        updateDashboardStatus(dashboardRecoveryStatus,
                OperationsDashboardStatusFormatter.unavailable("恢复"));
        dashboardFlowAvailable = false;
        dashboardFlowActive = false;
        dashboardFlowCancelAvailable = false;
        updateDashboardPriority(remoteDashboard && !dashboardRemoteHostFresh
                ? OperationsDashboardAdvisor.staleRemoteSnapshot()
                : OperationsDashboardAdvisor.unavailable());
        updateDashboardCancelFlowAction();
        updateDashboardRestartApplicationAction();
    }

    private void clearDashboardLiveStatusReferences() {
        dashboardFlowStatus = null;
        dashboardDeviceStatus = null;
        dashboardMessageStatus = null;
        dashboardAlertStatus = null;
        dashboardPerformanceStatus = null;
        dashboardRecoveryStatus = null;
        dashboardPriorityAction = null;
        dashboardCancelFlowButton = null;
        dashboardRestartApplicationButton = null;
        remoteRestartMqttButton = null;
        dashboardStatusHeading = null;
        dashboardStatusCaption = null;
        dashboardFlowAvailable = false;
        dashboardFlowActive = false;
        dashboardFlowCancelAvailable = false;
        dashboardFlowCancelCapabilityAvailable = false;
        dashboardRestartCapabilityAvailable = false;
        dashboardRemoteHostFresh = false;
    }

    private void updateDashboardCancelFlowAction() {
        if (dashboardCancelFlowButton == null) {
            return;
        }
        dashboardCancelFlowButton.setText(OperationsDashboardStatusFormatter.flowCancellation(
                dashboardFlowAvailable,
                dashboardFlowActive,
                dashboardFlowCancelAvailable,
                liveMonitorCancelInFlight));
        dashboardCancelFlowButton.setEnabled(OperationsDashboardStatusFormatter.flowCancellationEnabled(
                dashboardFlowAvailable,
                dashboardFlowActive,
                dashboardFlowCancelAvailable,
                liveMonitorCancelInFlight));
    }

    private void updateDashboardRestartApplicationAction() {
        if (dashboardRestartApplicationButton == null) {
            return;
        }
        dashboardRestartApplicationButton.setEnabled(
                dashboardRestartCapabilityAvailable
                        && dashboardRemoteHostFresh
                        && dashboardFlowAvailable
                        && !dashboardFlowActive);
    }

    private Button capabilityButton(String label, String path) {
        return dashboardButton(label, v -> loadCapability(path));
    }

    private void scrollDashboardToTop() {
        if (dashboardScroll != null) {
            dashboardScroll.post(() -> dashboardScroll.scrollTo(0, 0));
        }
    }

    private void addDashboardActionRow(Button left, Button right) {
        actions.addView(createDashboardActionRow(left, right), new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));
    }

    private LinearLayout createDashboardActionRow(Button left, Button right) {
        LinearLayout row = new LinearLayout(this);
        boolean singleColumn = AppResponsiveLayout.usesSingleColumn(
                getResources().getConfiguration().fontScale);
        row.setOrientation(singleColumn ? LinearLayout.VERTICAL : LinearLayout.HORIZONTAL);

        LinearLayout.LayoutParams leftParams = singleColumn
                ? new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MATCH_PARENT, dp(48))
                : new LinearLayout.LayoutParams(0, dp(48), 1);
        leftParams.setMargins(0, 0, singleColumn ? 0 : dp(4), dp(4));
        row.addView(left, leftParams);

        LinearLayout.LayoutParams rightParams = singleColumn
                ? new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MATCH_PARENT, dp(48))
                : new LinearLayout.LayoutParams(0, dp(48), 1);
        rightParams.setMargins(singleColumn ? 0 : dp(4), 0, 0, dp(4));
        row.addView(right, rightParams);
        return row;
    }

    private void addDashboardTaskGroup(
            String heading, String supportingText, Button primaryAction, Button secondaryAction) {
        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(dp(16), dp(12), dp(16), dp(8));

        TextView headingView = new TextView(this);
        headingView.setText(heading);
        TextViewCompat.setTextAppearance(headingView,
                com.google.android.material.R.style.TextAppearance_Material3_TitleSmall);
        headingView.setTextColor(themeManager.primaryTextColor());
        content.addView(headingView, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));

        TextView supportingView = new TextView(this);
        supportingView.setText(supportingText);
        TextViewCompat.setTextAppearance(supportingView,
                com.google.android.material.R.style.TextAppearance_Material3_BodySmall);
        supportingView.setTextColor(themeManager.secondaryTextColor());
        supportingView.setPadding(0, dp(2), 0, dp(8));
        content.addView(supportingView, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));
        content.addView(createDashboardActionRow(primaryAction, secondaryAction),
                new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MATCH_PARENT,
                        LinearLayout.LayoutParams.WRAP_CONTENT));

        MaterialCardView card = new MaterialCardView(this);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(content, new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT));
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        params.setMargins(0, 0, 0, dp(8));
        actions.addView(card, params);
    }

    private void addDashboardSegmentedChoices(
            String leftLabel,
            String rightLabel,
            boolean rightSelected,
            View.OnClickListener leftListener,
            View.OnClickListener rightListener) {
        MaterialButtonToggleGroup group = new MaterialButtonToggleGroup(this);
        group.setSingleSelection(true);
        group.setSelectionRequired(true);
        MaterialButton left = segmentedButton(leftLabel);
        MaterialButton right = segmentedButton(rightLabel);
        group.addView(left, new LinearLayout.LayoutParams(0, dp(48), 1));
        group.addView(right, new LinearLayout.LayoutParams(0, dp(48), 1));
        group.check(rightSelected ? right.getId() : left.getId());
        group.addOnButtonCheckedListener((buttonGroup, checkedId, isChecked) -> {
            if (!isChecked) {
                return;
            }
            if (checkedId == left.getId()) {
                leftListener.onClick(left);
            } else if (checkedId == right.getId()) {
                rightListener.onClick(right);
            }
        });
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        params.setMargins(0, 0, 0, dp(4));
        actions.addView(group, params);
    }

    private MaterialButton segmentedButton(String label) {
        MaterialButton button = new MaterialButton(
                this, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        button.setId(View.generateViewId());
        button.setText(label);
        button.setTextSize(13);
        button.setAllCaps(false);
        button.setMaxLines(1);
        button.setEllipsize(TextUtils.TruncateAt.END);
        return button;
    }

    private TextView addDashboardInfoCard(String value) {
        TextView note = new TextView(this);
        note.setText(value);
        TextViewCompat.setTextAppearance(note,
                com.google.android.material.R.style.TextAppearance_Material3_BodyMedium);
        note.setTextColor(themeManager.secondaryTextColor());
        note.setLineSpacing(0, 1.08f);
        note.setPadding(dp(16), dp(14), dp(16), dp(14));
        MaterialCardView card = new MaterialCardView(this);
        card.setCardBackgroundColor(themeManager.cardBackgroundColor());
        card.addView(note, new MaterialCardView.LayoutParams(
                MaterialCardView.LayoutParams.MATCH_PARENT,
                MaterialCardView.LayoutParams.WRAP_CONTENT));
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        params.setMargins(0, 0, 0, dp(8));
        actions.addView(card, params);
        return note;
    }

    private void addOperationsProfileWideAction(View button) {
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(66));
        params.setMargins(0, 0, 0, dp(4));
        actions.addView(button, params);
    }

    private void addDashboardWideAction(Button button) {
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(48));
        params.setMargins(0, 0, 0, dp(4));
        actions.addView(button, params);
    }

    private void showExistingProfileFailure(Exception localException, Exception relayException) {
        scrollDashboardToTop();
        leaveSupportCenter();
        remoteDashboard = false;
        if (restorePendingDestination(false)) {
            return;
        }
        currentDestination = OperationsDestinationState.OVERVIEW;
        dashboardVisible = true;
        showingDashboardSummary = false;
        connectionRecoveryVisible = true;
        progress.setVisibility(View.GONE);
        title.setText("连接恢复");
        state.setText(OperationsRecoveryOverview.waitingStatus());
        details.setText(OperationsRecoveryOverview.failureSummary(
                OperationsErrorPresentation.readable(localException),
                OperationsErrorPresentation.readable(relayException)));
        showConnectionRecoveryActions();
        scheduleConnectionHeartbeat();
    }

    private void runConnectionSelfCheck() {
        currentDestination = OperationsDestinationState.CONNECTION_CHECK;
        int checkGeneration = ++connectionCheckGeneration;
        showingDashboardSummary = false;
        connectionRecoveryVisible = false;
        scrollDashboardToTop();
        refreshOperationsTargetPresentation();
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = true;
        progress.setVisibility(View.VISIBLE);
        title.setText("连接自检");
        state.setText("正在检查安全连接…");
        details.setText(OperationsConnectionCheckPresentation.runningDescription());
        actions.removeAllViews();
        addDashboardSection("检查说明");
        addDashboardInfoCard("本次检查只读取连接状态，不会修改电脑、网络设置或配对资料。");
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
                result = new OperationsConnectionCheck.Result(
                        false,
                        "无法启动连接自检",
                        "请稍后重试；若持续失败，请返回连接方式确认当前电脑和配对资料。",
                        "启动自检：" + OperationsErrorPresentation.readable(ex));
            }
            OperationsConnectionCheck.Result finalResult = result;
            runOnUiThread(() -> {
                if (checkGeneration != connectionCheckGeneration
                        || isFinishing() || isDestroyed()) {
                    return;
                }
                progress.setVisibility(View.GONE);
                showConnectionCheckResult(finalResult, false);
            });
        });
    }

    private void showConnectionCheckResult(
            OperationsConnectionCheck.Result result, boolean detailsExpanded) {
        currentDestination = OperationsDestinationState.CONNECTION_CHECK;
        title.setText("连接自检");
        state.setText(OperationsConnectionCheckPresentation.status(result.success, result.heading));
        details.setText(result.recommendation);
        actions.removeAllViews();

        addDashboardSection("下一步");
        addDashboardWideAction(dashboardPrimaryButton(
                result.success ? "进入现场运维" : "再次运行连接自检",
                result.success ? v -> openExistingProfile() : v -> runConnectionSelfCheck()));
        addDashboardActionRow(
                dashboardButton(result.success ? "再次运行连接自检" : "重新连接电脑",
                        result.success ? v -> runConnectionSelfCheck() : v -> openExistingProfile()),
                dashboardButton("返回连接方式", v -> showConnectionPreference()));

        addDashboardSection("诊断详情");
        addDashboardInfoCard(OperationsConnectionCheckPresentation.diagnosticSummary(
                result.completedCheckCount));
        addDashboardWideAction(dashboardButton(
                OperationsConnectionCheckPresentation.detailsAction(
                        result.completedCheckCount, detailsExpanded),
                v -> showConnectionCheckResult(result, !detailsExpanded)));
        if (detailsExpanded) {
            TextView technicalDetails = addDashboardInfoCard(result.technicalDetails);
            technicalDetails.setTextIsSelectable(true);
        }
    }

    private void showConnectionRecoveryActions() {
        actions.removeAllViews();

        addDashboardSection("恢复连接");
        addDashboardWideAction(dashboardPrimaryButton(
                "立即重试",
                v -> openExistingProfile()));
        addDashboardInfoCard(OperationsRecoveryOverview.automaticRetryNote());

        addDashboardSection("需要排查");
        addDashboardActionRow(
                dashboardButton("运行连接自检", v -> runConnectionSelfCheck()),
                dashboardButton("管理连接方式", v -> showConnectionPreference()));
    }

    private void confirmClearProfile() {
        String hostId = preferences.getOperationsHostId();
        new MaterialAlertDialogBuilder(this)
                .setTitle("移除本机配对资料")
                .setMessage("仅删除当前电脑在手机中的设备密钥、证书指纹和端点记录。其他已配对电脑不受影响；电脑端的设备授权仍需单独撤销。")
                .setNegativeButton("取消", null)
                .setPositiveButton("确认移除", (dialog, which) -> removeOperationsProfile(hostId))
                .show();
    }

    private void confirmRemoveOperationsProfile(String hostId, String label) {
        new MaterialAlertDialogBuilder(this)
                .setTitle("移除" + label)
                .setMessage("将删除这台电脑在手机中的独立密钥、证书指纹、时间线和最近任务。其他电脑不受影响。")
                .setNegativeButton("取消", null)
                .setPositiveButton("确认移除",
                        (dialog, which) -> removeOperationsProfile(hostId))
                .show();
    }

    private void confirmMinimizeWindow() {
        showTargetedConfirmation(
                "最小化电脑主窗口",
                "该操作会立即最小化已连接电脑上的 ColorVision 主窗口，并写入运维审计。",
                "取消", "最小化", () -> runWindowAction("minimize", "主窗口已最小化"));
    }

    private void runWindowAction(String action, String successText) {
        if (!ensureOperationsClientTargetIsCurrent()) {
            return;
        }
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
                    details.setText(getString(
                            R.string.operations_window_action_audit_details, message));
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void showTriageCenter() {
        currentDestination = OperationsDestinationState.TRIAGE;
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
        OperationsTriagePresentation.ViewModel model =
                OperationsTriagePresentation.from(report, this::shortTime);
        scrollDashboardToTop();
        progress.setVisibility(View.GONE);
        title.setText("远程排障中心");
        state.setText(model.stateLabel);
        details.setText(model.summary);
        actions.removeAllViews();
        actions.addView(OperationsTriageContent.create(
                        this,
                        themeManager,
                        model,
                        this::runTriageAction,
                        this::showTriageCenter,
                        this::showDashboard),
                new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MATCH_PARENT,
                        LinearLayout.LayoutParams.WRAP_CONTENT));
    }

    private void runTriageAction(String actionId) {
        switch (actionId) {
            case "triage.events.view":
                loadCapability("/ops/v1/diagnostics/recent-events");
                return;
            case "triage.window.show":
                runWindowAction("show", "主窗口已显示");
                return;
            case "triage.jobs.review":
                showJobs();
                return;
            case "triage.mqtt.restart.request":
                confirmRestartMqtt();
                return;
            case "triage.devices.view":
                showDeviceHealthOverview();
                return;
            case "triage.messaging.view":
                loadCapability("/ops/v1/messaging/health");
                return;
            case "triage.messaging.reconnect.request":
                confirmRecoverMessageChannel();
                return;
            case "triage.failures.view":
                loadCapability("/ops/v1/diagnostics/failures");
                return;
            default:
                return;
        }
    }

    private void showJobs() {
        currentDestination = OperationsDestinationState.JOBS;
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
            Button download = new MaterialButton(this);
            download.setText("下载并分享安全诊断包");
            String jobId = downloadableDiagnostic.optString("jobId", "");
            download.setOnClickListener(v -> confirmDiagnosticBundleDownload(jobId));
            actions.addView(download, actionParams());
        }
        if (downloadableWindowSnapshot != null) {
            Button preview = new MaterialButton(this);
            preview.setText("下载并预览主窗口快照（单次）");
            String jobId = downloadableWindowSnapshot.optString("jobId", "");
            preview.setOnClickListener(v -> confirmWindowSnapshotDownload(jobId));
            actions.addView(preview, actionParams());
        }
        Button refresh = new MaterialButton(this);
        refresh.setText("刷新作业状态");
        refresh.setOnClickListener(v -> showJobs());
        actions.addView(refresh, actionParams());

        Button back = new MaterialButton(this);
        back.setText("返回现场运维概览");
        back.setOnClickListener(v -> showDashboard());
        actions.addView(back, actionParams());
    }

    private void addApprovalActions(JSONObject job) {
        Button approve = new MaterialButton(this);
        approve.setText("approved_mobile".equals(job.optString("status"))
                ? "继续执行已批准作业" : "确认并批准此作业");
        approve.setOnClickListener(v -> confirmJobApproval(job));
        actions.addView(approve, actionParams());
        if (!"approved_mobile".equals(job.optString("status"))) {
            Button reject = new MaterialButton(this);
            reject.setText("拒绝此作业");
            reject.setOnClickListener(v -> confirmJobRejection(
                    job.optString("jobId", ""), job.optString("title", "现场运维作业")));
            actions.addView(reject, actionParams());
        }
    }

    private void confirmJobRejection(String jobId, String jobTitle) {
        if (jobId.isEmpty()) {
            Toast.makeText(this, "作业标识无效", Toast.LENGTH_LONG).show();
            return;
        }
        showTargetedConfirmation(
                "拒绝作业：" + jobTitle,
                "拒绝后该作业不会由这台手机批准；结果会写入去标识运维审计。",
                "返回", "确认拒绝", () -> decideJob(jobId, false));
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
        showTargetedConfirmation(
                "确认批准：" + title,
                "作业能力：" + target
                        + (requiresLocalCoSign
                        ? "\n\n批准只记录这台已配对手机的明确意图，不会立即执行。电脑端仍需本机人员再次确认；未共签前作业保持阻塞。"
                        : "\n\n这是固定、无参数的远程动作。确认后会立即执行并写入审计，不需要电脑端再次共签。"),
                "取消", "确认批准", () -> decideJob(jobId, true));
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
        showTargetedConfirmation(
                "生成并分享诊断包",
                "确认后电脑端立即生成脱敏 ZIP，手机会校验 SHA-256 并打开系统分享面板，不再等待电脑端共签。包内只含有界运行状态、脱敏事件、白名单服务健康和去标识审计，不含凭据、用户名、机器名、设备 ID、用户文档、数据库或图像；仅本申请设备可在 24 小时内下载。",
                "取消", "确认生成", this::createDiagnosticJob);
    }

    private void confirmDiagnosticBundleDownload(String jobId) {
        if (jobId.isEmpty()) {
            Toast.makeText(this, "诊断作业标识无效", Toast.LENGTH_LONG).show();
            return;
        }
        showTargetedConfirmation(
                "下载安全诊断包",
                "仅下载当前设备已明确确认生成的脱敏 ZIP。下载内容会先校验 SHA-256，再交给你选择的应用；不要转发到不受信任的位置。",
                "取消", "下载并分享", () -> downloadAndShareDiagnosticBundle(jobId));
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
        details.setText(getString(
                R.string.operations_diagnostic_bundle_ready_details,
                Math.max(1, Math.round(sizeBytes / 1024f))));
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
        showTargetedConfirmation(
                "采集并预览主窗口快照",
                "确认后会先显示或还原 ColorVision 主窗口，再立即采集一张 JPEG；手机会校验后预览，不再等待电脑端共签。不会捕获整个桌面，也不会连续录屏；画面可能包含当前可见的检测数据。仅本申请设备可在 5 分钟内读取一次，读取后电脑端立即销毁。",
                "取消", "确认采集", this::createWindowSnapshotJob);
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
        showTargetedConfirmation(
                "读取一次主窗口快照",
                "将下载当前设备已明确确认采集的 ColorVision 主窗口 JPEG。SHA-256 校验通过后，电脑端证据立即销毁；应用先在本机预览，只有你再次点击分享才会交给其他应用。",
                "取消", "下载并预览", () -> downloadWindowSnapshot(jobId));
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
                runOnUiThread(() -> showWindowSnapshotPreview(
                        bitmap, uri, data.length, false, true));
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void showWindowSnapshotPreview(
            Bitmap bitmap,
            Uri uri,
            int sizeBytes,
            boolean remote,
            boolean consumeConfirmed) {
        progress.setVisibility(View.GONE);
        title.setText("主窗口安全快照");
        state.setText(remote
                ? "端到端加密快照已校验并预览"
                : "一次性证据已校验并从电脑端销毁");
        StringBuilder previewDetails = new StringBuilder("已读取 ")
                .append(Math.max(1, Math.round(sizeBytes / 1024f)))
                .append(" KiB JPEG，仅包含采集时的 ColorVision 主窗口。当前预览副本位于 Android 应用缓存；请确认画面后再决定是否分享。");
        if (remote) {
            previewDetails.append("\n\n图片在电脑端加密后才进入固定站点；固定站点只接触短时密文。");
            previewDetails.append(consumeConfirmed
                    ? " 手机已提交签名消费确认；固定站点按协议删除密文，并始终受 5 分钟有效期约束。"
                    : " 密文消费确认暂未送达，但不影响当前已验证预览；固定站点会在 5 分钟有效期结束时自动清理。");
        }
        details.setText(previewDetails.toString());
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

        Button share = new MaterialButton(this);
        share.setText("分享这张主窗口快照");
        share.setOnClickListener(v -> shareWindowSnapshot(uri));
        actions.addView(share, actionParams());

        Button back = new MaterialButton(this);
        back.setText(remote ? "返回远程告警" : "返回作业与审批");
        back.setOnClickListener(v -> {
            if (remote) {
                showLatestRemoteMonitorDetail("alerts");
            } else {
                showJobs();
            }
        });
        actions.addView(back, actionParams());
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
        showTargetedConfirmation(
                "确认重启 MQTT 服务",
                "确认后将立即通过 ColorVisionServiceHost 重启固定白名单中的 Mosquitto 服务，消息与设备通信可能短暂中断后自动恢复。手机不能选择其他服务、命令、路径或参数。",
                "取消", "确认重启", this::restartMqtt);
    }

    private void restartMqtt() {
        progress.setVisibility(View.VISIBLE);
        state.setText(R.string.operations_mqtt_restarting);
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

    private void confirmRecoverMessageChannel() {
        showTargetedConfirmation(
                "确认恢复消息通道",
                "只在 ColorVision 消息客户端断开或订阅未就绪时，使用电脑当前已有配置重建连接并恢复已登记订阅。健康通道不会断开；手机不能填写地址、端口、Topic、凭据或其他参数。",
                "取消", "确认恢复", this::recoverMessageChannel);
    }

    private void recoverMessageChannel() {
        progress.setVisibility(View.VISIBLE);
        state.setText(R.string.operations_message_channel_recovering);
        executor.execute(() -> {
            try {
                JSONObject job = createAndApproveJob(
                        "ops.messaging.reconnect", "现场消息通道恢复", new JSONObject(),
                        "message_channel_recovery_job_missing", "已配对手机明确确认恢复当前消息通道");
                String status = job.optString("status", "");
                if (!"completed".equals(status)) {
                    throw new IllegalStateException("message_channel_recovery_failed");
                }
                runOnUiThread(() -> {
                    Toast.makeText(this, "消息通道已就绪", Toast.LENGTH_LONG).show();
                    loadCapability("/ops/v1/messaging/health");
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
            }
        });
    }

    private void confirmRestartApplication() {
        showTargetedConfirmation(
                "确认重启 ColorVision",
                remoteDashboard
                        ? "确认后会通过设备签名中继重启当前 ColorVision。电脑先复核检测为空闲并返回已受理回执，新进程重新上线后再返回最终回执；不会选择程序、路径、命令或启动参数。"
                        : "确认后只会干净重启当前 ColorVision 应用，不会选择程序、路径、命令或启动参数。正在执行检测时电脑端会拒绝；重启期间会短暂断线，应用将保留配对资料并自动等待恢复。",
                "取消", "确认重启", () -> {
                    if (remoteDashboard) {
                        runRemoteTask(
                                OperationsRelayPolicy.CAPABILITY_RESTART_APPLICATION,
                                new JSONObject());
                    } else {
                        restartApplication();
                    }
                });
    }

    private void restartApplication() {
        progress.setVisibility(View.VISIBLE);
        title.setText(R.string.operations_restart_title);
        state.setText(R.string.operations_restart_checking);
        details.setText(R.string.operations_restart_disconnect_note);
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
        title.setText(R.string.operations_restart_failed_title);
        state.setText(OperationsErrorPresentation.readable(ex));
        details.setText(R.string.operations_restart_failed_details);
        actions.removeAllViews();

        Button reconnect = new MaterialButton(this);
        reconnect.setText("重新连接运维通道");
        reconnect.setOnClickListener(v -> openExistingProfile());
        actions.addView(reconnect, actionParams());

        Button jobs = new MaterialButton(this);
        jobs.setText("查看作业时间线");
        jobs.setOnClickListener(v -> showJobs());
        actions.addView(jobs, actionParams());

        Button selfCheck = new MaterialButton(this);
        selfCheck.setText("运行连接自检");
        selfCheck.setOnClickListener(v -> runConnectionSelfCheck());
        actions.addView(selfCheck, actionParams());
    }

    private void confirmDeploymentReceipt() {
        showTargetedConfirmation(
                "提交部署确认",
                "仅提交本移动伴侣当前版本的验证收据，不会触发远程部署。",
                "取消", "确认", this::submitDeploymentReceipt);
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
        currentDestination = OperationsDestinationState.SUPPORT;
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
            state.setText(R.string.operations_support_empty_state);
            details.setText(getString(
                    R.string.operations_support_empty_details,
                    sessionsData.optString("privacyNotice", "请勿发送密码、密钥或客户数据。")));
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

        Button refresh = new MaterialButton(this);
        refresh.setText("立即刷新会话");
        refresh.setOnClickListener(v -> loadSupportCenter(true));
        actions.addView(refresh, actionParams());

        Button back = new MaterialButton(this);
        back.setText("返回现场运维概览");
        back.setOnClickListener(v -> showDashboard());
        actions.addView(back, actionParams());
        scheduleSupportRefresh();
    }

    private void addSupportRequestButton() {
        Button request = new MaterialButton(this);
        request.setText(R.string.operations_support_request);
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

        Button send = new MaterialButton(this);
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
        currentDestination = OperationsDestinationState.LIVE_MONITOR;
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
        title.setText(R.string.operations_live_monitor_title);
        state.setText(R.string.operations_live_monitor_loading);
        details.setText(R.string.operations_live_monitor_details);
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
                            ? R.string.operations_live_monitor_retrying
                            : R.string.operations_live_monitor_failed);
                    details.setText(getString(
                            R.string.operations_live_monitor_failure_details,
                            OperationsErrorPresentation.readable(ex)));
                    renderLiveMonitorActions();
                    scheduleLiveMonitorRefresh();
                });
            }
        });
    }

    private void renderLiveMonitorActions() {
        actions.removeAllViews();

        Button refresh = new MaterialButton(this);
        refresh.setText("立即刷新");
        refresh.setEnabled(!liveMonitorRefreshInFlight && !liveMonitorCancelInFlight);
        refresh.setOnClickListener(v -> loadLiveMonitor(true));
        actions.addView(refresh, actionParams());

        Button toggle = new MaterialButton(this);
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

        Button cancelFlow = new MaterialButton(this);
        cancelFlow.setText(liveMonitorCancelAvailable
                ? "取消当前检测"
                : "当前没有可取消的主检测");
        cancelFlow.setEnabled(liveMonitorCancelAvailable
                && !liveMonitorRefreshInFlight
                && !liveMonitorCancelInFlight);
        cancelFlow.setOnClickListener(v -> confirmCancelCurrentFlow());
        actions.addView(cancelFlow, actionParams());

        Button share = new MaterialButton(this);
        share.setText(liveMonitorTrend.size() < 2
                ? "分享本次趋势（至少需要 2 个样本）"
                : "分享本次脱敏趋势");
        share.setEnabled(liveMonitorTrend.size() >= 2);
        share.setOnClickListener(v -> shareLiveMonitorTrend());
        actions.addView(share, actionParams());

        Button back = new MaterialButton(this);
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
        boolean cancelAvailable = liveMonitorVisible
                ? liveMonitorCancelAvailable
                : showingDashboardSummary && dashboardFlowCancelAvailable;
        if (!cancelAvailable) {
            Toast.makeText(this, "当前没有可取消的主检测", Toast.LENGTH_LONG).show();
            return;
        }
        showTargetedConfirmation(
                "取消当前检测？",
                remoteDashboard
                        ? "只会向已配对电脑当前主工作区正在执行的检测发送取消请求，不会选择、启动或修改其他流程，也不接受远程参数。请求由本机设备密钥签名，电脑核验后执行并返回签名收据。"
                        : "只会向当前主工作区正在执行的检测发送取消请求，不会选择、启动或修改其他流程，也不接受远程参数。确认后立即执行并记录审计。",
                "继续观察", "确认取消检测", () -> {
                    if (remoteDashboard) {
                        runRemoteTask(OperationsRelayPolicy.CAPABILITY_CANCEL_FLOW, new JSONObject());
                    } else {
                        requestCancelCurrentFlow();
                    }
                });
    }

    private void requestCancelCurrentFlow() {
        boolean requestedFromLiveMonitor = liveMonitorVisible;
        boolean requestedFromDashboard = !requestedFromLiveMonitor && showingDashboardSummary;
        liveMonitorRefreshHandler.removeCallbacks(liveMonitorRefresh);
        liveMonitorCancelInFlight = true;
        progress.setVisibility(View.VISIBLE);
        state.setText("正在提交并确认取消请求…");
        if (requestedFromLiveMonitor) {
            renderLiveMonitorActions();
        } else {
            updateDashboardCancelFlowAction();
        }
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
                    dashboardFlowCancelAvailable = false;
                    liveMonitorCancelInFlight = false;
                    Toast.makeText(this,
                            "completed".equals(status)
                                    ? "已向当前检测发送取消请求"
                                    : "当前检测未取消，已保留审计结果",
                            Toast.LENGTH_LONG).show();
                    if (requestedFromLiveMonitor && liveMonitorVisible) {
                        loadLiveMonitor(true);
                    } else if (requestedFromDashboard && showingDashboardSummary) {
                        showDashboard();
                    }
                });
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    liveMonitorCancelAvailable = false;
                    dashboardFlowCancelAvailable = false;
                    liveMonitorCancelInFlight = false;
                    if (requestedFromLiveMonitor && liveMonitorVisible) {
                        showTransientError(ex);
                        scheduleLiveMonitorRefresh();
                    } else if (requestedFromDashboard && showingDashboardSummary) {
                        showTransientError(ex);
                        updateDashboardCancelFlowAction();
                    } else {
                        Toast.makeText(this, "取消请求未完成", Toast.LENGTH_LONG).show();
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
        JSONObject applicationRecovery = snapshot.optJSONObject("applicationRecovery");
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

        text.append("\n\n应用异常恢复：");
        if (applicationRecovery == null || !applicationRecovery.optBoolean("supported", false)) {
            text.append("当前系统不支持");
        } else if (!applicationRecovery.optBoolean("registered", false)) {
            text.append("未就绪");
        } else if (applicationRecovery.optBoolean("restartedAfterFailure", false)) {
            text.append("已恢复 · 本次启动由固定目标看门狗或 Windows 异常恢复接管");
        } else if (applicationRecovery.optBoolean("automaticWatchdogActive", false)) {
            text.append("已就绪 · 本机看门狗只会自动恢复同目录 ColorVision");
        } else {
            text.append("已登记 · Windows 可在当前 ColorVision 异常退出或卡死后提供恢复");
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
                .append("\n页面刷新：前台每 ")
                .append(snapshot.optInt("suggestedRefreshSeconds", 10)).append(" 秒")
                .append(" · 后台守护每 60 秒，断线自动退避")
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
        showTargetedConfirmation(
                "申请引导支持",
                "申请 15 分钟有限文本会话。电脑端必须本机同意；不开放远程桌面、命令或文件。",
                "取消", "提交申请", this::submitSupportRequest);
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
        details.setText(OperationsErrorPresentation.readable(ex));
    }

    private void addAction(String label, String path) {
        Button button = new MaterialButton(this);
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
                    details.setText(OperationsErrorPresentation.readable(ex));
                });
            }
        });
    }

    private void showDeviceHealthOverview() {
        showingDashboardSummary = false;
        leaveSupportCenter();
        leaveLiveMonitor();
        progress.setVisibility(View.VISIBLE);
        executor.execute(() -> {
            try {
                JSONObject response = client.get("/ops/v1/devices/health");
                JSONObject data = response.optJSONObject("data");
                JSONObject payload = data == null ? response : data;
                DeviceHealthPresentation.ViewModel model =
                        DeviceHealthPresentation.from(payload);
                String observedAt = shortTime(model.observedAt);
                runOnUiThread(() -> {
                    progress.setVisibility(View.GONE);
                    DeviceHealthBottomSheet.show(
                            this,
                            themeManager,
                            model,
                            observedAt,
                            this::showDeviceHealthOverview,
                            this::showTriageCenter);
                });
            } catch (Exception ex) {
                runOnUiThread(() -> showTransientError(ex));
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
        if ("/ops/v1/diagnostics/failures".equals(path)) {
            return formatFailureEvidence(payload);
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
                .append(" · 运行 ").append(formatDuration(uptimeSeconds))
                .append(" · 窗口 ").append(windowVisible ? "可见" : windowExists ? "未显示" : "不可用")
                .append('/').append(windowState);
        if (memoryMb > 0) {
            summary.append(" · 内存 ").append(Math.round(memoryMb * 10) / 10.0).append(" MB");
        }
        summary.append("\n后台连接 ").append(secureRunning ? "持续" : "恢复中")
                .append(" · 已配对 ").append(pairedDevices).append(" 台");
        if (relayRunning || relayConfigured) {
            summary.append(" · 中继 ").append(relayRunning ? "运行中" : "未启动");
        }
        return summary.toString();
    }

    private String capabilityHeading(String path) {
        if ("/ops/v1/snapshot".equals(path)) {
            return directConnectionState();
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
        if ("/ops/v1/diagnostics/failures".equals(path)) {
            return "崩溃与卡死线索已刷新";
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
            case "diagnostics.failure-evidence.read": return "读取崩溃与卡死线索";
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
            case "message-channel-recovery-receipt": return "消息通道恢复回执";
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

    private String formatFailureEvidence(JSONObject payload) {
        OperationsFailureEvidence.Snapshot snapshot =
                OperationsFailureEvidence.fromLocalPayload(payload);
        return OperationsFailureEvidence.format(
                snapshot, shortTime(snapshot.latestEvidenceAt));
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

    private void removeOperationsProfile(String hostId) {
        leaveSupportCenter();
        leaveLiveMonitor();
        OperationsWatchService.stopForProfileRemoval(this);
        resetOperationsClientsForProfileChange();
        dashboardVisible = false;
        clearRemoteWindowSnapshotSecrets(hostId);
        try {
            if (!hostId.isEmpty()) {
                new OperationsDeviceIdentity(hostId).delete();
            }
        } catch (Exception ignored) {
        }
        preferences.removeOperationsProfile(hostId);
        if (preferences.hasOperationsProfile()) {
            OperationsWatchService.start(this);
            Toast.makeText(this, "这台电脑的本机配对资料已移除，已切换到下一台电脑",
                    Toast.LENGTH_LONG).show();
            openExistingProfile();
        } else {
            Toast.makeText(this, "本机配对资料已移除；电脑端仍可单独撤销设备",
                    Toast.LENGTH_LONG).show();
            finish();
        }
    }

    private void setBusy(String message) {
        cancelDashboardRefresh();
        connectionRecoveryVisible = false;
        stopPairingApprovalWait();
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = false;
        state.setText(message);
        details.setText(R.string.operations_keystore_busy_details);
        progress.setIndeterminate(true);
        progress.setVisibility(View.VISIBLE);
        actions.removeAllViews();
    }

    private void showError(String heading, String message, Runnable recovery) {
        cancelDashboardRefresh();
        connectionRecoveryVisible = false;
        stopPairingApprovalWait();
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = false;
        progress.setVisibility(View.GONE);
        title.setText(heading);
        state.setText(message);
        details.setText("请确认手机与电脑位于同一可信局域网，并重新扫描电脑端短时配对码。\n不会回退到 URL token。 ");
        actions.removeAllViews();
        if (recovery != null) {
            Button button = new MaterialButton(this);
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

    @Override
    protected void onPause() {
        activityResumed = false;
        cancelDashboardRefresh();
        pairingApprovalHandler.removeCallbacks(pairingApprovalTick);
        connectionHeartbeatHandler.removeCallbacks(connectionHeartbeat);
        supportRefreshHandler.removeCallbacks(supportRefresh);
        liveMonitorRefreshHandler.removeCallbacks(liveMonitorRefresh);
        super.onPause();
    }

    @Override
    protected void onRestart() {
        super.onRestart();
        selectOperationsTab();
    }

    private void selectOperationsTab() {
        if (bottomNavigation != null) {
            bottomNavigation.setSelectedItemId(NAV_OPERATIONS);
        }
    }

    private void showPairingFailure(String reason) {
        cancelDashboardRefresh();
        connectionRecoveryVisible = false;
        stopPairingApprovalWait();
        leaveSupportCenter();
        leaveLiveMonitor();
        dashboardVisible = false;
        progress.setVisibility(View.GONE);
        title.setText(PairingFailurePresentation.title(reason));
        state.setText(PairingFailurePresentation.message(reason));
        boolean hasExistingProfile = preferences.hasOperationsProfile();
        details.setText(PairingFailurePresentation.preservationNote(hasExistingProfile));
        actions.removeAllViews();

        Button retry = new MaterialButton(this);
        retry.setText("重新扫描");
        retry.setOnClickListener(v -> startOperationsPairingScan());
        actions.addView(retry, actionParams());

        Button secondary = new MaterialButton(
                this, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        secondary.setText(PairingFailurePresentation.secondaryAction(hasExistingProfile));
        secondary.setOnClickListener(v -> {
            if (hasExistingProfile) {
                openExistingProfile();
            } else {
                openMainTab(MainActivity.TAB_SETTINGS);
            }
        });
        actions.addView(secondary, actionParams());
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        outState.putString(STATE_DESTINATION,
                OperationsDestinationState.normalize(currentDestination));
        super.onSaveInstanceState(outState);
    }

    @Override
    protected void onResume() {
        super.onResume();
        selectOperationsTab();
        activityResumed = true;
        refreshOperationsTargetPresentation();
        if (preferences != null && preferences.hasOperationsProfile()) {
            OperationsWatchService.start(this);
        }
        if (state != null && showingDashboardSummary) {
            state.setText(remoteDashboard
                    ? remoteConnectionState(dashboardRemoteHostFresh)
                    : directConnectionState());
        }
        if (supportCenterVisible) {
            scheduleSupportRefresh();
        }
        if (liveMonitorVisible && liveMonitorAutoRefresh) {
            liveMonitorRefreshHandler.post(liveMonitorRefresh);
        }
        if (pairingApprovalWaiting) {
            pairingApprovalHandler.post(pairingApprovalTick);
        }
        scheduleConnectionHeartbeat();
    }

    @Override
    protected void onDestroy() {
        pairingRequestGeneration++;
        connectionRequestGeneration++;
        remoteTaskGeneration++;
        fleetCheckGeneration++;
        connectionHeartbeatHandler.removeCallbacks(connectionHeartbeat);
        pairingApprovalHandler.removeCallbacks(pairingApprovalTick);
        leaveSupportCenter();
        leaveLiveMonitor();
        executor.shutdownNow();
        fleetExecutor.shutdownNow();
        super.onDestroy();
    }
}

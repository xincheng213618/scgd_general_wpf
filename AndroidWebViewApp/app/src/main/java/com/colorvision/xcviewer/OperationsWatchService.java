package com.colorvision.xcviewer;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.net.NetworkRequest;
import android.net.Uri;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;

import androidx.core.app.NotificationCompat;
import androidx.core.app.ServiceCompat;
import androidx.core.content.ContextCompat;

import org.json.JSONObject;

import java.io.File;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class OperationsWatchService extends Service {
    private static final String ACTION_START =
            "com.colorvision.xcviewer.action.START_OPERATIONS_WATCH";
    private static final String ACTION_REFRESH_CONNECTION =
            "com.colorvision.xcviewer.action.REFRESH_OPERATIONS_CONNECTION";
    private static final String NOTIFICATION_CHANNEL_ID = "operations_watch";
    static final String ATTENTION_CHANNEL_ID = "operations_attention";
    private static final int NOTIFICATION_ID = 22023;
    private static final int LEGACY_ATTENTION_NOTIFICATION_ID = 22024;
    private static final int ATTENTION_NOTIFICATION_ID = 22024;
    private static final int REMINDER_TEST_NOTIFICATION_ID = 22025;
    private static final int FLEET_CONNECT_TIMEOUT_MILLISECONDS = 4_000;
    private static final int FLEET_READ_TIMEOUT_MILLISECONDS = 6_000;
    private static final String LOG_TAG = "CVOperationsWatch";

    private final Handler handler = new Handler(Looper.getMainLooper());
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final Runnable scheduledCheck = this::runCheck;
    private AppPreferences preferences;
    private OperationsApiClient client;
    private String clientProfileKey = "";
    private OperationsRelayApiClient relayClient;
    private String relayClientProfileKey = "";
    private boolean monitoring;
    private boolean checkInFlight;
    private boolean checkAgainAfterCurrent;
    private int checkGeneration;
    private int consecutiveFailures;
    private long firstFailureAtElapsedMilliseconds;
    private boolean offlineConfirmed;
    private boolean hasCompletedCheck;
    private String lastAttentionKey = "";
    private OperationsMonitorEvidenceRevision.Evidence lastEvidence =
            OperationsMonitorEvidenceRevision.Evidence.EMPTY;
    private final Map<String, SecondaryFailure> secondaryFailures = new HashMap<>();
    private ConnectivityManager connectivityManager;
    private ConnectivityManager.NetworkCallback networkCallback;
    private boolean networkCallbackRegistered;

    static void start(Context context) {
        AppPreferences preferences = new AppPreferences(context);
        if (!OperationsWatchPreferencePolicy.shouldRun(
                preferences.hasOperationsProfile(),
                preferences.isOperationsWatchUserEnabled())) {
            return;
        }
        Intent intent = new Intent(context, OperationsWatchService.class).setAction(ACTION_START);
        ContextCompat.startForegroundService(context, intent);
    }

    static void stopForProfileRemoval(Context context, String hostId) {
        context.stopService(new Intent(context, OperationsWatchService.class));
        NotificationManager manager = context.getSystemService(NotificationManager.class);
        if (manager != null) {
            manager.cancel(LEGACY_ATTENTION_NOTIFICATION_ID);
            manager.cancel(
                    OperationsBackgroundFleetPolicy.attentionNotificationTag(hostId),
                    ATTENTION_NOTIFICATION_ID);
        }
    }

    static void stopForUserPreference(Context context) {
        stopAndClearNotifications(context);
    }

    private static void stopAndClearNotifications(Context context) {
        context.stopService(new Intent(context, OperationsWatchService.class));
        NotificationManager manager = context.getSystemService(NotificationManager.class);
        if (manager != null) {
            manager.cancel(LEGACY_ATTENTION_NOTIFICATION_ID);
            for (OperationsProfileRegistry.Profile profile
                    : new AppPreferences(context).getOperationsProfiles()) {
                manager.cancel(
                        OperationsBackgroundFleetPolicy.attentionNotificationTag(profile.hostId),
                        ATTENTION_NOTIFICATION_ID);
            }
        }
    }

    static boolean postReminderTest(Context context) {
        if (!NotificationPermissionPolicy.canPostAttention(
                Build.VERSION.SDK_INT,
                NotificationPermissionState.hasRuntimePermission(context),
                NotificationPermissionState.appNotificationsEnabled(context),
                NotificationPermissionState.attentionChannelEnabled(context))) {
            return false;
        }
        createNotificationChannels(context);
        if (!NotificationPermissionState.attentionChannelEnabled(context)) {
            return false;
        }
        Context applicationContext = context.getApplicationContext();
        Intent openIntent = new Intent(applicationContext, MainActivity.class)
                .addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP)
                .putExtra(MainActivity.EXTRA_START_TAB, MainActivity.TAB_SETTINGS);
        PendingIntent contentIntent = PendingIntent.getActivity(
                applicationContext,
                2,
                openIntent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        Notification notification = new NotificationCompat.Builder(
                applicationContext, ATTENTION_CHANNEL_ID)
                .setSmallIcon(R.drawable.ic_devices_24)
                .setContentTitle(applicationContext.getString(
                        R.string.operations_reminder_test_notification_title))
                .setContentText(applicationContext.getString(
                        R.string.operations_reminder_test_notification_body))
                .setContentIntent(contentIntent)
                .setCategory(NotificationCompat.CATEGORY_STATUS)
                .setVisibility(NotificationCompat.VISIBILITY_PRIVATE)
                .setAutoCancel(true)
                .setOnlyAlertOnce(false)
                .setPriority(NotificationCompat.PRIORITY_DEFAULT)
                .build();
        NotificationManager manager = applicationContext.getSystemService(
                NotificationManager.class);
        if (manager == null) {
            return false;
        }
        try {
            manager.notify(REMINDER_TEST_NOTIFICATION_ID, notification);
            return true;
        } catch (SecurityException ex) {
            Log.w(LOG_TAG, "operations_reminder_test_denied", ex);
            return false;
        }
    }

    static void refreshConnectionPreference(Context context) {
        AppPreferences preferences = new AppPreferences(context);
        if (!OperationsWatchPreferencePolicy.shouldRun(
                preferences.hasOperationsProfile(),
                preferences.isOperationsWatchUserEnabled())) {
            return;
        }
        Intent intent = new Intent(context, OperationsWatchService.class)
                .setAction(ACTION_REFRESH_CONNECTION);
        ContextCompat.startForegroundService(context, intent);
    }

    static void restartForProfileChange(Context context) {
        context.stopService(new Intent(context, OperationsWatchService.class));
        start(context);
    }

    @Override
    public void onCreate() {
        super.onCreate();
        preferences = new AppPreferences(this);
        String persistedState = preferences.getOperationsWatchState();
        hasCompletedCheck = !persistedState.isEmpty();
        offlineConfirmed = OperationsWatchHistory.STATE_OFFLINE.equals(persistedState);
        lastAttentionKey = OperationsWatchHistory.attentionKey(persistedState);
        lastEvidence = preferences.getOperationsWatchEvidence(
                preferences.getOperationsHostId(), lastAttentionKey);
        createNotificationChannels(this);
        registerNetworkCallback();
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (!OperationsWatchPreferencePolicy.shouldRun(
                preferences.hasOperationsProfile(),
                preferences.isOperationsWatchUserEnabled())) {
            stopMonitoring(true);
            return START_NOT_STICKY;
        }

        boolean refreshRequested = intent != null
                && ACTION_REFRESH_CONNECTION.equals(intent.getAction());
        if (!monitoring) {
            monitoring = true;
            startInForeground("正在连接已配对主机…");
            handler.post(scheduledCheck);
        } else if (refreshRequested) {
            requestImmediateCheck();
        }
        return START_STICKY;
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onDestroy() {
        monitoring = false;
        checkGeneration++;
        handler.removeCallbacks(scheduledCheck);
        unregisterNetworkCallback();
        executor.shutdownNow();
        super.onDestroy();
    }

    private void registerNetworkCallback() {
        connectivityManager = getSystemService(ConnectivityManager.class);
        if (connectivityManager == null) {
            return;
        }
        networkCallback = new ConnectivityManager.NetworkCallback() {
            @Override
            public void onAvailable(Network network) {
                requestImmediateCheckAfterNetworkChange();
            }

            @Override
            public void onLost(Network network) {
                requestImmediateCheckAfterNetworkChange();
            }
        };
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
                connectivityManager.registerDefaultNetworkCallback(networkCallback);
            } else {
                NetworkRequest request = new NetworkRequest.Builder()
                        .addCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
                        .build();
                connectivityManager.registerNetworkCallback(request, networkCallback);
            }
            networkCallbackRegistered = true;
            Log.i(LOG_TAG, "operations_watch_network_callback_registered");
        } catch (RuntimeException ignored) {
            networkCallback = null;
            Log.w(LOG_TAG, "operations_watch_network_callback_unavailable");
        }
    }

    private void unregisterNetworkCallback() {
        if (!networkCallbackRegistered || connectivityManager == null || networkCallback == null) {
            return;
        }
        try {
            connectivityManager.unregisterNetworkCallback(networkCallback);
        } catch (RuntimeException ignored) {
        }
        networkCallbackRegistered = false;
        networkCallback = null;
    }

    private void requestImmediateCheckAfterNetworkChange() {
        handler.post(this::requestImmediateCheck);
    }

    private void requestImmediateCheck() {
        if (!monitoring) {
            return;
        }
        handler.removeCallbacks(scheduledCheck);
        if (checkInFlight) {
            checkAgainAfterCurrent = true;
            return;
        }
        handler.post(scheduledCheck);
    }

    private void runCheck() {
        if (!monitoring || checkInFlight) {
            return;
        }
        if (!preferences.hasOperationsProfile()) {
            stopMonitoring(true);
            return;
        }

        checkInFlight = true;
        int generation = ++checkGeneration;
        String hostId = preferences.getOperationsHostId();
        executor.execute(() -> {
            if (OperationsConnectionPreference.prefersRelay(
                    preferences.getOperationsConnectionPreference())) {
                runRelayPreferredCheck(generation, hostId);
            } else {
                runDirectPreferredCheck(generation, hostId);
            }
        });
    }

    private void runDirectPreferredCheck(int generation, String hostId) {
        String localCode;
        try {
            LocalCheck check = readLocalCheck();
            postCheckCompletion(generation, hostId,
                    () -> completeSuccessfulCheck(
                            check.status, check.attentionKey, check.evidence));
            runSecondaryProfileCheck(hostId);
            return;
        } catch (Exception localException) {
            localCode = errorCode(localException);
            if (isRevoked(localCode)) {
                postCheckCompletion(generation, hostId, () -> completeFailedCheck(localCode));
                return;
            }
        }

        try {
            RelayCheck check = readRelayCheck();
            postCheckCompletion(generation, hostId, () -> completeRemoteCheck(check));
            runSecondaryProfileCheck(hostId);
        } catch (Exception relayException) {
            String relayCode = errorCode(relayException);
            Log.w(LOG_TAG, "operations_watch_relay_unavailable code=" + relayCode);
            postCheckCompletion(generation, hostId,
                    () -> completeFailedCheck(isRevoked(relayCode) ? relayCode : localCode));
        }
    }

    private void runRelayPreferredCheck(int generation, String hostId) {
        String relayCode;
        try {
            RelayCheck check = readRelayCheck();
            postCheckCompletion(generation, hostId, () -> completeRemoteCheck(check));
            runSecondaryProfileCheck(hostId);
            return;
        } catch (Exception relayException) {
            relayCode = errorCode(relayException);
            Log.w(LOG_TAG, "operations_watch_relay_unavailable code=" + relayCode);
            if (isRevoked(relayCode)) {
                postCheckCompletion(generation, hostId, () -> completeFailedCheck(relayCode));
                return;
            }
        }

        try {
            LocalCheck check = readLocalCheck();
            postCheckCompletion(generation, hostId,
                    () -> completeSuccessfulCheck(
                            check.status, check.attentionKey, check.evidence));
            runSecondaryProfileCheck(hostId);
        } catch (Exception localException) {
            String localCode = errorCode(localException);
            postCheckCompletion(generation, hostId,
                    () -> completeFailedCheck(isRevoked(localCode) ? localCode : relayCode));
        }
    }

    private void postCheckCompletion(int generation, String hostId, Runnable completion) {
        handler.post(() -> {
            if (!monitoring) {
                return;
            }
            if (!OperationsWatchPolicy.isCurrentProfileCheck(
                    hostId, preferences.getOperationsHostId(), generation, checkGeneration)) {
                checkInFlight = false;
                checkAgainAfterCurrent = false;
                client = null;
                clientProfileKey = "";
                relayClient = null;
                relayClientProfileKey = "";
                handler.removeCallbacks(scheduledCheck);
                handler.post(scheduledCheck);
                return;
            }
            completion.run();
        });
    }

    private LocalCheck readLocalCheck() throws Exception {
        JSONObject response = getClient().get("/ops/v1/monitor");
        JSONObject snapshot = response.optJSONObject("data");
        if (snapshot == null) {
            throw new IllegalStateException("incomplete_live_monitor_response");
        }
        String attentionKey = OperationsMonitorClassifier.attentionKey(snapshot);
        return new LocalCheck(
                OperationsMonitorClassifier.status(snapshot),
                attentionKey,
                OperationsMonitorClassifier.evidence(snapshot, attentionKey));
    }

    private RelayCheck readRelayCheck() throws Exception {
        JSONObject response = getRelayClient().getSnapshot();
        JSONObject host = response.optJSONObject("host");
        if (host == null) {
            throw new IllegalStateException("incomplete_relay_snapshot");
        }
        boolean hostFresh = OperationsRelayPolicy.isHostFresh(
                host.optLong("signedAt", 0L),
                System.currentTimeMillis());
        JSONObject snapshot = host.optJSONObject("snapshot");
        JSONObject monitor = snapshot == null ? null : snapshot.optJSONObject("monitor");
        if (!hostFresh || monitor == null) {
            return new RelayCheck(
                    hostFresh,
                    "",
                    "",
                    OperationsMonitorEvidenceRevision.Evidence.EMPTY);
        }
        String attentionKey = OperationsMonitorClassifier.attentionKey(monitor);
        return new RelayCheck(
                true,
                OperationsMonitorClassifier.status(monitor),
                attentionKey,
                OperationsMonitorClassifier.evidence(monitor, attentionKey));
    }

    private void runSecondaryProfileCheck(String activeHostId) {
        long nowMilliseconds = System.currentTimeMillis();
        OperationsProfileRegistry.Profile profile =
                OperationsBackgroundFleetPolicy.selectSecondaryProfile(
                        preferences.getOperationsProfiles(), activeHostId, nowMilliseconds);
        if (profile == null) {
            return;
        }
        OperationsProfileMonitorProbe.Result result = OperationsProfileMonitorProbe.check(
                profile,
                preferences.getOrCreateDeviceId(),
                FLEET_CONNECT_TIMEOUT_MILLISECONDS,
                FLEET_READ_TIMEOUT_MILLISECONDS,
                nowMilliseconds);
        handler.post(() -> completeSecondaryProfileCheck(profile.hostId, result));
    }

    private void completeSecondaryProfileCheck(
            String hostId, OperationsProfileMonitorProbe.Result result) {
        if (!monitoring) {
            return;
        }
        OperationsProfileRegistry.Profile profile = findUsableProfile(hostId);
        if (profile == null) {
            secondaryFailures.remove(hostId);
            clearAttentionNotification(hostId);
            return;
        }
        long nowMilliseconds = System.currentTimeMillis();
        String previousState = OperationsBackgroundFleetPolicy.latestState(
                profile.watchHistory, nowMilliseconds);
        if (result.revoked) {
            secondaryFailures.remove(hostId);
            clearRemoteWindowSnapshotSecrets(hostId);
            preferences.saveOperationsWatchEvidence(
                    hostId, "", OperationsMonitorEvidenceRevision.Evidence.EMPTY);
            preferences.recordOperationsProfileWatchState(
                    hostId, OperationsWatchHistory.STATE_REVOKED, nowMilliseconds);
            preferences.markOperationsProfileRevoked(hostId);
            postAttentionNotification(
                    hostId,
                    preferences.getOperationsProfileLabel(hostId),
                    OperationsWatchPolicy.ATTENTION_REVOKED,
                    false);
            Log.w(LOG_TAG, "operations_watch_secondary_revoked");
            return;
        }
        if (!result.reachable) {
            completeSecondaryProfileFailure(profile, previousState, nowMilliseconds);
            return;
        }

        secondaryFailures.remove(hostId);
        preferences.recordOperationsProfileWatchState(hostId, result.state, nowMilliseconds);
        String previousAttentionKey = OperationsWatchHistory.attentionKey(previousState);
        OperationsMonitorEvidenceRevision.Evidence previousEvidence =
                preferences.getOperationsWatchEvidence(
                hostId, previousAttentionKey);
        boolean newEvidence = OperationsWatchPolicy.isEvidenceUpdate(
                result.attentionKey,
                previousAttentionKey,
                result.evidence,
                previousEvidence);
        if (OperationsWatchPolicy.shouldPostAttention(
                result.attentionKey,
                previousAttentionKey,
                result.evidence,
                previousEvidence)) {
            postAttentionNotification(
                    hostId,
                    preferences.getOperationsProfileLabel(hostId),
                    result.attentionKey,
                    newEvidence);
        } else if (result.attentionKey.isEmpty()) {
            clearAttentionNotification(hostId);
        }
        preferences.saveOperationsWatchEvidence(
                hostId, result.attentionKey, result.evidence);
        Log.i(LOG_TAG, "operations_watch_secondary_checked");
    }

    private void completeSecondaryProfileFailure(
            OperationsProfileRegistry.Profile profile,
            String previousState,
            long nowMilliseconds) {
        long nowElapsedMilliseconds = SystemClock.elapsedRealtime();
        SecondaryFailure failure = secondaryFailures.get(profile.hostId);
        if (failure == null) {
            failure = new SecondaryFailure(nowElapsedMilliseconds);
            secondaryFailures.put(profile.hostId, failure);
        } else {
            failure.consecutiveFailures++;
        }
        if (!OperationsWatchPolicy.shouldConfirmOffline(
                failure.consecutiveFailures,
                failure.firstFailureAtElapsedMilliseconds,
                nowElapsedMilliseconds)) {
            Log.w(LOG_TAG, "operations_watch_secondary_fluctuation");
            return;
        }

        secondaryFailures.remove(profile.hostId);
        boolean offlineJustConfirmed = !OperationsWatchHistory.STATE_OFFLINE.equals(previousState);
        preferences.saveOperationsWatchEvidence(
                profile.hostId,
                "",
                OperationsMonitorEvidenceRevision.Evidence.EMPTY);
        preferences.recordOperationsProfileWatchState(
                profile.hostId, OperationsWatchHistory.STATE_OFFLINE, nowMilliseconds);
        if (OperationsWatchPolicy.shouldPostOffline(
                OperationsWatchHistory.isOnlineState(previousState),
                offlineJustConfirmed,
                OperationsWatchHistory.attentionKey(previousState))) {
            postAttentionNotification(
                    profile.hostId,
                    preferences.getOperationsProfileLabel(profile.hostId),
                    OperationsWatchPolicy.ATTENTION_OFFLINE,
                    false);
        }
        Log.w(LOG_TAG, "operations_watch_secondary_offline");
    }

    private OperationsProfileRegistry.Profile findUsableProfile(String hostId) {
        for (OperationsProfileRegistry.Profile profile : preferences.getOperationsProfiles()) {
            if (profile.hostId.equals(hostId) && !profile.revoked) {
                return profile;
            }
        }
        return null;
    }

    private static final class SecondaryFailure {
        final long firstFailureAtElapsedMilliseconds;
        int consecutiveFailures = 1;

        SecondaryFailure(long firstFailureAtElapsedMilliseconds) {
            this.firstFailureAtElapsedMilliseconds = firstFailureAtElapsedMilliseconds;
        }
    }

    private static boolean isRevoked(String code) {
        return !OperationsConnectionPreference.canFallbackAfter(code);
    }

    private static final class LocalCheck {
        final String status;
        final String attentionKey;
        final OperationsMonitorEvidenceRevision.Evidence evidence;

        LocalCheck(
                String status,
                String attentionKey,
                OperationsMonitorEvidenceRevision.Evidence evidence) {
            this.status = status;
            this.attentionKey = attentionKey;
            this.evidence = evidence;
        }
    }

    private static final class RelayCheck {
        final boolean hostFresh;
        final String status;
        final String attentionKey;
        final OperationsMonitorEvidenceRevision.Evidence evidence;

        RelayCheck(
                boolean hostFresh,
                String status,
                String attentionKey,
                OperationsMonitorEvidenceRevision.Evidence evidence) {
            this.hostFresh = hostFresh;
            this.status = status;
            this.attentionKey = attentionKey;
            this.evidence = evidence;
        }
    }

    private OperationsApiClient getClient() throws Exception {
        String profileKey = preferences.getOperationsEndpoint()
                + "\n" + preferences.getOperationsCertificatePin()
                + "\n" + preferences.getOperationsHostId();
        if (client == null || !profileKey.equals(clientProfileKey)) {
            OperationsDeviceIdentity identity =
                    new OperationsDeviceIdentity(preferences.getOperationsHostId());
            client = new OperationsApiClient(
                    preferences.getOperationsEndpoint(),
                    preferences.getOperationsCertificatePin(),
                    preferences.getOrCreateDeviceId(),
                    identity);
            clientProfileKey = profileKey;
        }
        return client;
    }

    private OperationsRelayApiClient getRelayClient() throws Exception {
        String profileKey = preferences.getOperationsHostId()
                + "\n" + preferences.getOrCreateDeviceId();
        if (relayClient == null || !profileKey.equals(relayClientProfileKey)) {
            OperationsDeviceIdentity identity =
                    new OperationsDeviceIdentity(preferences.getOperationsHostId());
            relayClient = new OperationsRelayApiClient(
                    preferences.getOperationsHostId(),
                    preferences.getOrCreateDeviceId(),
                    preferences.getOperationsCertificatePin(),
                    identity);
            relayClientProfileKey = profileKey;
        }
        return relayClient;
    }

    private void completeSuccessfulCheck(
            String status,
            String attentionKey,
            OperationsMonitorEvidenceRevision.Evidence evidence) {
        if (!monitoring) {
            return;
        }
        checkInFlight = false;
        boolean reconnected = offlineConfirmed;
        boolean firstOrRecoveredCheck = !hasCompletedCheck
                || consecutiveFailures > 0
                || offlineConfirmed;
        hasCompletedCheck = true;
        consecutiveFailures = 0;
        firstFailureAtElapsedMilliseconds = 0L;
        offlineConfirmed = false;
        if (firstOrRecoveredCheck) {
            Log.i(LOG_TAG, "operations_watch_online");
        }
        String hostId = preferences.getOperationsHostId();
        preferences.recordOperationsProfileWatchState(
                hostId,
                attentionKey.isEmpty()
                        ? OperationsWatchHistory.STATE_ONLINE
                        : OperationsWatchHistory.attentionState(attentionKey),
                System.currentTimeMillis());
        updateNotification(OperationsWatchPolicy.successfulCheckNotification(status, reconnected), true);
        boolean newEvidence = OperationsWatchPolicy.isEvidenceUpdate(
                attentionKey, lastAttentionKey, evidence, lastEvidence);
        if (OperationsWatchPolicy.shouldPostAttention(
                attentionKey, lastAttentionKey, evidence, lastEvidence)) {
            postAttentionNotification(
                    hostId,
                    preferences.getActiveOperationsProfileLabel(),
                    attentionKey,
                    newEvidence);
        } else if (attentionKey.isEmpty()) {
            clearAttentionNotification(hostId);
        }
        preferences.saveOperationsWatchEvidence(hostId, attentionKey, evidence);
        lastAttentionKey = attentionKey;
        lastEvidence = attentionKey.isEmpty()
                ? OperationsMonitorEvidenceRevision.Evidence.EMPTY : evidence;
        scheduleNext(OperationsWatchPolicy.HEALTHY_CHECK_MILLISECONDS);
    }

    private void completeRemoteCheck(RelayCheck check) {
        if (!monitoring) {
            return;
        }
        checkInFlight = false;
        boolean reconnected = offlineConfirmed;
        hasCompletedCheck = true;
        consecutiveFailures = 0;
        firstFailureAtElapsedMilliseconds = 0L;
        offlineConfirmed = false;
        String attentionKey = check.hostFresh ? check.attentionKey : "";
        OperationsMonitorEvidenceRevision.Evidence evidence = check.hostFresh
                ? check.evidence : OperationsMonitorEvidenceRevision.Evidence.EMPTY;
        String relayState = !attentionKey.isEmpty()
                ? OperationsWatchHistory.attentionState(attentionKey)
                : check.hostFresh
                        ? OperationsWatchHistory.STATE_REMOTE_ONLINE
                        : OperationsWatchHistory.STATE_REMOTE_WAITING;
        String hostId = preferences.getOperationsHostId();
        preferences.recordOperationsProfileWatchState(
                hostId, relayState, System.currentTimeMillis());
        String status = check.hostFresh
                ? check.status.isEmpty()
                        ? "固定中继在线 · 电脑已连接"
                        : "固定中继 · " + check.status
                : "远程中继在线 · 等待电脑上线";
        updateNotification((reconnected ? "连接已恢复 · " : "") + status + " · 刚刚检查", true);
        boolean newEvidence = OperationsWatchPolicy.isEvidenceUpdate(
                attentionKey, lastAttentionKey, evidence, lastEvidence);
        if (OperationsWatchPolicy.shouldPostAttention(
                attentionKey, lastAttentionKey, evidence, lastEvidence)) {
            postAttentionNotification(
                    hostId,
                    preferences.getActiveOperationsProfileLabel(),
                    attentionKey,
                    newEvidence);
        } else if (attentionKey.isEmpty()) {
            clearAttentionNotification(hostId);
        }
        preferences.saveOperationsWatchEvidence(hostId, attentionKey, evidence);
        lastAttentionKey = attentionKey;
        lastEvidence = attentionKey.isEmpty()
                ? OperationsMonitorEvidenceRevision.Evidence.EMPTY : evidence;
        Log.i(LOG_TAG, check.hostFresh
                ? "operations_watch_remote_online"
                : "operations_watch_remote_waiting");
        scheduleNext(OperationsWatchPolicy.HEALTHY_CHECK_MILLISECONDS);
    }

    private void completeFailedCheck(String code) {
        if (!monitoring) {
            return;
        }
        checkInFlight = false;
        long nowMilliseconds = System.currentTimeMillis();
        long nowElapsedMilliseconds = SystemClock.elapsedRealtime();
        hasCompletedCheck = true;
        if (code.contains("unknown_or_revoked_device")) {
            String revokedHostId = preferences.getOperationsHostId();
            String revokedLabel = preferences.getActiveOperationsProfileLabel();
            clearRemoteWindowSnapshotSecrets();
            preferences.saveOperationsWatchEvidence(
                    revokedHostId,
                    "",
                    OperationsMonitorEvidenceRevision.Evidence.EMPTY);
            preferences.recordOperationsProfileWatchState(
                    revokedHostId, OperationsWatchHistory.STATE_REVOKED,
                    nowMilliseconds);
            preferences.markOperationsProfileRevoked(revokedHostId);
            Log.w(LOG_TAG, "operations_watch_pairing_revoked");
            clearAttentionNotification(revokedHostId);
            if (preferences.hasOperationsProfile()) {
                continueWithSelectedProfileAfterRevocation();
                return;
            }
            updateNotificationForTarget(
                    "配对授权已失效 · 请打开应用处理", false, revokedLabel);
            detachNotificationAndStop();
            return;
        }

        if (consecutiveFailures == 0) {
            firstFailureAtElapsedMilliseconds = nowElapsedMilliseconds;
        }
        consecutiveFailures++;
        long retryDelay = OperationsWatchPolicy.retryDelayMilliseconds(consecutiveFailures);
        boolean confirmOffline = offlineConfirmed
                || OperationsWatchPolicy.shouldConfirmOffline(
                consecutiveFailures,
                firstFailureAtElapsedMilliseconds,
                nowElapsedMilliseconds);
        boolean offlineJustConfirmed = confirmOffline && !offlineConfirmed;
        boolean previousStateOnline = OperationsWatchHistory.isOnlineState(
                preferences.getOperationsWatchState());
        boolean notifyOffline = OperationsWatchPolicy.shouldPostOffline(
                previousStateOnline, offlineJustConfirmed, lastAttentionKey);
        if (consecutiveFailures == 1 || offlineJustConfirmed) {
            Log.w(LOG_TAG, "operations_watch_offline retry_seconds=" + (retryDelay / 1000L));
        }
        if (confirmOffline) {
            preferences.recordOperationsProfileWatchState(
                    preferences.getOperationsHostId(), OperationsWatchHistory.STATE_OFFLINE,
                    nowMilliseconds);
            preferences.saveOperationsWatchEvidence(
                    preferences.getOperationsHostId(),
                    "",
                    OperationsMonitorEvidenceRevision.Evidence.EMPTY);
            offlineConfirmed = true;
        }
        updateNotification((confirmOffline ? "连接中断" : "连接波动 · 正在确认")
                + " · " + (retryDelay / 1000L) + " 秒后重试", true);
        if (notifyOffline) {
            postAttentionNotification(
                    preferences.getOperationsHostId(),
                    preferences.getActiveOperationsProfileLabel(),
                    OperationsWatchPolicy.ATTENTION_OFFLINE,
                    false);
        }
        if (confirmOffline) {
            lastAttentionKey = OperationsWatchPolicy.ATTENTION_OFFLINE;
            lastEvidence = OperationsMonitorEvidenceRevision.Evidence.EMPTY;
        }
        client = null;
        clientProfileKey = "";
        relayClient = null;
        relayClientProfileKey = "";
        scheduleNext(retryDelay);
    }

    private void clearRemoteWindowSnapshotSecrets() {
        clearRemoteWindowSnapshotSecrets(preferences.getOperationsHostId());
    }

    private void clearRemoteWindowSnapshotSecrets(String hostId) {
        if (OperationsRelayPolicy.isSafeIdentifier(hostId)) {
            try {
                new OperationsE2eIdentity(hostId).delete();
            } catch (Exception ignored) {
            }
        }
        File directory = new File(getCacheDir(), "diagnostic-share");
        File[] files = directory.listFiles((parent, name) ->
                name.startsWith("ColorVision-remote-window-snapshot-")
                        && name.endsWith(".jpg"));
        if (files != null) {
            for (File file : files) {
                file.delete();
            }
        }
    }

    private static String errorCode(Exception exception) {
        return exception.getMessage() == null ? "" : exception.getMessage();
    }

    private void scheduleNext(long delayMilliseconds) {
        handler.removeCallbacks(scheduledCheck);
        if (!monitoring) {
            return;
        }
        if (checkAgainAfterCurrent) {
            checkAgainAfterCurrent = false;
            handler.post(scheduledCheck);
        } else {
            handler.postDelayed(scheduledCheck, delayMilliseconds);
        }
    }

    private void startInForeground(String status) {
        int serviceType = Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q
                ? ServiceInfo.FOREGROUND_SERVICE_TYPE_CONNECTED_DEVICE
                : 0;
        ServiceCompat.startForeground(
                this,
                NOTIFICATION_ID,
                buildNotification(status, true),
                serviceType);
    }

    private void updateNotification(String status, boolean ongoing) {
        NotificationManager manager = getSystemService(NotificationManager.class);
        if (manager != null) {
            manager.notify(NOTIFICATION_ID, buildNotification(status, ongoing));
        }
    }

    private void updateNotificationForTarget(
            String status, boolean ongoing, String targetLabel) {
        NotificationManager manager = getSystemService(NotificationManager.class);
        if (manager != null) {
            manager.notify(NOTIFICATION_ID,
                    buildNotification(status, ongoing, targetLabel));
        }
    }

    private Notification buildNotification(String status, boolean ongoing) {
        return buildNotification(
                status, ongoing, preferences.getActiveOperationsProfileLabel());
    }

    private Notification buildNotification(
            String status, boolean ongoing, String targetLabel) {
        NotificationCompat.Builder builder = new NotificationCompat.Builder(this, NOTIFICATION_CHANNEL_ID)
                .setSmallIcon(R.drawable.ic_devices_24)
                .setContentTitle(OperationsTargetPolicy.watchNotificationTitle(
                        targetLabel, preferences.getUsableOperationsProfileCount()))
                .setContentText(status)
                .setContentIntent(createOperationsPendingIntent(0, ""))
                .setCategory(NotificationCompat.CATEGORY_SERVICE)
                .setVisibility(NotificationCompat.VISIBILITY_PRIVATE)
                .setOnlyAlertOnce(true)
                .setOngoing(ongoing)
                .setShowWhen(true)
                .setWhen(System.currentTimeMillis())
                .setPriority(NotificationCompat.PRIORITY_LOW);

        return builder.build();
    }

    private void continueWithSelectedProfileAfterRevocation() {
        checkGeneration++;
        checkAgainAfterCurrent = false;
        client = null;
        clientProfileKey = "";
        relayClient = null;
        relayClientProfileKey = "";
        consecutiveFailures = 0;
        firstFailureAtElapsedMilliseconds = 0L;
        String persistedState = preferences.getOperationsWatchState();
        hasCompletedCheck = !persistedState.isEmpty();
        offlineConfirmed = OperationsWatchHistory.STATE_OFFLINE.equals(persistedState);
        lastAttentionKey = OperationsWatchHistory.attentionKey(persistedState);
        lastEvidence = preferences.getOperationsWatchEvidence(
                preferences.getOperationsHostId(), lastAttentionKey);
        updateNotification("上一台电脑授权失效 · 正在连接当前电脑", true);
        handler.removeCallbacks(scheduledCheck);
        handler.post(scheduledCheck);
    }

    private void postAttentionNotification(
            String hostId,
            String targetLabel,
            String attentionKey,
            boolean newEvidence) {
        String message = OperationsWatchPolicy.attentionMessage(attentionKey, newEvidence);
        if (message.isEmpty()) {
            return;
        }
        Notification notification = new NotificationCompat.Builder(this, ATTENTION_CHANNEL_ID)
                .setSmallIcon(R.drawable.ic_devices_24)
                .setContentTitle(OperationsTargetPolicy.attentionNotificationTitle(
                        targetLabel))
                .setContentText(message)
                .setStyle(new NotificationCompat.BigTextStyle().bigText(message))
                .setContentIntent(createOperationsPendingIntent(
                        1, OperationsWatchPolicy.attentionDestination(attentionKey), hostId))
                .setCategory(NotificationCompat.CATEGORY_ERROR)
                .setVisibility(NotificationCompat.VISIBILITY_PRIVATE)
                .setAutoCancel(true)
                .setOnlyAlertOnce(false)
                .setPriority(NotificationCompat.PRIORITY_DEFAULT)
                .build();
        NotificationManager manager = getSystemService(NotificationManager.class);
        if (manager != null) {
            if (hostId.equals(preferences.getOperationsHostId())) {
                manager.cancel(LEGACY_ATTENTION_NOTIFICATION_ID);
            }
            manager.notify(
                    OperationsBackgroundFleetPolicy.attentionNotificationTag(hostId),
                    ATTENTION_NOTIFICATION_ID,
                    notification);
            Log.w(LOG_TAG, "operations_watch_attention state=" + attentionKey);
        }
    }

    private void clearAttentionNotification(String hostId) {
        NotificationManager manager = getSystemService(NotificationManager.class);
        if (manager != null) {
            if (hostId.equals(preferences.getOperationsHostId())) {
                manager.cancel(LEGACY_ATTENTION_NOTIFICATION_ID);
            }
            manager.cancel(
                    OperationsBackgroundFleetPolicy.attentionNotificationTag(hostId),
                    ATTENTION_NOTIFICATION_ID);
        }
    }

    private PendingIntent createOperationsPendingIntent(int requestCode, String destination) {
        return createOperationsPendingIntent(requestCode, destination, "");
    }

    private PendingIntent createOperationsPendingIntent(
            int requestCode, String destination, String hostId) {
        Intent openIntent = new Intent(this, OperationsActivity.class)
                .addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        if (OperationsRelayPolicy.isSafeIdentifier(hostId)) {
            openIntent.putExtra(OperationsActivity.EXTRA_SELECT_HOST_ID, hostId);
            openIntent.setData(Uri.parse(
                    "colorvision://operations/attention/" + Uri.encode(hostId)));
        }
        String safeDestination = OperationsWatchPolicy.normalizeDestination(destination);
        if (!safeDestination.isEmpty()) {
            openIntent.putExtra(OperationsActivity.EXTRA_OPEN_DESTINATION, safeDestination);
        }
        return PendingIntent.getActivity(
                this,
                requestCode,
                openIntent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    private static void createNotificationChannels(Context context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return;
        }
        NotificationChannel watchChannel = new NotificationChannel(
                NOTIFICATION_CHANNEL_ID,
                "ColorVision 运维守护",
                NotificationManager.IMPORTANCE_LOW);
        watchChannel.setDescription("显示已配对 ColorVision 主机的后台连接状态");
        watchChannel.setLockscreenVisibility(Notification.VISIBILITY_PRIVATE);
        NotificationChannel attentionChannel = new NotificationChannel(
                ATTENTION_CHANNEL_ID,
                "ColorVision 运维提醒",
                NotificationManager.IMPORTANCE_DEFAULT);
        attentionChannel.setDescription("各台电脑出现异常或同类新脱敏证据时分别提醒");
        attentionChannel.setLockscreenVisibility(Notification.VISIBILITY_PRIVATE);
        NotificationManager manager = context.getSystemService(NotificationManager.class);
        if (manager != null) {
            manager.createNotificationChannel(watchChannel);
            manager.createNotificationChannel(attentionChannel);
        }
    }

    private void stopMonitoring(boolean removeNotification) {
        monitoring = false;
        checkGeneration++;
        handler.removeCallbacks(scheduledCheck);
        stopForegroundCompat(removeNotification);
        stopSelf();
    }

    private void detachNotificationAndStop() {
        monitoring = false;
        checkGeneration++;
        handler.removeCallbacks(scheduledCheck);
        stopForegroundCompat(false);
        stopSelf();
    }

    @SuppressWarnings("deprecation")
    private void stopForegroundCompat(boolean removeNotification) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
            stopForeground(removeNotification ? STOP_FOREGROUND_REMOVE : STOP_FOREGROUND_DETACH);
        } else {
            stopForeground(removeNotification);
        }
    }
}
